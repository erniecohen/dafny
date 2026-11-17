using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace Microsoft.Dafny.Caching;

/// <summary>
/// The stored form of a content-addressed blob (issue 388). A blob's address is the SHA-256 of its
/// decoded bytes, never of the stored file, so an address, and so every key that names it, does not
/// depend on whether or how the blob was compressed.
///
/// A compressed blob is the header below followed by a zlib stream (RFC 1950), which both .NET's
/// <see cref="ZLibStream"/> and the standard libraries of most languages read. Every other file is the
/// blob's own bytes, as all blobs were before compression: a reader still accepts those, and a writer
/// keeps a blob raw when compression would not make it smaller.
/// <code>
///   offset 0  4 bytes  magic 00 64 63 7a ("\0dcz"); no JSON payload begins with NUL
///   offset 4  1 byte   format version, 1
///   offset 5  1 byte   algorithm, 1 = zlib
///   offset 6  4 bytes  decoded length, unsigned big-endian
///   offset 10          compressed stream
/// </code>
/// Decoding is only a candidate: <see cref="ContentAddressedCache"/> accepts a decoded blob only if it
/// hashes to its address, so a corrupt or truncated file is a miss, never a different value.
/// </summary>
internal static class BlobEncoding {
  internal const int HeaderLength = 10;
  internal const byte FormatVersion = 1;
  internal const byte ZLibAlgorithm = 1;
  private static ReadOnlySpan<byte> Magic => [0x00, 0x64, 0x63, 0x7a];

  /// <summary>Fixed, so that the same build writes the same file for the same blob.</summary>
  internal const CompressionLevel Level = CompressionLevel.Optimal;

  public static byte[] Encode(ReadOnlySpan<byte> payload) {
    using var output = new MemoryStream(payload.Length / 4 + HeaderLength + 64);
    Span<byte> header = stackalloc byte[HeaderLength];
    Magic.CopyTo(header);
    header[4] = FormatVersion;
    header[5] = ZLibAlgorithm;
    BinaryPrimitives.WriteUInt32BigEndian(header[6..], (uint)payload.Length);
    output.Write(header);
    using (var compressor = new ZLibStream(output, Level, leaveOpen: true)) {
      compressor.Write(payload);
    }
    return output.Length < payload.Length ? output.ToArray() : payload.ToArray();
  }

  /// <summary>Whether <paramref name="stored"/> carries the compressed header (of any version).</summary>
  public static bool IsEncoded(ReadOnlySpan<byte> stored) =>
    stored.Length >= Magic.Length && stored[..Magic.Length].SequenceEqual(Magic);

  /// <returns>
  /// The decoded bytes of a compressed blob, or null if <paramref name="stored"/> is not one this
  /// reader understands, is corrupt or truncated, or would decode to more than
  /// <paramref name="maximumBytes"/>. Never throws on any input.
  /// </returns>
  public static byte[] TryDecode(byte[] stored, int maximumBytes) {
    if (stored.Length < HeaderLength + 6 || !IsEncoded(stored) || stored[4] != FormatVersion || stored[5] != ZLibAlgorithm) {
      return null;
    }
    var length = BinaryPrimitives.ReadUInt32BigEndian(stored.AsSpan(6, 4));
    if (length > (uint)maximumBytes) {
      return null;
    }
    try {
      using var input = new MemoryStream(stored, HeaderLength, stored.Length - HeaderLength, writable: false);
      using var decompressor = new ZLibStream(input, CompressionMode.Decompress);
      var decoded = new byte[length];
      decompressor.ReadExactly(decoded);
      // The stream must end at the recorded length, and the file must end with the stream's
      // Adler-32, which the decompressor does not check. So a truncated file, or one with bytes
      // appended, is a miss, although the caller's digest check would also have caught any change
      // to the decoded bytes.
      return decompressor.ReadByte() == -1 &&
             BinaryPrimitives.ReadUInt32BigEndian(stored.AsSpan(stored.Length - 4)) == Adler32(decoded) ? decoded : null;
    } catch (Exception exception) when (exception is InvalidDataException or IOException) {
      return null;
    }
  }

  internal static uint Adler32(ReadOnlySpan<byte> bytes) {
    const uint modulus = 65521;
    uint a = 1, b = 0;
    while (!bytes.IsEmpty) {
      // 5552 is the largest block whose sums cannot overflow 32 bits before reduction (RFC 1950).
      var block = bytes[..Math.Min(bytes.Length, 5552)];
      foreach (var value in block) {
        a += value;
        b += a;
      }
      a %= modulus;
      b %= modulus;
      bytes = bytes[block.Length..];
    }
    return (b << 16) | a;
  }
}
