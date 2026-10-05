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

public class ConsistentObligationChecksTest : ClientBasedLanguageServerTest {
  public ConsistentObligationChecksTest(ITestOutputHelper output) : base(output) { }

  [Fact]
  public async Task ProjectOptionChangesReverifyTheSameDeclaration() {
    await SetUp(options => {
      options.Set(CachingProjectFileOpener.ProjectFileCacheExpiry, 0);
      if (System.Environment.GetEnvironmentVariable("Z3") is { } solver) {
        options.Set(BoogieOptionBag.SolverPath, new FileInfo(solver));
      }
    });
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    var projectPath = Path.Combine(directory, DafnyProject.FileName);
    const string source = "datatype T = T(i:int) ghost predicate F(t:T) decreases 1 { G(t) } ghost predicate G(t:T) decreases 0 { true || F(t) } type S = t:T | F(t) witness T(0) ghost function Example(i:int):S { var t:=T(i); t }";
    await File.WriteAllTextAsync(projectPath, "[options]\nconsistent-obligation-checks = false\nresource-limit = 16000000\nverification-time-limit = 0");
    var document = await CreateOpenAndWaitForResolve(source, Path.Combine(directory, "subset.dfy"));
    Assert.Contains(await GetLastDiagnostics(document), d => d.Message.Contains("subset"));
    foreach (var enabled in new[] { true, false }) {
      await FileTestExtensions.WriteWhenUnlocked(projectPath,
        $"[options]\nconsistent-obligation-checks = {enabled.ToString().ToLowerInvariant()}\nresource-limit = 16000000\nverification-time-limit = 0");
      ApplyChange(ref document, new Range(0, 0, 0, 0), "// recheck\n");
      var diagnostics = await GetLastDiagnostics(document);
      if (enabled) {
        Assert.Empty(diagnostics);
      } else {
        Assert.Contains(diagnostics, d => d.Message.Contains("subset"));
      }
    }
    Directory.Delete(directory, true);
  }
}
