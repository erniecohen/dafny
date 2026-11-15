using System;
using System.Buffers.Binary;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Dafny.Caching;

/// <summary>Versioned, domain-separated hashes for persistent cache descriptors.</summary>
internal static class CacheDigest {
  public static string Compute(string domain, params ReadOnlyMemory<byte>[] components) {
    ArgumentException.ThrowIfNullOrEmpty(domain);
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    var fields = new[] { (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes("dafny-cache/v1"),
      Encoding.UTF8.GetBytes(domain) }.Concat(components);
    Span<byte> number = stackalloc byte[8];
    BinaryPrimitives.WriteUInt64BigEndian(number, checked((ulong)components.Length + 2));
    hash.AppendData(number);
    foreach (var field in fields) {
      BinaryPrimitives.WriteUInt64BigEndian(number, (ulong)field.Length);
      hash.AppendData(number);
      hash.AppendData(field.Span);
    }
    return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
  }

  public static string Blob(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

  public static bool IsValid(string value) => value is { Length: 64 } &&
    value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
