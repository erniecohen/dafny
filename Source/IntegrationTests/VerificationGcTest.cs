#nullable enable
using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny;
using Xunit;

namespace IntegrationTests;

[CollectionDefinition("VerificationGc", DisableParallelization = true)]
public class VerificationGcCollection { }

[Collection("VerificationGc")]
public class VerificationGcTest {
  [Fact]
  public void OptionIsHiddenDefaultOffAndRejectsInvalidValues() {
    var command = new RootCommand { VerifyCommand.BatchGc };
    Assert.True(VerifyCommand.BatchGc.IsHidden);
    Assert.False(command.Parse(Array.Empty<string>()).GetValueForOption(VerifyCommand.BatchGc));
    Assert.False(command.Parse("--batch-gc=false").GetValueForOption(VerifyCommand.BatchGc));
    Assert.True(command.Parse("--batch-gc").GetValueForOption(VerifyCommand.BatchGc));
    Assert.NotEmpty(command.Parse("--batch-gc=invalid").Errors);
  }

  [Fact]
  public void ServerOptionIsHiddenDefaultOffAndRejectsInvalidValues() {
    OptionRegistry.CheckOptionsAreKnown(new[] { VerifyCommand.ServerGcMemory });
    Assert.DoesNotContain(VerifyCommand.ServerGcMemory, OptionRegistry.GlobalOptions);
    Assert.DoesNotContain(VerifyCommand.ServerGcMemory, OptionRegistry.TranslationOptions);
    Assert.DoesNotContain(VerifyCommand.ServerGcMemory, OptionRegistry.ModuleOptions);
    var verify = DafnyNewCli.RootCommand.Subcommands.Single(command => command.Name == "verify");
    Assert.Contains(VerifyCommand.ServerGcMemory, verify.Options);
    var command = new RootCommand { VerifyCommand.ServerGcMemory };
    Assert.True(VerifyCommand.ServerGcMemory.IsHidden);
    Assert.Equal(0u, command.Parse(Array.Empty<string>()).GetValueForOption(VerifyCommand.ServerGcMemory));
    Assert.Equal(0u, command.Parse("--server-gc-memory=0").GetValueForOption(VerifyCommand.ServerGcMemory));
    Assert.Equal(1024u, command.Parse("--server-gc-memory=1024").GetValueForOption(VerifyCommand.ServerGcMemory));
    Assert.NotEmpty(command.Parse("--server-gc-memory=invalid").Errors);
    Assert.NotEmpty(command.Parse("--server-gc-memory=-1").Errors);
  }

  [Fact]
  public void BoundedLaunchOverridesHeapCountsAndClearsCompetingLimitsOnlyWhenEnabled() {
    var keys = new[] { "Percent", "SOH", "LOH", "POH", "SOHPercent", "LOHPercent", "POHPercent" }
      .SelectMany(suffix => new[] { "DOTNET_", "COMPlus_" }.Select(prefix => prefix + "GCHeapHardLimit" + suffix)).ToArray();
    var previous = keys.Select(Environment.GetEnvironmentVariable).ToArray();
    try {
      foreach (var key in keys) { Environment.SetEnvironmentVariable(key, "1"); }
      var off = VerificationGc.StartInfo("dotnet", "entry.dll", "10.0.0", ["verify", "input.dfy"]);
      var on = VerificationGc.StartInfo("dotnet", "entry.dll", "10.0.0", ["verify", "input.dfy"], 1024);
      foreach (var key in keys) {
        Assert.Equal("1", off.Environment[key]);
        Assert.False(on.Environment.ContainsKey(key));
      }
      foreach (var prefix in new[] { "DOTNET_", "COMPlus_" }) {
        Assert.Equal("0", on.Environment[prefix + "gcConcurrent"]);
        Assert.Equal("1", on.Environment[prefix + "gcServer"]);
        Assert.Equal("2", on.Environment[prefix + "GCHeapCount"]);
        Assert.Equal("2", on.Environment[prefix + "GCMaxHeapCount"]);
        Assert.Equal("40000000", on.Environment[prefix + "GCHeapHardLimit"]);
      }
      Assert.Equal(off.ArgumentList, on.ArgumentList);
    } finally {
      for (var i = 0; i < keys.Length; i++) { Environment.SetEnvironmentVariable(keys[i], previous[i]); }
    }
  }

