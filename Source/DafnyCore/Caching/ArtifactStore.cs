#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny.Caching;

/// <summary>The tables of the large tier of the shared cache store (issue 261).</summary>
internal enum ArtifactTable { Resolutions, Translations, Preparations }

/// <summary>The budgets of the large tier. Sizes are in bytes.</summary>
internal sealed record ArtifactLimits(long PartitionBudget, long StoreCap, TimeSpan OrphanAge) {
  public const long DefaultPartitionBudgetMegabytes = 10 * 1024;
  public const long DefaultStoreCapMegabytes = 100 * 1024;
  public const int DefaultOrphanDays = 30;
  public static readonly ArtifactLimits Default = new(DefaultPartitionBudgetMegabytes << 20, DefaultStoreCapMegabytes << 20,
    TimeSpan.FromDays(DefaultOrphanDays));
}

/// <summary>
/// The large tier of the shared cache store of issue 261: resolution snapshots, Boogie text and Boogie
/// preparation state, under <c>ROOT/v1/artifacts</c>. Entries are content addressed and shared by every
/// project, branch and release that uses the root; see docs/dev/design/261-cache-store.md.
///
/// <list type="bullet">
/// <item>An entry is one file, <c>TABLE/KK/KEY</c>, with a header naming its table and key, its payload and
/// the SHA-256 of both. It is published by writing a temporary file in the same directory and renaming it
/// without replacement, so a reader sees no entry or a whole one, and two writers of one key publish one.
/// A read takes no lock; a file that does not check is a miss.</item>
/// <item>A <em>partition</em> (accounting only, never in a key) holds the entries it wrote or read, as
/// <c>partitions/P/holds/TABLE/KK/KEY</c>, a file holding the entry's size whose modification time is its
/// last use. A <em>tag</em> (<c>tags/T/...</c>) holds a set of entries until it is released.</item>
/// <item>An entry is deleted only when nothing holds it. A partition over its budget drops its least
/// recently used holds; a partition unused for longer than the orphan age drops all of them; a store
/// over its cap drops the least recently used untagged entries, then tagged ones, from every holder.</item>
/// </list>
/// </summary>
internal sealed class ArtifactStore {
  public const string LayoutVersion = "1";
  private const string Magic = "DART";
  private const int Format = 1;
  // A hold is touched at most this often, so a warm run does not rewrite every hold.
  private static readonly TimeSpan TouchInterval = TimeSpan.FromHours(1);
  // Collection leaves an entry this young alone: its writer may not have recorded its hold yet.
  private static readonly TimeSpan Settling = TimeSpan.FromMinutes(10);
  private static readonly TimeSpan StaleLock = TimeSpan.FromMinutes(10);
  private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);
  private static readonly ConcurrentDictionary<(string, string), ArtifactStore> Open = new();

  public string Root { get; }
  public string Partition { get; }
  public ArtifactLimits Limits { get; }
  private readonly string artifacts;
  private readonly ConcurrentDictionary<(ArtifactTable, string), byte> used = new();
  private long published;
  private volatile bool unavailable;
  private readonly Action<string>? log;

  public ArtifactStore(string root, string partition, ArtifactLimits limits, Action<string>? log = null) {
    if (!IsValidName(partition)) { throw new ArgumentException("invalid cache partition name " + partition, nameof(partition)); }
    Root = Path.GetFullPath(root);
    Partition = partition;
    Limits = limits;
    this.log = log;
    artifacts = Path.Combine(Root, "v1", "artifacts");
  }

  /// <summary>The store of these options, or null if no cache root is set. One per root and partition in a process.</summary>
  public static ArtifactStore? For(DafnyOptions options) {
    if (CacheRoot.Directory(options) is not { } root) { return null; }
    var partition = CacheRoot.Partition(options);
    return Open.GetOrAdd((Path.GetFullPath(root), partition), key => {
      var store = new ArtifactStore(key.Item1, key.Item2, CacheRoot.Limits(options),
        message => options.ErrorWriter?.WriteLine("cache: " + message));
      AppDomain.CurrentDomain.ProcessExit += (_, _) => store.Finish();
      return store;
    });
  }

  public static bool IsValidName(string name) => name is { Length: > 0 and <= 100 } &&
    name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.') && name[0] != '.';

  // ---------------------------------------------------------------- entries

  public byte[]? TryRead(ArtifactTable table, string key) {
    ValidateKey(key);
    if (unavailable) { return null; }
    try {
      var path = EntryPath(table, key);
      if (!File.Exists(path)) { return null; }
      var bytes = File.ReadAllBytes(path);
      var payload = Decode(bytes, table, key);
      if (payload == null) {
        // Entries are published whole, so one that does not check is damaged: delete it, so it can be stored again.
        TryDelete(path);
        return null;
      }
      Use(table, key, bytes.Length);
      return payload;
    } catch (Exception error) when (IsStorageFailure(error)) {
      return null;
    }
  }

  /// <summary>
  /// Publishes an entry, unless one is present. With <paramref name="replace"/>, which a caller passes when the
  /// present entry did not apply, the new one replaces it (still atomically).
  /// </summary>
  public CachePublication TryPublish(ArtifactTable table, string key, ReadOnlySpan<byte> payload, bool replace = false) {
    ValidateKey(key);
    if (unavailable) { return CachePublication.Unavailable; }
    var path = EntryPath(table, key);
    var bytes = Encode(table, key, payload);
    try {
      EnsureLayout();
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      var temporary = Path.Combine(Path.GetDirectoryName(path)!, "." + Guid.NewGuid().ToString("N") + ".tmp");
      bool stored;
      try {
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
          stream.Write(bytes);
          stream.Flush(flushToDisk: true);
        }
        try {
          // The rename is atomic, so a reader sees the old entry or the new one, never part of one. Without
          // replace, the check for an existing entry is not atomic with the rename on every platform, so two
          // writers racing on one key may both report Stored; the key is a digest of the content, so the second
          // rename replaces the entry with the same bytes.
          File.Move(temporary, path, overwrite: replace);
          stored = true;
        } catch (IOException) when (File.Exists(path)) {
          stored = false;
        }
      } finally {
        try { if (File.Exists(temporary)) { File.Delete(temporary); } } catch (Exception error) when (IsStorageFailure(error)) { }
      }
      if (stored) { System.Threading.Interlocked.Add(ref published, bytes.Length); }
      Use(table, key, bytes.Length, force: true);
      return stored ? CachePublication.Stored : CachePublication.AlreadyPresent;
    } catch (Exception error) when (IsStorageFailure(error)) {
      unavailable = true;
      return CachePublication.Unavailable;
    }
  }

  /// <summary>Records a use of an entry by this partition, as a hit does.</summary>
  public void Touch(ArtifactTable table, string key) {
    ValidateKey(key);
    try {
      var info = new FileInfo(EntryPath(table, key));
      if (info.Exists) { Use(table, key, info.Length); }
    } catch (Exception error) when (IsStorageFailure(error)) { }
  }

  private void Use(ArtifactTable table, string key, long size, bool force = false) {
    used.TryAdd((table, key), 0);
    try {
      var hold = Path.Combine(PartitionPath(Partition), "holds", Relative(table, key));
      var info = new FileInfo(hold);
      if (!force && info.Exists && DateTime.UtcNow - info.LastWriteTimeUtc < TouchInterval) { return; }
      Directory.CreateDirectory(Path.GetDirectoryName(hold)!);
      File.WriteAllText(hold, size.ToString(CultureInfo.InvariantCulture));
    } catch (Exception error) when (IsStorageFailure(error)) { }
  }

  private static byte[] Encode(ArtifactTable table, string key, ReadOnlySpan<byte> payload) {
    using var bytes = new MemoryStream();
    using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
      writer.Write(Encoding.ASCII.GetBytes(Magic));
      writer.Write(Format);
      writer.Write(TableName(table));
      writer.Write(key);
      writer.Write((long)payload.Length);
      writer.Write(payload);
    }
    var body = bytes.ToArray();
    return [.. body, .. SHA256.HashData(body)];
  }

  private static byte[]? Decode(byte[] bytes, ArtifactTable table, string key) {
    if (bytes.Length < 4 + 4 + 32) { return null; }
    var body = bytes.AsSpan(0, bytes.Length - 32);
    if (!SHA256.HashData(body).AsSpan().SequenceEqual(bytes.AsSpan(bytes.Length - 32))) { return null; }
    using var reader = new BinaryReader(new MemoryStream(bytes, 0, bytes.Length - 32), Encoding.UTF8);
    try {
      if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != Magic || reader.ReadInt32() != Format ||
          reader.ReadString() != TableName(table) || reader.ReadString() != key) { return null; }
      var length = reader.ReadInt64();
      var payload = reader.ReadBytes(checked((int)length));
      return payload.Length == length && reader.BaseStream.Position == reader.BaseStream.Length ? payload : null;
    } catch (Exception error) when (error is EndOfStreamException or IOException or OverflowException or ArgumentException) {
      return null;
    }
  }

  // ---------------------------------------------------------------- the end of a run

  /// <summary>
  /// Records this run's entries as the partition's last run, marks the partition active, enforces its budget
  /// if this run published anything, and sweeps the store (orphans and the cap) at most once a day.
  /// </summary>
  public void Finish() {
    if (used.IsEmpty) { return; }
    try {
      var directory = PartitionPath(Partition);
      Directory.CreateDirectory(directory);
      WriteAtomically(Path.Combine(directory, "last-run"),
        string.Concat(used.Keys.Select(p => TableName(p.Item1) + " " + p.Item2 + "\n").Order(StringComparer.Ordinal)));
      File.SetLastWriteTimeUtc(Path.Combine(directory, "last-run"), DateTime.UtcNow);
      if (System.Threading.Interlocked.Read(ref published) > 0) { EnforceBudget(Partition); }
      var sweep = Path.Combine(artifacts, "last-sweep");
      if (!File.Exists(sweep) || DateTime.UtcNow - File.GetLastWriteTimeUtc(sweep) > SweepInterval) { Sweep(); }
    } catch (Exception error) when (IsStorageFailure(error)) { }
  }

  // ---------------------------------------------------------------- retention

  /// <summary>
  /// Makes tag <paramref name="tag"/> hold exactly the entries that <paramref name="partition"/> holds now, so that
  /// they outlive the partition's budget and its expiry. A dedicated partition per branch (for example
  /// <c>--cache-partition main</c>) makes this that branch's working set. Returns the number of entries tagged,
  /// or -1 if the partition does not exist.
  /// </summary>
  public int Keep(string tag, string partition) {
    if (!IsValidName(tag)) { throw new ArgumentException("invalid cache tag " + tag, nameof(tag)); }
    if (!IsValidName(partition)) { throw new ArgumentException("invalid cache partition name " + partition, nameof(partition)); }
    if (!Directory.Exists(PartitionPath(partition))) { return -1; }
    var directory = TagPath(tag);
    var previous = Holds(directory).Select(h => h.Relative).ToHashSet(StringComparer.Ordinal);
    var current = new HashSet<string>(StringComparer.Ordinal);
    foreach (var hold in Holds(Path.Combine(PartitionPath(partition), "holds"))) {
      var entry = new FileInfo(Path.Combine(artifacts, hold.Relative));
      if (!entry.Exists) { continue; }
      var tagHold = Path.Combine(directory, hold.Relative);
      Directory.CreateDirectory(Path.GetDirectoryName(tagHold)!);
      File.WriteAllText(tagHold, entry.Length.ToString(CultureInfo.InvariantCulture));
      current.Add(hold.Relative);
    }
    foreach (var relative in previous.Except(current)) { DropHold(Path.Combine(directory, relative), relative); }
    return current.Count;
  }

  /// <summary>Releases tag <paramref name="tag"/>, deleting every entry it alone held. Returns the number of holds dropped.</summary>
  public int Release(string tag) {
    if (!IsValidName(tag)) { throw new ArgumentException("invalid cache tag " + tag, nameof(tag)); }
    var directory = TagPath(tag);
    var holds = Holds(directory).ToList();
    foreach (var hold in holds) { DropHold(hold.Path, hold.Relative); }
    DeleteDirectory(directory);
    return holds.Count;
  }

  /// <summary>Drops the least recently used holds of <paramref name="partition"/> until it is within its budget.</summary>
  public void EnforceBudget(string partition) {
    using var guard = Lock(Path.Combine(PartitionPath(partition), "lock"));
    if (guard == null) { return; }
    var holds = Holds(Path.Combine(PartitionPath(partition), "holds")).OrderBy(h => h.LastUse).ToList();
    var total = holds.Sum(h => h.Size);
    foreach (var hold in holds) {
      if (total <= Limits.PartitionBudget) { break; }
      DropHold(hold.Path, hold.Relative);
      total -= hold.Size;
    }
  }

  /// <summary>Expires orphaned partitions, then enforces the store's cap.</summary>
  public void Sweep() {
    using var guard = Lock(Path.Combine(artifacts, "sweep.lock"));
    if (guard == null) { return; }
    var partitions = Path.Combine(artifacts, "partitions");
    if (Directory.Exists(partitions)) {
      foreach (var directory in Directory.EnumerateDirectories(partitions)) {
        var name = Path.GetFileName(directory);
        if (name == Partition) { continue; }
        if (DateTime.UtcNow - LastActivity(directory) <= Limits.OrphanAge) { continue; }
        var holds = Holds(Path.Combine(directory, "holds")).ToList();
        foreach (var hold in holds) { DropHold(hold.Path, hold.Relative); }
        DeleteDirectory(directory);
        log?.Invoke("expired partition " + name + " (" + holds.Count + " holds), unused for more than " +
                    Limits.OrphanAge.TotalDays.ToString(CultureInfo.InvariantCulture) + " days");
      }
    }
    EnforceCap();
    WriteAtomically(Path.Combine(artifacts, "last-sweep"), DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    File.SetLastWriteTimeUtc(Path.Combine(artifacts, "last-sweep"), DateTime.UtcNow);
  }

  /// <summary>
  /// The last resort: while the store is over its cap, deletes the least recently used entries, untagged before
  /// tagged, from every holder, and logs which partitions lost entries.
  /// </summary>
  private void EnforceCap() {
    var entries = Entries().ToList();
    var total = entries.Sum(e => e.Size);
    if (total <= Limits.StoreCap) { return; }
    var holders = new Dictionary<string, List<(string Owner, Hold Hold)>>(StringComparer.Ordinal);
    foreach (var (owner, directory) in HolderDirectories()) {
      foreach (var hold in Holds(directory)) {
        if (!holders.TryGetValue(hold.Relative, out var list)) { holders[hold.Relative] = list = []; }
        list.Add((owner, hold));
      }
    }
    var order = entries.Select(e => {
      var list = holders.GetValueOrDefault(e.Relative) ?? [];
      var lastUse = list.Count == 0 ? e.LastUse : list.Max(h => h.Hold.LastUse);
      var tagged = list.Any(h => h.Owner.StartsWith("tag:", StringComparison.Ordinal));
      return (Entry: e, Holders: list, LastUse: lastUse, Tagged: tagged);
    }).OrderBy(e => e.Tagged).ThenBy(e => e.LastUse).ToList();
    var losers = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var deleted = 0;
    foreach (var item in order) {
      if (total <= Limits.StoreCap) { break; }
      foreach (var (owner, hold) in item.Holders) {
        TryDelete(hold.Path);
        losers[owner] = losers.GetValueOrDefault(owner) + 1;
      }
      TryDelete(item.Entry.Path);
      total -= item.Entry.Size;
      deleted++;
    }
    log?.Invoke("the store exceeded its cap of " + (Limits.StoreCap >> 20) + " MB; deleted " + deleted + " entries, held by " +
                (losers.Count == 0 ? "no partition" : string.Join(", ", losers.Select(p => p.Key + " (" + p.Value + ")"))));
  }

  /// <summary>Deletes the holds whose entry is gone, and the unheld entries older than the settling time.</summary>
  public (int Holds, int Entries) Collect() {
    using var guard = Lock(Path.Combine(artifacts, "sweep.lock"));
    if (guard == null) { return (0, 0); }
    var held = new HashSet<string>(StringComparer.Ordinal);
    var dangling = 0;
    foreach (var (_, directory) in HolderDirectories()) {
      foreach (var hold in Holds(directory)) {
        if (File.Exists(Path.Combine(artifacts, hold.Relative))) { held.Add(hold.Relative); } else { TryDelete(hold.Path); dangling++; }
      }
    }
    var unheld = 0;
    foreach (var entry in Entries()) {
      if (!held.Contains(entry.Relative) && DateTime.UtcNow - entry.LastUse > Settling) { TryDelete(entry.Path); unheld++; }
    }
    return (dangling, unheld);
  }

  /// <summary>Totals for <c>dafny cache status</c>: per holder, the number and size of its holds; and the whole store.</summary>
  public (IReadOnlyList<(string Holder, int Count, long Size, DateTime LastUse)> Holders, int Entries, long Size) Status() {
    var result = HolderDirectories().Select(p => {
      var holds = Holds(p.Directory).ToList();
      return (p.Owner, holds.Count, holds.Sum(h => h.Size), holds.Count == 0 ? DateTime.MinValue : holds.Max(h => h.LastUse));
    }).ToList();
    var entries = Entries().ToList();
    return (result, entries.Count, entries.Sum(e => e.Size));
  }

  // ---------------------------------------------------------------- helpers

  private sealed record Hold(string Path, string Relative, long Size, DateTime LastUse);

  private void DropHold(string path, string relative) {
    TryDelete(path);
    var entry = Path.Combine(artifacts, relative);
    if (!File.Exists(entry)) { return; }
    foreach (var (_, directory) in HolderDirectories()) {
      if (File.Exists(Path.Combine(directory, relative))) { return; }
    }
    TryDelete(entry);
  }

  private IEnumerable<(string Owner, string Directory)> HolderDirectories() {
    var partitions = Path.Combine(artifacts, "partitions");
    if (Directory.Exists(partitions)) {
      foreach (var directory in Directory.EnumerateDirectories(partitions).Order(StringComparer.Ordinal)) {
        yield return (Path.GetFileName(directory), Path.Combine(directory, "holds"));
      }
    }
    var tags = Path.Combine(artifacts, "tags");
    if (Directory.Exists(tags)) {
      foreach (var directory in Directory.EnumerateDirectories(tags).Order(StringComparer.Ordinal)) {
        yield return ("tag:" + Path.GetFileName(directory), directory);
      }
    }
  }

  private static IEnumerable<Hold> Holds(string directory) {
    if (!Directory.Exists(directory)) { yield break; }
    foreach (var table in Enum.GetValues<ArtifactTable>()) {
      var tableDirectory = Path.Combine(directory, TableName(table));
      if (!Directory.Exists(tableDirectory)) { continue; }
      foreach (var path in Directory.EnumerateFiles(tableDirectory, "*", SearchOption.AllDirectories)) {
        var key = Path.GetFileName(path);
        if (!CacheDigest.IsValid(key)) { continue; }
        long size;
        DateTime lastUse;
        try {
          size = long.TryParse(File.ReadAllText(path), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
          lastUse = File.GetLastWriteTimeUtc(path);
        } catch (Exception error) when (IsStorageFailure(error)) { continue; }
        yield return new Hold(path, Relative(table, key), size, lastUse);
      }
    }
  }

  private IEnumerable<Hold> Entries() {
    foreach (var table in Enum.GetValues<ArtifactTable>()) {
      var directory = Path.Combine(artifacts, TableName(table));
      if (!Directory.Exists(directory)) { continue; }
      foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) {
        var key = Path.GetFileName(path);
        if (!CacheDigest.IsValid(key)) { continue; }
        FileInfo info;
        try { info = new FileInfo(path); _ = info.Length; } catch (Exception error) when (IsStorageFailure(error)) { continue; }
        yield return new Hold(path, Relative(table, key), info.Length, info.LastWriteTimeUtc);
      }
    }
  }

  private static DateTime LastActivity(string partition) {
    var last = Directory.GetLastWriteTimeUtc(partition);
    var run = Path.Combine(partition, "last-run");
    if (File.Exists(run)) { last = File.GetLastWriteTimeUtc(run) > last ? File.GetLastWriteTimeUtc(run) : last; }
    return last;
  }

  /// <summary>An exclusive lock file, created atomically; one older than <see cref="StaleLock"/> is taken over. Null if held.</summary>
  private static IDisposable? Lock(string path) {
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    for (var attempt = 0; attempt < 2; attempt++) {
      try {
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        return new LockFile(stream, path);
      } catch (IOException) when (File.Exists(path)) {
        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) <= StaleLock) { return null; }
        TryDelete(path);
      }
    }
    return null;
  }

  private sealed class LockFile(FileStream stream, string path) : IDisposable {
    public void Dispose() { stream.Dispose(); TryDelete(path); }
  }

  private void EnsureLayout() {
    var version = Path.Combine(Root, "v1", "artifacts", "LAYOUT");
    if (!File.Exists(version)) {
      Directory.CreateDirectory(artifacts);
      WriteAtomically(version, LayoutVersion + "\n");
    }
  }

  private static void WriteAtomically(string path, string text) {
    var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    File.WriteAllText(temporary, text);
    File.Move(temporary, path, overwrite: true);
  }

  private static void TryDelete(string path) {
    try { File.Delete(path); } catch (Exception error) when (IsStorageFailure(error)) { }
  }

  private static void DeleteDirectory(string path) {
    try { if (Directory.Exists(path)) { Directory.Delete(path, true); } } catch (Exception error) when (IsStorageFailure(error)) { }
  }

  private string PartitionPath(string partition) => Path.Combine(artifacts, "partitions", partition);
  private string TagPath(string tag) => Path.Combine(artifacts, "tags", tag);
  private string EntryPath(ArtifactTable table, string key) => Path.Combine(artifacts, Relative(table, key));
  private static string Relative(ArtifactTable table, string key) => Path.Combine(TableName(table), key[..2], key);

  internal static string TableName(ArtifactTable table) => table switch {
    ArtifactTable.Resolutions => "resolutions",
    ArtifactTable.Translations => "translations",
    ArtifactTable.Preparations => "preparations",
    _ => throw new ArgumentOutOfRangeException(nameof(table))
  };

  private static void ValidateKey(string key) {
    if (!CacheDigest.IsValid(key)) { throw new ArgumentException("Cache keys must be lowercase SHA-256 digests.", nameof(key)); }
  }

  private static bool IsStorageFailure(Exception exception) =>
    exception is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}
