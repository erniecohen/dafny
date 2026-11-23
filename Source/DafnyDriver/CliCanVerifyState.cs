using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace DafnyDriver.Commands;

public record CliCanVerifyState {
  public Func<IVerificationWork, bool> TaskFilter = _ => true;
  public readonly TaskCompletionSource Finished = new();
  public int CompletedCount = 0;
  public readonly ConcurrentQueue<(IVerificationWork Task, VerificationWorkCompleted Result)> CompletedParts = new();
  public int TaskCount;
  public Microsoft.Dafny.ProofDependencyManager? ProofDependencies;
}