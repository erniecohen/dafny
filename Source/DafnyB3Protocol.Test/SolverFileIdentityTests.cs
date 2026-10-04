using System.Runtime.InteropServices;
using DafnyB3Protocol;
using Xunit;

namespace DafnyB3Protocol.Test;

public class SolverFileIdentityTests {
  private const string AbcDigest = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

  [Fact]
  public async Task FileDigestIsCanonicalSha256() {
    using var fixture = new FileFixture();
    File.WriteAllText(fixture.File, "abc");
    Assert.Equal(AbcDigest, await SolverFileIdentity.ComputeAsync(fixture.File));
  }

  [UnixFact]
  public async Task RelativeSymlinkChainUsesTheResolvedExecutable() {
    using var fixture = new FileFixture();
    File.WriteAllText(fixture.File, "abc");
    var link = Path.Combine(fixture.Directory, "link");
    var chain = Path.Combine(fixture.Directory, "chain");
    File.CreateSymbolicLink(link, Path.GetFileName(fixture.File));
    File.CreateSymbolicLink(chain, Path.GetFileName(link));
    Assert.Equal(AbcDigest, await SolverFileIdentity.ComputeAsync(chain));
  }

  [Theory]
  [InlineData(0L)]
  [InlineData(SolverFileIdentity.MaximumExecutableBytes + 1)]
  public async Task EmptyAndOversizedFilesAreRejectedBeforeHashing(long length) {
    using var fixture = new FileFixture();
    using (var file = File.OpenWrite(fixture.File)) { file.SetLength(length); }
    await Assert.ThrowsAsync<InvalidDataException>(() => SolverFileIdentity.ComputeAsync(fixture.File));
  }

  [UnixFact]
  public async Task DeviceIsRejectedBeforeOpening() {
    await Assert.ThrowsAsync<InvalidDataException>(() => SolverFileIdentity.ComputeAsync("/dev/zero"));
  }

  [UnixFact]
  public async Task FifoIsRejectedBeforeOpening() {
    using var fixture = new FileFixture();
    Assert.Equal(0, MkFifo(fixture.File, 0x180)); // owner read/write permissions
    await Assert.ThrowsAsync<InvalidDataException>(() =>
      Task.Run(() => SolverFileIdentity.ComputeAsync(fixture.File)).WaitAsync(TimeSpan.FromSeconds(2)));
  }

  [Fact]
  public async Task CallerCancellationPrecedesFileAccess() {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      SolverFileIdentity.ComputeAsync("absent-solver", cancellation.Token));
  }

  [Fact]
  public async Task NonSeekableInputIsRejected() {
    using var stream = new NonSeekableStream();
    await Assert.ThrowsAsync<InvalidDataException>(() =>
      SolverFileIdentity.ComputeAsync(stream, 16, CancellationToken.None));
  }

  [Fact]
  public async Task GrowthCannotExceedTheActualByteCeiling() {
    using var stream = new DeclaredLengthStream(new byte[17], 1);
    var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
      SolverFileIdentity.ComputeAsync(stream, 16, CancellationToken.None));
    Assert.Contains("exceeds file bounds while hashing", exception.Message);
  }

  [Theory]
  [InlineData(1L)]
  [InlineData(3L)]
  public async Task ChangedFileSizeCannotProduceAnIdentity(long declaredLength) {
    using var stream = new DeclaredLengthStream(new byte[2], declaredLength);
    var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
      SolverFileIdentity.ComputeAsync(stream, 16, CancellationToken.None));
    Assert.Contains("changed size while hashing", exception.Message);
  }

  [Fact]
  public async Task CallerCancellationReleasesAnUnfinishedRead() {
    using var stream = new UnfinishedStream();
    using var cancellation = new CancellationTokenSource();
    var identity = SolverFileIdentity.ComputeAsync(stream, 16, cancellation.Token);
    await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
    Assert.False(identity.IsCompleted);
    cancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => identity.WaitAsync(TimeSpan.FromSeconds(2)));
  }

  [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
  private static extern int MkFifo(string path, uint mode);

  private sealed class FileFixture : IDisposable {
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "b3-solver-file-" + Guid.NewGuid().ToString("N"));
    public string File => Path.Combine(Directory, "solver.bin");
    public FileFixture() => System.IO.Directory.CreateDirectory(Directory);
    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
  }

  private sealed class DeclaredLengthStream(byte[] bytes, long declaredLength) : MemoryStream(bytes) {
    public override long Length => declaredLength;
  }

  private sealed class NonSeekableStream : MemoryStream {
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
  }

  private sealed class UnfinishedStream : MemoryStream {
    public TaskCompletionSource<bool> ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override long Length => 1;
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
      ReadStarted.TrySetResult(true);
      await Task.Delay(Timeout.Infinite, cancellationToken);
      return 0;
    }
  }
}
