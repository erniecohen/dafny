#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IntervalTree;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.Dafny.Compilers;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Dafny.LanguageServer.Language.Symbols;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using VC;
using VCGeneration;
using Token = Microsoft.Dafny.Token;

namespace DafnyDriver.Commands;

public record CanVerifyResult(ICanVerify CanVerify, IReadOnlyList<VerificationTaskResult> Results) {
  public ProofDependencyManager? ProofDependencies { get; init; }
}


public class CliCompilation {
  public Compilation Compilation { get; }
  private readonly ConcurrentDictionary<MessageSource, int> errorsPerSource = new();
  private int errorCount;
  private int warningCount;
  public bool DidVerification { get; private set; }
  private bool batchInputsCaptured;

  // The module whose verification the current asynchronous flow started, when
  // --verification-module-window is greater than 1; null otherwise.
  private readonly AsyncLocal<ModuleWindowSlot?> currentModuleSlot = new();
  private Action<ICompilationEvent>? reportEvent;

  /// <summary>The largest number of modules in verification at once, for tests.</summary>
  public int MaxModulesInFlight { get; private set; }
  /// <summary>The largest number of admitted modules not yet reported, for tests.</summary>
  public int MaxModulesUnreported { get; private set; }

  private CliCompilation(
    CreateCompilation createCompilation,
    DafnyOptions options) {
    Options = options;

    if (options.DafnyProject == null) {
      var firstFile = options.CliRootSourceUris.FirstOrDefault();
      var uri = firstFile ?? new Uri(Directory.GetCurrentDirectory());
      options.DafnyProject = new DafnyProject(null, uri, null, new HashSet<string>() { uri.LocalPath }, new HashSet<string>(),
        new Dictionary<string, object>()) {
        ImplicitFromCli = true
      };
    }

    options.RunningBoogieFromCommandLine = true;

    var input = new CompilationInput(options, 0, options.DafnyProject);
    var executionEngine = new ExecutionEngine(options, new EmptyVerificationResultCache(), DafnyMain.LargeThreadScheduler);
    Compilation = createCompilation(executionEngine, input);
  }

  public async Task<int> GetAndReportExitCode() {
    var value = await GetAndReportExitValue();
    return (int)value;
  }

  public async Task<ExitValue> GetAndReportExitValue() {
    if (errorCount > 0) {
      if (HasErrorsFromSource(MessageSource.Project)) {
        return ExitValue.PREPROCESSING_ERROR;
      }

      if (HasErrorsFromSource(MessageSource.Verifier)) {
        return ExitValue.VERIFICATION_ERROR;
      }
      return ExitValue.DAFNY_ERROR;
    }

    if (warningCount > 0 && !Options.Get(CommonOptionBag.AllowWarnings)) {
      await Options.OutputWriter.Status(
        "Compilation failed because warnings were found and --allow-warnings is false");
      return ExitValue.DAFNY_ERROR;
    }

    return ExitValue.SUCCESS;

    bool HasErrorsFromSource(MessageSource source) {
      return errorsPerSource.GetOrAdd(source, _ => 0) != 0;
    }
  }

  public Task<ResolutionResult?> Resolution => Compilation.Resolution;

  public static CliCompilation Create(DafnyOptions options, IProgramVerifier? programVerifier = null) {
    var fileSystem = OnDiskFileSystem.Instance;
    ILoggerFactory factory = new LoggerFactory();
    var telemetryPublisher = new CliTelemetryPublisher(factory.CreateLogger<TelemetryPublisherBase>());
    return new CliCompilation(CreateCompilation, options);

    Compilation CreateCompilation(ExecutionEngine engine, CompilationInput input) =>
      new(factory.CreateLogger<Compilation>(), fileSystem,
        new TextDocumentLoader(factory.CreateLogger<ITextDocumentLoader>(),
          new DafnyLangParser(options, fileSystem, telemetryPublisher,
            factory.CreateLogger<DafnyLangParser>(),
            factory.CreateLogger<CachingParser>()),
          new DafnyLangSymbolResolver(factory.CreateLogger<DafnyLangSymbolResolver>(), factory.CreateLogger<CachingResolver>(), telemetryPublisher)),
        programVerifier ?? new DafnyProgramVerifier(factory.CreateLogger<DafnyProgramVerifier>()), engine, input);
  }

