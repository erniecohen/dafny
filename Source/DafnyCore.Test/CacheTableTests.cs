using System.Text;
using System.Text.Json;
using Microsoft.Dafny.Caching;
using Xunit;

namespace Microsoft.Dafny.Test;

/// <summary>Storage contracts only. These tests do not claim translator or solver replay fidelity.</summary>
public class CacheTableTests : IDisposable {
  private readonly string directory = Path.Combine(Path.GetTempPath(), "dafny-tables-" + Guid.NewGuid().ToString("N"));
  private static byte[] B(string text) => Encoding.UTF8.GetBytes(text);
  private static TaskCacheDescriptor TaskInput(string input = "pruned input", string environment = "complete environment",
    uint resourceLimit = 100, uint timeLimit = 0) => new(B(input), B(environment), resourceLimit, timeLimit);

  private sealed record Report(CacheTaskOutcome Outcome, long ResourceCount, string Data);
  private sealed class ReportLoader : ITaskResultSnapshotLoader<Report> {
    public bool Accept { get; init; } = true;
    public int Calls { get; private set; }
    public bool TryLoad(TaskCacheDescriptor descriptor, CacheTaskOutcome outcome, long resourceCount, byte[] reportData,
      out Report result) {
      Calls++;
      result = new Report(outcome, resourceCount, Encoding.UTF8.GetString(reportData));
      return Accept;
    }
  }

  private static CachePublication Publish(TaskResultCache cache, TaskCacheDescriptor descriptor, CacheTaskOutcome outcome,
    long resourceCount = 42, bool reachedLimit = false, string report = "report", bool completed = true) =>
    cache.TryPublish(descriptor, completed, outcome, resourceCount, resourceCount, reachedLimit, B(report));

