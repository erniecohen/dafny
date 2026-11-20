using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using VC;
using Xunit;
using Bpl = Microsoft.Boogie;

namespace DafnyCore.Test.Verification;

public class BoogieVerificationBackendTests {
  [Fact]
  public void InvalidStatusCannotBecomeWorkProgress() {
    Assert.Throws<ArgumentOutOfRangeException>(() => BoogieVerificationWork.ConvertStatus(null!));
  }

  [Theory]
  [InlineData(SolverOutcome.Valid, VerificationOutcome.Verified)]
  [InlineData(SolverOutcome.Invalid, VerificationOutcome.Failed)]
  [InlineData(SolverOutcome.Undetermined, VerificationOutcome.Inconclusive)]
  [InlineData(SolverOutcome.TimeOut, VerificationOutcome.Timeout)]
  [InlineData(SolverOutcome.OutOfResource, VerificationOutcome.ResourceExhaustion)]
  [InlineData(SolverOutcome.OutOfMemory, VerificationOutcome.OutOfMemory)]
  [InlineData(SolverOutcome.Bounded, VerificationOutcome.Bounded)]
  [InlineData((SolverOutcome)12345, VerificationOutcome.ToolError)]
  public void OutcomesRetainNativeEvidence(SolverOutcome nativeOutcome, VerificationOutcome outcome) {
    var result = new VerificationRunResult(7, 2, DateTime.UnixEpoch, nativeOutcome,
      TimeSpan.FromSeconds(3), 5, [], [], [], 123, null, []);
    var completed = Assert.IsType<VerificationWorkCompleted>(
      BoogieVerificationWork.ConvertStatus(new Completed(result)));
    Assert.Equal(outcome, completed.Result.Outcome);
    Assert.Null(completed.Result.Diagnostics); // Native reporting still owns diagnostic materialization.
    Assert.Equal(123, completed.Result.Metrics!.ResourceCount);
    Assert.Equal(TimeSpan.FromSeconds(3), completed.Result.Metrics.RunTime);
    Assert.Same(result, Assert.IsType<BoogieVerificationResult>(completed.Result.Payload).NativeResult);
  }

  [Fact]
  public void SchedulingAndCacheAreForwardedWithoutStartingWork() {
    var native = new NativeTask();
    var work = new BoogieVerificationWork(native);
    Assert.IsType<VerificationWorkStale>(work.CacheStatus);
    Assert.True(work.IsIdle);
    Assert.Same(native, Assert.Single(BoogieVerificationBackend.GetNativeTasks(VerificationPreparation.Ready([work]))));
    var seeded = Assert.IsType<BoogieVerificationWork>(work.FromSeed(73));
    Assert.Equal(73, native.Seed);
    Assert.Same(native.SeededTask, seeded.NativeTask);
    Assert.Equal(0, native.Runs);
    work.Cancel();
    Assert.Equal(1, native.Cancellations);
    native.NoRun = true;
    Assert.Null(work.TryRun());
    Assert.Equal(1, native.Runs);
    Assert.Null(work.TryRunForScheduling());
    Assert.Equal(2, native.Runs);
  }

  [Fact]
  public async Task StatusOrderCompletionAndCancellationArePreserved() {
    var native = new NativeTask();
    var work = new BoogieVerificationWork(native);
    var statuses = new List<VerificationWorkStatus>();
    var error = new TaskCanceledException("cancelled by caller");
    var completion = work.TryRun()!.Do(statuses.Add).LastAsync().ToTask();
    native.Updates.OnNext(new Queued());
    native.Updates.OnNext(new Running());
    native.Updates.OnError(error);
    Assert.Collection(statuses, s => Assert.IsType<VerificationWorkQueued>(s),
      s => Assert.IsType<VerificationWorkRunning>(s));
    Assert.Same(error, await Assert.ThrowsAsync<TaskCanceledException>(() => completion));
  }