  public void Start() {
    if (Compilation.Started) {
      throw new InvalidOperationException("Compilation was already started");
    }

    ErrorReporter consoleReporter = Options.DiagnosticsFormat switch {
      DafnyOptions.DiagnosticsFormats.PlainText => new ConsoleErrorReporter(Options),
      DafnyOptions.DiagnosticsFormats.JSON => new JsonConsoleErrorReporter(Options),
      _ => throw new ArgumentOutOfRangeException()
    };

    var internalExceptionsFound = 0;
    reportEvent = ev => {
      if (ev is NewDiagnostic newDiagnostic) {
        if (newDiagnostic.Diagnostic.Level == ErrorLevel.Error) {
          errorsPerSource.AddOrUpdate(newDiagnostic.Diagnostic.Source,
            _ => 1,
            (_, previous) => previous + 1);
          Interlocked.Increment(ref errorCount);
        }

        if (newDiagnostic.Diagnostic.Level == ErrorLevel.Warning) {
          Interlocked.Increment(ref warningCount);
        }

        consoleReporter.MessageCore(newDiagnostic.Diagnostic);
      } else if (ev is FinishedParsing finishedParsing) {
        if (errorCount > 0) {
          var programName = finishedParsing.ParseResult.Program.Name;
          _ = Options.OutputWriter.Status($"{errorCount} parse errors detected in {programName}");
        }
      } else if (ev is FinishedResolution finishedResolution) {
        DafnyMain.MaybePrintProgram(finishedResolution.Result.ResolvedProgram, Options.DafnyPrintResolvedFile, true);

        if (errorCount > 0) {
          var programName = finishedResolution.Result.ResolvedProgram.Name;
          _ = Options.OutputWriter.Status($"{errorCount} resolution/type errors detected in {programName}");
        }
      } else if (ev is InternalCompilationException internalCompilationException) {
        if (Interlocked.Increment(ref internalExceptionsFound) == 1) {
          _ = Options.OutputWriter.Status($"Encountered internal compilation exception: {internalCompilationException.Exception.Message}");
        }
      }

    };
    Compilation.Updates.Subscribe(ev => {
      // A module verified ahead of its turn reports its diagnostics when its turn comes.
      if (currentModuleSlot.Value is { } slot && slot.Defer(ev)) {
        return;
      }
      reportEvent(ev);
    });
    Compilation.Start();
  }

  public DafnyOptions Options { get; }

  public bool VerifiedAssertions { get; private set; }

