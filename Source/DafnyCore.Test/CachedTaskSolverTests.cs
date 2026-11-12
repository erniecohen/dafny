using System.Text;
using System.Text.Json;
using Microsoft.Boogie;
using Microsoft.Boogie.SMTLib;
using Microsoft.Dafny.Caching;
using Xunit;
using BplType = Microsoft.Boogie.Type;

namespace Microsoft.Dafny.Test;

public class CachedTaskSolverTests : IDisposable {
  private readonly string directory = Path.Combine(Path.GetTempPath(), "dafny-exchanges-" + Guid.NewGuid().ToString("N"));
  private int starts;
  private static SExpr S(string name, params SExpr[] children) => new(name, children);
  private CachedTaskSolver Transport(string reason = "unknown", bool warningsHaveNoEffects = false, string? warning = null) => new(new TaskResultCache(new ContentAddressedCache(directory)),
    () => Encoding.UTF8.GetBytes("frozen adaptive execution program"), () => {
      starts++;
      return new Solver(reason, warning);
    }, warningsHaveNoEffects);

  private static void Setup(CachedTaskSolver transport) {
    transport.NewProblem("current split");
    transport.Send("(set-option :timeout 0)");
    transport.Send("(set-option :rlimit 100)");
    transport.Send("(assert true)");
  }

