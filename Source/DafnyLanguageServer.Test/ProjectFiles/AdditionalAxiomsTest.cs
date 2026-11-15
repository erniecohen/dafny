using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.ProjectFiles;

public class AdditionalAxiomsTest : ClientBasedLanguageServerTest {
  public AdditionalAxiomsTest(ITestOutputHelper output) : base(output) { }

  [Fact]
  public async Task ProjectOptionChangesReverifyTheSameDeclaration() {
    await SetUp(options => options.Set(CachingProjectFileOpener.ProjectFileCacheExpiry, 0));
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    var projectPath = Path.Combine(directory, DafnyProject.FileName);
    const string source = "lemma RoundTrip(a: int) requires 0 <= a < 0x1_0000_0000 ensures (a as bv32) as int == a {}";
    await File.WriteAllTextAsync(projectPath, "[options]\nadditional-axioms = false\nresource-limit = 200000\nverification-time-limit = 0");
    var document = await CreateOpenAndWaitForResolve(source, Path.Combine(directory, "roundtrip.dfy"));
    Assert.Contains(await GetLastDiagnostics(document), d => d.Message.Contains("resource"));
    foreach (var enabled in new[] { true, false }) {
      await FileTestExtensions.WriteWhenUnlocked(projectPath,
        $"[options]\nadditional-axioms = {enabled.ToString().ToLowerInvariant()}\nresource-limit = 200000\nverification-time-limit = 0");
      ApplyChange(ref document, new Range(0, 0, 0, 0), "// recheck\n");
      var diagnostics = await GetLastDiagnostics(document);
      if (enabled) {
        Assert.Empty(diagnostics);
      } else {
        Assert.Contains(diagnostics, d => d.Message.Contains("resource"));
      }
    }
    Directory.Delete(directory, true);
  }
}
