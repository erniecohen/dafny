#nullable enable
using System;
using System.Reactive.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// A run publishes exactly one terminal result, and only after its observable closes cleanly.
/// Keep that result pending so a duplicate, trailing status or error cannot first publish success.
/// Progress is forwarded immediately; backend evidence and original errors are preserved.
/// </summary>
internal static class VerificationWorkStream {
  public static IObservable<VerificationWorkStatus> Validate(IObservable<VerificationWorkStatus> source) =>
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
          observer.OnNext(terminal);
          observer.OnCompleted();
        }
      });
    });
}
