#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Dafny;

/// <summary>Only Verified is a successful proof result. Preparation is never a proof result.</summary>
public enum VerificationOutcome {
  ToolError, Verified, Failed, Inconclusive, Timeout, ResourceExhaustion, OutOfMemory,
  Cancelled, Unsupported, Bounded
}

public record VerificationWorkId(string Scope, int Part);

/// <summary>Source ownership and identity do not require a backend implementation or split.</summary>
public record VerificationSource(VerificationWorkId Id, IOrigin Origin, IOrigin ScopeOrigin);

/// <summary>Metrics are optional; absence must not be interpreted as a zero cost proof.</summary>
public record VerificationMetrics(long? ResourceCount = null, TimeSpan? RunTime = null);

/// <summary>Backend-specific evidence, for example a native model, is kept intact in a payload.</summary>
public interface IVerificationPayload { }

public record VerificationWorkResult(VerificationOutcome Outcome,
  IReadOnlyList<DafnyDiagnostic>? Diagnostics = null, VerificationMetrics? Metrics = null,
  IVerificationPayload? Payload = null) {
  public bool IsVerified => Outcome == VerificationOutcome.Verified;
}

public abstract record VerificationWorkStatus;
public record VerificationWorkStale : VerificationWorkStatus;
public record VerificationWorkQueued : VerificationWorkStatus;
public record VerificationWorkRunning : VerificationWorkStatus;
public record VerificationWorkCompleted(VerificationWorkResult Result) : VerificationWorkStatus;

/// <summary>
/// Scheduling seam. A null TryRun means no new run was started, as on the native path;
/// inspect CacheStatus for a completed cached result. Cancellation/errors remain observable errors.
/// </summary>
public interface IVerificationWork {
  VerificationSource Source { get; }
  VerificationWorkStatus CacheStatus { get; }
  bool IsIdle { get; }
  IVerificationWork FromSeed(int seed);
  IObservable<VerificationWorkStatus>? TryRun();
  void Cancel();
}

/// <summary>
/// Each backend chooses its own typed input. Neither preparation nor work scheduling requires
/// an execution engine in this interface. A future backend can consume a captured translation
/// without constructing native verification tasks.
/// </summary>
public interface IVerificationBackend<in TInput> {
  Task<VerificationPreparation> PrepareAsync(TInput input, CancellationToken cancellationToken);
}

/// <summary>
/// Failure and a successfully prepared empty module are different states. Access to work from
/// a failed preparation rethrows the original error; it cannot silently yield an empty success.
/// </summary>
public sealed class VerificationPreparation {
  private readonly IReadOnlyList<IVerificationWork> work;
  public VerificationOutcome? FailureOutcome { get; }
  public Exception? Error { get; }
  public bool IsReady => Error == null;

  private VerificationPreparation(IReadOnlyList<IVerificationWork> work,
    VerificationOutcome? failureOutcome = null, Exception? error = null) {
    this.work = work;
    FailureOutcome = failureOutcome;
    Error = error;
  }

  public static VerificationPreparation Ready(IEnumerable<IVerificationWork> work) =>
    new(Array.AsReadOnly(work.ToArray()));

  public static VerificationPreparation Failed(VerificationOutcome outcome, Exception error) {
    if (outcome == VerificationOutcome.Verified) {
      throw new ArgumentException("Preparation failure cannot be verified", nameof(outcome));
    }
    ArgumentNullException.ThrowIfNull(error);
    return new VerificationPreparation([], outcome, error);
  }

  public IReadOnlyList<IVerificationWork> GetWorkOrThrow() {
    if (Error != null) {
      ExceptionDispatchInfo.Capture(Error).Throw();
    }
    return work;
  }
}
