namespace Microsoft.Dafny;

public record VerificationUpdate(ProofDependencyManager ProofDependencyManager,
  ICanVerify CanVerify, IVerificationWork VerificationTask, VerificationWorkStatus Status) : ICompilationEvent;
