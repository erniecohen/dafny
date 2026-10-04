using Microsoft.Dafny;

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
}
