using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test.Verification;

public class VerificationReportingTests {
  private static readonly DafnyOptions Options = new(TextReader.Null, TextWriter.Null, TextWriter.Null);
  private static readonly Work Task = new();

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void MaterializedDiagnosticsRetainIdentityAndTakePrecedence(bool error) {
    var diagnostic = new DafnyDiagnostic(MessageSource.Verifier, "", Token.NoToken.ReportingRange,
      ["backend diagnostic"], error ? ErrorLevel.Error : ErrorLevel.Warning, []);
    var payload = new LazyDiagnostics();
    var reporter = new BatchErrorReporter(Options);
    new VerificationWorkItemResult(Task, new(VerificationOutcome.Failed, [diagnostic], Payload: payload))
      .ReportDiagnostics(Options, "scope", reporter);
    Assert.Same(diagnostic, reporter.AllMessages[0]);
    Assert.Equal(0, payload.Calls);
    Assert.Equal(1, reporter.ErrorCount);
    Assert.Equal(error ? 1 : 2, reporter.AllMessages.Count);
  }

  [Fact]
  public void LazyDiagnosticsReceiveOriginalWorkAndReporter() {
    var payload = new LazyDiagnostics();
    var result = new VerificationWorkResult(VerificationOutcome.Verified, Payload: payload);
    var reporter = new BatchErrorReporter(Options);
    new VerificationWorkItemResult(Task, result).ReportDiagnostics(Options, "scope", reporter);
    Assert.Equal(1, payload.Calls);
    Assert.Same(Task, payload.Work);
    Assert.Same(Options, payload.Options);
    Assert.Same(reporter, payload.Reporter);
    Assert.Equal("scope", payload.Name);
    Assert.Empty(reporter.AllMessages);
  }

  [Fact]
  public void LazyReportingCannotImplicitlyHideNonSuccess() {
    foreach (var outcome in Enum.GetValues<VerificationOutcome>().Where(o => o != VerificationOutcome.Verified)) {
      var reporter = new BatchErrorReporter(Options);
      var payload = new LazyDiagnostics();
      Assert.False(((IVerificationDiagnosticReporting)payload).UsesLegacyDiagnosticPolicy);
      new VerificationWorkItemResult(Task, new(outcome, Payload: payload)).ReportDiagnostics(Options, "scope", reporter);
      Assert.Equal(1, payload.Calls);
      Assert.Equal(1, reporter.ErrorCount);
    }
  }

  [Fact]
  public void DiagnosticExceptionsRetainIdentity() {
    var payload = new LazyDiagnostics { Error = new InvalidOperationException("diagnostic materialization failed") };
    var pair = new VerificationWorkItemResult(Task, new(VerificationOutcome.ToolError, Payload: payload));
    Assert.Same(payload.Error, Assert.Throws<InvalidOperationException>(() =>
      pair.ReportDiagnostics(Options, "scope", new BatchErrorReporter(Options))));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void MissingErrorDiagnosticsCannotHideNonSuccess(bool materializedEmpty) {
    foreach (var outcome in Enum.GetValues<VerificationOutcome>().Append((VerificationOutcome)12345)) {
      var reporter = new BatchErrorReporter(Options);
      // An earlier part's error must not suppress this part's fallback diagnostic.
      reporter.Error(MessageSource.Verifier, Token.NoToken, "earlier error");
      var result = new VerificationWorkResult(outcome, materializedEmpty ? [] : null);
      new VerificationWorkItemResult(Task, result).ReportDiagnostics(Options, "scope", reporter);
      Assert.Equal(outcome == VerificationOutcome.Verified ? 1 : 2, reporter.ErrorCount);
      Assert.Null(result.Metrics);
    }
  }

  private sealed class LazyDiagnostics : IVerificationPayload, IVerificationDiagnosticReporting {
    public int Calls;
    public IVerificationWork? Work;
    public DafnyOptions? Options;
    public ErrorReporter? Reporter;
    public string? Name;
    public Exception? Error;
    public void ReportDiagnostics(IVerificationWork work, DafnyOptions options, string name, ErrorReporter reporter) {
      Calls++;
      Work = work;
      Options = options;
      Reporter = reporter;
      Name = name;
      if (Error != null) { throw Error; }
    }
  }

  private sealed class Work : IVerificationWork {
    public VerificationSource Source => new(new("scope", 0), Token.NoToken, Token.NoToken);
    public VerificationWorkStatus CacheStatus => new VerificationWorkStale();
    public bool IsIdle => true;
    public IVerificationWork FromSeed(int seed) => this;
    public IObservable<VerificationWorkStatus>? TryRun() => null;
    public void Cancel() { }
  }
}
