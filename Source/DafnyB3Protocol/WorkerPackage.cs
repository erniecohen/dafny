using System.Security.Cryptography;
using System.Text.Json;

namespace DafnyB3Protocol;

public sealed record WorkerManifest(int Version, string B3Commit, string NormalizerVersion,
  string BootstrapCompiler, string SourceFingerprint, IReadOnlyDictionary<string, string> Files);

/// <summary>Checks the executable and its library against the packaged build manifest.</summary>
public sealed record WorkerPackage(string WorkerPath, string Fingerprint, WorkerManifest Manifest) {
  public const string UpstreamCommit = "ea6e8a18dfe9e317d313de769291f989957dc5f2";
  public const string BootstrapCompiler = "4.11.0+fcb2042d.review.a171069d";
  public const string SourceFingerprint = "04acac15b45763c86af4b47b50b4415a9568d22ac304b86a0ba492d11bdb1722";
  public const string ManifestFileName = "b3-worker-manifest.json";
  public const int MaximumManifestBytes = 65536;
  public const long MaximumPackageBytes = 512L * 1024 * 1024;

  public static WorkerPackage Load(string workerPath) => LoadAsync(workerPath).GetAwaiter().GetResult();

  public static async Task<WorkerPackage> LoadAsync(string workerPath, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    deadline.CancelAfter(TimeSpan.FromSeconds(5));
    try {
      workerPath = Path.GetFullPath(workerPath);
      var directory = Path.GetDirectoryName(workerPath)!;
      var manifestPath = Path.Combine(directory, ManifestFileName);
      await using var manifestStream = OpenBoundedFile(manifestPath, MaximumManifestBytes);
      var bytes = new byte[checked((int)manifestStream.Length)];
      await manifestStream.ReadExactlyAsync(bytes, deadline.Token).ConfigureAwait(false);
      if (await manifestStream.ReadAsync(new byte[1], deadline.Token).ConfigureAwait(false) != 0) {
        throw new InvalidDataException("B3 worker manifest changed size during loading");
      }
      var manifest = JsonSerializer.Deserialize<WorkerManifest>(bytes, Protocol.JsonOptions)
        ?? throw new InvalidDataException("Missing B3 worker manifest");
      if (manifest.Version != Protocol.Version || manifest.B3Commit != UpstreamCommit ||
          manifest.NormalizerVersion != Protocol.NormalizerVersion || manifest.BootstrapCompiler != BootstrapCompiler ||
          manifest.SourceFingerprint != SourceFingerprint || manifest.Files == null || manifest.Files.Count is < 3 or > 32 ||
          !manifest.Files.ContainsKey(Path.GetFileName(workerPath)) ||
          !manifest.Files.ContainsKey("B3Library.dll") || !manifest.Files.ContainsKey("DafnyB3Protocol.dll")) {
        throw new InvalidDataException("Unsupported or incomplete B3 worker build manifest");
      }
      long packageBytes = 0;
      var buffer = new byte[65536];
      foreach (var (name, digest) in manifest.Files) {
        deadline.Token.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name.Contains('\\') ||
            name == "." || name == ".." || !IsDigest(digest)) {
          throw new InvalidDataException("Invalid B3 worker file manifest");
        }
        await using var file = OpenBoundedFile(Path.Combine(directory, name), MaximumPackageBytes);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int count;
        while ((count = await file.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0) {
          packageBytes += count;
          if (packageBytes > MaximumPackageBytes) { throw new InvalidDataException("B3 worker package exceeds size bound"); }
          hash.AppendData(buffer, 0, count);
        }
        if (Digest(hash.GetHashAndReset()) != digest) {
          throw new InvalidDataException($"B3 worker package digest mismatch for {name}");
        }
      }
      deadline.Token.ThrowIfCancellationRequested();
      return new WorkerPackage(workerPath, Digest(SHA256.HashData(bytes)), manifest);
    } catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested) {
      throw new TimeoutException("B3 worker package loading exceeded its deadline", exception);
    }
  }

  private static FileStream OpenBoundedFile(string path, long maximumBytes) {
    var file = new FileInfo(path);
    file = file.ResolveLinkTarget(true) as FileInfo ?? file;
    // Stat first: devices and FIFOs report zero length and must not be opened.
    if (file.Length <= 0 || file.Length > maximumBytes) {
      throw new InvalidDataException("B3 worker package file exceeds bounds: " + file.Name);
    }
    var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
      65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
    if (!stream.CanSeek || stream.Length <= 0 || stream.Length > maximumBytes) {
      stream.Dispose();
      throw new InvalidDataException("B3 worker package requires bounded regular files");
    }
    return stream;
  }
  private static bool IsDigest(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
  private static string Digest(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
}
