using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace B3AlcGate;

/// <summary>Source-only Linux ownership implementation. Program.Main does not enable it.</summary>
[SupportedOSPlatform("linux")]
public sealed class NativeProofSupervisor : IProofLifecycleSupervisor {
  private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 16 };
  private readonly string root;
  private readonly string delegatedParent;
  private readonly string solver;
  private readonly string solverDigest;
  private readonly string python;
  private readonly string templateDigest;
  private bool began;
  public string WrapperPath => Path.Combine(root, "solver-wrapper");

  public NativeProofSupervisor(string realSolver, string expectedSolverSha256, string delegatedCgroupParent,
    string evidenceParent, string pythonExecutable = "/usr/bin/python3") {
    if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64)) {
      throw new PlatformNotSupportedException("The native supervisor requires Linux x64/arm64 pidfds and cgroup v2.");
    }
    using (Native.OpenPidFd(Environment.ProcessId)) { } // Kernel capability check; never signal the host.
    solver = Path.GetFullPath(realSolver);
    solverDigest = Framework.Sha256(solver);
    if (solverDigest != expectedSolverSha256.ToLowerInvariant()) { throw new InvalidDataException("Real solver digest mismatch."); }
    python = new FileInfo(Path.GetFullPath(pythonExecutable)).ResolveLinkTarget(true)?.FullName ?? Path.GetFullPath(pythonExecutable);
    if (!File.Exists(python) || python.Any(char.IsWhiteSpace)) { throw new ArgumentException("An existing space-free Python interpreter is required."); }
    delegatedParent = Path.GetFullPath(delegatedCgroupParent).TrimEnd('/');
    ValidateParent();
    root = Path.Combine(Path.GetFullPath(evidenceParent), "alc-native-" + Guid.NewGuid().ToString("N"));
    if (!Directory.Exists(evidenceParent)) { throw new DirectoryNotFoundException("The evidence parent must already exist."); }
    Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    Directory.CreateDirectory(Path.Combine(root, "launches"));
    var templatePath = Path.Combine(AppContext.BaseDirectory, "native-solver-wrapper.py");
    templateDigest = Framework.Sha256(templatePath);
    using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source-manifest.json")));
    if (!manifest.RootElement.GetProperty("files").EnumerateArray().Any(entry =>
          entry.GetProperty("path").GetString() == "native-solver-wrapper.py" &&
          entry.GetProperty("sha256").GetString() == templateDigest)) {
      throw new InvalidDataException("The wrapper template does not match the source manifest.");
    }
    var source = File.ReadAllText(templatePath);
    if (!source.StartsWith("#!__PYTHON_EXECUTABLE__ -I\n", StringComparison.Ordinal)) {
      throw new InvalidDataException("Unexpected wrapper template header.");
    }
    File.WriteAllText(WrapperPath, source.Replace("__PYTHON_EXECUTABLE__", python, StringComparison.Ordinal));
    File.SetUnixFileMode(WrapperPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
  }

  public IProofRunScope BeginRun(Product product, IReadOnlyList<string> arguments) {
    if (began) { throw new InvalidOperationException("A native supervisor is single-use."); }
    began = true;
    ValidateParent();
    var leaf = Path.Combine(delegatedParent, "alc-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(leaf, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    if (ReadMembers(leaf).Length != 0 || Populated(leaf)) { throw new InvalidOperationException("The new owned cgroup is not empty."); }
    var host = Identity.Read(Environment.ProcessId);
    var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    File.WriteAllBytes(Path.Combine(root, "config.json"), JsonSerializer.SerializeToUtf8Bytes(new {
      token, leaf, host, realSolver = solver, solverSha256 = solverDigest, python, pythonSha256 = Framework.Sha256(python),
      product = product.Label, arguments, templateDigest, wrapperDigest = Framework.Sha256(WrapperPath)
    }, Json));
    using (File.Create(Path.Combine(root, "admission.lock"))) { }
    return new Scope(root, leaf, token, host, solverDigest);
  }

  private void ValidateParent() {
    for (var current = new DirectoryInfo(delegatedParent); current.FullName != "/sys/fs/cgroup";
         current = current.Parent ?? throw new InvalidOperationException("Invalid delegated cgroup ancestry.")) {
      if (current.LinkTarget != null) { throw new InvalidOperationException("Symlinked cgroup delegation is unsupported."); }
    }
    if (!delegatedParent.StartsWith("/sys/fs/cgroup/", StringComparison.Ordinal) ||
        new DirectoryInfo(delegatedParent).LinkTarget != null ||
        !File.Exists(Path.Combine(delegatedParent, "cgroup.controllers")) ||
        File.ReadAllText(Path.Combine(delegatedParent, "cgroup.type")).Trim() != "domain" ||
        (File.GetUnixFileMode(delegatedParent) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                                                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0 ||
        Directory.EnumerateDirectories(delegatedParent).Any() || ReadMembers(delegatedParent).Length != 0 || Populated(delegatedParent)) {
      throw new InvalidOperationException("Provide a private, empty, delegated domain cgroup-v2 parent under /sys/fs/cgroup.");
    }
  }

  internal sealed record Identity(int Pid, ulong StartTime, int Parent, int Group, int Session) {
    public static Identity Read(int pid) {
      var text = File.ReadAllText($"/proc/{pid}/stat");
      if (text.Length > 8192) { throw new InvalidDataException("Oversized proc stat."); }
      var fields = text[(text.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
      return new(pid, ulong.Parse(fields[19], System.Globalization.CultureInfo.InvariantCulture),
        int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture),
        int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
        int.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture));
    }
  }
  private sealed record Launch(string Token, Identity Wrapper, Identity Solver, string SolverSha256, string[] Arguments);
  private sealed record Tracked(Identity Identity, SafeFileHandle Handle, string Kind);

  private sealed class Scope : IProofRunScope {
    private readonly string root;
    private readonly string leaf;
    private readonly string token;
    private readonly Identity host;
    private readonly string solverDigest;
    private readonly object gate = new();
    private readonly Dictionary<(int, ulong), Tracked> tracked = [];
    private readonly Dictionary<string, Launch> launches = [];
    private readonly List<object> signalRequests = [];
    private readonly CancellationTokenSource stop = new();
    private readonly Task monitor;
    private string? monitorFailure;
    private bool cleaned;
    private bool disposed;

    public Scope(string root, string leaf, string token, Identity host, string solverDigest) {
      this.root = root; this.leaf = leaf; this.token = token; this.host = host; this.solverDigest = solverDigest;
      monitor = Task.Run(() => {
        while (!stop.IsCancellationRequested) {
          try { lock (gate) { Scan(); } }
          catch (Exception exception) { monitorFailure ??= exception.GetType().Name + ": " + exception.Message; }
          Thread.Sleep(10);
        }
      });
    }

    private void Track(Identity identity, string kind) {
      if (tracked.TryGetValue((identity.Pid, identity.StartTime), out var previous)) {
        if (kind is "wrapper" or "solver") { tracked[(identity.Pid, identity.StartTime)] = previous with { Kind = kind }; }
        return;
      }
      if (!ReadMembers(leaf).Contains(identity.Pid) || Identity.Read(identity.Pid).StartTime != identity.StartTime) {
        throw new InvalidDataException("Ownership identity changed before pidfd capture.");
      }
      var descriptor = Native.OpenPidFd(identity.Pid);
      try {
        if (Identity.Read(identity.Pid).StartTime != identity.StartTime || !ReadMembers(leaf).Contains(identity.Pid)) {
          throw new InvalidDataException("Ownership identity changed during pidfd capture.");
        }
        tracked.Add((identity.Pid, identity.StartTime), new(identity, descriptor, kind));
      } catch { descriptor.Dispose(); throw; }
    }

    private void Scan() {
      var directories = Directory.GetDirectories(Path.Combine(root, "launches"));
      if (directories.Length > 64) { throw new InvalidDataException("More than 64 solver launches."); }
      foreach (var directory in directories) {
        var ready = Path.Combine(directory, "ready.json");
        if (!File.Exists(ready) || launches.ContainsKey(directory)) { continue; }
        if (new FileInfo(ready).Length > 16384) { throw new InvalidDataException("Oversized launch receipt."); }
        var launch = JsonSerializer.Deserialize<Launch>(File.ReadAllBytes(ready), Json)
          ?? throw new InvalidDataException("Missing launch receipt.");
        if (launch.Token != token || launch.SolverSha256 != solverDigest || launch.Wrapper.Parent != host.Pid ||
            Identity.Read(host.Pid).StartTime != host.StartTime ||
            launch.Wrapper.Group != launch.Wrapper.Pid || launch.Wrapper.Session != launch.Wrapper.Pid ||
            launch.Solver.Parent != launch.Wrapper.Pid || launch.Solver.Group != launch.Wrapper.Pid ||
            launch.Solver.Session != launch.Wrapper.Pid) {
          throw new InvalidDataException("Launch ownership does not match this host/session.");
        }
        Track(launch.Wrapper, "wrapper"); Track(launch.Solver, "solver");
        launches.Add(directory, launch);
        File.WriteAllText(Path.Combine(directory, "admitted"), token);
      }
      TrackMembers();
    }

    private void TrackMembers() {
      var members = ReadMembers(leaf);
      if (members.Length > 256) { throw new InvalidDataException("More than 256 owned live processes."); }
      foreach (var pid in members) {
        if (tracked.Values.Any(p => p.Identity.Pid == pid && !Native.Exited(p.Handle))) { continue; }
        // An exclusive leaf provides inherited descendant ownership, including reparenting
        // and new sessions. No numeric PID alone is ever a signalling authority.
        try { Track(Identity.Read(pid), "descendant-or-pending-wrapper"); }
        catch (IOException) when (!ReadMembers(leaf).Contains(pid)) {
          // A naturally exiting member can disappear between kernel snapshots.
        }
      }
    }

    public ProofCleanup StopAndAssertNoOwnedSolvers(TimeSpan safetyDeadline) {
      if (cleaned || disposed || safetyDeadline <= TimeSpan.Zero) { throw new InvalidOperationException("Invalid cleanup lifecycle."); }
      var deadline = Stopwatch.StartNew();
      CloseAdmission(safetyDeadline);
      var forced = false;
      while (deadline.Elapsed < safetyDeadline) {
        lock (gate) {
          try { Scan(); }
          catch (Exception exception) {
            monitorFailure ??= exception.GetType().Name + ": " + exception.Message;
            // Metadata faults fail the receipt, but never skip owned-process cleanup.
            TrackMembers();
          }
          foreach (var process in tracked.Values.Where(p => !Native.Exited(p.Handle))) {
            if (process.Kind == "wrapper") { continue; } // Let wrapper drain logs/reap solver first.
            RequestSignal(process, deadline.Elapsed < safetyDeadline / 2 ? 15 : 9);
          }
          if (!Populated(leaf) && ReadMembers(leaf).Length == 0) { break; }
          if (deadline.Elapsed >= safetyDeadline * 3 / 4) {
            forced = true;
            foreach (var process in tracked.Values.Where(p => !Native.Exited(p.Handle))) { RequestSignal(process, 9); }
          }
        }
        Thread.Sleep(20);
      }
      stop.Cancel();
      if (!monitor.Wait(TimeSpan.FromSeconds(1))) { throw new TimeoutException("Ownership monitor did not stop."); }
      lock (gate) {
        var live = tracked.Values.Count(p => !Native.Exited(p.Handle));
        var members = ReadMembers(leaf);
        var populated = Populated(leaf);
        var failures = new List<string>();
        if (monitorFailure != null) { failures.Add(monitorFailure); }
        if (Directory.GetFiles(root, "wrapper-failure-*.json").Length != 0) { failures.Add("A wrapper reported a launch/relay failure."); }
        if (forced) { failures.Add("A wrapper required forced termination; stream completion is unproven."); }
        if (launches.Count == 0 || directoriesWithoutReceipt()) { failures.Add("Incomplete solver-launch ledger."); }
        foreach (var directory in launches.Keys) {
          var complete = Path.Combine(directory, "complete.json");
          if (!File.Exists(complete) || new FileInfo(complete).Length > 16384) { failures.Add("Missing/oversized stream receipt."); continue; }
          using var document = JsonDocument.Parse(File.ReadAllBytes(complete));
          var completion = document.RootElement;
          if (completion.GetProperty("token").GetString() != token || completion.GetProperty("errors").GetArrayLength() != 0 ||
              completion.GetProperty("solverExitCode").GetInt32() is not (0 or -15 or -9) ||
              !completion.GetProperty("streams").EnumerateObject().Select(s => s.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(new[] { "stderr", "stdin", "stdout" }) ||
              !completion.GetProperty("streams").EnumerateObject().All(s =>
                s.Value.GetProperty("readBytes").GetInt64() >= 0 && s.Value.GetProperty("readBytes").GetInt64() <= 64L * 1024 * 1024 &&
                s.Value.GetProperty("readBytes").GetInt64() == s.Value.GetProperty("writtenBytes").GetInt64() &&
                s.Value.GetProperty("readSha256").GetString() == s.Value.GetProperty("writtenSha256").GetString() &&
                new FileInfo(Path.Combine(directory, s.Name + ".bin")).Length == s.Value.GetProperty("readBytes").GetInt64() &&
                Framework.Sha256(Path.Combine(directory, s.Name + ".bin")) == s.Value.GetProperty("readSha256").GetString() &&
                (s.Name == "stdin" || s.Value.GetProperty("end").GetString() == "eof"))) {
            failures.Add("A solver stream was not forwarded completely and unchanged.");
          }
        }
        if (live != 0 || populated || members.Length != 0) { failures.Add("Owned processes remain live."); }
        var evidence = Path.Combine(root, "cleanup.json");
        File.WriteAllBytes(evidence, JsonSerializer.SerializeToUtf8Bytes(new {
          token, launches = launches.Values, processes = tracked.Values.Select(p => new { p.Identity, p.Kind }),
          liveOwnedProcesses = live, populated, finalMembers = members, signalRequests, failures, admissionClosed = true
        }, Json));
        if (failures.Count != 0) { throw new InvalidOperationException("Native ownership cleanup failed; evidence: " + evidence); }
        Directory.Delete(leaf); // Kernel refuses deletion if a late live member appeared.
        cleaned = true;
        return new(launches.Count, 0, evidence + " sha256=" + Framework.Sha256(evidence));
      }

      bool directoriesWithoutReceipt() => Directory.GetDirectories(Path.Combine(root, "launches")).Length != launches.Count;
    }

    private void RequestSignal(Tracked process, int signal) {
      var delivered = Native.Signal(process.Handle, signal);
      signalRequests.Add(new { process.Identity, process.Kind, signal, delivered, phase = "after-cli-completion-or-recorded-invocation-failure" });
    }

    private void CloseAdmission(TimeSpan deadline) {
      using var file = new FileStream(Path.Combine(root, "admission.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
      var clock = Stopwatch.StartNew();
      while (Native.Flock(file.SafeFileHandle, 6) != 0) {
        if (Marshal.GetLastPInvokeError() != 11 || clock.Elapsed >= deadline) { throw new IOException("Could not close launch admission."); }
        Thread.Sleep(10);
      }
      try { File.WriteAllText(Path.Combine(root, "closed"), token); }
      finally { Native.Flock(file.SafeFileHandle, 8); }
    }

    public void Dispose() {
      if (disposed) { return; }
      disposed = true;
      stop.Cancel();
      if (!monitor.Wait(TimeSpan.FromSeconds(1))) { throw new TimeoutException("Ownership monitor survived disposal."); }
      foreach (var process in tracked.Values) { process.Handle.Dispose(); }
      stop.Dispose();
      // Never erase a failure's cgroup or claim it cleaned. Outer host must stop.
      if (!cleaned) { throw new InvalidOperationException("The native scope was not proven empty; retain failure evidence and stop the host."); }
    }
  }

  private static int[] ReadMembers(string group) {
    var text = File.ReadAllText(Path.Combine(group, "cgroup.procs"));
    if (text.Length > 65536) { throw new InvalidDataException("Oversized cgroup member ledger."); }
    return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
      .Select(p => int.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).Distinct().ToArray();
  }
  private static bool Populated(string group) => File.ReadAllLines(Path.Combine(group, "cgroup.events"))
    .Single(line => line.StartsWith("populated ", StringComparison.Ordinal)) != "populated 0";

  private static class Native {
    [StructLayout(LayoutKind.Sequential)] private struct PollFd { public int Fd; public short Events; public short Returned; }
    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)] private static extern long PidFdOpen(long number, int pid, uint flags);
    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)] private static extern long PidFdSignal(long number, int fd, int signal, nint info, uint flags);
    [DllImport("libc", EntryPoint = "poll", SetLastError = true)] private static extern int Poll(ref PollFd fd, nuint count, int timeout);
    [DllImport("libc", EntryPoint = "flock", SetLastError = true)] internal static extern int Flock(SafeFileHandle fd, int operation);
    public static SafeFileHandle OpenPidFd(int pid) {
      var fd = PidFdOpen(434, pid, 0);
      if (fd < 0) { throw new IOException("pidfd_open failed: " + Marshal.GetLastPInvokeError()); }
      return new((nint)fd, ownsHandle: true);
    }
    public static bool Exited(SafeFileHandle handle) {
      var fd = new PollFd { Fd = handle.DangerousGetHandle().ToInt32(), Events = 1, Returned = 0 };
      if (Poll(ref fd, 1, 0) < 0) { throw new IOException("pidfd poll failed: " + Marshal.GetLastPInvokeError()); }
      return (fd.Returned & (1 | 16)) != 0;
    }
    public static bool Signal(SafeFileHandle handle, int signal) {
      var result = PidFdSignal(424, handle.DangerousGetHandle().ToInt32(), signal, 0, 0);
      if (result < 0 && Marshal.GetLastPInvokeError() != 3) {
        throw new IOException("pidfd_send_signal failed: " + Marshal.GetLastPInvokeError());
      }
      return result >= 0;
    }
  }
}
