#nullable enable
using System;

namespace Microsoft.Dafny;

public record VerificationException(ICanVerify CanVerify, IVerificationWork Task, Exception Exception) : ICompilationEvent {
  public VerificationRun? Run { get; init; }
}
