#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using Microsoft.Boogie;

namespace Microsoft.Dafny;

/// <summary>
/// The opt-in sidecar of --pipeline-profile (#241, milestone D0): timing events of the stages of a
/// verification run, one JSON object per line.  It only observes: with the option unset, For
/// returns null and every call site does nothing; with it set, the events go to the named file and
/// nothing else that Dafny writes, nor anything it sends to the solver, changes.
///
/// Times are milliseconds on a monotonic clock whose zero is when the profile was opened; the
/// manifest records that instant in UTC and how long after the process started it was.
/// A span records process-wide deltas of allocated bytes, GC count per generation, GC pause time
/// and process CPU time over its interval; where other threads run at the same time those deltas
/// include their work, so they apportion a serial stretch exactly and a parallel one only roughly.
/// A task's queue wait (from its submission to Running) is kept apart from its solve (Running to Completed).
/// </summary>
public sealed class PipelineProfile {
  private static readonly ConcurrentDictionary<string, PipelineProfile> Profiles = new();
  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
  private static readonly ConcurrentDictionary<string, bool> FailedPaths = new();
  private static readonly object OpenLock = new();

  private readonly object writeLock = new();
  private readonly StreamWriter writer;
  private readonly string path;
  private readonly Stopwatch clock = Stopwatch.StartNew();
  private readonly ConcurrentDictionary<object, TaskTimes> tasks = new(ReferenceEqualityComparer.Instance);
  private long eventCount;
  private int finished;
  private volatile bool disabled;
  private readonly DateTime openedUtc = DateTime.UtcNow;
  private readonly bool heapMode;
  private readonly string? holdDirectory;
  private readonly GcEvents? gcEvents;

  // Counters for the summary.
  private long translations;
  private long preparations;
  private long tasksSubmitted;
  private long tasksCompleted;
  private long tasksCached;

  /// <summary>A module's name in events; the default module's is "_module".</summary>
  public static string ModuleName(ModuleDefinition module) {
    var name = module.FullDafnyName;
    return string.IsNullOrEmpty(name) ? module.Name : name;
  }

