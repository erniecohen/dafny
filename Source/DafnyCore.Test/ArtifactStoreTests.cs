#nullable enable
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Dafny.Caching;
using Xunit;

namespace Microsoft.Dafny.Test;

/// <summary>The large tier of the shared cache store of issue 261: publication, holders, budgets, expiry, the cap and tags.</summary>
public class ArtifactStoreTests : IDisposable {
  private readonly string root = Path.Combine(Path.GetTempPath(), "dafny-artifacts-" + Guid.NewGuid().ToString("N"));
  private static readonly ArtifactLimits Roomy = new(1L << 30, 1L << 32, TimeSpan.FromDays(30));

  public void Dispose() {
    if (Directory.Exists(root)) { Directory.Delete(root, true); }
  }

  private static string Key(string name) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
  private static byte[] Payload(string name, int size = 1000) => Enumerable.Range(0, size).Select(i => (byte)(name[i % name.Length] + i)).ToArray();
  private ArtifactStore Store(string partition, ArtifactLimits? limits = null, List<string>? log = null) =>
    new(root, partition, limits ?? Roomy, log == null ? null : log.Add);
  private string Entry(string key) => Path.Combine(root, "v1", "artifacts", "resolutions", key[..2], key);
  private string Hold(string holder, string key) => Path.Combine(root, "v1", "artifacts", holder, "resolutions", key[..2], key);
  private string PartitionHold(string partition, string key) => Hold(Path.Combine("partitions", partition, "holds"), key);

  private static void Age(string path, double days) {
    var time = DateTime.UtcNow - TimeSpan.FromDays(days);
    if (Directory.Exists(path)) { Directory.SetLastWriteTimeUtc(path, time); } else { File.SetLastWriteTimeUtc(path, time); }
  }

  [Fact]
  public void APublishedEntryIsReadBackAndRecordsItsHolder() {
    var store = Store("one");
    var key = Key("a");
    Assert.Null(store.TryRead(ArtifactTable.Resolutions, key));
    Assert.False(Directory.Exists(root), "a miss must not create the store");
    Assert.Equal(CachePublication.Stored, store.TryPublish(ArtifactTable.Resolutions, key, Payload("a")));
    Assert.Equal(Payload("a"), store.TryRead(ArtifactTable.Resolutions, key));
    Assert.True(File.Exists(PartitionHold("one", key)));
    Assert.Equal("1\n", File.ReadAllText(Path.Combine(root, "v1", "artifacts", "LAYOUT")));
    // Tables are separate: the same key in another table is another entry.
    Assert.Null(store.TryRead(ArtifactTable.Translations, key));
  }

  [Fact]
  public void AnotherCheckoutOfTheSameContentSharesTheEntry() {
    // Two trees with their own partitions (the default partition is derived from the project's directory)
    // share one store: an entry one publishes is a hit for the other, which becomes a holder too.
    var first = Store(CacheRoot.DefaultPartition(Path.Combine(root, "checkout-one")));
    var second = Store(CacheRoot.DefaultPartition(Path.Combine(root, "elsewhere", "checkout-two")));
    Assert.NotEqual(first.Partition, second.Partition);
    var key = Key("shared");
    first.TryPublish(ArtifactTable.Resolutions, key, Payload("shared"));
    Assert.Equal(Payload("shared"), second.TryRead(ArtifactTable.Resolutions, key));
    Assert.True(File.Exists(PartitionHold(first.Partition, key)));
    Assert.True(File.Exists(PartitionHold(second.Partition, key)));
    // A different input is a different key, and a miss.
    Assert.Null(second.TryRead(ArtifactTable.Resolutions, Key("shared, edited")));
  }

