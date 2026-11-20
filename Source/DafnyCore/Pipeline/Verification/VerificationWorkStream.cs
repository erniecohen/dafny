#nullable enable
using System;
using System.Reactive.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// A run has exactly one terminal result followed by clean observable completion.
/// Validation holds the result pending; native auditing preserves immediate publication.
/// Progress is forwarded immediately; backend evidence and original errors are preserved.
/// </summary>
internal static class VerificationWorkStream {
  public static IObservable<VerificationWorkStatus> Validate(IObservable<VerificationWorkStatus> source) =>
    Check(source, publishBeforeClosure: false);

  /// <summary>Audit native closure without moving the engine's result publication point.</summary>
  public static IObservable<VerificationWorkStatus> Audit(IObservable<VerificationWorkStatus> source) =>
    Check(source, publishBeforeClosure: true);

  private static IObservable<VerificationWorkStatus> Check(IObservable<VerificationWorkStatus> source,
    bool publishBeforeClosure) =>
    Observable.Create<VerificationWorkStatus>(observer => {
      VerificationWorkCompleted? terminal = null;
      var stopped = false;
      void Fail(Exception error) {
        stopped = true;
        observer.OnError(error);
      }
      return source.Synchronize().Subscribe(status => {
        if (stopped) { return; }
        if (terminal != null) {
          Fail(new InvalidOperationException("Verification work emitted a status after its terminal result"));
        } else if (status is VerificationWorkCompleted completed) {
          if (completed.Result == null || !Enum.IsDefined(completed.Result.Outcome)) {
            Fail(new InvalidOperationException("Verification work emitted an invalid terminal result"));
          } else {
            terminal = completed;
            if (publishBeforeClosure) { observer.OnNext(terminal); }
          }
        } else if (status is VerificationWorkStale or VerificationWorkQueued or VerificationWorkRunning) {
          observer.OnNext(status);
        } else {
          Fail(new InvalidOperationException("Verification work emitted an unknown status"));
        }
      }, error => {
        if (!stopped) { Fail(error); }
      }, () => {
        if (stopped) { return; }
        if (terminal == null) {
          Fail(new InvalidOperationException("Verification work completed without a terminal result"));
        } else {
          if (!publishBeforeClosure) { observer.OnNext(terminal); }
          observer.OnCompleted();
        }
      });
    });
}
