using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using VC;
using Bpl = Microsoft.Boogie;
using Token = Microsoft.Dafny.Token;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
public class PersistentAssertionReportTests {
  private const string Source = "module M { lemma Good() { assert true; } }";

  private static async Task<(SourceLocations Locations, Token Token)> Parse(string text = Source, string uri = "file:///before/source.dfy") {
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(text, new Uri(uri), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Empty(reporter.AllMessagesByLevel[ErrorLevel.Error]);
    var method = SymbolExtensions.GetSymbolDescendants(parsed.Program.DefaultModule).OfType<Method>().Single(m => m.Name == "Good");
    return (SourceLocations.For(parsed.Program), method.StartToken);
  }

  [Fact]
  public async Task AnchoredReportsUseCurrentFilenameAndLine() {
    var (before, token) = await Parse();
    var stored = Assert.IsType<ReportPosition>(before.Report(token));
    var (after, current) = await Parse("\n\n" + Source, "file:///after/renamed.dfy");
    var report = Assert.IsType<VerificationAssertionReport>(after.Report(stored, "assertion always holds"));
    Assert.Equal("renamed.dfy", report.Filename);
    Assert.Equal(current.line, report.Line);
    Assert.Equal(current.col, report.Column);
    Assert.Equal("assertion always holds", report.Description);
  }

  [Theory]
  [InlineData(1, "renamed.dfy")]
  [InlineData(2, "/after/renamed.dfy")]
  [InlineData(3, "file:///after/renamed.dfy")]
  public async Task FilenameSpellingIsPreservedWhenRebound(int kind, string expected) {
    var (before, token) = await Parse();
    var stored = Assert.IsType<ReportPosition>(before.Report(token));
    var (after, _) = await Parse(Source, "file:///after/renamed.dfy");
    var report = Assert.IsType<VerificationAssertionReport>(after.Report(stored with { Filename = (ReportFilename)kind }, "description"));
    if (kind == (int)ReportFilename.Path) { expected = new Uri("file:///after/renamed.dfy").LocalPath; }
    Assert.Equal(expected, report.Filename);
  }

  [Fact]
  public async Task UnanchoredPositionsRequireCurrentFileContent() {
    var (before, token) = await Parse();
    var stored = new ReportPosition("", before.Label(token.Uri), token.line, token.col, token.pos, ReportFilename.Basename, null);
    var (after, _) = await Parse(Source, "file:///after/renamed.dfy");
    Assert.Equal("renamed.dfy", after.Report(stored, "description")?.Filename);
    Assert.Null(after.Report(stored with { File = "unknown-content" }, "description"));
  }

  [Theory]
  [InlineData(-1, 0)]
  [InlineData(0, -1)]
  [InlineData(0, int.MaxValue)]
  [InlineData(int.MaxValue, 0)]
  public async Task InvalidRelativePositionsMiss(int line, int offset) {
    var (locations, token) = await Parse();
    var stored = Assert.IsType<ReportPosition>(locations.Report(token));
    Assert.Null(locations.Report(stored with { Line = line, Offset = offset }, "description"));
  }

  [Fact]
  public async Task UnknownAnchorsAndFilenameKindsMiss() {
    var (locations, token) = await Parse();
    var stored = Assert.IsType<ReportPosition>(locations.Report(token));
    Assert.Null(locations.Report(stored with { Anchor = "missing" }, "description"));
    Assert.Null(locations.Report(stored with { Filename = (ReportFilename)99 }, "description"));
  }

  [Fact]
  public async Task LiteralFieldsAndNullDescriptionSurvive() {
    var (locations, _) = await Parse();
    var stored = new ReportPosition("", "", Bpl.Token.NoToken.line, Bpl.Token.NoToken.col, Bpl.Token.NoToken.pos,
      ReportFilename.Literal, Bpl.Token.NoToken.filename);
    var report = Assert.IsType<VerificationAssertionReport>(locations.Report(stored, null));
    Assert.Equal(Bpl.Token.NoToken.filename, report.Filename);
    Assert.Equal(Bpl.Token.NoToken.line, report.Line);
    Assert.Equal(Bpl.Token.NoToken.col, report.Column);
    Assert.Null(report.Description);
  }

  [Fact]
  public async Task PrintingPlaceholderTokensAreNotPortableReportLocations() {
    var (locations, _) = await Parse();
    Assert.Null(locations.Report(Bpl.Token.NoToken));
  }

  [Fact]
  public async Task MissingMetadataAndWrongAssertionCountsMiss() {
    var (locations, token) = await Parse();
    var stored = new StoredAssertionReport(locations.Report(token)!, "description");
    Assert.Null(PersistentAssertionReports.Restore(locations, null, 1));
    Assert.Null(PersistentAssertionReports.Restore(locations, [stored], 2));
    Assert.Empty(PersistentAssertionReports.Restore(locations, [], 0)!);
  }

  [Fact]
  public async Task SourceNamesInDescriptionsAreNotAssumedPortable() {
    var (locations, _) = await Parse();
    Assert.True(locations.PortableReportText("assertion always holds"));
    Assert.False(locations.PortableReportText("a check in source.dfy"));
  }

  [Fact]
  public async Task MissingDescriptionFieldIsRejectedButExplicitNullIsRetained() {
    var (locations, token) = await Parse();
    var stored = new StoredAssertionReport(locations.Report(token)!, null);
    var node = JsonSerializer.SerializeToNode(stored)!;
    Assert.NotNull(JsonSerializer.Deserialize<StoredAssertionReport>(node));
    node.AsObject().Remove("Description");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StoredAssertionReport>(node));
  }

  [Theory]
  [InlineData("Anchor")]
  [InlineData("File")]
  [InlineData("Line")]
  [InlineData("Column")]
  [InlineData("Offset")]
  [InlineData("Filename")]
  [InlineData("Literal")]
  public async Task MissingPositionFieldsAreRejected(string field) {
    var (locations, token) = await Parse();
    var stored = new StoredAssertionReport(locations.Report(token)!, "description");
    var node = JsonSerializer.SerializeToNode(stored)!;
    node["Position"]!.AsObject().Remove(field);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StoredAssertionReport>(node));
  }

  [Fact]
  public async Task UnavailableDescriptionDoesNotDisableCsvRecording() {
    var (locations, token) = await Parse();
    var assertion = new AssertCmd(token, Expr.True) { Description = new ThrowingDescription() };
    var result = new VerificationRunResult(1, 0, DateTime.MinValue, SolverOutcome.Valid, TimeSpan.Zero, 1,
      [], [assertion], Array.Empty<TrackedNodeComponent>(), 1, Bpl.SMTLib.SolverKind.Z3, []);
    Assert.Null(PersistentAssertionReports.Capture(locations, result));
  }

  private sealed class ThrowingDescription : Bpl.ProofObligationDescription {
    public override string SuccessDescription => throw new NotSupportedException();
    public override string FailureDescription => "failure";
    public override string ShortDescription => "check";
  }
}
