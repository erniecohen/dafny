using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using System.Runtime.CompilerServices;
using Xunit;
using Bpl = Microsoft.Boogie;
using VC;

namespace DafnyCore.Test;

public class TaskCacheSessionPolicyTests {
  // Models the prover/transport cycle made by the prover's error subscription.
  private sealed class ProverState(CachedTaskSolver transport) {
    private readonly byte[] setup = new byte[1024 * 1024];
    public void OnError(string message) { GC.KeepAlive(transport); GC.KeepAlive(setup); }
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static (WeakReference Transport, WeakReference Prover) CompletedTransport(TaskCacheSession.Run context) {
    var transport = new CachedTaskSolver(new TaskResultCache(new ContentAddressedCache(Path.GetTempPath())),
      () => null, () => throw new InvalidOperationException("No native solver is needed for the ownership control"));
    var prover = new ProverState(transport);
    transport.ErrorHandler += prover.OnError;
    context.Solvers.Add(transport);
    transport.Close();
    transport.CompleteNormally(CacheTaskOutcome.Unknown, 0);
    return (new WeakReference(transport), new WeakReference(prover));
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public void CapturedContextCanForgetCompletedProverGraphs(bool release, bool disabled) {
    // Keep the context alive as a completed task's captured execution context can.
    // Its task is irrelevant to this ownership boundary, including on a disabled retry.
    var context = new TaskCacheSession.Run(null!, disabled);
    var references = CompletedTransport(context);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    Assert.True(references.Transport.IsAlive && references.Prover.IsAlive);
    if (release) {
      Assert.Equal((1, 0), context.ReleaseRecordings(false));
      Assert.Equal((0, 0), context.ReleaseRecordings(false));
    }
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    Assert.Equal(!release, references.Transport.IsAlive);
    Assert.Equal(!release, references.Prover.IsAlive);
    GC.KeepAlive(context);
  }

  private sealed class ReporterTask(ManualSplit split) : Bpl.IVerificationTask {
    public ManualSplit Split => split;
    public Bpl.IVerificationStatus CacheStatus => new Bpl.Stale();
    public Bpl.IToken ScopeToken => throw new NotSupportedException();
    public string ScopeId => "ownership control";
    public Bpl.IToken Token => throw new NotSupportedException();
    public bool IsIdle => true;
    public void Cancel() { }
    public IObservable<Bpl.IVerificationStatus>? TryRun() => throw new NotSupportedException();
    public Bpl.IVerificationTask FromSeed(int seed) => throw new NotSupportedException();
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static WeakReference AttachReporter(ManualSplit split, DafnyOptions options) {
    var factory = new Bpl.SMTLib.Factory();
    var proverContext = factory.NewProverContext(factory.BlankProverOptions(options));
    split.reporter = new VerificationConditionGenerator.ErrorReporter(options,
      new Bpl.ControlFlowIdMap<Bpl.Absy>(), new List<Bpl.Block>(), new Dictionary<Bpl.Cmd, List<object>>(),
      new VerificationResultCollector(options), null!, proverContext, new Bpl.Program(), split);
    return new WeakReference(split.reporter);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void OnlyACompletedAttemptForgetsItsSplitReporter(bool completed) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    // Explicit seed avoids querying an implementation: this control exercises
    // ownership only, not Boogie preparation or solving.
    var split = new ManualSplit(options, () => new List<Bpl.Block>(), null!, null!, null!, 0);
    var reporter = AttachReporter(split, options);
    var context = new TaskCacheSession.Run(new ReporterTask(split), false);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    Assert.True(reporter.IsAlive);
    Assert.Equal((0, completed ? 1 : 0), context.ReleaseRecordings(completed));
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    Assert.Equal(!completed, reporter.IsAlive);
    GC.KeepAlive(context);
    GC.KeepAlive(split);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void DisabledCachePreservesNumericOrderingPolicy(bool deterministic) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null) {
      DeterministicLiteralHashes = deterministic
    };
    Assert.Null(TaskCacheSession.For(options));
    Assert.Equal(deterministic, options.DeterministicLiteralHashes);
  }

  [Fact]
  public void EnabledCacheStabilizesNumericInputsBeforePreparingTasks() {
    var directory = Path.Combine(Path.GetTempPath(), "task-cache-numeric-policy-" + Guid.NewGuid());
    try {
      var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null) {
        DeterministicLiteralHashes = false
      };
      options.Set(CommonOptionBag.TypeSystemRefresh, true);
      options.Set(CommonOptionBag.VerificationCache, directory);
      Assert.NotNull(TaskCacheSession.For(options));
      Assert.True(options.DeterministicLiteralHashes);
      Assert.True(options.RestartProverPerVC);
    } finally {
      if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
    }
  }
}
