module WorkerTests {
  import opened Std.Wrappers
  import Raw = RawAst
  import B3Library
  import SolverConfiguration
  import VerificationResults
  import Solvers
  import ExternalSolvers
  import OSProcesses

  function Program(value: bool): Raw.Program {
    Raw.Program({}, [], [], [], [], [], [
      Raw.Procedure("sUnit", [], [], [], Some(Raw.Block([
        Raw.Check(Raw.LabeledExpr("sO0", Raw.BLiteral(value)))])))])
  }

  method Fixture(scenario: string) returns (r: VerificationResults.UnitResult) {
    var configuration := SolverConfiguration.Configuration("python3",
      ["test/worker/fake-solver.py", scenario], 5000, 0, 1048576);
    r := B3Library.CheckAndVerify(Program(true), "sUnit", configuration);
  }

  @Test
  method PushFailureCannotBeProved() {
    var r := Fixture("bad-push");
    expect r.error.Some?;
    expect forall a <- r.attempts :: a.outcome.ToolError?;
  }

  @Test
  method StartupFailureCannotComplete() {
    var r := Fixture("bad-option");
    expect !r.complete && r.error.Some?;
  }

  @Test
  method InvalidAnswerCannotBeProved() {
    var r := Fixture("invalid-answer");
    expect r.error.Some?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.ToolError?;
  }

  @Test
  method QueryEofCannotBeProved() {
    var r := Fixture("eof-query");
    expect r.error.Some?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.ToolError?;
  }

  @Test
  method SilentSolverExceedsDeadline() {
    var c := SolverConfiguration.Configuration("python3", ["test/worker/fake-solver.py", "silent"], 100, 0, 1048576);
    var r := B3Library.CheckAndVerify(Program(true), "sUnit", c);
    expect !r.complete && r.error.Some?;
  }

  @Test
  method OversizedResponseIsError() {
    var r := Fixture("oversized");
    expect !r.complete && r.error.Some?;
  }

  @Test
  method ZeroDeadlineIsRejected() {
    var c := SolverConfiguration.Default.(timeoutMilliseconds := 0);
    var r := B3Library.CheckAndVerify(Program(true), "sUnit", c);
    expect !r.complete && r.error.Some?;
  }

  method GetModel(scenario: string) returns (r: Result<string, string>) {
    var c := SolverConfiguration.Configuration("python3", ["test/worker/fake-solver.py", scenario], 5000, 0, 1048576);
    var started := ExternalSolvers.StartConfiguredSolver(c, false);
    expect started.Success?;
    r := ExternalSolvers.Send(started.value, "(get-model)", false);
    var closed := started.value.Dispose();
    expect closed.Success?;
  }

  @Test
  method ModelFramingIgnoresQuotedParentheses() {
    var r := GetModel("unsat");
    expect r.Success?;
    expect r.value == "(model (define-fun |x(y)| () String \"(a) \"\"b\"\"\"))";
  }

  @Test
  method TruncatedModelIsError() {
    var r := GetModel("truncated-model");
    expect r.Failure?;
  }

  @Test
  method RealZ3ProvesTrueAndRefutesFalse() {
    var proved := B3Library.CheckAndVerify(Program(true), "sUnit", SolverConfiguration.Default);
    var failed := B3Library.CheckAndVerify(Program(false), "sUnit", SolverConfiguration.Default);
    expect proved.complete && proved.error.None? && |proved.attempts| == 1 && proved.attempts[0].outcome.Proved?;
    expect failed.complete && failed.error.None? && |failed.attempts| == 1 && failed.attempts[0].outcome.Unproved?;
  }

  @Test
  method FailedAssertionCannotBeProved() {
    var r := Fixture("bad-assert");
    expect r.error.Some?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.ToolError?;
  }

  @Test
  method RestoreFailureOverridesUnsat() {
    var r := Fixture("bad-pop");
    expect r.error.Some?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.ToolError?;
  }

  @Test
  method ReasonQueryErrorCannotBeInconclusive() {
    var r := Fixture("unknown-error");
    expect r.error.Some?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.ToolError?;
  }

  @Test
  method UnknownIsInconclusive() {
    var r := Fixture("unknown");
    expect r.complete && r.error.None?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.Inconclusive?;
  }

  @Test
  method SatIsUnproved() {
    var r := Fixture("sat");
    expect r.complete && r.error.None?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.Unproved?;
  }

  @Test
  method UnreadErrorCannotBeProved() {
    var r := Fixture("unread-error");
    expect r.error.Some?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.ToolError?;
  }

  @Test
  method CompletedProofPreservesIdentity() {
    var r := Fixture("unsat");
    expect r.complete && r.error.None?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.Proved?;
    expect r.attempts[0].sequenceNumber == 0 && r.attempts[0].obligationId == "sO0";
  }

  @Test
  method StderrFloodCannotDeadlock() {
    var r := Fixture("stderr-flood");
    expect r.complete && r.error.None?;
    expect |r.attempts| == 1 && r.attempts[0].outcome.Proved?;
  }

  @Test
  method ImmediateEofIsError() {
    var r := Fixture("immediate-eof");
    expect !r.complete && r.error.Some?;
  }

  @Test
  method UnknownUnitCannotComplete() {
    var r := B3Library.CheckAndVerify(Program(true), "sMissing", SolverConfiguration.Default);
    expect !r.complete && r.error.Some?;
  }

  @Test
  method DomainInputIsUnsupported() {
    var p := Program(true).(signatureTypes := {"sT"});
    var r := B3Library.CheckAndVerify(p, "sUnit", SolverConfiguration.Default);
    expect !r.complete && r.error.Some?;
  }
}
