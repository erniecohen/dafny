using DafnyCore.Test.Resolver;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;

namespace DafnyCore.Test.Verification;

[Collection("Cardinality resolution")]
public class VerificationPreparationPipelineTests {
  [Theory]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task ReadyPreparationPassesAnUnfinishedEarlierTranslationOnlyWithFreshSolvers(bool ready, bool fresh) {
    var program = await ModuleTranslationCacheTests.Resolve(Source, cached: false, refresh: true);
    program.Options.Set(CommonOptionBag.TranslationWorkers, 2);
    program.Options.Set(CommonOptionBag.ReadyModuleVerification, ready);
    program.Options.RestartProverPerVC = fresh;
    var module = program.RawModules().Single(m => m.Name == "Boundary");
    using var pipeline = new VerificationPreparationPipeline();
    // Hold the cursor of an earlier translation without relying on a timing delay in that translation.
    var earlier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    typeof(VerificationPreparationPipeline).GetField("lastCommit",
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(pipeline, earlier.Task);
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var captures = 0;
    var calls = 0;
    var backend = new Backend((graph, _) => {
      Assert.Equal(1, captures);
      Assert.NotEmpty(graph.Implementations);
      Interlocked.Increment(ref calls);
      entered.TrySetResult();
      return Task.FromResult(VerificationPreparation.Ready([]));
    });
    var preparation = pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, default,
      _ => Interlocked.Increment(ref captures));
    try {
      if (ready && fresh) {
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(earlier.Task.IsCompleted);
      } else {
        Assert.NotSame(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(100)));
      }
    } finally {
      earlier.TrySetResult();
      await preparation.WaitAsync(TimeSpan.FromSeconds(30));
    }
    Assert.Equal(1, captures);
    Assert.Equal(1, calls);
  }

  private const string Source = """
    module Boundary {
      lemma Positive(x: int) ensures x + 1 > x { }
      lemma Negative() { assert false; }
    }
    """;

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task CapturePrecedesBackendMutationWithoutAnEngine(bool cache, bool checksums) {
    ModuleTranslationCache.ProcessCache.Clear();
    var previousHits = ModuleTranslationCache.ProcessCache.Hits;
    var program = await ModuleTranslationCacheTests.Resolve(Source, cached: cache);
    var module = program.RawModules().Single(m => m.Name == "Boundary");
    using var pipeline = new VerificationPreparationPipeline();
    string? captured = null;
    Bpl.Program? observed = null;
    var factoryCalls = 0;
    var backend = new Backend((graph, _) => {
      Assert.Same(observed, graph);
      Assert.Equal(captured, ModuleTranslationCache.Emit(graph, program.Options));
      Assert.NotEmpty(graph.Implementations);
      Assert.Contains(graph.Implementations, i => i.StructuredStmts != null);
      Assert.Equal(checksums, graph.Implementations.Any(i =>
        Bpl.QKeyValue.FindStringAttribute(i.Attributes, "checksum") != null));
      // A mutating backend cannot change the earlier capture or the next cache consumer.
      graph.AddTopLevelDeclaration(new Bpl.Constant(Bpl.Token.NoToken,
        new Bpl.TypedIdent(Bpl.Token.NoToken, "BackendMutation", Bpl.Type.Int), false));
      return Task.FromResult(VerificationPreparation.Ready([]));
    });
    ModuleVerificationPreparation? first = null;
    for (var iteration = 0; iteration < 2; iteration++) {
      var prepared = await pipeline.PrepareAsync(new(false, program, null), module, checksums,
        () => { factoryCalls++; return backend; }, default, graph => {
          Assert.Equal(iteration, factoryCalls);
          observed = graph;
          var text = ModuleTranslationCache.Emit(graph, program.Options);
          Assert.DoesNotContain("BackendMutation", text);
          if (captured != null) { Assert.Equal(captured, text); }
          captured = text;
        });
      Assert.True(prepared.Preparation.IsReady);
      Assert.NotEmpty(prepared.ProofDependencies.ProofDependenciesById);
      if (first != null) {
        Assert.NotSame(first.ProofDependencies, prepared.ProofDependencies);
        Assert.Equal(first.ProofDependencies.ProofDependenciesById.Keys.Order(),
          prepared.ProofDependencies.ProofDependenciesById.Keys.Order());
      }
      first = prepared;
    }
    Assert.Equal(2, factoryCalls);
    Assert.Equal(previousHits + (cache ? 1 : 0), ModuleTranslationCache.ProcessCache.Hits);
    Assert.Empty(program.ProofDependencyManager.ProofDependenciesById);
  }

  [Theory]
  [InlineData(VerificationOutcome.Unsupported)]
  [InlineData(VerificationOutcome.ToolError)]
  [InlineData(VerificationOutcome.Cancelled)]
  public async Task FailedPreparationRetainsItsOutcomeAndOriginalError(VerificationOutcome outcome) {
    var program = await ModuleTranslationCacheTests.Resolve(Source, cached: false);
    var module = program.RawModules().Single(m => m.Name == "Boundary");
    var error = new InvalidOperationException("backend rejected the input");
    var failure = VerificationPreparation.Failed(outcome, error);
    using var pipeline = new VerificationPreparationPipeline();
    var backend = new Backend((_, _) => Task.FromResult(failure));
    var result = await pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, default);
    Assert.Same(failure, result.Preparation);
    Assert.Equal(outcome, result.Preparation.FailureOutcome);
    Assert.Same(error, Assert.Throws<InvalidOperationException>(() => result.Preparation.GetWorkOrThrow()));
  }

  [Fact]
  public async Task ProfilingAFailedPreparationDoesNotEnumerateFailedWork() {
    var program = await ModuleTranslationCacheTests.Resolve(Source, cached: false);
    var module = program.RawModules().Single(m => m.Name == "Boundary");
    var directory = Path.Combine(Path.GetTempPath(), "preparation-profile-" + Guid.NewGuid());
    program.Options.Set(CommonOptionBag.PipelineProfile, Path.Combine(directory, "profile.jsonl"));
    try {
      using var pipeline = new VerificationPreparationPipeline();
      var failure = VerificationPreparation.Failed(VerificationOutcome.Unsupported,
        new InvalidOperationException("unsupported input"));
      var backend = new Backend((_, _) => Task.FromResult(failure));
      var prepared = await pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, default);
      Assert.Same(failure, prepared.Preparation);
    } finally {
      PipelineProfile.For(program.Options)?.Finish("test complete");
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public async Task CaptureFailureAndCancellationCannotStartPreparationAndReleaseTheLock() {
    var program = await ModuleTranslationCacheTests.Resolve(Source, cached: false);
    var module = program.RawModules().Single(m => m.Name == "Boundary");
    var calls = 0;
    var backend = new Backend((_, _) => {
      calls++;
      return Task.FromResult(VerificationPreparation.Ready([]));
    });
    using var pipeline = new VerificationPreparationPipeline();
    var error = new InvalidOperationException("capture failed");
    Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() =>
      pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, default, _ => throw error)));
    using var cancelled = new CancellationTokenSource();
    await cancelled.CancelAsync();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, cancelled.Token));
    Assert.Equal(0, calls);
    await pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, default);
    Assert.Equal(1, calls);
  }

  [Fact]
  public async Task BackendExceptionsRemainExceptionalAndReleaseTheLock() {
    var program = await ModuleTranslationCacheTests.Resolve(Source, cached: false);
    var module = program.RawModules().Single(m => m.Name == "Boundary");
    var error = new InvalidOperationException("backend failed");
    using var pipeline = new VerificationPreparationPipeline();
    var backend = new Backend((_, _) => throw error);
    Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() =>
      pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, default)));
    var valid = new Backend((_, _) => Task.FromResult(VerificationPreparation.Ready([])));
    await pipeline.PrepareAsync(new(false, program, null), module, false, () => valid, default);
  }

  [Fact]
  public async Task PreparationIsSerializedAndWaitingCancellationDoesNotEnterTheBackend() {
    var program = await ModuleTranslationCacheTests.Resolve(Source, cached: false);
    var module = program.RawModules().Single(m => m.Name == "Boundary");
    using var pipeline = new VerificationPreparationPipeline();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var calls = 0;
    var backend = new Backend(async (_, token) => {
      Assert.False(token.IsCancellationRequested);
      calls++;
      entered.TrySetResult();
      await release.Task;
      return VerificationPreparation.Ready([]);
    });
    var first = pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, default);
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
    Task<ModuleVerificationPreparation>? waiting = null;
    try {
      using var cancellation = new CancellationTokenSource();
      var second = pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, cancellation.Token);
      await cancellation.CancelAsync();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
      Assert.Equal(1, calls);
      var secondCapture = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      waiting = pipeline.PrepareAsync(new(false, program, null), module, false, () => backend, default,
        _ => secondCapture.SetResult());
      Assert.NotSame(secondCapture.Task,
        await Task.WhenAny(secondCapture.Task, Task.Delay(TimeSpan.FromSeconds(2))));
      Assert.Equal(1, calls);
    } finally {
      release.SetResult();
      await first;
      if (waiting != null) { await waiting; }
    }
  }

  private sealed class Backend(Func<Bpl.Program, CancellationToken, Task<VerificationPreparation>> prepare)
    : IVerificationBackend<Bpl.Program> {
    public Task<VerificationPreparation> PrepareAsync(Bpl.Program input, CancellationToken cancellationToken) =>
      prepare(input, cancellationToken);
  }
}
