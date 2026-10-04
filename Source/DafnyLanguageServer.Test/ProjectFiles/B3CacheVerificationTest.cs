using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.ProjectFiles;

public class B3CacheVerificationTest : ClientBasedLanguageServerTest {
  public B3CacheVerificationTest(ITestOutputHelper output) : base(output) { }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  [InlineData(3)]
  public Task EmptyB3ProjectRejectsVerificationCaching(int level) =>
    CheckEmptyProject(level, "B3 does not support --cache-verification; use 0");

  [Fact]
  public Task EmptyB3ProjectKeepsTheDefaultWithoutCaching() =>
    CheckEmptyProject(0, OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
      ? "B3 requires --verification-time-limit" : "B3 workers currently require Unix process-group isolation");

  private async Task CheckEmptyProject(int level, string expectedDiagnostic) {
    await SetUp(options => options.Set(ProjectManager.Verification, VerifyOnMode.Never));
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    try {
      // There are no verification units. Global preflight must reject unsupported
      // caching even when no per-unit preparation or worker lookup can run.
      await File.WriteAllTextAsync(Path.Combine(directory, DafnyProject.FileName),
        "[options]\nverification-backend = \"b3\"\n" +
        $"cache-verification = {level}\n" +
        "verification-time-limit = 0\nb3-worker = \"missing-cache-worker.dll\"");
      var source = CreateTestDocument("", Path.Combine(directory, "empty.dfy"));
      await File.WriteAllTextAsync(source.Uri.ToUri().LocalPath, source.Text);
      await Projects.OpenDocument(source);
      var manager = (await Projects.GetProjectManager(new TextDocumentIdentifier(source.Uri)))!;
      Assert.Equal(level, manager.Compilation.Options.VerifySnapshots);
      var resolution = (await manager.Compilation.Resolution)!;
      Assert.True(resolution.HasErrors);
      Assert.Null(resolution.CanVerifies);
      var state = await manager.GetStateAfterResolutionAsync();
      Assert.Contains(state.GetAllDiagnostics(), diagnostic =>
        diagnostic.Diagnostic.Message.Contains(expectedDiagnostic));
      Assert.DoesNotContain(state.GetAllDiagnostics(), diagnostic =>
        diagnostic.Diagnostic.Message.Contains("worker package"));
      if (level == 0) {
        Assert.DoesNotContain(state.GetAllDiagnostics(), diagnostic =>
          diagnostic.Diagnostic.Message.Contains("--cache-verification"));
      }
    } finally {
      Directory.Delete(directory, true);
    }
  }
}
