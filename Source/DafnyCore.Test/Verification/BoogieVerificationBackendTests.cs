using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Dafny;
using VC;
using Xunit;
using Bpl = Microsoft.Boogie;

namespace DafnyCore.Test.Verification;

public class BoogieVerificationBackendTests {
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
    public IVerificationTask FromSeed(int seed) {
      Seed = seed;
      return SeededTask = new NativeTask();
    }
    public IVerificationStatus CacheStatus => new Stale();
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
