using Microsoft.Dafny;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DafnyCore.Test;

public class VerificationContractsTest {
  private static VerificationResult Result(VerificationOutcome outcome, bool complete,
    int errors = 0, IReadOnlyList<VerificationAssertion>? assertions = null) =>
    new(outcome, Array.Empty<DafnyDiagnostic>(), assertions ?? Array.Empty<VerificationAssertion>(), complete,
      DateTime.UnixEpoch, TimeSpan.Zero, null, errors);

  [Fact]
  public void CompletedEmptyTraversalCanVerifyWithoutInventingMetrics() {
    var result = Result(VerificationOutcome.Verified, true);
    Assert.True(result.IsVerified);
    Assert.Null(result.ResourceCount);
    Assert.Null(result.BoogieResult);
  }

  [Fact]
  public void MissingTraversalCompletionCannotVerify() {
    var result = Result(VerificationOutcome.Verified, false);
    Assert.Equal(VerificationOutcome.ToolError, result.Outcome);
    Assert.False(result.IsVerified);
  }

  [Fact]
  public void FailedObligationCannotBeOverwrittenBySuccessfulCompletion() {
    var assertions = new[] { new VerificationAssertion("check-1", Token.NoToken, null,
      "assertion", VerificationOutcome.Failed) };
    var result = Result(VerificationOutcome.Verified, true, assertions: assertions);
    Assert.Equal(VerificationOutcome.ToolError, result.Outcome);
    Assert.False(result.IsVerified);
  }

  [Theory]
  [InlineData(VerificationOutcome.Failed)]
  [InlineData(VerificationOutcome.Unknown)]
  [InlineData(VerificationOutcome.TimedOut)]
  [InlineData(VerificationOutcome.OutOfResource)]
  [InlineData(VerificationOutcome.OutOfMemory)]
  [InlineData(VerificationOutcome.Cancelled)]
  [InlineData(VerificationOutcome.Unsupported)]
  [InlineData(VerificationOutcome.ToolError)]
  public void NonSuccessOutcomesRemainDistinct(VerificationOutcome outcome) {
    var result = Result(outcome, true);
    Assert.Equal(outcome, result.Outcome);
    Assert.False(result.IsVerified);
  }
  [Fact]
  public void CompletionIsWithheldUntilTheStreamEndsNormally() {
    using var source = new Subject<VerificationStatus>();
    var received = new List<VerificationStatus>();
    source.RequireCompletion().Subscribe(received.Add);
    source.OnNext(new VerificationRunning());
    source.OnNext(new VerificationCompleted(Result(VerificationOutcome.Verified, true)));
    Assert.Single(received);
    source.OnCompleted();
    Assert.IsType<VerificationCompleted>(received.Last());
  }

  [Fact]
  public void StreamErrorAfterSuccessCannotPublishSuccess() {
    using var source = new Subject<VerificationStatus>();
    var received = new List<VerificationStatus>();
    Exception? error = null;
    source.RequireCompletion().Subscribe(received.Add, e => error = e);
    source.OnNext(new VerificationCompleted(Result(VerificationOutcome.Verified, true)));
    source.OnError(new InvalidOperationException("worker crashed"));
    Assert.Empty(received);
    Assert.NotNull(error);
  }

  [Fact]
  public void MissingOrDuplicateTerminalRecordCannotPublishSuccess() {
    foreach (var events in new[] {
      Array.Empty<VerificationStatus>(),
      new VerificationStatus[] { new VerificationCompleted(Result(VerificationOutcome.Verified, true)),
        new VerificationCompleted(Result(VerificationOutcome.Verified, true)) }
    }) {
      var received = new List<VerificationStatus>();
      Exception? error = null;
      events.ToObservable().RequireCompletion().Subscribe(received.Add, e => error = e);
      Assert.Empty(received);
      Assert.NotNull(error);
    }
  }

}
