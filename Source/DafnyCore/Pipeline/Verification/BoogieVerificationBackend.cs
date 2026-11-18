#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Dafny.Caching;
using VC;

namespace Microsoft.Dafny;

/// <summary>
/// The native backend owns preparation's mutating Boogie calls. Translation, synchronized
/// large-stack execution and pre-preparation printing remain owned by the front end.
/// </summary>
public sealed class BoogieVerificationBackend(ExecutionEngine engine, TaskCacheSession? resultCache = null)
  : IVerificationBackend<Boogie.Program> {
  public async Task<VerificationPreparation> PrepareAsync(Boogie.Program input, CancellationToken cancellationToken) {
    IReadOnlyList<IVerificationTask> tasks;
    try {
      tasks = await engine.GetVerificationTasks(input, cancellationToken);
    } catch (ArgumentException error) {
      // GetVerificationTasks uses ArgumentException for admission errors. Keep its original
      // exception for native consumers while making the failed state explicit at the seam.
      return VerificationPreparation.Failed(VerificationOutcome.ToolError, error);
    }
    if (resultCache != null) { tasks = tasks.Select(resultCache.Wrap).ToList(); }
    return VerificationPreparation.Ready(tasks.Select(task => new BoogieVerificationWork(task)));
  }

  /// <summary>
  /// Temporary bridge for native CLI/IDE consumers. Never fabricate native objects for other
  /// backends: a failed preparation throws, and a foreign work item is rejected.
  /// </summary>
  public static IReadOnlyList<IVerificationTask> GetNativeTasks(VerificationPreparation preparation) =>
    preparation.GetWorkOrThrow().Select(work => work is BoogieVerificationWork native
      ? native.NativeTask
      : throw new InvalidOperationException("Expected Boogie verification work")).ToList();
}

/// <summary>Native evidence retains its object identity, including models and assertion batches.</summary>
public sealed record BoogieVerificationResult(VerificationRunResult NativeResult) : IVerificationPayload;

public sealed class BoogieVerificationWork(IVerificationTask nativeTask) : IVerificationWork {
  public IVerificationTask NativeTask { get; } = nativeTask;
  public VerificationSource Source => new(
    new VerificationWorkId(NativeTask.ScopeId, NativeTask.Split.SplitIndex),
    BoogieGenerator.ToDafnyToken(NativeTask.Token), BoogieGenerator.ToDafnyToken(NativeTask.ScopeToken));
  public VerificationWorkStatus CacheStatus => ConvertStatus(NativeTask.CacheStatus);
  public bool IsIdle => NativeTask.IsIdle;
  public IVerificationWork FromSeed(int seed) => new BoogieVerificationWork(NativeTask.FromSeed(seed));
  public IObservable<VerificationWorkStatus>? TryRun() => NativeTask.TryRun()?.Select(ConvertStatus);
  public void Cancel() => NativeTask.Cancel();

  internal static VerificationWorkStatus ConvertStatus(IVerificationStatus status) => status switch {
    Stale => new VerificationWorkStale(),
    Queued => new VerificationWorkQueued(),
    Running => new VerificationWorkRunning(),
    Completed completed => new VerificationWorkCompleted(new VerificationWorkResult(
      ConvertOutcome(completed.Result.Outcome), null,
      new VerificationMetrics(completed.Result.ResourceCount, completed.Result.RunTime),
      new BoogieVerificationResult(completed.Result))),
    _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown Boogie verification status")
  };

  internal static VerificationOutcome ConvertOutcome(SolverOutcome outcome) => outcome switch {
    SolverOutcome.Valid => VerificationOutcome.Verified,
    SolverOutcome.Invalid => VerificationOutcome.Failed,
    SolverOutcome.Undetermined => VerificationOutcome.Inconclusive,
    SolverOutcome.TimeOut => VerificationOutcome.Timeout,
    SolverOutcome.OutOfResource => VerificationOutcome.ResourceExhaustion,
    SolverOutcome.OutOfMemory => VerificationOutcome.OutOfMemory,
    // Bounded success is distinct at the neutral seam. The native bridge preserves its
    // existing treatment by Compilation.GetOutcome without changing a shipped verdict.
    SolverOutcome.Bounded => VerificationOutcome.Bounded,
    _ => VerificationOutcome.ToolError
  };
}
