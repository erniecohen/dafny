using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test.Verification;

public class VerificationWorkTests {
  [Fact]
  public void DefaultOutcomeCannotClaimProofSuccess() {
    Assert.Equal(VerificationOutcome.ToolError, default(VerificationOutcome));
    Assert.False(new VerificationWorkResult(default).IsVerified);
  }

  [Fact]
  public void OnlyVerifiedIsProofSuccess() {
    foreach (var outcome in Enum.GetValues<VerificationOutcome>().Append((VerificationOutcome)12345)) {
      var result = new VerificationWorkResult(outcome, []);
      Assert.Equal(outcome == VerificationOutcome.Verified, result.IsVerified);
      Assert.Null(result.Metrics);
      Assert.Null(result.Payload);
    }
  }

  [Fact]
  public void FailedPreparationCannotSupplyEmptySuccessfulWork() {
    var error = new ArgumentException("ill-typed backend input");
    var preparation = VerificationPreparation.Failed(VerificationOutcome.ToolError, error);
    Assert.False(preparation.IsReady);
    Assert.Equal(VerificationOutcome.ToolError, preparation.FailureOutcome);
    Assert.Same(error, preparation.Error);
    Assert.Same(error, Assert.Throws<ArgumentException>(() => preparation.GetWorkOrThrow()));
  }

  [Fact]
  public void FailureCannotClaimVerified() {
    Assert.Throws<ArgumentException>(() =>
      VerificationPreparation.Failed(VerificationOutcome.Verified, new Exception()));
    Assert.Throws<ArgumentNullException>(() =>
      VerificationPreparation.Failed(VerificationOutcome.ToolError, null!));
  }

  [Fact]
  public void ReadyEmptyModuleIsExplicitlyDifferentFromFailure() {
    var preparation = VerificationPreparation.Ready([]);
    Assert.True(preparation.IsReady);
    Assert.Null(preparation.FailureOutcome);
    Assert.Null(preparation.Error);
    Assert.Empty(preparation.GetWorkOrThrow());
  }

  [Fact]
  public void PreparationOwnsAnImmutableWorkList() {
    var work = new List<IVerificationWork>();
    var preparation = VerificationPreparation.Ready(work);
    work.Add(null!);
    Assert.Empty(preparation.GetWorkOrThrow());
    Assert.Throws<NotSupportedException>(() =>
      ((IList<IVerificationWork>)preparation.GetWorkOrThrow()).Add(null!));
  }
}
