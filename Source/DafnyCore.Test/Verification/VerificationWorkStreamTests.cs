using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test.Verification;

public class VerificationWorkStreamTests {
  public static IEnumerable<object[]> Outcomes => Enum.GetValues<VerificationOutcome>().Select(o => new object[] { o });

  [Theory]
  [MemberData(nameof(Outcomes))]
  public void EveryOutcomeRetainsIdentityAndWaitsForCleanCompletion(VerificationOutcome outcome) {
    using var source = new Subject<VerificationWorkStatus>();
    var received = new List<VerificationWorkStatus>();
    var closed = false;
    using var subscription = VerificationWorkStream.Validate(source).Subscribe(received.Add, () => closed = true);
    var queued = new VerificationWorkQueued();
    var running = new VerificationWorkRunning();
    var result = new VerificationWorkResult(outcome, [], new(123, TimeSpan.FromSeconds(2)), new Evidence());
    var terminal = new VerificationWorkCompleted(result);
    source.OnNext(queued);
    source.OnNext(running);
    Assert.Equal(new VerificationWorkStatus[] { queued, running }, received);
    source.OnNext(terminal);
    Assert.Equal(2, received.Count);
    Assert.False(closed);
    source.OnCompleted();
    Assert.True(closed);
    Assert.Same(terminal, received[2]);
    Assert.Same(result, ((VerificationWorkCompleted)received[2]).Result);
    Assert.Equal(outcome == VerificationOutcome.Verified, result.IsVerified);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public void ErrorsAndCancellationDiscardAnyPendingResult(bool afterResult, bool cancelled) {
    using var source = new Subject<VerificationWorkStatus>();
    var results = new List<VerificationWorkStatus>();
    Exception? failure = null;
    var closed = false;
    using var subscription = VerificationWorkStream.Validate(source).Subscribe(results.Add, e => failure = e, () => closed = true);
    if (afterResult) { source.OnNext(new VerificationWorkCompleted(new(VerificationOutcome.Verified))); }
    var error = cancelled ? (Exception)new OperationCanceledException() : new InvalidOperationException("worker error");
    source.OnError(error);
    Assert.Same(error, failure);
    Assert.Empty(results);
    Assert.False(closed);
  }

  [Fact]
  public async Task EachSubscriptionOwnsItsTerminalAndDisposalUnsubscribes() {
    var terminal = new VerificationWorkCompleted(new(VerificationOutcome.Failed));
    var checkedStream = VerificationWorkStream.Validate(Observable.Return<VerificationWorkStatus>(terminal));
    Assert.Same(terminal, await checkedStream.SingleAsync().ToTask());
    Assert.Same(terminal, await checkedStream.SingleAsync().ToTask());
    using var source = new Subject<VerificationWorkStatus>();
    var results = new List<VerificationWorkStatus>();
    var subscription = VerificationWorkStream.Validate(source).Subscribe(results.Add);
    Assert.True(source.HasObservers);
    source.OnNext(terminal);
    subscription.Dispose();
    Assert.False(source.HasObservers);
    source.OnCompleted();
    Assert.Empty(results);
  }

  [Fact]
  public void ProtocolFailureIsReportedOnceAndDropsSubsequentMessages() {
    using var source = new Subject<VerificationWorkStatus>();
    var results = new List<VerificationWorkStatus>();
    var errors = new List<Exception>();
    var closed = false;
    using var subscription = VerificationWorkStream.Validate(source).Subscribe(results.Add, errors.Add, () => closed = true);
    source.OnNext(new UnknownStatus());
    source.OnNext(new VerificationWorkCompleted(new(VerificationOutcome.Verified)));
    source.OnError(new Exception("late error"));
    Assert.IsType<InvalidOperationException>(Assert.Single(errors));
    Assert.Empty(results);
    Assert.False(closed);
  }

  [Theory]
  [InlineData("empty", 0)]
  [InlineData("progress-only", 1)]
  [InlineData("duplicate", 0)]
  [InlineData("after-terminal", 0)]
  [InlineData("unknown-status", 0)]
  [InlineData("unknown-outcome", 0)]
  [InlineData("null-result", 0)]
  public void EnvelopeChecksRejectMalformedStreamsIndependentlyOfTheLedger(string script, int progressCount) {
    var terminal = new VerificationWorkCompleted(new(VerificationOutcome.Verified));
    VerificationWorkStatus[] statuses = script switch {
      "empty" => [],
      "progress-only" => [new VerificationWorkRunning()],
      "duplicate" => [terminal, terminal],
      "after-terminal" => [terminal, new VerificationWorkRunning()],
      "unknown-status" => [new UnknownStatus()],
      "unknown-outcome" => [new VerificationWorkCompleted(new((VerificationOutcome)12345))],
      "null-result" => [new VerificationWorkCompleted(null!)],
      _ => throw new ArgumentOutOfRangeException(nameof(script))
    };
    var received = new List<VerificationWorkStatus>();
    var errors = new List<Exception>();
    var closed = false;
    using var subscription = VerificationWorkStream.Validate(statuses.ToObservable())
      .Subscribe(received.Add, errors.Add, () => closed = true);
    Assert.IsType<InvalidOperationException>(Assert.Single(errors));
    Assert.False(closed);
    Assert.Equal(progressCount, received.Count);
    Assert.All(received, status => Assert.IsType<VerificationWorkRunning>(status));
  }

  private sealed record Evidence : IVerificationPayload;
  private sealed record UnknownStatus : VerificationWorkStatus;
}
