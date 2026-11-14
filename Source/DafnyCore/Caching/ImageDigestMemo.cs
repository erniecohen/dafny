#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Microsoft.Dafny.Caching;

/// <summary>
/// What the file system reports about a file without reading it: its size, its modification
/// and status-change times to the nanosecond, and its device and inode numbers.
/// </summary>
internal sealed record FileStamp(long Length, long ModifiedSeconds, long ModifiedNanoseconds,
  long ChangedSeconds, long ChangedNanoseconds, ulong Device, ulong Inode) {
  private delegate int StatFunction([MarshalAs(UnmanagedType.LPUTF8Str)] string path, byte[] buffer);
  private static readonly Lazy<StatFunction?> Stat = new(LoadStat, LazyThreadSafetyMode.ExecutionAndPublication);

  /// <summary>Null where the platform's stat layout is not known, or the file cannot be examined.</summary>
  public static FileStamp? Read(string path) {
    if (Stat.Value is not { } stat) { return null; }
    var buffer = new byte[512];
    if (stat(path, buffer) != 0) { return null; }
    long Int64(int offset) => BitConverter.ToInt64(buffer, offset);
    FileStamp stamp;
    if (OperatingSystem.IsMacOS()) {
      // struct stat (64-bit inodes): st_dev (int32) at 0, st_ino at 8, st_mtimespec at 48,
      // st_ctimespec at 64, st_size at 96.
      stamp = new FileStamp(Int64(96), Int64(48), Int64(56), Int64(64), Int64(72),
        (ulong)BitConverter.ToInt32(buffer, 0), (ulong)Int64(8));
    } else {
      // Linux x86-64 and AArch64 struct stat: st_dev at 0, st_ino at 8, st_size at 48,
      // st_mtim at 88, st_ctim at 104.
      stamp = new FileStamp(Int64(48), Int64(88), Int64(96), Int64(104), Int64(112), (ulong)Int64(0), (ulong)Int64(8));
    }
    // Check the layout against what .NET reports; a mismatch disables the memo, never misreads it.
    // stat follows symbolic links, as reading the bytes does, while FileInfo describes the link.
    var info = new FileInfo(path);
    if (info.LinkTarget != null && info.ResolveLinkTarget(true) is FileInfo target) { info = target; }
    return info.Exists && info.Length == stamp.Length &&
           new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds() == stamp.ModifiedSeconds ? stamp : null;
  }

  private static StatFunction? LoadStat() {
    try {
      string? library = null;
      if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture is Architecture.Arm64 or Architecture.X64) {
        library = "/usr/lib/libSystem.B.dylib";
      } else if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture is Architecture.Arm64 or Architecture.X64) {
        library = "libc.so.6";
      }
      // On x86-64 macOS, "stat" is the legacy layout with 32-bit inodes.
      var name = OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "stat$INODE64" : "stat";
      if (library == null || !NativeLibrary.TryLoad(library, out var handle) ||
          !NativeLibrary.TryGetExport(handle, name, out var function)) {
        return null;
      }
      return Marshal.GetDelegateForFunctionPointer<StatFunction>(function);
    } catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or
                               MarshalDirectiveException or ArgumentException) {
      return null;
    }
  }
}

/// <summary>
/// Remembers, under the cache directory, the SHA-256 digest of an execution image's bytes,
/// keyed by its absolute path and its <see cref="FileStamp"/>.  Any difference in those fields
/// is a different key, and a missing, unreadable or inconsistent memo entry is recomputed.
/// This identifies the bytes more weakly than hashing them: a rewrite that kept the size, both
/// times, the device and the inode would be missed.  An ordinary process cannot set the
/// status-change time, and the images are read-only system files and build outputs.
/// Also remembers the load-command listings of an image, keyed by the digest of its bytes,
/// of which they are a function.
/// </summary>
internal sealed class ImageDigestMemo {
  private sealed record Entry(int Schema, string Path, FileStamp Stamp, string Digest);
  private const int Schema = 1;
  private readonly ContentAddressedCache? store;
  private readonly ConcurrentDictionary<(string, FileStamp), string> known = new();
  private int hashed;
  private int remembered;
  public int Hashed => Volatile.Read(ref hashed);
  public int Remembered => Volatile.Read(ref remembered);

  public ImageDigestMemo(string? cacheDirectory) {
    store = string.IsNullOrEmpty(cacheDirectory) ? null : new ContentAddressedCache(cacheDirectory);
  }

  internal static string Key(string path, FileStamp stamp) => CacheDigest.Compute("execution-image-digest/v1",
    Encoding.UTF8.GetBytes(path), JsonSerializer.SerializeToUtf8Bytes(stamp));

  public string Digest(string path) => Digest(path, FileStamp.Read);

  internal string Digest(string path, Func<string, FileStamp?> stat) {
    var before = store == null ? null : stat(path);
    if (before != null) {
      if (known.TryGetValue((path, before), out var seen)) { return seen; }
      try {
        var payload = store!.TryRead(CacheTable.ImageDigest, Key(path, before));
        var entry = payload == null ? null : JsonSerializer.Deserialize<Entry>(payload);
        if (entry is { Schema: Schema } && entry.Path == path && entry.Stamp == before && CacheDigest.IsValid(entry.Digest)) {
          Interlocked.Increment(ref remembered);
          known[(path, before)] = entry.Digest;
          return entry.Digest;
        }
      } catch (Exception e) when (e is JsonException or NotSupportedException) { }
    }
    string digest;
    using (var stream = File.OpenRead(path)) { digest = Convert.ToHexStringLower(SHA256.HashData(stream)); }
    Interlocked.Increment(ref hashed);
    // Remember only bytes that did not change while they were read.
    if (before != null && stat(path) == before) {
      known[(path, before)] = digest;
      store!.TryPublish(CacheTable.ImageDigest, Key(path, before),
        JsonSerializer.SerializeToUtf8Bytes(new Entry(Schema, path, before, digest)));
    }
    return digest;
  }

  /// <summary>
  /// The output of <paramref name="inspect"/>, a deterministic listing of the image's bytes
  /// selected by <paramref name="kind"/>, remembered under the digest of those bytes.
  /// </summary>
  public string Inspect(string path, string kind, Func<string> inspect) {
    if (store == null || FileStamp.Read(path) == null) { return inspect(); }
    var digest = Digest(path);
    var key = CacheDigest.Compute("execution-image-inspection/v1", Encoding.UTF8.GetBytes(digest), Encoding.UTF8.GetBytes(kind));
    if (store.TryRead(CacheTable.ImageDigest, key) is { } bytes) {
      try { return new UTF8Encoding(false, true).GetString(bytes); } catch (ArgumentException) { }
    }
    var output = inspect();
    // Remember the listing only of bytes that did not change while they were inspected.
    if (Digest(path) == digest) {
      store.TryPublish(CacheTable.ImageDigest, key, Encoding.UTF8.GetBytes(output));
    }
    return output;
  }
}