  [Fact]
  public void DotnetLaunchPreservesArgumentsAndRuntime() {
    string[] args = ["verify", "input with spaces.dfy", "--batch-gc", "--cores", "2"];
    var start = VerificationGc.StartInfo("dotnet", "entry with spaces.dll", "10.0.0", args);
    Assert.False(start.UseShellExecute);
    Assert.Equal(new[] { "exec", "--fx-version", "10.0.0", "entry with spaces.dll", "verify",
      "input with spaces.dfy", "--batch-gc", "--cores", "2" }, start.ArgumentList);
    Assert.Equal("0", start.Environment["DOTNET_gcConcurrent"]);
    Assert.Equal("0", start.Environment["COMPlus_gcConcurrent"]);
    Assert.Equal("1", start.Environment[VerificationGc.RestartedVariable]);
  }

  [Fact]
  public void ApphostLaunchPreservesArguments() {
    var start = VerificationGc.StartInfo("dafny", "unused.dll", "10.0.0", ["verify", "x.dfy"]);
    Assert.Equal(new[] { "verify", "x.dfy" }, start.ArgumentList);
  }

  [Fact]
  public async Task PreflightIsPublishedOnceAndThenStreams() {
    var target = new StringWriter();
    var writer = new VerificationGc.PreflightWriter(target);
    await writer.WriteAsync("first");
    Assert.Equal("", target.ToString());
    writer.Publish();
    writer.Publish();
    writer.Write(' ');
    writer.Write(new[] { 'n', 'e', 'x', 't' }, 0, 4);
    await writer.WriteLineAsync();
    writer.Flush();
    Assert.Equal("first next" + Environment.NewLine, target.ToString());
    Assert.Equal(target.Encoding, writer.Encoding);
  }

  [Fact]
  public void PreflightPreservesInterleavedStreamOrder() {
    var merged = new StringWriter();
    var journal = new VerificationGc.Preflight();
    var output = new VerificationGc.PreflightWriter(merged, journal);
    var error = new VerificationGc.PreflightWriter(merged, journal);
    error.Write("error");
    output.Write("help");
    error.Write("last");
    Assert.Equal("", merged.ToString());
    output.Publish();
    error.Publish();
    output.Write("next");
    Assert.Equal("errorhelplastnext", merged.ToString());
  }

  [Fact]
  public async Task CliParseErrorsPreserveMergedDiagnosticOrder() {
    string[] args = ["verify", "--cores", "invalid"];
    var expected = new StringWriter();
    var expectedExit = await DafnyBackwardsCompatibleCli.MainWithWriters(expected, expected, TextReader.Null, args);
    Assert.Equal((int)ExitValue.PREPROCESSING_ERROR, expectedExit);
    var previousOutput = Console.Out;
    var previousError = Console.Error;
    var actual = new StringWriter();
    try {
      Console.SetOut(actual);
      Console.SetError(actual);
      Assert.Equal(expectedExit, await DafnyBackwardsCompatibleCli.Main(args));
    } finally {
      Console.SetOut(previousOutput);
      Console.SetError(previousError);
    }
    Assert.NotEmpty(expected.ToString());
    Assert.Equal(expected.ToString(), actual.ToString());
  }

