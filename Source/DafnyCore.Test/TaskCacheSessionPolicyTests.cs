using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using Xunit;

namespace DafnyCore.Test;

public class TaskCacheSessionPolicyTests {
  [Fact]
  public void DisabledCachePreservesLegacyNumericOrdering() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    Assert.False(options.DeterministicLiteralHashes);
    Assert.Null(TaskCacheSession.For(options));
    Assert.False(options.DeterministicLiteralHashes);
  }

  [Fact]
  public void EnabledCacheStabilizesNumericInputsBeforePreparingTasks() {
    var directory = Path.Combine(Path.GetTempPath(), "task-cache-numeric-policy-" + Guid.NewGuid());
    try {
      var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
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
