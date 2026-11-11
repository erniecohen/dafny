using System;
using System.IO;
using System.Security;
using System.Text.Json;

namespace Microsoft.Dafny.Caching;

internal enum CacheTable { Translation, VerificationResult, SolverIdentity }
internal enum CachePublication { Stored, AlreadyPresent, Unavailable, Conflict }

/// <summary>
/// Immutable, bounded objects and fail-closed lookup records. This class supplies storage only:
/// callers must establish descriptor completeness, producer trust, eligibility and replay fidelity.
/// No cache is constructed on the default verification path.
/// </summary>
internal sealed class ContentAddressedCache {
  internal const int MaximumRecordBytes = 4096;
  private const int Schema = 1;
  private readonly string root;
  private readonly int maximumPayloadBytes;
  private volatile bool unavailable;

  private sealed record Entry(int Schema, string Table, string Key, string Blob, int Length);

  public ContentAddressedCache(string directory, int maximumPayloadBytes = 64 * 1024 * 1024) {
    if (maximumPayloadBytes <= 0) {
      throw new ArgumentOutOfRangeException(nameof(maximumPayloadBytes));
    }
    root = Path.Combine(Path.GetFullPath(directory), "v1");
    this.maximumPayloadBytes = maximumPayloadBytes;
  }

  public byte[] TryRead(CacheTable table, string key) {
    ValidateKey(key);
    if (unavailable) {
      return null;
    }
    try {
      if (IsDisabled(table, key)) {
        return null;
      }
      var record = ReadBounded(EntryPath(table, key), MaximumRecordBytes);
      var entry = record == null ? null : JsonSerializer.Deserialize<Entry>(record);
      if (entry == null || entry.Schema != Schema || entry.Table != TableName(table) ||
          entry.Key != key || !CacheDigest.IsValid(entry.Blob) ||
          entry.Length < 0 || entry.Length > maximumPayloadBytes) {
        return null;
      }
      var bytes = ReadBounded(BlobPath(entry.Blob), maximumPayloadBytes);
      if (bytes == null || bytes.Length != entry.Length || CacheDigest.Blob(bytes) != entry.Blob ||
          IsDisabled(table, key)) {
        return null;
      }
      return bytes;
    } catch (Exception exception) when (IsStorageFailure(exception)) {
      return null;
    }
  }

  public CachePublication TryPublish(CacheTable table, string key, ReadOnlySpan<byte> payload) {
    ValidateKey(key);
    // Resolve table names before doing any I/O, including on oversized inputs.
    var entryPath = EntryPath(table, key);
    if (unavailable || payload.Length > maximumPayloadBytes) {
      return CachePublication.Unavailable;
    }
    try {
      if (IsDisabled(table, key)) {
        return CachePublication.Conflict;
      }
      var blob = CacheDigest.Blob(payload);
      var blobPath = BlobPath(blob);
      if (!PublishFile(blobPath, payload)) {
        var existingBlob = ReadBounded(blobPath, maximumPayloadBytes);
        if (existingBlob == null || !payload.SequenceEqual(existingBlob)) {
          Disable(table, key);
          return CachePublication.Conflict;
        }
      }
      var entry = new Entry(Schema, TableName(table), key, blob, payload.Length);
      var record = JsonSerializer.SerializeToUtf8Bytes(entry);
      var stored = PublishFile(entryPath, record);
      if (!stored) {
        var existing = TryRead(table, key);
        if (existing == null || !payload.SequenceEqual(existing)) {
          Disable(table, key);
          return CachePublication.Conflict;
        }
      }
      return IsDisabled(table, key) ? CachePublication.Conflict :
        stored ? CachePublication.Stored : CachePublication.AlreadyPresent;
    } catch (Exception exception) when (IsStorageFailure(exception)) {
      // In particular, do not keep reading this instance after a failed conflict marker.
      unavailable = true;
      return CachePublication.Unavailable;
    }
  }

