#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DafnyCore;
using Serilog.Events;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

/// <summary>Backend selection and translation flags, independent of a native execution engine.</summary>
public record VerificationPreparationRequest(bool InsertChecksums,
  Func<IVerificationBackend<Bpl.Program>> CreateBackend, Action<Bpl.Program>? Capture = null);

/// <summary>A prepared module and its source/dependency map retain one reporting owner.</summary>
public record ModuleVerificationPreparation(VerificationPreparation Preparation, ProofDependencyManager ProofDependencies);

/// <summary>
/// Shared large-stack translation and backend preparation. The capture callback observes the
/// translator graph before backend mutation; it must copy anything it intends to retain.
/// The backend factory is delayed until after capture, preserving native cache/print ordering.
/// Translation requires no execution engine; only the selected backend owns task preparation.
///
/// With <c>--translation-workers N</c> greater than 1 and an eligible configuration (see
/// <see cref="TranslationWorkers"/>), up to N modules are translated and up to N prepared at once.
/// Every translation is already history-free: <see cref="BoogieGenerator.DoTranslation"/> runs
/// in its own <see cref="ModuleNamingScope"/>, so names, counters, proof-dependency ids and the
/// translation-time AST caches are private to it. What remains ordered is committed through a
/// cursor: each call takes a ticket when it arrives, synchronously, and its commit (the capture
/// callback, that is <c>--bprint</c>) runs only after every earlier ticket has committed or failed.
/// Translation diagnostics keep their module's order and are published by the caller in its
/// own order (the CLI's module window defers them per module). A translation is admitted only
/// while the managed heap plus the running mean of a translation's allocation, for each
/// translation in flight, is below the memory budget.
/// </summary>
public sealed class VerificationPreparationPipeline : IDisposable {
  private readonly SemaphoreSlim mutex = new(1);

  // Parallel path state, all guarded by gate.
  private readonly object gate = new();
  private Task lastCommit = Task.CompletedTask;
  private long arrivals;
  private int translating;
  private int preparing;
  private long translatedCount;
  private long translatedBytes;
  private TaskCompletionSource slotFreed = NewSignal();
  private bool bitvectorTypesInterned;

  private static readonly ConditionalWeakTable<DafnyOptions, StrongBox<long>> MemoryBudgets = new();

  private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

  /// <summary>
  /// The byte budget of translation admission for these options. The CLI sets it from
  /// <c>--verification-module-window-memory</c>; otherwise it is half of the memory available.
  /// </summary>
  public static void SetMemoryBudget(DafnyOptions options, long bytes) {
    MemoryBudgets.AddOrUpdate(options, new StrongBox<long>(bytes));
  }

  public static long MemoryBudget(DafnyOptions options) =>
    MemoryBudgets.TryGetValue(options, out var box) && box.Value > 0
      ? box.Value
      : GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2;

  /// <summary>
  /// The number of translation workers to use for this program: the value of
  /// <c>--translation-workers</c>, or 1 when the configuration has output that depends on
  /// process history or on the order of translations beyond what the commit cursor orders.
  /// </summary>
  public static int TranslationWorkers(Program program, out string? fallbackReason) {
    var options = program.Options;
    var requested = options.Get(CommonOptionBag.TranslationWorkers);
    fallbackReason = null;
    if (requested <= 1) {
      return 1;
    }
    fallbackReason = IneligibleReason(options);
    return fallbackReason == null ? requested : 1;
  }

  private static string? IneligibleReason(DafnyOptions options) {
    if (!options.Get(CommonOptionBag.TypeSystemRefresh)) {
      return "--type-system-refresh is off"; // rejected by the CLI; a safe default elsewhere
    }
    // T16: plugin rewriters run PreVerify at the start of each translation.
    var plugins = options.Plugins;
    if (plugins.Count != DafnyOptions.DefaultPlugins.Count || !plugins.SequenceEqual(DafnyOptions.DefaultPlugins)) {
      return "a plugin is loaded";
    }
    // T15: the verbose status line is written at the start of each translation.
    if (options.GetOrOptionDefault(CommonOptionBag.LogLevelOption).CompareTo(LogEventLevel.Verbose) <= 0) {
      return "the log level is verbose";
    }
    // B1: process-global AST ids reach names through monomorphization and printing.
    if (options.TypeEncodingMethod != Bpl.CoreOptions.TypeEncoding.Arguments) {
      return "the type encoding is not Arguments";
    }
    if (options.PrintWithUniqueASTIds) {
      return "AST ids are printed";
    }
    // B2, T22: process-global skolem ids reach the solver input only with debug information.
    if (options.EmitDebugInformation) {
      return "debug information is emitted";
    }
    if (options.VerifySnapshots > 0) {
      return "/verifySnapshots is used";
    }
    // B7: Boogie's printing during preparation toggles shared options.
    if (options.PrintUnstructured != 0 || options.PrintDesugarings || options.PrintPassiveFile != null ||
        options.PrintLambdaLifting || options.PrintInstrumented || options.PrintInlined ||
        options.PrintSplitFile != null || options.Trace || options.TraceVerify) {
      return "a Boogie print or trace option is given";
    }
    return null;
  }

