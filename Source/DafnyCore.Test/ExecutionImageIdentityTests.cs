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

  /// <summary>https://github.com/erniecohen/dafny/issues/387: a self-contained publish has no
  /// <c>host/fxr</c>.  Its bundled host libraries identify the host instead, and the
  /// directory above it is not taken for a .NET installation.</summary>
  [Fact]
  public void SelfContainedLayoutIsIdentifiedByItsBundledHost() {
    var root = Path.Combine(Path.GetTempPath(), "image-identity-layout-" + Guid.NewGuid());
    try {
      var app = Path.Combine(root, "a", "b", "c", "dafny");
      Directory.CreateDirectory(app);
      foreach (var name in new[] { "dafny", "Dafny.dll", "libcoreclr.so", "libhostfxr.so", "libhostpolicy.so" }) {
        File.WriteAllText(Path.Combine(app, name), name);
      }
      var appWithSlash = app + Path.DirectorySeparatorChar;
      Assert.True(ImageRoots.IsSelfContained(appWithSlash, app));
      Assert.Null(ImageRoots.RuntimeRoot(appWithSlash, app));
      Assert.Equal(new[] { Path.Combine(app, "libhostfxr.so"), Path.Combine(app, "libhostpolicy.so") },
        ExecutionImageIdentity.HostDependencies(appWithSlash, appWithSlash));

      // A changed bundled host changes the identity's bytes, as any other image does.
      var memo = new ImageDigestMemo(null);
      var before = ExecutionImageIdentity.Digests(ExecutionImageIdentity.HostDependencies(app, app), memo).Digests;
      File.WriteAllText(Path.Combine(app, "libhostfxr.so"), "another host");
      var after = ExecutionImageIdentity.Digests(ExecutionImageIdentity.HostDependencies(app, app), new ImageDigestMemo(null)).Digests;
      Assert.NotEqual(before[Path.Combine(app, "libhostfxr.so")], after[Path.Combine(app, "libhostfxr.so")]);
      Assert.Equal(before[Path.Combine(app, "libhostpolicy.so")], after[Path.Combine(app, "libhostpolicy.so")]);

      // Without its host libraries a self-contained layout is unidentified, as before.
      File.Delete(Path.Combine(app, "libhostpolicy.so"));
      Assert.Throws<NotSupportedException>(() => ExecutionImageIdentity.HostDependencies(app, app));
      File.Delete(Path.Combine(app, "libhostfxr.so"));
      Assert.Throws<NotSupportedException>(() => ExecutionImageIdentity.HostDependencies(app, app));
    } finally {
      Directory.Delete(root, true);
    }
  }

  [Fact]
  public void FrameworkDependentLayoutIsIdentifiedByItsInstallationsHost() {
    var root = Path.Combine(Path.GetTempPath(), "image-identity-layout-" + Guid.NewGuid());
    try {
      var dotnet = Path.Combine(root, "dotnet");
      var runtime = Path.Combine(dotnet, "shared", "Microsoft.NETCore.App", "10.0.0");
      var fxr = Path.Combine(dotnet, "host", "fxr", "10.0.0");
      var app = Path.Combine(root, "dafny");
      foreach (var directory in new[] { runtime, fxr, app }) { Directory.CreateDirectory(directory); }
      File.WriteAllText(Path.Combine(runtime, "libhostpolicy.so"), "policy");
      File.WriteAllText(Path.Combine(fxr, "libhostfxr.so"), "fxr");
      // A host library beside the application does not stand in for the installation's.
      File.WriteAllText(Path.Combine(app, "libhostfxr.so"), "unused");
      Assert.False(ImageRoots.IsSelfContained(runtime, app));
      Assert.Equal(dotnet, ImageRoots.RuntimeRoot(runtime, app)!.FullName);
      Assert.Equal(new[] { Path.Combine(fxr, "libhostfxr.so") }, ExecutionImageIdentity.HostDependencies(runtime, app));
      Directory.Delete(Path.Combine(dotnet, "host"), true);
      Assert.Throws<NotSupportedException>(() => ExecutionImageIdentity.HostDependencies(runtime, app));
    } finally {
      Directory.Delete(root, true);
    }
  }
}