  [Theory]
  [InlineData((int)CacheTaskOutcome.Correct)]
  [InlineData((int)CacheTaskOutcome.Errors)]
  [InlineData((int)CacheTaskOutcome.OutOfResource)]
  public void ReproducibleOutcomesRetainExactResourcesAndReportData(int outcomeValue) {
    var outcome = (CacheTaskOutcome)outcomeValue;
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Stored, Publish(cache, TaskInput(), outcome, 100, report: "models + errors + coverage"));
    var loaded = cache.TryRead(TaskInput(), new ReportLoader());
    Assert.Equal(new Report(outcome, 100, "models + errors + coverage"), loaded);
  }

  [Theory]
  [InlineData((int)CacheTaskOutcome.Timeout)]
  [InlineData((int)CacheTaskOutcome.OutOfMemory)]
  [InlineData((int)CacheTaskOutcome.Cancelled)]
  [InlineData((int)CacheTaskOutcome.SolverException)]
  [InlineData((int)CacheTaskOutcome.Unknown)]
  [InlineData(200)]
  public void NonreproducibleOutcomesNeverPublish(int outcomeValue) {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Unavailable, Publish(cache, TaskInput(), (CacheTaskOutcome)outcomeValue));
    Assert.False(Directory.Exists(directory));
  }

  [Fact]
  public void PartialCompletionOrMissingReportingDataNeverPublishes() {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Unavailable, Publish(cache, TaskInput(), CacheTaskOutcome.Correct, completed: false));
    Assert.Equal(CachePublication.Unavailable, Publish(cache, TaskInput(), CacheTaskOutcome.Correct, -1));
    Assert.Equal(CachePublication.Unavailable, Publish(cache, TaskInput(), CacheTaskOutcome.Errors, report: ""));
    Assert.Equal(CachePublication.Unavailable,
      cache.TryPublish(TaskInput(), true, CacheTaskOutcome.Correct, 42, 41, false, B("report")));
    Assert.False(Directory.Exists(directory));
  }

  [Theory]
  [InlineData(0U, 0U)]
  [InlineData(100U, 30U)]
  public void EffectiveElapsedTimeOrMissingRuBoundBypassesNonValid(uint resourceLimit, uint timeLimit) {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    var descriptor = TaskInput(resourceLimit: resourceLimit, timeLimit: timeLimit);
    Assert.Equal(CachePublication.Unavailable, Publish(cache, descriptor, CacheTaskOutcome.Errors));
    Assert.Equal(CachePublication.Unavailable, Publish(cache, descriptor, CacheTaskOutcome.OutOfResource));
    var loader = new ReportLoader();
    Assert.Null(cache.TryRead(descriptor, loader));
    Assert.Equal(0, loader.Calls);
    Assert.False(Directory.Exists(directory));
  }

  [Theory]
  [InlineData(0U, 0U)]
  [InlineData(100U, 30U)]
  public void CompletedValidIsReusableWithTimeLimitsOrNoRuBound(uint resourceLimit, uint timeLimit) {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    var descriptor = TaskInput(resourceLimit: resourceLimit, timeLimit: timeLimit);
    Assert.Equal(CachePublication.Stored, Publish(cache, descriptor, CacheTaskOutcome.Correct));
    Assert.NotNull(cache.TryRead(descriptor, new ReportLoader()));
    Assert.Null(cache.TryRead(TaskInput(resourceLimit: resourceLimit, timeLimit: timeLimit + 1), new ReportLoader()));
  }

  [Fact]
  public void TheResourceLimitIsNotPartOfTheTaskKey() {
    Assert.Equal(TaskInput(resourceLimit: 100).Key, TaskInput(resourceLimit: 7).Key);
    Assert.Equal(TaskInput(resourceLimit: 100).Key, TaskInput(resourceLimit: 0).Key);
    Assert.NotEqual(TaskInput().Key, TaskInput(timeLimit: 1).Key);
  }

  // u = 60 resource units used, produced under L = 100. Each current limit L' is checked.
  [Theory]
  [InlineData(60U, true)]    // L' = u
  [InlineData(59U, false)]   // L' = u - 1: a cold run would run out of resources
  [InlineData(100U, true)]   // L' = L
  [InlineData(101U, true)]   // L' = L + 1
  [InlineData(0U, true)]     // no limit
  public void ValidIsReusedWhenTheLimitCoversItsResourceUse(uint limit, bool reused) {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Stored, Publish(cache, TaskInput(resourceLimit: 100), CacheTaskOutcome.Correct, 60));
    Assert.Equal(reused, cache.TryRead(TaskInput(resourceLimit: limit), new ReportLoader()) != null);
    // The same interaction under another sufficient limit is the same record, not a conflict.
    Assert.Equal(CachePublication.AlreadyPresent, Publish(cache, TaskInput(resourceLimit: 61), CacheTaskOutcome.Correct, 60));
  }

  [Theory]
  [InlineData(60U, true)]
  [InlineData(59U, false)]
  [InlineData(100U, true)]
  [InlineData(101U, true)]
  [InlineData(0U, false)]    // errors are reused under an RU limit only
  public void ErrorsThatNeverReachedTheLimitAreReusedWhenTheLimitCoversTheirResourceUse(uint limit, bool reused) {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Stored, Publish(cache, TaskInput(resourceLimit: 100), CacheTaskOutcome.Errors, 60));
    Assert.Equal(reused, cache.TryRead(TaskInput(resourceLimit: limit), new ReportLoader()) != null);
  }

  [Theory]
  [InlineData(100U, true)]
  [InlineData(99U, false)]
  [InlineData(101U, false)]
  public void ErrorsFromAnInteractionThatReachedTheLimitAreReusedOnlyAtThatLimit(uint limit, bool reused) {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Stored, Publish(cache, TaskInput(resourceLimit: 100), CacheTaskOutcome.Errors, 140, reachedLimit: true));
    Assert.Equal(reused, cache.TryRead(TaskInput(resourceLimit: limit), new ReportLoader()) != null);
  }

  // Out of resources at L = 100, having used u = 104. Under a smaller limit a fresh run stops
  // sooner and reports a different resource count, so only L' = L reuses it.
  [Theory]
  [InlineData(104U, false)]  // L' = u
  [InlineData(103U, false)]  // L' = u - 1
  [InlineData(100U, true)]   // L' = L
  [InlineData(101U, false)]  // L' = L + 1: a cold run might finish
  [InlineData(99U, false)]   // L' = L - 1: a cold run reports a smaller count
  [InlineData(0U, false)]
  public void OutOfResourceIsReusedOnlyAtTheSameLimit(uint limit, bool reused) {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Stored, Publish(cache, TaskInput(resourceLimit: 100), CacheTaskOutcome.OutOfResource, 104));
    Assert.Equal(reused, cache.TryRead(TaskInput(resourceLimit: limit), new ReportLoader()) != null);
  }

  [Fact]
  public void OutOfResourceRecordsUnderDifferentLimitsAreSeparateEntries() {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Stored, Publish(cache, TaskInput(resourceLimit: 100), CacheTaskOutcome.OutOfResource, 104));
    Assert.Equal(CachePublication.Stored, Publish(cache, TaskInput(resourceLimit: 200), CacheTaskOutcome.OutOfResource, 207));
    Assert.NotNull(cache.TryRead(TaskInput(resourceLimit: 100), new ReportLoader()));
    Assert.NotNull(cache.TryRead(TaskInput(resourceLimit: 200), new ReportLoader()));
    Assert.Null(cache.TryRead(TaskInput(resourceLimit: 150), new ReportLoader()));
  }

  [Fact]
  public void InputEnvironmentAndTimeLimitInvalidateIndependently() {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Publish(cache, TaskInput(), CacheTaskOutcome.Correct);
    Assert.Null(cache.TryRead(TaskInput(input: "changed pruned input"), new ReportLoader()));
    Assert.Null(cache.TryRead(TaskInput(environment: "changed solver/options/prelude"), new ReportLoader()));
    Assert.Null(cache.TryRead(TaskInput(timeLimit: 30), new ReportLoader()));
    Assert.NotNull(cache.TryRead(TaskInput(), new ReportLoader()));
  }

  [Fact]
  public void FailedReportBindingCannotBecomeAResult() {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Publish(cache, TaskInput(), CacheTaskOutcome.Correct);
    var loader = new ReportLoader { Accept = false };
    Assert.Null(cache.TryRead(TaskInput(), loader));
    Assert.Equal(1, loader.Calls);
  }

  [Theory]
  [InlineData("schema")]
  [InlineData("slot")]
  [InlineData("input")]
  [InlineData("environment")]
  [InlineData("limit")]
  [InlineData("time")]
  [InlineData("outcome")]
  [InlineData("peak")]
  public void StructurallyValidButMismatchedTaskRecordNeverReachesTheLoader(string mismatch) {
    var store = new ContentAddressedCache(directory);
    var descriptor = TaskInput();
    var payload = JsonSerializer.SerializeToUtf8Bytes(new {
      Schema = mismatch == "schema" ? 1 : TaskResultCache.Schema,
      Slot = mismatch == "slot" ? TaskResultCache.ErrorsSlot : TaskResultCache.ValidSlot,
      Input = B(mismatch == "input" ? "other task" : "pruned input"),
      Environment = B(mismatch == "environment" ? "other solver" : "complete environment"),
      ResourceLimit = mismatch == "limit" ? 100U : 0U,
      TimeLimit = mismatch == "time" ? 30U : 0U,
      Outcome = mismatch == "outcome" ? CacheTaskOutcome.Timeout : CacheTaskOutcome.Correct,
      ResourceCount = 42L,
      PeakResourceCount = mismatch == "peak" ? 101L : 42L,
      ReportData = B("report")
    });
    Assert.Equal(CachePublication.Stored, store.TryPublish(CacheTable.VerificationResult, descriptor.SlotKey(TaskResultCache.ValidSlot), payload));
    var loader = new ReportLoader();
    Assert.Null(new TaskResultCache(store).TryRead(descriptor, loader));
    Assert.Equal(0, loader.Calls);
  }

  [Fact]
  public void UnchangedPrunedDescriptorSurvivesChangesToOtherTaskInputs() {
    var results = new TaskResultCache(new ContentAddressedCache(directory));
    Publish(results, TaskInput(input: "unaffected task"), CacheTaskOutcome.Correct);
    Publish(results, TaskInput(input: "edited task before"), CacheTaskOutcome.Correct, 17);
    Assert.NotNull(results.TryRead(TaskInput(input: "unaffected task"), new ReportLoader()));
    Assert.Null(results.TryRead(TaskInput(input: "edited task after"), new ReportLoader()));
    // Real compiler/pruning and solver-call-count controls belong to integration, not this store test.
  }

  [Fact]
  public void DescriptorsFreezeTheirInputs() {
    var input = B("pruned input");
    var context = B("environment");
    var task = new TaskCacheDescriptor(input, context, 100, 0);
    var taskKey = task.Key;
    input[0] ^= 0xff;
    context[0] ^= 0xff;
    task.Input[0] ^= 0xff;
    Assert.Equal(taskKey, task.Key);
    Assert.Equal(B("pruned input"), task.Input);
  }

  public void Dispose() {
    if (Directory.Exists(directory)) {
      Directory.Delete(directory, recursive: true);
    }
  }
}
