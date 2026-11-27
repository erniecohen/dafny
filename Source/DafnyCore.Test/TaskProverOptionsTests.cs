using Microsoft.Dafny.Caching;
using Xunit;

namespace Microsoft.Dafny.Test;

public class TaskProverOptionsTests {
  [Fact]
  public void RepeatedIdenticalPathsHaveTheSameDescriptorWithoutChangingOptions() {
    string[] single = ["PROVER_PATH=solver", "O:timeout=0", "C:argument"];
    string[] repeated = ["PROVER_PATH=solver", "PROVER_PATH=solver", "O:timeout=0", "C:argument", "PROVER_PATH=solver"];
    var original = repeated.ToArray();
    Assert.Equal(single, TaskExecutionEnvironment.StableProverOptions(repeated));
    Assert.Equal(original, repeated);
    Assert.Equal(single, TaskExecutionEnvironment.StableProverOptions(single));
  }

  [Theory]
  [InlineData("PROVER_PATH=other")]
  [InlineData("PROVER_PATH:other")]
  [InlineData("PROVER_PATH:solver")]
  [InlineData("PROVER_PATH")]
  public void MixedPathSettingsRetainEveryAssignmentAndItsOrder(string middle) {
    string[] options = ["PROVER_PATH=solver", middle, "PROVER_PATH=solver"];
    Assert.Equal(options, TaskExecutionEnvironment.StableProverOptions(options));
  }

  [Fact]
  public void OtherOptionsKeepTheirMultiplicityAndOrder() {
    string[] options = ["O:timeout=0", "C:argument", "O:timeout=0", "C:argument"];
    Assert.Equal(options, TaskExecutionEnvironment.StableProverOptions(options));
    Assert.Equal(new[] { "PROVER_PATH=solver" }.Concat(options),
      TaskExecutionEnvironment.StableProverOptions(new[] { "PROVER_PATH=solver", "PROVER_PATH=solver" }.Concat(options)));
    Assert.Empty(TaskExecutionEnvironment.StableProverOptions([]));
  }
}
