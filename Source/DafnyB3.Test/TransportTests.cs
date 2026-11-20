using System.Diagnostics;
using System.Text;
using DafnyB3;
using Xunit;

public class TransportTests {
  private static Request ValidRequest() => new(Protocol.Version, Guid.NewGuid().ToString("N"), "unit:0", ["a", "b"]);
  private static WorkerCommand Peer(string mode, params string[] rest) => new(Environment.ProcessPath!,
    new[] { typeof(TransportTests).Assembly.Location, mode }.Concat(rest).ToArray());

  // Testhost's process path is not the dotnet host on all platforms.
  private static WorkerCommand DotnetPeer(string mode, params string[] rest) =>
    Peer(mode, rest) with { Executable = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet" };

  [Theory]
  [InlineData("success")]
  [InlineData("stderr")]
  public async Task CompleteCorrelatedTranscriptIsAccepted(string mode) {
    var receipt = await WorkerClient.CheckAsync(DotnetPeer(mode), ValidRequest(), TimeSpan.FromSeconds(10));
    Assert.Equal(mode == "stderr", receipt.StandardErrorTruncated);
    Assert.Equal(mode == "stderr" ? WorkerClient.MaxStandardErrorBytes : 0, receipt.StandardError.Length);
  }

  [Theory]
  [InlineData("close")]
  [InlineData("empty")]
  [InlineData("malformed")]
  [InlineData("truncated")]
  [InlineData("invalid-utf8")]
  [InlineData("large")]
  [InlineData("version")]
  [InlineData("stale")]
  [InlineData("unit")]
  [InlineData("sequence")]
  [InlineData("foreign")]
  [InlineData("missing")]
  [InlineData("duplicate")]
  [InlineData("count")]
  [InlineData("unknown")]
  [InlineData("no-completion")]
  [InlineData("trailing")]
  [InlineData("late-error")]
  [InlineData("crash")]
  public async Task BadPeerNeverPasses(string mode) {
    await Assert.ThrowsAnyAsync<Exception>(() => WorkerClient.CheckAsync(DotnetPeer(mode), ValidRequest(),
      TimeSpan.FromSeconds(10)));
  }

  [Theory]
  [InlineData("hang")]
  [InlineData("stderr-hang")]
  [InlineData("no-exit")]
  [InlineData("no-read")]
  public async Task DeadlineBoundsUnresponsivePeer(string mode) {
    var request = ValidRequest();
    if (mode == "no-read") {
      request = request with { Obligations = Enumerable.Range(0, 256).Select(i => $"{i:D3}" + new string('x', 125)).ToArray() };
    }
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WorkerClient.CheckAsync(DotnetPeer(mode),
      request, TimeSpan.FromMilliseconds(500)));
  }

