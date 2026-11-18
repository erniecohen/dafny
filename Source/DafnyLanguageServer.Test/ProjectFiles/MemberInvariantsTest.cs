using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.ProjectFiles;

/// <summary>
/// Issue 105: member invariants in the IDE, with --member-invariants set in a project file. The reserved names of
/// invariants and witnesses ("invariant#0", "witness#0") are never shown.
/// </summary>
public class MemberInvariantsTest : ClientBasedLanguageServerTest {
  public MemberInvariantsTest(ITestOutputHelper output) : base(output) { }

  private const string Source = @"datatype Pos = Pos(k: int) {
  invariant 10 / k > 0
}
method Bad(r: raw Pos) returns (p: Pos) {
  p := r;
}
method Good() returns (p: Pos) {
  p := Pos(1);
}
datatype Even = Even(n: int) {
  invariant n % 2 == 0
  witness Even(0)
}
";

  private async Task<(string ProjectPath, TextDocumentItem Document)> OpenInProject(string options) {
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    var projectPath = Path.Combine(directory, DafnyProject.FileName);
    await File.WriteAllTextAsync(projectPath, "[options]\n" + options);
    var document = await CreateOpenAndWaitForResolve(Source, Path.Combine(directory, "source.dfy"));
    return (projectPath, document);
  }

  private async Task<string> Hover(TextDocumentItem document, int line, int character) {
    var hover = await client.RequestHover(
      new HoverParams { TextDocument = document.Uri, Position = (line, character) }, CancellationToken);
    var text = hover?.Contents.MarkupContent?.Value ?? "";
    Assert.DoesNotContain("#0", text);
    return text;
  }

  [Fact]
  public async Task DiagnosticsStatusesHoversAndSymbols() {
    var (projectPath, document) = await OpenInProject("member-invariants = true\n");

    // The invariant's well-formedness, and the conversion of a raw value at its site, with the invariant as related
    var diagnostics = await GetLastDiagnostics(document);
    Assert.Equal(2, diagnostics.Length);
    var division = Assert.Single(diagnostics, d => d.Message == "possible division by zero");
    Assert.Equal(new Range(1, 15, 1, 16), division.Range);
    var failure = Assert.Single(diagnostics, d => d.Message == "invariant might not hold");
    Assert.Equal(new Range(4, 7, 4, 8), failure.Range);
    var related = Assert.Single(failure.RelatedInformation!);
    Assert.Equal("this is the invariant that might not hold", related.Message);
    Assert.Equal(new Range(1, 19, 1, 20), related.Location.Range);

    // The statuses of the gutter: the invariant's well-formedness fails, as does the method with the failing conversion
    var statuses = (await WaitUntilAllStatusAreCompleted(document)).NamedVerifiables;
    Assert.Contains(statuses, s => s.NameRange == new Range(1, 2, 1, 11) && s.Status == PublishedVerificationStatus.Error);
    Assert.Contains(statuses, s => s.NameRange == new Range(3, 7, 3, 10) && s.Status == PublishedVerificationStatus.Error);
    Assert.Contains(statuses, s => s.NameRange == new Range(6, 7, 6, 11) && s.Status == PublishedVerificationStatus.Correct);
    Assert.Contains(statuses, s => s.NameRange.Start.Line == 11 && s.Status == PublishedVerificationStatus.Correct);

    var invariant = await Hover(document, 1, 2);
    Assert.Contains("Verification performance metrics for invariant of `Pos`", invariant);
    Assert.Contains("invariant 10 / k > 0", invariant);
    Assert.Contains("This is the only assertion in invariant of `Pos`", await Hover(document, 1, 17));
    Assert.Contains("r: raw Pos", await Hover(document, 3, 11));
    var conversion = await Hover(document, 4, 7);
    Assert.Contains("invariant might not hold", conversion);
    Assert.Contains("Could not prove: `10 / k > 0`", conversion);
    Assert.Contains("invariant of 'Pos' holds", await Hover(document, 7, 7));
    Assert.Contains("witness Even(0)", await Hover(document, 11, 2));

    var symbols = (await RequestDocumentSymbol(document)).ToList();
    var pos = Assert.Single(symbols, s => s.Name == "Pos");
    Assert.Contains(pos.Children!, s => s.Name == "invariant" && s.Kind == SymbolKind.Function &&
                                          s.SelectionRange == new Range(1, 2, 1, 11));
    var even = Assert.Single(symbols, s => s.Name == "Even");
    Assert.DoesNotContain(even.Children!, s => s.Name.Contains('#'));
    Directory.Delete(Path.GetDirectoryName(projectPath)!, true);
  }

  [Fact]
  public async Task VerificationOnSave() {
    await SetUp(options => options.Set(ProjectManager.Verification, VerifyOnMode.Save));
    var (projectPath, document) = await OpenInProject("member-invariants = true\n");

    // Nothing is verified until the document is saved, and then the invariants are
    await AssertNoDiagnosticsAreComing(CancellationToken);
    await client.SaveDocumentAndWaitAsync(document, CancellationToken);
    var diagnostics = await GetLastDiagnostics(document);
    Assert.Equal(["invariant might not hold", "possible division by zero"], diagnostics.Select(d => d.Message).Order());
    var failure = Assert.Single(diagnostics, d => d.Message == "invariant might not hold");
    Assert.Equal(new Range(4, 7, 4, 8), failure.Range);
    Directory.Delete(Path.GetDirectoryName(projectPath)!, true);
  }

  [Fact]
  public async Task OptionsFromTheProjectFile() {
    await SetUp(options => options.Set(CachingProjectFileOpener.ProjectFileCacheExpiry, 0));
    var (projectPath, document) = await OpenInProject("");

    // Without the option, an invariant is not a member
    var diagnostics = await GetLastDiagnostics(document);
    Assert.Equal("rbrace expected", diagnostics.First().Message);

    await FileTestExtensions.WriteWhenUnlocked(projectPath, "[options]\nmember-invariants = true\n");
    ApplyChange(ref document, new Range(0, 0, 0, 0), "// recheck\n");
    diagnostics = await GetLastDiagnostics(document);
    Assert.Equal(["invariant might not hold", "possible division by zero"], diagnostics.Select(d => d.Message).Order());

    // The option requires --type-system-refresh; the error is reported in the project file
    await FileTestExtensions.WriteWhenUnlocked(projectPath, "[options]\nmember-invariants = true\ntype-system-refresh = false\n");
    ApplyChange(ref document, new Range(0, 0, 0, 0), "// recheck\n");
    Assert.Empty(await GetLastDiagnostics(document));
    var projectUri = DocumentUri.File(projectPath);
    while (true) {
      var projectDiagnostics = diagnosticsReceiver.History.LastOrDefault(d => d.Uri == projectUri);
      if (projectDiagnostics != null && projectDiagnostics.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) {
        var error = Assert.Single(projectDiagnostics.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("use of --member-invariants requires --type-system-refresh", error.Message);
        break;
      }
      await diagnosticsReceiver.AwaitNextNotificationAsync(CancellationToken);
    }
    Directory.Delete(Path.GetDirectoryName(projectPath)!, true);
  }
}
