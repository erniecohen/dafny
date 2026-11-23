using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Dafny.LanguageServer.Language.Symbols;
using Microsoft.Dafny.LanguageServer.Workspace.ChangeProcessors;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.Dafny.LanguageServer.Workspace;

public record IdeVerificationTaskState(Range Range, PublishedVerificationStatus Status,
  IReadOnlyList<Diagnostic> Diagnostics, bool HitErrorLimit, IVerificationWork Task, VerificationWorkStatus RawStatus);

public enum VerificationPreparationState { NotStarted, InProgress, Done }
/// <param name="PreparationPerCopy">
/// How far the preparation of each ICanVerify at a range has come, keyed like their tasks (IdeState.BelongsTo,
/// IdeState.CopyKey), so that one of them being prepared does not make the range read as prepared while another is
/// still being prepared (https://github.com/erniecohen/dafny/issues/433).
/// </param>
/// <param name="DiagnosticsPerCopy">
/// The diagnostics computed once an implementation has verified, such as the proof dependency warnings.  They are
/// keyed like the tasks of the ICanVerify at a range (IdeState.BelongsTo, IdeState.CopyKey), so that each
/// keeps and replaces only its own (https://github.com/erniecohen/dafny/issues/424).
/// </param>
public record IdeCanVerifyState(ImmutableDictionary<string, VerificationPreparationState> PreparationPerCopy,
  ImmutableDictionary<string, IdeVerificationTaskState> VerificationTasks,
  ImmutableDictionary<string, IReadOnlyList<Diagnostic>> DiagnosticsPerCopy) {
  /// <summary>
  /// The range is in progress while an ICanVerify at it has been scheduled and its tasks are not yet identified.
  /// Otherwise it is done once the tasks of one of them have been identified.  An ICanVerify that is never scheduled,
  /// such as one whose module is not verified, does not hold the range back, as before.
  /// </summary>
  public VerificationPreparationState PreparationProgress =>
    PreparationPerCopy.Values.Contains(VerificationPreparationState.InProgress) ? VerificationPreparationState.InProgress
    : PreparationPerCopy.Values.Contains(VerificationPreparationState.Done) ? VerificationPreparationState.Done
    : VerificationPreparationState.NotStarted;

  public IEnumerable<Diagnostic> Diagnostics => DiagnosticsPerCopy.Values.SelectMany(diagnostics => diagnostics);
}



