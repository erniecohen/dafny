using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace B3AlcGate;

/// <summary>Fixed native prerequisite controls. Never constructs a product context or invokes verification.</summary>
[SupportedOSPlatform("linux")]
internal static class NativeLifecycleControls {
  private static readonly string[] Names = ["missing-delegation", "wrong-solver-pin", "sealed-version", "sealed-mutation",
    "creator-thread-exit", "binary-streams-eof", "relay-broken-pipe", "timeout-reparented-descendant",
    "live-descendant-cap", "refused-late-launch", "malformed-completion"];
  private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 20 };
  private sealed record Result(string Name, string ExpectedOutcome, string ObservedOutcome, bool Passed, bool NoProductLoaded,
    bool ZeroOwnedMembers, bool OwnedLeafRemoved, bool SupervisorFailure, bool FallbackUsed,
    string? CleanupSha256, Dictionary<string, object?> Facts, string? FailureCode);
  private sealed record Inputs(string Solver, string SolverSha256, string Parent, string Receipt, string Evidence,
    string Fixture, string FixtureSha256, string FixtureSourceSha256, string BuildReceiptSha256, JsonElement BuildDeclaration);

  public static int Main(string[] arguments) {
    var results = new List<Result>();
    Inputs? input = null;
    string? failure = null;
    try {
      Require(OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64, "linux-x64-required");
      Require(Framework.LiveChildren().Length == 0 && NoProductLoaded(), "clean-framework-host-required");
      input = Prepare(arguments);
      foreach (var name in Names) {
        var result = Control(name, input);
        results.Add(result);
        if (!result.Passed) { break; }
      }
    } catch (Exception exception) { failure = exception is ControlFailure ? exception.Message : exception.GetType().Name; }
    var passed = failure == null && results.Count == Names.Length && results.All(r => r.Passed);
    if (input != null) {
      var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
        schemaVersion = 1, scope = "prototype/native-lifecycle-controls", passed,
        sourceManifestSha256 = Framework.Sha256(Path.Combine(AppContext.BaseDirectory, "source-manifest.json")),
        harnessAssemblySha256 = Framework.Sha256(typeof(Program).Assembly.Location), solverSha256 = input.SolverSha256,
        fixtureSourceSha256 = input.FixtureSourceSha256, fixtureSha256 = input.FixtureSha256,
        fixtureBuildReceiptSha256 = input.BuildReceiptSha256, fixtureBuildDeclaration = input.BuildDeclaration,
        fixtureBuildEvidenceKind = "CI-declaration-not-signed-compiler-origin", delegatedPidCeiling = 300,
        noProductLoaded = NoProductLoaded(), invokesVerification = false, controls = results, failureCode = failure
      }, Json);
      if (bytes.Length > 1048576) { Console.Error.WriteLine("Lifecycle receipt bound exceeded."); return 2; }
      File.WriteAllBytes(input.Receipt, bytes);
    }
    Console.WriteLine(passed ? "Eleven fixed native lifecycle controls passed; no verification ran." : "Lifecycle control failed; no later control ran.");
    if (failure != null) { Console.Error.WriteLine("Lifecycle preflight failure: " + failure); }
    return passed ? 0 : failure == null ? 1 : 2;
  }

  private static Inputs Prepare(string[] arguments) {
    Require(arguments.Length == 9 && arguments[0] == "--lifecycle-controls", "fixed-lifecycle-options-required");
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 1; index < arguments.Length; index += 2) {
      Require(arguments[index] is "--solver" or "--solver-sha256" or "--cgroup-parent" or "--receipt", "unknown-lifecycle-option");
      Require(!string.IsNullOrWhiteSpace(arguments[index + 1]) && options.TryAdd(arguments[index], arguments[index + 1]), "duplicate-lifecycle-option");
    }
    Require(options.Count == 4, "missing-lifecycle-option");
    var solver = Path.GetFullPath(options["--solver"]);
    var digest = options["--solver-sha256"].ToLowerInvariant();
    Require(digest.Length == 64 && digest.All(Uri.IsHexDigit) && Framework.Sha256(solver) == digest, "solver-pin-mismatch");
    var parent = Path.GetFullPath(options["--cgroup-parent"]).TrimEnd('/');
    Require(parent.StartsWith("/sys/fs/cgroup/", StringComparison.Ordinal), "explicit-delegation-required");
    Require(File.ReadAllText(Path.Combine(parent, "pids.max")).Trim() == "300", "caller-pids-max-300-required");
    var receipt = Path.GetFullPath(options["--receipt"]);
    var evidence = Path.Combine(Path.GetDirectoryName(receipt) ?? throw new ControlFailure("receipt-parent-required"), "lifecycle-evidence-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(evidence, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    VerifyStatic("native-solver-wrapper.py"); VerifyStatic("lifecycle-seal-probe.py"); VerifyStatic("native-lifecycle-fixture.c");
    VerifyStatic("lifecycle-control-fixtures.json");
    using var registry = ReadJson(Path.Combine(AppContext.BaseDirectory, "lifecycle-control-fixtures.json"), 16384);
    Require(registry.RootElement.GetProperty("schemaVersion").GetInt32() == 1 &&
      registry.RootElement.GetProperty("controls").EnumerateArray().Select(e => e.GetString()).SequenceEqual(Names), "fixed-registry-mismatch");
    var fixture = Path.Combine(AppContext.BaseDirectory, "native-lifecycle-fixture");
    var fixtureDigest = Framework.Sha256(fixture);
    var sourceDigest = Framework.Sha256(Path.Combine(AppContext.BaseDirectory, "native-lifecycle-fixture.c"));
    var buildPath = Path.Combine(AppContext.BaseDirectory, "native-lifecycle-fixture.build.json");
    using var build = ReadJson(buildPath, 32768);
    var declaration = build.RootElement;
    Require(declaration.GetProperty("schemaVersion").GetInt32() == 1 &&
      declaration.GetProperty("sourceSha256").GetString() == sourceDigest && declaration.GetProperty("binarySha256").GetString() == fixtureDigest &&
      declaration.GetProperty("sourceManifestSha256").GetString() == Framework.Sha256(Path.Combine(AppContext.BaseDirectory, "source-manifest.json")), "fixture-build-declaration-mismatch");
    var compiler = declaration.GetProperty("compiler");
    Require(Framework.Sha256(compiler.GetProperty("path").GetString() ?? "") == compiler.GetProperty("sha256").GetString() &&
      compiler.GetProperty("version").GetString() is { Length: > 0 and <= 4096 }, "compiler-declaration-mismatch");
    var command = declaration.GetProperty("arguments").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
    Require(command.Length == 9 && Path.GetFullPath(command[0]) == Path.GetFullPath(compiler.GetProperty("path").GetString() ?? "") &&
      command.Skip(1).Take(5).SequenceEqual(["-std=c11", "-O2", "-Wall", "-Wextra", "-Werror"]) &&
      Path.GetFileName(command[6]) == "native-lifecycle-fixture.c" && command[7] == "-o" &&
      Path.GetFileName(command[8]) == "native-lifecycle-fixture", "fixed-compiler-command-required");
    return new(solver, digest, parent, receipt, evidence, fixture, fixtureDigest, sourceDigest, Framework.Sha256(buildPath), declaration.Clone());
  }

  private static Result Control(string name, Inputs input) {
    var facts = new Dictionary<string, object?>();
    var expected = name is "missing-delegation" or "wrong-solver-pin" ? "preflight-rejection" :
      name is "relay-broken-pipe" or "live-descendant-cap" or "malformed-completion" ? "controlled-supervisor-failure-and-empty-owned-group" :
      name == "timeout-reparented-descendant" ? "controlled-timeout-and-owned-cleanup" : "complete-owned-cleanup";
    Wrapped? run = null;
    string? failure = null;
    try {
      Require(NoProductLoaded() && Framework.LiveChildren().Length == 0, "clean-control-baseline-required");
      if (name is "missing-delegation" or "wrong-solver-pin") {
        var rejected = false;
        try {
          _ = new NativeProofSupervisor(input.Solver,
            name == "wrong-solver-pin" ? (input.SolverSha256[0] == '0' ? "1" : "0") + input.SolverSha256[1..] : input.SolverSha256,
            name == "missing-delegation" ? "/sys/fs/cgroup" : input.Parent, input.Evidence);
        } catch (Exception exception) {
          rejected = name == "wrong-solver-pin"
            ? exception is InvalidDataException && exception.Message == "Real solver digest mismatch."
            : exception is InvalidOperationException && exception.Message == "Provide a private, empty, delegated domain cgroup-v2 parent under /sys/fs/cgroup.";
          facts["preflightExceptionType"] = exception.GetType().Name;
        }
        Require(rejected && NoProductLoaded() && !Directory.EnumerateDirectories(input.Parent).Any(), "expected-preflight-rejection-missing");
        facts["rejectedBeforeProductLoad"] = true;
        facts["ownedLeafDisposition"] = "never-created-preflight-rejection";
      } else {
        var real = name is "sealed-version" or "creator-thread-exit" ? input.Solver : input.Fixture;
        var digest = real == input.Solver ? input.SolverSha256 : input.FixtureSha256;
        string[] args = name switch {
          "sealed-version" => new[] { "-version" },
          "creator-thread-exit" => new[] { "-in", "-smt2" },
          "sealed-mutation" => ["seal-check", Path.Combine(AppContext.BaseDirectory, "lifecycle-seal-probe.py"),
            Path.Combine(AppContext.BaseDirectory, "native-solver-wrapper.py"), input.Solver, input.SolverSha256],
          "binary-streams-eof" => ["streams"],
          "relay-broken-pipe" => ["close-input"],
          "timeout-reparented-descendant" => ["reparented"],
          "live-descendant-cap" => ["cap"],
          _ => new[] { "version" }
        };
        run = new Wrapped(real, digest, input.Parent, input.Evidence, name, args);
        if (name == "creator-thread-exit") {
          Exception? startup = null;
          Wrapped threadRun = run;
          var creator = new Thread(() => { try { threadRun.Start(); } catch (Exception exception) { startup = exception; } });
          creator.Start();
          Require(creator.Join(TimeSpan.FromSeconds(3)) && startup == null && !creator.IsAlive, "creator-thread-start-failed");
          Thread.Sleep(200);
          Require(!run.Process.HasExited, "wrapper-died-with-creator-thread");
          run.Send(Encoding.ASCII.GetBytes("(echo \"creator-thread-retired\")\n"));
          run.WaitOutput("creator-thread-retired", TimeSpan.FromSeconds(3));
          Require(!creator.IsAlive && !run.Process.HasExited, "host-alive-thread-retirement-failed");
          facts["creatorThreadRetiredWhileHostAndWrapperAlive"] = true;
          run.Send(Encoding.ASCII.GetBytes("(exit)\n")); run.CloseInput();
          Require(run.WaitExit(TimeSpan.FromSeconds(3)) && run.Process.ExitCode == 0, "interactive-z3-exit-failed");
        } else {
          run.Start();
          if (name == "binary-streams-eof") { run.WriteAndClose(Pattern(131071, 13, 7)); }
          else if (name == "relay-broken-pipe") {
            run.WaitOutput("input-closed\n", TimeSpan.FromSeconds(3));
            run.WriteAndClose(Pattern(262144, 13, 7));
          } else { run.CloseInput(); }
          if (name == "timeout-reparented-descendant") {
            run.WaitOutput("descendant ", TimeSpan.FromSeconds(3));
            run.WaitOutput("\n", TimeSpan.FromSeconds(3));
            var line = Encoding.ASCII.GetString(run.Output.Bytes()).Trim();
            Require(line.StartsWith("descendant ", StringComparison.Ordinal) && int.TryParse(line[11..], NumberStyles.None, CultureInfo.InvariantCulture, out _), "descendant-receipt-invalid");
            var descendantPid = int.Parse(line[11..], CultureInfo.InvariantCulture);
            var until = Stopwatch.StartNew();
            NativeProofSupervisor.Identity? descendant = null;
            while (until.Elapsed < TimeSpan.FromSeconds(2)) {
              descendant = NativeProofSupervisor.Identity.Read(descendantPid);
              if (descendant.Session == descendantPid && descendant.Parent != run.SolverPid()) { break; }
              Thread.Sleep(10);
            }
            Require(descendant != null && descendant.Session == descendantPid && descendant.Parent != run.SolverPid() && run.Members().Contains(descendantPid), "reparented-owned-descendant-missing");
            Require(!run.WaitExit(TimeSpan.FromMilliseconds(150)), "controlled-timeout-did-not-occur");
            facts["controlledTimeout"] = true; facts["reparentedSessionLeaderObserved"] = true;
          } else if (name == "live-descendant-cap") {
            run.WaitOutput("cap-ready 257\n", TimeSpan.FromSeconds(5));
            var members = run.Members();
            Require(members.Length > 256 && members.Length <= 300 && File.ReadAllText(Path.Combine(input.Parent, "pids.max")).Trim() == "300", "cap-fixture-precondition-failed");
            facts["observedOwnedLiveMembers"] = members.Length;
          } else { Require(run.WaitExit(TimeSpan.FromSeconds(5)), "native-control-exit-deadline"); }
        }
        if (name is "refused-late-launch" or "malformed-completion") {
          Require(run.Process.ExitCode == 0 && Encoding.ASCII.GetString(run.Output.Bytes()) == "NativeLifecycleFixture 1\n" &&
            run.Error.Bytes().Length == 0, "positive-fixture-version-prerequisite-failed");
        }
        if (name == "malformed-completion") {
          var complete = Path.Combine(run.LaunchDirectory(), "complete.json");
          using var original = ReadJson(complete, 16384);
          using var config = ReadJson(Path.Combine(run.Root, "config.json"), 16384);
          Require(original.RootElement.GetProperty("token").GetString() == config.RootElement.GetProperty("token").GetString() &&
            original.RootElement.GetProperty("errors").GetArrayLength() == 0 && original.RootElement.GetProperty("solverExitCode").GetInt32() == 0,
            "positive-completion-prerequisite-failed");
          var altered = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(original.RootElement.GetRawText())!;
          altered["token"] = JsonSerializer.SerializeToElement("wrong-control-token");
          var temporary = complete + ".control-pending";
          File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(altered));
          File.Move(temporary, complete, overwrite: true);
          facts["completionTokenDeliberatelyCorrupted"] = true;
        }
        run.Cleanup();
        Require(run.EmptyRemoved && run.HostHasNoChildren && run.ReadersCompleted, "owned-cleanup-not-proven");
        Require(name == "relay-broken-pipe" || !run.InputFailed, "unexpected-control-input-failure");
        var expectedFailure = name is "relay-broken-pipe" or "live-descendant-cap" or "malformed-completion";
        Require(run.SupervisorFailure == expectedFailure, "unexpected-supervisor-outcome");
        if (name == "live-descendant-cap") {
          Require(run.FallbackUsed && run.CleanupFailures().Any(s => s.Contains("More than 256 owned live processes", StringComparison.Ordinal)), "cap-fault-or-fallback-not-recorded");
        } else if (name == "relay-broken-pipe") {
          using var complete = ReadJson(Path.Combine(run.LaunchDirectory(), "complete.json"), 16384);
          Require(complete.RootElement.GetProperty("errors").EnumerateArray().Any(e => e.GetProperty("stream").GetString() == "stdin" && e.GetProperty("errno").GetInt32() == 32), "actual-relay-broken-pipe-not-recorded");
          Require(run.Output.Bytes().SequenceEqual(Encoding.ASCII.GetBytes("input-closed\n")) &&
            run.Error.Bytes().SequenceEqual(Encoding.ASCII.GetBytes("fixture-stderr-on-stdin-close\n")), "relay-error-stream-changed");
          facts["actualRelayErrno"] = 32;
        } else if (name == "malformed-completion") {
          Require(run.CleanupFailures().Any(s => s.Contains("receipt failed exact validation", StringComparison.Ordinal)), "malformed-receipt-not-rejected");
        } else if (name == "sealed-version") {
          Require(run.Process.ExitCode == 0 && Encoding.UTF8.GetString(run.Output.Bytes()).Trim() == "Z3 version 5.1.0 - 64 bit" && run.Error.Bytes().Length == 0, "sealed-z3-version-mismatch");
        } else if (name == "sealed-mutation") {
          Require(run.Process.ExitCode == 0 && run.Error.Bytes().Length == 0, "seal-probe-process-failed");
          using var probe = JsonDocument.Parse(run.Output.Bytes());
          var value = probe.RootElement;
          Require(value.GetProperty("capturedSha256").GetString() == input.SolverSha256 && value.GetProperty("sourceMutationChangedDigest").GetBoolean() &&
            value.GetProperty("requiredSeals").GetInt32() == 15 && (value.GetProperty("seals").GetInt32() & 15) == 15 &&
            value.GetProperty("denied").EnumerateArray().Select(e => e.GetProperty("operation").GetString()).SequenceEqual(["write", "grow", "shrink", "add-seal"]) &&
            value.GetProperty("denied").EnumerateArray().All(e => e.GetProperty("errno").GetInt32() == 1), "actual-seal-mutation-control-failed");
          var component = value.GetProperty("writeLoopComponent");
          Require(component.GetProperty("kind").GetString() == "deterministic-callback-not-kernel-partial-write" && component.GetProperty("partialWrites").GetInt32() > 0 &&
            component.GetProperty("interruptedWrites").GetInt32() == 1 && component.GetProperty("bytes").GetInt32() == 257 &&
            component.GetProperty("sha256").GetString() == Digest(Pattern(257, 13, 7)), "production-write-loop-component-failed");
          facts["sealProbe"] = value.Clone();
        } else if (name == "binary-streams-eof") {
          var wanted = Pattern(131071, 13, 7).Concat(Pattern(4097, 17, 19)).ToArray();
          Require(run.Process.ExitCode == 0 && run.Output.Bytes().SequenceEqual(wanted) && run.Error.Bytes().SequenceEqual(Pattern(196613, 37, 11)), "native-binary-stream-inequality");
          using var complete = ReadJson(Path.Combine(run.LaunchDirectory(), "complete.json"), 16384);
          Require(complete.RootElement.GetProperty("streams").GetProperty("stdin").GetProperty("end").GetString() == "eof", "native-input-eof-not-observed");
          facts["measuredKernelPartialWrites"] = complete.RootElement.GetProperty("streams").EnumerateObject().Sum(s => s.Value.GetProperty("partialWrites").GetInt32());
          facts["stdinSha256"] = Digest(Pattern(131071, 13, 7));
          facts["stdoutSha256"] = Digest(wanted); facts["stderrSha256"] = Digest(Pattern(196613, 37, 11));
        }
        if (name == "refused-late-launch") {
          using var late = StartProcess(run.Wrapper, ["version"], run.Root);
          var output = new Capture(late.StandardOutput.BaseStream); var error = new Capture(late.StandardError.BaseStream);
          using var lateOwner = new NativeProofSupervisor.DirectChildGuard(late);
          try {
            Require(!Framework.LiveChildren().Any(pid => pid != late.Id), "late-control-has-other-host-children");
            late.StandardInput.Close();
            Require(late.WaitForExit(3000) && Task.WaitAll([output.Completion, error.Completion], 1000) && late.ExitCode == 125 &&
              output.Bytes().Length == 0 && error.Bytes().Length == 0 && !Directory.Exists(run.Leaf), "late-launch-was-not-refused");
          } finally {
            lateOwner.Stop(TimeSpan.FromSeconds(2));
            facts["lateDirectChildPidfdForcedDrain"] = lateOwner.Forced;
            facts["lateDirectChildProvenExited"] = lateOwner.ProvenExited;
          }
          Require(lateOwner.ProvenExited && !lateOwner.Forced, "late-child-required-forced-drain");
          var rejected = Directory.GetFiles(run.Root, "wrapper-failure-*.json");
          Require(rejected.Length == 1, "late-launch-failure-receipt-missing");
          using var fault = ReadJson(rejected[0], 16384);
          Require(fault.RootElement.GetProperty("message").GetString() == "Launch admission is closed", "late-launch-failed-for-wrong-reason");
          facts["lateLaunchRejectedBeforeFork"] = true; facts["lateFailureSha256"] = Framework.Sha256(rejected[0]);
        }
        facts["stdoutSha256"] = Digest(run.Output.Bytes()); facts["stderrSha256"] = Digest(run.Error.Bytes());
      }
    } catch (Exception exception) { failure = exception is ControlFailure ? exception.Message : exception.GetType().Name; }
    finally { run?.Cleanup(); }
    var noProduct = NoProductLoaded();
    var empty = run?.EmptyRemoved ?? !Directory.EnumerateDirectories(input.Parent).Any();
    var hostChildren = Framework.LiveChildren();
    var zeroOwned = empty && hostChildren.Length == 0;
    var passed = failure == null && noProduct && zeroOwned;
    facts["finalDirectHostChildren"] = hostChildren;
    if (run?.Started == true) {
      facts["stdoutBytes"] = run.Output.Bytes().Length; facts["stderrBytes"] = run.Error.Bytes().Length;
      facts["stdoutSha256"] = Digest(run.Output.Bytes()); facts["stderrSha256"] = Digest(run.Error.Bytes());
    }
    var observed = failure != null ? "unexpected-control-failure" : run == null ? "preflight-rejection" :
      run.SupervisorFailure ? "controlled-supervisor-failure-and-empty-owned-group" :
      name == "timeout-reparented-descendant" ? "controlled-timeout-and-owned-cleanup" : "complete-owned-cleanup";
    var result = new Result(name, expected, observed, passed, noProduct, zeroOwned, run?.EmptyRemoved ?? false, run?.SupervisorFailure ?? false, run?.FallbackUsed ?? false,
      run?.CleanupDigest, facts, failure);
    run?.DisposeResources();
    return result;
  }

  private sealed class Capture {
    private readonly MemoryStream data = new();
    public Task Completion { get; }
    public Capture(Stream source) { Completion = Read(source); }
    private async Task Read(Stream source) {
      var buffer = new byte[4096];
      for (;;) {
        var count = await source.ReadAsync(buffer);
        if (count == 0) { return; }
        lock (data) {
          if (data.Length + count > 1048576) { throw new IOException("control-stream-bound"); }
          data.Write(buffer, 0, count);
        }
      }
    }
    public byte[] Bytes() { lock (data) { return data.ToArray(); } }
  }

  private sealed class Wrapped {
    private readonly IProofRunScope scope;
    private readonly string[] arguments;
    private readonly string parent;
    private bool started;
    private bool cleaned;
    private Task? input;
    private JsonDocument? cleanup;
    public bool Started => started;
    public Process Process { get; private set; } = new();
    public Capture Output { get; private set; } = null!;
    public Capture Error { get; private set; } = null!;
    public string Wrapper { get; }
    public string Root { get; }
    public string Leaf { get; }
    public bool SupervisorFailure { get; private set; }
    public bool FallbackUsed => cleanup?.RootElement.GetProperty("fallbackRequests").GetArrayLength() > 0;
    public bool EmptyRemoved => cleanup != null && cleanup.RootElement.GetProperty("leafRemoved").GetBoolean() &&
      cleanup.RootElement.GetProperty("finalMembers").GetArrayLength() == 0 && !cleanup.RootElement.GetProperty("populated").GetBoolean() &&
      !Directory.Exists(Leaf) && !Directory.EnumerateDirectories(parent).Any();
    public bool HostHasNoChildren => Framework.LiveChildren().Length == 0;
    public bool ReadersCompleted { get; private set; }
    public bool InputFailed { get; private set; }
    public string? CleanupDigest { get; private set; }
    public Wrapped(string solver, string digest, string parent, string evidence, string name, string[] arguments) {
      this.parent = parent; this.arguments = arguments;
      var supervisor = new NativeProofSupervisor(solver, digest, parent, evidence);
      Wrapper = supervisor.WrapperPath; Root = Path.GetDirectoryName(Wrapper)!;
      scope = supervisor.BeginRun(new Product("lifecycle/" + name, AppContext.BaseDirectory), Array.AsReadOnly(arguments));
      using var config = ReadJson(Path.Combine(Root, "config.json"), 16384);
      Leaf = config.RootElement.GetProperty("leaf").GetString()!;
    }
    public void Start() {
      Process.Dispose(); Process = StartProcess(Wrapper, arguments, Root); started = true;
      Output = new Capture(Process.StandardOutput.BaseStream); Error = new Capture(Process.StandardError.BaseStream);
    }
    public void Send(byte[] bytes) { Require(Process.StandardInput.BaseStream.WriteAsync(bytes).AsTask().Wait(TimeSpan.FromSeconds(2)), "control-input-deadline"); }
    public void CloseInput() { Process.StandardInput.Close(); }
    public void WriteAndClose(byte[] bytes) {
      input = Task.Run(async () => { try { await Process.StandardInput.BaseStream.WriteAsync(bytes); } finally { Process.StandardInput.Close(); } });
    }
    public bool WaitExit(TimeSpan deadline) {
      var clock = Stopwatch.StartNew();
      if (!Process.WaitForExit((int)deadline.TotalMilliseconds)) { return false; }
      var remaining = Math.Max(1, (int)(deadline - clock.Elapsed).TotalMilliseconds);
      return Task.WaitAll([Output.Completion, Error.Completion], remaining);
    }
    public void WaitOutput(string marker, TimeSpan deadline) {
      var clock = Stopwatch.StartNew();
      while (clock.Elapsed < deadline) {
        if (Encoding.ASCII.GetString(Output.Bytes()).Contains(marker, StringComparison.Ordinal)) { return; }
        if (Process.HasExited) { break; }
        Thread.Sleep(10);
      }
      throw new ControlFailure("control-output-marker-deadline");
    }
    public int[] Members() => File.ReadAllText(Path.Combine(Leaf, "cgroup.procs")).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
      .Select(s => int.Parse(s, CultureInfo.InvariantCulture)).Distinct().ToArray();
    public string LaunchDirectory() => Directory.GetDirectories(Path.Combine(Root, "launches")).Single();
    public int SolverPid() {
      using var ready = ReadJson(Path.Combine(LaunchDirectory(), "ready.json"), 16384);
      return ready.RootElement.GetProperty("solver").GetProperty("pid").GetInt32();
    }
    public string[] CleanupFailures() => cleanup?.RootElement.GetProperty("failures").EnumerateArray().Select(e => e.GetString() ?? "").ToArray() ?? [];
    public void Cleanup() {
      if (cleaned) { return; }
      cleaned = true;
      try { _ = scope.StopAndAssertNoOwnedSolvers(TimeSpan.FromSeconds(6)); }
      catch (Exception) { SupervisorFailure = true; }
      finally { try { scope.Dispose(); } catch (Exception) { SupervisorFailure = true; } }
      if (started) {
        try {
          var exited = Process.WaitForExit(2000);
          ReadersCompleted = exited && Task.WaitAll([Output.Completion, Error.Completion], 1000);
          if (input != null) {
            try { if (!input.Wait(TimeSpan.FromSeconds(1))) { ReadersCompleted = false; InputFailed = true; } }
            catch (AggregateException) { InputFailed = true; /* Expected broken pipe is checked from the native relay receipt. */ }
          }
        } catch (Exception) { ReadersCompleted = false; }
      }
      var path = Path.Combine(Root, "cleanup.json");
      if (File.Exists(path)) {
        cleanup = ReadJson(path, 1048576);
        CleanupDigest = Framework.Sha256(path);
      }
    }
    public void DisposeResources() { Process.Dispose(); cleanup?.Dispose(); }
  }

  private static Process StartProcess(string executable, string[] arguments, string directory) {
    var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardInput = true,
      RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory };
    foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
    return Process.Start(start) ?? throw new ControlFailure("native-process-start-failed");
  }
  private static bool NoProductLoaded() => !AssemblyLoadContext.Default.Assemblies.Any(a =>
    (a.GetName().Name ?? "").StartsWith("Dafny", StringComparison.OrdinalIgnoreCase) ||
    (a.GetName().Name ?? "").StartsWith("Boogie", StringComparison.OrdinalIgnoreCase));
  private static byte[] Pattern(int length, int factor, int offset) => Enumerable.Range(0, length).Select(i => (byte)((i * factor + offset) & 255)).ToArray();
  private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  private static JsonDocument ReadJson(string path, int bound) {
    Require(new FileInfo(path).Length <= bound, "control-json-bound");
    var bytes = File.ReadAllBytes(path); Require(bytes.Length <= bound, "control-json-bound");
    return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 20 });
  }
  private static void VerifyStatic(string name) {
    using var manifest = ReadJson(Path.Combine(AppContext.BaseDirectory, "source-manifest.json"), 65536);
    var entries = manifest.RootElement.GetProperty("files").EnumerateArray().Where(e => e.GetProperty("path").GetString() == name).ToArray();
    var path = Path.Combine(AppContext.BaseDirectory, name);
    Require(entries.Length == 1 && entries[0].GetProperty("sha256").GetString() == Framework.Sha256(path) &&
      entries[0].GetProperty("bytes").GetInt64() == new FileInfo(path).Length, "static-control-source-mismatch");
  }
  private sealed class ControlFailure(string code) : Exception(code) { }
  private static void Require(bool condition, string code) { if (!condition) { throw new ControlFailure(code); } }
}
