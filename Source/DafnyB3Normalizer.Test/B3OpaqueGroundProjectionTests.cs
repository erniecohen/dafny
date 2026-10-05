// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Collections.Immutable;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

public class B3OpaqueGroundProjectionTests {
  [Fact]
  public void WholeOriginalIrrationalContextHasSingletonExtension() {
    var before = PhaseA(Irrational()); var result = Project(before);
    Assert.True(result.Evidence[0].Applied);
    Assert.Empty(result.Requests[0].Program.Types); Assert.Empty(result.Requests[0].Program.Functions);
    Assert.Equal(new[] { "sx", "sy", "si" }, result.Requests[0].Program.Unit.Variables.Select(binding => binding.Name));
    Assert.Equal(2, Checks(result.Requests[0].Program.Unit.Body).Count());
    Assert.Equal(4, before.Requests[0].Program.Types.Count); Assert.Equal(5, before.Requests[0].Program.Functions.Count);
    // No solver model is asserted here. These controls exercise the universal
    // singleton extension's complete syntactic premises and retained numeric trace.
  }

  [Theory]
  [InlineData("equality")]
  [InlineData("predicate")]
  [InlineData("conjunction")]
  public void PositiveOpaqueEqualityAndPredicateConjunctionsAreEligible(string shape) {
    Ir.Expression premise = shape switch {
      "equality" => Eq(O("so"), O("sp")), "predicate" => P(O("so")),
      _ => And(P(O("so")), And(Eq(O("so"), O("sp")), Eq(X(), Q("2"))))
    };
    var result = Project(PhaseA(Program(new Ir.Assume(premise), C(false), new Ir.Return())));
    Assert.True(result.Evidence[0].Applied);
    var assumption = Assert.IsType<Ir.Assume>(Assert.IsType<Ir.Block>(result.Requests[0].Program.Unit.Body).Statements[0]);
    if (shape == "conjunction") {
      var and = Assert.IsType<Ir.Operation>(assumption.Condition);
      Assert.True(Assert.IsType<Ir.BooleanLiteral>(and.Arguments[0]).Value);
      var nested = Assert.IsType<Ir.Operation>(and.Arguments[1]);
      Assert.True(Assert.IsType<Ir.BooleanLiteral>(nested.Arguments[0]).Value); Same(Eq(X(), Q("2")), nested.Arguments[1]);
    } else { Assert.True(Assert.IsType<Ir.BooleanLiteral>(assumption.Condition).Value); }
  }

  [Theory]
  [InlineData("truechain")]
  [InlineData("falsechain")]
  public void RemovedBooleanCopiesAreDefinitelyAssignedAndUnused(string shape) {
    var original = Program(new Ir.Assign("sb", new Ir.BooleanLiteral(shape == "truechain")),
      new Ir.Block(new Ir.Statement[] { new Ir.Assign("sc", new Ir.Variable("sb", "bool")),
        new Ir.Assign("sc", new Ir.Variable("sc", "bool")) }), C(false), new Ir.Return());
    var result = Project(PhaseA(original)); Assert.True(result.Evidence[0].Applied);
    var body = Assert.IsType<Ir.Block>(result.Requests[0].Program.Unit.Body);
    Assert.Empty(Assert.IsType<Ir.Block>(body.Statements[0]).Statements);
    Assert.All(Assert.IsType<Ir.Block>(body.Statements[1]).Statements, slot => Assert.Empty(Assert.IsType<Ir.Block>(slot).Statements));
    // A copy before its first definition is a decline; no initialized value is guessed.
    Declined(Program(new Ir.Assign("sc", new Ir.Variable("sb", "bool")), C(false), new Ir.Return()));
  }

