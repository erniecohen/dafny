using System.Text.Json;
using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
public class PersistentIdeReportTests {
  private const string Source = "module M { lemma Good() { assert true; } }";
  private static async Task<(SourceLocations Locations, Method Method)> Parse(string text = Source, string uri = "file:///before/source.dfy") {
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(text, new Uri(uri), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Empty(reporter.AllMessagesByLevel[ErrorLevel.Error]);
    return (SourceLocations.For(parsed.Program), SymbolExtensions.GetSymbolDescendants(parsed.Program.DefaultModule)
      .OfType<Method>().Single(m => m.Name == "Good"));
  }

  private static StoredIdeTaskReport Report(SourceLocations locations, Method method) => new("Good", "Good (correctness)",
    PersistentFailureReports.CaptureRange(locations, method.EntireRange)!,
    [new(PersistentFailureReports.CaptureRange(locations, method.Body!.Body[0].EntireRange)!,
      "assert", "assertion always holds", "source details", [PersistentFailureReports.CaptureRange(locations, method.EntireRange)!], Dynamic: [])]);

  [Fact]
  public void UnconfiguredIdeLogsAreEmptyButUnsupportedLogsBypass() {
    var directory = Directory.CreateTempSubdirectory("ide-report-options");
    try {
      var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
      options.ApplyDefaultOptionsWithoutSettingsDefault();
      options.Set(CommonOptionBag.VerificationCache, directory.FullName);
      options.Set(CommonOptionBag.PersistentTranslation, true);
      options.Set(CommonOptionBag.VerificationLogFormat, null!);
      var cache = Assert.IsType<PersistentModuleCache>(PersistentModuleCache.For(options));
      Assert.Null(cache.ReplayBypass());
      options.Set(CommonOptionBag.VerificationLogFormat, ["unsupported"]);
      Assert.Equal("verification logs", cache.ReplayBypass());
    } finally { directory.Delete(true); }
  }

  [Fact]
  public async Task CompleteReportsRebindToCurrentSource() {
    var (before, method) = await Parse();
    var stored = Report(before, method);
    var (after, current) = await Parse("\n\n" + Source, "file:///after/renamed.dfy");
    var report = Assert.IsType<IdeTaskReport>(PersistentIdeReports.Restore(after, stored, 1));
    Assert.Equal(stored.Name, report.Name);
    Assert.Equal(stored.VerboseName, report.VerboseName);
    Assert.Equal(current.Origin.Uri, report.Origin.Uri);
    Assert.Equal(current.EntireRange.ToLspRange(), report.Origin.GetLspRange());
    var assertion = Assert.Single(report.Assertions);
    Assert.Equal(current.Body!.Body[0].EntireRange.ToLspRange(), assertion.Origin.GetLspRange());
    Assert.Equal(current.Origin.Uri, assertion.Origin.Uri);
    Assert.Equal("assert", assertion.Kind);
    Assert.Equal("assertion always holds", assertion.Description);
    Assert.Equal("source details", assertion.MoreInformation);
    var related = Assert.Single(assertion.RelatedOrigins);
    Assert.Equal(current.Origin.Uri, related.Uri);
    Assert.Equal(current.EntireRange.ToLspRange(), related.GetLspRange());
  }

  [Fact]
  public async Task IncompleteCorruptedAndStaleReportsMiss() {
    var (locations, method) = await Parse();
    var stored = Report(locations, method);
    Assert.Null(PersistentIdeReports.Restore(locations, null, 1));
    Assert.Null(PersistentIdeReports.Restore(locations, stored, 2));
    Assert.Null(PersistentIdeReports.Restore(locations, stored with { Assertions = [] }, 1));
    var assertion = stored.Assertions[0];
    foreach (var invalid in new[] {
      assertion with { Kind = "unknown" }, assertion with { Description = null! },
      assertion with { MoreInformation = null! },
      assertion with { Outcome = Microsoft.Boogie.SolverOutcome.Invalid },
      assertion with { CounterexampleKind = "unsupported" },
      assertion with { FailureDescription = null! },
      assertion with { PostconditionFailureDescription = null! },
      assertion with { PostconditionMoreInformation = null! },
      assertion with { Dynamic = null! },
      assertion with { Secondary = stored.Range },
      assertion with { SecondaryPosition = stored.Range.Start },
      assertion with { Action = new("unknown", "a", "b") },
      assertion with { Action = new("calc", null!, "b") },
      assertion with { Action = new("calc", "a", null!) },
      assertion with { Range = assertion.Range with { SourceOriginal = "stale source" } },
      assertion with { Range = assertion.Range with { Start = assertion.Range.Start with { Anchor = "missing" } } }
    }) {
      Assert.Null(PersistentIdeReports.Restore(locations, stored with { Assertions = [invalid] }, 1));
    }
    var (changed, _) = await Parse(Source.Replace("assert true", "assert false"));
    Assert.Null(PersistentIdeReports.Restore(changed, stored, 1));
  }

  [Fact]
  public async Task AuditComparesEveryDisplayField() {
    var (locations, method) = await Parse();
    var stored = Report(locations, method);
    Assert.True(PersistentIdeReports.Same(stored, stored));
    Assert.True(PersistentIdeReports.Same(null, null));
    Assert.False(PersistentIdeReports.Same(stored, null));
    var assertion = stored.Assertions[0];
    foreach (var changed in new[] {
      stored with { Name = "changed" }, stored with { VerboseName = "changed" },
      stored with { Range = stored.Range with { SourceOriginal = "changed" } },
      stored with { Assertions = [] },
      stored with { Assertions = [assertion with { Kind = "call" }] },
      stored with { Assertions = [assertion with { Description = "changed" }] },
      stored with { Assertions = [assertion with { MoreInformation = "changed" }] },
      stored with { Assertions = [assertion with { Related = [] }] },
      stored with { Assertions = [assertion with { Outcome = Microsoft.Boogie.SolverOutcome.Invalid }] },
      stored with { Assertions = [assertion with { Secondary = stored.Range }] },
      stored with { Assertions = [assertion with { SecondaryPosition = stored.Range.Start }] },
      stored with { Assertions = [assertion with { CounterexampleKind = "return" }] },
      stored with { Assertions = [assertion with { FailureDescription = "changed" }] },
      stored with { Assertions = [assertion with { PostconditionFailureDescription = "changed" }] },
      stored with { Assertions = [assertion with { PostconditionMoreInformation = "changed" }] },
      stored with { Assertions = [assertion with { Dynamic = [stored.Range] }] },
      stored with { Assertions = [assertion with { Action = new("calc", "left", "right") }] },
      stored with { Failure = new(1, []) }
    }) {
      Assert.False(PersistentIdeReports.Same(stored, changed));
      var symbol = new PersistentModuleCache.SymbolEntry("Method", "M.Good", "source", 0, 0, 0, 0);
      var task = new PersistentModuleCache.TaskEntry(symbol, false, "scope", 0, 0, true,
        1, 1, 42, 1, 0, "execution", IdeReport: stored);
      var manifest = new PersistentModuleCache.ModuleManifest(3, "M", "graph", "fingerprint", [task]);
      Assert.True(PersistentModuleCache.SameResults(manifest, manifest));
      Assert.False(PersistentModuleCache.SameResults(manifest, manifest with { Tasks = [task with { IdeReport = changed }] }));
    }
  }

  [Fact]
  public async Task IdeModelsRequireCompleteModelEvidenceEvenWithoutTextExtraction() {
    var (locations, method) = await Parse();
    var stored = Report(locations, method);
    var range = stored.Range;
    var failure = new StoredFailureReport(1,
      [new(MessageSource.Verifier, ErrorLevel.Error, range, null, ["assertion might not hold"], [])]);
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    Assert.False(options.ExtractCounterexample);
    Assert.NotNull(PersistentFailureReports.Restore(locations, failure, options, out _));
    Assert.Null(PersistentFailureReports.Restore(locations, failure, options, out _, requireModels: true));
    Assert.Null(PersistentIdeReports.Restore(locations, stored with { Failure = failure }, 1,
      options, Microsoft.Boogie.SolverOutcome.Invalid, 1));
    Assert.Null(PersistentIdeReports.Restore(locations, stored, 1, options, Microsoft.Boogie.SolverOutcome.Invalid, 1));
  }

  [Theory]
  [InlineData(false, "Name")]
  [InlineData(false, "VerboseName")]
  [InlineData(false, "Range")]
  [InlineData(false, "Assertions")]
  [InlineData(false, "Failure")]
  [InlineData(true, "Range")]
  [InlineData(true, "Kind")]
  [InlineData(true, "Description")]
  [InlineData(true, "MoreInformation")]
  [InlineData(true, "Related")]
  [InlineData(true, "Outcome")]
  [InlineData(true, "Secondary")]
  [InlineData(true, "SecondaryPosition")]
  [InlineData(true, "CounterexampleKind")]
  [InlineData(true, "FailureDescription")]
  [InlineData(true, "PostconditionFailureDescription")]
  [InlineData(true, "PostconditionMoreInformation")]
  [InlineData(true, "Dynamic")]
  [InlineData(true, "Action")]
  public async Task EveryReportFieldIsRequired(bool assertion, string field) {
    var (locations, method) = await Parse();
    var report = Report(locations, method);
    object value = assertion ? report.Assertions[0] : report;
    var json = JsonSerializer.SerializeToNode(value, value.GetType())!;
    Assert.NotNull(JsonSerializer.Deserialize(json, value.GetType()));
    json.AsObject().Remove(field);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, value.GetType()));
  }

  [Theory]
  [InlineData("Kind")]
  [InlineData("First")]
  [InlineData("Second")]
  public void EveryActionFieldIsRequired(string field) {
    var value = new IdeAssertionAction("calc", "left", "right");
    var json = JsonSerializer.SerializeToNode(value)!;
    Assert.NotNull(JsonSerializer.Deserialize<IdeAssertionAction>(json));
    json.AsObject().Remove(field);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IdeAssertionAction>(json));
  }


  [Fact]
  public async Task SecondaryCenterIsDistinctFromItsDisplayRange() {
    var (locations, method) = await Parse();
    var stored = Report(locations, method);
    var assertion = stored.Assertions[0] with {
      Secondary = stored.Range, SecondaryPosition = locations.ProofPosition(method.Origin.Center)
    };
    var report = Assert.IsType<IdeTaskReport>(PersistentIdeReports.Restore(locations,
      stored with { Assertions = [assertion] }, 1));
    var secondary = Assert.Single(report.Assertions).SecondaryOrigin!;
    Assert.Equal(method.EntireRange.ToLspRange(), secondary.GetLspRange());
    Assert.Equal(method.Origin.GetLspPosition(), secondary.GetLspPosition());
    Assert.NotEqual(secondary.GetLspRange().Start, secondary.GetLspPosition());
  }

}