  public async IAsyncEnumerable<CanVerifyResult> VerifyAllLazily(int? randomSeed = null,
    string? filterPosition = null, bool repeatVerification = false) {
    if (!Options.Get(CommonOptionBag.UnicodeCharacters) && Options.Backend is not CppBackend) {
      Compilation.Reporter.Deprecated(MessageSource.Verifier, "unicodeCharDeprecated", Token.Cli,
        "the option unicode-char has been deprecated.");
    }

    var canVerifyResults = new ConcurrentDictionary<ICanVerify, CliCanVerifyState>();
    using var subscription = Compilation.Updates.Subscribe(ev => {

      if (ev is CanVerifyPartsIdentified canVerifyPartsIdentified) {
        var canVerifyResult = canVerifyResults[canVerifyPartsIdentified.CanVerify];
        foreach (var part in canVerifyPartsIdentified.Parts.Where(canVerifyResult.TaskFilter)) {
          Interlocked.Increment(ref canVerifyResult.TaskCount);
        }

        if (canVerifyResult.CompletedParts.Count == canVerifyResult.TaskCount) {
          canVerifyResult.Finished.SetResult();
        }
      }
      if (ev is InternalCompilationException internalCompilationException) {
        var states = currentModuleSlot.Value is { } slot
          ? slot.States.Select(s => s.State)
          : canVerifyResults.Values;
        foreach (var state in states) {
          state.Finished.TrySetException(internalCompilationException.Exception);
        }
      }

      if (ev is BoogieException boogieException) {
        var canVerifyResult = canVerifyResults[boogieException.CanVerify];
        canVerifyResult.Finished.SetException(boogieException.Exception);
      }

      if (ev is BoogieUpdate { BoogieStatus: Completed completed } boogieUpdate) {
        var canVerifyResult = canVerifyResults[boogieUpdate.CanVerify];
        canVerifyResult.CompletedParts.Enqueue((boogieUpdate.VerificationTask, completed));
        var completedPartsCount = Interlocked.Increment(ref canVerifyResult.CompletedCount);

        if (Options.Get(CommonOptionBag.ProgressOption) == CommonOptionBag.ProgressLevel.Batch) {
          var partOrigin = boogieUpdate.VerificationTask.Split.Token;

          var wellFormedness = boogieUpdate.VerificationTask.Split.Implementation.Name.Contains("CheckWellFormed$");

          string OriginDescription(IImplementationPartOrigin origin, bool outer) {
            if (outer && origin is ImplementationRootOrigin) {
              return (wellFormedness ? "contract consistency" : "entire body");
            }
            var result = origin switch {
              PathOrigin pathOrigin => $"{OriginDescription(pathOrigin.Inner, false)}" +
                                       $"after executing lines {string.Join(", ", pathOrigin.BranchTokens.Select(b => b.line))}",
              RemainingAssertionsOrigin remainingAssertions => OriginDescription(remainingAssertions.Origin, false) + (outer ? "remaining assertions" : ""),
              IsolatedAssertionOrigin isolateOrigin => $"{OriginDescription(isolateOrigin.Origin, false)}assertion at line {isolateOrigin.line}",
              JumpOrigin returnOrigin => $"{OriginDescription(returnOrigin.Origin, false)}{JumpOriginKind(returnOrigin)} at line {returnOrigin.line}",
              AfterSplitOrigin splitOrigin => $"{OriginDescription(splitOrigin.Inner, false)}assertions after split_here at line {splitOrigin.line}",
              FocusOrigin focusOrigin =>
                $"{OriginDescription(focusOrigin.Inner, false)}with focus " +
                $"{string.Join(", ", focusOrigin.FocusChoices.Select(b => (b.DidFocus ? "+" : "-") + b.Token.line))}",
              UntilFirstSplitOrigin untilFirstSplit => $"{OriginDescription(untilFirstSplit.Inner, false)}assertions until first split",
              ImplementationRootOrigin => "",
              _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
            };
            if (!outer && !string.IsNullOrEmpty(result)) {
              result += ", ";
            }
            return result;
          }

          var runResult = completed.Result;
          var timeString = runResult.RunTime.ToString("g");
          _ = Options.OutputWriter.Status(
            $"Verified {completedPartsCount}/{canVerifyResult.TaskCount} of {boogieUpdate.CanVerify.FullDafnyName}: " +
            $"{OriginDescription(partOrigin, true)} - " +
            $"{DescribeOutcome(Compilation.GetOutcome(runResult.Outcome))}" +
            $" (time: {timeString}, resource count: {runResult.ResourceCount})");
        }
        if (completedPartsCount == canVerifyResult.TaskCount) {
          canVerifyResult.Finished.TrySetResult();
        }
      }
    });

    var profile = PipelineProfile.For(Options);
    var resolution = await Compilation.Resolution;
    if (resolution == null || resolution.HasErrors) {
      yield break;
    }

    if (resolution.CanVerifies == null) {
      yield break;
    }

    if (repeatVerification) {
      Compilation.CancellationToken.ThrowIfCancellationRequested();
      if (!batchInputsCaptured) {
        var preludePath = BoogieGenerator.PreludePath(Options);
        resolution.ResolvedProgram.PreludeText = await File.ReadAllTextAsync(preludePath, Compilation.CancellationToken);
        batchInputsCaptured = true;
      }
      // Earlier results retain their own manager; retranslations must not append
      // new dependencies to the definitions used by a previous request.
      resolution.ResolvedProgram.ProofDependencyManager = new ProofDependencyManager();
    }
    var requestDependencies = resolution.ResolvedProgram.ProofDependencyManager;

    DidVerification = true;

    var canVerifies = FilterCanVerifies(resolution.CanVerifies, out var filterRange, filterPosition);
    VerifiedAssertions = filterRange.Filters;

    int done = 0;

    var window = Options.Get(VerifyCommand.VerificationModuleWindow);
    if (window > 1) {
      await foreach (var result in VerifyModuleWindow((int)Math.Min(window, int.MaxValue), canVerifies, canVerifyResults,
                       filterRange, randomSeed, repeatVerification, requestDependencies)) {
        yield return result;
      }
      yield break;
    }

    var canVerifiesPerModule = canVerifies.GroupBy(c => c.ContainingModule);
    foreach (var canVerifiesForModule in canVerifiesPerModule.
               OrderBy(v => v.Key.Origin.pos)) {
      var moduleName = profile != null ? PipelineProfile.ModuleName(canVerifiesForModule.Key) : null;
      var moduleAdmitted = profile?.Now;
      var toAwait = new List<ICanVerify>();
      foreach (var canVerify in canVerifiesForModule.OrderBy(v => v.Origin.pos)) {
        var results = new CliCanVerifyState();
        canVerifyResults[canVerify] = results;
        if (VerifiedAssertions) {
          results.TaskFilter = t => KeepVerificationTask(t, filterRange);
        }

        if (repeatVerification) {
          Compilation.CancellationToken.ThrowIfCancellationRequested();
        }
        var shouldVerify = await Compilation.VerifyCanVerify(canVerify, results.TaskFilter, randomSeed);
        if (shouldVerify) {
          toAwait.Add(canVerify);
        }
      }
      var moduleScheduled = profile?.Now;

      foreach (var canVerify in toAwait) {
        var results = canVerifyResults[canVerify];
        try {
          if (Options.Get(CommonOptionBag.ProgressOption) > CommonOptionBag.ProgressLevel.None) {
            await Options.OutputWriter.Status(
              $"Verified {done}/{canVerifies.ToList().Count} symbols. Waiting for {canVerify.FullDafnyName} to verify.");
          }

          if (repeatVerification) {
            // Preparation may be cancelled before it identifies any tasks. Do not
            // wait for a completion event that the detached preparation cannot emit.
            await results.Finished.Task.WaitAsync(Compilation.CancellationToken);
          } else {
            await results.Finished.Task;
          }
          done++;
          profile?.Point("symbol", new Dictionary<string, object?> {
            ["module"] = moduleName,
            ["symbol"] = canVerify.FullDafnyName,
            ["tasks"] = results.TaskCount
          });
        } catch (ProverException e) {
          Compilation.Reporter.Error(MessageSource.Verifier, ResolutionErrors.ErrorId.none, canVerify.Origin, e.Message);
          if (repeatVerification) {
            throw; // Abort a batch before a later request can use an unhealthy solver session.
          }
          yield break;
        } catch (OperationCanceledException) {
          if (repeatVerification) {
            throw;
          }
        } catch (Exception e) {
          Compilation.Reporter.Error(MessageSource.Verifier, ResolutionErrors.ErrorId.none, canVerify.Origin,
            $"Internal error occurred during verification: {e.Message}\n{e.StackTrace}");
          throw;
        }

        yield return new CanVerifyResult(canVerify,
          results.CompletedParts.Select(c => new VerificationTaskResult(c.Task, c.Result.Result)).ToList()) {
          ProofDependencies = repeatVerification ? requestDependencies : null
        };

        canVerifyResults.TryRemove(canVerify, out _); // Free memory
        Compilation.ClearCanVerifyCache(canVerify, repeatVerification);
      }
      var moduleCompleted = profile?.Now;
      Compilation.ClearModuleCache(canVerifiesForModule.Key);
      // admitted: the CLI started this module's symbols; scheduled: all of them were handed to the
      // compilation; completed: the last of them finished; released: its cache was cleared and
      // the next module may start (the per-module barrier).
      profile?.Point("module", new Dictionary<string, object?> {
        ["module"] = moduleName,
        ["symbols"] = toAwait.Count,
        ["admitted"] = moduleAdmitted,
        ["scheduled"] = moduleScheduled,
        ["completed"] = moduleCompleted,
        ["released"] = profile.Now
      });
      profile?.HeapPoint("module", new Dictionary<string, object?> { ["module"] = moduleName });
    }
  }