  [Fact]
  public void RetainedChecksLearningOriginsAndPrefixOrderRemainExact() {
    var first = C(true, "sFirst", false); var second = C(false, "sSecond", true);
    var before = PhaseA(Program(new Ir.Assume(P(O("so"))), first,
      new Ir.Block(new Ir.Statement[] { new Ir.Assign("sy", X()), second }), new Ir.Return()));
    var result = Project(before);
    var checks = Checks(result.Requests[0].Program.Unit.Body).ToArray();
    Assert.Equal(new[] { ("sFirst", false), ("sSecond", true) }, checks.Select(check => (check.ObligationId, check.Learn)));
    Same(before.Requests[0].Obligations, result.Requests[0].Obligations);
    Assert.Same(Checks(before.Requests[0].Program.Unit.Body).First(), checks[0]);
    Assert.Same(Checks(before.Requests[0].Program.Unit.Body).Last(), checks[1]);
    Assert.Equal(before.Requests[0].Configuration, result.Requests[0].Configuration);
    Assert.Equal(before.Requests[0].RequestId, result.Requests[0].RequestId);
  }

  [Fact]
  public void FullRealCarrierAndIrrationalConstraintRemainUnchanged() {
    var before = PhaseA(Irrational()); var result = Project(before);
    var oldNumeric = Assumptions(before.Requests[0].Program.Unit.Body).Last().Condition;
    var newNumeric = Assumptions(result.Requests[0].Program.Unit.Body).Last().Condition;
    Assert.Same(oldNumeric, newNumeric); Same(Eq(Mul(X(), X()), Q("2")), newNumeric);
    Assert.False(Assert.IsType<Ir.BooleanLiteral>(Checks(result.Requests[0].Program.Unit.Body).Last().Condition).Value);
    Assert.Equal("real", result.Requests[0].Program.Unit.Variables[0].Type);
  }

  [Theory]
  [InlineData("numericfalse")]
  [InlineData("not")]
  [InlineData("neq")]
  [InlineData("or")]
  [InlineData("booleanread")]
  [InlineData("implication")]
  public void UnsafeRemovedAssumptionGrammarDeclinesProjection(string shape) {
    Ir.Expression premise = shape switch {
      "numericfalse" => And(new Ir.BooleanLiteral(false), P(O("so"))),
      "not" => new Ir.Operation(Ir.Operator.Not, "bool", new[] { P(O("so")) }),
      "neq" => new Ir.Operation(Ir.Operator.NotEqual, "bool", new[] { O("so"), O("sp") }),
      "or" => new Ir.Operation(Ir.Operator.Or, "bool", new[] { P(O("so")), P(O("sp")) }),
      "booleanread" => new Ir.Variable("sb", "bool"),
      _ => new Ir.Operation(Ir.Operator.Implies, "bool", new[] { P(O("so")), P(O("sp")) })
    };
    var input = PhaseA(Program(new Ir.Assume(premise), C(false), new Ir.Return()));
    if (shape != "numericfalse") { Declined(input); return; }
    var result = Project(input); Assert.True(result.Evidence[0].Applied);
    var retained = Assert.IsType<Ir.Operation>(Assumptions(result.Requests[0].Program.Unit.Body).Single().Condition);
    Assert.Equal(Ir.Operator.And, retained.Operator);
    Assert.False(Assert.IsType<Ir.BooleanLiteral>(retained.Arguments[0]).Value);
    Assert.True(Assert.IsType<Ir.BooleanLiteral>(retained.Arguments[1]).Value);
    var body = Assert.IsType<Ir.Block>(result.Requests[0].Program.Unit.Body);
    var forged = Target(result.Requests[0], body with { Statements = body.Statements.Select(statement =>
      statement is Ir.Assume ? new Ir.Assume(And(new Ir.BooleanLiteral(true), new Ir.BooleanLiteral(true))) : statement).ToImmutableArray() });
    RejectRelation(input, forged, result.Evidence[0] with { FinalProgramHash = forged.ProgramHash });
  }

  [Theory]
  [InlineData("intarg")]
  [InlineData("realarg")]
  [InlineData("boolarg")]
  [InlineData("intresult")]
  [InlineData("realresult")]
  public void NumericCoupledFunctionSignaturesDeclineProjection(string shape) {
    var original = Program(C(false), new Ir.Return());
    var parameter = shape.EndsWith("arg") ? shape[..^3] : "sOpaque";
    var result = shape.EndsWith("result") ? shape[..^6] : "sOpaque";
    var unused = new Ir.Function("sCoupled", new[] { new Ir.Binding("sParam", parameter) }, result);
    Declined(original with { Functions = original.Functions.Append(unused).ToArray() });
  }

