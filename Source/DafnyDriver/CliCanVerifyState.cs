using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace DafnyDriver.Commands;

public record CliCanVerifyState {
  public Func<IVerificationWorkItem, bool> TaskFilter = _ => true;
  public readonly TaskCompletionSource Finished = new();
  public int CompletedCount = 0;
  public readonly ConcurrentQueue<(IVerificationWorkItem Task, VerificationCompleted Result)> CompletedParts = new();
  public int TaskCount;
}