  /// <summary>
  /// The loop of <see cref="VerifyAllLazily"/> with --verification-module-window K greater than 1.
  /// Up to K modules are in verification at once: a module is admitted when one in verification
  /// finishes, not only when the oldest is reported.  Admission follows the baseline module order,
  /// so translation, task submission (the verification tickets) and proof-dependency ids keep the
  /// order of the default loop.  Results are reported in that order too: each module's results,
  /// and the diagnostics and internal-exception reports its preparation raised, wait until every
  /// earlier module has been reported.  At most 2K modules are admitted and unreported, and a module is
  /// admitted only while the managed heap is below the byte budget, unless it would be the only
  /// unreported module.  A module's cache is cleared only after all its symbols have been reported.
  /// </summary>
  private async IAsyncEnumerable<CanVerifyResult> VerifyModuleWindow(int window,
    IReadOnlyList<ICanVerify> canVerifies,
    ConcurrentDictionary<ICanVerify, CliCanVerifyState> canVerifyResults,
    LineRange filterRange, int? randomSeed, bool repeatVerification,
    ProofDependencyManager requestDependencies) {
    var modules = canVerifies.GroupBy(c => c.ContainingModule).
      OrderBy(v => v.Key.Origin.pos).
      Select(g => new ModuleWindowSlot(g.Key, g.OrderBy(v => v.Origin.pos).ToList())).
      ToList();
    var symbolCount = canVerifies.Count;
    var memoryMiB = Options.Get(VerifyCommand.VerificationModuleWindowMemory);
    var byteBudget = memoryMiB > 0
      ? memoryMiB * 1024L * 1024L
      : GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2;
    var cancellationToken = Compilation.CancellationToken;
    var windowProfile = PipelineProfile.For(Options);
    var admitted = 0;
    var head = 0;
    var done = 0;

    bool CanAdmit() {
      if (admitted >= modules.Count) {
        return false;
      }
      var unreported = admitted - head;
      if (unreported == 0) {
        return true;
      }
      var inFlight = modules.Skip(head).Take(unreported).Count(m => !m.Completion.IsCompleted);
      return inFlight < window && unreported < 2L * window && GC.GetTotalMemory(false) < byteBudget;
    }

    async Task Admit(ModuleWindowSlot slot) {
      // Register every symbol's state before any of its events can arrive.
      foreach (var canVerify in slot.Symbols) {
        var results = new CliCanVerifyState();
        if (VerifiedAssertions) {
          results.TaskFilter = t => KeepVerificationTask(t, filterRange);
        }
        canVerifyResults[canVerify] = results;
        slot.States.Add((canVerify, results));
      }
      var toAwait = new List<Task>();
      currentModuleSlot.Value = slot;
      try {
        foreach (var (canVerify, results) in slot.States) {
          if (repeatVerification) {
            cancellationToken.ThrowIfCancellationRequested();
          }
          var shouldVerify = await Compilation.VerifyCanVerify(canVerify, results.TaskFilter, randomSeed);
          if (shouldVerify) {
            slot.ToAwait.Add(canVerify);
            toAwait.Add(results.Finished.Task);
          }
        }
      }
      finally {
        currentModuleSlot.Value = null;
      }
      slot.Completion = Task.WhenAll(toAwait).ContinueWith(t => _ = t.Exception, TaskContinuationOptions.ExecuteSynchronously);
      admitted++;
      MaxModulesInFlight = Math.Max(MaxModulesInFlight,
        modules.Skip(head).Take(admitted - head).Count(m => !m.Completion.IsCompleted));
      MaxModulesUnreported = Math.Max(MaxModulesUnreported, admitted - head);
    }

    // Wait for one symbol of the head module, admitting modules whenever there is room.
    async Task WaitFor(Task finished) {
      while (true) {
        while (CanAdmit()) {
          await Admit(modules[admitted]);
        }
        if (finished.IsCompleted) {
          break;
        }
        var wakeUps = modules.Skip(head + 1).Take(admitted - head - 1).
          Select(m => m.Completion).Where(c => !c.IsCompleted).ToList();
        if (admitted >= modules.Count || wakeUps.Count == 0) {
          break;
        }
        wakeUps.Add(finished);
        var any = Task.WhenAny(wakeUps);
        await (repeatVerification ? any.WaitAsync(cancellationToken) : any);
      }
      if (repeatVerification) {
        // Preparation may be cancelled before it identifies any tasks. Do not
        // wait for a completion event that the detached preparation cannot emit.
        await finished.WaitAsync(cancellationToken);
      } else {
        await finished;
      }
    }

    try {
      for (; head < modules.Count; head++) {
        if (head == admitted) {
          await Admit(modules[head]);
        }
        var slot = modules[head];
        slot.Release(reportEvent);
        foreach (var canVerify in slot.ToAwait) {
          var results = canVerifyResults[canVerify];
          try {
            if (Options.Get(CommonOptionBag.ProgressOption) > CommonOptionBag.ProgressLevel.None) {
              await Options.OutputWriter.Status(
                $"Verified {done}/{symbolCount} symbols. Waiting for {canVerify.FullDafnyName} to verify.");
            }

            await WaitFor(results.Finished.Task);
            done++;
          } catch (ProverException e) {
            Compilation.Reporter.Error(MessageSource.Verifier, ResolutionErrors.ErrorId.none, canVerify.Origin, e.Message);
            if (repeatVerification) {
              throw; // Abort a batch before a later request can use an unhealthy solver session.
            }
            yield break;
          } catch (OperationCanceledException) {
            if (repeatVerification) {
              throw;
            }
          } catch (Exception e) {
            Compilation.Reporter.Error(MessageSource.Verifier, ResolutionErrors.ErrorId.none, canVerify.Origin,
              $"Internal error occurred during verification: {e.Message}\n{e.StackTrace}");
            throw;
          }

          yield return new CanVerifyResult(canVerify,
            results.CompletedParts.Select(c => new VerificationTaskResult(c.Task, c.Result.Result)).ToList()) {
            ProofDependencies = repeatVerification ? requestDependencies : null
          };

          canVerifyResults.TryRemove(canVerify, out _); // Free memory
          Compilation.ClearCanVerifyCache(canVerify, repeatVerification);
        }
        Compilation.ClearModuleCache(slot.Module);
        // The live heap once this module is reported and released, with later modules in flight.
        windowProfile?.HeapPoint("module", new Dictionary<string, object?> {
          ["module"] = PipelineProfile.ModuleName(slot.Module),
          ["window"] = window
        });
      }
    }
    finally {
      // Stopped early, by an error or cancellation: modules admitted ahead of their turn are
      // abandoned, as the default loop never starts them.  Their deferred diagnostics are dropped.
      for (var index = head + 1; index < admitted; index++) {
        var slot = modules[index];
        slot.Abandon();
        foreach (var canVerify in slot.ToAwait) {
          Compilation.CancelCanVerify(canVerify);
          Compilation.ClearCanVerifyCache(canVerify, repeatVerification);
        }
        Compilation.ClearModuleCache(slot.Module);
      }
    }
  }