  [Fact]
  public async Task CompletedCacheAndObservableRetainTheSameEvidence() {
    var result = new VerificationRunResult(0, 0, DateTime.UnixEpoch, SolverOutcome.Invalid,
      TimeSpan.Zero, 5, [], [], [], 99, null, []);
    var native = new NativeTask { Status = new Completed(result) };
    var work = new BoogieVerificationWork(native);
    var cached = Assert.IsType<VerificationWorkCompleted>(work.CacheStatus);
    Assert.Same(result, Assert.IsType<BoogieVerificationResult>(cached.Result.Payload).NativeResult);
    var completion = work.TryRun()!.LastAsync().ToTask();
    native.Updates.OnNext(new Completed(result));
    native.Updates.OnCompleted();
    var completed = Assert.IsType<VerificationWorkCompleted>(await completion);
    Assert.Same(result, Assert.IsType<BoogieVerificationResult>(completed.Result.Payload).NativeResult);
    Assert.Equal(VerificationOutcome.Failed, completed.Result.Outcome);
  }

  [Fact]
  public void NativeSchedulingRetainsImmediateResultPublication() {
    var result = new VerificationRunResult(0, 0, DateTime.UnixEpoch, SolverOutcome.Valid,
      TimeSpan.Zero, 5, [], [], [], 99, null, []);
    var native = new NativeTask();
    IVerificationWork work = new BoogieVerificationWork(native);
    var received = new List<VerificationWorkStatus>();
    var closed = false;
    using var subscription = work.TryRunForScheduling()!.Subscribe(received.Add, () => closed = true);
    native.Updates.OnNext(new Completed(result));
    var completed = Assert.IsType<VerificationWorkCompleted>(Assert.Single(received));
    Assert.Same(result, BoogieVerificationReporting.GetResult(completed.Result));
    Assert.False(closed);
    native.Updates.OnCompleted();
    Assert.True(closed);
    Assert.Single(received);
  }

