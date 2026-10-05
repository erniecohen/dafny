// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using Microsoft.Dafny;
using Xunit;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

public class B3ContextCoordinatorTests {
  [Fact]
  public async Task AllDisjointContextsMustCompleteAndAttemptSequencesAreAggregated() {
    var requests = Requests(); var launched = new List<Ir.Request>();
    var result = await B3ContextCoordinator.RunAsync(requests, (request, _) => {
      launched.Add(request); return Task.FromResult(Completion(request, Ir.Outcome.Verified));
    }, CancellationToken.None);
    Assert.Equal(Ir.Outcome.Verified, result.Outcome); Assert.True(result.TraversalCompleted);
    Assert.Equal(2, launched.Count); Assert.Equal(new[] { 0, 1 }, result.Attempts.Select(attempt => attempt.Sequence));
    Assert.All(launched, request => Assert.InRange(request.Configuration.TimeoutMilliseconds, 1, requests[0].Configuration.TimeoutMilliseconds));
    Assert.NotSame(launched[0].Program, launched[1].Program);
  }

  [Theory]
  [InlineData(Ir.Outcome.Failed)]
  [InlineData(Ir.Outcome.Inconclusive)]
  public async Task MathematicalFailureOrUnknownCannotBeWaivedByAnotherVerifiedMask(Ir.Outcome outcome) {
    var requests = Requests(); var count = 0;
    var result = await B3ContextCoordinator.RunAsync(requests, (request, _) =>
      Task.FromResult(Completion(request, count++ == 0 ? outcome : Ir.Outcome.Verified)), CancellationToken.None);
    Assert.Equal(outcome, result.Outcome); Assert.True(result.TraversalCompleted); Assert.Equal(2, count);
  }

  [Fact]
  public async Task FaultCannotLaunchLaterMaskOrReportCompleteTraversal() {
    var count = 0;
    var result = await B3ContextCoordinator.RunAsync(Requests(), (request, _) => {
      count++; return Task.FromResult(Completion(request, Ir.Outcome.ToolError, false));
    }, CancellationToken.None);
    Assert.Equal(Ir.Outcome.ToolError, result.Outcome); Assert.False(result.TraversalCompleted); Assert.Equal(1, count);
  }

  [Fact]
  public async Task CancellationAwaitsOwnedLaunchCleanup() {
    using var cancellation = new CancellationTokenSource(); var drained = false; var count = 0;
    var result = await B3ContextCoordinator.RunAsync(Requests(), async (request, token) => {
      count++; cancellation.Cancel();
      try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
      // The real launcher owns its worker and descendants and returns only after cleanup.
      await Task.Yield(); drained = true; return Completion(request, Ir.Outcome.Cancelled, false);
    }, cancellation.Token);
    Assert.True(drained); Assert.Equal(1, count); Assert.Equal(Ir.Outcome.Cancelled, result.Outcome);
    Assert.False(result.TraversalCompleted);
  }

  [Fact]
  public async Task ValueEqualConfigurationDoesNotRequireArrayReferenceIdentity() {
    var requests = Requests();
    requests[1] = requests[1] with { Configuration = requests[1].Configuration with { SolverArguments = new[] { "-in", "-smt2" } } };
    var result = await B3ContextCoordinator.RunAsync(requests, (request, _) => Task.FromResult(Completion(request, Ir.Outcome.Verified)), CancellationToken.None);
    Assert.Equal(Ir.Outcome.Verified, result.Outcome);
  }

  [Fact]
  public async Task DuplicateOriginalIdentityIsRejectedBeforeLaunching() {
    var requests = Requests(); requests[1] = requests[0]; var count = 0;
    var result = await B3ContextCoordinator.RunAsync(requests, (request, _) => {
      count++; return Task.FromResult(Completion(request, Ir.Outcome.Verified));
    }, CancellationToken.None);
    Assert.Equal(Ir.Outcome.ToolError, result.Outcome); Assert.Equal(0, count);
  }

  private static Ir.Request[] Requests() {
    var configuration = new Ir.Configuration("/pinned/z3", new[] { "-in", "-smt2" }, 10000, 1000000, 1048576, 2, "5.1.0", new string('a', 64));
    return Enumerable.Range(0, 2).Select(index => {
      var id = "sO" + index;
      var program = new Ir.Program(Array.Empty<string>(), Array.Empty<Ir.Function>(), Array.Empty<Ir.Axiom>(),
        new Ir.Unit("sUnit", Array.Empty<Ir.Binding>(), new Ir.Check(id, new Ir.BooleanLiteral(true), true)));
      return new Ir.Request(Ir.Protocol.Version, "request-" + index, Ir.Protocol.NormalizerVersion, Ir.WorkerPackage.UpstreamCommit,
        Ir.Protocol.GetProgramHash(program), program.Unit.Name, program, configuration,
        new[] { new Ir.SourceIdentity(id, "file:///context-control.bpl", 1, 1, "assertion") }, new string('b', 64));
    }).ToArray();
  }
  private static Ir.Completion Completion(Ir.Request request, Ir.Outcome outcome, bool complete = true) => new(
    Ir.Protocol.Version, request.RequestId, request.ProgramHash, request.UnitId, request.B3Commit, complete, outcome,
    request.Obligations.Select((identity, index) => new Ir.Attempt(index, identity.Id, outcome, outcome == Ir.Outcome.Verified ? null : "control")).ToArray(),
    outcome == Ir.Outcome.ToolError ? "controlled fault" : null, request.WorkerFingerprint);
}