  private sealed class ModuleWindowSlot(ModuleDefinition module, IReadOnlyList<ICanVerify> symbols) {
    public ModuleDefinition Module { get; } = module;
    public IReadOnlyList<ICanVerify> Symbols { get; } = symbols;
    public List<(ICanVerify CanVerify, CliCanVerifyState State)> States { get; } = new();
    public List<ICanVerify> ToAwait { get; } = new();
    public Task Completion { get; set; } = Task.CompletedTask;
    private readonly List<ICompilationEvent> deferred = new();
    private bool released;
    private bool abandoned;

    /// <summary>Holds a reported event of this module until its turn; false once it is reported.</summary>
    public bool Defer(ICompilationEvent ev) {
      if (ev is not (NewDiagnostic or InternalCompilationException)) {
        return false;
      }
      lock (deferred) {
        if (released) {
          return false;
        }
        if (!abandoned) {
          deferred.Add(ev);
        }
        return true;
      }
    }

    public void Release(Action<ICompilationEvent>? report) {
      lock (deferred) {
        foreach (var ev in deferred) {
          report?.Invoke(ev);
        }
        deferred.Clear();
        released = true;
      }
    }

    public void Abandon() {
      lock (deferred) {
        deferred.Clear();
        abandoned = true;
      }
    }
  }