  [Theory]
  [InlineData("activeaxiom")]
  [InlineData("unusedaxiom")]
  [InlineData("nativewordtype")]
  public void AxiomsAndUnknownTypeDependenciesDeclineProjection(string shape) {
    var original = Program(C(false), new Ir.Return());
    if (shape == "nativewordtype") {
      original = original with { Unit = original.Unit with { Variables = original.Unit.Variables.Append(new Ir.Binding("sw", "#bv3")).ToArray() } };
    } else {
      original = original with { Axioms = new[] { new Ir.Axiom(shape == "activeaxiom" ? new[] { "sPred" } : Array.Empty<string>(),
        new Ir.BooleanLiteral(true)) } };
    }
    Declined(original);
  }

  [Theory]
  [InlineData("opaqueCheckEq")]
  [InlineData("boolcopyCheckread")]
  [InlineData("numericITEremovedbool")]
  public void RetainedDependencyOnRemovedVariableDeclinesProjection(string shape) {
    Ir.Expression goal = shape switch {
      "opaqueCheckEq" => Eq(O("so"), O("sp")), "boolcopyCheckread" => new Ir.Variable("sb", "bool"),
      _ => Eq(new Ir.Operation(Ir.Operator.IfThenElse, "real", new Ir.Expression[] { new Ir.Variable("sb", "bool"), X(), Q("0") }), X())
    };
    Declined(Program(new Ir.Assign("sb", new Ir.BooleanLiteral(false)), new Ir.Check("sCheck", goal, true), new Ir.Return()));
  }

  [Theory]
  [InlineData("changedsignature")]
  [InlineData("undeclaredfunction")]
  [InlineData("assignmenttypemismatch")]
  public void OpaqueAssignmentTypeOrSymbolMutationIsRejected(string shape) {
    var source = PhaseA(Program(new Ir.Assign("so", O("sp")), C(false), new Ir.Return()));
    var program = source.Requests[0].Program;
    var value = shape == "assignmenttypemismatch" ? (Ir.Expression)new Ir.IntegerLiteral("1") :
      new Ir.Application(shape == "undeclaredfunction" ? "sMissing" : "sPred", "sOpaque", new[] { O("so") });
    var mutated = program with { Unit = program.Unit with { Body = new Ir.Block(ImmutableArray.Create<Ir.Statement>(
      new Ir.Assign("so", value is Ir.Application app ? app with { Arguments = app.Arguments.ToImmutableArray() } : value), C(false), new Ir.Return())) } };
    var request = source.Requests[0] with { Program = mutated, ProgramHash = B3RealContextPreparation.Hash(mutated) };
    var forged = source with { Requests = ImmutableArray.Create(request), Evidence = ImmutableArray.Create(source.Evidence[0] with { FinalProgramHash = request.ProgramHash }) };
    Assert.Throws<B3RealPreparationRejection>(() => Project(forged));
    if (shape == "changedsignature") {
      // A base Type/positional ResultType disagreement cannot be canonicalized
      // into a different formula by the owned-input admission path.
      var badType = new Ir.Variable("sx", "real") with { Type = "int" };
      var aliasProgram = program with { Unit = program.Unit with { Body = new Ir.Block(ImmutableArray.Create<Ir.Statement>(
        new Ir.Assign("si", badType), C(false), new Ir.Return())) } };
      var aliasRequest = source.Requests[0] with { Program = aliasProgram, ProgramHash = B3RealContextPreparation.Hash(aliasProgram) };
      Assert.Throws<B3RealPreparationRejection>(() => Project(source with {
        Requests = ImmutableArray.Create(aliasRequest), Evidence = ImmutableArray.Create(source.Evidence[0] with { FinalProgramHash = aliasRequest.ProgramHash }) }));
    }
  }

