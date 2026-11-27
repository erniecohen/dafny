using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using VC;
using Token = Microsoft.Dafny.Token;

namespace DafnyCore.Test;

public class PersistentModuleOutcomeTests {
  [Theory]
  [InlineData(SolverOutcome.Valid, 0, 0, 0, true)]
  [InlineData(SolverOutcome.Valid, 1, 10, 0, true)]
  [InlineData(SolverOutcome.OutOfResource, 0, 1, 0, true)]
  [InlineData(SolverOutcome.OutOfResource, 1, 1, 0, false)]
  [InlineData(SolverOutcome.OutOfResource, 0, 0, 0, false)]
  [InlineData(SolverOutcome.Invalid, 0, 1, 0, false)]
  [InlineData(SolverOutcome.Invalid, 0, 1, 1, true)]
  [InlineData(SolverOutcome.Invalid, 0, 1, 3, true)]
  [InlineData(SolverOutcome.Invalid, 1, 1, 1, false)]
  [InlineData(SolverOutcome.Invalid, 0, 0, 1, false)]
  [InlineData(SolverOutcome.Invalid, 0, 1, -1, false)]
  [InlineData(SolverOutcome.TimeOut, 0, 1, 0, false)]
  [InlineData(SolverOutcome.OutOfMemory, 0, 1, 0, false)]
  [InlineData(SolverOutcome.Undetermined, 0, 1, 0, false)]
  [InlineData(SolverOutcome.Bounded, 0, 1, 0, false)]
  [InlineData((SolverOutcome)(-1), 0, 1, 0, false)]
  [InlineData(SolverOutcome.OutOfResource, 0, 1, 1, false)]
  [InlineData(SolverOutcome.Valid, 0, 1, 1, false)]
  public void OnlySupportedOutcomesAndLimitsReplay(SolverOutcome outcome, uint time, uint resource, int counterexamples, bool expected) {
    Assert.Equal(expected, PersistentModuleCache.ReplayableOutcome(outcome, time, resource, counterexamples, true));
  }

  [Fact]
  public void CounterexampleReplayRequiresItsOptIn() {
    Assert.False(PersistentModuleCache.ReplayableOutcome(SolverOutcome.Invalid, 0, 1000, 1));
    Assert.True(PersistentModuleCache.ReplayableOutcome(SolverOutcome.Invalid, 0, 1000, 1, true));
  }

  private static PersistentModuleCache.PersistedTask Task(SolverOutcome outcome, uint time, out Method method, StoredFailureReport? failure = null, IReadOnlyList<DafnyDiagnostic>? diagnostics = null) {
    var token = new Token(12, 3) { Uri = new Uri("file:///current.dfy"), val = "Limited" };
    var origin = new SourceOrigin(token, token);
    method = new Method(origin, new Name(origin, "Limited"), null, true, true, [], [], [], [],
      new Specification<FrameExpression>(), new Specification<Expression>([], null), [],
      new Specification<FrameExpression>([], null), new BlockStmt(origin, []), Token.NoToken);
    var symbol = new PersistentModuleCache.SymbolEntry("Method", "Mixed.Limited", "declaration:Mixed.Limited", 0, 3, 0, 3);
    var entry = new PersistentModuleCache.TaskEntry(symbol, false, "Mixed.Limited (correctness)", 0, 7, true,
      1, 1, 42, 1, 0, new string('a', 64), Outcome: outcome, TimeLimit: time, ResourceLimit: 1000, Failure: failure);
    return new PersistentModuleCache.PersistedTask(method, new CanVerifyOrigin(method), entry, [], failureDiagnostics: diagnostics);
  }

  [Fact]
  public void ResourceDiagnosticUsesCurrentOriginWithoutABoogieSplit() {
    var task = Task(SolverOutcome.OutOfResource, 0, out var method);
    var options = DafnyOptions.CreateUsingOldParser(TextWriter.Null);
    var result = Assert.IsType<Completed>(task.CacheStatus).Result;
    Assert.Equal(SolverOutcome.OutOfResource, result.Outcome);
    Assert.Throws<InvalidOperationException>(() => task.Split);
    var diagnostic = Assert.Single(Compilation.GetDiagnosticsFromResult(options, method.Origin.Uri, method, task, result));
    Assert.Contains("out of resource", string.Join("", diagnostic.MessageParts));
    Assert.Equal(12, diagnostic.Range.StartToken.line);
    Assert.Equal(3, diagnostic.Range.StartToken.col);
    Assert.Equal(method.Origin.Uri, diagnostic.Range.StartToken.Uri);
  }

  [Fact]
  public void TerminalFailureUsesStoredCountAndCurrentDiagnosticsWithoutABoogieSplit() {
    var packet = new StoredFailureReport(2, []);
    var range = new TokenRange(new Token(7, 2) { Uri = new Uri("file:///current.dfy"), val = "assert" }, null);
    DafnyDiagnostic[] diagnostics = [
      new(MessageSource.Verifier, "", range, ["first failure"], ErrorLevel.Error, []),
      new(MessageSource.Verifier, "", range, ["second failure"], ErrorLevel.Error,
        [new(range, "", ["related contract"])])
    ];
    var task = Task(SolverOutcome.Invalid, 0, out var method, packet, diagnostics);
    var result = Assert.IsType<Completed>(task.CacheStatus).Result;
    Assert.Throws<InvalidOperationException>(() => task.Split);
    Assert.Empty(result.CounterExamples);
    Assert.Equal(2, VerificationTaskFacts.Counterexamples(task, result));
    Assert.Equal(diagnostics, Compilation.GetDiagnosticsFromResult(DafnyOptions.CreateUsingOldParser(TextWriter.Null),
      method.Origin.Uri, method, task, result));
  }

  [Fact]
  public void StoredTaskRetainsItsEffectiveTimeLimit() {
    var task = Task(SolverOutcome.Valid, 17, out _);
    var options = DafnyOptions.CreateUsingOldParser(TextWriter.Null);
    Assert.Equal(17U, VerificationTaskFacts.TimeLimit(task, options));
  }
}
