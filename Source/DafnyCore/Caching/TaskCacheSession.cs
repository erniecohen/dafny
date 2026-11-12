#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Boogie.SMTLib;
using VC;

namespace Microsoft.Dafny.Caching;

public interface IReplayedVerificationTask {
  bool IsReplayed { get; }
  string? ExecutionKey { get; }
}

/// <summary>Owns one opt-in compilation's recording transports; never shares a solver history.</summary>
public sealed class TaskCacheSession {
  private static readonly ConditionalWeakTable<DafnyOptions, TaskCacheSession> Sessions = new();
  private static readonly AsyncLocal<Run?> Current = new();
  private readonly TaskResultCache results;
  private readonly DafnyOptions options;
  private long starts;
  private long hits;
  private long publications;
  private long fallbacks;
  private long conflicts;
  public long SolverStarts => Interlocked.Read(ref starts);
  public long Hits => Interlocked.Read(ref hits);
  public long Publications => Interlocked.Read(ref publications);
  public long Fallbacks => Interlocked.Read(ref fallbacks);
  public long Conflicts => Interlocked.Read(ref conflicts);
  public string? LastBypassReason { get; internal set; }
  private readonly TaskExecutionEnvironment environment;

  private sealed class Run(IVerificationTask task, bool disabled) {
    public readonly IVerificationTask Task = task;
    public readonly bool Disabled = disabled;
    public readonly List<CachedTaskSolver> Solvers = new();
  }

  private TaskCacheSession(DafnyOptions options, string directory) {
    this.options = options;
    results = new TaskResultCache(new ContentAddressedCache(directory));
    environment = new TaskExecutionEnvironment(options);
    var original = options.CreateSolver;
    options.RestartProverPerVC = true;
    options.CreateSolver = (library, solverOptions) => {
      var run = Current.Value;
      if (run == null || run.Disabled || original.Method.Module.Assembly != typeof(CommandLineOptions).Assembly || solverOptions.BatchMode || solverOptions.Solver != SolverKind.Z3 ||
          solverOptions.Inspector != null || solverOptions.LogFilename != null) {
        if (run != null) { Interlocked.Increment(ref starts); }
        return original(library, solverOptions);
      }
      var task = run.Task;
      var transport = new CachedTaskSolver(results,
        () => environment.Freeze(library, solverOptions, out var reason) is { } bytes ? bytes : Bypass(reason),
        () => { Interlocked.Increment(ref starts); return original(library, solverOptions); },
        library.PrintProverWarnings == CoreOptions.ProverWarnings.None);
      run.Solvers.Add(transport);
      return transport;
    };
  }

  private byte[]? Bypass(string? reason) { LastBypassReason = reason; return null; }

  public static TaskCacheSession? TryFor(DafnyOptions options) => Sessions.TryGetValue(options, out var value) ? value : null;

  public static TaskCacheSession? For(DafnyOptions options) {
    var directory = options.Get(CommonOptionBag.VerificationCache);
    if (string.IsNullOrEmpty(directory) || options.VerificationCacheBatchMode) { return null; }
    try { return Sessions.GetValue(options, o => new TaskCacheSession(o, directory)); }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return null; }
  }

  public IVerificationTask Wrap(IVerificationTask task) => new CachedVerificationTask(this, task);

  private sealed class CachedVerificationTask(TaskCacheSession session, IVerificationTask original)
    : IVerificationTask, IReplayedVerificationTask {
    private IVerificationTask active = original;
    private readonly object gate = new();
    private ReplaySubject<IVerificationStatus>? running;
    private IVerificationStatus status = new Stale();
    public bool IsReplayed { get; private set; }
    public string? ExecutionKey { get; private set; }
    public IVerificationTask FromSeed(int seed) => session.Wrap(original.FromSeed(seed));
    public IVerificationStatus CacheStatus => status;
    public ManualSplit Split => original.Split;
    public IToken ScopeToken => original.ScopeToken;
    public string ScopeId => original.ScopeId;
    public IToken Token => original.Token;
    public bool IsIdle { get { lock (gate) { return running == null; } } }
    public void Cancel() => active.Cancel();

    public IObservable<IVerificationStatus>? TryRun() {
      lock (gate) {
        if (running != null) { return null; }
        if (status is Completed) { return System.Reactive.Linq.Observable.Return(status); }
        var subject = new ReplaySubject<IVerificationStatus>();
        running = subject;
        _ = Execute(subject);
        return subject;
      }
    }

    private async Task Execute(ReplaySubject<IVerificationStatus> subject) {
      try {
        Completed? completed = null;
        for (var attempt = 0; attempt < 2; attempt++) {
          var context = new Run(active, attempt != 0);
          var previous = Current.Value;
          Current.Value = context;
          try {
            completed = await Observe(active, subject);
            if (context.Solvers.Any(s => s.ReplayMismatch)) { throw new CacheReplayMismatchException(); }
            if (completed != null && context.Solvers.Count == 1) {
              var solver = context.Solvers[0];
              var outcome = completed.Result.Outcome switch {
                SolverOutcome.Valid => CacheTaskOutcome.Correct,
                SolverOutcome.Invalid => CacheTaskOutcome.Errors,
                SolverOutcome.OutOfResource => CacheTaskOutcome.OutOfResource,
                _ => CacheTaskOutcome.Unknown
              };
              var publication = solver.CompleteNormally(outcome, completed.Result.ResourceCount);
              IsReplayed = solver.Replayed;
              ExecutionKey = solver.ExecutionKey;
              if (IsReplayed) { Interlocked.Increment(ref session.hits); }
              if (publication == CachePublication.Stored) { Interlocked.Increment(ref session.publications); }
              if (publication == CachePublication.Conflict) { Interlocked.Increment(ref session.conflicts); }
            } else {
              foreach (var solver in context.Solvers) { solver.Abort(); }
            }
            break;
          } catch (Exception) when (attempt == 0 && context.Solvers.Any(s => s.ReplayMismatch)) {
            foreach (var solver in context.Solvers) { solver.Abort(); }
            Interlocked.Increment(ref session.fallbacks);
            active = original.FromSeed(original.Split.RandomSeed);
          } finally {
            Current.Value = previous;
          }
        }
        if (completed != null) {
          PipelineProfile.For(session.options)?.Point("result-task", new Dictionary<string, object?> {
            ["task"] = ScopeId, ["split"] = Split.SplitIndex, ["seed"] = Split.RandomSeed,
            ["executionKey"] = ExecutionKey, ["replayed"] = IsReplayed,
            ["outcome"] = completed.Result.Outcome.ToString(), ["resourceCount"] = completed.Result.ResourceCount
          });
        }
        status = completed is null ? new Stale() : completed;
        subject.OnNext(status);
        lock (gate) { running = null; }
        subject.OnCompleted();
      } catch (Exception e) {
        lock (gate) { running = null; }
        subject.OnError(e);
      }
    }

    private static Task<Completed?> Observe(IVerificationTask task, ReplaySubject<IVerificationStatus> subject) {
      var finished = new TaskCompletionSource<Completed?>(TaskCreationOptions.RunContinuationsAsynchronously);
      Completed? completion = null;
      var observable = task.TryRun();
      if (observable == null) { return Task.FromResult(task.CacheStatus as Completed); }
      observable.Subscribe(update => {
        if (update is Completed value) { completion = value; }
        else { subject.OnNext(update); }
      }, error => finished.TrySetException(error), () => finished.TrySetResult(completion));
      return finished.Task;
    }
  }
}
