using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace DafnyDriver.Commands;

public record CliCanVerifyState {
  public Func<IVerificationWork, bool> TaskFilter = _ => true;
  public readonly TaskCompletionSource Finished = new();
  public VerificationRun? Run;
  private readonly HashSet<VerificationWorkId> received = new();
  public int CompletedCount { get { lock (received) { return received.Count; } } }
  public readonly ConcurrentQueue<(IVerificationWork Task, VerificationWorkCompleted Result)> CompletedParts = new();
  public int TaskCount => Run?.ExpectedCount ?? 0;
  public Microsoft.Dafny.ProofDependencyManager? ProofDependencies;

  public int RecordCompletion(IVerificationWork task, VerificationWorkCompleted result) {
    lock (received) {
      if (Run == null || !Run.HasResult(task, result.Result) || !received.Add(task.Source.Id)) {
        throw new InvalidOperationException("CLI received an unexpected verification result");
      }
      CompletedParts.Enqueue((task, result));
      return received.Count;
    }
  }
}
