using System.Text.Json;
using DafnyB3Protocol;
using Xunit;
using Program = DafnyB3Protocol.Program;

namespace DafnyB3Protocol.Test;

public class ProtocolTests {
  private static Request CreateRequest(Statement? body = null) {
    var program = new Program(Array.Empty<string>(), Array.Empty<Function>(), Array.Empty<Axiom>(),
      new Unit("sP0", Array.Empty<Binding>(), body ?? new Check("sO0", new BooleanLiteral(true), false)));
    return new Request(Protocol.Version, "request-1", Protocol.NormalizerVersion, new string('a', 40),
      Protocol.GetProgramHash(program), "sP0", program,
      new Configuration("z3", new[] { "-in", "-smt2" }, 1000, 10000, 100000, 2, "5.1.0", new string('d', 64)),
      new[] { new SourceIdentity("sO0", "file:///program.dfy", 2, 4, "assertion") }, new string('f', 64));
  }
  private static Completion Complete(Request request) => new(Protocol.Version, request.RequestId,
    request.ProgramHash, request.UnitId, request.B3Commit, true, Outcome.Verified,
    new[] { new Attempt(0, "sO0", Outcome.Verified, null) }, null, request.WorkerFingerprint);

  [Fact]
  public void ValidRoundTripPreservesTypedProgramAndManifest() {
    var request = CreateRequest();
    var restored = JsonSerializer.Deserialize<Request>(JsonSerializer.Serialize(request, Protocol.JsonOptions), Protocol.JsonOptions)!;
    ProtocolValidation.ValidateRequest(restored);
    Assert.Equal(request.ProgramHash, Protocol.GetProgramHash(restored.Program));
    ProtocolValidation.ValidateCompletion(restored, Complete(restored));
  }
  [Theory]
  [InlineData(false, Outcome.Verified, null)]
  [InlineData(true, Outcome.Failed, null)]
  [InlineData(true, Outcome.Verified, "cleanup failed")]
  public void IncompleteFailedOrErroredUnitCannotBeVerified(bool completed, Outcome attemptOutcome, string? error) {
    var request = CreateRequest();
    var completion = Complete(request) with { TraversalCompleted = completed, Error = error,
      Attempts = new[] { new Attempt(0, "sO0", attemptOutcome, null) } };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateCompletion(request, completion));
  }
  [Fact]
  public void CompletionMustBelongToTheExactRequestProgramUnitAndBuild() {
    var request = CreateRequest();
    var completion = Complete(request);
    foreach (var altered in new[] { completion with { RequestId = "another" },
      completion with { ProgramHash = new string('b', 64) }, completion with { UnitId = "sP1" },
      completion with { B3Commit = new string('b', 40) }, completion with { Version = 0 }, completion with { WorkerFingerprint = new string('b', 64) } }) {
      Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateCompletion(request, altered));
    }
  }
  [Fact]
  public void DuplicateOutOfOrderAndUnknownAttemptsAreRejected() {
    var request = CreateRequest();
    foreach (var attempts in new[] {
      new[] { new Attempt(1, "sO0", Outcome.Verified, null) },
      new[] { new Attempt(0, "sOther", Outcome.Verified, null) },
      new[] { new Attempt(0, "sO0", Outcome.Verified, null), new Attempt(0, "sO0", Outcome.Verified, null) } }) {
      Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateCompletion(request,
        Complete(request) with { Attempts = attempts }));
    }
  }
  [Fact]
  public void AlteredProgramOrDroppedStaticCheckIsRejected() {
    var request = CreateRequest();
    var dropped = request.Program with { Unit = request.Program.Unit with { Body = new Block(Array.Empty<Statement>()) } };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(request with { Program = dropped }));
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(request with {
      Program = dropped, ProgramHash = Protocol.GetProgramHash(dropped) }));
  }
  [Fact]
  public void IllTypedAssertionCannotReachB3Construction() {
    var request = CreateRequest(new Check("sO0", new IntegerLiteral("1"), false));
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(request));
  }
  [Theory]
  [InlineData("1.5")]
  [InlineData("(assert false)")]
  public void IntegerLiteralsAreExactIntegers(string literal) {
    var request = CreateRequest(new Check("sO0", new Operation(Operator.Equal, "bool",
      new Expression[] { new IntegerLiteral(literal), new IntegerLiteral("1") }), false));
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(request));
  }
  [Fact]
  public void UnsupportedOptionsAndUnsafeNamesFailExplicitly() {
    var request = CreateRequest();
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(request with {
      Configuration = request.Configuration with { ArithmeticSolver = 6 } }));
    var program = request.Program with { Unit = request.Program.Unit with { Name = "sP0) (assert false)" } };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(request with {
      UnitId = program.Unit.Name, Program = program, ProgramHash = Protocol.GetProgramHash(program) }));
  }
  [Theory]
  [InlineData(Protocol.MaximumDepth - 1, true)]
  [InlineData(Protocol.MaximumDepth, false)]
  public void LogicalDepthBoundIsIndependentOfJsonContainerDepth(int nesting, bool valid) {
    Expression condition = new BooleanLiteral(true);
    for (var i = 0; i < nesting; i++) {
      condition = new Operation(Operator.Not, "bool", new[] { condition });
    }
    var request = CreateRequest(new Check("sO0", condition, false));
    var json = JsonSerializer.Serialize(request, Protocol.JsonOptions);
    var restored = JsonSerializer.Deserialize<Request>(json, Protocol.JsonOptions)!;
    if (valid) {
      ProtocolValidation.ValidateRequest(restored);
      Assert.Equal(request.ProgramHash, Protocol.GetProgramHash(restored.Program));
    } else {
      Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(restored));
    }
  }
  [Fact]
  public void UnknownJsonFieldsAreRejected() {
    var json = JsonSerializer.Serialize(CreateRequest(), Protocol.JsonOptions);
    json = json.Insert(1, "\"unexpected\":true,");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Request>(json, Protocol.JsonOptions));
  }
}
