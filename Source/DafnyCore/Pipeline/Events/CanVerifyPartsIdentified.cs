using System.Collections.Generic;

namespace Microsoft.Dafny;

public record CanVerifyPartsIdentified(ICanVerify CanVerify, IReadOnlyList<IVerificationWork> Parts) : ICompilationEvent {
}