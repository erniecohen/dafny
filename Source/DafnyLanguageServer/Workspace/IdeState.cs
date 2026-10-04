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
  IReadOnlyList<Diagnostic> Diagnostics, bool HitErrorLimit, IVerificationWorkItem Task, VerificationStatus RawStatus);

public enum VerificationPreparationState { NotStarted, InProgress, Done }
public record IdeCanVerifyState(VerificationPreparationState PreparationProgress,
  ImmutableDictionary<string, IdeVerificationTaskState> VerificationTasks,
  IReadOnlyList<Diagnostic> Diagnostics);



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
      case VerificationUpdate boogieUpdate:
        return HandleVerificationUpdate(options, logger, boogieUpdate);
      case CanVerifyPartsIdentified canVerifyPartsIdentified:
        return HandleCanVerifyPartsUpdated(logger, canVerifyPartsIdentified);
      case FinishedParsing finishedParsing:
        return HandleFinishedParsing(finishedParsing);
      case FinishedResolution finishedResolution:
        return HandleFinishedResolution(options, logger, telemetryPublisher, finishedResolution);
      case InternalCompilationException internalCompilationException:
        return HandleInternalCompilationException(internalCompilationException);
      case VerificationException boogieException:
        return HandleVerificationException(boogieException);
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
    var preparationProgress = new[]
      { previousVerificationResult.PreparationProgress, VerificationPreparationState.InProgress }.Max();
    var verificationResult = new IdeCanVerifyState(PreparationProgress: preparationProgress,
      VerificationTasks: previousImplementations.ToImmutableDictionary(kv => kv.Key, kv => kv.Value with {
        Status = PublishedVerificationStatus.Stale,
        Diagnostics = MarkDiagnosticsAsOutdated(kv.Value.Diagnostics).ToList()
      }), previousVerificationResult.Diagnostics);
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
      MergeStates(a.PreparationProgress, b.PreparationProgress),
      a.VerificationTasks.ToImmutableDictionary().Merge(b.VerificationTasks,
        (aView, bView) => aView /* Merge should not occur */),
      a.Diagnostics.Concat(b.Diagnostics)));
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
    return new IdeCanVerifyState(PreparationProgress: VerificationPreparationState.NotStarted,
      VerificationTasks: previousImplementations.ToImmutableDictionary(kv => kv.Key, kv => kv.Value with {
        Status = PublishedVerificationStatus.Stale,
        Diagnostics = MarkDiagnosticsAsOutdated(kv.Value.Diagnostics).ToList()
      }), previousIdeCanVerifyState?.Diagnostics ?? new List<Diagnostic>());
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
    var gutterIconManager = new GutterIconAndHoverVerificationDetailsManager(logger);

    var uri = canVerifyPartsIdentified.CanVerify.Origin.Uri;
    gutterIconManager.ReportWorkItemsBeforeVerification(previousState,
      canVerifyPartsIdentified.CanVerify, canVerifyPartsIdentified.Parts);

    var range = canVerifyPartsIdentified.CanVerify.NavigationRange.ToLspRange();
    var previousImplementations = previousState.CanVerifyStates[uri][range].VerificationTasks;
    var verificationResult = new IdeCanVerifyState(PreparationProgress: VerificationPreparationState.Done,
      VerificationTasks: canVerifyPartsIdentified.Parts.ToImmutableDictionary(Compilation.GetTaskName,
        k => {
          var previous = previousImplementations.GetValueOrDefault(Compilation.GetTaskName(k));
          return new IdeVerificationTaskState(range, PublishedVerificationStatus.Queued,
            previous?.Diagnostics ?? Array.Empty<Diagnostic>(),
            previous?.HitErrorLimit ?? false, k, new VerificationStale());
        }), new List<Diagnostic>());
    return previousState with {
      CanVerifyStates = previousState.CanVerifyStates.SetItem(uri,
        previousState.CanVerifyStates[uri].SetItem(range, verificationResult))
    };
  }

  private IdeState HandleVerificationException(VerificationException boogieException) {
    var previousState = this;

    var name = Compilation.GetTaskName(boogieException.Task);
    var uri = boogieException.CanVerify.Origin.Uri;
    var range = boogieException.CanVerify.NavigationRange.ToLspRange();

    var previousVerificationResult = previousState.CanVerifyStates[uri][range];
    var previousImplementations = previousVerificationResult.VerificationTasks;
    var previousView = previousImplementations.GetValueOrDefault(name);
    var diagnostics = previousView?.Diagnostics ?? Array.Empty<Diagnostic>();
    var hitErrorLimit = previousView?.HitErrorLimit ?? false;

    var internalErrorDiagnostic = new Diagnostic {
      Message = boogieException.Exception.Message,
      Severity = DiagnosticSeverity.Error,
      Range = range
    };
    diagnostics = diagnostics.Concat(new[] { internalErrorDiagnostic }).ToList();

    var view = new IdeVerificationTaskState(range, PublishedVerificationStatus.Error, diagnostics.ToList(), hitErrorLimit, boogieException.Task, new VerificationStale());

    return previousState with {
      CanVerifyStates = previousState.CanVerifyStates.SetItem(uri,
        previousState.CanVerifyStates[uri].SetItem(range, previousVerificationResult with {
          VerificationTasks = previousVerificationResult.VerificationTasks.SetItem(name, view)
        }))
    };
  }

  private IdeState HandleVerificationUpdate(DafnyOptions options, ILogger logger, VerificationUpdate boogieUpdate) {
    var previousState = this;

    var name = Compilation.GetTaskName(boogieUpdate.VerificationTask);
    var status = StatusFromVerificationStatus(boogieUpdate.Status);
    var uri = boogieUpdate.CanVerify.Origin.Uri;
    var range = boogieUpdate.CanVerify.NavigationRange.ToLspRange();

    var previousVerificationResult = previousState.CanVerifyStates[uri][range];
    var previousImplementations = previousVerificationResult.VerificationTasks;
    var previousView = previousImplementations.GetValueOrDefault(name);
    var counterExamples = previousState.Counterexamples;
    bool hitErrorLimit = previousView?.HitErrorLimit ?? false;
    VerificationStatus rawStatus = boogieUpdate.Status;
    var diagnostics = previousView?.Diagnostics ?? Array.Empty<Diagnostic>();
    if (boogieUpdate.Status is VerificationRunning) {
      diagnostics = Array.Empty<Diagnostic>();
      hitErrorLimit = false;
    }

    if (boogieUpdate.Status is VerificationCompleted completed) {
      // WarnContradictoryAssumptions should be computable after completing a single assertion batch.
      // And we should do this because it allows this warning to be shown when doing --filter-position on a single assertion
      // https://github.com/dafny-lang/dafny/issues/5039 

      if (completed.Result.BoogieResult != null) {
        counterExamples = counterExamples.Concat(completed.Result.BoogieResult.CounterExamples);
      }
      hitErrorLimit |= completed.Result.HitErrorLimit;
      var newDiagnostics =
        Compilation.GetDiagnosticsFromResult(options, previousState.Uri, boogieUpdate.CanVerify,
          boogieUpdate.VerificationTask, completed.Result);
      diagnostics = newDiagnostics.Select(d => d.ToLspDiagnostic()).ToList();
      logger.LogTrace(
        $"Completed received for {previousState.Input} and found #{diagnostics.Count} diagnostics and #{completed.Result.ErrorCount} counterexamples.");
    }

    var newCanVerifyDiagnostics = new List<Diagnostic>();
    var taskState = new IdeVerificationTaskState(range, status, diagnostics.ToList(),
      hitErrorLimit, boogieUpdate.VerificationTask, rawStatus);
    var newTaskStates = previousVerificationResult.VerificationTasks.SetItem(name, taskState);

    var scopeGroup = newTaskStates.Values.Where(s => s.Task.Identity.ScopeId == boogieUpdate.VerificationTask.Identity.ScopeId).ToList();
    var allTasksAreCompleted = scopeGroup.All(s => s.RawStatus is VerificationCompleted);
    if (allTasksAreCompleted) {

      var errorReporter = new ObservableErrorReporter(options, uri);
      List<DafnyDiagnostic> verificationCoverageDiagnostics = [];
      errorReporter.Updates.Subscribe(d => verificationCoverageDiagnostics.Add(d.Diagnostic));

      ProofDependencyWarnings.ReportSuspiciousDependencies(options,
        scopeGroup.Select(s => new VerificationWorkItemResult(s.Task, ((VerificationCompleted)s.RawStatus).Result)),
        errorReporter, boogieUpdate.ProofDependencyManager);

      newCanVerifyDiagnostics = previousVerificationResult.Diagnostics.Concat(verificationCoverageDiagnostics.Select(d => d.ToLspDiagnostic())).ToList();
    }

    UpdateGutterIconTrees(logger, boogieUpdate, scopeGroup);

    return previousState with {
      Counterexamples = counterExamples,
      CanVerifyStates = previousState.CanVerifyStates.SetItem(uri,
        previousState.CanVerifyStates[uri].SetItem(range, previousVerificationResult with {
          VerificationTasks = newTaskStates,
          Diagnostics = newCanVerifyDiagnostics
        }))
    };
  }

  private void UpdateGutterIconTrees(ILogger logger, VerificationUpdate update, IReadOnlyList<IdeVerificationTaskState> scopeGroup) {
    var gutter = new GutterIconAndHoverVerificationDetailsManager(logger);
    if (update.Status is VerificationRunning && scopeGroup.Count(e => e.Status == PublishedVerificationStatus.Running) == 1) {
      gutter.ReportWorkItemRunning(this, update.VerificationTask);
    }
    if (update.Status is VerificationCompleted completed) {
      gutter.ReportWorkItemResult(this, update.VerificationTask, completed.Result);
    }
    if (scopeGroup.All(e => e.RawStatus is VerificationCompleted)) {
      var results = scopeGroup.Select(e => ((VerificationCompleted)e.RawStatus).Result).ToList();
      foreach (var item in scopeGroup) {
        gutter.ReportWorkItemResult(this, item.Task, ((VerificationCompleted)item.RawStatus).Result);
      }
      gutter.ReportEndVerifyWorkItem(this, update.VerificationTask, results);
    }
  }

  private static PublishedVerificationStatus StatusFromVerificationStatus(VerificationStatus verificationStatus) {
    switch (verificationStatus) {
      case VerificationStale:
        return PublishedVerificationStatus.Stale;
      case VerificationQueued:
        return PublishedVerificationStatus.Queued;
      case VerificationRunning:
        return PublishedVerificationStatus.Running;
      case VerificationCompleted { Result.Outcome: VerificationOutcome.Cancelled }:
        return PublishedVerificationStatus.Stale;
      case VerificationCompleted completed:
        return completed.Result.IsVerified
          ? PublishedVerificationStatus.Correct
          : PublishedVerificationStatus.Error;
      default:
        throw new ArgumentOutOfRangeException();
    }
  }
}