  private static async Task Finish(CachedTaskSolver transport) {
    transport.Send("; Valid");
    await transport.PingPong();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CompleteExchangeReplaysWithoutStartingAnotherSolver(bool retireBeforeCompletion) {
    var cold = Transport();
    Setup(cold);
    Assert.Equal("unsat", (await cold.SendRequest("(check-sat)")).Name);
    await Finish(cold);
    if (retireBeforeCompletion) { cold.Close(); }
    Assert.Equal(CachePublication.Stored, cold.CompleteNormally(CacheTaskOutcome.Correct, 17));
    cold.Close();
    var warm = Transport();
    Setup(warm);
    Assert.Equal("unsat", (await warm.SendRequest("(check-sat)")).Name);
    await Finish(warm);
    warm.Close();
    // Cleanup may have retired a checker before its completion is observed.
    Assert.Equal(CachePublication.AlreadyPresent, warm.CompleteNormally(CacheTaskOutcome.Correct, 17));
    Assert.True(warm.Replayed);
    Assert.Equal(1, starts);
  }

  [Fact]
  public async Task RetirementBeforeTheFinalHandshakeCannotPublishAResult() {
    var cold = Transport();
    Setup(cold);
    await cold.SendRequest("(check-sat)");
    cold.Send("; Valid");
    cold.Close();
    Assert.Equal(CachePublication.Unavailable, cold.CompleteNormally(CacheTaskOutcome.Correct, 17));
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task RetirementOvertakingAnInFlightHandshakeWaitsForItsEvidence(bool valid) {
    var handshake = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var cold = new CachedTaskSolver(new TaskResultCache(new ContentAddressedCache(directory)),
      () => Encoding.UTF8.GetBytes("frozen adaptive execution program"), () => {
        starts++;
        return new Solver("canceled", handshake: handshake.Task, response: valid ? "unsat" : "unknown");
      });
    Setup(cold);
    await cold.SendRequest("(check-sat)");
    cold.Send(valid ? "; Valid" : "; Out of resource");
    var pending = cold.PingPong();
    // Model the pinned checker's retirement notification overtaking its final
    // handshake continuation, without relying on wall-clock scheduling in a test.
    cold.Close();
    var finishing = cold.AwaitPendingHandshake();
    Assert.False(finishing.IsCompleted);
    Assert.False(Directory.Exists(Path.Combine(directory, "v1", "results")));
    handshake.SetResult();
    await pending;
    await finishing;
    var outcome = valid ? CacheTaskOutcome.Correct : CacheTaskOutcome.OutOfResource;
    Assert.Equal(CachePublication.Stored, cold.CompleteNormally(outcome, 17));

    var warm = Transport();
    Setup(warm);
    Assert.Equal(valid ? "unsat" : "unknown", (await warm.SendRequest("(check-sat)")).Name);
    warm.Send(valid ? "; Valid" : "; Out of resource");
    await warm.PingPong();
    await warm.AwaitPendingHandshake();
    Assert.Equal(CachePublication.AlreadyPresent, warm.CompleteNormally(outcome, 17));
    Assert.True(warm.Replayed);
    Assert.Equal(1, starts);
    warm.Close();
  }

  [Fact]
  public async Task AFailedInFlightHandshakeStillCannotPublishAResult() {
    var handshake = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var cold = new CachedTaskSolver(new TaskResultCache(new ContentAddressedCache(directory)),
      () => Encoding.UTF8.GetBytes("frozen adaptive execution program"),
      () => new Solver("canceled", handshake: handshake.Task));
    Setup(cold);
    await cold.SendRequest("(check-sat)");
    cold.Send("; Valid");
    var pending = cold.PingPong();
    cold.Close();
    handshake.SetException(new IOException("retired transport failed"));
    await Assert.ThrowsAsync<IOException>(() => pending);
    await cold.AwaitPendingHandshake();
    Assert.Equal(CachePublication.Unavailable, cold.CompleteNormally(CacheTaskOutcome.Correct, 17));
  }

  [Theory]
  [InlineData(true, "WARNING: ignored pattern", true)]
  [InlineData(false, "WARNING: displayed pattern", false)]
  [InlineData(true, "solver error", false)]
  [InlineData(true, "WARNING: ignored pattern\nsolver error", false)]
  public async Task OnlyWarningsWithNoObservableCallbackEffectsCanBeOmitted(bool ignored, string message, bool reusable) {
    var cold = Transport(warningsHaveNoEffects: ignored, warning: message);
    Setup(cold);
    await cold.SendRequest("(check-sat)");
    await Finish(cold);
    Assert.Equal(reusable ? CachePublication.Stored : CachePublication.Unavailable,
      cold.CompleteNormally(CacheTaskOutcome.Correct, 17));
    cold.Close();
    if (reusable) {
      var warm = Transport(warningsHaveNoEffects: ignored);
      Setup(warm);
      await warm.SendRequest("(check-sat)");
      await Finish(warm);
      Assert.Equal(CachePublication.AlreadyPresent, warm.CompleteNormally(CacheTaskOutcome.Correct, 17));
      Assert.Equal(1, starts);
      warm.Close();
    }
  }

  [Fact]
  public async Task DivergenceRetiresTheCheckerBeforeAnOwnerRetriesTheTask() {
    var cold = Transport();
    Setup(cold);
    await cold.SendRequest("(check-sat)");
    await Finish(cold);
    cold.CompleteNormally(CacheTaskOutcome.Correct, 17);
    cold.Close();
    var warm = Transport();
    Setup(warm);
    await warm.SendRequest("(check-sat)");
    await Assert.ThrowsAsync<CacheReplayMismatchException>(() => warm.SendRequest("(unexpected-command)"));
    // Pinned Checker.GoBackToIdle catches TimeoutException to free the pool slot.
    await Assert.ThrowsAsync<TimeoutException>(warm.PingPong);
    Assert.True(warm.ReplayMismatch);
    warm.Abort();
    warm.Close();
    Assert.Equal(1, starts);
    var next = Transport();
    Setup(next);
    await next.SendRequest("(check-sat)");
    Assert.False(next.Replayed);
    Assert.Equal(2, starts);
    next.Close();
  }

  [Theory]
  [InlineData("memout")]
  [InlineData("timeout")]
  [InlineData("unknown")]
  public async Task AnEarlierCounterexampleDoesNotHideANonreproducibleLaterQuery(string reason) {
    var task = Transport(reason);
    Setup(task);
    await task.SendRequest("(check-sat)");
    await task.SendRequest("(get-info :reason-unknown)");
    await Finish(task);
    Assert.Equal(CachePublication.Unavailable, task.CompleteNormally(CacheTaskOutcome.Errors, 17));
    Assert.False(Directory.Exists(Path.Combine(directory, "v1", "results")));
    task.Close();
  }

  [Fact]
  public void UsedTypesOutsideTheInitialInputAndTheirOrderChangeTheTaskKey() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    var a = new TypeCtorDecl(Token.NoToken, "UnprunedA", 0);
    var b = new TypeCtorDecl(Token.NoToken, "UnprunedB", 0);
    var ta = new CtorType(Token.NoToken, a, new List<BplType>());
    var tb = new CtorType(Token.NoToken, b, new List<BplType>());
    byte[] Types(params BplType[] types) => JsonSerializer.SerializeToUtf8Bytes(TaskExecutionEnvironment.EncodeUsedTypes(types, options));
    var input = Encoding.UTF8.GetBytes("identical initial pruned commands");
    var first = new TaskCacheDescriptor(input, Types(ta, tb), 100, 0);
    Assert.NotEqual(first.Key, new TaskCacheDescriptor(input, Types(tb, ta), 100, 0).Key);
    Assert.NotEqual(first.Key, new TaskCacheDescriptor(input, Types(ta), 100, 0).Key);
  }

  private sealed class Solver(string reason, string? warning = null, Task? handshake = null, string response = "unsat") : SMTLibSolver {
    public override event Action<string>? ErrorHandler;
    public override void Close() { }
    public override void Send(string command) { }
    public override void NewProblem(string name) { }
    public override Task PingPong() => handshake ?? Task.CompletedTask;
    public override void AddErrorHandler(Action<string> handler) { }
    public override Task<SExpr> SendRequest(string request, CancellationToken cancellationToken = default) {
      if (warning != null) { ErrorHandler?.Invoke(warning); }
      return Task.FromResult(request == "(check-sat)" ? S(response) : S(":reason-unknown", S(reason)));
    }
    public override Task<IReadOnlyList<SExpr>> SendRequestsAndCloseInput(IReadOnlyList<string> requests,
      CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  public void Dispose() { if (Directory.Exists(directory)) { Directory.Delete(directory, true); } }
}
