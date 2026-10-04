// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Numerics;
using DafnyB3Protocol;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3UnsignedWrapperTests {
  private static string Header(int width = 3, string? axiom = null) {
    axiom ??= $"forall b:bv{width} :: {{ F(b) }} ((0 <= F(b) && F(b) < {(BigInteger.One << width)}) && F(b) == N(b))";
    return $"function {{:bvbuiltin \"bv2int\"}} N(x:bv{width}):int; function F(x:bv{width}):int; axiom ({axiom}); ";
  }
  private static (Bpl.Program Source, DafnyOptions Options) Parse(string text) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    Assert.Equal(0, Bpl.Parser.Parse(text, "B3UnsignedWrapperTests.bpl", out var source));
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    return (source, options);
  }
  private static B3NormalizationResult Normalize(Bpl.Program source, DafnyOptions options) =>
    B3Normalizer.Normalize(source, source.Implementations.Single(), options);
  private static B3NormalizationResult Boogie(string text) { var (source, options) = Parse(text); return Normalize(source, options); }
  private static void Valid(B3NormalizationResult result) {
    Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
    B3DefinitionContexts.ValidatePartition(result.Program!, result.Obligations, result.Contexts!, Bpl.Token.NoToken);
    Assert.Empty(result.Program!.Axioms);
  }
  private static void Unsupported(B3NormalizationResult result, string code = "b3_unsigned_wrapper") {
    Assert.False(result.Success); Assert.Null(result.Program); Assert.Empty(result.Obligations);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
  }
  private static IEnumerable<Ir.Expression> Expressions(Ir.Expression root) {
    yield return root;
    var children = root switch {
      Ir.Application application => application.Arguments, Ir.Operation operation => operation.Arguments,
      Ir.BitvectorOperation operation => operation.Arguments, Ir.Quantifier quantifier => quantifier.Patterns.SelectMany(pattern => pattern).Append(quantifier.Body),
      Ir.Let let => new[] { let.Value, let.Body }, Ir.Label label => new[] { label.Body }, _ => Array.Empty<Ir.Expression>()
    };
    foreach (var child in children) { foreach (var expression in Expressions(child)) { yield return expression; } }
  }
  private static IEnumerable<Ir.Statement> Statements(Ir.Statement root) {
    yield return root;
    var children = root switch {
      Ir.Block block => block.Statements, Ir.Choice choice => choice.Branches,
      Ir.Conditional conditional => new[] { conditional.Then, conditional.Else }, Ir.Loop loop => new[] { loop.Body },
      Ir.Labeled labeled => new[] { labeled.Body }, _ => Array.Empty<Ir.Statement>()
    };
    foreach (var child in children) { foreach (var statement in Statements(child)) { yield return statement; } }
  }
  private static IEnumerable<Ir.Expression> Expressions(Ir.Program program) => Statements(program.Unit.Body).SelectMany(statement => statement switch {
    Ir.Check check => new[] { check.Condition }, Ir.Assume assume => new[] { assume.Condition }, Ir.Assign assign => new[] { assign.Value },
    Ir.Conditional conditional => new[] { conditional.Condition }, _ => Array.Empty<Ir.Expression>()
  }).SelectMany(Expressions);
  private static IEnumerable<Ir.BitvectorOperation> Unsigned(Ir.Program program) => Expressions(program)
    .OfType<Ir.BitvectorOperation>().Where(operation => operation.Operator == Ir.BitvectorOperator.BitvectorToUnsignedInt);

  [Theory]
  [InlineData(1)]
  [InlineData(3)]
  [InlineData(67)]
  [InlineData(4096)]
  public void ExactLiveShapeLowersTheDirectCallWithoutAnAxiom(int width) {
    var (source, options) = Parse(Header(width) + $"procedure P(x:bv{width}); implementation P(x:bv{width}) {{ assert F(x) >= 0; }}");
    using var before = new StringWriter(); using (var writer = new Bpl.TokenTextWriter(before, options)) { source.Emit(writer); }
    var result = Normalize(source, options); Valid(result);
    var conversion = Assert.Single(Unsigned(result.Program!)); Assert.Equal(width, conversion.Width);
    Assert.Equal("int", conversion.Type); Assert.Equal(Ir.Protocol.BitvectorTypeName(width), Assert.Single(conversion.Arguments).Type);
    Assert.All(result.Contexts!, context => Assert.Empty(context.Program.Axioms));
    using var after = new StringWriter(); using (var writer = new Bpl.TokenTextWriter(after, options)) { source.Emit(writer); }
    Assert.Equal(before.ToString(), after.ToString());
    Assert.False(options.Get(CommonOptionBag.AdditionalAxioms));
  }

  [Theory]
  [InlineData("forall b:bv3 :: { F(b) } ((0 <= F(b) && F(b) < 7) && F(b) == N(b))")]
  [InlineData("forall b:bv3 :: { F(b) } ((F(b) == N(b)) && (0 <= F(b) && F(b) < 8))")]
  [InlineData("forall b:bv3 :: { F(b) } ((0 <= F(b) && F(b) < 8) && N(b) == F(b))")]
  [InlineData("forall b:bv3 :: { F(b) } b == 0bv3 ==> ((0 <= F(b) && F(b) < 8) && F(b) == N(b))")]
  [InlineData("forall b:bv3 :: { F(b) } (((0 <= F(b) && F(b) < 8) && F(b) == N(b)) && true)")]
  [InlineData("forall b:bv3 :: { F(b), N(b) } ((0 <= F(b) && F(b) < 8) && F(b) == N(b))")]
  [InlineData("forall b:bv3 :: { F(b) } { N(b) } ((0 <= F(b) && F(b) < 8) && F(b) == N(b))")]
  [InlineData("forall b:bv3 :: ((0 <= F(b) && F(b) < 8) && F(b) == N(b))")]
  [InlineData("forall b:bv3,c:bv3 :: { F(b), F(c) } ((0 <= F(b) && F(b) < 8) && F(b) == N(c))")]
  public void InexactConjunctsRangeBindersOrTriggersStayOpaque(string axiom) {
    var result = Boogie(Header(3, axiom) + "procedure P(x:bv3); implementation P(x:bv3) { assert F(x) >= 0; }");
    Valid(result); Assert.Empty(Unsigned(result.Program!)); Assert.Contains(Expressions(result.Program!), expression => expression is Ir.Application);
  }

  [Theory]
  [InlineData("missing")]
  [InlineData("detached")]
  [InlineData("hideable")]
  [InlineData("negative-trigger")]
  [InlineData("axiom-attributes")]
  [InlineData("quantifier-attributes")]
  [InlineData("native-body")]
  [InlineData("wrapper-body")]
  [InlineData("native-attribute")]
  [InlineData("formal-where")]
  [InlineData("binder-identity")]
  [InlineData("native-identity")]
  [InlineData("native-input-sort")]
  [InlineData("native-result-sort")]
  [InlineData("native-formal-identity")]
  [InlineData("binder-where")]
  [InlineData("native-definition-body")]
  [InlineData("native-extra-attribute")]
  [InlineData("native-type-parameter")]
  [InlineData("quantifier-type-parameter")]
  [InlineData("active-definition-metadata")]
  [InlineData("duplicate")]
  public void SourceObjectOrMetadataMismatchCannotSupplyRecognition(string change) {
    var (source, options) = Parse(Header() + "procedure P(x:bv3); implementation P(x:bv3) { assert F(x) >= 0; }");
    var axiom = source.Axioms.Single(); var quantified = Assert.IsType<Bpl.ForallExpr>(axiom.Expr);
    var wrapper = source.Functions.Single(function => function.Name == "F"); var native = source.Functions.Single(function => function.Name == "N");
    switch (change) {
      case "missing": source.RemoveTopLevelDeclaration(axiom); break;
      case "detached": source.RemoveTopLevelDeclaration(axiom); wrapper.DefinitionAxiom = axiom; break;
      case "hideable": axiom.CanHide = true; break;
      case "negative-trigger": quantified.Triggers = new Bpl.Trigger(quantified.tok, false, quantified.Triggers.Tr); break;
      case "axiom-attributes": axiom.Attributes = new Bpl.QKeyValue(axiom.tok, "include_dep", new List<object>(), null); break;
      case "quantifier-attributes": quantified.Attributes = new Bpl.QKeyValue(quantified.tok, "qid", new List<object> { "wrapper" }, null); break;
      case "native-body": native.Body = Bpl.Expr.Literal(0); break;
      case "wrapper-body": wrapper.Body = Bpl.Expr.Literal(0); break;
      case "native-attribute": native.Attributes = new Bpl.QKeyValue(native.tok, "bvbuiltin", new List<object> { "sbv2int" }, null); break;
      case "formal-where": wrapper.InParams[0].TypedIdent.WhereExpr = Bpl.Expr.True; break;
      case "binder-identity":
        var triggerCall = (Bpl.NAryExpr)quantified.Triggers.Tr.Single();
        triggerCall.Args[0] = new Bpl.IdentifierExpr(quantified.tok, new Bpl.BoundVariable(quantified.tok,
          new Bpl.TypedIdent(quantified.tok, "b", new Bpl.BvType(3)))) { Type = new Bpl.BvType(3) }; break;
      case "native-identity":
        var outer = (Bpl.NAryExpr)quantified.Body; var equality = (Bpl.NAryExpr)outer.Args[1];
        var nativeCall = (Bpl.NAryExpr)equality.Args[1];
        nativeCall.Fun = new Bpl.FunctionCall(new Bpl.Function(native.tok, "N", new List<Bpl.TypeVariable>(),
          native.InParams, native.OutParams[0], null, native.Attributes)); break;
      case "native-input-sort": native.InParams[0].TypedIdent.Type = new Bpl.BvType(5); break;
      case "native-result-sort": native.OutParams[0].TypedIdent.Type = Bpl.Type.Bool; break;
      case "native-formal-identity": native.InParams[0] = wrapper.InParams[0]; break;
      case "binder-where": quantified.Dummies[0].TypedIdent.WhereExpr = Bpl.Expr.True; break;
      case "native-definition-body": native.DefinitionBody = (Bpl.NAryExpr)((Bpl.NAryExpr)quantified.Body).Args[1]; break;
      case "native-extra-attribute": native.Attributes.Next = new Bpl.QKeyValue(native.tok, "inline", new List<object>(), null); break;
      case "native-type-parameter": native.TypeParameters.Add(new Bpl.TypeVariable(native.tok, "T")); break;
      case "quantifier-type-parameter": quantified.TypeParameters.Add(new Bpl.TypeVariable(quantified.tok, "T")); break;
      case "active-definition-metadata": wrapper.OtherDefinitionAxioms.Add(axiom); break;
      case "duplicate": source.AddTopLevelDeclaration(new Bpl.Axiom(axiom.tok, axiom.Expr)); break;
    }
    var result = Normalize(source, options);
    if (change == "duplicate") { Unsupported(result); }
    else { Valid(result); Assert.Empty(Unsigned(result.Program!)); }
  }

  [Theory]
  [InlineData("assume F(x) >= 0; assert true;")]
  [InlineData("var y:int; y := F(x); assert y >= 0;")]
  [InlineData("assert {:subsumption 0} F(x) == F(x);")]
  public void OrdinaryAndNonlearningDirectRootsKeepTheirSourceIdentity(string body) {
    var result = Boogie(Header() + "procedure P(x:bv3); implementation P(x:bv3) { " + body + " }");
    Valid(result); Assert.NotEmpty(Unsigned(result.Program!));
    Assert.All(result.Contexts!, context => Assert.NotEmpty(Unsigned(context.Program)));
  }

  [Fact]
  public void NestedStateCommandUsesTheActualChildRoot() {
    var (source, options) = Parse(Header() + "procedure P(x:bv3); implementation P(x:bv3) { assert F(x) >= 0; }");
    var unit = source.Implementations.Single(); var commands = unit.StructuredStmts!.BigBlocks[0].simpleCmds;
    var assertion = Assert.IsType<Bpl.AssertCmd>(Assert.Single(commands));
    var state = new Bpl.StateCmd(assertion.tok, new List<Bpl.Variable>(), new List<Bpl.Cmd> { assertion });
    commands[0] = state;
    Assert.Same(state, unit.Blocks[0].Cmds[0]);
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    var result = Normalize(source, options); Valid(result); Assert.Single(Unsigned(result.Program!));
  }

  [Fact]
  public void OneSourceCallSharedInsideAnEligibleRootHasSeparateTargetWitnesses() {
    var (source, options) = Parse(Header() + "procedure P(x:bv3); implementation P(x:bv3) { assert F(x) == F(x); }");
    var assertion = source.Implementations.Single().Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    var equation = Assert.IsType<Bpl.NAryExpr>(assertion.Expr); equation.Args[1] = equation.Args[0];
    var result = Normalize(source, options); Valid(result);
    var conversions = Unsigned(result.Program!).ToArray(); Assert.Equal(2, conversions.Length);
    Assert.NotSame(conversions[0], conversions[1]);
  }

  [Fact]
  public void ReadOverWriteCopiesRequireAllRetainedOccurrenceReceipts() {
    var result = Boogie(Header() + "procedure P(m:[int]int,x:bv3); implementation P(m:[int]int,x:bv3) { " +
      "assert m[F(x) := 3][F(x)] == 3; }");
    Valid(result);
    // Both source calls occur once. The reviewed read-over-write ITE copies
    // the stored index into its equality and unchanged opaque fallback.
    Assert.True(Unsigned(result.Program!).Count() > 2);
    Assert.All(result.Contexts!, context => Assert.Equal(Unsigned(result.Program!).Count(), Unsigned(context.Program).Count()));
  }

  [Theory]
  [InlineData(64, true)]
  [InlineData(65, false)]
  public void ShapeInventoryHasAnExplicitAdmissionCeiling(int count, bool admitted) {
    var declarations = string.Concat(Enumerable.Range(0, count).Select(i => Header().Replace("F(", "F" + i + "(").Replace("N(", "N" + i + "(")));
    var result = Boogie(declarations + "procedure P(x:bv3); implementation P(x:bv3) { assert F0(x) >= 0; }");
    if (admitted) { Valid(result); Assert.Single(Unsigned(result.Program!)); } else { Unsupported(result); }
  }

  [Theory]
  [InlineData(1021, true)]
  [InlineData(1022, false)]
  public void EveryCopiedArgumentChargesTheExistingCompleteWordBound(int locals, bool admitted) {
    // One retained F signature and two distinct x0 occurrences add three
    // width charges to the local declarations. 1024*4096 is the ceiling.
    var names = string.Join(",", Enumerable.Range(0, locals).Select(i => "x" + i));
    var result = Boogie(Header(4096) + $"procedure P(); implementation P() {{ var {names}:bv4096; assert F(x0) == F(x0); }}");
    if (admitted) { Valid(result); Assert.Equal(2, Unsigned(result.Program!).Count()); }
    else { Unsupported(result, "b3_bitvector_limit"); }
  }

  [Theory]
  [InlineData(0)]
  [InlineData(2)]
  public void EachMaskCertifiesOnlyItsOwnRetainedCalls(int subsumption) {
    var (source, options) = Parse(Header() + "function Guard():bool; function G():int; axiom Guard() ==> G() == 7; " +
      "procedure P(x:bv3); implementation P(x:bv3) { assume Guard(); " +
      $"assert {{:subsumption {subsumption}}} G() == F(x); hide G; assert G() == 7; }}");
    var guarded = source.Axioms.Single(axiom => axiom.Expr is not Bpl.ForallExpr);
    guarded.CanHide = true; source.Functions.Single(function => function.Name == "G").OtherDefinitionAxioms.Add(guarded);
    var result = Normalize(source, options); Valid(result);
    Assert.Equal(2, result.Contexts!.Count);
    Assert.NotEmpty(Unsigned(result.Contexts.Single(context => context.Definitions.Count != 0).Program));
    var hidden = result.Contexts.Single(context => context.Definitions.Count == 0);
    if (subsumption == 0) { Assert.Empty(Unsigned(hidden.Program)); }
    else { Assert.NotEmpty(Unsigned(hidden.Program)); }
  }

  [Theory]
  [InlineData("procedure P(x:bv3); requires F(x) >= 0; implementation P(x:bv3) { assert true; }")]
  [InlineData("procedure P(x:bv3); ensures F(x) >= 0; implementation P(x:bv3) { assert true; }")]
  [InlineData("var g:bv3 where F(g) >= 0; procedure P(); implementation P() { assert true; }")]
  [InlineData("procedure Q(x:bv3); requires F(x) >= 0; procedure P(x:bv3); implementation P(x:bv3) { call Q(x); }")]
  [InlineData("procedure Q(y:int); procedure P(x:bv3); implementation P(x:bv3) { call Q(F(x)); }")]
  [InlineData("procedure P(); implementation P() { var y:bv3 where F(y) >= 0; assert true; }")]
  [InlineData("procedure P(x:bv3); implementation P(x:bv3) { while (*) invariant F(x) >= 0; { break; } }")]
  public void NonDirectRootsCannotBorrowAProgramShape(string body) { Unsupported(Boogie(Header() + body)); }

  [Fact]
  public void AnActualBodyChainStillRequiresItsOwnReviewedRootRoute() {
    Unsupported(Boogie(Header() + "function {:inline} G(x:bv3):int { F(x) } procedure P(x:bv3); " +
      "implementation P(x:bv3) { assert G(x) >= 0; }"), "b3_bitvector_body");
  }

  [Fact]
  public void SharedRawAndSpecExpressionDoesNotLicenseTheSpecInvocation() {
    var (source, options) = Parse(Header() + "var g:bv3; procedure P(); implementation P() { assert F(g) >= 0; }");
    var unit = source.Implementations.Single(); var assertion = unit.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    unit.Proc.Requires.Add(new Bpl.Requires(false, assertion.Expr));
    Unsupported(Normalize(source, options));
  }

  [Fact]
  public async Task ActualDafnyDirectRangeRoundTripAndFalseChecksRemainPresent() {
    var results = await B3DefinitionContextTests.Dafny("""
      method Native(x:bv3) {
        assert 0 <= (x as int) < 8;
        assert ((x as int) as bv3) == x;
      }
      method False(x:bv67) requires x == (73786976294838206464 as bv67) {
        assert 0 <= (x as int);
        assert false;
      }
      """);
    Assert.Equal(3, results.Count);
    Assert.All(results, Valid);
    Assert.Equal(new[] { 4, 1, 2 }, results.Select(result => result.Obligations.Count).ToArray());
    Assert.Contains(results.SelectMany(result => Unsigned(result.Program!)), operation => operation.Width == 3);
    Assert.Contains(results.SelectMany(result => Unsigned(result.Program!)), operation => operation.Width == 67);
    Assert.Contains(results.SelectMany(result => Expressions(result.Program!)), expression => expression is Ir.BooleanLiteral { Value: false });
  }

  private sealed record Fixture(B3UnsignedWrappers Helper, Ir.Program Original,
    IReadOnlyList<Ir.SourceIdentity> Identities,
    IReadOnlyDictionary<string, IReadOnlyList<B3DefinitionContexts.Formula>> Definitions,
    IReadOnlyList<B3VerificationContext> Preliminary, IReadOnlyList<B3VerificationContext> Final, Bpl.Program Source);
  private static Fixture Certified() {
    var (source, _) = Parse(Header() + "procedure P(x:bv3); implementation P(x:bv3) { assert F(x) == F(x); }");
    var unit = source.Implementations.Single(); B3StructuredCfgCorrespondence.Validate(unit);
    var root = unit.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    var equation = (Bpl.NAryExpr)root.Expr;
    var helper = new B3UnsignedWrappers(source, unit);
    var applications = new[] { new Ir.Application("sF", "int", new[] { new Ir.Variable("sX", "#bv3") }),
      new Ir.Application("sF", "int", new[] { new Ir.Variable("sX", "#bv3") }) };
    var body = helper.InCommand(root, () => {
      for (var i = 0; i < 2; i++) { helper.Capture((Bpl.NAryExpr)equation.Args[i], applications[i]); }
      return new Ir.Check("sO0", new Ir.Operation(Ir.Operator.Equal, "bool", applications), false);
    });
    var original = new Ir.Program(Array.Empty<string>(), new[] { new Ir.Function("sF", new[] { new Ir.Binding("sP", "#bv3") }, "int") },
      Array.Empty<Ir.Axiom>(), new Ir.Unit("sUnit", new[] { new Ir.Binding("sX", "#bv3") }, body));
    var identities = new[] { new Ir.SourceIdentity("sO0", "source.dfy", 1, 1, "assertion") };
    var definitions = new Dictionary<string, IReadOnlyList<B3DefinitionContexts.Formula>> { ["sO0"] = Array.Empty<B3DefinitionContexts.Formula>() };
    var preliminary = B3DefinitionContexts.Create(original, identities, definitions, Bpl.Token.NoToken);
    var lower = helper.Lower(original, identities, definitions, preliminary);
    var final = B3DefinitionContexts.Create(lower, identities, definitions, Bpl.Token.NoToken);
    helper.ValidateContexts(preliminary, final);
    return new Fixture(helper, original, identities, definitions, preliminary, final, source);
  }

  [Theory]
  [InlineData("argument")]
  [InlineData("width")]
  [InlineData("operator")]
  [InlineData("indices")]
  [InlineData("arity")]
  [InlineData("result-field")]
  [InlineData("constructor")]
  [InlineData("learning")]
  [InlineData("check-id")]
  [InlineData("mask")]
  [InlineData("definitions")]
  public void NarrowValidatorRejectsEveryUncertifiedDelta(string change) {
    var fixture = Certified(); var context = Assert.Single(fixture.Final);
    var check = Assert.IsType<Ir.Check>(context.Program.Unit.Body); var equality = Assert.IsType<Ir.Operation>(check.Condition);
    var conversion = Assert.IsType<Ir.BitvectorOperation>(equality.Arguments[0]);
    Ir.Expression replacement = change switch {
      "argument" => conversion with { Arguments = new[] { new Ir.BitvectorLiteral("0", 3) } },
      "width" => conversion with { Width = 5 }, "operator" => conversion with { Operator = Ir.BitvectorOperator.IntToBitvector },
      "indices" => conversion with { Start = 1 }, "arity" => conversion with { Arguments = Array.Empty<Ir.Expression>() },
      "result-field" => conversion with { ResultType = "bool" }, _ => conversion
    };
    var changedCheck = check with { Condition = equality with { Arguments = new[] { replacement, equality.Arguments[1] } } };
    if (change == "constructor") { changedCheck = check with { Condition = new Ir.BooleanLiteral(true) }; }
    if (change == "learning") { changedCheck = changedCheck with { Learn = true }; }
    if (change == "check-id") { changedCheck = changedCheck with { ObligationId = "sO9" }; }
    var changed = context with { Program = context.Program with { Unit = context.Program.Unit with { Body = changedCheck } } };
    if (change == "mask") { changed = changed with { MaskId = "wrong-mask" }; }
    if (change == "definitions") { changed = changed with { Definitions = new[] { new B3DefinitionOrigin("other", "F", 0, "hash", "instance") } }; }
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => fixture.Helper.ValidateContexts(fixture.Preliminary, new[] { changed }));
  }

  [Fact]
  public void AResolvedCallCannotSupplyANonemptyTypeInstantiation() {
    var (source, options) = Parse(Header() + "procedure P(x:bv3); implementation P(x:bv3) { assert F(x) >= 0; }");
    var assertion = source.Implementations.Single().Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    var call = Assert.IsType<Bpl.NAryExpr>(((Bpl.NAryExpr)assertion.Expr).Args[0]);
    call.TypeParameters = Bpl.SimpleTypeParamInstantiation.From(new List<Bpl.TypeVariable> {
      new Bpl.TypeVariable(call.tok, "T") }, new List<Bpl.Type> { Bpl.Type.Int });
    Unsupported(Normalize(source, options));
  }

  [Theory]
  [InlineData("axiom")]
  [InlineData("command")]
  [InlineData("formal")]
  [InlineData("body")]
  [InlineData("expression")]
  [InlineData("call")]
  [InlineData("argument")]
  [InlineData("argument-declaration")]
  [InlineData("call-type")]
  [InlineData("call-instantiation")]
  public void SourceOwnershipAndSignatureCannotChangeAfterCapture(string change) {
    var fixture = Certified(); var source = fixture.Source;
    var assertion = source.Implementations.Single().Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    var equation = Assert.IsType<Bpl.NAryExpr>(assertion.Expr); var call = Assert.IsType<Bpl.NAryExpr>(equation.Args[0]);
    switch (change) {
      case "axiom": source.RemoveTopLevelDeclaration(source.Axioms.Single()); break;
      case "command": source.Implementations.Single().Blocks[0].Cmds.Clear(); break;
      case "formal":
        var native = source.Functions.Single(function => function.Name == "N"); var formal = native.InParams[0];
        native.InParams[0] = new Bpl.Formal(formal.tok, new Bpl.TypedIdent(formal.tok, formal.Name, formal.TypedIdent.Type), true); break;
      case "body": source.Functions.Single(function => function.Name == "F").Body = Bpl.Expr.Literal(0); break;
      case "expression": assertion.Expr = Bpl.Expr.True; break;
      case "call": equation.Args[0] = new Bpl.NAryExpr(call.tok, call.Fun, call.Args) { Type = call.Type }; break;
      case "argument":
        var identifier = Assert.IsType<Bpl.IdentifierExpr>(call.Args[0]);
        call.Args[0] = new Bpl.IdentifierExpr(call.tok, identifier.Decl) { Type = identifier.Type }; break;
      case "argument-declaration":
        Assert.IsType<Bpl.IdentifierExpr>(call.Args[0]).Decl = new Bpl.LocalVariable(call.tok,
          new Bpl.TypedIdent(call.tok, "other", new Bpl.BvType(3))); break;
      case "call-type": call.Type = Bpl.Type.Real; break;
      case "call-instantiation": call.TypeParameters = Bpl.SimpleTypeParamInstantiation.From(
        new List<Bpl.TypeVariable> { new Bpl.TypeVariable(call.tok, "T") }, new List<Bpl.Type> { Bpl.Type.Int }); break;
    }
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => fixture.Helper.Lower(fixture.Original, fixture.Identities, fixture.Definitions, fixture.Preliminary));
  }

  [Fact]
  public void AssignmentRhsCannotChangeAfterCapture() {
    var (source, _) = Parse(Header() + "procedure P(x:bv3); implementation P(x:bv3) { var z:int; z := F(x); assert true; }");
    var unit = source.Implementations.Single(); var helper = new B3UnsignedWrappers(source, unit);
    var root = unit.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssignCmd>().Single();
    var call = Assert.IsType<Bpl.NAryExpr>(Assert.Single(root.Rhss));
    var application = new Ir.Application("sF", "int", new[] { new Ir.Variable("sX", "#bv3") });
    var assigned = helper.InCommand(root, () => { helper.Capture(call, application); return new Ir.Assign("sZ", application); });
    var original = new Ir.Program(Array.Empty<string>(), new[] { new Ir.Function("sF", new[] { new Ir.Binding("sP", "#bv3") }, "int") },
      Array.Empty<Ir.Axiom>(), new Ir.Unit("sUnit", new[] { new Ir.Binding("sX", "#bv3"), new Ir.Binding("sZ", "int") },
        new Ir.Block(new Ir.Statement[] { assigned, new Ir.Check("sO0", new Ir.BooleanLiteral(true), false) })));
    var identities = new[] { new Ir.SourceIdentity("sO0", "source.dfy", 1, 1, "assertion") };
    var definitions = new Dictionary<string, IReadOnlyList<B3DefinitionContexts.Formula>> { ["sO0"] = Array.Empty<B3DefinitionContexts.Formula>() };
    var preliminary = B3DefinitionContexts.Create(original, identities, definitions, Bpl.Token.NoToken);
    root.Rhss = new List<Bpl.Expr> { Bpl.Expr.Literal(0) };
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => helper.Lower(original, identities, definitions, preliminary));
  }

  [Fact]
  public void AClonedRawCommandCannotBorrowItsSourcesRoot() {
    var (source, _) = Parse(Header() + "procedure P(x:bv3); implementation P(x:bv3) { assert F(x) >= 0; }");
    var unit = source.Implementations.Single(); var helper = new B3UnsignedWrappers(source, unit);
    var root = unit.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    var call = Assert.IsType<Bpl.NAryExpr>(((Bpl.NAryExpr)root.Expr).Args[0]);
    var forged = new Bpl.AssertCmd(root.tok, root.Expr);
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => helper.InCommand(forged, () => {
      var application = new Ir.Application("sF", "int", new[] { new Ir.Variable("sX", "#bv3") });
      helper.Capture(call, application); return new Ir.Check("sO0", application, false);
    }));
  }

  [Fact]
  public void AStructurallyEqualApplicationCannotBorrowAnotherCallsReferenceWitness() {
    var fixture = Certified(); var check = Assert.IsType<Ir.Check>(fixture.Original.Unit.Body);
    var equality = Assert.IsType<Ir.Operation>(check.Condition); var call = Assert.IsType<Ir.Application>(equality.Arguments[0]);
    var forged = call with { }; Assert.Equal(call, forged); Assert.NotSame(call, forged);
    var original = fixture.Original with { Unit = fixture.Original.Unit with { Body = check with {
      Condition = equality with { Arguments = new[] { forged, equality.Arguments[1] } } } } };
    var contexts = B3DefinitionContexts.Create(original, fixture.Identities, fixture.Definitions, Bpl.Token.NoToken);
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => fixture.Helper.Lower(original, fixture.Identities, fixture.Definitions, contexts));
  }
}