  public static PipelineProfile? For(DafnyOptions? options) {
    var path = options?.Get(CommonOptionBag.PipelineProfile);
    if (string.IsNullOrEmpty(path)) {
      return null;
    }
    string fullPath;
    try {
      fullPath = Path.GetFullPath(path);
    } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException) {
      ReportFailure(path, e);
      return null;
    }
    if (FailedPaths.ContainsKey(fullPath)) {
      return null;
    }
    lock (OpenLock) {
      if (Profiles.TryGetValue(fullPath, out var existing)) {
        return existing;
      }
      if (FailedPaths.ContainsKey(fullPath)) {
        return null;
      }
      try {
        var profile = new PipelineProfile(fullPath, options!);
        Profiles[fullPath] = profile;
        return profile;
      } catch (Exception e) {
        // The profile only observes: a file that cannot be opened disables it, and verification goes on.
        FailedPaths[fullPath] = true;
        ReportFailure(fullPath, e);
        return null;
      }
    }
  }

  private static void ReportFailure(string path, Exception e) {
    try {
      Console.Error.WriteLine($"Warning: --pipeline-profile disabled: cannot write {path}: {e.Message}");
    } catch (Exception) {
      // Nothing else to do.
    }
  }

  private PipelineProfile(string path, DafnyOptions options) {
    this.path = path;
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory)) {
      Directory.CreateDirectory(directory);
    }
    writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
      AutoFlush = true
    };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => Finish("process-exit");
    heapMode = options.Get(CommonOptionBag.PipelineProfileHeap);
    holdDirectory = options.Get(CommonOptionBag.PipelineProfileHold);
    if (string.IsNullOrEmpty(holdDirectory)) {
      holdDirectory = null;
    } else {
      Directory.CreateDirectory(holdDirectory);
    }

    var process = Process.GetCurrentProcess();
    Emit(new Dictionary<string, object?> {
      ["ev"] = "manifest",
      ["schema"] = 1,
      ["utc"] = openedUtc.ToString("O"),
      ["sinceProcessStartMs"] = Math.Round((DateTime.Now - process.StartTime).TotalMilliseconds, 3),
      ["dafnyVersion"] = options.VersionNumber,
      ["args"] = Environment.GetCommandLineArgs().Skip(1).ToArray(),
      ["cwd"] = Directory.GetCurrentDirectory(),
      ["os"] = RuntimeInformation.OSDescription,
      ["arch"] = RuntimeInformation.ProcessArchitecture.ToString(),
      ["runtime"] = RuntimeInformation.FrameworkDescription,
      ["processorCount"] = Environment.ProcessorCount,
      ["serverGC"] = GCSettings.IsServerGC,
      ["concurrentGC"] = AppContext.TryGetSwitch("System.GC.Concurrent", out var concurrent) ? concurrent : null,
      ["gcLatencyMode"] = GCSettings.LatencyMode.ToString(),
      ["cores"] = options.VcsCores,
      ["typeSystemRefresh"] = options.Get(CommonOptionBag.TypeSystemRefresh),
      ["resolutionThreads"] = options.Get(CommonOptionBag.ResolutionThreads),
      ["heap"] = heapMode,
      ["hold"] = holdDirectory != null,
      ["gcEvents"] = options.Get(CommonOptionBag.PipelineProfileGcEvents),
      ["threadCpu"] = ThreadCpu.CurrentThreadMs() != null,
      ["isolateAssertions"] = options.Get(BoogieOptionBag.IsolateAssertions),
      ["verifySnapshots"] = options.VerifySnapshots,
      ["randomSeed"] = options.RandomSeed,
      ["resourceLimit"] = options.ResourceLimit.ToString(),
      ["timeLimit"] = options.TimeLimit,
      ["proverPath"] = options.ProverOptions.FirstOrDefault(o => o.StartsWith("PROVER_PATH=")),
      ["environment"] = Environment.GetEnvironmentVariables().Keys.Cast<object>().Select(k => k.ToString()!)
        .Where(k => k.StartsWith("DOTNET_") || k.StartsWith("COMPlus_")).OrderBy(k => k)
        .ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k))
    });
    if (options.Get(CommonOptionBag.PipelineProfileGcEvents)) {
      gcEvents = GcEvents.Start(this);
    }
  }

  public double Now => Math.Round(clock.Elapsed.TotalMilliseconds, 3);

  /// <summary>A point in time: the process-wide counters that a span takes deltas of.</summary>
  public readonly record struct Mark(double Time, long Allocated, int Gen0, int Gen1, int Gen2, double PauseMs, double CpuMs);

  public Mark Begin() {
    return new Mark(Now, GC.GetTotalAllocatedBytes(false),
      GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
      GC.GetTotalPauseDuration().TotalMilliseconds, ProcessCpuMs());
  }

  private static double ProcessCpuMs() {
    try {
      using var process = Process.GetCurrentProcess();
      return process.TotalProcessorTime.TotalMilliseconds;
    } catch (Exception) {
      // Not available on every platform.
      return 0;
    }
  }

  /// <summary>Emit a span from <paramref name="start"/> to now, with extra fields.</summary>
  public void Span(string stage, Mark start, IDictionary<string, object?>? fields = null) {
    var end = Begin();
    var ev = new Dictionary<string, object?> {
      ["ev"] = "span",
      ["stage"] = stage,
      ["start"] = start.Time,
      ["end"] = end.Time,
      ["thread"] = Environment.CurrentManagedThreadId,
      ["allocBytes"] = end.Allocated - start.Allocated,
      ["gc"] = new[] { end.Gen0 - start.Gen0, end.Gen1 - start.Gen1, end.Gen2 - start.Gen2 },
      ["gcPauseMs"] = Math.Round(end.PauseMs - start.PauseMs, 3),
      ["cpuMs"] = Math.Round(end.CpuMs - start.CpuMs, 3)
    };
    if (fields != null) {
      foreach (var (key, value) in fields) {
        ev[key] = value;
      }
    }
    if (stage == "translate") {
      Interlocked.Increment(ref translations);
    } else if (stage == "prepare") {
      Interlocked.Increment(ref preparations);
    }
    Emit(ev);
  }

  /// <summary>
  /// A point in time for the front-end events (#241, milestone D1): the current thread's allocated
  /// bytes and CPU time, and the process's GC pause time and collection counts.  Unlike
  /// <see cref="Begin"/> it does not query the process, so it costs well under a microsecond.
  /// </summary>
  public readonly record struct ThreadMark(double Time, long ThreadAllocated, double? ThreadCpuMs, double PauseMs, int Gen0, int Gen1, int Gen2);

  public ThreadMark BeginThread() {
    return new ThreadMark(Now, GC.GetAllocatedBytesForCurrentThread(), ThreadCpu.CurrentThreadMs(),
      GC.GetTotalPauseDuration().TotalMilliseconds,
      GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
  }

  /// <summary>
  /// Emit a front-end span ("fe") from <paramref name="start"/> to now on this thread: wall time, this
  /// thread's CPU time and allocated bytes, and the process's GC pause time and collections in the
  /// interval. CPU time and allocation belong to this worker alone; simultaneous workers'
  /// events overlap in wall time, while GC deltas are process-wide.
  /// </summary>
  public void FrontEnd(string stage, ThreadMark start, IDictionary<string, object?>? fields = null) {
    var end = BeginThread();
    var ev = new Dictionary<string, object?> {
      ["ev"] = "fe",
      ["stage"] = stage,
      ["start"] = start.Time,
      ["end"] = end.Time,
      ["thread"] = Environment.CurrentManagedThreadId,
      ["threadAllocBytes"] = end.ThreadAllocated - start.ThreadAllocated,
      ["threadCpuMs"] = end.ThreadCpuMs != null && start.ThreadCpuMs != null
        ? Math.Round(end.ThreadCpuMs.Value - start.ThreadCpuMs.Value, 3) : null,
      ["gcPauseMs"] = Math.Round(end.PauseMs - start.PauseMs, 3),
      ["gc"] = new[] { end.Gen0 - start.Gen0, end.Gen1 - start.Gen1, end.Gen2 - start.Gen2 }
    };
    if (fields != null) {
      foreach (var (key, value) in fields) {
        ev[key] = value;
      }
    }
    Emit(ev);
  }

  [ThreadStatic] private static List<object?>? modulePhases;
  [ThreadStatic] private static ThreadMark modulePhaseMark;

  /// <summary>Whether this thread is recording a module's phases; guards building phase names.</summary>
  public static bool RecordingModulePhases => modulePhases != null;

  /// <summary>Start recording the phases of one module's resolution on this thread.</summary>
  public void ModulePhasesStart() {
    modulePhases = [];
    modulePhaseMark = BeginThread();
  }

  /// <summary>Close the current phase of the module being resolved on this thread, if any.</summary>
  public static void ModulePhase(DafnyOptions? options, string phase) {
    if (modulePhases == null) {
      return;
    }
    var profile = For(options);
    if (profile == null) {
      return;
    }
    var end = profile.BeginThread();
    modulePhases.Add(new Dictionary<string, object?> {
      ["phase"] = phase,
      ["ms"] = Math.Round(end.Time - modulePhaseMark.Time, 3),
      ["threadCpuMs"] = end.ThreadCpuMs != null && modulePhaseMark.ThreadCpuMs != null
        ? Math.Round(end.ThreadCpuMs.Value - modulePhaseMark.ThreadCpuMs.Value, 3) : null,
      ["threadAllocBytes"] = end.ThreadAllocated - modulePhaseMark.ThreadAllocated
    });
    modulePhaseMark = end;
  }

  /// <summary>The phases recorded since <see cref="ModulePhasesStart"/>; stops recording.</summary>
  public List<object?>? ModulePhasesTake() {
    var result = modulePhases;
    modulePhases = null;
    modulePhaseMark = default;
    return result;
  }

  private long heapPoints;

  /// <summary>
  /// With --pipeline-profile-heap, force a full blocking, compacting collection and record the
  /// live heap after it ("heap" event); with --pipeline-profile-hold, then wait for an external
  /// heap dump (see <see cref="Hold"/>).  Without either it does nothing.  The forced
  /// collection changes the timing of what follows, so heap runs are kept apart from timing runs.
  /// </summary>
  public void HeapPoint(string where, IDictionary<string, object?>? fields = null) {
    if (!heapMode && holdDirectory == null) {
      return;
    }
    var index = Interlocked.Increment(ref heapPoints);
    var ev = new Dictionary<string, object?> { ["ev"] = "heap", ["where"] = where, ["index"] = index };
    if (fields != null) {
      foreach (var (key, value) in fields) {
        ev[key] = value;
      }
    }
    if (heapMode) {
      var before = Now;
      var heapBefore = GC.GetTotalMemory(false);
      GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
      GC.Collect(2, GCCollectionMode.Forced, true, true);
      GC.WaitForPendingFinalizers();
      GC.Collect(2, GCCollectionMode.Forced, true, true);
      var info = GC.GetGCMemoryInfo(GCKind.FullBlocking);
      ev["t"] = Now;
      ev["collectMs"] = Math.Round(Now - before, 3);
      ev["heapBytesBefore"] = heapBefore;
      ev["liveBytes"] = GC.GetTotalMemory(false);
      ev["heapSizeBytes"] = info.HeapSizeBytes;
      ev["fragmentedBytes"] = info.FragmentedBytes;
      ev["promotedBytes"] = info.PromotedBytes;
      ev["committedBytes"] = info.TotalCommittedBytes;
      ev["generations"] = info.GenerationInfo.ToArray().Select(g => g.SizeAfterBytes).ToArray();
    } else {
      ev["t"] = Now;
    }
    Emit(ev);
    if (holdDirectory != null) {
      Hold(index, where);
    }
  }

  /// <summary>
  /// Write <c>&lt;dir&gt;/&lt;index&gt;.ready</c> (the process id and the point's name) and wait,
  /// up to 15 minutes, for <c>&lt;dir&gt;/&lt;index&gt;.go</c>, so that a driver can take a heap
  /// dump (dotnet-gcdump) at a fixed point of the run.
  /// </summary>
  private void Hold(long index, string where) {
    try {
      var ready = Path.Combine(holdDirectory!, $"{index}.ready");
      var go = Path.Combine(holdDirectory!, $"{index}.go");
      File.WriteAllText(ready + ".part", $"{Environment.ProcessId}\t{where}\n");
      File.Move(ready + ".part", ready, true);
      var waited = Stopwatch.StartNew();
      while (!File.Exists(go) && waited.Elapsed < TimeSpan.FromMinutes(15)) {
        Thread.Sleep(20);
      }
    } catch (Exception e) {
      ReportFailure(holdDirectory!, e);
    }
  }

  /// <summary>
  /// With --pipeline-profile-gc-events, one "gc" event per collection from the runtime's own GC
  /// events (the EventPipe provider Microsoft-Windows-DotNETRuntime, keyword GC): its number,
  /// generation, reason and type, when it started and ended, its promoted bytes and the heap after
  /// it; and one "gcpause" event per execution-engine suspension, the pause the program sees.  The events arrive on a listener thread, a little after the fact;
  /// their times are converted to the profile's clock.
  /// </summary>
  private sealed class GcEvents : System.Diagnostics.Tracing.EventListener {
    private readonly PipelineProfile profile;
    private DateTime? suspendBegin;
    private long? lastEnded;
    private readonly Dictionary<long, Dictionary<string, object?>> open = new();

    private GcEvents(PipelineProfile profile) {
      this.profile = profile;
    }

    public static GcEvents Start(PipelineProfile profile) {
      // The base constructor may deliver events before this class's fields are set; OnEventWritten
      // ignores those (its handler catches the null reference).
      return new GcEvents(profile);
    }

    protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source) {
      if (source.Name == "Microsoft-Windows-DotNETRuntime") {
        EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Informational,
          (System.Diagnostics.Tracing.EventKeywords)0x1);
      }
    }

    private double At(DateTime utc) => Math.Round((utc - profile.openedUtc).TotalMilliseconds, 3);

    protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e) {
      try {
        var payload = new Dictionary<string, object?>();
        if (e.PayloadNames != null && e.Payload != null) {
          for (var i = 0; i < e.PayloadNames.Count; i++) {
            payload[e.PayloadNames[i]] = e.Payload[i];
          }
        }
        long Num(string key) => payload.TryGetValue(key, out var v) && v != null ? Convert.ToInt64(v) : 0;
        lock (open) {
          switch (e.EventName) {
            case "GCSuspendEEBegin_V1":
              suspendBegin = e.TimeStamp;
              break;
            case "GCRestartEEEnd_V1":
              // One execution-engine suspension: the pause the program sees.
              if (suspendBegin != null) {
                profile.Emit(new Dictionary<string, object?> {
                  ["ev"] = "gcpause",
                  ["start"] = At(suspendBegin.Value),
                  ["end"] = At(e.TimeStamp),
                  ["ms"] = Math.Round((e.TimeStamp - suspendBegin.Value).TotalMilliseconds, 3)
                });
              }
              suspendBegin = null;
              break;
            case "GCStart_V2":
              open[Num("Count")] = new Dictionary<string, object?> {
                ["ev"] = "gc",
                ["count"] = Num("Count"),
                ["gen"] = Num("Depth"),
                ["reason"] = Num("Reason"),
                ["type"] = Num("Type"),
                ["start"] = At(e.TimeStamp)
              };
              break;
            case "GCEnd_V1":
              if (open.TryGetValue(Num("Count"), out var ended)) {
                ended["end"] = At(e.TimeStamp);
                lastEnded = Num("Count");
              }
              break;
            case "GCHeapStats_V2":
              // Follows the GCEnd of the collection it describes.
              if (lastEnded != null && open.Remove(lastEnded.Value, out var gc)) {
                gc["promotedBytes"] = Num("TotalPromotedSize0") + Num("TotalPromotedSize1") + Num("TotalPromotedSize2") +
                                      Num("TotalPromotedSize3") + Num("TotalPromotedSize4");
                gc["heapBytesAfter"] = Num("GenerationSize0") + Num("GenerationSize1") + Num("GenerationSize2") +
                                       Num("GenerationSize3") + Num("GenerationSize4");
                profile.Emit(gc);
              }
              lastEnded = null;
              break;
          }
        }
      } catch (Exception) {
        // The profile only observes.
      }
    }
  }

  /// <summary>Emit an instantaneous event.</summary>
  public void Point(string kind, IDictionary<string, object?> fields) {
    var ev = new Dictionary<string, object?> { ["ev"] = kind, ["t"] = Now };
    foreach (var (key, value) in fields) {
      ev[key] = value;
    }
    Emit(ev);
  }

  private sealed class TaskTimes {
    public double Submitted;
    public double? Queued;
    public double? Running;
  }

  public void TaskSubmitted(IVerificationTask task) {
    Interlocked.Increment(ref tasksSubmitted);
    tasks[task] = new TaskTimes { Submitted = Now };
  }

  /// <summary>Record a status of a task; on completion, emit its event.</summary>
  public void TaskStatus(string module, string symbol, IVerificationTask task, IVerificationStatus status, bool cached) {
    var now = Now;
    var times = tasks.GetOrAdd(task, _ => new TaskTimes { Submitted = now });
    switch (status) {
      case Queued:
        times.Queued ??= now;
        return;
      case Running:
        times.Running ??= now;
        return;
      case Completed completed:
        tasks.TryRemove(task, out _);
        Interlocked.Increment(ref tasksCompleted);
        if (cached) {
          Interlocked.Increment(ref tasksCached);
        }
        var result = completed.Result;
        Emit(new Dictionary<string, object?> {
          ["ev"] = "task",
          ["module"] = module,
          ["symbol"] = symbol,
          ["task"] = Compilation.GetTaskName(task),
          ["split"] = task.Split.SplitIndex,
          ["cached"] = cached,
          ["submitted"] = times.Submitted,
          ["queued"] = times.Queued,
          ["running"] = times.Running,
          ["completed"] = now,
          // Boogie reports Queued only when the task has to wait for a solver; the wait is from
          // submission to Running either way.
          ["queueWaitMs"] = times.Running != null ? Math.Round(times.Running.Value - times.Submitted, 3) : null,
          ["solveMs"] = times.Running != null ? Math.Round(now - times.Running.Value, 3) : null,
          ["solverRunMs"] = Math.Round(result.RunTime.TotalMilliseconds, 3),
          ["outcome"] = result.Outcome.ToString(),
          ["resourceCount"] = result.ResourceCount
        });
        return;
    }
  }

  /// <summary>Emit the summary, once.</summary>
  public void Finish(string reason) {
    if (Interlocked.Exchange(ref finished, 1) != 0) {
      return;
    }
    var end = Begin();
    var info = GC.GetGCMemoryInfo(GCKind.Any);
    long peakWorkingSet = 0;
    try {
      using var process = Process.GetCurrentProcess();
      peakWorkingSet = process.PeakWorkingSet64;
    } catch (Exception) {
      // Not available on every platform.
    }
    Emit(new Dictionary<string, object?> {
      ["ev"] = "summary",
      ["t"] = end.Time,
      ["reason"] = reason,
      ["allocBytes"] = end.Allocated,
      ["gc"] = new[] { end.Gen0, end.Gen1, end.Gen2 },
      ["gcPauseMs"] = Math.Round(end.PauseMs, 3),
      ["gcPauseTimePercentage"] = info.PauseTimePercentage,
      ["heapBytesAfterLastGC"] = info.HeapSizeBytes,
      ["cpuMs"] = Math.Round(end.CpuMs, 3),
      ["peakWorkingSetBytes"] = peakWorkingSet,
      ["translations"] = Interlocked.Read(ref translations),
      ["preparations"] = Interlocked.Read(ref preparations),
      ["tasksSubmitted"] = Interlocked.Read(ref tasksSubmitted),
      ["tasksCompleted"] = Interlocked.Read(ref tasksCompleted),
      ["tasksCached"] = Interlocked.Read(ref tasksCached),
      ["tasksUnfinished"] = tasks.Count,
      ["events"] = Interlocked.Read(ref eventCount) + 1
    });
    try {
      gcEvents?.Dispose();
    } catch (Exception) {
      // The profile only observes.
    }
  }

  private void Emit(Dictionary<string, object?> ev) {
    if (disabled) {
      return;
    }
    lock (writeLock) {
      if (disabled) {
        return;
      }
      try {
        writer.WriteLine(JsonSerializer.Serialize(ev, JsonOptions));
        eventCount++;
      } catch (ObjectDisposedException) {
        // After process exit has closed the stream.
        disabled = true;
      } catch (Exception e) {
        // The profile only observes: a failed write (a full disk, say) stops the profile, never verification.
        disabled = true;
        ReportFailure(path, e);
      }
    }
  }
}

