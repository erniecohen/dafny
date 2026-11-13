using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Dafny.Caching;
using Xunit;

namespace Microsoft.Dafny.Test;

public class ContentAddressedCacheTests : IDisposable {
  private readonly string directory = Path.Combine(Path.GetTempPath(), "dafny-cache-" + Guid.NewGuid().ToString("N"));
  private readonly string key = CacheDigest.Compute("translation-lookup/v1", Encoding.UTF8.GetBytes("module key"));
  private static readonly byte[] Payload = Encoding.UTF8.GetBytes("exact bytes\r\n\0λ\n");

  [Fact]
  public void ConstructionAndMissDoNotCreateDirectories() {
    var cache = new ContentAddressedCache(directory);
    Assert.Null(cache.TryRead(CacheTable.Translation, key));
    Assert.False(Directory.Exists(directory));
  }

  [Fact]
  public void DomainsAndComponentBoundariesMatter() {
    static ReadOnlyMemory<byte> B(string text) => Encoding.UTF8.GetBytes(text);
    Assert.NotEqual(CacheDigest.Compute("a", B("bc")), CacheDigest.Compute("ab", B("c")));
    Assert.NotEqual(CacheDigest.Compute("a", B("bc")), CacheDigest.Compute("a", B("b"), B("c")));
    Assert.NotEqual(CacheDigest.Compute("a"), CacheDigest.Compute("a", B("")));
    Assert.Equal(CacheDigest.Compute("a", B("λ")), CacheDigest.Compute("a", new byte[] { 0xce, 0xbb }));
    Assert.Matches("^[a-f0-9]{64}$", key);
    // Fixed vector for the documented big-endian, length-delimited wire format.
    Assert.Equal("5d75f1050eed7cfc84b77d81af7f368917a4a6e0d73f97e9f4c8ad0077003487",
      CacheDigest.Compute("task/v1", B("abc"), B("")));
  }