/// <summary>
/// Contains information from the latest document, and from older documents if some information is missing,
/// to provide the IDE with as much information as possible.
/// </summary>
public record IdeState(
  ISet<Uri> OwnedUris,
  IReadOnlyDictionary<Uri, int> VersionedFiles,
  CompilationInput Input,
  CompilationStatus Status,
  Node Program,
  ImmutableList<FileDiagnostic> PreviousFastDiagnostics,
  ImmutableList<FileDiagnostic> CurrentFastDiagnostics,
  Node? ResolvedProgram,
  SymbolTable SymbolTable,
  LegacySignatureAndCompletionTable SignatureAndCompletionTable,
  ImmutableDictionary<Uri, ImmutableDictionary<Range, IdeCanVerifyState>> CanVerifyStates,
  IReadOnlyList<Counterexample> Counterexamples,
  IReadOnlyDictionary<Uri, IReadOnlyList<Range>> GhostRanges,
  ImmutableDictionary<Uri, DocumentVerificationTree> VerificationTrees
) {

  public ImmutableList<FileDiagnostic> FastDiagnostics => Status is CompilationStatus.Parsing or CompilationStatus.ResolutionStarted
    ? PreviousFastDiagnostics
    : CurrentFastDiagnostics;

  public Uri Uri => Input.Uri.ToUri();
  public int Version => Input.Version;

  public static IEnumerable<Diagnostic> MarkDiagnosticsAsOutdated(IEnumerable<Diagnostic> diagnostics) {
    return diagnostics.Select(diagnostic => diagnostic with {
      Severity = diagnostic.Severity == DiagnosticSeverity.Error ? DiagnosticSeverity.Warning : diagnostic.Severity,
      Message = diagnostic.Message.StartsWith(OutdatedPrefix)
        ? diagnostic.Message
        : OutdatedPrefix + diagnostic.Message
    });
  }

  public static IdeState InitialIdeState(CompilationInput input) {
    var program = new EmptyNode();
    return new IdeState(ImmutableHashSet<Uri>.Empty,
      ImmutableDictionary<Uri, int>.Empty,
      input,
      CompilationStatus.Parsing,
      program,
      ImmutableList<FileDiagnostic>.Empty,
      ImmutableList<FileDiagnostic>.Empty,
      program,
      SymbolTable.Empty(),
      LegacySignatureAndCompletionTable.Empty(input.Options, input.Project), ImmutableDictionary<Uri, ImmutableDictionary<Range, IdeCanVerifyState>>.Empty,
      Array.Empty<Counterexample>(),
      ImmutableDictionary<Uri, IReadOnlyList<Range>>.Empty,
      ImmutableDictionary<Uri, DocumentVerificationTree>.Empty
    );
  }

  public const string OutdatedPrefix = "Outdated: ";

  public IdeState Migrate(DafnyOptions options, IMigrator migrator, int newVersion, bool clientSide) {
    var migratedVerificationTrees = VerificationTrees.ToImmutableDictionary(
      kv => kv.Key, kv =>
        (DocumentVerificationTree)migrator.RelocateVerificationTree(kv.Value));

    var verificationResults = clientSide
      ? CanVerifyStates
      : MigrateImplementationViews(migrator, CanVerifyStates);

    return this with {
      Input = Input with {
        Version = newVersion
      },
      PreviousFastDiagnostics = CurrentFastDiagnostics,
      CurrentFastDiagnostics = ImmutableList<FileDiagnostic>.Empty,
      Status = CompilationStatus.Parsing,
      CanVerifyStates = verificationResults,
      SignatureAndCompletionTable = options.Get(LegacySignatureAndCompletionTable.MigrateSignatureAndCompletionTable)
        ? migrator.MigrateSymbolTable(SignatureAndCompletionTable) : LegacySignatureAndCompletionTable.Empty(options, Input.Project),
      VerificationTrees = migratedVerificationTrees
    };
  }

  private ImmutableDictionary<Uri, ImmutableDictionary<Range, IdeCanVerifyState>> MigrateImplementationViews(
    IMigrator migrator,
    ImmutableDictionary<Uri, ImmutableDictionary<Range, IdeCanVerifyState>> oldVerificationDiagnostics) {
    var uri = migrator.MigratedUri;
    var previous = oldVerificationDiagnostics.GetValueOrDefault(uri);
    if (previous == null) {
      return oldVerificationDiagnostics;
    }
    var result = ImmutableDictionary<Range, IdeCanVerifyState>.Empty;
    foreach (var entry in previous) {
      var newOuterRange = migrator.MigrateRange(entry.Key);
      if (newOuterRange == null) {
        continue;
      }

      var newValue = ImmutableDictionary<string, IdeVerificationTaskState>.Empty;
      foreach (var innerEntry in entry.Value.VerificationTasks) {
        var newInnerRange = migrator.MigrateRange(innerEntry.Value.Range);
        if (newInnerRange != null) {
          newValue = newValue.Add(innerEntry.Key, innerEntry.Value with {
            Range = newInnerRange,
            Diagnostics = migrator.MigrateDiagnostics(innerEntry.Value.Diagnostics)
          });
        }
      }

      result = result.Add(newOuterRange, entry.Value with { VerificationTasks = newValue });
    }

    return oldVerificationDiagnostics.SetItem(uri, result);
  }

  public IReadOnlyDictionary<Range, IdeCanVerifyState> GetVerificationResults(Uri uri) {
    return CanVerifyStates.GetValueOrDefault(uri) ??
      ((IReadOnlyDictionary<Range, IdeCanVerifyState>)ImmutableDictionary<Range, IdeCanVerifyState>.Empty);
  }

  public IEnumerable<FileDiagnostic> GetAllDiagnostics() {
    var verificationDiagnostics = CanVerifyStates.SelectMany(s =>
      s.Value.Values.SelectMany(cvs =>
        cvs.VerificationTasks.SelectMany(t => t.Value.Diagnostics.Select(d => new FileDiagnostic(s.Key, d))).
          Concat(cvs.Diagnostics.Select(d => new FileDiagnostic(s.Key, d)))));
    return FastDiagnostics.Concat(verificationDiagnostics).Concat(GetErrorLimitDiagnostics());
  }

  private IEnumerable<FileDiagnostic> GetErrorLimitDiagnostics() {
    var anyVerificationHitErrorLimit = CanVerifyStates.Values.SelectMany(x => x.Values)
      .SelectMany(s => s.VerificationTasks.Select(t => t.Value.HitErrorLimit)).Any(x => x);
    IEnumerable<FileDiagnostic> result;
    if (anyVerificationHitErrorLimit) {
      var diagnostic = new Diagnostic() {
        Severity = DiagnosticSeverity.Warning,
        Code = new DiagnosticCode("errorLimitHit"),
        Message =
          "Verification hit error limit so not all errors may be shown. Configure this limit using --error-limit",
        Range = Input.Project.StartingToken.GetLspRange(),
        Source = MessageSource.Verifier.ToString()
      };
      result = new[] { new FileDiagnostic(Input.Project.Uri, diagnostic) };
    } else {
      result = [];
    }

    return result;
  }

  public async Task<IdeState> UpdateState(DafnyOptions options,
    ILogger logger,
    TelemetryPublisherBase telemetryPublisher,
    IProjectDatabase projectDatabase, ICompilationEvent e) {
    switch (e) {
      case DeterminedRootFiles determinedRootFiles:
        return await HandleDeterminedRootFiles(options, logger, projectDatabase, determinedRootFiles);
      case VerificationUpdate verificationUpdate:
        return HandleVerificationUpdate(options, logger, verificationUpdate);
      case CanVerifyPartsIdentified canVerifyPartsIdentified:
        return HandleCanVerifyPartsUpdated(logger, canVerifyPartsIdentified);
      case FinishedParsing finishedParsing:
        return HandleFinishedParsing(finishedParsing);
      case FinishedResolution finishedResolution:
        return HandleFinishedResolution(options, logger, telemetryPublisher, finishedResolution);
      case InternalCompilationException internalCompilationException:
        return HandleInternalCompilationException(internalCompilationException);
      case VerificationException verificationException:
        return HandleVerificationException(verificationException);
      case NewDiagnostic newDiagnostic:
        return HandleNewDiagnostic(newDiagnostic);
      case ScheduledVerification scheduledVerification:
        return HandleScheduledVerification(scheduledVerification);
      default:
        throw new ArgumentOutOfRangeException(nameof(e));
    }
  }

  private async Task<IdeState> HandleDeterminedRootFiles(DafnyOptions options, ILogger logger,
    IProjectDatabase projectDatabase, DeterminedRootFiles determinedRootFiles) {

    var errors = CurrentFastDiagnostics.Where(d => d.Diagnostic.Severity == DiagnosticSeverity.Error);
    var status = errors.Any() ? CompilationStatus.ParsingFailed : Status;

    var ownedUris = new HashSet<Uri>();
    foreach (var file in determinedRootFiles.Roots) {
      var uriProject = await projectDatabase.GetProject(file.Uri);
      logger.LogDebug($"HandleDeterminedRootFiles found project for {file.Uri} to be {uriProject.Uri}");
      var ownedUri = uriProject.Equals(determinedRootFiles.Project);
      if (ownedUri) {
        ownedUris.Add(file.Uri);
      }
    }
    ownedUris.Add(determinedRootFiles.Project.Uri);

    var versionedFiles = ImmutableDictionary<Uri, int>.Empty;
    if (determinedRootFiles.Project.Version != null) {
      versionedFiles = versionedFiles.Add(determinedRootFiles.Project.Uri, determinedRootFiles.Project.Version.Value);
    }
    return this with {
      OwnedUris = ownedUris,
      VersionedFiles = versionedFiles,
      Status = status,
      VerificationTrees = determinedRootFiles.Roots.ToImmutableDictionary(
        file => file.Uri,
        file => VerificationTrees.GetValueOrDefault(file.Uri) ??
                new DocumentVerificationTree(Program, file.Uri))
    };
  }

  private IdeState HandleScheduledVerification(ScheduledVerification scheduledVerification) {
    var previousState = this;

    var uri = scheduledVerification.CanVerify.NavigationRange.Uri;
    var range = scheduledVerification.CanVerify.NavigationRange.ToLspRange();
    var previousVerificationResult = previousState.CanVerifyStates[uri][range];
    var previousImplementations = previousVerificationResult.VerificationTasks;
    var key = CopyKey(scheduledVerification.CanVerify);
    var preparationProgress = new[] {
      previousVerificationResult.PreparationPerCopy.GetValueOrDefault(key, VerificationPreparationState.NotStarted),
      VerificationPreparationState.InProgress
    }.Max();
    var verificationResult = new IdeCanVerifyState(
      PreparationPerCopy: previousVerificationResult.PreparationPerCopy.SetItem(key, preparationProgress),
      VerificationTasks: previousImplementations.ToImmutableDictionary(kv => kv.Key,
        kv => !BelongsTo(kv.Value, scheduledVerification.CanVerify) ? kv.Value : kv.Value with {
          Status = PublishedVerificationStatus.Stale,
          Diagnostics = MarkDiagnosticsAsOutdated(kv.Value.Diagnostics).ToList()
        }), previousVerificationResult.DiagnosticsPerCopy);
    return previousState with {
      CanVerifyStates = previousState.CanVerifyStates.SetItem(uri,
        previousState.CanVerifyStates[uri].SetItem(range, verificationResult))
    };
  }

  private IdeState HandleNewDiagnostic(NewDiagnostic newDiagnostic) {
    return this with {
      CurrentFastDiagnostics = CurrentFastDiagnostics.Add(new FileDiagnostic(newDiagnostic.Uri, newDiagnostic.Diagnostic.ToLspDiagnostic()))
    };
  }

  private IdeState HandleInternalCompilationException(InternalCompilationException internalCompilationException) {
    var previousState = this;
    var internalErrorDiagnostic = new Diagnostic {
      Message =
        "Dafny encountered an internal error. Please report it at <https://github.com/dafny-lang/dafny/issues>.\n" +
        internalCompilationException.Exception,
      Severity = DiagnosticSeverity.Error,
      Range = new Range(0, 0, 0, 1)
    };

    return previousState with {
      Status = CompilationStatus.InternalException,
      CurrentFastDiagnostics = CurrentFastDiagnostics.Add(new FileDiagnostic(previousState.Input.Uri.ToUri(), internalErrorDiagnostic)),
    };
  }

  private IdeState HandleFinishedResolution(DafnyOptions options,
    ILogger logger,
    TelemetryPublisherBase telemetryPublisher,
    FinishedResolution finishedResolution) {
    var previousState = this;
    var errors = CurrentFastDiagnostics.Where(d =>
      d.Diagnostic.Severity == DiagnosticSeverity.Error && d.Diagnostic.Source != MessageSource.Compiler.ToString()).ToList();
    var status = errors.Any() ? CompilationStatus.ResolutionFailed : CompilationStatus.ResolutionSucceeded;

    var cancellationToken = CancellationToken.None; // TODO ?
    SymbolTable? symbolTable = null;
    try {
      symbolTable = SymbolTable.CreateFrom(logger, finishedResolution.Result.ResolvedProgram, cancellationToken);
    } catch (Exception e) {
      logger.LogError(e, "failed to create symbol table");
    }

    var beforeLegacyServerResolution = DateTime.Now;
    var legacySignatureAndCompletionTable = new SymbolTableFactory(logger).CreateFrom(Input,
      finishedResolution.Result, cancellationToken);
    telemetryPublisher.PublishTime("LegacyServerResolution", options.DafnyProject.Uri.ToString(), DateTime.Now - beforeLegacyServerResolution);

    IReadOnlyDictionary<Uri, IReadOnlyList<Range>> ghostRanges = new GhostStateDiagnosticCollector(options, logger).
      GetGhostStateDiagnostics(legacySignatureAndCompletionTable, cancellationToken);

    var trees = previousState.VerificationTrees;
    if (status == CompilationStatus.ResolutionSucceeded) {
      foreach (var uri in trees.Keys) {
        trees = trees.SetItem(uri,
          GutterIconAndHoverVerificationDetailsManager.UpdateTree(options, finishedResolution.Result.ResolvedProgram,
            previousState.VerificationTrees[uri]));
      }
    }

    var verificationResults = finishedResolution.Result.CanVerifies == null
      ? previousState.CanVerifyStates
      : finishedResolution.Result.CanVerifies.ToImmutableDictionary(k => k.Key,
        k => k.Value.Values.GroupBy<ICanVerify, Range>(l => l.NavigationRange.ToLspRange()).ToImmutableDictionary(
          l => l.Key,
          l => MergeResults(l.Select(canVerify => MergeVerifiable(previousState, canVerify)))));
    var signatureAndCompletionTable = legacySignatureAndCompletionTable.Resolved
      ? legacySignatureAndCompletionTable
      : previousState.SignatureAndCompletionTable;

    return previousState with {
      Status = status,
      Counterexamples = Array.Empty<Counterexample>(),
      ResolvedProgram = finishedResolution.Result.ResolvedProgram,
      SymbolTable = symbolTable ?? previousState.SymbolTable,
      SignatureAndCompletionTable = signatureAndCompletionTable,
      GhostRanges = ghostRanges,
      CanVerifyStates = verificationResults,
      VerificationTrees = trees
    };
  }

  private static IdeCanVerifyState MergeResults(IEnumerable<IdeCanVerifyState> results) {
    return results.Aggregate((a, b) => new IdeCanVerifyState(
      a.PreparationPerCopy.ToImmutableDictionary().Merge(b.PreparationPerCopy, MergeStates),
      a.VerificationTasks.ToImmutableDictionary().Merge(b.VerificationTasks,
        (aView, bView) => aView /* Merge should not occur */),
      // ICanVerify with the same key carry over the same diagnostics (MergeVerifiable), so these are kept once.
      a.DiagnosticsPerCopy.SetItems(b.DiagnosticsPerCopy)));
  }

  private static VerificationPreparationState MergeStates(VerificationPreparationState a, VerificationPreparationState b) {
    return new[] { a, b }.Max();
  }

  private static PublishedVerificationStatus Combine(PublishedVerificationStatus first, PublishedVerificationStatus second) {
    return new[] { first, second }.Min();
  }

  private static IdeCanVerifyState MergeVerifiable(IdeState previousState, ICanVerify canVerify) {
    var range = canVerify.NavigationRange.ToLspRange();
    var previousIdeCanVerifyState = previousState.GetVerificationResults(canVerify.NavigationRange.Uri).GetValueOrDefault(range);
    var previousImplementations =
      previousIdeCanVerifyState?.VerificationTasks ??
      ImmutableDictionary<string, IdeVerificationTaskState>.Empty;
    var key = CopyKey(canVerify);
    var previousDiagnostics = previousIdeCanVerifyState?.DiagnosticsPerCopy.GetValueOrDefault(key);
    return new IdeCanVerifyState(
      PreparationPerCopy: ImmutableDictionary<string, VerificationPreparationState>.Empty.Add(key,
        VerificationPreparationState.NotStarted),
      VerificationTasks: previousImplementations.Where(kv => BelongsTo(kv.Value, canVerify)).ToImmutableDictionary(
        kv => kv.Key, kv => kv.Value with {
          Status = PublishedVerificationStatus.Stale,
          Diagnostics = MarkDiagnosticsAsOutdated(kv.Value.Diagnostics).ToList()
        }), previousDiagnostics == null
        ? ImmutableDictionary<string, IReadOnlyList<Diagnostic>>.Empty
        : ImmutableDictionary<string, IReadOnlyList<Diagnostic>>.Empty.Add(key, previousDiagnostics));
  }

  /// <summary>
  /// Several ICanVerify can share a navigation range.  A refining module's copy of an inherited member, whose
  /// override check that module makes again (BoogieGenerator.MustReverifyInheritedOverrideCheck), has the range of
  /// the member in the refinement base, which is verified too, and so may the copies of other refining modules.
  /// The events of one of them must leave the tasks of the others alone.  So a task belongs to the copies of the
  /// module that copied its ICanVerify, or to the declarations that are not copies.  Those keep the range's tasks as
  /// before, under a rename too, because their key has no name in it.
  /// </summary>
  private static bool BelongsTo(IdeVerificationTaskState task, ICanVerify canVerify) {
    return CopyingModule(Compilation.GetCanVerify(task.Task)) == CopyingModule(canVerify);
  }

  internal static string? CopyingModule(ICanVerify canVerify) {
    return canVerify.Origin.IsCopy ? canVerify.ContainingModule.FullDafnyName : null;
  }

  /// <summary>
  /// The key of an ICanVerify in IdeCanVerifyState.PreparationPerCopy and IdeCanVerifyState.DiagnosticsPerCopy, the
  /// same as BelongsTo's.  A dictionary key cannot be null, and no module's full name is empty.
  /// </summary>
  private static string CopyKey(ICanVerify canVerify) {
    return CopyingModule(canVerify) ?? "";
  }

  private IdeState HandleFinishedParsing(FinishedParsing finishedParsing) {
    var previousState = this;
    var trees = previousState.VerificationTrees;
    foreach (var uri in trees.Keys) {
      trees = trees.SetItem(uri,
        new DocumentVerificationTree(finishedParsing.ParseResult.Program, uri) {
          Children = trees[uri].Children
        });
    }

    var errors = CurrentFastDiagnostics.Where(d => d.Diagnostic.Severity == DiagnosticSeverity.Error);
    var status = errors.Any() ? CompilationStatus.ParsingFailed : CompilationStatus.ResolutionStarted;

    foreach (var entry in previousState.VersionedFiles) {
      finishedParsing.ParseResult.VersionedFiles.Add(entry.Key, entry.Value);
    }
    return previousState with {
      VersionedFiles = finishedParsing.ParseResult.VersionedFiles,
      Program = finishedParsing.ParseResult.Program,
      Status = status,
      VerificationTrees = trees
    };
  }

  private IdeState HandleCanVerifyPartsUpdated(ILogger logger, CanVerifyPartsIdentified canVerifyPartsIdentified) {
    var previousState = this;
    var implementations = canVerifyPartsIdentified.Parts.Select(t => BoogieVerificationReporting.GetTask(t).Split.Implementation).Distinct();
    var gutterIconManager = new GutterIconAndHoverVerificationDetailsManager(logger);

    var uri = canVerifyPartsIdentified.CanVerify.Origin.Uri;
    gutterIconManager.ReportImplementationsBeforeVerification(previousState,
      canVerifyPartsIdentified.CanVerify, implementations.ToArray());

    var range = canVerifyPartsIdentified.CanVerify.NavigationRange.ToLspRange();
    var previousImplementations = previousState.CanVerifyStates[uri][range].VerificationTasks;
    var tasksOfOthers = previousImplementations.Where(kv => !BelongsTo(kv.Value, canVerifyPartsIdentified.CanVerify));
    var key = CopyKey(canVerifyPartsIdentified.CanVerify);
    var diagnosticsOfOthers = previousState.CanVerifyStates[uri][range].DiagnosticsPerCopy.Remove(key);
    var verificationResult = new IdeCanVerifyState(
      PreparationPerCopy: previousState.CanVerifyStates[uri][range].PreparationPerCopy.SetItem(key,
        VerificationPreparationState.Done),
      VerificationTasks: ImmutableDictionary.CreateRange(tasksOfOthers).SetItems(
        canVerifyPartsIdentified.Parts.ToImmutableDictionary(Compilation.GetTaskName,
        k => {
          var previous = previousImplementations.GetValueOrDefault(Compilation.GetTaskName(k));
          return new IdeVerificationTaskState(range, PublishedVerificationStatus.Queued,
            previous?.Diagnostics ?? Array.Empty<Diagnostic>(),
            previous?.HitErrorLimit ?? false, k, new VerificationWorkStale());
        })), diagnosticsOfOthers);
    return previousState with {
      CanVerifyStates = previousState.CanVerifyStates.SetItem(uri,
        previousState.CanVerifyStates[uri].SetItem(range, verificationResult))
    };
  }

  private IdeState HandleVerificationException(VerificationException verificationException) {
    var previousState = this;

    var name = Compilation.GetTaskName(verificationException.Task);
    var uri = verificationException.CanVerify.Origin.Uri;
    var range = verificationException.CanVerify.NavigationRange.ToLspRange();

    var previousVerificationResult = previousState.CanVerifyStates[uri][range];
    var previousImplementations = previousVerificationResult.VerificationTasks;
    var previousView = previousImplementations.GetValueOrDefault(name);
    var diagnostics = previousView?.Diagnostics ?? Array.Empty<Diagnostic>();
    var hitErrorLimit = previousView?.HitErrorLimit ?? false;

    var internalErrorDiagnostic = new Diagnostic {
      Message = verificationException.Exception.Message,
      Severity = DiagnosticSeverity.Error,
      Range = range
    };
    diagnostics = diagnostics.Concat(new[] { internalErrorDiagnostic }).ToList();

    var view = new IdeVerificationTaskState(range, PublishedVerificationStatus.Error, diagnostics.ToList(), hitErrorLimit, verificationException.Task, new VerificationWorkStale());

    return previousState with {
      CanVerifyStates = previousState.CanVerifyStates.SetItem(uri,
        previousState.CanVerifyStates[uri].SetItem(range, previousVerificationResult with {
          VerificationTasks = previousVerificationResult.VerificationTasks.SetItem(name, view)
        }))
    };
  }

  private IdeState HandleVerificationUpdate(DafnyOptions options, ILogger logger, VerificationUpdate verificationUpdate) {
    var previousState = this;

    var name = Compilation.GetTaskName(verificationUpdate.VerificationTask);
    var status = StatusFromVerificationStatus(verificationUpdate.Status);
    var uri = verificationUpdate.CanVerify.Origin.Uri;
    var range = verificationUpdate.CanVerify.NavigationRange.ToLspRange();

    var previousVerificationResult = previousState.CanVerifyStates[uri][range];
    var previousImplementations = previousVerificationResult.VerificationTasks;
    var previousView = previousImplementations.GetValueOrDefault(name);
    var counterExamples = previousState.Counterexamples;
    bool hitErrorLimit = previousView?.HitErrorLimit ?? false;
    VerificationWorkStatus rawStatus = verificationUpdate.Status;
    var diagnostics = previousView?.Diagnostics ?? Array.Empty<Diagnostic>();
    if (verificationUpdate.Status is VerificationWorkRunning) {
      diagnostics = Array.Empty<Diagnostic>();
      hitErrorLimit = false;
    }

    if (verificationUpdate.Status is VerificationWorkCompleted completed) {
      // WarnContradictoryAssumptions should be computable after completing a single assertion batch.
      // And we should do this because it allows this warning to be shown when doing --filter-position on a single assertion
      // https://github.com/dafny-lang/dafny/issues/5039 

      var nativeResult = BoogieVerificationReporting.GetResult(completed.Result);
      counterExamples = counterExamples.Concat(nativeResult.CounterExamples);
      hitErrorLimit |= nativeResult.MaxCounterExamples == nativeResult.CounterExamples.Count;
      var newDiagnostics =
        Compilation.GetDiagnosticsFromResult(options, previousState.Uri, verificationUpdate.CanVerify,
          BoogieVerificationReporting.GetTask(verificationUpdate.VerificationTask), nativeResult);
      diagnostics = newDiagnostics.Select(d => d.ToLspDiagnostic()).ToList();
      logger.LogTrace(
        $"Completed received for {previousState.Input} and found #{diagnostics.Count} diagnostics and #{nativeResult.CounterExamples.Count} counterexamples.");
    }

    // An event replaces only the diagnostics of the ICanVerify whose task it is.
    var diagnosticsKey = CopyKey(verificationUpdate.CanVerify);
    var newCanVerifyDiagnostics = new List<Diagnostic>();
    var taskState = new IdeVerificationTaskState(range, status, diagnostics.ToList(),
      hitErrorLimit, verificationUpdate.VerificationTask, rawStatus);
    var newTaskStates = previousVerificationResult.VerificationTasks.SetItem(name, taskState);

    var scopeGroup = newTaskStates.Values.Where(s => s.Task.Source.Id.Scope == verificationUpdate.VerificationTask.Source.Id.Scope).ToList();
    var allTasksAreCompleted = scopeGroup.All(s => s.Status >= PublishedVerificationStatus.Error);
    if (allTasksAreCompleted) {

      var errorReporter = new ObservableErrorReporter(options, uri);
      List<DafnyDiagnostic> verificationCoverageDiagnostics = [];
      errorReporter.Updates.Subscribe(d => verificationCoverageDiagnostics.Add(d.Diagnostic));

      ProofDependencyWarnings.ReportSuspiciousDependencies(options,
        scopeGroup.Select(s => BoogieVerificationReporting.GetTaskResult(s.Task, ((VerificationWorkCompleted)s.RawStatus).Result)),
        errorReporter, verificationUpdate.ProofDependencyManager);

      newCanVerifyDiagnostics = (previousVerificationResult.DiagnosticsPerCopy.GetValueOrDefault(diagnosticsKey) ?? [])
        .Concat(verificationCoverageDiagnostics.Select(d => d.ToLspDiagnostic())).ToList();
    }

    UpdateGutterIconTrees(logger, verificationUpdate, scopeGroup);

    return previousState with {
      Counterexamples = counterExamples,
      CanVerifyStates = previousState.CanVerifyStates.SetItem(uri,
        previousState.CanVerifyStates[uri].SetItem(range, previousVerificationResult with {
          VerificationTasks = newTaskStates,
          DiagnosticsPerCopy = previousVerificationResult.DiagnosticsPerCopy.SetItem(diagnosticsKey, newCanVerifyDiagnostics)
        }))
    };
  }

  private void UpdateGutterIconTrees(ILogger logger, VerificationUpdate verificationUpdate, IReadOnlyList<IdeVerificationTaskState> scopeGroup) {
    var gutterIconManager = new GutterIconAndHoverVerificationDetailsManager(logger);
    if (verificationUpdate.Status is VerificationWorkRunning && scopeGroup.Count(e => e.Status == PublishedVerificationStatus.Running) == 1) {
      gutterIconManager.ReportVerifyImplementationRunning(this, BoogieVerificationReporting.GetTask(verificationUpdate.VerificationTask).Split.Implementation);
    }

    if (verificationUpdate.Status is VerificationWorkCompleted completed) {
      gutterIconManager.ReportAssertionBatchResult(this,
        new AssertionBatchResult(BoogieVerificationReporting.GetTask(verificationUpdate.VerificationTask).Split.Implementation, BoogieVerificationReporting.GetResult(completed.Result)));
    }

    if (scopeGroup.All(e => e.Status >= PublishedVerificationStatus.Error)) {
      var results = scopeGroup.Select(e => e.RawStatus).OfType<VerificationWorkCompleted>().Select(c => BoogieVerificationReporting.GetResult(c.Result)).ToList();

      foreach (var result in results) {
        logger.LogDebug(
          $"Possibly duplicate reporting assertion batch {result.VcNum}, version {Version}");
        gutterIconManager.ReportAssertionBatchResult(this,
          new AssertionBatchResult(BoogieVerificationReporting.GetTask(verificationUpdate.VerificationTask).Split.Implementation, result));
      }

      var resourceCount = results.Sum(e => e.ResourceCount);
      var outcome = results.Select(e => Compilation.GetOutcome(e.Outcome)).Max();
      gutterIconManager.ReportEndVerifyImplementation(this, BoogieVerificationReporting.GetTask(verificationUpdate.VerificationTask).Split.Implementation, resourceCount, outcome);
    }
  }

  private static PublishedVerificationStatus StatusFromVerificationStatus(VerificationWorkStatus verificationStatus) {
    switch (verificationStatus) {
      case VerificationWorkStale:
        return PublishedVerificationStatus.Stale;
      case VerificationWorkQueued:
        return PublishedVerificationStatus.Queued;
      case VerificationWorkRunning:
        return PublishedVerificationStatus.Running;
      case VerificationWorkCompleted completed:
        return completed.Result.IsVerified
          ? PublishedVerificationStatus.Correct
          : PublishedVerificationStatus.Error;
      default:
        throw new ArgumentOutOfRangeException();
    }
  }
}