#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny.Caching;

/// <summary>
/// Where the shared cache store of issue 261 is, and which partition of its large tier a run accounts to.
/// The root is <c>--verification-cache</c> (or its alias <c>--cache-dir</c>), set on the command line or as
/// <c>verification-cache</c> in <c>dfyconfig.toml</c>, or else the environment variable <c>DAFNY_CACHE_DIR</c>.
/// With none of them, nothing is cached.
/// </summary>
internal static class CacheRoot {
  public const string DirectoryVariable = "DAFNY_CACHE_DIR";
  public const string PartitionVariable = "DAFNY_CACHE_PARTITION";

  public static string? Directory(DafnyOptions options) {
    var value = options.Get(CommonOptionBag.VerificationCache);
    if (string.IsNullOrEmpty(value)) { value = Environment.GetEnvironmentVariable(DirectoryVariable); }
    return string.IsNullOrEmpty(value) ? null : value;
  }

  /// <summary>
  /// <c>--cache-partition</c> (command line or project file), then <c>DAFNY_CACHE_PARTITION</c>, then a name derived
  /// from the project root: the directory of the project file, or else the current directory.
  /// </summary>
  public static string Partition(DafnyOptions options) {
    var value = options.Get(CommonOptionBag.CachePartition);
    if (string.IsNullOrEmpty(value)) { value = Environment.GetEnvironmentVariable(PartitionVariable); }
    // The option is validated when it is parsed; a malformed environment value is ignored.
    if (!string.IsNullOrEmpty(value) && ArtifactStore.IsValidName(value)) { return value; }
    var project = options.DafnyProject is { UsesProjectFile: true } dafnyProject
      ? Path.GetDirectoryName(dafnyProject.Uri.LocalPath)!
      : System.IO.Directory.GetCurrentDirectory();
    return DefaultPartition(project);
  }

  public static string DefaultPartition(string projectRoot) {
    var digest = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(projectRoot)));
    return "project-" + Convert.ToHexStringLower(digest)[..16];
  }

  public static ArtifactLimits Limits(DafnyOptions options) => new(
    (long)options.GetOrOptionDefault(CommonOptionBag.CachePartitionBudget) << 20,
    (long)options.GetOrOptionDefault(CommonOptionBag.CacheSizeCap) << 20,
    TimeSpan.FromDays(options.GetOrOptionDefault(CommonOptionBag.CacheOrphanDays)));
}
