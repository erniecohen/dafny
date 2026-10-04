using Microsoft.Boogie;

namespace Microsoft.Dafny;

public record VerificationUpdate(ProofDependencyManager ProofDependencyManager,
  ICanVerify CanVerify, IVerificationWorkItem VerificationTask, VerificationStatus Status)
  : ICompilationEvent {

}