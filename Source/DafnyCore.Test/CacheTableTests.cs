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

  [Theory]
  [InlineData((int)CacheTaskOutcome.Correct)]
  [InlineData((int)CacheTaskOutcome.Errors)]
  [InlineData((int)CacheTaskOutcome.OutOfResource)]
  public void ReproducibleOutcomesRetainExactResourcesAndReportData(int outcomeValue) {
    var outcome = (CacheTaskOutcome)outcomeValue;
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Stored, cache.TryPublish(TaskInput(), true, outcome, 123, B("models + errors + coverage")));
    var loaded = cache.TryRead(TaskInput(), new ReportLoader());
    Assert.Equal(new Report(outcome, 123, "models + errors + coverage"), loaded);
  }

  [Theory]
  [InlineData((int)CacheTaskOutcome.Timeout)]
  [InlineData((int)CacheTaskOutcome.OutOfMemory)]
  [InlineData((int)CacheTaskOutcome.Cancelled)]
  [InlineData((int)CacheTaskOutcome.SolverException)]
  [InlineData((int)CacheTaskOutcome.Unknown)]
  [InlineData(200)]
  public void NonreproducibleOutcomesNeverPublish(int outcomeValue) {
    var outcome = (CacheTaskOutcome)outcomeValue;
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Unavailable, cache.TryPublish(TaskInput(), true, outcome, 42, B("report")));
    Assert.False(Directory.Exists(directory));
  }

  [Fact]
  public void PartialCompletionOrMissingReportingDataNeverPublishes() {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    Assert.Equal(CachePublication.Unavailable, cache.TryPublish(TaskInput(), false, CacheTaskOutcome.Correct, 42, B("report")));
    Assert.Equal(CachePublication.Unavailable, cache.TryPublish(TaskInput(), true, CacheTaskOutcome.Correct, -1, B("report")));
    Assert.Equal(CachePublication.Unavailable, cache.TryPublish(TaskInput(), true, CacheTaskOutcome.Errors, 42, []));
    Assert.False(Directory.Exists(directory));
  }

  [Theory]
  [InlineData(0U, 0U)]
  [InlineData(100U, 30U)]
  public void EffectiveElapsedTimeOrMissingRuBoundBypasses(uint resourceLimit, uint timeLimit) {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    var descriptor = TaskInput(resourceLimit: resourceLimit, timeLimit: timeLimit);
    Assert.Equal(CachePublication.Unavailable, cache.TryPublish(descriptor, true, CacheTaskOutcome.Errors, 42, B("report")));
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
    Assert.Equal(CachePublication.Stored, cache.TryPublish(descriptor, true, CacheTaskOutcome.Correct, 42, B("report")));
    Assert.NotNull(cache.TryRead(descriptor, new ReportLoader()));
  }

  [Fact]
  public void InputEnvironmentAndExactLimitInvalidateIndependently() {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    cache.TryPublish(TaskInput(), true, CacheTaskOutcome.Correct, 42, B("report"));
    Assert.Null(cache.TryRead(TaskInput(input: "changed pruned input"), new ReportLoader()));
    Assert.Null(cache.TryRead(TaskInput(environment: "changed solver/options/prelude"), new ReportLoader()));
    Assert.Null(cache.TryRead(TaskInput(resourceLimit: 101), new ReportLoader()));
    Assert.NotNull(cache.TryRead(TaskInput(), new ReportLoader()));
  }

  [Fact]
  public void FailedReportBindingCannotBecomeAResult() {
    var cache = new TaskResultCache(new ContentAddressedCache(directory));
    cache.TryPublish(TaskInput(), true, CacheTaskOutcome.Correct, 42, B("report"));
    var loader = new ReportLoader { Accept = false };
    Assert.Null(cache.TryRead(TaskInput(), loader));
    Assert.Equal(1, loader.Calls);
  }

  [Theory]
  [InlineData("schema")]
  [InlineData("input")]
  [InlineData("environment")]
  [InlineData("limit")]
  [InlineData("outcome")]
  public void StructurallyValidButMismatchedTaskRecordNeverReachesTheLoader(string mismatch) {
    var store = new ContentAddressedCache(directory);
    var descriptor = TaskInput();
    var payload = JsonSerializer.SerializeToUtf8Bytes(new {
      Schema = mismatch == "schema" ? 2 : 1,
      Input = B(mismatch == "input" ? "other task" : "pruned input"),
      Environment = B(mismatch == "environment" ? "other solver" : "complete environment"),
      ResourceLimit = mismatch == "limit" ? 101U : 100U,
      TimeLimit = 0U,
      Outcome = mismatch == "outcome" ? CacheTaskOutcome.Timeout : CacheTaskOutcome.Correct,
      ResourceCount = 42L,
      ReportData = B("report")
    });
    Assert.Equal(CachePublication.Stored, store.TryPublish(CacheTable.VerificationResult, descriptor.Key, payload));
    var loader = new ReportLoader();
    Assert.Null(new TaskResultCache(store).TryRead(descriptor, loader));
    Assert.Equal(0, loader.Calls);
  }

  [Fact]
  public void UnchangedPrunedDescriptorSurvivesChangesToOtherTaskInputs() {
    var results = new TaskResultCache(new ContentAddressedCache(directory));
    results.TryPublish(TaskInput(input: "unaffected task"), true, CacheTaskOutcome.Correct, 42, B("report"));
    results.TryPublish(TaskInput(input: "edited task before"), true, CacheTaskOutcome.Correct, 17, B("report"));
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
