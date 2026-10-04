using System.Security.Cryptography;
using System.Text.Json;
using DafnyB3Protocol;
using Xunit;

namespace DafnyB3Protocol.Test;

public class WorkerPackageTests {
  [Fact]
  public async Task ValidPackageHasTheManifestFingerprint() {
    using var fixture = new PackageFixture();
    var package = await WorkerPackage.LoadAsync(fixture.Worker);
    Assert.Equal(fixture.ManifestDigest, package.Fingerprint);
  }

  [Fact]
  public async Task CancellationPrecedesPackageAccess() {
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      WorkerPackage.LoadAsync("absent-worker.dll", cancelled.Token));
  }

  [Fact]
  public void OversizedManifestIsRejectedBeforeReading() {
    using var fixture = new PackageFixture();
    using (var file = File.OpenWrite(fixture.Manifest)) { file.SetLength(WorkerPackage.MaximumManifestBytes + 1); }
    Assert.Throws<InvalidDataException>(() => WorkerPackage.Load(fixture.Worker));
  }

  [Fact]
  public void OversizedPackageFileIsRejectedBeforeHashing() {
    using var fixture = new PackageFixture();
    using (var file = File.OpenWrite(fixture.Library)) { file.SetLength(WorkerPackage.MaximumPackageBytes + 1); }
    Assert.Throws<InvalidDataException>(() => WorkerPackage.Load(fixture.Worker));
  }

  [UnixTheory]
  [InlineData(true)]
  [InlineData(false)]
  public void DeviceManifestAndLibraryAreRejectedBeforeOpening(bool manifest) {
    using var fixture = new PackageFixture();
    var path = manifest ? fixture.Manifest : fixture.Library;
    File.Delete(path);
    File.CreateSymbolicLink(path, "/dev/zero");
    Assert.Throws<InvalidDataException>(() => WorkerPackage.Load(fixture.Worker));
  }

  [Theory]
  [InlineData(1, "experimental-2")]
  [InlineData(2, "experimental-1")]
  public void PreviousWorkerIdentityCannotBeRelabelledForReal(int version, string normalizer) {
    using var fixture = new PackageFixture();
    var manifest = JsonSerializer.Deserialize<WorkerManifest>(File.ReadAllText(fixture.Manifest), Protocol.JsonOptions)!;
    File.WriteAllText(fixture.Manifest, JsonSerializer.Serialize(manifest with {
      Version = version, NormalizerVersion = normalizer }, Protocol.JsonOptions));
    Assert.Throws<InvalidDataException>(() => WorkerPackage.Load(fixture.Worker));
  }

  private sealed class PackageFixture : IDisposable {
    private readonly string directory = Path.Combine(Path.GetTempPath(), "b3-package-" + Guid.NewGuid().ToString("N"));
    public string Worker => Path.Combine(directory, "DafnyB3Host.dll");
    public string Library => Path.Combine(directory, "B3Library.dll");
    public string Manifest => Path.Combine(directory, WorkerPackage.ManifestFileName);
    public string ManifestDigest { get; }
    public PackageFixture() {
      Directory.CreateDirectory(directory);
      var files = new Dictionary<string, string>();
      foreach (var name in new[] { "DafnyB3Host.dll", "B3Library.dll", "DafnyB3Protocol.dll" }) {
        var bytes = new byte[] { 1, 2, 3 };
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
        files[name] = Digest(bytes);
      }
      var manifest = new WorkerManifest(Protocol.Version, WorkerPackage.UpstreamCommit, Protocol.NormalizerVersion,
        WorkerPackage.BootstrapCompiler, WorkerPackage.SourceFingerprint, files);
      var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, Protocol.JsonOptions);
      File.WriteAllBytes(Manifest, manifestBytes);
      ManifestDigest = Digest(manifestBytes);
    }
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public void Dispose() => Directory.Delete(directory, recursive: true);
  }
}
