using System.Security.Cryptography;
using System.Text.Json;

namespace DafnyB3Protocol;

public sealed record WorkerManifest(int Version, string B3Commit, string NormalizerVersion,
  string BootstrapCompiler, string SourceFingerprint, IReadOnlyDictionary<string, string> Files);

/// <summary>Checks the executable and its library against the packaged build manifest.</summary>
public sealed record WorkerPackage(string WorkerPath, string Fingerprint, WorkerManifest Manifest) {
  public const string UpstreamCommit = "ea6e8a18dfe9e317d313de769291f989957dc5f2";
  public const string BootstrapCompiler = "4.11.0+fcb2042d.review.a171069d";
  public const string SourceFingerprint = "22997b3e47b9b05da58d3efe42e8d7cb46d01a46ea741558c886257791153239";
  public const string ManifestFileName = "b3-worker-manifest.json";

  public static WorkerPackage Load(string workerPath) {
    workerPath = Path.GetFullPath(workerPath);
    var directory = Path.GetDirectoryName(workerPath)!;
    var manifestPath = Path.Combine(directory, ManifestFileName);
    var bytes = File.ReadAllBytes(manifestPath);
    if (bytes.Length > 65536) { throw new InvalidDataException("B3 worker manifest exceeds size bound"); }
    var manifest = JsonSerializer.Deserialize<WorkerManifest>(bytes, Protocol.JsonOptions)
      ?? throw new InvalidDataException("Missing B3 worker manifest");
    if (manifest.Version != Protocol.Version || manifest.B3Commit != UpstreamCommit ||
        manifest.NormalizerVersion != Protocol.NormalizerVersion || manifest.BootstrapCompiler != BootstrapCompiler ||
        manifest.SourceFingerprint != SourceFingerprint || manifest.Files.Count is < 3 or > 32 ||
        !manifest.Files.ContainsKey(Path.GetFileName(workerPath)) ||
        !manifest.Files.ContainsKey("B3Library.dll") || !manifest.Files.ContainsKey("DafnyB3Protocol.dll")) {
      throw new InvalidDataException("Unsupported or incomplete B3 worker build manifest");
    }
    foreach (var (name, digest) in manifest.Files) {
      if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name.Contains('\\') ||
          name == "." || name == ".." || !IsDigest(digest)) {
        throw new InvalidDataException("Invalid B3 worker file manifest");
      }
      using var file = File.OpenRead(Path.Combine(directory, name));
      if (Digest(SHA256.HashData(file)) != digest) {
        throw new InvalidDataException($"B3 worker package digest mismatch for {name}");
      }
    }
    return new WorkerPackage(workerPath, Digest(SHA256.HashData(bytes)), manifest);
  }
  private static bool IsDigest(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
  private static string Digest(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
}