  [Fact]
  public void PreflightAsyncWritesKeepConsoleOrderAndCompleteInline() {
    var target = new CallingThreadWriter();
    var journal = new VerificationGc.Preflight();
    var output = journal.Writer(target);
    var error = journal.Writer(target);
    Assert.True(output.WriteLineAsync("buffered output").IsCompletedSuccessfully);
    Assert.True(error.WriteLineAsync("buffered error").IsCompletedSuccessfully);
    Assert.Equal("", target.ToString());
    journal.Publish();
    journal.Publish();
    // Blocking here also makes a deferred write's different thread observable.
    output.WriteLineAsync("diagnostic").GetAwaiter().GetResult();
    error.WriteLine("related");
    output.WriteAsync('x').GetAwaiter().GetResult();
    output.WriteAsync(new[] { 'y' }, 0, 1).GetAwaiter().GetResult();
    output.WriteAsync("z".AsMemory()).GetAwaiter().GetResult();
    output.WriteLineAsync().GetAwaiter().GetResult();
    output.FlushAsync().GetAwaiter().GetResult();
    Assert.Equal(string.Join(Environment.NewLine,
      "buffered output", "buffered error", "diagnostic", "related", "xyz", ""), target.ToString());
    Assert.True(target.Writes > 0);
    Assert.False(target.WroteOnAnotherThread);
  }

  private sealed class CallingThreadWriter : StringWriter {
    private readonly int callingThread = Environment.CurrentManagedThreadId;
    internal int Writes { get; private set; }
    internal bool WroteOnAnotherThread { get; private set; }
    private void RecordWrite() {
      Writes++;
      WroteOnAnotherThread |= callingThread != Environment.CurrentManagedThreadId;
    }
    public override void Write(char value) { RecordWrite(); base.Write(value); }
    public override void Write(string? value) { RecordWrite(); base.Write(value); }
    public override void Write(char[] buffer, int index, int count) {
      RecordWrite();
      base.Write(buffer, index, count);
    }
  }

  [Fact]
  public void DelegatedPreflightIsDiscardedIncludingLaterWrites() {
    var target = new StringWriter();
    var writer = new VerificationGc.PreflightWriter(target);
    writer.Write("warning");
    writer.Discard();
    writer.Write("later");
    writer.Publish();
    Assert.Equal("", target.ToString());
  }

  [Fact]
  public async Task InProcessOptInIsRejectedWithoutRunningVerification() {
    var output = new StringWriter();
    var options = new DafnyOptions(TextReader.Null, output, output);
    options.Options.OptionArguments[VerifyCommand.BatchGc] = true;
    var previous = GCSettings.LatencyMode;
    var ran = false;
    var code = await VerificationGc.RunVerification(options, () => { ran = true; return Task.FromResult(17); });
    Assert.Equal((int)ExitValue.PREPROCESSING_ERROR, code);
    Assert.False(ran);
    Assert.Contains("requires the command-line executable", output.ToString());
    Assert.Equal(previous, GCSettings.LatencyMode);
  }

