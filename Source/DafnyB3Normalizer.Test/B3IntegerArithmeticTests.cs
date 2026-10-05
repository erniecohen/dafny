// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3IntegerArithmeticTests {
  [Theory]
  [InlineData("div", Ir.Operator.Divide)]
  [InlineData("mod", Ir.Operator.Modulo)]
  public void NativeIntegerOperationsKeepOperandOrder(string operation, Ir.Operator expected) {
    var result = Boogie($"procedure P(x: int, y: int); implementation P(x: int, y: int) {{ assert x {operation} y == 0; }}");
    Validate(result);
    var equality = Assert.IsType<Ir.Operation>(Checks(result).Single().Condition);
    var arithmetic = Assert.IsType<Ir.Operation>(equality.Arguments[0]);
    Assert.Equal(expected, arithmetic.Operator);
    Assert.Equal("int", arithmetic.Type);
    Assert.Equal(result.Program!.Unit.Variables[0].Name, Assert.IsType<Ir.Variable>(arithmetic.Arguments[0]).Name);
    Assert.Equal(result.Program.Unit.Variables[1].Name, Assert.IsType<Ir.Variable>(arithmetic.Arguments[1]).Name);
  }

  [Theory]
  [InlineData("div", Ir.Operator.Divide)]
  [InlineData("mod", Ir.Operator.Modulo)]
  public void AnInlineTypedBodyUsesItsFormalsRatherThanItsName(string operation, Ir.Operator expected) {
    var options = Options();
    var source = ParseBoogie($"function {{:inline}} DifferentName(x: int, y: int): int {{ x {operation} y }} " +
      "procedure P(); implementation P() { assert DifferentName(5, 2) == 0; }", options);
    Assert.NotNull(source.TopLevelDeclarations.OfType<Bpl.Function>().Single().Body);
    var before = Emit(source, options);
    var result = Normalize(source, options);
    Validate(result);
    Assert.Equal(before, Emit(source, options));
    var arithmetic = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(Checks(result).Single().Condition).Arguments[0]);
    Assert.Equal(expected, arithmetic.Operator);
    Assert.Equal(new Ir.IntegerLiteral("5"), arithmetic.Arguments[0]);
    Assert.Equal(new Ir.IntegerLiteral("2"), arithmetic.Arguments[1]);
    Assert.Empty(result.Program!.Functions);
    Assert.Empty(result.Program.Axioms);
  }

  [Fact]
  public void ActualTypedBodyNeedsNoInlineAttribute() {
    var options = Options();
    var source = ParseBoogie("function F(x: int, y: int): int; procedure P(); implementation P() { assert F(5, 2) == 0; }", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    function.Body = Bpl.Expr.Binary(Bpl.BinaryOperator.Opcode.Div,
      new Bpl.IdentifierExpr(function.tok, function.InParams[0]),
      new Bpl.IdentifierExpr(function.tok, function.InParams[1]));
    Assert.Equal(0, source.Typecheck(options));
    var result = Normalize(source, options);
    Validate(result);
    var arithmetic = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(Checks(result).Single().Condition).Arguments[0]);
    Assert.Equal(Ir.Operator.Divide, arithmetic.Operator);
  }

  [Theory]
  [InlineData("Div")]
  [InlineData("Mod")]
  [InlineData("INTERNAL_div_boogie")]
  public void APrimitiveLookingNameCannotDefineAnOperation(string name) {
    var result = Boogie($"function {name}(x: int, y: int): int; procedure P(); implementation P() {{ assert {name}(5, 2) == 0; }}");
    Validate(result);
    var equality = Assert.IsType<Ir.Operation>(Checks(result).Single().Condition);
    Assert.IsType<Ir.Application>(equality.Arguments[0]);
    Assert.Empty(result.Program!.Axioms);
  }

  [Theory]
  [InlineData("y div x")]
  [InlineData("x div x")]
  [InlineData("x div 2")]
  [InlineData("y mod x")]
  public void ADirectBodyMustUseTheExactOrderedFormalDeclarations(string body) {
    var result = Boogie($"function {{:inline}} F(x: int, y: int): int {{ {body} }} " +
      "procedure P(); implementation P() { assert F(5, 2) == 0; }");
    Unsupported(result);
  }

  [Theory]
  [InlineData("div", Ir.Operator.Divide)]
  [InlineData("mod", Ir.Operator.Modulo)]
  public void ZeroDivisorsDoNotSynthesizeAnAssumptionOrCheck(string operation, Ir.Operator expected) {
    var result = Boogie($"procedure P(x: int); implementation P(x: int) {{ assert x {operation} 0 == 0; assert false; }}");
    Validate(result);
    var checks = Checks(result).ToArray();
    Assert.Equal(2, checks.Length);
    Assert.Equal(2, result.Obligations.Count);
    Assert.DoesNotContain(Statements(result.Program!.Unit.Body), statement => statement is Ir.Assume);
    var arithmetic = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(checks[0].Condition).Arguments[0]);
    Assert.Equal(expected, arithmetic.Operator);
    Assert.Equal(new Ir.IntegerLiteral("0"), arithmetic.Arguments[1]);
    Assert.Equal(new Ir.BooleanLiteral(false), checks[1].Condition);
  }

  [Fact]
  public async Task ActualDafnyNativeOperatorsRetainTheirExistingDivisorObligations() {
    var results = await Dafny("method P(x: int, y: int) { assert x / y == x; assert x % y == x; }", 0);
    Assert.NotEmpty(results);
    Assert.All(results, Validate);
    var expressions = results.SelectMany(result => Checks(result)).SelectMany(check => Expressions(check.Condition)).ToArray();
    Assert.Contains(expressions, expression => expression is Ir.Operation { Operator: Ir.Operator.Divide });
    Assert.Contains(expressions, expression => expression is Ir.Operation { Operator: Ir.Operator.Modulo });
    Assert.Equal(2, results.SelectMany(result => result.Obligations).Count(source => source.Description == "possible division by zero"));
  }

  [Fact]
  public void PowerStillFailsClosed() {
    Unsupported(Boogie("procedure P(); implementation P() { assert 2.0 ** 2.0 == 4.0; }"), "b3_arithmetic");
  }

  [Theory]
  [InlineData("div", Ir.Operator.Divide)]
  [InlineData("mod", Ir.Operator.Modulo)]
  public void AnActiveUniversalDefinitionUsesTheExactOrderedBinders(string operation, Ir.Operator expected) {
    var options = Options();
    var source = ParseBoogie($"revealed function F(x: int, y: int): int {{ x {operation} y }} " +
      "procedure P(); implementation P() { assert F(5, 2) == 0; }", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    Assert.Null(function.Body);
    Assert.NotNull(function.DefinitionAxiom);
    Assert.Contains(function.DefinitionAxiom, source.TopLevelDeclarations);
    var before = Emit(source, options);
    var result = Normalize(source, options);
    Validate(result);
    Assert.Equal(before, Emit(source, options));
    var arithmetic = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(Checks(result).Single().Condition).Arguments[0]);
    Assert.Equal(expected, arithmetic.Operator);
    Assert.Equal(new Ir.IntegerLiteral("5"), arithmetic.Arguments[0]);
    Assert.Equal(new Ir.IntegerLiteral("2"), arithmetic.Arguments[1]);
    Assert.Empty(result.Program!.Functions);
    Assert.Empty(result.Program.Axioms);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void DetachedMetadataCannotBorrowAnEquivalentAxiomObject(bool keepEquivalentAxiom) {
    var options = Options();
    var source = ParseBoogie("revealed function F(x: int, y: int): int { x div y } " +
      "procedure P(); implementation P() { assert F(5, 2) == 0; }", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    var definition = function.DefinitionAxiom;
    Assert.NotNull(definition);
    source.RemoveTopLevelDeclaration(definition);
    if (keepEquivalentAxiom) { source.AddTopLevelDeclaration(new Bpl.Axiom(definition.tok, definition.Expr)); }
    Assert.Same(definition, function.DefinitionAxiom);
    Unsupported(Normalize(source, options));
  }

  [Fact]
  public void AnUnlinkedActiveAxiomLeavesTheFunctionOpaque() {
    var options = Options();
    var source = ParseBoogie("revealed function F(x: int, y: int): int { x div y } " +
      "procedure P(); implementation P() { assert F(5, 2) == 0; }", options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().DefinitionAxiom = null;
    Opaque(Normalize(source, options));
  }

  [Theory]
  [InlineData("forall x, y: int :: y != 0 ==> F(x, y) == x div y")]
  [InlineData("exists x, y: int :: F(x, y) == x div y")]
  public void AGuardOrExistentialCannotSupplyAUniversalDefinition(string expression) {
    Opaque(DefinitionMetadata(expression));
  }

  [Theory]
  [InlineData("forall x, y, z: int :: F(x, y) == x div y")]
  [InlineData("forall x, y: int :: F(x, x) == x div x")]
  [InlineData("forall x, y: int :: F(y, x) == y div x")]
  [InlineData("forall x, y: int :: F(x, y) == y div x")]
  [InlineData("forall x, y: int :: F(x, y) == x div x")]
  [InlineData("forall x, y: int :: F(x, y) == x div 2")]
  public void TheOrderedBijectionAndBothOperandIdentitiesAreRequired(string expression) {
    Unsupported(DefinitionMetadata(expression));
  }

  [Fact]
  public void AnotherFunctionsAxiomCannotDefineTheCalledFunction() {
    var options = Options();
    var source = ParseBoogie("""
      revealed function F(x: int, y: int): int;
      revealed function G(x: int, y: int): int;
      axiom (forall x, y: int :: G(x, y) == x div y);
      procedure P(); implementation P() { assert F(5, 2) == 0; }
      """, options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single(function => function.Name == "F").DefinitionAxiom =
      source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
    Opaque(Normalize(source, options));
  }

  [Fact]
  public void AnIntegerInstanceCannotDefineAllInstancesOfAGenericFunction() {
    var options = Options();
    var source = ParseBoogie("""
      revealed function F<T>(x: T, y: T): T;
      axiom (forall x, y: int :: F(x, y) == x div y);
      procedure P(); implementation P() { assert F(5, 2) == 0; }
      """, options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().DefinitionAxiom =
      source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
    Unsupported(Normalize(source, options));
  }

  [Fact]
  public void ANonrevealedAxiomDefinitionStaysOutsideTheExpansionBoundary() {
    Unsupported(Boogie("function F(x: int, y: int): int { x div y } " +
      "procedure P(); implementation P() { assert F(5, 2) == 0; }"));
  }

  [Fact]
  public void ABooleanResultIsNotAnIntegerOperation() {
    var result = Boogie("revealed function F(x: int, y: int): bool { x div y == 0 } " +
      "procedure P(); implementation P() { assert F(5, 2); }");
    Validate(result);
    Assert.IsType<Ir.Application>(Checks(result).Single().Condition);
  }

  [Fact]
  public void TwoIntegerInputsAndOneIntegerResultAreRequired() {
    Unsupported(Boogie("function {:inline} F(x: int, y: bool): int { x div x } " +
      "procedure P(); implementation P() { assert F(5, true) == 0; }"));
    Unsupported(Boogie("revealed function F(x: int, y: int, z: int): int { x div y } " +
      "procedure P(); implementation P() { assert F(5, 2, 1) == 0; }"));
  }

  [Fact]
  public void MatchingNamesCannotReplaceFormalDeclarationIdentity() {
    var options = Options();
    var source = ParseBoogie("function {:inline} F(x: int, y: int): int { x div y } " +
      "procedure P(); implementation P() { assert F(5, 2) == 0; }", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    var body = Assert.IsType<Bpl.NAryExpr>(function.Body);
    var detached = new Bpl.LocalVariable(function.tok,
      new Bpl.TypedIdent(function.tok, function.InParams[0].Name, Bpl.Type.Int));
    body.Args[0] = new Bpl.IdentifierExpr(function.tok, detached);
    Assert.Equal(function.InParams[0].Name, detached.Name);
    Assert.NotSame(function.InParams[0], detached);
    Unsupported(Normalize(source, options));
  }

  [Fact]
  public async Task ActualDafnyDefaultArithmeticWrappersAndZeroChecksNormalize() {
    var results = await Dafny("method P(x: int, y: int) { assert x / y == x; assert x % y == x; }");
    Assert.NotEmpty(results);
    Assert.All(results, Validate);
    var expressions = results.SelectMany(result => Checks(result)).SelectMany(check => Expressions(check.Condition)).ToArray();
    Assert.Contains(expressions, expression => expression is Ir.Operation { Operator: Ir.Operator.Divide });
    Assert.Contains(expressions, expression => expression is Ir.Operation { Operator: Ir.Operator.Modulo });
    Assert.Equal(2, results.SelectMany(result => result.Obligations).Count(source => source.Description == "possible division by zero"));
  }

  [Fact]
  public void AFreeConstantCannotReplaceTheDivisorBinder() {
    var options = Options();
    var source = ParseBoogie("const c: int; revealed function F(x: int, y: int): int; " +
      "axiom (forall x, y: int :: F(x, y) == x div c); " +
      "procedure P(); implementation P() { assert F(5, 2) == 0; }", options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().DefinitionAxiom =
      source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
    Unsupported(Normalize(source, options));
  }

  [Theory]
  [InlineData("div", Ir.Operator.Divide)]
  [InlineData("mod", Ir.Operator.Modulo)]
  public void AnActiveDefinitionAtZeroAddsNoNonzeroPremise(string operation, Ir.Operator expected) {
    var result = Boogie($"revealed function F(x: int, y: int): int {{ x {operation} y }} " +
      "procedure P(x: int); implementation P(x: int) { assert F(x, 0) == 0; assert false; }");
    Validate(result);
    Assert.Equal(2, Checks(result).Count());
    Assert.Equal(2, result.Obligations.Count);
    Assert.Empty(result.Program!.Axioms);
    Assert.DoesNotContain(Statements(result.Program.Unit.Body), statement => statement is Ir.Assume);
    var arithmetic = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(Checks(result).First().Condition).Arguments[0]);
    Assert.Equal(expected, arithmetic.Operator);
    Assert.Equal(new Ir.IntegerLiteral("0"), arithmetic.Arguments[1]);
  }

  private static B3NormalizationResult DefinitionMetadata(string expression) {
    var options = Options();
    var source = ParseBoogie("revealed function F(x: int, y: int): int; axiom (" + expression + "); " +
      "procedure P(); implementation P() { assert F(5, 2) == 0; }", options);
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().DefinitionAxiom =
      source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
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
    var parsed = await ProgramParser.Parse(text, new Uri("file:///B3IntegerArithmeticTests.dfy"), reporter);
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
    Assert.Equal(0, Bpl.Parser.Parse(text, "B3IntegerArithmeticTests.bpl", out var source));
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
    var request = new Ir.Request(Ir.Protocol.Version, "integer-arithmetic-test", Ir.Protocol.NormalizerVersion,
      new string('0', 40), Ir.Protocol.GetProgramHash(result.Program!), result.Program!.Unit.Name, result.Program,
      new Ir.Configuration("z3", new[] { "-in", "-smt2" }, 1000, 10000, 100000, 2, "5.1.0", new string('d', 64)), result.Obligations, new string('f', 64));
    Ir.ProtocolValidation.ValidateRequest(request);
  }
}
