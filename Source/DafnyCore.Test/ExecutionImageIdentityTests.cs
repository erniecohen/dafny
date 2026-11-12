using Microsoft.Dafny.Caching;
using Xunit;

namespace Microsoft.Dafny.Test;

public class ExecutionImageIdentityTests {
  [Fact]
  public void UnloadedOptionalRuntimeProvidersAreNotDependencyRoots() {
    var files = new[] { "/runtime/dotnet", "/solver/z3", "/runtime/libcoreclr.so",
      "/runtime/liboptional-tracing.so", "/runtime/System.Private.CoreLib.dll" };
    var mapped = new HashSet<string> { files[0], files[2], files[4] };
    Assert.Equal(files.Take(3), ExecutionImageIdentity.LinuxDependencyRoots(files, mapped, files[1], files[0]));
    // Once loaded, the provider's required dependencies must be resolved too.
    mapped.Add(files[3]);
    Assert.Contains(files[3], ExecutionImageIdentity.LinuxDependencyRoots(files, mapped, files[1], files[0]));
  }
}