  [Theory]
  [InlineData("havoc")]
  [InlineData("choice")]
  [InlineData("conditional")]
  [InlineData("loop")]
  [InlineData("labeled")]
  [InlineData("earlyReturn")]
  public void NonStraightLineControlDeclinesProjection(string shape) {
    Ir.Statement control = shape switch {
      "havoc" => new Ir.Havoc(new[] { "sx" }), "choice" => new Ir.Choice(new[] { new Ir.Block(Array.Empty<Ir.Statement>()) }),
      "conditional" => new Ir.Conditional(new Ir.BooleanLiteral(true), new Ir.Block(Array.Empty<Ir.Statement>()), new Ir.Block(Array.Empty<Ir.Statement>())),
      "loop" => new Ir.Loop(Array.Empty<Ir.Expression>(), new Ir.Block(Array.Empty<Ir.Statement>())),
      "labeled" => new Ir.Labeled("sLabel", new Ir.Block(Array.Empty<Ir.Statement>())), _ => new Ir.Return()
    };
    Declined(Program(control, C(false), new Ir.Return()));
    // Trailing empty Blocks are allowed; an additional non-Block is not.
    var positive = Project(PhaseA(Program(C(false), new Ir.Return(), new Ir.Block(Array.Empty<Ir.Statement>()))));
    Assert.True(positive.Evidence[0].Applied);
  }

  [Theory]
  [InlineData("quantifier")]
  [InlineData("let")]
  [InlineData("wordoperation")]
  [InlineData("numericUFapp")]
  public void BindersWordsAndRetainedApplicationsDeclineProjection(string shape) {
    Ir.Expression goal = shape switch {
      "quantifier" => new Ir.Quantifier(true, new[] { new Ir.Binding("sz", "int") }, Array.Empty<IReadOnlyList<Ir.Expression>>(),
        Eq(new Ir.Variable("sz", "int"), new Ir.Variable("sz", "int"))),
      "let" => new Ir.Let(new Ir.Binding("sz", "real"), X(), Eq(new Ir.Variable("sz", "real"), X())),
      "wordoperation" => Eq(new Ir.BitvectorOperation(Ir.BitvectorOperator.BitvectorToUnsignedInt, 3, 0, 0, "int",
        new Ir.Expression[] { new Ir.BitvectorLiteral("0", 3) }), new Ir.IntegerLiteral("0")),
      _ => Eq(new Ir.Application("sNumeric", "real", Array.Empty<Ir.Expression>()), X())
    };
    var program = Program(new Ir.Check("sCheck", goal, false), new Ir.Return());
    if (shape == "numericUFapp") { program = program with { Functions = program.Functions.Append(new Ir.Function("sNumeric", Array.Empty<Ir.Binding>(), "real")).ToArray() }; }
    Declined(program);
  }

  [Theory]
  [InlineData("originalhash")]
  [InlineData("outputhash")]
  [InlineData("insertedAssume")]
  [InlineData("changedCheck")]
  public void GroundCertificateIsBoundToInputAndFinalBytes(string shape) {
    var input = PhaseA(Irrational()); var result = Project(input); var target = result.Requests[0]; var evidence = result.Evidence[0];
    if (shape == "originalhash") { evidence = evidence with { InputProgramHash = new string('e', 64) }; }
    if (shape == "outputhash") { evidence = evidence with { FinalProgramHash = new string('e', 64) }; }
    if (shape is "insertedAssume" or "changedCheck") {
      var body = Assert.IsType<Ir.Block>(target.Program.Unit.Body);
      target = Target(target, body with { Statements = shape == "insertedAssume" ?
        body.Statements.Prepend(new Ir.Assume(new Ir.BooleanLiteral(false))).ToImmutableArray() :
        body.Statements.Select(statement => statement is Ir.Check check ? check with { Condition = new Ir.BooleanLiteral(true) } : statement).ToImmutableArray() });
      evidence = evidence with { FinalProgramHash = target.ProgramHash };
    }
    RejectRelation(input, target, evidence);
  }

