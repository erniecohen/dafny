using System.Runtime.CompilerServices;
using System.Security.Cryptography;

[assembly: InternalsVisibleTo("DafnyB3Protocol.Test")]

namespace DafnyB3Protocol;

/// <summary>Computes the identity of a bounded solver file without executing it.</summary>
public static class SolverFileIdentity {
  public const long MaximumExecutableBytes = 512L * 1024 * 1024;
  public const int MaximumCaptureMilliseconds = 30000;

  public static async Task<string> ComputeAsync(string path, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    var file = new FileInfo(path);
    file = file.ResolveLinkTarget(true) as FileInfo ?? file;
    // Stat before opening: devices and FIFOs report zero size and must not be opened.
    if (file.Length is <= 0 or > MaximumExecutableBytes) {
      throw new InvalidDataException("B3 solver executable exceeds file bounds");
    }
    await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
      65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
    return await ComputeAsync(stream, MaximumExecutableBytes, cancellationToken).ConfigureAwait(false);
  }

  internal static async Task<string> ComputeAsync(Stream stream, long maximumBytes, CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();
    if (!stream.CanRead || !stream.CanSeek) {
      throw new InvalidDataException("B3 requires a bounded regular solver executable");
    }
    var expectedLength = stream.Length;
    if (expectedLength <= 0 || expectedLength > maximumBytes) {
      throw new InvalidDataException("B3 solver executable exceeds file bounds");
    }
    var buffer = new byte[65536];
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    long totalBytes = 0;
    int count;
    while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0) {
      cancellationToken.ThrowIfCancellationRequested();
      if (count > maximumBytes - totalBytes) {
        throw new InvalidDataException("B3 solver executable exceeds file bounds while hashing");
      }
      totalBytes += count;
      hash.AppendData(buffer, 0, count);
    }
    cancellationToken.ThrowIfCancellationRequested();
    if (totalBytes != expectedLength) {
      throw new InvalidDataException("B3 solver executable changed size while hashing");
    }
    return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
  }
}
