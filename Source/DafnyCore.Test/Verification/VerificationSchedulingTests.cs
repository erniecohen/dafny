using System.Collections.Concurrent;
using DafnyCore.Test.Resolver;
using Microsoft.Dafny;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DafnyCore.Test.Verification;

[Collection("Cardinality resolution")]
public class VerificationSchedulingTests {
  [Theory]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task ReadyTasksPassAnUnpreparedModuleOnlyWithFreshSolvers(bool ready, bool fresh) {
    var program = await ModuleTranslationCacheTests.Resolve("""
      module A { lemma First() {} lemma Second() {} }
      module B { lemma Later() {} }
      """, cached: false);
    program.Options.Set(CommonOptionBag.ReadyModuleVerification, ready);
    program.Options.RestartProverPerVC = fresh;
    var owners = program.Modules().SelectMany(m => m.TopLevelDecls).OfType<TopLevelDeclWithMembers>()
      .SelectMany(d => d.Members).OfType<Method>().ToDictionary(m => m.Name);
    var works = owners.ToDictionary(p => p.Key,
      p => new Work(new(new(p.Key, 0), p.Value.Origin, p.Value.Origin) { CanVerify = p.Value }, false));
    var earlier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var preparedLater = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var verifier = new ModuleVerifier(async module => {
      if (module.Name == "A") { await earlier.Task; }
      else { preparedLater.TrySetResult(); }
      return new(VerificationPreparation.Ready(works.Values.Where(w =>
        ReferenceEquals(w.Source.CanVerify!.ContainingModule, module)).ToArray()), new());
    });
    using var compilation = Create(program, verifier, new(false, () => throw new Exception()));
    var finishedLater = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var finishedAll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var order = new ConcurrentQueue<string>();
    using var subscription = compilation.Updates.Subscribe(update => {
      if (update is VerificationUpdate { Status: VerificationWorkCompleted } result) {
        order.Enqueue(((Method)result.CanVerify).Name);
        if (((Method)result.CanVerify).Name == "Later") { finishedLater.TrySetResult(); }
        if (order.Count == 3) { finishedAll.TrySetResult(); }
      }
    });
    compilation.Start();
    foreach (var name in new[] { "First", "Second", "Later" }) {
      Assert.True(await compilation.VerifyCanVerify(owners[name], _ => true, null));
    }
    await preparedLater.Task.WaitAsync(TimeSpan.FromSeconds(30));
    try {
      if (ready && fresh) {
        await finishedLater.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, works["First"].Runs);
      } else {
        Assert.NotSame(finishedLater.Task, await Task.WhenAny(finishedLater.Task, Task.Delay(100)));
        Assert.Equal(0, works["Later"].Runs);
      }
    } finally {
      earlier.TrySetResult();
      await finishedLater.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    await finishedAll.Task.WaitAsync(TimeSpan.FromSeconds(30));
    Assert.All(works.Values, work => Assert.Equal(1, work.Runs));
    Assert.True(Array.IndexOf(order.ToArray(), "First") < Array.IndexOf(order.ToArray(), "Second"));
    Assert.Equal(2, verifier.Calls);
    var queues = (System.Collections.IDictionary)typeof(Compilation).GetField("moduleVerificationTickets",
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(compilation)!;
    Assert.Equal(ready && fresh ? 2 : 0, queues.Count);
    foreach (var module in owners.Values.Select(o => o.ContainingModule).Distinct()) {
      compilation.ClearModuleCache(module);
    }
    Assert.Empty(queues);
    foreach (var owner in owners.Values) {
      compilation.ClearCanVerifyCache(owner, allowRepeat: true);
    }
    order.Clear();
    finishedAll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    foreach (var name in new[] { "First", "Second", "Later" }) {
      Assert.True(await compilation.VerifyCanVerify(owners[name], _ => true, null));
    }
    await finishedAll.Task.WaitAsync(TimeSpan.FromSeconds(30));
    Assert.All(works.Values, work => Assert.Equal(2, work.Runs));
    Assert.Equal(4, verifier.Calls);
    foreach (var module in owners.Values.Select(o => o.ContainingModule).Distinct()) {
      compilation.ClearModuleCache(module);
    }
    Assert.Empty(queues);
  }

  private sealed class ModuleVerifier(Func<ModuleDefinition, Task<ModuleVerificationPreparation>> prepare) : IProgramVerifier {
    public int Calls;
    public Task<ModuleVerificationPreparation> GetVerificationTasksAsync(VerificationPreparationRequest request,
      ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
      Interlocked.Increment(ref Calls);
      return prepare(moduleDefinition);
    }
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(true, false)]
  [InlineData(false, true)]
  [InlineData(true, true)]
  public async Task EngineFreeSchedulingPreservesFiltersSeedsCacheAndCancellation(bool cached, bool ready) {
    var program = await ModuleTranslationCacheTests.Resolve("lemma Good() { assert true; }", cached: false);
    program.Options.Set(CommonOptionBag.ReadyModuleVerification, ready);
    program.Options.RestartProverPerVC = true;
    var owner = Owner(program);
    var kept = new Work(new(new("Good", 1), owner.Origin, owner.Origin) { CanVerify = owner }, cached);
    var excluded = new Work(kept.Source with { Id = new("Good", 2) }, cached);
    var dependencies = new ProofDependencyManager();
    var verifier = new Verifier(new(VerificationPreparation.Ready([kept, excluded]), dependencies));
    var request = new VerificationPreparationRequest(false, () => throw new Exception("not a native backend"));
    using var compilation = Create(program, verifier, request);
    var events = new ConcurrentQueue<ICompilationEvent>();
    var identified = new TaskCompletionSource<CanVerifyPartsIdentified>(TaskCreationOptions.RunContinuationsAsynchronously);
    var completed = new TaskCompletionSource<VerificationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var subscription = compilation.Updates.Subscribe(update => {
      events.Enqueue(update);
      if (update is CanVerifyPartsIdentified parts) { identified.TrySetResult(parts); }
      if (update is VerificationUpdate { Status: VerificationWorkCompleted } result) { completed.TrySetResult(result); }
    });
    compilation.Start();
    Assert.False((await compilation.Resolution)!.HasErrors);
    Assert.True(await compilation.VerifyCanVerify(owner, w => w.Source.Id.Part == 1, 73));
    var parts = await identified.Task.WaitAsync(TimeSpan.FromSeconds(30));
    var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
    Assert.Equal(new IVerificationWork[] { kept, excluded }, parts.Parts);
    Assert.Same(request, verifier.Request);
    Assert.Same(dependencies, result.ProofDependencyManager);
    Assert.Same(kept, result.VerificationTask);
    Assert.Equal(73, kept.Seed);
    Assert.Equal(1, kept.Runs);
    Assert.Equal(0, excluded.Runs);
    Assert.Null(result.VerificationTask.Payload);
    Assert.Equal(VerificationOutcome.Unsupported, Assert.IsType<VerificationWorkCompleted>(result.Status).Result.Outcome);
    Assert.False(Assert.IsType<VerificationWorkCompleted>(result.Status).Result.IsVerified);
    if (!cached) {
      Assert.Collection(events.OfType<VerificationUpdate>(),
        e => Assert.IsType<VerificationWorkQueued>(e.Status),
        e => Assert.IsType<VerificationWorkRunning>(e.Status),
        e => Assert.IsType<VerificationWorkCompleted>(e.Status));
    }
    compilation.CancelPendingUpdates();
    Assert.Equal(1, kept.Cancellations);
    Assert.Equal(1, excluded.Cancellations);
  }

  [Fact]
  public async Task FailedPreparationEmitsAnErrorWithoutIdentifyingEmptyWork() {
    var program = await ModuleTranslationCacheTests.Resolve("lemma Good() { assert true; }", cached: false);
    var error = new InvalidOperationException("unsupported backend input");
    var verifier = new Verifier(new(VerificationPreparation.Failed(VerificationOutcome.Unsupported, error), new()));
    using var compilation = Create(program, verifier, new(false, () => throw new Exception()));
    var errors = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
    var parts = new List<CanVerifyPartsIdentified>();
    using var subscription = compilation.Updates.Subscribe(update => {
      if (update is InternalCompilationException failure) { errors.TrySetResult(failure.Exception); }
      if (update is CanVerifyPartsIdentified identified) { parts.Add(identified); }
    });
    compilation.Start();
    Assert.True(await compilation.VerifyCanVerify(Owner(program), _ => true));
    Assert.Same(error, await errors.Task.WaitAsync(TimeSpan.FromSeconds(30)));
    Assert.Empty(parts);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SchedulingErrorsRetainOriginalExceptions(bool cancelled) {
    var program = await ModuleTranslationCacheTests.Resolve("lemma Good() { assert true; }", cached: false);
    var owner = Owner(program);
    var error = cancelled ? (Exception)new TaskCanceledException("cancelled work") : new InvalidOperationException("worker error");
    var work = new Work(new(new("Good", 0), owner.Origin, owner.Origin) { CanVerify = owner }, false, error);
    var verifier = new Verifier(new(VerificationPreparation.Ready([work]), new()));
    using var compilation = Create(program, verifier, new(false, () => throw new Exception()));
    var failure = new TaskCompletionSource<VerificationException>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var subscription = compilation.Updates.Subscribe(update => {
      if (update is VerificationException result) { failure.TrySetResult(result); }
    });
    compilation.Start();
    Assert.True(await compilation.VerifyCanVerify(owner, _ => true, null));
    var result = await failure.Task.WaitAsync(TimeSpan.FromSeconds(30));
    Assert.Same(owner, result.CanVerify);
    Assert.Same(work, result.Task);
    Assert.Same(error, result.Exception);
  }

  [Fact]
  public void NamesRangesAndMissingOwnershipUseNeutralMetadata() {
    var uri = new Uri("file:///source.dfy");
    IOrigin Origin(int start, int end) => new SourceOrigin(
      new Token(start, 1) { Uri = uri, pos = start }, new Token(end, 1) { Uri = uri, pos = end });
    var a = new Work(new(new("Scope", 1), Origin(1, 4), Origin(1, 1)), false);
    var b = new Work(a.Source with { Id = new("Scope", 2), Origin = Origin(3, 6) }, false);
    var c = new Work(a.Source with { Id = new("Scope", 3), Origin = Origin(9, 10) }, false);
    Assert.Equal("Scope1", Compilation.GetTaskName(a));
    var groups = Compilation.GroupOverlappingRanges([c, b, a]).ToList();
    Assert.Equal(2, groups.Count);
    Assert.Equal(new IVerificationWork[] { a, b }, groups[0].Tasks);
    Assert.Equal(6, groups[0].Group.EndToken!.line);
    Assert.Same(c, Assert.Single(groups[1].Tasks));
    Assert.Throws<InvalidOperationException>(() => Compilation.GetCanVerify(a));
    Assert.Empty(Compilation.GroupOverlappingRanges([]));
  }

  [Fact]
  public void ProfilingPreservesNativeLabelsAndKeepsAbsentMetricsAbsent() {
    var directory = Path.Combine(Path.GetTempPath(), "scheduling-profile-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "profile.jsonl");
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.PipelineProfile, path);
    var profile = PipelineProfile.For(options)!;
    try {
      var work = new Work(new(new("Scope", 7), Token.NoToken, Token.NoToken), false);
      profile.TaskSubmitted(work);
      profile.TaskStatus("Module", "Symbol", work, new VerificationWorkQueued(), false);
      profile.TaskStatus("Module", "Symbol", work, new VerificationWorkRunning(), false);
      profile.TaskStatus("Module", "Symbol", work, new VerificationWorkCompleted(new(VerificationOutcome.Unsupported)), false);
      var result = new VC.VerificationRunResult(0, 0, DateTime.UnixEpoch, Microsoft.Boogie.SolverOutcome.Valid,
        TimeSpan.FromSeconds(3), 5, [], [], [], 123, null, []);
      profile.TaskSubmitted(work);
      profile.TaskStatus("Module", "Symbol", work, new VerificationWorkCompleted(new(VerificationOutcome.Verified,
        Metrics: new(123, result.RunTime), Payload: new BoogieVerificationResult(result))), true);
      profile.Finish("test complete");
      var events = File.ReadLines(path).Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement)
        .Where(e => e.GetProperty("ev").GetString() == "task").ToList();
      Assert.Equal(2, events.Count);
      Assert.Equal("Scope7", events[0].GetProperty("task").GetString());
      Assert.Equal(7, events[0].GetProperty("split").GetInt32());
      Assert.Equal("Unsupported", events[0].GetProperty("outcome").GetString());
      Assert.Equal(System.Text.Json.JsonValueKind.Null, events[0].GetProperty("resourceCount").ValueKind);
      Assert.Equal(System.Text.Json.JsonValueKind.Null, events[0].GetProperty("solverRunMs").ValueKind);
      Assert.Equal("Valid", events[1].GetProperty("outcome").GetString());
      Assert.Equal(123, events[1].GetProperty("resourceCount").GetInt64());
      Assert.Equal(3000, events[1].GetProperty("solverRunMs").GetDouble());
      Assert.True(events[1].GetProperty("cached").GetBoolean());
    } finally {
      profile.Finish("test cleanup");
      Directory.Delete(directory, true);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task NativeRequestPreservesPrintingChecksumsCacheAndSourceOwnership(bool snapshots) {
    var program = await ModuleTranslationCacheTests.Resolve(
      "function F(): int { 1 } lemma Good() { hide F; assert F() == 1; }", cached: false);
    var owner = Owner(program);
    var directory = Path.Combine(Path.GetTempPath(), "native-request-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var options = program.Options;
    options.PrintFile = Path.Combine(directory, "translated.bpl");
    options.VerifySnapshots = snapshots ? 1 : 0;
    options.Prune = true;
    options.Set(CommonOptionBag.VerificationCache, Path.Combine(directory, "cache"));
    try {
      using var engine = new Microsoft.Boogie.ExecutionEngine(options,
        new Microsoft.Boogie.EmptyVerificationResultCache(), TaskScheduler.Default);
      var resolution = new ResolutionResult(false, program, null);
      var request = BoogieVerificationBackend.CreateRequest(engine, resolution, owner.ContainingModule);
      Assert.Equal(snapshots, request.InsertChecksums);
      using var pipeline = new VerificationPreparationPipeline();
      var prepared = await pipeline.PrepareAsync(resolution, owner.ContainingModule, request.InsertChecksums,
        request.CreateBackend, default, request.Capture);
      var work = prepared.Preparation.GetWorkOrThrow();
      Assert.NotEmpty(work);
      Assert.Contains(work, w => ReferenceEquals(owner, Compilation.GetCanVerify(w)));
      Assert.Contains(work, w => w.HiddenFunctions.Any(f => f.Name == "F"));
      Assert.All(work, w => {
        var native = BoogieVerificationReporting.GetTask(w);
        Assert.IsAssignableFrom<Microsoft.Dafny.Caching.IReplayedVerificationTask>(native);
        Assert.Same(((CanVerifyOrigin)native.ScopeToken).CanVerify, w.Source.CanVerify);
        Assert.Equal(((CanVerifyOrigin)native.ScopeToken).IsReverifiedInheritedCheck, w.Source.IsReverifiedInheritedCheck);
        Assert.Equal(native.ScopeId, w.Source.Id.Scope);
        Assert.Equal(native.Split.SplitIndex, w.Source.Id.Part);
        Assert.Same(BoogieGenerator.ToDafnyToken(native.Token), w.Source.Origin);
        Assert.Same(native.ScopeToken, w.Source.ScopeOrigin);
      });
      var printed = File.ReadAllText(options.PrintFile);
      Assert.Contains("implementation", printed);
      Assert.Equal(snapshots, printed.Contains("{:checksum"));
    } finally {
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public async Task NativeRefinementMetadataRetainsChecksForCopies() {
    var program = await ModuleTranslationCacheTests.Resolve("""
      module Base {
        trait S { lemma L() ensures true }
        class C extends S { constructor () {} lemma L() ensures true {} }
      }
      module R refines Base {
        trait S ... { lemma L... ensures false }
      }
      """, cached: false);
    var module = program.RawModules().Single(m => m.Name == "R");
    using var engine = new Microsoft.Boogie.ExecutionEngine(program.Options,
      new Microsoft.Boogie.EmptyVerificationResultCache(), TaskScheduler.Default);
    using var pipeline = new VerificationPreparationPipeline();
    var prepared = await pipeline.PrepareAsync(new(false, program, null), module, false,
      () => new BoogieVerificationBackend(engine), default);
    Assert.Contains(prepared.Preparation.GetWorkOrThrow(), work =>
      work.Source.CanVerify!.Origin.IsCopy && work.Source.IsReverifiedInheritedCheck);
  }

  private static Method Owner(Microsoft.Dafny.Program program) =>
    program.Modules().SelectMany(m => m.TopLevelDecls).OfType<TopLevelDeclWithMembers>()
      .SelectMany(d => d.Members).OfType<Method>().Single(m => m.Name == "Good");

  private static Compilation Create(Microsoft.Dafny.Program program, IProgramVerifier verifier,
    VerificationPreparationRequest request) {
    var input = new CompilationInput(program.Options, 0,
      new DafnyProject(null, new Uri("file:///backend-test/dfyconfig.toml"), null, new HashSet<string>()));
    program.Options.UseStdin = true;
    return new Compilation(NullLogger<Compilation>.Instance, OnDiskFileSystem.Instance,
      new Loader(program), verifier, input, (_, _) => request);
  }

  private sealed class Verifier(ModuleVerificationPreparation result) : IProgramVerifier {
    public VerificationPreparationRequest? Request;
    public Task<ModuleVerificationPreparation> GetVerificationTasksAsync(VerificationPreparationRequest request,
      ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
      Request = request;
      return Task.FromResult(result);
    }
  }

  private sealed class Loader(Microsoft.Dafny.Program program) : ITextDocumentLoader {
    public Task<ProgramParseResult> ParseAsync(Compilation compilation, CancellationToken cancellationToken) =>
      Task.FromResult(new ProgramParseResult(program, new()));
    public Task<ResolutionResult?> ResolveAsync(Compilation compilation, Microsoft.Dafny.Program parsed,
      CancellationToken cancellationToken) => Task.FromResult<ResolutionResult?>(new(false, program, null));
  }

  private sealed class Work(VerificationSource source, bool cached, Exception? error = null) : IVerificationWork {
    public VerificationSource Source => source;
    public VerificationWorkStatus CacheStatus => new VerificationWorkCompleted(new(VerificationOutcome.Unsupported));
    public bool IsIdle => true;
    public int Seed, Runs, Cancellations;
    public IVerificationWork FromSeed(int seed) { Seed = seed; return this; }
    public IObservable<VerificationWorkStatus>? TryRun() {
      Runs++;
      if (cached) { return null; }
      return System.Reactive.Linq.Observable.Create<VerificationWorkStatus>(observer => {
        observer.OnNext(new VerificationWorkQueued());
        observer.OnNext(new VerificationWorkRunning());
        if (error != null) { observer.OnError(error); }
        else {
          observer.OnNext(CacheStatus);
          observer.OnCompleted();
        }
        return System.Reactive.Disposables.Disposable.Empty;
      });
    }
    public void Cancel() { Cancellations++; }
  }
}
