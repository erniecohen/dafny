using Microsoft.Dafny;
using Microsoft.Dafny.LanguageServer.Workspace;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DafnyCore.Test;

public class VerificationContractsTest {
  private static VerificationResult Result(VerificationOutcome outcome, bool complete,
    int errors = 0, IReadOnlyList<VerificationAssertion>? assertions = null) =>
    new(outcome, Array.Empty<DafnyDiagnostic>(), assertions ?? Array.Empty<VerificationAssertion>(), complete,
      DateTime.UnixEpoch, TimeSpan.Zero, null, errors);

  [Fact]
  public void CompletedEmptyTraversalCanVerifyWithoutInventingMetrics() {
    var result = Result(VerificationOutcome.Verified, true);
    Assert.True(result.IsVerified);
    Assert.Null(result.ResourceCount);
    Assert.Null(result.BoogieResult);
  }

  [Fact]
  public void MissingTraversalCompletionCannotVerify() {
    var result = Result(VerificationOutcome.Verified, false);
    Assert.Equal(VerificationOutcome.ToolError, result.Outcome);
    Assert.False(result.IsVerified);
  }

  [Fact]
  public void FailedObligationCannotBeOverwrittenBySuccessfulCompletion() {
    var assertions = new[] { new VerificationAssertion("check-1", Token.NoToken, null,
      "assertion", VerificationOutcome.Failed) };
    var result = Result(VerificationOutcome.Verified, true, assertions: assertions);
    Assert.Equal(VerificationOutcome.ToolError, result.Outcome);
    Assert.False(result.IsVerified);
  }

  [Theory]
  [InlineData(VerificationOutcome.Failed)]
  [InlineData(VerificationOutcome.Unknown)]
  [InlineData(VerificationOutcome.TimedOut)]
  [InlineData(VerificationOutcome.OutOfResource)]
  [InlineData(VerificationOutcome.OutOfMemory)]
  [InlineData(VerificationOutcome.Cancelled)]
  [InlineData(VerificationOutcome.Unsupported)]
  [InlineData(VerificationOutcome.ToolError)]
  public void NonSuccessOutcomesRemainDistinct(VerificationOutcome outcome) {
    var result = Result(outcome, true);
    Assert.Equal(outcome, result.Outcome);
    Assert.False(result.IsVerified);
  }
  [Fact]
  public void CompletionIsWithheldUntilTheStreamEndsNormally() {
    using var source = new Subject<VerificationStatus>();
    var received = new List<VerificationStatus>();
    source.RequireCompletion().Subscribe(received.Add);
    source.OnNext(new VerificationRunning());
    source.OnNext(new VerificationCompleted(Result(VerificationOutcome.Verified, true)));
    Assert.Single(received);
    source.OnCompleted();
    Assert.IsType<VerificationCompleted>(received.Last());
  }

  [Fact]
  public void StreamErrorAfterSuccessCannotPublishSuccess() {
    using var source = new Subject<VerificationStatus>();
    var received = new List<VerificationStatus>();
    Exception? error = null;
    source.RequireCompletion().Subscribe(received.Add, e => error = e);
    source.OnNext(new VerificationCompleted(Result(VerificationOutcome.Verified, true)));
    source.OnError(new InvalidOperationException("worker crashed"));
    Assert.Empty(received);
    Assert.NotNull(error);
  }

  [Fact]
  public void MissingOrDuplicateTerminalRecordCannotPublishSuccess() {
    foreach (var events in new[] {
      Array.Empty<VerificationStatus>(),
      new VerificationStatus[] { new VerificationCompleted(Result(VerificationOutcome.Verified, true)),
        new VerificationCompleted(Result(VerificationOutcome.Verified, true)) }
    }) {
      var received = new List<VerificationStatus>();
      Exception? error = null;
      events.ToObservable().RequireCompletion().Subscribe(received.Add, e => error = e);
      Assert.Empty(received);
      Assert.NotNull(error);
    }
  }


  [Theory]
  [InlineData(ErrorLevel.Info)]
  [InlineData(ErrorLevel.Warning)]
  public void NonSuccessCannotBeHiddenByNonErrorDiagnostics(ErrorLevel level) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    var owner = new DiagnosticOwner();
    var task = new DiagnosticWorkItem(owner);
    var diagnostic = new DafnyDiagnostic(MessageSource.Verifier, "", Token.NoToken.ReportingRange,
      new[] { "backend detail" }, level, Array.Empty<DafnyRelatedInformation>());
    foreach (var outcome in new[] { VerificationOutcome.Failed, VerificationOutcome.Unknown,
      VerificationOutcome.TimedOut, VerificationOutcome.OutOfResource, VerificationOutcome.OutOfMemory,
      VerificationOutcome.Unsupported, VerificationOutcome.ToolError, VerificationOutcome.Cancelled }) {
      var result = new VerificationResult(outcome, new[] { diagnostic }, Array.Empty<VerificationAssertion>(),
        true, DateTime.UnixEpoch, TimeSpan.Zero, null, 0);
      var reporter = new BatchErrorReporter(options);
      Compilation.ReportDiagnosticsInResult(options, owner, task, result, reporter);
      Assert.Equal(1, reporter.Count(ErrorLevel.Error));
      Assert.Contains(diagnostic, reporter.AllMessages);
      var ideDiagnostics = Compilation.GetDiagnosticsFromResult(options, new Uri("file:///test.dfy"),
        owner, task, result);
      Assert.Contains(diagnostic, ideDiagnostics);
      Assert.Equal(outcome == VerificationOutcome.Cancelled ? 0 : 1,
        ideDiagnostics.Count(item => item.Level == ErrorLevel.Error));
    }
  }

  private sealed class DiagnosticOwner : Method {
    public DiagnosticOwner() : base(Token.NoToken, new Name(Token.NoToken, "M"), null, false, false,
      [], [], [], [], new Specification<FrameExpression>(), new Specification<Expression>([], null),
      [], new Specification<FrameExpression>([], null), null, Token.NoToken) { }
    public override string FullDafnyName => "M";
  }

  private sealed class DiagnosticWorkItem : IVerificationWorkItem {
    public DiagnosticWorkItem(ICanVerify owner) {
      Source = new VerificationSourceInfo(owner, Token.NoToken, Token.NoToken, VerificationUnitKind.Body,
        "M", "entire body", Array.Empty<Function>());
    }
    public VerificationIdentity Identity => new("M", "M0", 0, 0);
    public VerificationSourceInfo Source { get; }
    public VerificationStatus CacheStatus => new VerificationStale();
    public IVerificationWorkItem FromSeed(int newSeed) => this;
    public IObservable<VerificationStatus>? TryRun() => null;
    public bool IsIdle => true;
    public void Cancel() { }
  }

}
