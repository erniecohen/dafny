namespace Microsoft.Dafny;

public record VerificationUpdate(ProofDependencyManager ProofDependencyManager,
  ICanVerify CanVerify, IVerificationWork VerificationTask, VerificationWorkStatus Status) : ICompilationEvent {
  public VerificationRun Run { get; init; }
}