  public async Task<ModuleVerificationPreparation> PrepareAsync(
    ResolutionResult resolution, ModuleDefinition moduleDefinition, bool insertChecksums,
    Func<IVerificationBackend<Bpl.Program>> createBackend, CancellationToken cancellationToken,
    Action<Bpl.Program>? capture = null) {
    if (!BoogieGenerator.ShouldVerifyModule(resolution.ResolvedProgram, moduleDefinition)) {
      throw new Exception("tried to get verification tasks for a module that is not verified");
    }

    var workers = TranslationWorkers(resolution.ResolvedProgram, out _);
    if (workers > 1) {
      return await PrepareInParallelAsync(workers, resolution, moduleDefinition, insertChecksums, createBackend,
        cancellationToken, capture);
    }

    var profile = PipelineProfile.For(resolution.ResolvedProgram.Options);
    var moduleName = profile != null ? PipelineProfile.ModuleName(moduleDefinition) : null;
    var profileStart = profile?.Begin();
    await mutex.WaitAsync(cancellationToken);
    try {
      if (profile != null) {
        profile.Span("translate.wait", profileStart!.Value, new Dictionary<string, object?> { ["module"] = moduleName });
      }

      cancellationToken.ThrowIfCancellationRequested();

      var (boogieProgram, dependencies, _) = await DafnyMain.LargeStackFactory.StartNew(
        () => Translate(resolution, moduleDefinition, insertChecksums, profile, moduleName, cancellationToken),
        cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      capture?.Invoke(boogieProgram);

      var preparation = await Prepare(boogieProgram, createBackend, profile, moduleName, cancellationToken);
      return new ModuleVerificationPreparation(preparation, dependencies);
    }
    finally {
      mutex.Release();
    }
  }

  /// <summary>
  /// Translate, commit in arrival order, then prepare. The ticket is taken before the first
  /// await, so the arrival order is the caller's call order. Commit k waits for commit k-1, and
  /// a failed or cancelled call still completes its ticket only after its predecessor, so a later
  /// commit never overtakes an earlier one.
  /// </summary>
  private async Task<ModuleVerificationPreparation> PrepareInParallelAsync(int workers,
    ResolutionResult resolution, ModuleDefinition moduleDefinition, bool insertChecksums,
    Func<IVerificationBackend<Bpl.Program>> createBackend, CancellationToken cancellationToken,
    Action<Bpl.Program>? capture) {
    var options = resolution.ResolvedProgram.Options;
    var profile = PipelineProfile.For(options);
    var moduleName = profile != null ? PipelineProfile.ModuleName(moduleDefinition) : null;
    var committed = NewSignal();
    Task previous;
    long ticket;
    lock (gate) {
      previous = lastCommit;
      lastCommit = committed.Task;
      ticket = arrivals++;
      if (!bitvectorTypesInterned) {
        // B4: Boogie interns bit-vector types in a lazily filled static array. Fill it before
        // translations run at once, so that every width has one type object.
        for (var width = 0; width < 128; width++) {
          Bpl.Type.GetBvType(width);
        }
        bitvectorTypesInterned = true;
      }
    }
    var stressDelay = StressDelay(options, ticket);
    var previousAwaited = false;
    Bpl.Program boogieProgram;
    ProofDependencyManager dependencies;
    try {
      var profileStart = profile?.Begin();
      await AdmitTranslation(workers, MemoryBudget(options), cancellationToken);
      long allocated = -1;
      try {
        if (profile != null) {
          profile.Span("translate.wait", profileStart!.Value, new Dictionary<string, object?> { ["module"] = moduleName });
        }
        if (stressDelay > 0) {
          await Task.Delay(stressDelay, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        (boogieProgram, dependencies, allocated) = await DafnyMain.LargeStackFactory.StartNew(
          () => Translate(resolution, moduleDefinition, insertChecksums, profile, moduleName, cancellationToken),
          cancellationToken);
      }
      finally {
        FinishTranslation(allocated);
      }

      profileStart = profile?.Begin();
      previousAwaited = true;
      await previous;
      if (profile != null) {
        profile.Span("translate.commit", profileStart!.Value, new Dictionary<string, object?> {
          ["module"] = moduleName,
          ["ticket"] = ticket
        });
      }
      cancellationToken.ThrowIfCancellationRequested();
      capture?.Invoke(boogieProgram);
    }
    finally {
      if (!previousAwaited) {
        await previous;
      }
      committed.SetResult();
    }

    await AdmitPreparation(workers, cancellationToken);
    try {
      var preparation = await Prepare(boogieProgram, createBackend, profile, moduleName, cancellationToken);
      return new ModuleVerificationPreparation(preparation, dependencies);
    }
    finally {
      FinishPreparation();
    }
  }

  /// <summary>
  /// A slot for translation: fewer than <paramref name="workers"/> translations in flight and, unless
  /// none is, <c>GC.GetTotalMemory + (#translating)·B̂ &lt; budget</c>, with B̂ the running mean of a
  /// finished translation's allocation.
  /// </summary>
  private async Task AdmitTranslation(int workers, long budget, CancellationToken cancellationToken) {
    while (true) {
      Task wait;
      lock (gate) {
        if (translating == 0 ||
            (translating < workers && GC.GetTotalMemory(false) + translating * MeanTranslationBytes() < budget)) {
          translating++;
          return;
        }
        wait = slotFreed.Task;
      }
      await wait.WaitAsync(cancellationToken);
    }
  }

  private long MeanTranslationBytes() => translatedCount == 0 ? 0 : translatedBytes / translatedCount;

  private void FinishTranslation(long allocated) {
    TaskCompletionSource freed;
    lock (gate) {
      translating--;
      if (allocated >= 0) {
        translatedCount++;
        translatedBytes += allocated;
      }
      freed = slotFreed;
      slotFreed = NewSignal();
    }
    freed.SetResult();
  }

  private async Task AdmitPreparation(int workers, CancellationToken cancellationToken) {
    while (true) {
      Task wait;
      lock (gate) {
        if (preparing < workers) {
          preparing++;
          return;
        }
        wait = slotFreed.Task;
      }
      await wait.WaitAsync(cancellationToken);
    }
  }

  private void FinishPreparation() {
    TaskCompletionSource freed;
    lock (gate) {
      preparing--;
      freed = slotFreed;
      slotFreed = NewSignal();
    }
    freed.SetResult();
  }

  /// <summary>
  /// The hidden stress schedule: a delay for ticket k before its translation starts. A positive
  /// seed draws delays of up to 40 ms; -1 delays ticket k by 100·(3 − k mod 4) ms, so that each
  /// group of four translations admitted together finishes in reverse arrival order.
  /// </summary>
  private static int StressDelay(DafnyOptions options, long ticket) {
    var seed = options.Get(CommonOptionBag.TranslationWorkersStress);
    return seed switch {
      0 => 0,
      < 0 => (int)(100 * (3 - ticket % 4)),
      _ => new Random(unchecked(seed * 7919 + (int)ticket)).Next(41)
    };
  }

  private static (Bpl.Program Program, ProofDependencyManager Dependencies, long Allocated) Translate(
    ResolutionResult resolution, ModuleDefinition moduleDefinition, bool insertChecksums,
    PipelineProfile? profile, string? moduleName, CancellationToken cancellationToken) {
    var program = resolution.ResolvedProgram;
    var errorReporter = program.Reporter;
    var profileStart = profile?.Begin();
    var started = GC.GetAllocatedBytesForCurrentThread();
    Type.ResetScopes();
    var translatorFlags = new BoogieGenerator.TranslatorFlags(errorReporter.Options) {
      InsertChecksums = insertChecksums,
      ReportRanges = program.Options.Get(Snippets.ShowSnippets),
      PreludeText = program.PreludeText
    };
    if (program.Options.Get(CommonOptionBag.TranslationCache)) {
      var cache = ModuleTranslationCache.ProcessCache;
      var module = cache.TranslateModule(program, moduleDefinition, translatorFlags, cancellationToken, out var reused);
      var cached = module.Program;
      if (profile != null) {
        profile.Span("translate", profileStart!.Value, new Dictionary<string, object?> {
          ["module"] = moduleName,
          ["cached"] = reused,
          ["declarations"] = cached.TopLevelDeclarations.Count(),
          ["implementations"] = cached.Implementations.Count()
        });
      }
      return (cached, module.ProofDependencies, GC.GetAllocatedBytesForCurrentThread() - started);
    }
    var dependencies = new ProofDependencyManager();
    var threadAllocated = profile != null ? GC.GetAllocatedBytesForCurrentThread() : 0;
    var translator = new BoogieGenerator(errorReporter, dependencies, translatorFlags);
    if (profile != null) {
      // The constructor reads, parses and resolves the prelude, once per module.
      profile.Span("translate.prelude", profileStart!.Value, new Dictionary<string, object?> {
        ["module"] = moduleName,
        ["threadAllocBytes"] = GC.GetAllocatedBytesForCurrentThread() - threadAllocated
      });
      profileStart = profile.Begin();
      threadAllocated = GC.GetAllocatedBytesForCurrentThread();
    }
    var threadStart = profile?.BeginThread();
    var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);
    if (profile != null) {
      profile.FrontEnd("translate.module", threadStart!.Value, new Dictionary<string, object?> {
        ["module"] = moduleName
      });
      profile.Span("translate", profileStart!.Value, new Dictionary<string, object?> {
        ["module"] = moduleName,
        ["threadAllocBytes"] = GC.GetAllocatedBytesForCurrentThread() - threadAllocated,
        ["declarations"] = translated.TopLevelDeclarations.Count(),
        ["implementations"] = translated.Implementations.Count(),
        // Under the stress schedule, the source locations of the module's Boogie graph, which its
        // printed text omits, so that a test can compare them across worker counts (issue 569).
        ["tokens"] = program.Options.Get(CommonOptionBag.TranslationWorkersStress) != 0
          ? TokenFingerprint.Of(translated) : null
      });
    }
    return (translated, dependencies, GC.GetAllocatedBytesForCurrentThread() - started);
  }

  private static async Task<VerificationPreparation> Prepare(Bpl.Program boogieProgram,
    Func<IVerificationBackend<Bpl.Program>> createBackend, PipelineProfile? profile, string? moduleName,
    CancellationToken cancellationToken) {
    var profileStart = profile?.Begin();
    var preparation = await createBackend().PrepareAsync(boogieProgram, cancellationToken);
    if (profile != null) {
      // Boogie's preparation: type checking, inlining, preprocessing, passification and splitting.
      profile.Span("prepare", profileStart!.Value, new Dictionary<string, object?> {
        ["module"] = moduleName,
        ["tasks"] = preparation.IsReady ? preparation.GetWorkOrThrow().Count : (int?)null
      });
    }
    return preparation;
  }

  public void Dispose() {
    mutex.Dispose();
  }

  /// <summary>
  /// A digest of the Dafny source location of every node of a translated Boogie graph, in visiting
  /// order: the line and column of each token, and of both parts of a nested origin. Error locations come
  /// from these tokens, so two translations with equal text but different tokens can still report
  /// differently.
  /// </summary>
  private sealed class TokenFingerprint : Bpl.ReadOnlyVisitor {
    private readonly System.Text.StringBuilder locations = new();

    public static string Of(Bpl.Program program) {
      var visitor = new TokenFingerprint();
      visitor.Visit(program);
      var digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(visitor.locations.ToString()));
      return Convert.ToHexString(digest)[..16].ToLowerInvariant();
    }

    public override Bpl.Absy Visit(Bpl.Absy node) {
      Append(node.tok);
      locations.Append(';');
      return base.Visit(node);
    }

    // The base visit of an implementation expects a resolved graph; this one is not yet resolved.
    public override Bpl.Implementation VisitImplementation(Bpl.Implementation node) {
      foreach (var block in node.Blocks ?? new List<Bpl.Block>()) {
        Append(block.tok);
        locations.Append(';');
        foreach (var command in block.Cmds) {
          Visit(command);
        }
        if (block.TransferCmd != null) {
          Append(block.TransferCmd.tok);
          locations.Append(';');
        }
      }
      return node;
    }

    // Only Dafny source locations count. Boogie's printer, by default, replaces the token of every
    // node it prints with one for its output position, and some nodes (Expr.True, Type.Bool, ...)
    // are process-wide singletons, so the preparation of another module, printing them, changes
    // those tokens; they never report a Dafny location.
    private void Append(Bpl.IToken? token) {
      if (token is not IOrigin) {
        locations.Append('-');
        return;
      }
      locations.Append(token.line).Append(':').Append(token.col);
      if (token is NestedOrigin nested) {
        locations.Append('(');
        Append(nested.Inner);
        locations.Append(')');
      }
    }
  }
}
