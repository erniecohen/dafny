using System.Diagnostics;
using DafnyB3Protocol;
using Xunit;
using Program = DafnyB3Protocol.Program;

namespace DafnyB3Protocol.Test;

public class WorkerProcessTests {
  private static Request CreateRequest(int timeout = 10000) {
    var program = new Program(Array.Empty<string>(), Array.Empty<Function>(), Array.Empty<Axiom>(),
      new Unit("sP0", Array.Empty<Binding>(), new Check("sO0", new BooleanLiteral(true), false)));
    return new Request(Protocol.Version, "worker-test", Protocol.NormalizerVersion,
      new string('a', 40), Protocol.GetProgramHash(program), "sP0", program,
      new Configuration("z3", new[] { "-in", "-smt2" }, timeout, 10000, 100000, 2),
      new[] { new SourceIdentity("sO0", "test.dfy", 1, 1, "assertion") });
  }
  private static WorkerProcessClient Client(string mode, string pidFile = "") {
    var fixture = Environment.GetEnvironmentVariable("B3_WORKER_FIXTURE")
      ?? throw new InvalidOperationException("Set B3_WORKER_FIXTURE to the built fixture DLL");
    return new WorkerProcessClient("dotnet", new[] { fixture, mode, pidFile });
  }
  [Theory]
  [InlineData("missing")]
  [InlineData("truncated")]
  [InlineData("wrong-id")]
  public async Task ExitZeroIncompleteAndMismatchedWorkersCannotVerify(string mode) {
    var result = await Client(mode).RunAsync(CreateRequest(), CancellationToken.None);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.False(result.TraversalCompleted);
  }
  [Fact]
  public async Task DrainedStderrCannotDeadlockSuccessfulWorker() {
    var result = await Client("stderr").RunAsync(CreateRequest(), CancellationToken.None);
    Assert.Equal(Outcome.Verified, result.Outcome);
  }
  [Theory]
  [InlineData("crash-with-child")]
  [InlineData("hang-with-child")]
  public async Task CrashAndDeadlineTerminateWorkerChildren(string mode) {
    var pidFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    try {
      var result = await Client(mode, pidFile).RunAsync(CreateRequest(3000), CancellationToken.None);
      Assert.Equal(mode == "crash-with-child" ? Outcome.ToolError : Outcome.TimedOut, result.Outcome);
      Assert.True(File.Exists(pidFile));
      var pid = int.Parse(await File.ReadAllTextAsync(pidFile));
      for (var i = 0; i < 20 && IsRunning(pid); i++) { await Task.Delay(50); }
      Assert.False(IsRunning(pid));
    } finally { File.Delete(pidFile); }
  }
  [Fact]
  public async Task UserCancellationIsDistinctFromDeadline() {
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    var result = await Client("missing").RunAsync(CreateRequest(), cancelled.Token);
    Assert.Equal(Outcome.Cancelled, result.Outcome);
  }
  private static bool IsRunning(int pid) {
    try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
    catch (ArgumentException) { return false; }
  }
}
