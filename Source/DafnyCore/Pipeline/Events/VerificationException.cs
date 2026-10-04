#nullable enable
using System;
using Microsoft.Boogie;

namespace Microsoft.Dafny;

public record VerificationException(ICanVerify CanVerify, IVerificationWorkItem Task, Exception Exception) : ICompilationEvent;