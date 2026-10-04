// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Globalization;
using Microsoft.BaseTypes;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3RealArithmeticTests {
  [Theory]
  [InlineData("0", "0", "1")]
  [InlineData("-1.3", "-13", "10")]
  [InlineData("0.25", "25", "100")]
  [InlineData("1.0", "1", "1")]
  [InlineData("9007199254740993.125", "9007199254740993125", "1000")]
  [InlineData("123e4", "1230000", "1")]
  [InlineData("123e-4", "123", "10000")]
  public void ExactBigDecCaptureNeverRounds(string value, string numerator, string denominator) {
    var result = Literal(value);
    Validate(result);
    var equality = Assert.IsType<Ir.Operation>(Checks(result).Single().Condition);
    Assert.Equal(new Ir.RationalLiteral(numerator, denominator), equality.Arguments[0]);
  }

  [Theory]
  [InlineData("1e2147483647")]
  [InlineData("1e-2147483648")]
  [InlineData("1e10000")]
  [InlineData("1e-10000")]
  public void ExpansionBoundsPrecedePowerAllocation(string value) {
    Unsupported(Literal(value), "b3_literal_limit");
  }

  [Fact]
  public void ExactDecimalBoundaryIncludesTheSignAndDenominatorDigits() {
    var positive = Literal("1e9999");
    Validate(positive);
    Assert.Equal(Ir.Protocol.MaximumIntegerCharacters,
      Assert.IsType<Ir.RationalLiteral>(Assert.IsType<Ir.Operation>(Checks(positive).Single().Condition).Arguments[0]).Numerator.Length);
    Unsupported(Literal("-1e9999"), "b3_literal_limit");
    var fraction = Literal("1e-9999");
    Validate(fraction);
    Assert.Equal(Ir.Protocol.MaximumIntegerCharacters,
      Assert.IsType<Ir.RationalLiteral>(Assert.IsType<Ir.Operation>(Checks(fraction).Single().Condition).Arguments[0]).Denominator.Length);
  }

  [Fact]
  public void AnOversizedMantissaIsRejectedBeforeDecimalExpansion() {
    Unsupported(Literal(new string('7', Ir.Protocol.MaximumIntegerCharacters + 1)), "b3_literal_limit");
  }

  [Fact]
  public void RealVariablesKeepTheNativeSort() {
    var result = Boogie("procedure P(x: real); implementation P(x: real) { assert x == x; }");
    Validate(result);
    Assert.Equal("real", Assert.Single(result.Program!.Unit.Variables).Type);
    Assert.Empty(result.Program.Types);
  }

  [Theory]
  [InlineData("int", "int", true, true)]
  [InlineData("int", "real", true, false)]
  [InlineData("real", "int", false, true)]
  [InlineData("real", "real", false, false)]
  public void OnlyRealDivisionIndependentlyEmbedsIntegerOperands(string left, string right, bool embedLeft, bool embedRight) {
    var result = Boogie($"procedure P(x: {left}, y: {right}); implementation P(x: {left}, y: {right}) {{ assert x / y == 0.0; }}");
    Validate(result);
    var division = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(Checks(result).Single().Condition).Arguments[0]);
    Assert.Equal(Ir.Operator.RealDivide, division.Operator);
    Assert.Equal("real", division.Type);
    Assert.Equal(embedLeft, division.Arguments[0] is Ir.Operation { Operator: Ir.Operator.ToReal });
    Assert.Equal(embedRight, division.Arguments[1] is Ir.Operation { Operator: Ir.Operator.ToReal });
    Assert.All(division.Arguments, argument => Assert.Equal("real", argument.Type));
  }

  [Theory]
  [InlineData("real", "int", "int(x)", Ir.Operator.ToInt)]
  [InlineData("int", "real", "real(x)", Ir.Operator.ToReal)]
  public void DirectCoercionsKeepTheirDifferentResultTypes(string input, string output, string body, Ir.Operator expected) {
    var result = Boogie($"procedure P(x: {input}); implementation P(x: {input}) {{ assert {body} == {body}; }}");
    Validate(result);
    var operation = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(Checks(result).Single().Condition).Arguments[0]);
    Assert.Equal(expected, operation.Operator);
    Assert.Equal(output, operation.Type);
    Assert.Equal(input, Assert.Single(operation.Arguments).Type);
  }

  [Theory]
  [InlineData("function {:inline}", "real", "int", "int(x)", Ir.Operator.ToInt)]
  [InlineData("revealed function", "real", "int", "int(x)", Ir.Operator.ToInt)]
  [InlineData("function {:inline}", "int", "real", "real(x)", Ir.Operator.ToReal)]
  [InlineData("revealed function", "int", "real", "real(x)", Ir.Operator.ToReal)]
  public void TypedBodiesAndActiveDefinitionsUseStructureRatherThanNames(string declaration, string input, string output,
    string body, Ir.Operator expected) {
    var options = Options();
    var source = ParseBoogie($"{declaration} DifferentName(x: {input}): {output} {{ {body} }} " +
      $"procedure P(x: {input}); implementation P(x: {input}) {{ assert DifferentName(x) == DifferentName(x); }}", options);
    var before = Emit(source, options);
    var result = Normalize(source, options);
    Validate(result);
    Assert.Equal(before, Emit(source, options));
    var operation = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(Checks(result).Single().Condition).Arguments[0]);
    Assert.Equal(expected, operation.Operator);
    Assert.Empty(result.Program!.Functions);
    Assert.Empty(result.Program.Axioms);
  }

  [Theory]
  [InlineData("real", "int", "int(x)", Ir.Operator.ToInt)]
  [InlineData("int", "real", "real(x)", Ir.Operator.ToReal)]
  public void AnActualTypedBodyDoesNotRequireAnInlineAttribute(string input, string output, string body, Ir.Operator expected) {
    var options = Options();
    var source = ParseBoogie($"function {{:inline}} F(x: {input}): {output} {{ {body} }} " +
      $"procedure P(x: {input}); implementation P(x: {input}) {{ assert F(x) == F(x); }}", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    function.Attributes = null;
    Assert.NotNull(function.Body);
    var result = Normalize(source, options);
    Validate(result);
    Assert.Equal(expected,
      Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(Checks(result).Single().Condition).Arguments[0]).Operator);
  }

  [Fact]
  public void AnActiveEqualityWithoutDefinitionMetadataDoesNotClaimNativeSemantics() {
    var options = Options();
    var source = ParseBoogie("revealed function F(x: real): int { int(x) } " +
      "procedure P(); implementation P() { assert F(1.3) == 1; }", options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().DefinitionAxiom = null;
    Opaque(Normalize(source, options));
  }

  [Fact]
  public void ADefinitionWithVisibilityRestrictionsCannotSupplyAnActiveCoercion() {
    var options = Options();
    var source = ParseBoogie("revealed function F(x: real): int { int(x) } " +
      "procedure P(); implementation P() { assert F(1.3) == 1; }", options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().AlwaysRevealed = false;
    Unsupported(Normalize(source, options));
  }

  [Theory]
  [InlineData("Int", "real", "int")]
  [InlineData("Real", "int", "real")]
  public void APreludeLookingNameWithoutADefinitionStaysOpaque(string name, string input, string output) {
    Opaque(Boogie($"function {name}(x: {input}): {output}; " +
      $"procedure P(x: {input}); implementation P(x: {input}) {{ assert {name}(x) == {name}(x); }}"));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void DetachedMetadataCannotBorrowAnEquivalentCoercionAxiom(bool equivalentReplacement) {
    var options = Options();
    var source = ParseBoogie("revealed function F(x: real): int { int(x) } " +
      "procedure P(); implementation P() { assert F(1.3) == 1; }", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    var axiom = function.DefinitionAxiom;
    source.RemoveTopLevelDeclaration(axiom);
    if (equivalentReplacement) { source.AddTopLevelDeclaration(new Bpl.Axiom(axiom.tok, axiom.Expr)); }
    Unsupported(Normalize(source, options));
  }

  [Theory]
  [InlineData("forall x: real :: x >= 0.0 ==> F(x) == int(x)")]
  [InlineData("exists x: real :: F(x) == int(x)")]
  [InlineData("forall x: real :: F(x) == int(1.3)")]
  public void NonuniversalOrCapturedDefinitionsCannotSupplyACoercion(string axiom) {
    var options = Options();
    var source = ParseBoogie("revealed function F(x: real): int; axiom (" + axiom + "); " +
      "procedure P(); implementation P() { assert F(1.3) == 1; }", options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().DefinitionAxiom = source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
    var result = Normalize(source, options);
    if (axiom.Contains("int(1.3)")) { Unsupported(result); } else { Opaque(result); }
  }

  [Fact]
  public void MatchingFormalNamesDoNotReplaceDeclarationIdentity() {
    var options = Options();
    var source = ParseBoogie("function {:inline} F(x: real): int { int(x) } " +
      "procedure P(); implementation P() { assert F(1.3) == 1; }", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    var body = Assert.IsType<Bpl.NAryExpr>(function.Body);
    var detached = new Bpl.LocalVariable(function.tok, new Bpl.TypedIdent(function.tok, "x", Bpl.Type.Real));
    body.Args[0] = new Bpl.IdentifierExpr(function.tok, detached);
    Unsupported(Normalize(source, options));
  }

  [Theory]
  [InlineData("forall x, y: real :: F(x) == int(y)")]
  [InlineData("forall x: real :: F(1.3) == int(x)")]
  public void AnActiveDefinitionRequiresTheCompleteBinderBijection(string axiom) {
    var options = Options();
    var source = ParseBoogie("revealed function F(x: real): int; axiom (" + axiom + "); " +
      "procedure P(); implementation P() { assert F(1.3) == 1; }", options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().DefinitionAxiom = source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
    Unsupported(Normalize(source, options));
  }

  [Fact]
  public void AGenericClosedInstanceCannotDefineEveryConversion() {
    var options = Options();
    var source = ParseBoogie("revealed function F<T>(x: T): int; axiom (forall x: real :: F(x) == int(x)); " +
      "procedure P(); implementation P() { assert F(1.3) == 1; }", options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().DefinitionAxiom = source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
    Unsupported(Normalize(source, options));
  }

  [Fact]
  public void AnotherFunctionsActiveDefinitionCannotSupplyTheCalledConversion() {
    var options = Options();
    var source = ParseBoogie("revealed function F(x: real): int; revealed function G(x: real): int; " +
      "axiom (forall x: real :: G(x) == int(x)); procedure P(); implementation P() { assert F(1.3) == 1; }", options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single(function => function.Name == "F").DefinitionAxiom =
      source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
    Opaque(Normalize(source, options));
  }

  [Fact]
  public void AnInlineFloorCompositionNeedsAnEligibleNestedDefinition() {
    var result = Boogie("revealed function Inner(x: real): int { int(x) } " +
      "function {:inline} Outer(x: real): int { Inner(x) } " +
      "procedure P(); implementation P() { assert Outer(-1.3) == -2; }");
    Validate(result);
    Assert.Equal(Ir.Operator.ToInt,
      Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(Checks(result).Single().Condition).Arguments[0]).Operator);
    Assert.Empty(result.Program!.Axioms);
    Assert.Empty(result.Program.Functions);
  }

  [Fact]
  public void ACompositionCannotSubstituteAConstantForItsFormal() {
    Opaque(Boogie("revealed function Inner(x: real): int { int(x) } " +
      "function {:inline} Outer(x: real): int { Inner(1.3) } " +
      "procedure P(); implementation P() { assert Outer(2.0) == 2; }"));
  }

  [Fact]
  public void RawRealDivisionAtZeroAddsNoSyntheticNonzeroFact() {
    var result = Boogie("procedure P(x: real); implementation P(x: real) { assert x / 0.0 == 0.0; assert false; }");
    Validate(result);
    Assert.Equal(2, result.Obligations.Count);
    Assert.DoesNotContain(Statements(result.Program!.Unit.Body), statement => statement is Ir.Assume);
    Assert.Equal(2, Checks(result).Count());
  }

  [Fact]
  public async Task ActualDafnyConversionsFloorAndZeroKeepTheirOriginalChecks() {
    var results = await Dafny("""
      lemma Floor(x: real) { assert (x.Floor as real) <= x; }
      method Fractional() returns (i: int) { i := 1.5 as int; }
      lemma Embed(i: int) { assert ((i as real) as int) == i; }
      method Zero() { var q := 1.0 / 0.0; }
      """);
    Assert.All(results, Validate);
    var expressions = results.SelectMany(Checks).SelectMany(check => Expressions(check.Condition)).ToArray();
    Assert.Contains(expressions, expression => expression is Ir.Operation { Operator: Ir.Operator.ToInt });
    Assert.Contains(expressions, expression => expression is Ir.Operation { Operator: Ir.Operator.ToReal });
    Assert.Contains(results.SelectMany(result => result.Obligations), obligation => obligation.Description.Contains("must be an integer"));
    Assert.Contains(results.SelectMany(result => result.Obligations), obligation => obligation.Description.Contains("possible division by zero"));
    Assert.All(results, result => Assert.Empty(result.Program!.Axioms));
  }

  [Fact]
  public void RealMapObservationsUseNativeRealResultsWithoutNewAxioms() {
    var result = Boogie("procedure P(); implementation P() { var m: [int]real; m := m[0 := 0.25]; assert m[0] == 0.25; }");
    Validate(result);
    Assert.Empty(result.Program!.Axioms);
    Assert.Contains(result.Program.Functions, function => function.ResultType == "real");
  }

  private static B3NormalizationResult Literal(string value) {
    var options = Options();
    var source = ParseBoogie("procedure P(); implementation P() { assert 0.0 == 0.0; }", options);
    var check = source.Implementations.Single().StructuredStmts!.BigBlocks[0].simpleCmds.OfType<Bpl.AssertCmd>().Single();
    Assert.IsType<Bpl.NAryExpr>(check.Expr).Args[0] = new Bpl.LiteralExpr(check.tok, BigDec.FromString(value));
    return Normalize(source, options);
  }

  private static void Opaque(B3NormalizationResult result) {
    Validate(result);
    var equality = Assert.IsType<Ir.Operation>(Checks(result).Single().Condition);
    Assert.IsType<Ir.Application>(equality.Arguments[0]);
    Assert.Empty(result.Program!.Axioms);
  }

  private static DafnyOptions Options() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    return options;
  }

  private static async Task<List<B3NormalizationResult>> Dafny(string text, int? arithmeticMode = null) {
    Microsoft.Dafny.Type.ResetScopes();
    var options = Options();
    if (arithmeticMode.HasValue) { options.ArithMode = arithmeticMode.Value; }
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(text, new Uri("file:///B3RealArithmeticTests.dfy"), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(message => message.Message)));
    var results = new List<B3NormalizationResult>();
    foreach (var (_, source) in BoogieGenerator.Translate(parsed.Program, reporter)) {
      Assert.Equal(0, source.Resolve(options));
      Assert.Equal(0, source.Typecheck(options));
      foreach (var implementation in source.Implementations) { results.Add(B3Normalizer.Normalize(source, implementation, options)); }
    }
    Assert.False(reporter.HasErrors);
    return results;
  }

  private static Bpl.Program ParseBoogie(string text, DafnyOptions options) {
    Assert.Equal(0, Bpl.Parser.Parse(text, "B3RealArithmeticTests.bpl", out var source));
    Assert.Equal(0, source.Resolve(options));
    Assert.Equal(0, source.Typecheck(options));
    return source;
  }

  private static B3NormalizationResult Normalize(Bpl.Program source, DafnyOptions options) =>
    B3Normalizer.Normalize(source, source.Implementations.Single(), options);

  private static B3NormalizationResult Boogie(string text) {
    var options = Options();
    return Normalize(ParseBoogie(text, options), options);
  }

  private static string Emit(Bpl.Program source, DafnyOptions options) {
    using var output = new StringWriter();
    using var writer = new Bpl.TokenTextWriter(output, options);
    source.Emit(writer);
    return output.ToString();
  }

  private static IEnumerable<Ir.Expression> Expressions(Ir.Expression expression) {
    yield return expression;
    if (expression is Ir.Operation operation) {
      foreach (var child in operation.Arguments.SelectMany(Expressions)) { yield return child; }
    } else if (expression is Ir.Label label) {
      foreach (var child in Expressions(label.Body)) { yield return child; }
    }
  }

  private static IEnumerable<Ir.Check> Checks(B3NormalizationResult result) => Statements(result.Program!.Unit.Body).OfType<Ir.Check>();

  private static IEnumerable<Ir.Statement> Statements(Ir.Statement statement) {
    yield return statement;
    IEnumerable<Ir.Statement> children = statement switch {
      Ir.Block block => block.Statements,
      Ir.Choice choice => choice.Branches,
      Ir.Conditional conditional => new[] { conditional.Then, conditional.Else },
      Ir.Loop loop => new[] { loop.Body },
      Ir.Labeled labeled => new[] { labeled.Body },
      _ => Array.Empty<Ir.Statement>()
    };
    foreach (var child in children.SelectMany(Statements)) { yield return child; }
  }

  private static void Unsupported(B3NormalizationResult result, string code = "b3_arithmetic") {
    Assert.False(result.Success);
    Assert.Null(result.Program);
    Assert.Empty(result.Obligations);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
  }

  private static void Validate(B3NormalizationResult result) {
    Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
    var request = new Ir.Request(Ir.Protocol.Version, "real-arithmetic-test", Ir.Protocol.NormalizerVersion,
      new string('0', 40), Ir.Protocol.GetProgramHash(result.Program!), result.Program!.Unit.Name, result.Program,
      new Ir.Configuration("z3", new[] { "-in", "-smt2" }, 1000, 10000, 100000, 2, "5.1.0", new string('d', 64)), result.Obligations, new string('f', 64));
    Ir.ProtocolValidation.ValidateRequest(request);
  }
}