  internal byte[] TryReadBlob(string digest) {
    ValidateKey(digest);
    if (unavailable) {
      return null;
    }
    try {
      var bytes = ReadBounded(BlobPath(digest), maximumPayloadBytes);
      return bytes != null && CacheDigest.Blob(bytes) == digest ? bytes : null;
    } catch (Exception exception) when (IsStorageFailure(exception)) {
      return null;
    }
  }

  internal bool TryPublishBlob(ReadOnlySpan<byte> bytes, out string digest) {
    digest = CacheDigest.Blob(bytes);
    if (unavailable || bytes.Length > maximumPayloadBytes) {
      return false;
    }
    try {
      if (PublishFile(BlobPath(digest), bytes)) {
        return true;
      }
      var existing = TryReadBlob(digest);
      return existing != null && bytes.SequenceEqual(existing);
    } catch (Exception exception) when (IsStorageFailure(exception)) {
      return false;
    }
  }

  private void Disable(CacheTable table, string key) {
    // Retain this marker until explicit eviction. Never silently choose one of two values.
    PublishFile(DisabledPath(table, key), []);
  }

  private static byte[] ReadBounded(string path, int maximumBytes) {
    using var stream = new FileStream(Path.Combine(path, "data"), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
    var length = stream.Length;
    if (length < 0 || length > maximumBytes) {
      return null;
    }
    var bytes = new byte[(int)length];
    stream.ReadExactly(bytes);
    return stream.ReadByte() == -1 ? bytes : null;
  }

  /// <returns>False only if another writer already published this destination.</returns>
  private static bool PublishFile(string path, ReadOnlySpan<byte> bytes) {
    var directory = Path.GetDirectoryName(path)!;
    if (OperatingSystem.IsWindows()) { Directory.CreateDirectory(directory); }
    else { Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    var temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
    try {
      if (OperatingSystem.IsWindows()) { Directory.CreateDirectory(temporary); }
      else { Directory.CreateDirectory(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
      using (var stream = new FileStream(Path.Combine(temporary, "data"), FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
      }
      try {
        // A published directory is nonempty. Even on platforms whose rename can
        // overwrite a file, it cannot replace another nonempty directory.
        Directory.Move(temporary, path);
        return true;
      } catch (IOException) when (Directory.Exists(path)) {
        return false;
      }
    } finally {
      // This writer owns only its temporary directory; never remove another writer's files.
      try {
        if (Directory.Exists(temporary)) {
          Directory.Delete(temporary, recursive: true);
        }
      } catch (Exception exception) when (IsStorageFailure(exception)) {
        // An orphaned temporary directory is not a lookup entry.
      }
    }
  }

  private static bool IsStorageFailure(Exception exception) =>
    exception is IOException or UnauthorizedAccessException or SecurityException or JsonException;

  private static string TableName(CacheTable table) => table switch {
    CacheTable.Translation => "translations",
    CacheTable.VerificationResult => "results",
    CacheTable.SolverIdentity => "solver-identities",
    _ => throw new ArgumentOutOfRangeException(nameof(table))
  };

  private bool IsDisabled(CacheTable table, string key) {
    try {
      _ = File.GetAttributes(DisabledPath(table, key));
      return true;
    } catch (FileNotFoundException) {
      return false;
    } catch (DirectoryNotFoundException) {
      return false;
    }
  }

  private string BlobPath(string digest) => Path.Combine(root, "blobs", "sha256", digest[..2], digest);
  private string EntryPath(CacheTable table, string key) =>
    Path.Combine(root, TableName(table), key[..2], key);
  private string DisabledPath(CacheTable table, string key) =>
    Path.Combine(root, "disabled", TableName(table), key[..2], key);

  private static void ValidateKey(string key) {
    if (!CacheDigest.IsValid(key)) {
      throw new ArgumentException("Cache keys must be lowercase SHA-256 digests.", nameof(key));
    }
  }
}
