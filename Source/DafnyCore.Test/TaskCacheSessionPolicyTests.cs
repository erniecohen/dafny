using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using Xunit;

namespace DafnyCore.Test;

public class TaskCacheSessionPolicyTests {
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
