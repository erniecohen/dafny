#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;

namespace Microsoft.Dafny;

[Flags]
public enum VerificationCapabilities {
  None = 0, SeededVerification = 1, Counterexamples = 2, ProofDependencies = 4,
  ResourceCounts = 8, AssertionIsolation = 16
}

public interface IVerificationBackend : IDisposable {
  string Name { get; }
  VerificationCapabilities Capabilities { get; }
  Task<IReadOnlyList<IVerificationWorkItem>> PrepareAsync(VerificationPreparation input,
    CancellationToken cancellationToken);
}

/// <summary>The shared, pre-VC translation. A backend owns any subsequent mutation.</summary>
public record VerificationPreparation(ResolutionResult Resolution, ModuleDefinition Module,
  Microsoft.Boogie.Program IntermediateProgram) {
  /// <summary>Distinguishes reported translation errors from a legitimate empty program.</summary>
  public bool TranslationHasErrors { get; init; }
}

public record VerificationIdentity(string ScopeId, string Key, int BatchId, int RandomSeed);
public enum VerificationUnitKind { Body, Wellformedness, Override, Other }
public record VerificationSourceInfo(ICanVerify CanVerify, IOrigin ScopeOrigin, IOrigin Origin,
  VerificationUnitKind Kind, string DisplayName, string ProgressDescription,
  IReadOnlyList<Function> HiddenFunctions);

public interface IVerificationWorkItem {
  VerificationIdentity Identity { get; }
  VerificationSourceInfo Source { get; }
  VerificationStatus CacheStatus { get; }
  IVerificationWorkItem FromSeed(int newSeed);
  IObservable<VerificationStatus>? TryRun();
  bool IsIdle { get; }
  void Cancel();
}

public abstract record VerificationStatus;
public record VerificationStale : VerificationStatus;
public record VerificationQueued : VerificationStatus;
public record VerificationRunning : VerificationStatus;
public record VerificationCompleted(VerificationResult Result) : VerificationStatus;

public enum VerificationOutcome {
  Verified, Failed, Unknown, TimedOut, OutOfResource, OutOfMemory, Cancelled,
  Unsupported, ToolError,
  // The existing Boogie bounded outcome is retained for compatibility, never treated as verified by the IDE.
  Bounded
}

public record VerificationAssertion(string Id, IOrigin Origin, IOrigin? SecondaryOrigin,
  string Description, VerificationOutcome Outcome);

public record VerificationWorkItemResult(IVerificationWorkItem Task, VerificationResult Result);

/// <summary>Metrics and model/coverage data may be unavailable. Success requires completed traversal.</summary>
public sealed class VerificationResult {
  public VerificationOutcome Outcome { get; }
  public IReadOnlyList<DafnyDiagnostic> Diagnostics { get; }
  public IReadOnlyList<VerificationAssertion> Assertions { get; }
  public bool TraversalCompleted { get; }
  public DateTime StartTime { get; }
  public TimeSpan RunTime { get; }
  public int? ResourceCount { get; }
  public int ErrorCount { get; }
  public bool HitErrorLimit { get; }
  public VC.VerificationRunResult? BoogieResult { get; }
  public bool IsVerified => Outcome == VerificationOutcome.Verified && TraversalCompleted;

  public VerificationResult(VerificationOutcome outcome, IReadOnlyList<DafnyDiagnostic> diagnostics,
    IReadOnlyList<VerificationAssertion> assertions, bool traversalCompleted,
    DateTime startTime, TimeSpan runTime, int? resourceCount, int errorCount,
    bool hitErrorLimit = false, VC.VerificationRunResult? boogieResult = null) {
    Outcome = outcome == VerificationOutcome.Verified &&
      (!traversalCompleted || errorCount != 0 || diagnostics.Any(diagnostic => diagnostic.Level == ErrorLevel.Error) ||
       assertions.Any(assertion => assertion.Outcome != VerificationOutcome.Verified))
      ? VerificationOutcome.ToolError : outcome;
    Diagnostics = diagnostics;
    Assertions = assertions;
    TraversalCompleted = traversalCompleted;
    StartTime = startTime;
    RunTime = runTime;
    ResourceCount = resourceCount;
    ErrorCount = errorCount;
    HitErrorLimit = hitErrorLimit;
    BoogieResult = boogieResult;
  }
}