  [Fact]
  public void TablesAreIndependentAndObjectsAreShared() {
    var cache = new ContentAddressedCache(directory);
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.Translation, key, Payload));
    Assert.Equal(Payload, cache.TryRead(CacheTable.Translation, key));
    Assert.Null(cache.TryRead(CacheTable.VerificationResult, key));
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.VerificationResult, key, Payload));
    Assert.Single(Directory.GetFiles(Path.Combine(directory, "v1", "blobs"), "*", SearchOption.AllDirectories));
    Assert.Equal(CachePublication.AlreadyPresent, cache.TryPublish(CacheTable.Translation, key, Payload));
    Assert.Empty(Directory.GetFileSystemEntries(directory, "*.tmp", SearchOption.AllDirectories));
    Assert.Equal(Payload, new ContentAddressedCache(directory).TryRead(CacheTable.Translation, key));
  }

  [Fact]
  public void ReadsDoNotExposeSharedMutableBuffers() {
    var cache = new ContentAddressedCache(directory);
    cache.TryPublish(CacheTable.Translation, key, Payload);
    var first = cache.TryRead(CacheTable.Translation, key);
    first[0] ^= 0xff;
    Assert.Equal(Payload, cache.TryRead(CacheTable.Translation, key));
  }

  [Theory]
  [InlineData("Schema")]
  [InlineData("Table")]
  [InlineData("Key")]
  [InlineData("Blob")]
  [InlineData("Length")]
  public void MismatchedRecordIsAMissAndCannotBeOverwritten(string field) {
    var cache = new ContentAddressedCache(directory);
    cache.TryPublish(CacheTable.Translation, key, Payload);
    var record = JsonNode.Parse(File.ReadAllText(EntryPath))!;
    switch (field) {
      case "Schema": record[field] = 2; break;
      case "Table": record[field] = "results"; break;
      case "Key": record[field] = new string('a', 64); break;
      case "Blob": record[field] = "../elsewhere"; break;
      case "Length": record[field] = Payload.Length + 1; break;
    }
    File.WriteAllText(EntryPath, record.ToJsonString());
    Assert.Null(cache.TryRead(CacheTable.Translation, key));
    Assert.Equal(CachePublication.Conflict, cache.TryPublish(CacheTable.Translation, key, Payload));
    Assert.Equal(record.ToJsonString(), File.ReadAllText(EntryPath));
    Assert.Null(new ContentAddressedCache(directory).TryRead(CacheTable.Translation, key));
  }

  [Theory]
  [InlineData("")]
  [InlineData("{")]
  [InlineData("null")]
  [InlineData("[]")]
  [InlineData("{} trailing")]
  public void MalformedRecordsAreMisses(string record) {
    var cache = new ContentAddressedCache(directory);
    cache.TryPublish(CacheTable.Translation, key, Payload);
    File.WriteAllText(EntryPath, record);
    Assert.Null(cache.TryRead(CacheTable.Translation, key));
  }

  [Fact]
  public void CorruptBlobAndMissingBlobAreMisses() {
    var cache = new ContentAddressedCache(directory);
    cache.TryPublish(CacheTable.Translation, key, Payload);
    File.WriteAllBytes(BlobPath, new byte[Payload.Length]);
    Assert.Null(cache.TryRead(CacheTable.Translation, key));
    Assert.Equal(CachePublication.Conflict, cache.TryPublish(CacheTable.Translation, key, Payload));
    File.Delete(BlobPath);
    Assert.Null(new ContentAddressedCache(directory).TryRead(CacheTable.Translation, key));
  }

  [Fact]
  public void EvictionAndOrphanedObjectsAreHarmless() {
    var cache = new ContentAddressedCache(directory);
    cache.TryPublish(CacheTable.Translation, key, Payload);
    Directory.Delete(Path.GetDirectoryName(EntryPath)!, recursive: true);
    Assert.Null(cache.TryRead(CacheTable.Translation, key));
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.Translation, key, Payload));
    Directory.Delete(Path.Combine(directory, "v1"), recursive: true);
    Assert.Null(cache.TryRead(CacheTable.Translation, key));
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.Translation, key, Payload));
  }

  [Fact]
  public void LimitsAreCheckedBeforeAllocatingOrPublishing() {
    var cache = new ContentAddressedCache(directory, maximumPayloadBytes: Payload.Length - 1);
    Assert.Equal(CachePublication.Unavailable, cache.TryPublish(CacheTable.Translation, key, Payload));
    Assert.False(Directory.Exists(directory));
    new ContentAddressedCache(directory).TryPublish(CacheTable.Translation, key, Payload);
    Assert.Null(cache.TryRead(CacheTable.Translation, key));
    using (var stream = File.OpenWrite(EntryPath)) {
      stream.SetLength(ContentAddressedCache.MaximumRecordBytes + 1L);
    }
    Assert.Null(new ContentAddressedCache(directory).TryRead(CacheTable.Translation, key));
  }

  [Fact]
  public void ConflictingValueDisablesKeyForFutureInstances() {
    var cache = new ContentAddressedCache(directory);
    cache.TryPublish(CacheTable.Translation, key, Payload);
    Assert.Equal(CachePublication.Conflict, cache.TryPublish(CacheTable.Translation, key, "different"u8));
    Assert.Null(new ContentAddressedCache(directory).TryRead(CacheTable.Translation, key));
    Assert.Equal(CachePublication.Conflict, cache.TryPublish(CacheTable.Translation, key, Payload));
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.VerificationResult, key, Payload));
    Assert.Equal(Payload, cache.TryRead(CacheTable.VerificationResult, key));
  }

  [Fact]
  public void AnExpectedAlternativeValueKeepsTheFirstWithoutDisablingTheKey() {
    var cache = new ContentAddressedCache(directory);
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.VerificationResult, key, Payload, keepExisting: true));
    Assert.Equal(CachePublication.AlreadyPresent, cache.TryPublish(CacheTable.VerificationResult, key, "different"u8, keepExisting: true));
    Assert.Equal(Payload, new ContentAddressedCache(directory).TryRead(CacheTable.VerificationResult, key));
  }

  [Fact]
  public async Task ConcurrentIdenticalPublishersConvergeWithoutPartialReads() {
    var writers = Enumerable.Range(0, 16).Select(_ => Task.Run(() => {
      var cache = new ContentAddressedCache(directory);
      var result = cache.TryPublish(CacheTable.Translation, key, Payload);
      Assert.Contains(result, new[] { CachePublication.Stored, CachePublication.AlreadyPresent });
      Assert.Equal(Payload, cache.TryRead(CacheTable.Translation, key));
      return result;
    }));
    var results = await Task.WhenAll(writers);
    Assert.Single(results.Where(result => result == CachePublication.Stored));
    Assert.Empty(Directory.GetFileSystemEntries(directory, "*.tmp", SearchOption.AllDirectories));
  }

  [Fact]
  public async Task ConcurrentConflictingPublishersCannotLeaveAUsableKey() {
    var writes = Enumerable.Range(0, 16).Select(index => Task.Run(() => {
      var cache = new ContentAddressedCache(directory);
      return cache.TryPublish(CacheTable.Translation, key, Encoding.UTF8.GetBytes(index.ToString()));
    }));
    var results = await Task.WhenAll(writes);
    Assert.Contains(CachePublication.Conflict, results);
    Assert.Null(new ContentAddressedCache(directory).TryRead(CacheTable.Translation, key));
  }

  [Theory]
  [InlineData("../escape")]
  [InlineData("")]
  [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
  public void KeysCannotSupplyPaths(string invalid) {
    var cache = new ContentAddressedCache(directory);
    Assert.Throws<ArgumentException>(() => cache.TryRead(CacheTable.Translation, invalid));
    Assert.Throws<ArgumentException>(() => cache.TryPublish(CacheTable.Translation, invalid, Payload));
    Assert.False(Directory.Exists(directory));
  }

  [Fact]
  public void UnavailableStorageIsAMissAndDoesNotFailPublication() {
    Directory.CreateDirectory(directory);
    File.WriteAllText(Path.Combine(directory, "v1"), "not a directory");
    var cache = new ContentAddressedCache(directory);
    Assert.Equal(CachePublication.Unavailable, cache.TryPublish(CacheTable.Translation, key, Payload));
    Assert.Null(cache.TryRead(CacheTable.Translation, key));
  }

  private string EntryPath => Path.Combine(directory, "v1", "translations", key[..2], key, "data");
  private string BlobPath => Path.Combine(directory, "v1", "blobs", "sha256", CacheDigest.Blob(Payload)[..2], CacheDigest.Blob(Payload), "data");

  public void Dispose() {
    if (Directory.Exists(directory)) {
      Directory.Delete(directory, recursive: true);
    }
  }
}
