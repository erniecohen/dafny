#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Microsoft.Dafny.Caching;

/// <summary>The actions of <c>dafny cache</c> on the large tier of the cache store (docs/dev/design/261-cache-store.md).</summary>
public static class CacheMaintenance {
  public const string Keep = "keep", Release = "release", Collect = "gc", Status = "status";
  private const int PreprocessingError = 1, DafnyError = 2;

  /// <summary>Runs <paramref name="action"/>, reporting to the options' output. Returns the exit code.</summary>
  public static int Run(DafnyOptions options, string action, string? tag) {
    var output = options.BaseOutputWriter;
    if (options.CliRootSourceUris.Count > 0) {
      output.WriteLine("Error: dafny cache takes at most a project file (" + DafnyProject.Extension + "), whose verification-cache and cache-partition it uses");
      return PreprocessingError;
    }
    if (CacheRoot.Directory(options) is not { } root) {
      output.WriteLine("Error: no cache store; set --cache-dir, verification-cache in dfyconfig.toml, or " + CacheRoot.DirectoryVariable);
      return PreprocessingError;
    }
    var partition = CacheRoot.Partition(options);
    var store = new ArtifactStore(root, partition, CacheRoot.Limits(options), message => output.WriteLine("cache: " + message));
    if (action is Keep or Release && (tag == null || !ArtifactStore.IsValidName(tag))) {
      output.WriteLine("Error: " + action + " needs --tag NAME: 1 to 100 letters, digits, '-', '_' or '.', not starting with '.'");
      return PreprocessingError;
    }
    switch (action) {
      case Keep:
        var kept = store.Keep(tag!, partition);
        if (kept < 0) {
          output.WriteLine("Error: the store has no partition " + partition);
          return DafnyError;
        }
        output.WriteLine($"tag {tag} holds the {kept} entries of partition {partition}");
        return 0;
      case Release:
        output.WriteLine($"released tag {tag} ({store.Release(tag!)} entries)");
        return 0;
      case Collect:
        store.EnforceBudget(partition);
        store.Sweep();
        var (holds, entries) = store.Collect();
        output.WriteLine($"collected {entries} unheld entries and {holds} holds of deleted entries");
        return 0;
      case Status:
        var (holders, count, size) = store.Status();
        output.WriteLine($"store {store.Root}: {count} large entries, {Megabytes(size)} MB (cap {store.Limits.StoreCap >> 20} MB)");
        output.WriteLine($"this run's partition: {partition} (budget {store.Limits.PartitionBudget >> 20} MB)");
        foreach (var holder in holders.OrderBy(h => h.Holder, StringComparer.Ordinal)) {
          var last = holder.LastUse == DateTime.MinValue ? "never" : holder.LastUse.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
          output.WriteLine($"  {holder.Holder}: {holder.Count} entries, {Megabytes(holder.Size)} MB, last used {last}");
        }
        return 0;
      default:
        throw new ArgumentOutOfRangeException(nameof(action));
    }
  }

  private static string Megabytes(long bytes) => (bytes / (1024.0 * 1024)).ToString("0.0", CultureInfo.InvariantCulture);
}
