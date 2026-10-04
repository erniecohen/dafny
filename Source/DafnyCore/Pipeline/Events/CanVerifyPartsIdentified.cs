using System.Collections.Generic;
using Microsoft.Boogie;

namespace Microsoft.Dafny;

public record CanVerifyPartsIdentified(ICanVerify CanVerify, IReadOnlyList<IVerificationWorkItem> Parts) : ICompilationEvent {
}