using DafnyCore.Test.Resolver;
using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test.Verification;

[Collection("Cardinality resolution")]
public class VerificationRunTests {
  public static IEnumerable<object[]> Outcomes => Enum.GetValues<VerificationOutcome>().Select(o => new object[] { o });

  [Theory]
  [MemberData(nameof(Outcomes))]
  public async Task CompletionRequiresEveryIdentityAndRetainsEveryOutcome(VerificationOutcome outcome) {
    var owner = await Owner();
    var first = new Work(owner, new("Scope", 1));
    var second = new Work(owner, new("Scope", 2));
    var run = new VerificationRun(owner, [first, second]);
    Assert.Equal(2, run.ExpectedCount);
    Assert.False(run.IsComplete);
    Assert.False(run.IsVerified);
    var result = new VerificationWorkResult(outcome, [], new(123));
    run.Complete(second, result);
    var snapshot = run.Results;
    Assert.Same(result, snapshot[second.Source.Id]);
    Assert.False(run.IsComplete);
    Assert.Throws<InvalidOperationException>(() => run.Complete(second, result));
    Assert.Equal(1, run.CompletedCount);
    run.Complete(first, new(VerificationOutcome.Verified));
    Assert.True(run.IsComplete);
    Assert.False(run.IsClosed);
    Assert.False(run.IsVerified);
    Assert.False(run.AuditCompletion.IsCompleted);
    run.Close(second, VerificationCompletionKind.StreamClosed);
    var closures = run.Closures;
    Assert.Single(closures);
    Assert.False(run.IsClosed);
    run.Close(first, VerificationCompletionKind.CachedResult);
    await run.AuditCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.True(run.IsClosed);
    Assert.Single(closures);
    Assert.Equal(VerificationCompletionKind.CachedResult, run.Closures[first.Source.Id]);
    Assert.Equal(VerificationCompletionKind.StreamClosed, run.Closures[second.Source.Id]);
    Assert.Equal(outcome == VerificationOutcome.Verified, run.IsVerified);
    Assert.Single(snapshot);
    Assert.Equal(2, run.Results.Count);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ErrorsAndCancellationRemainNonSuccessEvenAfterAResult(bool cancelled) {
    var owner = await Owner();
    var work = new Work(owner, new("Scope", 1));
    var run = new VerificationRun(owner, [work]);
    run.Complete(work, new(VerificationOutcome.Verified));
    var error = cancelled ? (Exception)new OperationCanceledException() : new InvalidOperationException("worker error");
    run.Fail(work, error);
    Assert.True(run.IsComplete);
    Assert.False(run.IsVerified);
    var failure = Assert.Single(run.Failures).Value;
    Assert.Equal(cancelled ? VerificationOutcome.Cancelled : VerificationOutcome.ToolError, failure.Outcome);
    Assert.Same(error, failure.Exception);
    run.Fail(work, new Exception("later error"));
    Assert.Same(error, Assert.Single(run.Failures).Value.Exception);
    Assert.Throws<InvalidOperationException>(() => run.Complete(work, new(VerificationOutcome.Verified)));
  }

  [Fact]
  public async Task ForeignRepeatedMalformedAndChangedResponsesCannotFillMissingWork() {
    var owner = await Owner();
    var first = new Work(owner, new("Scope", 1));
    var missing = new Work(owner, new("Scope", 2));
    var run = new VerificationRun(owner, [first, missing]);
    var result = new VerificationWorkResult(VerificationOutcome.Verified);
    Assert.Throws<InvalidOperationException>(() => run.Complete(new Work(owner, first.Source.Id), result));
    Assert.Throws<InvalidOperationException>(() => run.Complete(first, new((VerificationOutcome)12345)));
    Assert.Throws<InvalidOperationException>(() => run.Complete(first, null!));
    var original = first.Source;
    first.Source = first.Source with { Id = missing.Source.Id };
    Assert.Throws<InvalidOperationException>(() => run.Complete(first, result));
    first.Source = original with { CanVerify = null };
    Assert.Throws<InvalidOperationException>(() => run.Complete(first, result));
    first.Source = original;
    run.Complete(first, result);
    Assert.Throws<InvalidOperationException>(() => run.Complete(first, result));
    Assert.False(run.IsComplete);
    Assert.False(run.IsVerified);
    var next = new VerificationRun(owner, [new Work(owner, first.Source.Id)]);
    Assert.Throws<InvalidOperationException>(() => next.Complete(first, result));
    Assert.False(next.IsComplete);
  }

  [Fact]
  public async Task ManifestChecksTheWholeModuleAndRequestsSelectTheirOwnWork() {
    var owner = await Owner();
    var first = new Work(owner, new("Scope", 1));
    var second = new Work(owner, new("Scope", 2));
    var manifest = VerificationWorkManifest.Check(owner.ContainingModule, [first, second]);
    Assert.Equal(2, manifest.Expected.Count);
    Assert.Same(owner.ContainingModule, manifest.Module);
    Assert.Throws<InvalidOperationException>(() => VerificationWorkManifest.Check(owner.ContainingModule, [first, first]));
    Assert.Throws<InvalidOperationException>(() => VerificationWorkManifest.Check(owner.ContainingModule,
      [first, new Work(owner, first.Source.Id)]));
    second.Source = second.Source with { CanVerify = null };
    Assert.Throws<InvalidOperationException>(() => VerificationWorkManifest.Check(owner.ContainingModule, [first, second]));
    second.Source = first.Source with { Id = new("", 3) };
    Assert.Throws<InvalidOperationException>(() => VerificationWorkManifest.Check(owner.ContainingModule, [second]));
    Assert.Throws<InvalidOperationException>(() => new VerificationRun(owner, [first, first]));
    Assert.Throws<InvalidOperationException>(() => new VerificationRun(owner, [second.WithOwner(null)]));
    var selected = manifest.StartRun(owner, [first]);
    Assert.Same(manifest, selected.Manifest);
    Assert.Throws<InvalidOperationException>(() => manifest.StartRun(owner, [new Work(owner, new("Unknown", 0))]));
    selected.Complete(first, new(VerificationOutcome.Verified));
    Assert.False(selected.IsVerified);
    selected.Close(first, VerificationCompletionKind.StreamClosed);
    Assert.True(selected.IsVerified);
    var empty = new VerificationRun(owner, []);
    Assert.True(empty.IsComplete);
    Assert.Empty(empty.Results);
    Assert.True(empty.IsClosed);
    Assert.True(empty.AuditCompletion.IsCompletedSuccessfully);
  }

  [Fact]
  public async Task ClosureCannotReplaceResultsOrFailuresAndRequiresOriginalWork() {
    var owner = await Owner();
    var work = new Work(owner, new("Scope", 1));
    var run = new VerificationRun(owner, [work]);
    Assert.Throws<InvalidOperationException>(() => run.Close(work, VerificationCompletionKind.StreamClosed));
    Assert.Empty(run.Closures);
    Assert.False(run.AuditCompletion.IsCompleted);
    run.Complete(work, new(VerificationOutcome.Verified));
    Assert.Throws<InvalidOperationException>(() => run.Close(new Work(owner, work.Source.Id), VerificationCompletionKind.StreamClosed));
    Assert.Throws<InvalidOperationException>(() => run.Close(work, (VerificationCompletionKind)123));
    run.Close(work, VerificationCompletionKind.StreamClosed);
    Assert.True(run.IsVerified);
    Assert.Throws<InvalidOperationException>(() => run.Close(work, VerificationCompletionKind.StreamClosed));
    var error = new OperationCanceledException();
    run.Fail(work, error);
    Assert.False(run.IsVerified);
    Assert.Same(error, Assert.Single(run.Failures).Value.Exception);
    var failed = new VerificationRun(owner, [work]);
    failed.Complete(work, new(VerificationOutcome.Verified));
    failed.Fail(work, error);
    await failed.AuditCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.False(failed.IsClosed);
    Assert.False(failed.IsVerified);
    Assert.Throws<InvalidOperationException>(() => failed.Close(work, VerificationCompletionKind.StreamClosed));
  }

  private static async Task<Method> Owner() {
    var program = await ModuleTranslationCacheTests.Resolve("lemma Good() { assert true; }", cached: false);
    return program.Modules().SelectMany(m => m.TopLevelDecls).OfType<TopLevelDeclWithMembers>()
      .SelectMany(d => d.Members).OfType<Method>().Single(m => m.Name == "Good");
  }

  private sealed class Work(ICanVerify owner, VerificationWorkId id) : IVerificationWork {
    public VerificationSource Source { get; set; } = new(id, owner.Origin, owner.Origin) { CanVerify = owner };
    public Work WithOwner(ICanVerify? other) { Source = Source with { CanVerify = other }; return this; }
    public VerificationWorkStatus CacheStatus => new VerificationWorkStale();
    public bool IsIdle => true;
    public IVerificationWork FromSeed(int seed) => this;
    public IObservable<VerificationWorkStatus>? TryRun() => throw new NotSupportedException();
    public void Cancel() { }
  }
}
