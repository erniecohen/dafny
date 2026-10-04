#nullable enable
using System;
using System.Reactive.Linq;

namespace Microsoft.Dafny;

public static class VerificationStatusStream {
  /// <summary>
  /// A completion is published only after the stream itself completes normally.
  /// Errors, missing completion, duplicate completion and events after completion cannot publish success.
  /// </summary>
  public static IObservable<VerificationStatus> RequireCompletion(this IObservable<VerificationStatus> source) =>
    Observable.Create<VerificationStatus>(observer => {
      VerificationCompleted? completion = null;
      var stopped = false;
      return source.Subscribe(status => {
        if (stopped) { return; }
        if (completion != null) {
          stopped = true;
          observer.OnError(new InvalidOperationException("Verification produced an event after its terminal result"));
        } else if (status is VerificationCompleted completed) {
          completion = completed;
        } else {
          observer.OnNext(status);
        }
      }, error => {
        if (!stopped) { stopped = true; observer.OnError(error); }
      }, () => {
        if (stopped) { return; }
        stopped = true;
        if (completion == null) {
          observer.OnError(new InvalidOperationException("Verification stream ended without a terminal result"));
        } else {
          observer.OnNext(completion);
          observer.OnCompleted();
        }
      });
    });
}