  private static string JumpOriginKind(JumpOrigin returnOrigin) {
    return returnOrigin.IsolatedReturn is GotoCmd ? "continue" : "return";
  }

  public static string DescribeOutcome(VcOutcome outcome) {
    return outcome switch {
      VcOutcome.Correct => "verified successfully",
      VcOutcome.Errors => "could not be verified",
      VcOutcome.Inconclusive => "was inconclusive",
      VcOutcome.TimedOut => "timed out",
      VcOutcome.OutOfResource => "ran out of resources",
      VcOutcome.OutOfMemory => "ran out of memory",
      VcOutcome.SolverException => "ran into a solver exception",
      _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };
  }

  class LineRange(int start, int end) {
    public int Start { get; } = start;
    public int End { get; } = end;

    public bool Contains(int value) {
      return Start <= value && value <= End;
    }
    public bool Filters => Start != int.MinValue || End != int.MaxValue;
  }

  private IReadOnlyList<ICanVerify> FilterCanVerifies(IDictionary<Uri, IIntervalTree<DafnyPosition, ICanVerify>> canVerifies, out LineRange range,
    string? filterPosition = null) {
    int start = int.MinValue;
    int end = int.MaxValue;
    filterPosition ??= Options.Get(VerifyCommand.FilterPosition);
    IEnumerable<ICanVerify> result;
    if (filterPosition == null) {
      range = new LineRange(start, end);
      result = canVerifies.SelectMany(x => x.Value.Values);
    } else {
      var regex = new Regex(@"(.*)(?::(\d*)(-?)(\d*))?", RegexOptions.RightToLeft);
      var regexResult = regex.Match(filterPosition);
      if (regexResult.Length != filterPosition.Length || !regexResult.Success) {
        Compilation.Reporter.Error(MessageSource.Project, Token.Cli, "Could not parse value passed to --filter-position");
        range = new LineRange(start, end);
        return [];
      }
      var filePart = regexResult.Groups[1].Value;
      string? lineStart = regexResult.Groups[2].Value;
      bool hasRange = regexResult.Groups[3].Value == "-";
      string lineEnd = regexResult.Groups[4].Value;
      var validFiles = canVerifies.Keys.Where(u => u.ToString().EndsWith(filePart));
      result = validFiles.SelectMany(k => canVerifies[k].Values);
      if (string.IsNullOrEmpty(lineStart) && string.IsNullOrEmpty(lineEnd)) {
        range = new LineRange(start, end);
      } else {
        if (!string.IsNullOrEmpty(lineEnd)) {
          end = int.Parse(lineEnd);
          if (!hasRange) {
            start = end;
          }
        }
        if (!string.IsNullOrEmpty(lineStart)) {
          start = int.Parse(lineStart);
        }

        range = new LineRange(start, end);
        result = result.Where(c => c.StartToken.line <= end && start <= c.EndToken.line);
      }
    }

    var symbolFilter = Options.Get(VerifyCommand.FilterSymbol);
    if (symbolFilter != null) {
      if (symbolFilter.EndsWith(".")) {
        var withoutDot = new string(symbolFilter.SkipLast(1).ToArray());
        result = result.Where(canVerify => canVerify.FullDafnyName.EndsWith(withoutDot));
      } else {
        result = result.Where(canVerify => canVerify.FullDafnyName.Contains(symbolFilter));
      }
    }

    return result.ToList();

  }

  private bool KeepVerificationTask(IVerificationTask task, LineRange range) {
    return range.Contains(task.ScopeToken.line) || range.Contains(task.Token.line);
  }
}

record VerificationStatistics {
  public int ErrorCount;
  public int VerifiedAssertions;
  public int VerifiedSymbols;
  public int InconclusiveCount;
  public int TimeoutCount;
  public int OutOfResourceCount;
  public int OutOfMemoryCount;
  public int SolverExceptionCount;
  public int TotalResourcesUsed;
  public int MaxVcResourcesUsed;
}