  [Theory]
  [InlineData("empty", 0)]
  [InlineData("progress-only", 1)]
  [InlineData("duplicate", 1)]
  [InlineData("after-terminal", 1)]
  public void NativeSchedulingAuditsMalformedClosure(string script, int delivered) {
    var native = new NativeTask();
    IVerificationWork work = new BoogieVerificationWork(native);
    var received = new List<VerificationWorkStatus>();
    var errors = new List<Exception>();
    var closed = false;
    using var subscription = work.TryRunForScheduling()!.Subscribe(received.Add, errors.Add, () => closed = true);
    var result = new Completed(new VerificationRunResult(0, 0, DateTime.UnixEpoch, SolverOutcome.Valid,
      TimeSpan.Zero, 5, [], [], [], 99, null, []));
    if (script == "progress-only") { native.Updates.OnNext(new Running()); }
    if (script is "duplicate" or "after-terminal") {
      native.Updates.OnNext(result);
      Assert.Single(received); // Native publication must still precede closure.
      native.Updates.OnNext(script == "duplicate" ? result : new Running());
    }
    native.Updates.OnCompleted();
    Assert.IsType<InvalidOperationException>(Assert.Single(errors));
    Assert.False(closed);
    Assert.Equal(delivered, received.Count);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public void NativeAuditRetainsErrorsAndCancellationAfterPublication(bool afterResult, bool cancelled) {
    var native = new NativeTask();
    IVerificationWork work = new BoogieVerificationWork(native);
    var received = new List<VerificationWorkStatus>();
    Exception? failure = null;
    var closed = false;
    using var subscription = work.TryRunForScheduling()!.Subscribe(received.Add, e => failure = e, () => closed = true);
    if (afterResult) {
      native.Updates.OnNext(new Completed(new VerificationRunResult(0, 0, DateTime.UnixEpoch, SolverOutcome.Valid,
        TimeSpan.Zero, 5, [], [], [], 99, null, [])));
    }
    var error = cancelled ? (Exception)new OperationCanceledException() : new InvalidOperationException("native worker error");
    native.Updates.OnError(error);
    Assert.Same(error, failure);
    Assert.False(closed);
    Assert.Equal(afterResult ? 1 : 0, received.Count);
  }

  [Theory]
  [InlineData("procedure P(); implementation P() { assert true; }", true)]
  [InlineData("procedure P(); implementation P() { assert 0; }", false)]
  [InlineData("procedure P(); implementation P() { assert missing; }", false)]
  public async Task RealPreparationRejectsInvalidInput(string source, bool ready) {
    Assert.Equal(0, Bpl.Parser.Parse(source, "boundary.bpl", out var input));
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    using var engine = new ExecutionEngine(options, new EmptyVerificationResultCache(), TaskScheduler.Default);
    IVerificationBackend<Bpl.Program> backend = new BoogieVerificationBackend(engine);
    var preparation = await backend.PrepareAsync(input, CancellationToken.None);
    Assert.Equal(ready, preparation.IsReady);
    if (ready) {
      var work = Assert.IsType<BoogieVerificationWork>(Assert.Single(preparation.GetWorkOrThrow()));
      Assert.IsType<VerificationWorkStale>(work.CacheStatus);
      Assert.True(work.IsIdle);
      Assert.Same(work.NativeTask, Assert.Single(BoogieVerificationBackend.GetNativeTasks(preparation)));
      Assert.Equal(work.NativeTask.ScopeId, work.Source.Id.Scope);
      Assert.Equal(work.NativeTask.Split.SplitIndex, work.Source.Id.Part);
    } else {
      Assert.Equal(VerificationOutcome.ToolError, preparation.FailureOutcome);
      Assert.IsType<ArgumentException>(preparation.Error);
      Assert.Same(preparation.Error, Assert.Throws<ArgumentException>(() =>
        BoogieVerificationBackend.GetNativeTasks(preparation)));
    }
  }

  [Fact]
  public void NativeBridgeRejectsForeignBackendWork() {
    Assert.Throws<InvalidOperationException>(() =>
      BoogieVerificationBackend.GetNativeTasks(VerificationPreparation.Ready([new ForeignWork()])));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task NativePreparationPreservesOptInCacheWrapping(bool enabled) {
    var directory = Path.Combine(Path.GetTempPath(), "verification-boundary-" + Guid.NewGuid());
    try {
      Assert.Equal(0, Bpl.Parser.Parse("procedure P(); implementation P() { assert true; }",
        "boundary.bpl", out var input));
      var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
      options.ApplyDefaultOptionsWithoutSettingsDefault();
      if (enabled) { options.Set(CommonOptionBag.VerificationCache, directory); }
      using var engine = new ExecutionEngine(options, new EmptyVerificationResultCache(), TaskScheduler.Default);
      var backend = new BoogieVerificationBackend(engine, TaskCacheSession.For(options));
      var prepared = await backend.PrepareAsync(input, CancellationToken.None);
      var native = Assert.Single(BoogieVerificationBackend.GetNativeTasks(prepared));
      Assert.Equal(enabled, native is IReplayedVerificationTask);
      Assert.Same(native, Assert.IsType<BoogieVerificationWork>(Assert.Single(prepared.GetWorkOrThrow())).NativeTask);
    } finally {
      if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
    }
  }

  [Fact]
  public void ReportingRetainsNativeIdentityAndRejectsForeignEvidence() {
    var task = new NativeTask();
    var nativeResult = new VerificationRunResult(0, 0, DateTime.UnixEpoch, SolverOutcome.Invalid,
      TimeSpan.Zero, 5, [], [], [], 99, null, []);
    var result = new VerificationWorkResult(VerificationOutcome.Failed, Payload: new BoogieVerificationResult(nativeResult));
    var pair = BoogieVerificationReporting.GetTaskResult(new BoogieVerificationWork(task), result);
    Assert.Same(task, pair.Task);
    Assert.Same(nativeResult, pair.Result);
    Assert.Throws<InvalidOperationException>(() => BoogieVerificationReporting.GetTask(new ForeignWork()));
    Assert.Throws<InvalidOperationException>(() => BoogieVerificationReporting.GetResult(new(VerificationOutcome.Unsupported)));
  }

  [Theory]
  [InlineData(SolverOutcome.Valid)]
  [InlineData(SolverOutcome.Invalid)]
  [InlineData(SolverOutcome.Undetermined)]
  [InlineData(SolverOutcome.TimeOut)]
  [InlineData(SolverOutcome.OutOfResource)]
  [InlineData(SolverOutcome.OutOfMemory)]
  [InlineData(SolverOutcome.Bounded)]
  public void NativeDiagnosticCapabilityPreservesOriginalPolicy(SolverOutcome outcome) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    var task = new NativeTask();
    var nativeResult = new VerificationRunResult(0, 0, DateTime.UnixEpoch, outcome,
      TimeSpan.FromSeconds(7), 5, [], [], [], 99, null, []);
    var result = Assert.IsType<VerificationWorkCompleted>(BoogieVerificationWork.ConvertStatus(new Completed(nativeResult))).Result;
    var direct = new BatchErrorReporter(options);
    var adapted = new BatchErrorReporter(options);
    Compilation.ReportDiagnosticsInResult(options, "scope", BoogieGenerator.ToDafnyToken(task.Token),
      7, nativeResult, direct);
    new VerificationWorkItemResult(new BoogieVerificationWork(task), result).ReportDiagnostics(options, "scope", adapted);
    Assert.Equal(direct.AllMessages.Select(d => (d.Source, d.ErrorId, d.Message, d.Level, Range: d.Range.ToString())),
      adapted.AllMessages.Select(d => (d.Source, d.ErrorId, d.Message, d.Level, Range: d.Range.ToString())));
    Assert.True(Assert.IsAssignableFrom<IVerificationDiagnosticReporting>(result.Payload).UsesLegacyDiagnosticPolicy);
    var counts = Assert.IsAssignableFrom<IVerificationAssertionStatistics>(result.Payload);
    Assert.Equal(nativeResult.Asserts.Count, counts.AssertionCount);
    Assert.Equal(nativeResult.CounterExamples.Count, counts.FailedAssertionCount);
    Assert.Throws<InvalidOperationException>(() =>
      new VerificationWorkItemResult(new ForeignWork(), result).ReportDiagnostics(options, "scope", adapted));
    Assert.Equal(outcome == SolverOutcome.Valid, result.IsVerified);
  }

  private class ForeignWork : IVerificationWork {
    public VerificationSource Source => new(new("foreign", 0), Microsoft.Dafny.Token.NoToken, Microsoft.Dafny.Token.NoToken);
    public VerificationWorkStatus CacheStatus => new VerificationWorkStale();
    public bool IsIdle => true;
    public IVerificationWork FromSeed(int seed) => this;
    public IObservable<VerificationWorkStatus>? TryRun() => null;
    public void Cancel() { }
  }

  private class NativeTask : IVerificationTask {
    public readonly Subject<IVerificationStatus> Updates = new();
    public NativeTask? SeededTask;
    public int Seed;
    public int Runs;
    public int Cancellations;
    public bool NoRun;
    public IVerificationStatus Status = new Stale();
    public IVerificationTask FromSeed(int seed) {
      Seed = seed;
      return SeededTask = new NativeTask();
    }
    public IVerificationStatus CacheStatus => Status;
    public ManualSplit Split => throw new NotSupportedException();
    public Bpl.IToken ScopeToken => Bpl.Token.NoToken;
    public string ScopeId => "native";
    public Bpl.IToken Token => Bpl.Token.NoToken;
    public IObservable<IVerificationStatus>? TryRun() {
      Runs++;
      return NoRun ? null : Updates;
    }
    public bool IsIdle => true;
    public void Cancel() => Cancellations++;
  }
}
