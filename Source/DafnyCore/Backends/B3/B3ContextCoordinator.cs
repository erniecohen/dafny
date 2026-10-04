// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

public sealed record B3ContextCompletion(Ir.Outcome Outcome, bool TraversalCompleted,
  IReadOnlyList<Ir.Attempt> Attempts, string? Error);

/// <summary>One original-unit deadline and one terminal result for disjoint fresh-session contexts.</summary>
public static class B3ContextCoordinator {
  // The host-owned launcher creates a fresh process/session and drains it before completion,
  // including cancellation. Do not return merely because a WaitAsync wrapper cancelled.
  public static async Task<B3ContextCompletion> RunAsync(IReadOnlyList<Ir.Request> requests,
    Func<Ir.Request, CancellationToken, Task<Ir.Completion>> launch, CancellationToken cancellationToken) {
    if (requests.Count is < 1 or > B3DefinitionContexts.MaximumContexts) {
      return new(Ir.Outcome.ToolError, false, Array.Empty<Ir.Attempt>(), "Invalid definition context count");
    }
    var initial = requests[0];
    var ids = new HashSet<string>(StringComparer.Ordinal);
    if (initial.Configuration.TimeoutMilliseconds <= 0 || requests.Any(request => request.UnitId != initial.UnitId ||
        request.B3Commit != initial.B3Commit || request.WorkerFingerprint != initial.WorkerFingerprint ||
        !SameConfiguration(request.Configuration, initial.Configuration) || request.Obligations.Any(identity => !ids.Add(identity.Id)))) {
      return new(Ir.Outcome.ToolError, false, Array.Empty<Ir.Attempt>(), "Definition requests disagree or duplicate original checks");
    }
    var clock = Stopwatch.StartNew();
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    deadline.CancelAfter(initial.Configuration.TimeoutMilliseconds);
    var attempts = new List<Ir.Attempt>();
    var outcomes = new List<Ir.Outcome>();
    var errors = new List<string>();
    var complete = true; var completed = 0;
    foreach (var original in requests) {
      try {
        cancellationToken.ThrowIfCancellationRequested();
        var remaining = (long)initial.Configuration.TimeoutMilliseconds - (long)Math.Ceiling(clock.Elapsed.TotalMilliseconds);
        if (remaining < 1 || deadline.IsCancellationRequested) {
          outcomes.Add(Ir.Outcome.TimedOut); complete = false; errors.Add("Original B3 unit deadline expired between definition contexts"); break;
        }
        var request = original with { Configuration = original.Configuration with { TimeoutMilliseconds = (int)remaining } };
        Ir.ProtocolValidation.ValidateRequest(request);
        var completion = await launch(request, deadline.Token);
        Ir.ProtocolValidation.ValidateCompletion(request, completion);
        completed++;
        if ((long)attempts.Count + completion.Attempts.Count > Ir.Protocol.MaximumNodes) {
          outcomes.Add(Ir.Outcome.ToolError); complete = false;
          errors.Add("Definition context aggregate exceeds its attempt bound"); break;
        }
        foreach (var attempt in completion.Attempts) { attempts.Add(attempt with { Sequence = attempts.Count }); }
        var outcome = completion.Outcome == Ir.Outcome.Cancelled && deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested
          ? Ir.Outcome.TimedOut : completion.Outcome;
        outcomes.Add(outcome); complete &= completion.TraversalCompleted;
        if (completion.Error != null) { errors.Add(completion.Error); }
        // Every selected group is still required for success. A terminal infrastructure
        // or deadline fault cannot launch a later context; mathematical failures may continue.
        if (outcome is Ir.Outcome.ToolError or Ir.Outcome.Cancelled or Ir.Outcome.TimedOut or Ir.Outcome.OutOfMemory) {
          complete = false; break;
        }
      } catch (OperationCanceledException) {
        outcomes.Add(cancellationToken.IsCancellationRequested ? Ir.Outcome.Cancelled : Ir.Outcome.TimedOut);
        complete = false; errors.Add("Original B3 unit execution was cancelled or reached its deadline"); break;
      } catch (Exception exception) {
        outcomes.Add(Ir.Outcome.ToolError); complete = false; errors.Add(exception.Message); break;
      }
    }
    complete &= completed == requests.Count;
    var result = outcomes.Contains(Ir.Outcome.ToolError) ? Ir.Outcome.ToolError :
      outcomes.Contains(Ir.Outcome.Cancelled) ? Ir.Outcome.Cancelled :
      outcomes.Contains(Ir.Outcome.TimedOut) ? Ir.Outcome.TimedOut :
      outcomes.Contains(Ir.Outcome.OutOfMemory) ? Ir.Outcome.OutOfMemory :
      outcomes.Contains(Ir.Outcome.Failed) ? Ir.Outcome.Failed :
      outcomes.FirstOrDefault(outcome => outcome != Ir.Outcome.Verified, Ir.Outcome.Verified);
    if (result == Ir.Outcome.Verified && (!complete || attempts.Any(attempt => attempt.Outcome != Ir.Outcome.Verified))) {
      result = Ir.Outcome.ToolError; errors.Add("Definition context aggregate was incomplete");
    }
    return new(result, complete, attempts.ToArray(), errors.Count == 0 ? null : string.Join("; ", errors));
  }
  private static bool SameConfiguration(Ir.Configuration first, Ir.Configuration second) =>
    first.SolverExecutable == second.SolverExecutable && first.SolverArguments.SequenceEqual(second.SolverArguments) &&
    first.TimeoutMilliseconds == second.TimeoutMilliseconds && first.ResourceLimit == second.ResourceLimit &&
    first.MaximumResponseCharacters == second.MaximumResponseCharacters && first.ArithmeticSolver == second.ArithmeticSolver &&
    first.SolverVersion == second.SolverVersion && first.SolverSha256 == second.SolverSha256;
}
