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

  // A record-like payload that compresses well, as verification results do (issue 388).
  private static readonly byte[] Large = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 400).Select(i =>
    $"{{\"Step\":{i},\"Request\":\"(check-sat)\",\"Response\":\"unsat\",\"Resources\":{i * 37}}}\n")));
  private string LargeBlobPath => BlobPathOf(Large);

  [Fact]
  public void CompressibleBlobsAreStoredCompressedAndRoundTrip() {
    var cache = new ContentAddressedCache(directory);
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.VerificationResult, key, Large));
    var stored = File.ReadAllBytes(LargeBlobPath);
    Assert.True(BlobEncoding.IsEncoded(stored));
    Assert.True(stored.Length * 4 < Large.Length, $"{stored.Length} of {Large.Length}");
    Assert.Equal(Large, cache.TryRead(CacheTable.VerificationResult, key));
    Assert.Equal(Large, new ContentAddressedCache(directory).TryRead(CacheTable.VerificationResult, key));
    Assert.Equal(CachePublication.AlreadyPresent, new ContentAddressedCache(directory).TryPublish(CacheTable.VerificationResult, key, Large));
    Assert.Equal(Large, new ContentAddressedCache(directory).TryReadBlob(CacheDigest.Blob(Large)));
  }

  [Fact]
  public void AddressesAndRecordsCoverTheDecodedBytes() {
    // The address, and the record's Blob and Length, are those of the payload, whatever the
    // stored form; so neither keys nor addresses depend on the compression level or library.
    var cache = new ContentAddressedCache(directory);
    cache.TryPublish(CacheTable.Translation, key, Large);
    var record = JsonNode.Parse(File.ReadAllText(EntryPath))!;
    Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Large)).ToLowerInvariant(),
      (string)record["Blob"]!);
    Assert.Equal(Large.Length, (int)record["Length"]!);
    Assert.True(File.Exists(LargeBlobPath));
  }

  [Fact]
  public void IncompressibleBlobsAreStoredRaw() {
    var random = new byte[4096];
    new Random(388).NextBytes(random);
    var cache = new ContentAddressedCache(directory);
    cache.TryPublish(CacheTable.Translation, key, random);
    Assert.Equal(random, File.ReadAllBytes(BlobPathOf(random)));
    Assert.Equal(random, new ContentAddressedCache(directory).TryRead(CacheTable.Translation, key));
  }

  [Fact]
  public void UncompressedBlobsFromEarlierWritersStillRead() {
    var other = CacheDigest.Compute("translation-lookup/v1", Encoding.UTF8.GetBytes("other module"));
    // An earlier writer stored every blob raw. Plant that layout for one key, and add a new
    // compressed entry beside it.
    WriteLegacy("results", key, Large);
    var cache = new ContentAddressedCache(directory);
    Assert.Equal(Large, File.ReadAllBytes(LargeBlobPath));
    Assert.Equal(Large, cache.TryRead(CacheTable.VerificationResult, key));
    // A new publication of the same blob reuses the raw file as it is.
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.Translation, other, Large));
    Assert.Equal(Large, File.ReadAllBytes(LargeBlobPath));
    Assert.Equal(Large, cache.TryRead(CacheTable.Translation, other));
    Assert.Equal(CachePublication.AlreadyPresent, cache.TryPublish(CacheTable.VerificationResult, key, Large));
    // A new blob is compressed, and old and new entries both read in a fresh instance.
    var newer = Large.Concat("tail"u8.ToArray()).ToArray();
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.Translation, key, newer));
    Assert.True(BlobEncoding.IsEncoded(File.ReadAllBytes(BlobPathOf(newer))));
    var fresh = new ContentAddressedCache(directory);
    Assert.Equal(Large, fresh.TryRead(CacheTable.VerificationResult, key));
    Assert.Equal(Large, fresh.TryRead(CacheTable.Translation, other));
    Assert.Equal(newer, fresh.TryRead(CacheTable.Translation, key));
  }

  [Fact]
  public void RawBlobThatBeginsWithTheHeaderStillReads() {
    var lookalike = new byte[] { 0x00, 0x64, 0x63, 0x7a, 1, 1, 0, 0, 0, 3, 1, 2, 3 };
    WriteLegacy("translations", key, lookalike);
    Assert.Equal(lookalike, new ContentAddressedCache(directory).TryRead(CacheTable.Translation, key));
    // And the writer, which cannot shrink it, stores it raw too.
    var other = CacheDigest.Compute("translation-lookup/v1", Encoding.UTF8.GetBytes("other"));
    Directory.Delete(Path.Combine(directory, "v1"), recursive: true);
    var cache = new ContentAddressedCache(directory);
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.Translation, other, lookalike));
    Assert.Equal(lookalike, new ContentAddressedCache(directory).TryRead(CacheTable.Translation, other));
  }

  public static IEnumerable<object[]> Corruptions() {
    var encoded = BlobEncoding.Encode(Large);
    var cases = new List<(string, byte[])>();
    foreach (var length in new[] { 0, 3, 4, 9, 10, 11, encoded.Length / 2, encoded.Length - 4, encoded.Length - 1 }) {
      cases.Add(($"truncated to {length}", encoded[..length]));
    }
    foreach (var offset in new[] { 0, 3, 4, 5, 6, 9, 10, 11, encoded.Length / 2, encoded.Length - 1 }) {
      var flipped = encoded.ToArray();
      flipped[offset] ^= 0x01;
      cases.Add(($"bit flipped at {offset}", flipped));
    }
    var longer = encoded.ToArray();
    BinaryPrimitivesWrite(longer, (uint)Large.Length + 1);
    cases.Add(("length too large", longer));
    var shorter = encoded.ToArray();
    BinaryPrimitivesWrite(shorter, (uint)Large.Length - 1);
    cases.Add(("length too small", shorter));
    var bomb = encoded.ToArray();
    BinaryPrimitivesWrite(bomb, uint.MaxValue);
    cases.Add(("length beyond the bound", bomb));
    cases.Add(("trailing bytes", encoded.Concat(new byte[] { 0 }).ToArray()));
    var version = encoded.ToArray();
    version[4] = 2;
    cases.Add(("unknown version", version));
    var algorithm = encoded.ToArray();
    algorithm[5] = 2;
    cases.Add(("unknown algorithm", algorithm));
    cases.Add(("another blob, compressed", BlobEncoding.Encode(Large.Concat(new byte[] { 1 }).ToArray())));
    return cases.Select(c => new object[] { c.Item1, c.Item2 });
  }

  private static void BinaryPrimitivesWrite(byte[] stored, uint length) =>
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(6, 4), length);

  [Theory]
  [MemberData(nameof(Corruptions))]
  public void CorruptOrTruncatedCompressedBlobsAreMisses(string description, byte[] stored) {
    var cache = new ContentAddressedCache(directory);
    Assert.Equal(CachePublication.Stored, cache.TryPublish(CacheTable.VerificationResult, key, Large));
    File.WriteAllBytes(LargeBlobPath, stored);
    Assert.True(new ContentAddressedCache(directory).TryRead(CacheTable.VerificationResult, key) == null, description);
    Assert.Null(new ContentAddressedCache(directory).TryReadBlob(CacheDigest.Blob(Large)));
    // A corrupt object is not replaced by a later publisher: the key fails closed.
    Assert.Equal(CachePublication.Conflict, cache.TryPublish(CacheTable.VerificationResult, key, Large));
    Assert.Null(new ContentAddressedCache(directory).TryRead(CacheTable.VerificationResult, key));
  }

  [Fact]
  public void Adler32MatchesTheZLibTrailer() {
    Assert.Equal(0x11E60398u, BlobEncoding.Adler32("Wikipedia"u8));
    Assert.Equal(1u, BlobEncoding.Adler32([]));
    var big = new byte[100_000];
    Array.Fill(big, (byte)0xff);
    var encoded = BlobEncoding.Encode(big);
    Assert.Equal(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(encoded.AsSpan(encoded.Length - 4)),
      BlobEncoding.Adler32(big));
    Assert.Equal(big, BlobEncoding.TryDecode(encoded, big.Length));
    Assert.Null(BlobEncoding.TryDecode(encoded, big.Length - 1));
  }

  [Fact]
  public void DecodingArbitraryBytesNeverThrows() {
    var random = new Random(388);
    var encoded = BlobEncoding.Encode(Large);
    for (var trial = 0; trial < 2000; trial++) {
      var stored = trial % 2 == 0 ? encoded.ToArray() : new byte[random.Next(0, 64)];
      if (trial % 2 == 0) {
        for (var flips = random.Next(1, 8); flips > 0; flips--) {
          stored[random.Next(BlobEncoding.HeaderLength, stored.Length)] = (byte)random.Next(256);
        }
      } else {
        random.NextBytes(stored);
        if (stored.Length >= BlobEncoding.HeaderLength) {
          BlobEncoding.Encode(Large).AsSpan(0, 6).CopyTo(stored);
        }
      }
      var decoded = BlobEncoding.TryDecode(stored, 1 << 20);
      Assert.True(decoded == null || decoded.Length <= 1 << 20);
    }
  }

  [Fact]
  public async Task ConcurrentCompressedPublishersAndReadersSeeOnlyTheValue() {
    var tasks = Enumerable.Range(0, 32).Select(index => Task.Run(() => {
      var cache = new ContentAddressedCache(directory);
      if (index % 2 == 0) {
        var result = cache.TryPublish(CacheTable.VerificationResult, key, Large);
        Assert.Contains(result, new[] { CachePublication.Stored, CachePublication.AlreadyPresent });
        Assert.Equal(Large, cache.TryRead(CacheTable.VerificationResult, key));
      } else {
        var read = cache.TryRead(CacheTable.VerificationResult, key);
        Assert.True(read == null || read.SequenceEqual(Large));
      }
    }));
    await Task.WhenAll(tasks);
    Assert.Equal(Large, new ContentAddressedCache(directory).TryRead(CacheTable.VerificationResult, key));
    Assert.Empty(Directory.GetFileSystemEntries(directory, "*.tmp", SearchOption.AllDirectories));
  }

  private void WriteLegacy(string tableName, string entryKey, byte[] payload) {
    var digest = CacheDigest.Blob(payload);
    Directory.CreateDirectory(Path.GetDirectoryName(BlobPathOf(payload))!);
    File.WriteAllBytes(BlobPathOf(payload), payload);
    var entry = Path.Combine(directory, "v1", tableName, entryKey[..2], entryKey, "data");
    Directory.CreateDirectory(Path.GetDirectoryName(entry)!);
    File.WriteAllText(entry, new JsonObject {
      ["Schema"] = 1,
      ["Table"] = tableName,
      ["Key"] = entryKey,
      ["Blob"] = digest,
      ["Length"] = payload.Length
    }.ToJsonString());
  }

  private string BlobPathOf(byte[] payload) =>
    Path.Combine(directory, "v1", "blobs", "sha256", CacheDigest.Blob(payload)[..2], CacheDigest.Blob(payload), "data");

  private string EntryPath => Path.Combine(directory, "v1", "translations", key[..2], key, "data");
  private string BlobPath => Path.Combine(directory, "v1", "blobs", "sha256", CacheDigest.Blob(Payload)[..2], CacheDigest.Blob(Payload), "data");

  public void Dispose() {
    if (Directory.Exists(directory)) {
      Directory.Delete(directory, recursive: true);
    }
  }
}