  [Fact]
  public async Task InProcessDefaultRunsWithoutChangingTheMode() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    var previous = GCSettings.LatencyMode;
    Assert.Equal(17, await VerificationGc.RunVerification(options, () => Task.FromResult(17)));
    Assert.Equal(previous, GCSettings.LatencyMode);
  }

  private static ProcessStartInfo CliStartInfo(string[] args, bool restarted = false, bool batchStartup = false) {
    var start = new ProcessStartInfo("dotnet") {
      UseShellExecute = false, RedirectStandardInput = true,
      RedirectStandardOutput = true, RedirectStandardError = true
    };
    start.ArgumentList.Add(typeof(Dafny.Dafny).Assembly.Location);
    foreach (var arg in args) { start.ArgumentList.Add(arg); }
    // The guard control deliberately starts in Interactive mode even if the test host is Batch.
    start.Environment["DOTNET_gcConcurrent"] = batchStartup ? "0" : "1";
    start.Environment["COMPlus_gcConcurrent"] = batchStartup ? "0" : "1";
    start.Environment["DOTNET_gcServer"] = "0";
    start.Environment["COMPlus_gcServer"] = "0";
    start.Environment[VerificationGc.RestartedVariable] = restarted ? "1" : "0";
    return start;
  }

  private static async Task<(int Exit, string Output)> Cli(string[] args, string input = "", bool restarted = false,
    bool batchStartup = false, Action<ProcessStartInfo>? configure = null) {
    var start = CliStartInfo(args, restarted, batchStartup);
    configure?.Invoke(start);
    using var child = Process.Start(start)!;
    try {
      var output = child.StandardOutput.ReadToEndAsync();
      var error = child.StandardError.ReadToEndAsync();
      await child.StandardInput.WriteAsync(input);
      child.StandardInput.Close();
      using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
      var exit = await VerificationGc.WaitForChild(child, timeout.Token);
      return (exit, await output + await error);
    } finally {
      if (!child.HasExited) { child.Kill(entireProcessTree: true); }
      await child.WaitForExitAsync();
    }
  }

  private static string[] VerifyArgs(params string[] args) {
    var extra = Environment.GetEnvironmentVariable("DAFNY_EXTRA_TEST_ARGUMENTS");
    return new[] { "verify", "--batch-gc", "--cores", "2", "--type-system-refresh", "--show-snippets:false" }
      .Concat(extra == null ? Array.Empty<string>() : new[] { extra }).Concat(args).ToArray();
  }

  [Theory]
  [InlineData(1024, false, 1, "requires --batch-gc")]
  [InlineData(128, true, 1, "at least 256 MiB")]
  [InlineData(1024, true, 4, "requires an explicit --verification-module-window-memory")]
  public async Task InvalidServerPolicyIsRejectedBeforeVerification(int memory, bool batch, int window, string diagnostic) {
    var args = VerifyArgs("--stdin", "--server-gc-memory", memory.ToString(),
      "--verification-module-window", window.ToString());
    if (!batch) { args = args.Where(a => a != "--batch-gc").ToArray(); }
    var result = await Cli(args, "lemma Control() { assert false; }");
    Assert.Equal((int)ExitValue.PREPROCESSING_ERROR, result.Exit);
    Assert.Contains(diagnostic, result.Output);
    Assert.DoesNotContain("Dafny program verifier", result.Output);
  }

  [Fact]
  public async Task ServerRestartGuardRejectsBatchWorkstationGcBeforeVerification() {
    var result = await Cli(VerifyArgs("--stdin", "--server-gc-memory", "1024"),
      "lemma Control() { assert false; }", restarted: true, batchStartup: true);
    Assert.Equal((int)ExitValue.PREPROCESSING_ERROR, result.Exit);
    Assert.Contains("could not configure bounded server GC", result.Output);
    Assert.DoesNotContain("Dafny program verifier", result.Output);
  }

  [Theory]
  [InlineData("limit")]
  [InlineData("count")]
  [InlineData("per-object")]
  [InlineData("workstation")]
  public async Task ServerRestartGuardRejectsConflictingRuntimeConfiguration(string conflict) {
    void Configure(ProcessStartInfo start) {
      foreach (var prefix in new[] { "DOTNET_", "COMPlus_" }) {
        start.Environment[prefix + "gcServer"] = conflict == "workstation" ? "0" : "1";
        start.Environment[prefix + "GCHeapCount"] = "2";
        start.Environment[prefix + "GCMaxHeapCount"] = conflict == "count" ? "3" : "2";
        start.Environment[prefix + "GCHeapHardLimit"] = conflict == "limit" ? "20000000" : "40000000";
        if (conflict == "per-object") {
          // Their sum equals the overall limit, so only the per-object guard
          // rejects this case; a different total would also trip the limit guard.
          start.Environment[prefix + "GCHeapHardLimitSOH"] = "20000000";
          start.Environment[prefix + "GCHeapHardLimitLOH"] = "10000000";
          start.Environment[prefix + "GCHeapHardLimitPOH"] = "10000000";
        }
      }
    }
    var result = await Cli(VerifyArgs("--stdin", "--server-gc-memory", "1024"),
      "lemma Control() { assert false; }", restarted: true, batchStartup: true, configure: Configure);
    Assert.Equal((int)ExitValue.PREPROCESSING_ERROR, result.Exit);
    Assert.Contains("could not configure bounded server GC", result.Output);
    Assert.DoesNotContain("Dafny program verifier", result.Output);
  }

  [Fact]
  public async Task ServerStartupChildForwardsAReachableFalseAssertion() {
    var result = await Cli(VerifyArgs("--stdin", "--server-gc-memory", "1024"),
      "lemma Positive() ensures 1 + 1 == 2 { } lemma Control() { assert false; }");
    Assert.Equal((int)ExitValue.VERIFICATION_ERROR, result.Exit);
    Assert.Contains("assertion might not hold", result.Output);
    Assert.Contains("1 verified, 1 error", result.Output);
  }

  [Fact]
  public async Task RestartGuardRejectsAnUnconfiguredChildBeforeVerification() {
    var result = await Cli(VerifyArgs("--stdin"), "lemma Control() { assert false; }", restarted: true);
    Assert.Equal((int)ExitValue.PREPROCESSING_ERROR, result.Exit);
    Assert.Contains("could not disable background GC at startup", result.Output);
    Assert.DoesNotContain("Dafny program verifier", result.Output);
  }

  [Fact]
  public async Task StartupChildInheritsStdinAndForwardsTheVerificationFailure() {
    var result = await Cli(VerifyArgs("--stdin"),
      "lemma Positive(x: int) ensures x + 1 > x {}\nlemma Counterexample() { assert false; }");
    Assert.Equal((int)ExitValue.VERIFICATION_ERROR, result.Exit);
    Assert.Contains("1 verified, 1 error", result.Output);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task JsonDiagnosticsPreserveAssertionOrder(bool batchGc) {
    var args = VerifyArgs("--stdin", "--json-diagnostics", "--isolate-assertions", "--extract-counterexample");
    if (!batchGc) { args = args.Where(a => a != "--batch-gc").ToArray(); }
    var result = await Cli(args,
      "method Counterexamples(x: int) {\n  assert x > 0;\n  assert x > 1;\n  assert x > 2;\n}");
    Assert.Equal((int)ExitValue.VERIFICATION_ERROR, result.Exit);
    var diagnostics = result.Output.Split('\n').Where(line => line.StartsWith('{'))
      .Select(line => JsonDocument.Parse(line)).ToArray();
    try {
      Assert.Equal(new[] { 2, 3, 4 }, diagnostics.Select(d => d.RootElement.GetProperty("location")
        .GetProperty("range").GetProperty("start").GetProperty("line").GetInt32()));
      Assert.All(diagnostics, d => Assert.Contains("Related counterexample:",
        d.RootElement.GetProperty("message").GetString()));
    } finally {
      foreach (var diagnostic in diagnostics) { diagnostic.Dispose(); }
    }
  }

  [Fact]
  public async Task ProjectDiagnosticsArePublishedOnceByTheChild() {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-batch-gc-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    try {
      await File.WriteAllTextAsync(Path.Combine(directory, "input.dfy"), "lemma Control() { assert false; }");
      var project = Path.Combine(directory, "dfyconfig.toml");
      await File.WriteAllTextAsync(project,
        "includes = [\"input.dfy\"]\n[options]\nbatch-gc = true\nunknown-gc-control = true\n");
      // Exercise project-only activation, without a command-line --batch-gc.
      var result = await Cli(VerifyArgs(project).Where(a => a != "--batch-gc").ToArray());
      Assert.Equal((int)ExitValue.VERIFICATION_ERROR, result.Exit);
      Assert.Equal(1, result.Output.Split("Warning: option 'unknown-gc-control'").Length - 1);
      Assert.Contains("0 verified, 1 error", result.Output);
    } finally {
      Directory.Delete(directory, true);
    }
  }

  [SkippableFact]
  public async Task InterruptingTheLauncherStopsAndReapsItsChild() {
    Skip.If(OperatingSystem.IsWindows(), "This control sends a POSIX interrupt to the launcher.");
    var directory = Path.Combine(Path.GetTempPath(), "dafny-batch-gc-interrupt-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var profile = Path.Combine(directory, "profile.jsonl");
    using var parent = Process.Start(CliStartInfo(VerifyArgs("--stdin", "--pipeline-profile", profile)))!;
    Process? verifier = null;
    try {
      var output = parent.StandardOutput.ReadToEndAsync();
      var error = parent.StandardError.ReadToEndAsync();
      // Keep stdin open: the configured child blocks on input after creating its profile.
      using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
      while (!File.Exists(profile) || !File.ReadAllText(profile).Contains("\"gcLatencyMode\":\"Batch\"")) {
        Assert.False(parent.HasExited, "launcher exited before the configured child started");
        await Task.Delay(25, timeout.Token);
      }
      var children = new ProcessStartInfo("ps") { RedirectStandardOutput = true, UseShellExecute = false };
      foreach (var arg in new[] { "-axo", "pid=,ppid=" }) { children.ArgumentList.Add(arg); }
      using var ps = Process.Start(children)!;
      var ids = (await ps.StandardOutput.ReadToEndAsync()).Split('\n')
        .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .Where(cells => cells.Length == 2 && cells[1] == parent.Id.ToString()).Select(cells => cells[0]).ToArray();
      await ps.WaitForExitAsync(timeout.Token);
      Assert.Equal(0, ps.ExitCode);
      verifier = Process.GetProcessById(int.Parse(Assert.Single(ids)));
      var interrupt = new ProcessStartInfo("kill") { UseShellExecute = false };
      interrupt.ArgumentList.Add("-INT");
      interrupt.ArgumentList.Add(parent.Id.ToString());
      using var signal = Process.Start(interrupt)!;
      await signal.WaitForExitAsync(timeout.Token);
      Assert.Equal(0, signal.ExitCode);
      await parent.WaitForExitAsync(timeout.Token);
      Assert.Equal(130, parent.ExitCode);
      Assert.True(verifier.HasExited, "launcher returned before its child stopped");
      await output;
      await error;
    } finally {
      // Independent cleanup keeps signal/cleanup mutations from leaving either process alive.
      if (!parent.HasExited) { parent.Kill(entireProcessTree: true); }
      await parent.WaitForExitAsync();
      if (verifier != null) {
        if (!verifier.HasExited) { verifier.Kill(entireProcessTree: true); }
        verifier.Dispose();
      }
      Directory.Delete(directory, true);
    }
  }

  private static Process Shell(string command) {
    var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd" : "sh") { UseShellExecute = false };
    start.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
    start.ArgumentList.Add(command);
    return Process.Start(start)!;
  }

  [Fact]
  public async Task ChildExitIsForwarded() {
    using var child = Shell("exit 7");
    Assert.Equal(7, await VerificationGc.WaitForChild(child, CancellationToken.None));
    Assert.True(child.HasExited);
  }

  [Fact]
  public async Task CancellationStopsTheChildBeforeReturning() {
    using var child = Shell(OperatingSystem.IsWindows() ? "ping -n 30 127.0.0.1 >nul" : "exec sleep 30");
    try {
      using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => VerificationGc.WaitForChild(child, cancellation.Token));
      Assert.True(child.HasExited);
    } finally {
      // This independent cleanup also prevents the cleanup mutation from leaving a process behind.
      if (!child.HasExited) { child.Kill(entireProcessTree: true); }
      await child.WaitForExitAsync();
    }
  }
}
