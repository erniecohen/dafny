using Microsoft.Dafny.Caching;
using Xunit;

namespace Microsoft.Dafny.Test;

public class ExecutionImageIdentityTests {
  [Fact]
  public void LinuxLoaderHasNoDependenciesButMissingLibrariesStillReject() {
    Assert.Empty(ExecutionImageIdentity.LinuxDependencies("\tstatically linked\n"));
    Assert.Equal(new[] { "/lib/libc.so.6", "/lib64/ld-linux-x86-64.so.2" },
      ExecutionImageIdentity.LinuxDependencies("linux-vdso.so.1 (0x123)\nlibc.so.6 => /lib/libc.so.6 (0x456)\n/lib64/ld-linux-x86-64.so.2 (0x789)\n"));
    Assert.Throws<NotSupportedException>(() => ExecutionImageIdentity.LinuxDependencies("liboptional.so => not found\n"));
    Assert.Throws<NotSupportedException>(() => ExecutionImageIdentity.LinuxDependencies("statically linked\nliboptional.so => not found\n"));
  }

  [Fact]
  public void DiscoveredDependenciesDoNotInvalidateRootEnumeration() {
    var files = new HashSet<string> { "/runtime/dotnet", "/solver/z3", "/runtime/libcoreclr.so" };
    var mapped = new HashSet<string>(files);
    var roots = ExecutionImageIdentity.LinuxDependencyRoots(files, mapped, "/solver/z3", "/runtime/dotnet");
    foreach (var root in roots) { files.Add(root + ".dependency"); }
    Assert.Equal(3, roots.Count);
    Assert.Equal(6, files.Count);
  }

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
