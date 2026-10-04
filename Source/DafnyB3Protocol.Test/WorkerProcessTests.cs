using System.Diagnostics;
using System.Text.Json;
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
      new Configuration("z3", new[] { "-in", "-smt2" }, timeout, 10000, 100000, 2, "5.1.0", new string('d', 64)),
      new[] { new SourceIdentity("sO0", "test.dfy", 1, 1, "assertion") }, new string('f', 64));
  }
  private static WorkerProcessClient Client(string mode, string pidFile = "") {
    var fixture = Environment.GetEnvironmentVariable("B3_WORKER_FIXTURE")
      ?? Path.Combine(AppContext.BaseDirectory, "worker-fixture", "WorkerFixture.dll");
    return new WorkerProcessClient("dotnet", new[] { fixture, mode, pidFile });
  }
  [UnixTheory]
  [InlineData("missing")]
  [InlineData("truncated")]
  [InlineData("wrong-id")]
  public async Task ExitZeroIncompleteAndMismatchedWorkersCannotVerify(string mode) {
    var result = await Client(mode).RunAsync(CreateRequest(), CancellationToken.None);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.False(result.TraversalCompleted);
  }
  [UnixFact]
  public async Task DrainedStderrCannotDeadlockSuccessfulWorker() {
    var result = await Client("stderr").RunAsync(CreateRequest(), CancellationToken.None);
    Assert.Equal(Outcome.Verified, result.Outcome);
  }
  [UnixTheory]
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
  [UnixFact]
  public async Task UserCancellationIsDistinctFromDeadline() {
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    var result = await Client("missing").RunAsync(CreateRequest(), cancelled.Token);
    Assert.Equal(Outcome.Cancelled, result.Outcome);
  }
  [LinuxTheory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task BackpressureOnRecordTerminatorHonorsCancellation(bool userCancellation) {
    using var cancelled = new CancellationTokenSource();
    var request = CreateRequest(5000) with {
      Obligations = new[] { new SourceIdentity("sO0", "test.dfy", 1, 1, new string('x', 1024 * 1024)) }
    };
    var payloadLength = JsonSerializer.SerializeToUtf8Bytes(request, Protocol.JsonOptions).Length;
    var fixture = Environment.GetEnvironmentVariable("B3_WORKER_FIXTURE")
      ?? Path.Combine(AppContext.BaseDirectory, "worker-fixture", "WorkerFixture.dll");
    var pidFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    var client = new WorkerProcessClient("dotnet", new[] { fixture, "stop-before-newline", payloadLength.ToString(), pidFile });
    var run = client.RunAsync(request, cancelled.Token);
    try {
      for (var i = 0; i < 100 && !File.Exists(pidFile); i++) { await Task.Delay(20); }
      Assert.True(File.Exists(pidFile), "Worker did not reach the payload boundary");
      await Task.Delay(100);
      if (userCancellation) { cancelled.Cancel(); }
      var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
      Assert.Equal(userCancellation ? Outcome.Cancelled : Outcome.TimedOut, result.Outcome);
      Assert.False(result.TraversalCompleted);
    } finally {
      // A regression in the write cancellation must not leave the fixture alive.
      if (File.Exists(pidFile)) {
        var pid = int.Parse(await File.ReadAllTextAsync(pidFile));
        try { using var process = Process.GetProcessById(pid); process.Kill(entireProcessTree: true); }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
      }
      File.Delete(pidFile);
    }
  }
  private static bool IsRunning(int pid) {
    try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
    catch (ArgumentException) { return false; }
  }
}

internal sealed class UnixFactAttribute : FactAttribute {
  public UnixFactAttribute() {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) { Skip = "B3 process isolation requires Unix"; }
  }
}
internal sealed class UnixTheoryAttribute : TheoryAttribute {
  public UnixTheoryAttribute() {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) { Skip = "B3 process isolation requires Unix"; }
  }
}

internal sealed class LinuxTheoryAttribute : TheoryAttribute {
  public LinuxTheoryAttribute() {
    if (!OperatingSystem.IsLinux()) { Skip = "This fixture measures Linux pipe capacity"; }
  }
}
