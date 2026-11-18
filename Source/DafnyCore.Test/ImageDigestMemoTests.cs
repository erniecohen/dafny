using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Dafny.Caching;
using Xunit;

namespace Microsoft.Dafny.Test;

/// <summary>
/// The on-disk memo of execution-image digests (https://github.com/erniecohen/dafny/issues/261),
/// and the relocation that keeps the installation directories out of a task's key.
/// </summary>
public class ImageDigestMemoTests : IDisposable {
  private readonly string directory = Path.Combine(Path.GetTempPath(), "dafny-image-memo-" + Guid.NewGuid().ToString("N"));
  private string Cache => Path.Combine(directory, "cache");
  private static readonly FileStamp Stamp = new(5, 1_700_000_000, 1, 1_700_000_001, 2, 3, 4);

  public ImageDigestMemoTests() { Directory.CreateDirectory(directory); }

  public void Dispose() {
    if (Directory.Exists(directory)) { Directory.Delete(directory, recursive: true); }
  }

  private string Image(string name, string text) {
    var path = Path.Combine(directory, name);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, text);
    return path;
  }

  private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

  [Fact]
  public void AnUnchangedImageIsHashedOnceAcrossProcesses() {
    var path = Image("lib.so", "bytes");
    var first = new ImageDigestMemo(Cache);
    Assert.Equal(Sha(path), first.Digest(path, _ => Stamp));
    Assert.Equal((1, 0), (first.Hashed, first.Remembered));
    var second = new ImageDigestMemo(Cache);
    Assert.Equal(Sha(path), second.Digest(path, _ => Stamp));
    Assert.Equal((0, 1), (second.Hashed, second.Remembered));
  }

  [Fact]
  public void EachStampFieldIsPartOfTheKey() {
    var path = Image("lib.so", "bytes");
    new ImageDigestMemo(Cache).Digest(path, _ => Stamp);
    var variants = new[] {
      Stamp with { Length = 6 }, Stamp with { ModifiedSeconds = Stamp.ModifiedSeconds + 1 },
      Stamp with { ModifiedNanoseconds = 7 }, Stamp with { ChangedSeconds = Stamp.ChangedSeconds + 1 },
      Stamp with { ChangedNanoseconds = 8 }, Stamp with { Device = 9 }, Stamp with { Inode = 10 }
    };
    foreach (var variant in variants) {
      var memo = new ImageDigestMemo(Cache);
      Assert.Equal(Sha(path), memo.Digest(path, _ => variant));
      Assert.Equal((1, 0), (memo.Hashed, memo.Remembered));
    }
    // The absolute path is part of the key too: the same bytes and stamp elsewhere are hashed.
    var copy = Image("other/lib.so", "bytes");
    var moved = new ImageDigestMemo(Cache);
    moved.Digest(copy, _ => Stamp);
    Assert.Equal((1, 0), (moved.Hashed, moved.Remembered));
  }

  [Fact]
  public void AnImageThatChangesWhileItIsHashedIsNotRemembered() {
    var path = Image("lib.so", "bytes");
    var calls = 0;
    var memo = new ImageDigestMemo(Cache);
    memo.Digest(path, _ => Stamp with { Inode = (ulong)++calls });
    var next = new ImageDigestMemo(Cache);
    next.Digest(path, _ => Stamp with { Inode = 1 });
    Assert.Equal(1, next.Hashed);
  }

  [Fact]
  public void RealFileChangesAreSeen() {
    var path = Image("lib.so", "bytes");
    if (FileStamp.Read(path) == null) { return; } // no known stat layout: the memo is off
    var stamp = FileStamp.Read(path)!;
    var info = new FileInfo(path);
    Assert.Equal(info.Length, stamp.Length);
    Assert.Equal(new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds(), stamp.ModifiedSeconds);
    Assert.Equal(Sha(path), new ImageDigestMemo(Cache).Digest(path));
    // Same size, new bytes, the old modification time restored: the status-change time moves.
    var modified = File.GetLastWriteTimeUtc(path);
    Thread.Sleep(20);
    File.WriteAllText(path, "BYTES");
    File.SetLastWriteTimeUtc(path, modified);
    var memo = new ImageDigestMemo(Cache);
    Assert.Equal(Sha(path), memo.Digest(path));
    Assert.Equal(1, memo.Hashed);
    // Replaced by another file (a new inode) with the same size and modification time.
    var replacement = Image("replacement", "Bytes");
    File.SetLastWriteTimeUtc(replacement, modified);
    File.Move(replacement, path, overwrite: true);
    var replaced = new ImageDigestMemo(Cache);
    Assert.Equal(Sha(path), replaced.Digest(path));
    Assert.Equal(1, replaced.Hashed);
    var again = new ImageDigestMemo(Cache);
    Assert.Equal(Sha(path), again.Digest(path));
    Assert.Equal((0, 1), (again.Hashed, again.Remembered));
  }

  [Fact]
  public void ImagesNamedThroughSymbolicLinksAreRemembered() {
    var target = Image("lib.so.1.2", "bytes");
    if (FileStamp.Read(target) == null) { return; } // no known stat layout: the memo is off
    var link = Path.Combine(Path.GetDirectoryName(target)!, "lib.so.1");
    File.CreateSymbolicLink(link, target);
    Assert.Equal(FileStamp.Read(target), FileStamp.Read(link));
    Assert.Equal(Sha(target), new ImageDigestMemo(Cache).Digest(link));
    var again = new ImageDigestMemo(Cache);
    Assert.Equal(Sha(target), again.Digest(link));
    Assert.Equal((0, 1), (again.Hashed, again.Remembered));
    // Pointed at other bytes: another inode, so another key.
    var other = Image("lib.so.1.3", "BYTES");
    File.Delete(link);
    File.CreateSymbolicLink(link, other);
    var repointed = new ImageDigestMemo(Cache);
    Assert.Equal(Sha(other), repointed.Digest(link));
    Assert.Equal((1, 0), (repointed.Hashed, repointed.Remembered));
  }

  [Fact]
  public void CorruptEntriesAreRecomputed() {
    var path = Image("lib.so", "bytes");
    var key = ImageDigestMemo.Key(path, Stamp);
    var root = Path.Combine(Cache, "v1");
    var index = Path.Combine(root, "image-digests", key[..2], key, "data");

    void Expect(string what) {
      var memo = new ImageDigestMemo(Cache);
      Assert.True(Sha(path) == memo.Digest(path, _ => Stamp), what);
      Assert.True(memo.Hashed == 1, what);
    }

    // An unreadable index entry.
    new ImageDigestMemo(Cache).Digest(path, _ => Stamp);
    File.WriteAllText(index, "{not json");
    Expect("index entry");
    // A blob whose bytes no longer match their digest.
    Directory.Delete(Path.Combine(Cache), recursive: true);
    new ImageDigestMemo(Cache).Digest(path, _ => Stamp);
    foreach (var blob in Directory.GetFiles(Path.Combine(root, "blobs"), "data", SearchOption.AllDirectories)) {
      File.WriteAllText(blob, "{\"Schema\":1}");
    }
    Expect("blob");
    // Well-formed records under the right key that name another path, another stamp, an
    // invalid digest, another schema, or are not JSON.
    foreach (var payload in new[] {
               Record(path + "x", Stamp, Sha(path)), Record(path, Stamp with { Inode = 99 }, Sha(path)),
               Record(path, Stamp, "not-a-digest"), Record(path, Stamp, Sha(path), 2), "[1,2,3]", "null"
             }) {
      Directory.Delete(Path.Combine(Cache), recursive: true);
      Assert.Equal(CachePublication.Stored,
        new ContentAddressedCache(Cache).TryPublish(CacheTable.ImageDigest, key, Encoding.UTF8.GetBytes(payload)));
      Expect(payload);
    }
  }

  private static string Record(string path, FileStamp stamp, string digest, int schema = 1) =>
    JsonSerializer.Serialize(new { Schema = schema, Path = path, Stamp = stamp, Digest = digest });

  [Fact]
  public void WithoutACacheDirectoryNothingIsRemembered() {
    var path = Image("lib.so", "bytes");
    var memo = new ImageDigestMemo(null);
    memo.Digest(path, _ => Stamp);
    memo.Digest(path, _ => Stamp);
    Assert.Equal(2, memo.Hashed);
    Assert.False(Directory.Exists(Cache));
  }

  [Fact]
  public void ListingsAreRememberedUnderTheImageBytes() {
    var path = Image("lib.dylib", "bytes");
    if (FileStamp.Read(path) == null) { return; }
    var runs = 0;
    string Listing() { runs++; return "listing of " + File.ReadAllText(path); }
    Assert.Equal("listing of bytes", new ImageDigestMemo(Cache).Inspect(path, "-L", Listing));
    Assert.Equal("listing of bytes", new ImageDigestMemo(Cache).Inspect(path, "-L", Listing));
    Assert.Equal(1, runs);
    Assert.Equal("listing of bytes", new ImageDigestMemo(Cache).Inspect(path, "-D", Listing));
    Assert.Equal(2, runs);
    Thread.Sleep(20);
    File.WriteAllText(path, "other");
    Assert.Equal("listing of other", new ImageDigestMemo(Cache).Inspect(path, "-L", Listing));
    Assert.Equal(3, runs);
  }

  [Fact]
  public void TwoCopiesOfABuildHaveTheSameRelocatedIdentity() {
    if (OperatingSystem.IsWindows()) { return; } // execution identities are Unix-only
    (string Dafny, string Solver) Build(string at) {
      var dafny = Path.Combine(directory, at, "out", "dafny");
      var solver = Path.Combine(directory, at, "ci-z3", "bin");
      foreach (var (name, text) in new[] { ("Dafny.dll", "dafny"), ("DafnyCore.dll", "core"), ("runtimes/libx.so", "native") }) {
        Image(Path.Combine(at, "out", "dafny", name), text);
      }
      Image(Path.Combine(at, "ci-z3", "bin", "z3"), "solver");
      Image(Path.Combine(at, "ci-z3", "bin", "libz3.so"), "solver library");
      return (dafny, solver);
    }
    var a = Build("runner-1/work");
    var b = Build("runner-2/_work/checkout");
    SortedDictionary<string, string> Identity((string Dafny, string Solver) build) {
      var files = Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(build.Dafny))!, "*", SearchOption.AllDirectories)
        .Concat(Directory.GetFiles(build.Solver));
      var roots = new ImageRoots(new[] { (build.Dafny, "<dafny>"), (build.Solver, "<solver>"), ("/opt/dotnet", "<dotnet>") });
      return ExecutionImageIdentity.Relocate(ExecutionImageIdentity.Digests(files.Distinct(), new ImageDigestMemo(Cache)).Digests, roots);
    }
    var first = Identity(a);
    Assert.Equal(first, Identity(b));
    Assert.Contains("<dafny>/runtimes/libx.so", first.Keys);
    Assert.Contains("<solver>/z3", first.Keys);
    Assert.DoesNotContain(first.Keys, k => k.Contains(directory));
    // The bytes still count: a changed image under the same relocated name differs.
    File.WriteAllText(Path.Combine(b.Dafny, "DafnyCore.dll"), "changed");
    Assert.NotEqual(first, Identity(b));
  }

  [Fact]
  public void RelocationReplacesWholePathsOnly() {
    if (OperatingSystem.IsWindows()) { return; } // execution identities are Unix-only
    var roots = new ImageRoots(new[] { ("/w/r1/out/dafny", "<dafny>"), ("/w/r1/out/dafny/z3/bin", "<solver>"), ("/w/tool/dotnet", "<dotnet>") });
    Assert.Equal("<dafny>/Dafny.dll", roots.Relocate("/w/r1/out/dafny/Dafny.dll"));
    Assert.Equal("<solver>/z3", roots.Relocate("/w/r1/out/dafny/z3/bin/z3"));
    Assert.Equal("<dotnet>", roots.Relocate("/w/tool/dotnet"));
    Assert.Equal("<dotnet>:/usr/bin:<dafny>", roots.Relocate("/w/tool/dotnet:/usr/bin:/w/r1/out/dafny"));
    Assert.Equal("PROVER_PATH=<solver>/z3", roots.Relocate("PROVER_PATH=/w/r1/out/dafny/z3/bin/z3"));
    Assert.Equal("/w/r1/out/dafny2/x", roots.Relocate("/w/r1/out/dafny2/x"));
    Assert.Equal("/x/w/tool/dotnet", roots.Relocate("/x/w/tool/dotnet"));
    Assert.Equal("/usr/lib/libc.so", roots.Relocate("/usr/lib/libc.so"));
  }

  [Fact]
  public void EnvironmentManifestValuesAreRelocatedAndNamesKept() {
    if (OperatingSystem.IsWindows()) { return; } // execution identities are Unix-only
    var roots = new ImageRoots(new[] { ("/w/r1/z3", "<solver>") });
    var node = JsonNode.Parse("""{"/w/r1/z3":"a","o":["PROVER_PATH=/w/r1/z3/z3",1,true,null],"p":{"q":"/w/r1/z3/z3"}}""");
    var expected = JsonNode.Parse("""{"/w/r1/z3":"a","o":["PROVER_PATH=<solver>/z3",1,true,null],"p":{"q":"<solver>/z3"}}""");
    Assert.True(JsonNode.DeepEquals(expected, TaskExecutionEnvironment.Relocate(node, roots)));
  }
}