  [Fact]
  public async Task StartupFailureIsAnError() {
    await Assert.ThrowsAnyAsync<Exception>(() => WorkerClient.CheckAsync(new WorkerCommand(
      Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), []), ValidRequest(), TimeSpan.FromSeconds(1)));
  }

  [Fact]
  public async Task ScaffoldCannotReturnProofSuccess() {
    var worker = Path.Combine(AppContext.BaseDirectory, "DafnyB3.Worker.dll");
    var command = new WorkerCommand(DotnetPeer("unused").Executable, [worker, "--protocol-v1"]);
    await Assert.ThrowsAsync<ProtocolException>(() => WorkerClient.CheckAsync(command, ValidRequest(), TimeSpan.FromSeconds(10)));
  }

  [Theory]
  [InlineData("", "--protocol-v1")]
  [InlineData("{}\n", "--protocol-v1")]
  [InlineData("{\"Version\":1,\"RequestId\":\"r\",\"UnitId\":\"u\",\"Obligations\":[\"a\"]}\n{\"Version\":1,\"RequestId\":\"r\",\"UnitId\":\"u\",\"Obligations\":[\"a\"]}\n", "--protocol-v1")]
  [InlineData("", "--unknown")]
  [InlineData("{\"Version\":1,\"RequestId\":\"r\",\"UnitId\":\"u\",\"Obligations\":[\"a\"]}\n", "--unknown")]
  public async Task ScaffoldRejectsInvalidInput(string input, string argument) {
    using var worker = new Process {
      StartInfo = new ProcessStartInfo(DotnetPeer("unused").Executable) {
        UseShellExecute = false, RedirectStandardInput = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
        ArgumentList = { Path.Combine(AppContext.BaseDirectory, "DafnyB3.Worker.dll"), argument }
      }
    };
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    Assert.True(worker.Start());
    try {
      await worker.StandardInput.WriteAsync(input);
      worker.StandardInput.Close();
      var output = worker.StandardOutput.ReadToEndAsync(deadline.Token);
      var error = worker.StandardError.ReadToEndAsync(deadline.Token);
      await worker.WaitForExitAsync(deadline.Token);
      Assert.Equal(2, worker.ExitCode);
      Assert.Empty(await output);
      Assert.True((await error).Length < 128);
    } finally {
      if (!worker.HasExited) { worker.Kill(entireProcessTree: true); }
      await worker.WaitForExitAsync();
    }
  }

  [Fact]
  public async Task ScaffoldEmitsOnlyUnsupportedAndExitsUnsuccessfully() {
    using var worker = new Process {
      StartInfo = new ProcessStartInfo(DotnetPeer("unused").Executable) {
        UseShellExecute = false, RedirectStandardInput = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
        ArgumentList = { Path.Combine(AppContext.BaseDirectory, "DafnyB3.Worker.dll"), "--protocol-v1" }
      }
    };
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    Assert.True(worker.Start());
    try {
      var request = ValidRequest();
      await Protocol.WriteAsync(worker.StandardInput.BaseStream, request, deadline.Token);
      worker.StandardInput.Close();
      var ledger = new ResponseLedger(request);
      var frames = 0;
      while (await Protocol.ReadAsync<Response>(worker.StandardOutput.BaseStream, deadline.Token) is { } response) {
        Assert.Equal(Outcome.Unsupported, response.Outcome);
        ledger.Accept(response);
        frames++;
      }
      await worker.WaitForExitAsync(deadline.Token);
      Assert.Equal(3, frames);
      Assert.Equal(1, worker.ExitCode);
      Assert.Throws<ProtocolException>(ledger.RequireProved);
    } finally {
      if (!worker.HasExited) { worker.Kill(entireProcessTree: true); }
      await worker.WaitForExitAsync();
    }
  }

  [Fact]
  public async Task CancellationReapsLiveProcessTreeRepeatedly() {
    for (var attempt = 0; attempt < 3; attempt++) {
      var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      using var cancel = new CancellationTokenSource();
      var owned = new List<Process>();
      var check = WorkerClient.CheckAsync(DotnetPeer("tree", file), ValidRequest(), TimeSpan.FromSeconds(10), cancel.Token);
      try {
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!File.Exists(file)) { await Task.Delay(10, wait.Token); }
        var pids = (await File.ReadAllTextAsync(file)).Split(',').Select(int.Parse).ToArray();
        Assert.Equal(2, pids.Length);
        owned.AddRange(pids.Select(Process.GetProcessById));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check);
        foreach (var pid in pids) {
          using var reaped = new CancellationTokenSource(TimeSpan.FromSeconds(3));
          while (IsRunning(pid) && !reaped.IsCancellationRequested) { await Task.Delay(10); }
          Assert.False(IsRunning(pid), $"Owned process {pid} survived cancellation");
        }
      } finally {
        cancel.Cancel();
        try { await check; } catch (Exception) { }
        // Independently clean up mutation failures; never leave a deliberately broken client running.
        foreach (var process in owned) {
          try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
          catch (InvalidOperationException) { }
          process.Dispose();
        }
        File.Delete(file);
      }
    }
  }

  private static bool IsRunning(int pid) {
    try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
    catch (ArgumentException) { return false; }
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("null")]
  [InlineData("[]")]
  [InlineData("{\"Version\":1,\"Version\":1}")]
  [InlineData("{\"Version\":1,\"Version\":1,\"RequestId\":\"r\",\"UnitId\":\"u\",\"Obligations\":[\"a\"]}")]
  [InlineData("{\"Version\":1,\"RequestId\":\"r\",\"UnitId\":\"u\",\"Obligations\":[\"a\"],\"Extra\":true}")]
  public async Task StrictJsonRejectsAmbiguityAndMissingFields(string json) {
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json + "\n"));
    await Assert.ThrowsAsync<ProtocolException>(() => Protocol.ReadAsync<Request>(stream, CancellationToken.None));
  }

  [Theory]
  [InlineData("\"proved\"")]
  [InlineData("\"1\"")]
  [InlineData("1")]
  [InlineData("null")]
  [InlineData("\"FutureOutcome\"")]
  public async Task OutcomeEncodingIsExact(string outcome) {
    var json = "{\"Version\":1,\"RequestId\":\"r\",\"UnitId\":\"u\",\"Sequence\":0,\"Kind\":\"result\",\"ObligationId\":\"a\",\"Outcome\":" + outcome + ",\"Completed\":0}\n";
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    await Assert.ThrowsAsync<ProtocolException>(() => Protocol.ReadAsync<Response>(stream, CancellationToken.None));
  }

  [Fact]
  public async Task FramingRejectsOversizePartialAndInvalidEncodingBeforeInterpretation() {
    var large = "{\"Version\":1,\"RequestId\":\"" + new string('x', Protocol.MaxFrameBytes) + "\",\"UnitId\":\"u\",\"Obligations\":[\"a\"]}\n";
    foreach (var bytes in new[] {
      Encoding.UTF8.GetBytes(large), Encoding.UTF8.GetBytes("{}"),
      Encoding.UTF8.GetBytes("{\"Version\":1,\"RequestId\":\"").Concat(new byte[] { 255 })
        .Concat(Encoding.UTF8.GetBytes("\",\"UnitId\":\"u\",\"Obligations\":[\"a\"]}\n")).ToArray()
    }) {
      using var stream = new MemoryStream(bytes);
      await Assert.ThrowsAsync<ProtocolException>(() => Protocol.ReadAsync<Request>(stream, CancellationToken.None));
    }
    using var output = new MemoryStream();
    await Assert.ThrowsAsync<ProtocolException>(() => Protocol.WriteAsync(output,
      ValidRequest() with { RequestId = new string('x', Protocol.MaxFrameBytes) }, CancellationToken.None));
    Assert.Equal(0, output.Length);
  }

  [Fact]
  public void RequestLimitsAreChecked() {
    var request = ValidRequest();
    foreach (var bad in new[] {
      request with { Version = 2 }, request with { RequestId = "" }, request with { UnitId = "a b" },
      request with { RequestId = new string('x', 129) },
      request with { Obligations = null! }, request with { Obligations = [null!] },
      request with { Obligations = Enumerable.Range(0, 257).Select(i => i.ToString()).ToArray() }
    }) { Assert.Throws<ProtocolException>(() => Protocol.Validate(bad)); }
  }

  [Fact]
  public async Task InvalidDeadlineAndPrecancelledRequestCannotStart() {
    foreach (var deadline in new[] { TimeSpan.Zero, TimeSpan.FromHours(2) }) {
      await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => WorkerClient.CheckAsync(DotnetPeer("success"), ValidRequest(), deadline));
    }
    using var cancel = new CancellationTokenSource();
    cancel.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WorkerClient.CheckAsync(DotnetPeer("success"), ValidRequest(), TimeSpan.FromSeconds(1), cancel.Token));
  }

  [Fact]
  public void MalformedResultAndCompletionCannotPass() {
    var request = ValidRequest();
    Response Result(int i) => new(1, request.RequestId, request.UnitId, i, "result", request.Obligations[i], Outcome.Proved, 0);
    foreach (var bad in new[] {
      Result(0) with { Kind = "other" }, Result(0) with { Completed = 1 },
      Result(0) with { ObligationId = null }, Result(0) with { Outcome = (Outcome)999 }
    }) {
      var ledger = new ResponseLedger(request);
      Assert.Throws<ProtocolException>(() => ledger.Accept(bad));
    }
    foreach (var bad in new[] {
      new Response(1, request.RequestId, request.UnitId, 2, "complete", "a", Outcome.Proved, 2),
      new Response(1, request.RequestId, request.UnitId, 2, "complete", null, Outcome.Proved, 1)
    }) {
      var ledger = new ResponseLedger(request);
      ledger.Accept(Result(0)); ledger.Accept(Result(1));
      Assert.Throws<ProtocolException>(() => ledger.Accept(bad));
    }
    var inconsistent = new ResponseLedger(request);
    inconsistent.Accept(Result(0) with { Outcome = Outcome.Failed }); inconsistent.Accept(Result(1));
    Assert.Throws<ProtocolException>(() => inconsistent.Accept(new Response(1, request.RequestId, request.UnitId, 2, "complete", null, Outcome.Proved, 2)));
    var incomplete = new ResponseLedger(request);
    Assert.Throws<ProtocolException>(incomplete.RequireProved);
    var trailing = new ResponseLedger(request);
    trailing.Accept(Result(0)); trailing.Accept(Result(1));
    trailing.Accept(new Response(1, request.RequestId, request.UnitId, 2, "complete", null, Outcome.Proved, 2));
    Assert.Throws<ProtocolException>(() => trailing.Accept(new Response(1, request.RequestId, request.UnitId, 3, "complete", null, Outcome.Proved, 2)));
  }

  [Theory]
  [InlineData(Outcome.ToolError)]
  [InlineData(Outcome.Failed)]
  [InlineData(Outcome.Unknown)]
  [InlineData(Outcome.Unsupported)]
  [InlineData(Outcome.Cancelled)]
  [InlineData(Outcome.Timeout)]
  [InlineData(Outcome.ResourceLimit)]
  public void EveryNonSuccessOutcomeIsRejected(Outcome outcome) {
    var request = ValidRequest();
    var ledger = new ResponseLedger(request);
    for (var i = 0; i < request.Obligations.Length; i++) {
      ledger.Accept(new Response(1, request.RequestId, request.UnitId, i, "result", request.Obligations[i], outcome, 0));
    }
    ledger.Accept(new Response(1, request.RequestId, request.UnitId, 2, "complete", null, outcome, 2));
    Assert.Throws<ProtocolException>(ledger.RequireProved);
  }

  [Fact]
  public void RejectedResponsePoisonsLedger() {
    var request = ValidRequest();
    var ledger = new ResponseLedger(request);
    Assert.Throws<ProtocolException>(() => ledger.Accept(new Response(1, "other", request.UnitId, 0, "result", "a", Outcome.Proved, 0)));
    Assert.Throws<ProtocolException>(() => ledger.Accept(new Response(1, request.RequestId, request.UnitId, 0, "result", "a", Outcome.Proved, 0)));
    Assert.Throws<ProtocolException>(ledger.RequireProved);
  }

  [Fact]
  public void EmptyAndDuplicateManifestsAreRejectedBeforeStartup() {
    Assert.Throws<ProtocolException>(() => Protocol.Validate(ValidRequest() with { Obligations = [] }));
    Assert.Throws<ProtocolException>(() => Protocol.Validate(ValidRequest() with { Obligations = ["a", "a"] }));
  }

  [Fact]
  public void LedgerSnapshotsTheManifest() {
    var request = ValidRequest();
    var ledger = new ResponseLedger(request);
    request.Obligations[0] = "replaced";
    ledger.Accept(new Response(1, request.RequestId, request.UnitId, 0, "result", "a", Outcome.Proved, 0));
    ledger.Accept(new Response(1, request.RequestId, request.UnitId, 1, "result", "b", Outcome.Proved, 0));
    ledger.Accept(new Response(1, request.RequestId, request.UnitId, 2, "complete", null, Outcome.Proved, 2));
    ledger.RequireProved();
  }

  [Fact]
  public async Task ReadAndWriteErrorsPropagate() {
    using var stream = new BrokenStream();
    await Assert.ThrowsAsync<IOException>(() => Protocol.ReadAsync<Request>(stream, CancellationToken.None));
    await Assert.ThrowsAsync<IOException>(() => Protocol.WriteAsync(stream, ValidRequest(), CancellationToken.None));
  }

  private sealed class BrokenStream : MemoryStream {
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
      ValueTask.FromException<int>(new IOException("Read failed"));
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
      ValueTask.FromException(new IOException("Write failed"));
  }
}