  [Fact]
  public void ConcurrentWritersPublishOneEntryAndReadersSeeAllOrNothing() {
    var key = Key("raced");
    var payload = Payload("raced", 1 << 20);
    var outcomes = new ConcurrentBag<CachePublication>();
    var torn = 0;
    Parallel.For(0, 32, i => {
      var store = Store("p" + (i % 4));
      if (i % 2 == 0) {
        outcomes.Add(store.TryPublish(ArtifactTable.Resolutions, key, payload));
      } else if (store.TryRead(ArtifactTable.Resolutions, key) is { } read && !read.AsSpan().SequenceEqual(payload)) {
        Interlocked.Increment(ref torn);
      }
    });
    Assert.Equal(0, torn);
    // Racing writers of one key may each rename their copy into place (see TryPublish); at least one stores it.
    Assert.Contains(CachePublication.Stored, outcomes);
    Assert.All(outcomes, o => Assert.Contains(o, new[] { CachePublication.Stored, CachePublication.AlreadyPresent }));
    Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(Entry(key))!, "*.tmp"));
    Assert.Equal(payload, Store("p9").TryRead(ArtifactTable.Resolutions, key));
  }

  [Fact]
  public void ADamagedEntryIsAMissAndCanBeStoredAgain() {
    var store = Store("one");
    var key = Key("damaged");
    store.TryPublish(ArtifactTable.Resolutions, key, Payload("damaged"));
    var bytes = File.ReadAllBytes(Entry(key));
    bytes[bytes.Length / 2] ^= 1;
    File.WriteAllBytes(Entry(key), bytes);
    Assert.Null(store.TryRead(ArtifactTable.Resolutions, key));
    Assert.False(File.Exists(Entry(key)));
    Assert.Equal(CachePublication.Stored, store.TryPublish(ArtifactTable.Resolutions, key, Payload("damaged")));
    Assert.Equal(Payload("damaged"), store.TryRead(ArtifactTable.Resolutions, key));
    // An entry renamed to another key does not check either.
    var other = Key("other");
    Directory.CreateDirectory(Path.GetDirectoryName(Entry(other))!);
    File.Copy(Entry(key), Entry(other));
    Assert.Null(store.TryRead(ArtifactTable.Resolutions, other));
  }

  [Fact]
  public void ReplaceOverwritesAnEntryThatDidNotApply() {
    var store = Store("one");
    var key = Key("replaced");
    store.TryPublish(ArtifactTable.Resolutions, key, Payload("old"));
    Assert.Equal(CachePublication.AlreadyPresent, store.TryPublish(ArtifactTable.Resolutions, key, Payload("new")));
    Assert.Equal(Payload("old"), store.TryRead(ArtifactTable.Resolutions, key));
    Assert.Equal(CachePublication.Stored, store.TryPublish(ArtifactTable.Resolutions, key, Payload("new"), replace: true));
    Assert.Equal(Payload("new"), store.TryRead(ArtifactTable.Resolutions, key));
  }

  [Fact]
  public void AnEntryIsDeletedOnlyWhenEveryHolderHasDroppedIt() {
    var tight = new ArtifactLimits(1, 1L << 32, TimeSpan.FromDays(30));
    var key = Key("shared");
    Store("one").TryPublish(ArtifactTable.Resolutions, key, Payload("shared"));
    Store("two").TryRead(ArtifactTable.Resolutions, key);
    // Each holder is charged the whole entry: one byte of budget is too little for either.
    Store("one", tight).EnforceBudget("one");
    Assert.False(File.Exists(PartitionHold("one", key)));
    Assert.True(File.Exists(Entry(key)), "the other partition still holds the entry");
    Assert.Equal(Payload("shared"), Store("three").TryRead(ArtifactTable.Resolutions, key));
    Store("two", tight).EnforceBudget("two");
    Assert.True(File.Exists(Entry(key)), "the reader of the last step holds it too");
    Store("three", tight).EnforceBudget("three");
    Assert.False(File.Exists(Entry(key)), "nothing holds the entry");
  }

  [Fact]
  public void AChurningPartitionLeavesAnotherPartitionsEntriesAlone() {
    var size = Payload("x").Length + 200;
    var small = new ArtifactLimits(5L * size, 1L << 32, TimeSpan.FromDays(30));
    var steady = Store("steady", small);
    var kept = Enumerable.Range(0, 5).Select(i => Key("steady" + i)).ToList();
    foreach (var key in kept) { steady.TryPublish(ArtifactTable.Resolutions, key, Payload(key)); }
    steady.EnforceBudget("steady");
    var churn = Store("churn", small);
    var churned = Enumerable.Range(0, 50).Select(i => Key("churn" + i)).ToList();
    for (var i = 0; i < churned.Count; i++) {
      churn.TryPublish(ArtifactTable.Resolutions, churned[i], Payload(churned[i]));
      Age(PartitionHold("churn", churned[i]), churned.Count - i);
      churn.EnforceBudget("churn");
    }
    Assert.All(kept, key => Assert.True(File.Exists(Entry(key)) && File.Exists(PartitionHold("steady", key))));
    // The churning partition keeps its most recently used entries within its budget.
    var left = churned.Where(key => File.Exists(Entry(key))).ToList();
    Assert.InRange(left.Count, 1, 5);
    Assert.Equal(churned.TakeLast(left.Count), left);
  }

  [Fact]
  public void AnOrphanedPartitionExpires() {
    var log = new List<string>();
    var onlyOrphan = Key("only the orphan");
    var shared = Key("shared with an active partition");
    var orphan = Store("orphan");
    orphan.TryPublish(ArtifactTable.Resolutions, onlyOrphan, Payload("o"));
    orphan.TryPublish(ArtifactTable.Resolutions, shared, Payload("s"));
    orphan.Finish();
    Store("active").TryRead(ArtifactTable.Resolutions, shared);
    var directory = Path.Combine(root, "v1", "artifacts", "partitions", "orphan");
    Age(Path.Combine(directory, "last-run"), 31);
    Age(directory, 31);
    Store("active", log: log).Sweep();
    Assert.False(Directory.Exists(directory));
    Assert.False(File.Exists(Entry(onlyOrphan)));
    Assert.True(File.Exists(Entry(shared)));
    Assert.Contains(log, line => line.StartsWith("expired partition orphan (2 holds)"));
    // A partition used within the orphan age stays.
    Assert.True(Directory.Exists(Path.Combine(root, "v1", "artifacts", "partitions", "active")));
  }

  [Fact]
  public void TheCapDeletesTheLeastRecentlyUsedUntaggedEntriesAndNamesTheirHolders() {
    var log = new List<string>();
    var keys = Enumerable.Range(0, 6).Select(i => Key("capped" + i)).ToList();
    var store = Store("one");
    foreach (var key in keys) { store.TryPublish(ArtifactTable.Resolutions, key, Payload(key)); }
    Store("two").TryRead(ArtifactTable.Resolutions, keys[1]);
    for (var i = 0; i < keys.Count; i++) {
      Age(PartitionHold("one", keys[i]), 10 - i);
      if (File.Exists(PartitionHold("two", keys[i]))) { Age(PartitionHold("two", keys[i]), 10 - i); }
    }
    // The oldest entry is tagged, so the next ones go first.
    Directory.CreateDirectory(Path.GetDirectoryName(Hold("tags/release", keys[0]))!);
    File.WriteAllText(Hold("tags/release", keys[0]), "0");
    var size = new FileInfo(Entry(keys[0])).Length;
    new ArtifactStore(root, "one", new ArtifactLimits(1L << 30, 3 * size, TimeSpan.FromDays(30)), log.Add).Sweep();
    Assert.Equal(new[] { true, false, false, false, true, true }, keys.Select(k => File.Exists(Entry(k))));
    Assert.False(File.Exists(PartitionHold("two", keys[1])), "a deleted entry's holds go with it");
    Assert.Contains(log, line => line.Contains("deleted 3 entries, held by one (3), two (1)"));
  }

  [Fact]
  public void ATagKeepsAPartitionsEntriesUntilItIsReleased() {
    var key = Key("tagged");
    var store = Store("main");
    store.TryPublish(ArtifactTable.Resolutions, key, Payload("tagged"));
    Assert.Equal(-1, store.Keep("release", "no-such-partition"));
    Assert.Equal(1, store.Keep("release", "main"));
    Store("main", new ArtifactLimits(1, 1L << 32, TimeSpan.FromDays(30))).EnforceBudget("main");
    Assert.False(File.Exists(PartitionHold("main", key)));
    Assert.True(File.Exists(Entry(key)), "the tag holds it");
    // Keeping again replaces the tag's entries with the partition's current ones: none.
    var other = Key("later");
    Store("main").TryPublish(ArtifactTable.Resolutions, other, Payload("later"));
    Assert.Equal(1, store.Keep("release", "main"));
    Assert.False(File.Exists(Entry(key)), "nothing holds the first entry any more");
    Assert.Equal(1, store.Release("release"));
    Assert.True(File.Exists(Entry(other)), "the partition still holds the later entry");
    Assert.False(Directory.Exists(Path.Combine(root, "v1", "artifacts", "tags", "release")));
  }

  [Fact]
  public void CollectionDeletesUnheldEntriesAndDanglingHolds() {
    var store = Store("one");
    var held = Key("held");
    var unheld = Key("unheld");
    store.TryPublish(ArtifactTable.Resolutions, held, Payload("held"));
    store.TryPublish(ArtifactTable.Resolutions, unheld, Payload("unheld"));
    File.Delete(PartitionHold("one", unheld));
    Assert.Equal((0, 0), store.Collect());
    Age(Entry(unheld), 1);
    File.Delete(Entry(held));
    Assert.Equal((1, 1), store.Collect());
    Assert.False(File.Exists(Entry(unheld)));
    Assert.False(File.Exists(PartitionHold("one", held)));
  }

  [Fact]
  public void NamesAndKeysAreValidated() {
    Assert.Throws<ArgumentException>(() => Store("../escape"));
    Assert.Throws<ArgumentException>(() => Store(".hidden"));
    Assert.Throws<ArgumentException>(() => Store("one").TryRead(ArtifactTable.Resolutions, "not-a-key"));
    Assert.Throws<ArgumentException>(() => Store("one").Keep("a/b", "one"));
    Assert.True(ArtifactStore.IsValidName("project-0123abcd"));
    Assert.Matches("^project-[0-9a-f]{16}$", CacheRoot.DefaultPartition(root));
    Assert.Equal(CacheRoot.DefaultPartition(root), CacheRoot.DefaultPartition(root + "/."));
  }
}
