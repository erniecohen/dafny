using System.Text.Json;
using Microsoft.Boogie;
using VC;
using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
public class PersistentFailureReportTests {
  private const string Source = "module M { lemma Bad(x: int) requires x > 0 { assert x > 1; } }";
  private static async Task<(Program Program, Method Method)> Parse(string source = Source, string uri = "file:///before/source.dfy") {
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri(uri), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Empty(reporter.AllMessagesByLevel[ErrorLevel.Error]);
    return (parsed.Program, SymbolExtensions.GetSymbolDescendants(parsed.Program.DefaultModule).OfType<Method>().Single(m => m.Name == "Bad"));
  }

  private static StoredFailureReport Capture(Program program, Method method) {
    var diagnostic = new DafnyDiagnostic(MessageSource.Verifier, null!, method.Body!.Body[0].ReportingRange,
      ["an assertion might not hold"], ErrorLevel.Error,
      [new(method.Req[0].E.ReportingRange, "", ["related contract"])]);
    return Assert.IsType<StoredFailureReport>(PersistentFailureReports.Capture(SourceLocations.For(program), 1, [diagnostic]));
  }

  [Fact]
  public async Task RelatedLocationsRebindToCurrentSource() {
    var (before, method) = await Parse();
    var stored = Capture(before, method);
    var (after, current) = await Parse("\n\n" + Source, "file:///after/renamed.dfy");
    var diagnostic = Assert.Single(PersistentFailureReports.Restore(SourceLocations.For(after), stored)!);
    Assert.Equal(current.Body!.Body[0].ReportingRange.StartToken.line, diagnostic.Range.StartToken.line);
    Assert.Equal(current.Origin.Uri, diagnostic.Range.Uri);
    Assert.Equal("an assertion might not hold", diagnostic.Message);
    var related = Assert.Single(diagnostic.RelatedInformation);
    Assert.Equal(current.Req[0].E.ReportingRange.StartToken.line, related.Range.StartToken.line);
    Assert.Equal(current.Origin.Uri, related.Range.Uri);
    Assert.Equal(current.Req[0].E.ReportingRange, related.Range);
    Assert.Equal(current.Req[0].E.ReportingRange.PrintOriginal(), related.Range.PrintOriginal());
    Assert.Equal("related contract", related.Message);
  }

  private sealed class TaskProbe : IVerificationTask {
    public bool Accessed { get; private set; }
    public IToken ScopeToken { get { Accessed = true; throw new InvalidOperationException("native graph unavailable"); } }
    public IToken Token => throw new NotSupportedException();
    public string ScopeId => throw new NotSupportedException();
    public ManualSplit Split => throw new NotSupportedException();
    public IVerificationStatus CacheStatus => throw new NotSupportedException();
    public bool IsIdle => false;
    public IVerificationTask FromSeed(int seed) => throw new NotSupportedException();
    public void Cancel() => throw new NotSupportedException();
    public IObservable<IVerificationStatus>? TryRun() => throw new NotSupportedException();
  }

  [Fact]
  public async Task ModelRequestsBypassCaptureBeforeAccessingNativeTaskState() {
    var (program, _) = await Parse();
    program.Options.ExtractCounterexample = true;
    var task = new TaskProbe();
    var result = new VerificationRunResult(1, 0, DateTime.MinValue, SolverOutcome.Invalid, TimeSpan.Zero, 1,
      [], [], [], 1, Microsoft.Boogie.SMTLib.SolverKind.Z3, []);
    Assert.Null(PersistentFailureReports.Capture(program, task, result));
    Assert.False(task.Accessed);
    program.Options.ExtractCounterexample = false;
    Assert.Null(PersistentFailureReports.Capture(program, task, result));
    Assert.True(task.Accessed);
  }

  [Theory]
  [InlineData("report", "Counterexamples")]
  [InlineData("report", "Diagnostics")]
  [InlineData("diagnostic", "Source")]
  [InlineData("diagnostic", "Level")]
  [InlineData("diagnostic", "Range")]
  [InlineData("diagnostic", "ErrorId")]
  [InlineData("diagnostic", "MessageParts")]
  [InlineData("diagnostic", "Related")]
  [InlineData("related", "Range")]
  [InlineData("related", "ErrorId")]
  [InlineData("related", "MessageParts")]
  [InlineData("range", "Start")]
  [InlineData("range", "End")]
  [InlineData("range", "InclusiveEnd")]
  [InlineData("range", "SourceOriginal")]
  public async Task EveryFieldIsRequired(string table, string field) {
    var (program, method) = await Parse();
    var report = Capture(program, method);
    object value = table switch {
      "report" => report, "diagnostic" => report.Diagnostics[0], "related" => report.Diagnostics[0].Related[0],
      _ => report.Diagnostics[0].Range
    };
    var json = JsonSerializer.SerializeToNode(value, value.GetType())!;
    Assert.NotNull(JsonSerializer.Deserialize(json, value.GetType()));
    json.AsObject().Remove(field);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, value.GetType()));
  }

  [Fact]
  public async Task InvalidCountsKindsRangesAndMessagesMiss() {
    var (program, method) = await Parse();
    var report = Capture(program, method);
    var diagnostic = report.Diagnostics[0];
    var cases = new[] {
      report with { Counterexamples = 0 }, report with { Counterexamples = 2 },
      report with { Diagnostics = [diagnostic with { Level = (ErrorLevel)99 }] },
      report with { Diagnostics = [diagnostic with { Source = MessageSource.Parser }] },
      report with { Diagnostics = [diagnostic with { MessageParts = [] }] },
      report with { Diagnostics = [diagnostic with { MessageParts = ["at file:///before/source.dfy"] }] },
      report with { Diagnostics = [diagnostic with { Related = [diagnostic.Related[0] with {
        MessageParts = ["from file:///before/source.dfy"]
      }] }] },
      report with { Diagnostics = [diagnostic with { Range = diagnostic.Range with { SourceOriginal = "changed" } }] },
      report with { Diagnostics = [diagnostic with { Related = [diagnostic.Related[0] with {
        Range = diagnostic.Related[0].Range with { End = diagnostic.Related[0].Range.End with { Column = 999 } }
      }] }] }
    };
    foreach (var invalid in cases) { Assert.Null(PersistentFailureReports.Restore(SourceLocations.For(program), invalid)); }
  }
}
