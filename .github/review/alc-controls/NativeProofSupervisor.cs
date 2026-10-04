using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
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
    if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64) {
      throw new PlatformNotSupportedException("The native supervisor currently requires Linux x64 pidfds and cgroup v2.");
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
    return new Scope(root, delegatedParent, leaf, token, host, solverDigest);
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
  private sealed record SolverImage(string Sha256, long Bytes, int Seals, int RequiredSeals, bool NativeElf, bool ExecutableMemfd);
  private sealed record Launch(string Token, Identity Wrapper, Identity Solver, string SolverSha256, SolverImage SolverImage, string[] Arguments);
  private sealed record Tracked(Identity Identity, SafeFileHandle Handle, string Kind);

  private sealed class Scope : IProofRunScope {
    private readonly string root;
    private readonly string parent;
    private readonly string leaf;
    private readonly SafeFileHandle parentDirectory;
    private readonly SafeFileHandle leafDirectory;
    private readonly string token;
    private readonly Identity host;
    private readonly string solverDigest;
    private readonly object gate = new();
    private readonly Dictionary<(int, ulong), Tracked> tracked = [];
    private readonly Dictionary<string, Launch> launches = [];
    private readonly List<object> signalRequests = [];
    private readonly HashSet<(int, ulong, int)> signalled = [];
    private readonly List<string> failures = [];
    private readonly List<object> fallbackRequests = [];
    private readonly List<object> streamReceipts = [];
    private readonly CancellationTokenSource stop = new();
    private readonly Task monitor;
    private string? monitorFailure;
    private bool cleaned;
    private bool disposed;
    private bool admissionClosed;
    private bool leafRemoved;
    private bool monitorStopped;

    public Scope(string root, string parent, string leaf, string token, Identity host, string solverDigest) {
      this.root = root; this.parent = parent; this.leaf = leaf; this.token = token; this.host = host; this.solverDigest = solverDigest;
      parentDirectory = Native.OpenDirectory(parent);
      SafeFileHandle? capturedLeaf = null;
      try {
        leafDirectory = capturedLeaf = Native.OpenChildDirectory(parentDirectory, Path.GetFileName(leaf));
        ValidateOwnedLeaf();
        using (Native.OpenOwnedKillFile(leafDirectory)) { } // Permission/capability check on an empty leaf; no write or signal.
      } catch { capturedLeaf?.Dispose(); parentDirectory.Dispose(); throw; }
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
      if (tracked.Count >= 4096) { throw new InvalidDataException("More than 4096 historical owned process identities."); }
      if (!ReadOwnedMembers().Contains(identity.Pid) || Identity.Read(identity.Pid).StartTime != identity.StartTime) {
        throw new InvalidDataException("Ownership identity changed before pidfd capture.");
      }
      var descriptor = Native.OpenPidFd(identity.Pid);
      try {
        if (Identity.Read(identity.Pid).StartTime != identity.StartTime || !ReadOwnedMembers().Contains(identity.Pid)) {
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
        if (launch.Token != token || launch.SolverSha256 != solverDigest || !ValidImage(launch.SolverImage) || launch.Wrapper.Parent != host.Pid ||
            Identity.Read(host.Pid).StartTime != host.StartTime ||
            launch.Wrapper.Group != launch.Wrapper.Pid || launch.Wrapper.Session != launch.Wrapper.Pid ||
            launch.Solver.Parent != launch.Wrapper.Pid || launch.Solver.Group != launch.Wrapper.Pid ||
            launch.Solver.Session != launch.Wrapper.Pid) {
          throw new InvalidDataException("Launch ownership does not match this host/session.");
        }
        Track(launch.Wrapper, "wrapper"); Track(launch.Solver, "solver");
        launches.Add(directory, launch);
        AtomicToken(Path.Combine(directory, "admitted"));
      }
      TrackMembers();
    }

    private void TrackMembers() {
      var members = ReadOwnedMembers();
      if (members.Length > 256) { throw new InvalidDataException("More than 256 owned live processes."); }
      foreach (var pid in members) {
        if (tracked.Values.Any(p => p.Identity.Pid == pid && !Native.Exited(p.Handle))) { continue; }
        // An exclusive leaf provides inherited descendant ownership, including reparenting
        // and new sessions. No numeric PID alone is ever a signalling authority.
        try { Track(Identity.Read(pid), "descendant-or-pending-wrapper"); }
        catch (IOException) when (!ReadOwnedMembers().Contains(pid)) {
          // A naturally exiting member can disappear between kernel snapshots.
        }
      }
    }

    public ProofCleanup StopAndAssertNoOwnedSolvers(TimeSpan safetyDeadline) {
      if (cleaned || disposed) { throw new InvalidOperationException("Invalid cleanup lifecycle."); }
      if (safetyDeadline <= TimeSpan.Zero || safetyDeadline > TimeSpan.FromMinutes(2)) {
        failures.Add("Invalid cleanup deadline; use a bounded failure-only drain.");
        safetyDeadline = TimeSpan.FromSeconds(10);
      }
      var clock = Stopwatch.StartNew();
      try {
        CloseAdmission(TimeSpan.FromTicks(Math.Min(safetyDeadline.Ticks / 8, TimeSpan.FromSeconds(1).Ticks)));
        while (clock.Elapsed < safetyDeadline / 2) {
          if (!Monitor.TryEnter(gate, TimeSpan.FromMilliseconds(50))) { throw new TimeoutException("Ownership monitor held the ledger lock."); }
          try {
            // A cap/metadata/signal fault leaves this loop immediately. The finally
            // drain does not depend on another successful Scan or tracked ledger.
            Scan();
            if (monitorFailure != null) { throw new InvalidOperationException(monitorFailure); }
            foreach (var process in tracked.Values.Where(p => !Native.Exited(p.Handle))) {
              if (process.Kind != "wrapper") { RequestSignal(process, clock.Elapsed < safetyDeadline / 4 ? 15 : 9); }
            }
            if (OwnedEmpty()) { break; }
          } finally { Monitor.Exit(gate); }
          Thread.Sleep(20);
        }
      } catch (Exception exception) { Fault("normal cleanup", exception); }
      finally {
        StopMonitor(TimeSpan.FromTicks(Math.Min(safetyDeadline.Ticks / 8, TimeSpan.FromSeconds(1).Ticks)));
        // Even failure to acquire the admission flock or stop the monitor cannot
        // bypass the independently anchored, bounded exclusive-leaf drain.
        if (!admissionClosed) {
          try { AtomicToken(Path.Combine(root, "closed"), overwrite: true); }
          catch (Exception exception) { Fault("failure admission marker", exception); }
        }
        DrainExclusiveLeaf(clock, safetyDeadline);
      }

      var live = -1;
      if (monitorStopped) {
        try {
          if (monitorFailure != null) { failures.Add(monitorFailure); }
          live = tracked.Values.Count(p => !Native.Exited(p.Handle));
          if (live != 0) { failures.Add("Captured owned pidfds remain live."); }
          ValidateReceipts();
        } catch (Exception exception) { Fault("final receipt validation", exception); }
      } else { failures.Add("The process ledger could not be frozen for validation."); }
      if (!leafRemoved) { failures.Add("The owned cgroup was not authoritatively empty and removed."); }
      var evidence = Path.Combine(root, "cleanup.json");
      var evidenceBytes = JsonSerializer.SerializeToUtf8Bytes(new {
        token, launches = monitorStopped ? launches.Values.ToArray() : [],
        processes = monitorStopped ? tracked.Values.Select(p => new { p.Identity, p.Kind }).ToArray() : [],
        liveOwnedProcesses = live, populated = leafRemoved ? (bool?)false : null,
        finalMembers = leafRemoved ? Array.Empty<int>() : null, leafRemoved, monitorStopped,
        signalRequests, fallbackRequests, streamReceipts, failures, admissionClosed
      }, Json);
      File.WriteAllBytes(evidence, evidenceBytes);
      if (failures.Count != 0) { throw new InvalidOperationException("Native ownership cleanup failed; evidence: " + evidence); }
      cleaned = true;
      return new(launches.Count, 0, evidence + " sha256=" + Digest(evidenceBytes));
    }

    private void ValidateReceipts() {
      if (Directory.GetFiles(root, "wrapper-failure-*.json").Length != 0) { failures.Add("A wrapper reported a launch/relay failure."); }
      if (launches.Count == 0 || Directory.GetDirectories(Path.Combine(root, "launches")).Length != launches.Count) {
        failures.Add("Incomplete solver-launch ledger.");
      }
      foreach (var (directory, launch) in launches) {
        try {
          var complete = Path.Combine(directory, "complete.json");
          if (!File.Exists(complete) || new FileInfo(complete).Length > 16384) { throw new InvalidDataException("Missing/oversized stream receipt."); }
          var bytes = File.ReadAllBytes(complete);
          if (bytes.Length > 16384) { throw new InvalidDataException("Oversized stream receipt."); }
          using var document = JsonDocument.Parse(bytes);
          var completion = document.RootElement;
          var image = completion.GetProperty("solverImage").Deserialize<SolverImage>(Json);
          var headerValid = completion.GetProperty("token").GetString() == token &&
            completion.GetProperty("errors").GetArrayLength() == 0 && image == launch.SolverImage && ValidImage(image) &&
            completion.GetProperty("solverExitCode").GetInt32() is (0 or -15 or -9) &&
            completion.GetProperty("streams").EnumerateObject().Select(s => s.Name).Order(StringComparer.Ordinal)
              .SequenceEqual(new[] { "stderr", "stdin", "stdout" });
          var streams = new List<object>();
          var allStreamsValid = true;
          foreach (var name in new[] { "stderr", "stdin", "stdout" }) {
            var stream = completion.GetProperty("streams").GetProperty(name);
            var path = Path.Combine(directory, name + ".bin");
            using var captured = File.OpenRead(path);
            var capturedBytes = captured.Length;
            if (capturedBytes > 64L * 1024 * 1024) { throw new InvalidDataException("Oversized captured stream."); }
            // Hash this opened stream once; bind that same digest to validation
            // and cleanup.json instead of reopening an independently mutable path.
            var digest = Convert.ToHexString(SHA256.HashData(captured)).ToLowerInvariant();
            var valid = stream.GetProperty("readBytes").GetInt64() == capturedBytes &&
              stream.GetProperty("writtenBytes").GetInt64() == capturedBytes &&
              stream.GetProperty("readSha256").GetString() == digest && stream.GetProperty("writtenSha256").GetString() == digest &&
              (name == "stdin" || stream.GetProperty("end").GetString() == "eof");
            allStreamsValid &= valid;
            streams.Add(new { name, bytes = capturedBytes, sha256 = digest, valid, end = stream.GetProperty("end").GetString() });
          }
          streamReceipts.Add(new {
            launch = Path.GetFileName(directory), completionSha256 = Digest(bytes), completionBytes = bytes.Length,
            headerValid, streams, valid = headerValid && allStreamsValid
          });
          if (!headerValid || !allStreamsValid) { failures.Add("A solver image/stream receipt failed exact validation."); }
        } catch (Exception exception) { Fault("launch receipt " + Path.GetFileName(directory), exception); }
      }
    }

    private bool ValidImage(SolverImage? image) => image != null && image.Bytes > 0 && image.Bytes <= 256L * 1024 * 1024 &&
      image.RequiredSeals == 15 && image.NativeElf && image.ExecutableMemfd && image.Sha256 == solverDigest && (image.Seals & 15) == 15;

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private void Fault(string phase, Exception exception) => failures.Add(phase + ": " + exception.GetType().Name + ": " + exception.Message);
    private string OwnedPath(string name) => Path.Combine("/proc/self/fd/" + leafDirectory.DangerousGetHandle().ToInt32(), name);
    private int[] ReadOwnedMembers() => ParseMembers(File.ReadAllText(OwnedPath("cgroup.procs")));
    private bool OwnedEmpty() => ReadOwnedMembers().Length == 0 &&
      File.ReadAllLines(OwnedPath("cgroup.events")).Single(line => line.StartsWith("populated ", StringComparison.Ordinal)) == "populated 0";

    private void ValidateOwnedLeaf() {
      var parentTarget = new FileInfo("/proc/self/fd/" + parentDirectory.DangerousGetHandle().ToInt32()).LinkTarget;
      var leafTarget = new FileInfo("/proc/self/fd/" + leafDirectory.DangerousGetHandle().ToInt32()).LinkTarget;
      if (parentTarget != parent || leafTarget != leaf || Path.GetDirectoryName(leaf) != parent ||
          new DirectoryInfo(parent).LinkTarget != null || new DirectoryInfo(leaf).LinkTarget != null ||
          (File.GetUnixFileMode(parent) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                                         UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0 ||
          File.ReadAllText(OwnedPath("cgroup.type")).Trim() != "domain") {
        throw new InvalidOperationException("The pinned owned leaf/private delegated parent identity changed.");
      }
    }

    private void DrainExclusiveLeaf(Stopwatch clock, TimeSpan deadline) {
      var requests = 0;
      var first = true;
      while (!leafRemoved && (first || clock.Elapsed < deadline)) {
        first = false; // An expired deadline still cannot skip the first owned drain attempt.
        var authorityValid = false;
        try {
          ValidateOwnedLeaf();
          authorityValid = true;
          if (OwnedEmpty()) {
            // Removal is an additional kernel check and prevents a wrapper that
            // passed admission before a failed flock from ever joining later.
            Directory.Delete(leaf);
            leafRemoved = true;
            break;
          }
        } catch (Exception exception) {
          if (requests < 8) {
            Fault("exclusive leaf drain", exception);
          }
        }
        // Failure to read membership/events or a capped ledger cannot suppress
        // cleanup after the exact pinned leaf authority has been revalidated.
        if (requests < 8) {
          requests++;
          if (authorityValid) {
            if (requests == 1) { failures.Add("Exclusive owned cgroup.kill fallback was required; forwarding completion is unproven."); }
            try {
              Native.KillOwnedCgroup(leafDirectory);
              fallbackRequests.Add(new { leaf, request = requests, elapsedMilliseconds = clock.ElapsedMilliseconds,
                authority = "pinned-exclusive-leaf-directory-fd", signal = 9, delivered = true });
            } catch (Exception exception) {
              Fault("owned cgroup.kill", exception);
              fallbackRequests.Add(new { leaf, request = requests, elapsedMilliseconds = clock.ElapsedMilliseconds,
                authority = "pinned-exclusive-leaf-directory-fd", delivered = false, error = exception.GetType().Name });
            }
          } else {
            fallbackRequests.Add(new { leaf, request = requests, elapsedMilliseconds = clock.ElapsedMilliseconds,
              authority = "unproven-pinned-leaf-no-signal", delivered = false });
          }
        }
        Thread.Sleep(20);
      }
      if (!leafRemoved) { failures.Add("Exclusive leaf drain deadline expired; emptiness is unproven. Stop the host."); }
    }

    private void StopMonitor(TimeSpan? wait = null) {
      stop.Cancel();
      try {
        monitorStopped = monitor.Wait(wait ?? TimeSpan.FromSeconds(1));
        if (!monitorStopped) { failures.Add("Ownership monitor did not stop; no process-ledger success may be claimed."); }
      } catch (Exception exception) { Fault("monitor shutdown", exception); }
    }

    private void RequestSignal(Tracked process, int signal) {
      if (!signalled.Add((process.Identity.Pid, process.Identity.StartTime, signal))) { return; }
      var delivered = Native.Signal(process.Handle, signal);
      signalRequests.Add(new { process.Identity, process.Kind, signal, delivered, phase = "after-cli-completion-or-recorded-invocation-failure" });
    }

    private void AtomicToken(string target, bool overwrite = false) {
      var temporary = target + ".pending-" + Guid.NewGuid().ToString("N");
      using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        output.Write(bytes);
        output.Flush(flushToDisk: true);
      }
      File.Move(temporary, target, overwrite);
    }

    private void CloseAdmission(TimeSpan deadline) {
      using var file = new FileStream(Path.Combine(root, "admission.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
      var clock = Stopwatch.StartNew();
      while (Native.Flock(file.SafeFileHandle, 6) != 0) {
        if (Marshal.GetLastPInvokeError() != 11 || clock.Elapsed >= deadline) { throw new IOException("Could not close launch admission."); }
        Thread.Sleep(10);
      }
      try { AtomicToken(Path.Combine(root, "closed"), overwrite: true); admissionClosed = true; }
      finally {
        if (Native.Flock(file.SafeFileHandle, 8) != 0) { throw new IOException("Could not unlock closed launch admission."); }
      }
    }

    public void Dispose() {
      if (disposed) { return; }
      disposed = true;
      if (!monitorStopped) { StopMonitor(); }
      if (!leafRemoved) {
        try { AtomicToken(Path.Combine(root, "closed"), overwrite: true); }
        catch (Exception exception) { Fault("disposal admission marker", exception); }
        // Disposal without successful cleanup is never green, but it still
        // attempts the same owned-only drain before releasing authority.
        DrainExclusiveLeaf(Stopwatch.StartNew(), TimeSpan.FromSeconds(5));
      }
      if (monitorStopped) {
        foreach (var process in tracked.Values) { process.Handle.Dispose(); }
        stop.Dispose();
        parentDirectory.Dispose();
        leafDirectory.Dispose();
      }
      if (!cleaned) {
        var evidence = Path.Combine(root, "dispose-cleanup.json");
        File.WriteAllBytes(evidence, JsonSerializer.SerializeToUtf8Bytes(new {
          token, leafRemoved, monitorStopped, admissionClosed, fallbackRequests, failures, successfulProofCleanup = false
        }, Json));
        throw new InvalidOperationException("Native scope failed cleanup; stop the host. Disposal evidence: " + evidence);
      }
    }
  }

  private static int[] ReadMembers(string group) {
    return ParseMembers(File.ReadAllText(Path.Combine(group, "cgroup.procs")));
  }
  private static int[] ParseMembers(string text) {
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
    [DllImport("libc", EntryPoint = "open", SetLastError = true)] private static extern int Open(string path, int flags);
    [DllImport("libc", EntryPoint = "openat", SetLastError = true)] private static extern int OpenAt(SafeFileHandle directory, string path, int flags);
    public static SafeFileHandle OpenDirectory(string path) {
      var fd = Open(path, 0x10000 | 0x80000 | 0x20000); // O_DIRECTORY | O_CLOEXEC | O_NOFOLLOW
      if (fd < 0) { throw new IOException("Owned directory open failed: " + Marshal.GetLastPInvokeError()); }
      return new((nint)fd, ownsHandle: true);
    }
    public static SafeFileHandle OpenChildDirectory(SafeFileHandle parent, string name) {
      var fd = OpenAt(parent, name, 0x10000 | 0x80000 | 0x20000);
      if (fd < 0) { throw new IOException("Owned child directory open failed: " + Marshal.GetLastPInvokeError()); }
      return new((nint)fd, ownsHandle: true);
    }
    public static SafeFileHandle OpenOwnedKillFile(SafeFileHandle leaf) {
      var fd = OpenAt(leaf, "cgroup.kill", 1 | 0x80000 | 0x20000); // O_WRONLY | O_CLOEXEC | O_NOFOLLOW
      if (fd < 0) { throw new IOException("Owned cgroup.kill open failed: " + Marshal.GetLastPInvokeError()); }
      return new((nint)fd, ownsHandle: true);
    }
    public static void KillOwnedCgroup(SafeFileHandle leaf) {
      using var descriptor = OpenOwnedKillFile(leaf);
      using var output = new FileStream(descriptor, FileAccess.Write);
      output.WriteByte((byte)'1');
      output.Flush();
    }
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