  [Theory]
  [InlineData("below")]
  [InlineData("at")]
  [InlineData("above")]
  public void ProjectionBoundsDeclineWithoutAllocatingOversizedWitness(string position) {
    var source = PhaseA(Irrational()); var measured = Project(source);
    Assert.True(measured.Evidence[0].Applied); Assert.InRange(measured.WitnessSlots, 10, 1000);
    var exact = checked((int)measured.WitnessSlots);
    var limit = position == "below" ? exact - 1 : position == "above" ? exact + 1 : exact;
    var result = Project(source, new B3OpaqueGroundLimits(MaximumWitnessSlots: limit));
    if (position == "below") {
      Assert.False(result.Evidence[0].Applied); Assert.Contains("witness", result.Evidence[0].DeclineReason);
      Same(source.Requests[0], result.Requests[0]);
    } else { Assert.True(result.Evidence[0].Applied); Assert.Equal(exact, result.WitnessSlots); }
    // Shared work exhaustion falls back for the complete unit, including mask16.
    var contexts = Enumerable.Range(0, 16).Select(index => Context(Irrational(), "sMask" + index)).ToArray();
    var originals = contexts.Select(Request).ToArray(); var owned = B3RealContextPreparation.Prepare(contexts, originals, Bpl.Token.NoToken);
    var all = Project(owned, new B3OpaqueGroundLimits(MaximumWork: 1));
    Assert.Equal(16, all.Requests.Length); Assert.All(all.Evidence, row => Assert.False(row.Applied));
    for (var i = 0; i < 16; i++) { Same(owned.Requests[i], all.Requests[i]); }
  }

  [Theory]
  [InlineData("nonemptydefinitionmask")]
  [InlineData("differentcontextcertificatereuse")]
  public void G3DefinitionContextsCannotBorrowSingletonCertificate(string shape) {
    if (shape == "nonemptydefinitionmask") {
      var origin = new B3DefinitionOrigin("sDef", "sOwner", 0, new string('a', 64), "bool", 3, 4);
      var context = Context(Irrational()) with { Definitions = new[] { origin } };
      var owned = B3RealContextPreparation.Prepare(new[] { context }, new[] { Request(context) }, Bpl.Token.NoToken);
      Assert.Same(origin, owned.Definitions[0][0]); Declined(owned);
    } else {
      var input = PhaseA(Irrational()); var result = Project(input);
      Assert.Throws<B3RealPreparationRejection>(() => B3OpaqueGroundProjectionRelation.Validate(input.Requests[0], result.Requests[0],
        result.Evidence[0], "sAnotherMask", ImmutableArray<B3DefinitionOrigin>.Empty, Bpl.Token.NoToken));
    }
  }

