using System.Collections.Concurrent;
using System.Reactive.Linq;
using DafnyCore.Test.Resolver;
using Microsoft.Dafny;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DafnyCore.Test.Verification;

[Collection("Cardinality resolution")]
public class VerificationSchedulingTests {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task EngineFreeSchedulingPreservesFiltersSeedsCacheAndCancellation(bool cached) {
    var program = await ModuleTranslationCacheTests.Resolve("lemma Good() { assert true; }", cached: false);
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
    var run = Assert.Single(events.OfType<VerificationRunStarted>()).Run;
    Assert.Same(run, result.Run);
    Assert.Single(run.Work);
    Assert.Equal(2, run.Manifest!.Expected.Count);
    Assert.Same(run.Manifest, Assert.Single(events.OfType<ModuleVerificationWorkIdentified>()).Manifest);
    await run.AuditCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.True(run.IsComplete);
    Assert.True(run.IsClosed);
    Assert.Equal(cached ? VerificationCompletionKind.CachedResult : VerificationCompletionKind.StreamClosed,
      Assert.Single(run.Closures).Value);
    Assert.False(run.IsVerified);
    Assert.Equal(VerificationOutcome.Unsupported, Assert.Single(run.Results).Value.Outcome);
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
    Assert.Same(error, Assert.Single(result.Run!.Failures).Value.Exception);
    Assert.True(result.Run.IsComplete);
    Assert.False(result.Run.IsVerified);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  public async Task ModuleIdentityChecksPrecedeRequestFiltering(bool foreignOwner, bool duplicateOwner) {
    var program = await ModuleTranslationCacheTests.Resolve("""
      lemma Good() { assert true; }
      lemma Sibling() { assert true; }
      module Other { lemma Foreign() { assert true; } }
      """, cached: false);
    var owner = Owner(program);
    var other = program.Modules().SelectMany(m => m.TopLevelDecls).OfType<TopLevelDeclWithMembers>()
      .SelectMany(d => d.Members).OfType<Method>().Single(m => m.Name == (foreignOwner ? "Foreign" : "Sibling"));
    var source = new VerificationSource(new("Good", 1), owner.Origin, owner.Origin) { CanVerify = owner };
    var first = new Work(source, false);
    var invalid = new Work(foreignOwner ? source with { Id = new("Other", 2), CanVerify = other } :
      source with { CanVerify = duplicateOwner ? other : owner }, false);
    var verifier = new Verifier(new(VerificationPreparation.Ready([first, invalid]), new()));
    using var compilation = Create(program, verifier, new(false, () => throw new Exception()));
    var failure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
    var parts = new ConcurrentQueue<CanVerifyPartsIdentified>();
    using var subscription = compilation.Updates.Subscribe(update => {
      if (update is InternalCompilationException error) { failure.TrySetResult(error.Exception); }
      if (update is CanVerifyPartsIdentified identified) { parts.Enqueue(identified); }
    });
    compilation.Start();
    Assert.True(await compilation.VerifyCanVerify(owner, _ => false, null));
    Assert.IsType<InvalidOperationException>(await failure.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    Assert.Empty(parts);
    Assert.Equal(0, first.Runs);
    Assert.Equal(0, invalid.Runs);
  }

  [Fact]
  public async Task SeedingCannotSubstituteWorkAndSynchronousStartErrorsAreAccountedFor() {
    var program = await ModuleTranslationCacheTests.Resolve("lemma Good() { assert true; }", cached: false);
    var owner = Owner(program);
    var source = new VerificationSource(new("Good", 1), owner.Origin, owner.Origin) { CanVerify = owner };
    var error = new InvalidOperationException("failed before returning a stream");
    var work = new StartFailureWork(source, error);
    var excluded = new Work(source with { Id = new("Other", 0) }, false);
    var verifier = new Verifier(new(VerificationPreparation.Ready([work, excluded]), new()));
    using var compilation = Create(program, verifier, new(false, () => throw new Exception()));
    var rejected = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
    var failure = new TaskCompletionSource<VerificationException>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var subscription = compilation.Updates.Subscribe(update => {
      if (update is InternalCompilationException exception) { rejected.TrySetResult(exception.Exception); }
      if (update is VerificationException workFailure) { failure.TrySetResult(workFailure); }
    });
    compilation.Start();
    Assert.True(await compilation.VerifyCanVerify(owner, w => w.Source.Id.Scope == "Good", 73));
    Assert.IsType<InvalidOperationException>(await rejected.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    Assert.True(await compilation.VerifyCanVerify(owner, w => w.Source.Id.Scope == "Good", 74));
    var failed = await failure.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Same(error, failed.Exception);
    Assert.Same(error, Assert.Single(failed.Run!.Failures).Value.Exception);
  }

  [Theory]
  [InlineData("empty")]
  [InlineData("progress-only")]
  [InlineData("duplicate")]
  [InlineData("after-terminal")]
  [InlineData("unknown-status")]
  [InlineData("unknown-outcome")]
  [InlineData("null-result")]
  public async Task MalformedWorkStreamsFailWithoutPublishingAProofResult(string script) {
    var program = await ModuleTranslationCacheTests.Resolve("lemma Good() { assert true; }", cached: false);
    var owner = Owner(program);
    var terminal = new VerificationWorkCompleted(new(VerificationOutcome.Verified));
    VerificationWorkStatus[] statuses = script switch {
      "empty" => [],
      "progress-only" => [new VerificationWorkRunning()],
      "duplicate" => [terminal, terminal],
      "after-terminal" => [terminal, new VerificationWorkRunning()],
      "unknown-status" => [null!],
      "unknown-outcome" => [new VerificationWorkCompleted(new((VerificationOutcome)12345))],
      "null-result" => [new VerificationWorkCompleted(null!)],
      _ => throw new ArgumentOutOfRangeException(nameof(script))
    };
    var work = new ScriptedWork(new(new("Good", 0), owner.Origin, owner.Origin) { CanVerify = owner },
      statuses.ToObservable());
    var verifier = new Verifier(new(VerificationPreparation.Ready([work]), new()));
    using var compilation = Create(program, verifier, new(false, () => throw new Exception()));
    var failure = new TaskCompletionSource<VerificationException>(TaskCreationOptions.RunContinuationsAsynchronously);
    var results = new ConcurrentQueue<VerificationWorkResult>();
    using var subscription = compilation.Updates.Subscribe(update => {
      if (update is VerificationException error) { failure.TrySetResult(error); }
      if (update is VerificationUpdate { Status: VerificationWorkCompleted completed }) { results.Enqueue(completed.Result); }
    });
    compilation.Start();
    Assert.True(await compilation.VerifyCanVerify(owner, _ => true, null));
    var rejected = await failure.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Same(work, rejected.Task);
    Assert.Same(owner, rejected.CanVerify);
    Assert.IsType<InvalidOperationException>(rejected.Exception);
    Assert.Empty(results);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ClosureAuditSeparatesPublicationFromCleanCompletion(bool hasResult) {
    var program = await ModuleTranslationCacheTests.Resolve("lemma Good() { assert true; }", cached: false);
    var owner = Owner(program);
    using var source = new System.Reactive.Subjects.Subject<VerificationWorkStatus>();
    var work = new ScriptedWork(new(new("Good", 0), owner.Origin, owner.Origin) { CanVerify = owner }, source, raw: true);
    using var compilation = Create(program, new Verifier(new(VerificationPreparation.Ready([work]), new())),
      new(false, () => throw new Exception()));
    var started = new TaskCompletionSource<VerificationRun>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var events = compilation.Updates.Subscribe(update => {
      if (update is VerificationRunStarted run) { started.TrySetResult(run.Run); }
    });
    compilation.Start();
    await compilation.VerifyCanVerify(owner, _ => true);
    var run = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    // Simulate an adapter retaining native publication timing, including a missing terminal result.
    if (hasResult) { source.OnNext(new VerificationWorkCompleted(new(VerificationOutcome.Verified))); }
    Assert.False(run.IsVerified);
    Assert.False(run.IsClosed);
    Assert.False(run.AuditCompletion.IsCompleted);
    source.OnCompleted();
    await run.AuditCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(hasResult, run.IsClosed);
    Assert.Equal(hasResult, run.IsVerified);
    Assert.Equal(hasResult ? 0 : 1, run.Failures.Count);
    if (!hasResult) { Assert.IsType<InvalidOperationException>(Assert.Single(run.Failures).Value.Exception); }
  }

  [Fact]
  public async Task AlreadyRunningWorkDoesNotInventAResultOrClosure() {
    var program = await ModuleTranslationCacheTests.Resolve("lemma Good() { assert true; }", cached: false);
    var owner = Owner(program);
    var work = new PendingWork(new(new("Good", 0), owner.Origin, owner.Origin) { CanVerify = owner });
    using var compilation = Create(program, new Verifier(new(VerificationPreparation.Ready([work]), new())),
      new(false, () => throw new Exception()));
    var started = new TaskCompletionSource<VerificationRun>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var events = compilation.Updates.Subscribe(update => {
      if (update is VerificationRunStarted run) { started.TrySetResult(run.Run); }
    });
    compilation.Start();
    await compilation.VerifyCanVerify(owner, _ => true);
    var run = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.False(run.IsComplete);
    Assert.False(run.IsClosed);
    Assert.False(run.IsVerified);
    Assert.False(run.AuditCompletion.IsCompleted);
    Assert.Empty(run.Results);
    Assert.Empty(run.Failures);
    Assert.Empty(run.Closures);
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

  private sealed class ScriptedWork(VerificationSource source, IObservable<VerificationWorkStatus> statuses, bool raw = false) : IVerificationWork {
    public VerificationSource Source => source;
    public VerificationWorkStatus CacheStatus => new VerificationWorkStale();
    public bool IsIdle => true;
    public IVerificationWork FromSeed(int seed) => this;
    public IObservable<VerificationWorkStatus> TryRun() => statuses;
    public IObservable<VerificationWorkStatus> TryRunForScheduling() => raw ? statuses : VerificationWorkStream.Validate(statuses);
    public void Cancel() { }
  }

  private sealed class PendingWork(VerificationSource source) : IVerificationWork {
    public VerificationSource Source => source;
    public VerificationWorkStatus CacheStatus => new VerificationWorkRunning();
    public bool IsIdle => false;
    public IVerificationWork FromSeed(int seed) => this;
    public IObservable<VerificationWorkStatus>? TryRun() => null;
    public void Cancel() { }
  }

  private sealed class StartFailureWork(VerificationSource source, Exception error) : IVerificationWork {
    public VerificationSource Source => source;
    public VerificationWorkStatus CacheStatus => new VerificationWorkStale();
    public bool IsIdle => true;
    public IVerificationWork FromSeed(int seed) => seed == 73
      ? new StartFailureWork(source with { Id = new("Other", 0) }, error) : this;
    public IObservable<VerificationWorkStatus>? TryRun() => throw error;
    public void Cancel() { }
  }
}
