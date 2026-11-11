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

  private readonly object writeLock = new();
  private readonly StreamWriter writer;
  private readonly Stopwatch clock = Stopwatch.StartNew();
  private readonly ConcurrentDictionary<object, TaskTimes> tasks = new(ReferenceEqualityComparer.Instance);
  private long eventCount;
  private int finished;

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
    return Profiles.GetOrAdd(Path.GetFullPath(path), p => new PipelineProfile(p, options!));
  }

  private PipelineProfile(string path, DafnyOptions options) {
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory)) {
      Directory.CreateDirectory(directory);
    }
    writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
      AutoFlush = true
    };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => Finish("process-exit");

    var process = Process.GetCurrentProcess();
    Emit(new Dictionary<string, object?> {
      ["ev"] = "manifest",
      ["schema"] = 1,
      ["utc"] = DateTime.UtcNow.ToString("O"),
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
    using var process = Process.GetCurrentProcess();
    return process.TotalProcessorTime.TotalMilliseconds;
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
  }

  private void Emit(Dictionary<string, object?> ev) {
    var line = JsonSerializer.Serialize(ev, JsonOptions);
    lock (writeLock) {
      try {
        writer.WriteLine(line);
        eventCount++;
      } catch (ObjectDisposedException) {
        // After process exit has closed the stream.
      }
    }
  }
}