  private static Ir.Program Irrational() => Program(
    new Ir.Assign("so", O("sp")), new Ir.Assign("st", new Ir.Application("sNull", "sOther", Array.Empty<Ir.Expression>())),
    new Ir.Assign("sb", new Ir.BooleanLiteral(false)), new Ir.Assign("sc", new Ir.Variable("sb", "bool")),
    new Ir.Assign("sy", X()), new Ir.Assume(P(O("so"))),
    new Ir.Assume(new Ir.Application("sPredOther", "bool", new[] { new Ir.Variable("st", "sOther") })),
    C(true, "sPrefix", true), new Ir.Assume(Eq(Mul(X(), X()), Q("2"))), C(false), new Ir.Return());
  private static Ir.Program Program(params Ir.Statement[] statements) => new(new[] { "sOpaque", "sOther", "sThird", "sFourth" },
    new[] { new Ir.Function("sPred", new[] { new Ir.Binding("sArg", "sOpaque") }, "bool"),
      new Ir.Function("sPredOther", new[] { new Ir.Binding("sArg", "sOther") }, "bool"),
      new Ir.Function("sNull", Array.Empty<Ir.Binding>(), "sOther"),
      new Ir.Function("sOpaqueFun", new[] { new Ir.Binding("sArg", "sOpaque") }, "sOpaque"),
      new Ir.Function("sUnused", new[] { new Ir.Binding("sArg", "sThird") }, "sFourth") }, Array.Empty<Ir.Axiom>(),
    new Ir.Unit("sUnit", new[] { new Ir.Binding("so", "sOpaque"), new Ir.Binding("sp", "sOpaque"), new Ir.Binding("st", "sOther"),
      new Ir.Binding("sx", "real"), new Ir.Binding("sb", "bool"), new Ir.Binding("sy", "real"), new Ir.Binding("sc", "bool"), new Ir.Binding("si", "int") },
      new Ir.Block(statements)));
  private static Ir.Expression X() => new Ir.Variable("sx", "real");
  private static Ir.Expression O(string name) => new Ir.Variable(name, "sOpaque");
  private static Ir.Expression Q(string value) => new Ir.RationalLiteral(value, "1");
  private static Ir.Expression P(Ir.Expression argument) => new Ir.Application("sPred", "bool", new[] { argument });
  private static Ir.Expression Eq(Ir.Expression a, Ir.Expression b) => new Ir.Operation(Ir.Operator.Equal, "bool", new[] { a, b });
  private static Ir.Expression And(Ir.Expression a, Ir.Expression b) => new Ir.Operation(Ir.Operator.And, "bool", new[] { a, b });
  private static Ir.Expression Mul(Ir.Expression a, Ir.Expression b) => new Ir.Operation(Ir.Operator.Multiply, "real", new[] { a, b });
  private static Ir.Check C(bool value, string id = "sCheck", bool learn = true) => new(id, new Ir.BooleanLiteral(value), learn);
  private static IEnumerable<Ir.Check> Checks(Ir.Statement body) => body is Ir.Check check ? new[] { check } :
    body is Ir.Block block ? block.Statements.SelectMany(Checks) : Enumerable.Empty<Ir.Check>();
  private static IEnumerable<Ir.Assume> Assumptions(Ir.Statement body) => body is Ir.Assume assume ? new[] { assume } :
    body is Ir.Block block ? block.Statements.SelectMany(Assumptions) : Enumerable.Empty<Ir.Assume>();
  private static B3VerificationContext Context(Ir.Program program, string mask = "sMask") => new(mask, program,
    Checks(program.Unit.Body).Select(check => new Ir.SourceIdentity(check.ObligationId, "file:///OpaqueGroundTests.dfy", 4, 3, "original check")).ToArray(),
    Array.Empty<B3DefinitionOrigin>());
  private static Ir.Request Request(B3VerificationContext context) => new(Ir.Protocol.Version, "sRequest", Ir.Protocol.NormalizerVersion,
    new string('a', 40), string.Empty, context.Program.Unit.Name, context.Program,
    new Ir.Configuration("z3", new[] { "-in", "-smt2" }, 20000, 200000, 1024 * 1024, 2, "5.1.0", new string('c', 64)),
    context.Obligations, new string('d', 64));
  private static B3RealPreparedRequests PhaseA(Ir.Program program) {
    var context = Context(program); return B3RealContextPreparation.Prepare(new[] { context }, new[] { Request(context) }, Bpl.Token.NoToken);
  }
  private static B3OpaqueGroundPreparedRequests Project(B3RealPreparedRequests source, B3OpaqueGroundLimits? limits = null) =>
    B3OpaqueGroundProjection.Prepare(source, Bpl.Token.NoToken, limits);
  private static void Same<T>(T before, T after) => Assert.True(B3RealContextPreparation.JsonEqual(before, after));
  private static void Declined(Ir.Program source) => Declined(PhaseA(source));
  private static void Declined(B3RealPreparedRequests source) {
    var result = Project(source); Assert.False(result.Evidence[0].Applied); Assert.NotNull(result.Evidence[0].DeclineReason);
    Same(source.Requests[0], result.Requests[0]);
  }
  private static Ir.Request Target(Ir.Request source, Ir.Statement body) {
    var program = source.Program with { Unit = source.Program.Unit with { Body = body } };
    return source with { Program = program, ProgramHash = B3RealContextPreparation.Hash(program) };
  }
  private static void RejectRelation(B3RealPreparedRequests source, Ir.Request target, B3OpaqueGroundEvidence evidence) =>
    Assert.Throws<B3RealPreparationRejection>(() => B3OpaqueGroundProjectionRelation.Validate(source.Requests[0], target, evidence,
      source.Evidence[0].MaskId, source.Definitions[0], Bpl.Token.NoToken));
}