/// <summary>The CPU time of the calling thread, where the platform gives it cheaply.</summary>
internal static class ThreadCpu {
  [StructLayout(LayoutKind.Sequential)]
  private struct TimeSpec {
    public long Seconds;
    public long Nanoseconds;
  }

  [DllImport("libc", EntryPoint = "clock_gettime", SetLastError = false)]
  private static extern int ClockGetTime(int clock, out TimeSpec time);

  [DllImport("kernel32.dll")]
  private static extern IntPtr GetCurrentThread();

  [DllImport("kernel32.dll")]
  private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);

  private static volatile bool unavailable;

  /// <summary>CLOCK_THREAD_CPUTIME_ID: 3 on Linux, 16 on macOS.</summary>
  private static readonly int ThreadClock = OperatingSystem.IsMacOS() ? 16 : 3;

  public static double? CurrentThreadMs() {
    if (unavailable) {
      return null;
    }
    try {
      if (OperatingSystem.IsWindows()) {
        return GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user)
          ? (kernel + user) / 10_000.0 : null;
      }
      if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) {
        return ClockGetTime(ThreadClock, out var time) == 0
          ? time.Seconds * 1000.0 + time.Nanoseconds / 1_000_000.0 : null;
      }
    } catch (Exception) {
      // No such entry point on this platform.
    }
    unavailable = true;
    return null;
  }
}
