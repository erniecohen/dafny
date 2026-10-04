// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Numerics;
using Microsoft.BaseTypes;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3BitvectorNormalizationTests {
  [Theory]
  [InlineData("bvand", "bv3", "bv3", 2, Ir.BitvectorOperator.And)]
  [InlineData("bvor", "bv3", "bv3", 2, Ir.BitvectorOperator.Or)]
  [InlineData("bvxor", "bv3", "bv3", 2, Ir.BitvectorOperator.Xor)]
  [InlineData("bvnot", "bv3", "bv3", 1, Ir.BitvectorOperator.Not)]
  [InlineData("bvadd", "bv3", "bv3", 2, Ir.BitvectorOperator.Add)]
  [InlineData("bvsub", "bv3", "bv3", 2, Ir.BitvectorOperator.Subtract)]
  [InlineData("bvmul", "bv3", "bv3", 2, Ir.BitvectorOperator.Multiply)]
  [InlineData("bvudiv", "bv3", "bv3", 2, Ir.BitvectorOperator.UnsignedDivide)]
  [InlineData("bvurem", "bv3", "bv3", 2, Ir.BitvectorOperator.UnsignedRemainder)]
  [InlineData("bvult", "bv3", "bool", 2, Ir.BitvectorOperator.UnsignedLess)]
  [InlineData("bvule", "bv3", "bool", 2, Ir.BitvectorOperator.UnsignedLessEqual)]
  [InlineData("bvugt", "bv3", "bool", 2, Ir.BitvectorOperator.UnsignedLess)]
  [InlineData("bvuge", "bv3", "bool", 2, Ir.BitvectorOperator.UnsignedLessEqual)]
  [InlineData("bvshl", "bv3", "bv3", 2, Ir.BitvectorOperator.ShiftLeft)]
  [InlineData("bvlshr", "bv3", "bv3", 2, Ir.BitvectorOperator.LogicalShiftRight)]
  [InlineData("(_ int2bv 3)", "int", "bv3", 1, Ir.BitvectorOperator.IntToBitvector)]
  [InlineData("bv2int", "bv3", "int", 1, Ir.BitvectorOperator.BitvectorToUnsignedInt)]
  public void ExactSemanticAttributesAndSortsProduceClosedNativeOperations(string builtin, string input,
    string output, int arity, Ir.BitvectorOperator expected) {
    var result = Boogie(NativeCall(builtin, input, output, arity));
    Validate(result);
    var operation = Assert.IsType<Ir.BitvectorOperation>(Left(result));
    Assert.Equal(expected, operation.Operator);
    Assert.Equal(3, operation.Width);
    Assert.Equal(0, operation.Start); Assert.Equal(0, operation.End);
    Assert.Equal(arity, operation.Arguments.Count);
    Assert.Equal(output == "bv3" ? "#bv3" : output, operation.Type);
    Assert.Empty(result.Program!.Functions); Assert.Empty(result.Program.Types); Assert.Empty(result.Program.Axioms);
    if (builtin is "bvugt" or "bvuge") {
      Assert.Equal(result.Program.Unit.Variables[1].Name, Assert.IsType<Ir.Variable>(operation.Arguments[0]).Name);
      Assert.Equal(result.Program.Unit.Variables[0].Name, Assert.IsType<Ir.Variable>(operation.Arguments[1]).Name);
    }
  }

  [Theory]
  [InlineData(1, "3", "1")]
  [InlineData(3, "8", "0")]
  [InlineData(3, "19", "3")]
  [InlineData(67, "147573952589676412929", "1")]
  public void SourceWordLiteralsUseThePinnedLowBitSemantics(int width, string sourceValue, string expected) {
    var options = Options();
    var source = ParseBoogie($"procedure P(); implementation P() {{ assert 0bv{width} == 0bv{width}; }}", options);
    var assertion = Assertion(source);
    Assert.IsType<Bpl.NAryExpr>(assertion.Expr).Args[0] = new Bpl.LiteralExpr(assertion.tok,
      BigNum.FromBigInt(BigInteger.Parse(sourceValue, CultureInfo.InvariantCulture)), width);
    var before = Emit(source, options);
    var result = Normalize(source, options);
    Validate(result);
    Assert.Equal(new Ir.BitvectorLiteral(expected, width), Left(result));
    Assert.Equal(before, Emit(source, options));
  }

  [Fact]
  public void TheMaximumWidthAndLiteralRetainEveryBit() {
    var value = (BigInteger.One << Ir.Protocol.MaximumBitvectorWidth) - 1;
    var result = Boogie($"procedure P(); implementation P() {{ assert {value}bv4096 == 0bv4096; }}");
    Validate(result);
    Assert.Equal(new Ir.BitvectorLiteral(value.ToString(CultureInfo.InvariantCulture), 4096), Left(result));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [InlineData(4097)]
  [InlineData(int.MaxValue)]
  public void MalformedNativeWidthsNeverBecomeBv0OrOpaqueSorts(int width) {
    var options = Options();
    var source = ParseBoogie("procedure P(); implementation P() { var x: bv3; assert true; }", options);
    source.Implementations.Single().LocVars.Single().TypedIdent.Type = new Bpl.BvType(width);
    Unsupported(Normalize(source, options), "b3_bitvector_width");
  }

  [Fact]
  public void SourceLiteralMagnitudeIsBoundedBeforeCanonicalization() {
    var options = Options();
    var source = ParseBoogie("procedure P(); implementation P() { assert 0bv3 == 0bv3; }", options);
    var assertion = Assertion(source);
    Assert.IsType<Bpl.NAryExpr>(assertion.Expr).Args[0] = new Bpl.LiteralExpr(assertion.tok,
      BigNum.FromBigInt(BigInteger.One << (4 * Ir.Protocol.MaximumIntegerCharacters + 1)), 3);
    Unsupported(Normalize(source, options), "b3_literal_limit");
  }

  [Fact]
  public void AWordLiteralCannotClaimAnUnrelatedResolvedWidth() {
    var options = Options();
    var source = ParseBoogie("procedure P(); implementation P() { assert 0bv3 == 0bv3; }", options);
    var literal = Assert.IsType<Bpl.LiteralExpr>(Assert.IsType<Bpl.NAryExpr>(Assertion(source).Expr).Args[0]);
    literal.Type = Bpl.Type.GetBvType(4);
    Unsupported(Normalize(source, options), "b3_bitvector_literal");
  }

  [Fact]
  public void ResolvedAliasesKeepTheNativeWordSortWithoutSourceMutation() {
    var options = Options();
    var source = ParseBoogie("type W = bv3; function {:bvbuiltin \"bvadd\"} F(x: W,y: W): W; " +
      "procedure P(x: W,y: W); implementation P(x: W,y: W) { assert F(x,y) == x; }", options);
    var before = Emit(source, options);
    var result = Normalize(source, options);
    Validate(result); Assert.Equal(before, Emit(source, options));
    Assert.Equal("#bv3", Assert.IsType<Ir.BitvectorOperation>(Left(result)).Type);
    Assert.Empty(result.Program!.Types);
  }

  [Fact]
  public void ExtractAndConcatPreserveIndicesAndHighLowOrder() {
    var options = Options();
    var source = ParseBoogie("procedure P(x: bv3, y: bv5); implementation P(x: bv3, y: bv5) { assert x == x; }", options);
    var assertion = Assertion(source);
    var implementation = source.Implementations.Single();
    var concat = new Bpl.BvConcatExpr(assertion.tok,
      new Bpl.IdentifierExpr(assertion.tok, implementation.InParams[0]),
      new Bpl.IdentifierExpr(assertion.tok, implementation.InParams[1])) { Type = Bpl.Type.GetBvType(8) };
    var extract = new Bpl.BvExtractExpr(assertion.tok, concat, 7, 2) { Type = Bpl.Type.GetBvType(5) };
    Assert.IsType<Bpl.NAryExpr>(assertion.Expr).Args[0] = extract;
    Assert.IsType<Bpl.NAryExpr>(assertion.Expr).Args[1] = new Bpl.LiteralExpr(assertion.tok, BigNum.ZERO, 5);
    var result = Normalize(source, options);
    Validate(result);
    var normalized = Assert.IsType<Ir.BitvectorOperation>(Left(result));
    Assert.Equal((Ir.BitvectorOperator.Extract, 5, 2, 7), (normalized.Operator, normalized.Width, normalized.Start, normalized.End));
    var pair = Assert.IsType<Ir.BitvectorOperation>(Assert.Single(normalized.Arguments));
    Assert.Equal(Ir.BitvectorOperator.Concat, pair.Operator);
    Assert.Equal(new[] { "#bv3", "#bv5" }, pair.Arguments.Select(argument => argument.Type));
    Assert.Equal(8, pair.Width);
  }

  [Theory]
  [InlineData(-1, 1)]
  [InlineData(0, 0)]
  [InlineData(0, 4)]
  [InlineData(2, 1)]
  public void ExtractExplicitlyChecksBothBoundsAndNonemptyWidth(int start, int end) {
    var options = Options();
    var source = ParseBoogie("procedure P(x: bv3); implementation P(x: bv3) { assert x == x; }", options);
    var assertion = Assertion(source);
    var extract = new Bpl.BvExtractExpr(assertion.tok,
      new Bpl.IdentifierExpr(assertion.tok, source.Implementations.Single().InParams[0]), end, start) { Type = Bpl.Type.GetBvType(2) };
    Assert.IsType<Bpl.NAryExpr>(assertion.Expr).Args[0] = extract;
    Unsupported(Normalize(source, options), "b3_bitvector_extract");
  }

  [Fact]
  public void ConcatMustHaveTheExactSumWidth() {
    var options = Options();
    var source = ParseBoogie("procedure P(x: bv3); implementation P(x: bv3) { assert x == x; }", options);
    var assertion = Assertion(source);
    var x = new Bpl.IdentifierExpr(assertion.tok, source.Implementations.Single().InParams[0]);
    Assert.IsType<Bpl.NAryExpr>(assertion.Expr).Args[0] = new Bpl.BvConcatExpr(assertion.tok, x, x) { Type = Bpl.Type.GetBvType(5) };
    Unsupported(Normalize(source, options), "b3_bitvector_concat");
  }

  [Theory]
  [InlineData("{:bvbuiltin \"bvadd\"}", "int", "int", 2)]
  [InlineData("{:bvbuiltin \"bvnot\"}", "bv3", "bv3", 2)]
  [InlineData("{:bvbuiltin \"bvult\"}", "bv3", "bv3", 2)]
  [InlineData("{:bvbuiltin \"(_ int2bv 4)\"}", "int", "bv3", 1)]
  [InlineData("{:bvbuiltin \"(_ int2bv 03)\"}", "int", "bv3", 1)]
  [InlineData("{:bvbuiltin \"bv2int\"}", "bv3", "real", 1)]
  [InlineData("{:bvbuiltin \"bvashr\"}", "bv3", "bv3", 2)]
  [InlineData("{:bvbuiltin \"ext_rotate_left\"}", "bv3", "bv3", 2)]
  [InlineData("{:bvbuiltin \"ext_rotate_right\"}", "bv3", "bv3", 2)]
  [InlineData("{:bvbuiltin 0}", "bv3", "bv3", 2)]
  [InlineData("{:bvbuiltin \"bvadd\"} {:bvbuiltin \"bvsub\"}", "bv3", "bv3", 2)]
  [InlineData("{:builtin \"bvadd\"} {:builtin \"bvsub\"}", "bv3", "bv3", 2)]
  public void MalformedOrUnreviewedClaimsNeverProduceAPartialProgram(string attributes, string input, string output, int arity) {
    Unsupported(Boogie(NativeCall(null, input, output, arity, attributes)));
  }

  [Fact]
  public void SameWidthCountsAreRequiredForNativeShifts() {
    Unsupported(Boogie("function {:bvbuiltin \"bvshl\"} F(x: bv3, y: int): bv3; " +
      "procedure P(x: bv3, y: int); implementation P(x: bv3, y: int) { assert F(x,y) == x; }"));
  }

  [Theory]
  [InlineData("bvshl", Ir.BitvectorOperator.ShiftLeft)]
  [InlineData("bvlshr", Ir.BitvectorOperator.LogicalShiftRight)]
  public void HugeSameWidthCountsArePreservedWithoutModuloReductionOrNewGuards(string builtin, Ir.BitvectorOperator expected) {
    var result = Boogie($"function {{:bvbuiltin \"{builtin}\"}} F(x: bv67,y: bv67): bv67; " +
      "procedure P(x: bv67); implementation P(x: bv67) { assert F(x,18446744073709551616bv67) == 0bv67; }");
    Validate(result);
    var operation = Assert.IsType<Ir.BitvectorOperation>(Left(result));
    Assert.Equal(expected, operation.Operator);
    Assert.Equal(new Ir.BitvectorLiteral("18446744073709551616", 67), operation.Arguments[1]);
    Assert.DoesNotContain(Statements(result.Program!.Unit.Body), statement => statement is Ir.Assume);
  }

  [Fact]
  public void APrimitiveIntConversionPreservesNegativeIntegersWithoutInventingSourceChecks() {
    var result = Boogie("function {:bvbuiltin \"(_ int2bv 3)\"} F(x: int): bv3; " +
      "procedure P(); implementation P() { assert F(-1) == 7bv3; assert false; }");
    Validate(result);
    var operation = Assert.IsType<Ir.BitvectorOperation>(Assert.IsType<Ir.Operation>(Checks(result).First().Condition).Arguments[0]);
    Assert.Equal(Ir.BitvectorOperator.IntToBitvector, operation.Operator);
    Assert.Equal("int", Assert.Single(operation.Arguments).Type);
    Assert.Equal(2, result.Obligations.Count);
    Assert.DoesNotContain(Statements(result.Program!.Unit.Body), statement => statement is Ir.Assume);
  }

  [Theory]
  [InlineData("{:builtin \"bvadd\"}", Ir.BitvectorOperator.Add)]
  [InlineData("{:bvbuiltin \"bvsub\"} {:builtin \"bvadd\"}", Ir.BitvectorOperator.Subtract)]
  public void PinnedEffectiveAttributePriorityIsPreserved(string attributes, Ir.BitvectorOperator expected) {
    var result = Boogie(NativeCall(null, "bv3", "bv3", 2, attributes));
    Validate(result);
    Assert.Equal(expected, Assert.IsType<Ir.BitvectorOperation>(Left(result)).Operator);
  }

  [Fact]
  public void ADetachedResolvedFunctionCannotClaimNativeSemantics() {
    var options = Options();
    var source = ParseBoogie(NativeCall("bvadd", "bv3", "bv3", 2), options);
    source.RemoveTopLevelDeclaration(source.TopLevelDeclarations.OfType<Bpl.Function>().Single());
    Unsupported(Normalize(source, options), "b3_bitvector_function");
  }

  [Fact]
  public void DuplicateFormalObjectsCannotSupplyTwoArguments() {
    var options = Options();
    var source = ParseBoogie(NativeCall("bvadd", "bv3", "bv3", 2), options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    function.InParams[1] = function.InParams[0];
    Unsupported(Normalize(source, options), "b3_bitvector_function");
  }

  [Fact]
  public void NativeLookingNamesAndDetachedDefinitionMetadataStayOpaque() {
    var options = Options();
    var source = ParseBoogie("function smt_nat_from_bv3(x: bv3): int; " +
      "axiom (forall x: bv3 :: smt_nat_from_bv3(x) == 0); " +
      "procedure P(x: bv3); implementation P(x: bv3) { assert smt_nat_from_bv3(x) == 0; }", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    function.DefinitionAxiom = source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
    source.RemoveTopLevelDeclaration(function.DefinitionAxiom);
    var result = Normalize(source, options);
    Validate(result);
    Assert.IsType<Ir.Application>(Left(result));
    Assert.Empty(result.Program!.Axioms);
  }

  [Fact]
  public void AnOrdinaryGenericClosedWordInstanceKeepsTheExistingOpaqueRoute() {
    var result = Boogie("function Unknown<T>(x: T): T; " +
      "procedure P(x: bv3); implementation P(x: bv3) { assert Unknown(x) == x; }");
    Validate(result); Assert.IsType<Ir.Application>(Left(result)); Assert.Empty(result.Program!.Axioms);
  }

  [Fact]
  public void TheActualDafnyUnsignedWrapperIsNotInferredFromAnActiveLookingEquality() {
    var result = Boogie("function {:bvbuiltin \"bv2int\"} Native(x: bv3): int; function nat_from_bv3(x: bv3): int; " +
      "axiom (forall x: bv3 :: { nat_from_bv3(x) } 0 <= nat_from_bv3(x) && nat_from_bv3(x) < 8 && nat_from_bv3(x) == Native(x)); " +
      "procedure P(x: bv3); implementation P(x: bv3) { assert nat_from_bv3(x) == 0; }");
    Validate(result); Assert.IsType<Ir.Application>(Left(result)); Assert.Empty(result.Program!.Axioms);
  }

  [Fact]
  public void ActualBodyTakesPrecedenceOverANativeLookingAttribute() {
    var result = Boogie("function {:inline} {:bvbuiltin \"bvadd\"} F(x: bv3,y: bv3): bv3 { 3bv3 } " +
      "procedure P(x: bv3,y: bv3); implementation P(x: bv3,y: bv3) { assert F(x,y) == 3bv3; }");
    Validate(result);
    Assert.Equal(new Ir.BitvectorLiteral("3", 3), Left(result));
    Assert.Empty(result.Program!.Functions);
  }

  [Fact]
  public void ActualBodyCompositionUsesTheExactFormalObjectsAndNativeDeclarations() {
    var options = Options();
    var source = ParseBoogie("function {:bvbuiltin \"bvsub\"} Sub(x: bv3,y: bv3): bv3; " +
      "function {:inline} {:bvbuiltin \"bvadd\"} F(x: bv3,y: bv3): bv3 { Sub(y,x) } " +
      "procedure P(x: bv3,y: bv3); implementation P(x: bv3,y: bv3) { assert F(x,y) == x; }", options);
    var before = Emit(source, options);
    var result = Normalize(source, options);
    Validate(result);
    var operation = Assert.IsType<Ir.BitvectorOperation>(Left(result));
    Assert.Equal(Ir.BitvectorOperator.Subtract, operation.Operator);
    Assert.Equal(result.Program!.Unit.Variables[1].Name, Assert.IsType<Ir.Variable>(operation.Arguments[0]).Name);
    Assert.Equal(result.Program.Unit.Variables[0].Name, Assert.IsType<Ir.Variable>(operation.Arguments[1]).Name);
    Assert.Equal(before, Emit(source, options));
  }

  [Fact]
  public void ABodyCannotBorrowAMatchingFormalNameFromADifferentObject() {
    var options = Options();
    var source = ParseBoogie("function {:inline} {:bvbuiltin \"bvadd\"} F(x: bv3): bv3 { x } " +
      "procedure P(x: bv3); implementation P(x: bv3) { assert F(x) == x; }", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    function.Body = new Bpl.IdentifierExpr(function.tok,
      new Bpl.LocalVariable(function.tok, new Bpl.TypedIdent(function.tok, "x", Bpl.Type.GetBvType(3))));
    Unsupported(Normalize(source, options), "b3_bitvector_body");
  }

  [Fact]
  public void CapturesAndUnreviewedBodiesCannotBeOverriddenByAttributes() {
    Unsupported(Boogie("const captured: bv3; function {:inline} {:bvbuiltin \"bvadd\"} F(x: bv3): bv3 { captured } " +
      "procedure P(x: bv3); implementation P(x: bv3) { assert F(x) == x; }"), "b3_bitvector_body");
  }

  [Fact]
  public void AnUnreviewedConditionalBodyCannotFallBackToItsNativeAttribute() {
    Unsupported(Boogie("function {:inline} {:bvbuiltin \"bvadd\"} F(x: bv3): bv3 { if true then x else 0bv3 } " +
      "procedure P(x: bv3); implementation P(x: bv3) { assert F(x) == x; }"), "b3_bitvector_body");
  }

  [Fact]
  public void CyclicActualBodiesFailClosedWithinTheExpansionBound() {
    var options = Options();
    var source = ParseBoogie("function {:inline} F(x: bv3): bv3 { x } " +
      "procedure P(x: bv3); implementation P(x: bv3) { assert F(x) == x; }", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    function.Body = new Bpl.NAryExpr(function.tok, new Bpl.FunctionCall(function),
      new List<Bpl.Expr> { new Bpl.IdentifierExpr(function.tok, function.InParams[0]) }) { Type = Bpl.Type.GetBvType(3) };
    Unsupported(Normalize(source, options), "b3_bitvector_body");
  }

  [Theory]
  [InlineData("int", "0", "int", "0")]
  [InlineData("bool", "true", "bool", "true")]
  [InlineData("bool", "false", "bool", "false")]
  public void IntAliasLiteralBodiesDoNotDependOnBv0NamesOrAttributes(string output, string body, string type, string expected) {
    var options = Options();
    var source = ParseBoogie($"function {{:inline}} ArbitraryName(x: int): {output} {{ {body} }} " +
      $"procedure P(x: int); implementation P(x: int) {{ assert ArbitraryName(x) == {body}; }}", options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    function.Attributes = null; function.AlwaysRevealed = false;
    var result = Normalize(source, options);
    Validate(result);
    Assert.Equal(type, Left(result).Type);
    if (type == "int") { Assert.Equal(new Ir.IntegerLiteral(expected), Left(result)); }
    else { Assert.Equal(new Ir.BooleanLiteral(bool.Parse(expected)), Left(result)); }
    Assert.Equal("int", Assert.Single(result.Program!.Unit.Variables).Type);
    Assert.Empty(result.Program.Functions); Assert.Empty(result.Program.Axioms);
  }

  [Theory]
  [InlineData(1022, true)]
  [InlineData(1023, false)]
  public void CompleteOutputChargesDeclarationsAndEachExpressionOccurrence(int variables, bool accepted) {
    var locals = string.Join(",", Enumerable.Range(0, variables).Select(index => "x" + index));
    var result = Boogie($"procedure P(); implementation P() {{ var {locals}: bv4096; assert x0 == x0; }}");
    if (accepted) { Validate(result); } else { Unsupported(result, "b3_bitvector_limit"); }
  }

  [Theory]
  [InlineData(1023, true)]
  [InlineData(1024, false)]
  public void HavocTargetsCountEvenWithoutWordExpressions(int repeats, bool accepted) {
    var result = Boogie("procedure P(); implementation P() { var x: bv4096; " +
      string.Concat(Enumerable.Repeat("havoc x;", repeats)) + " assert true; }");
    if (accepted) { Validate(result); } else { Unsupported(result, "b3_bitvector_limit"); }
  }

  [Fact]
  public async Task ActualDafnyKeepsNativeWordsAndOriginalDivisorAndShiftGuards() {
    var results = await Dafny("""
      method Native(x: bv3, y: bv3, n: nat) {
        var a := x + y; var b := x - y; var c := x * y;
        var d := x & y; var e := x | y; var f := x ^ y; var g := !x;
        var q := x / y; var r := x % y;
        var left := x << n; var right := x >> n;
        assert x < y || x >= y;
      }
      method Zero() { var q := (1 as bv3) / (0 as bv3); }
      """);
    Assert.All(results, Validate);
    var operators = results.SelectMany(AllExpressions)
      .OfType<Ir.BitvectorOperation>().Select(operation => operation.Operator).ToHashSet();
    Assert.Contains(Ir.BitvectorOperator.UnsignedDivide, operators);
    Assert.Contains(Ir.BitvectorOperator.IntToBitvector, operators);
    Assert.Contains(Ir.BitvectorOperator.UnsignedLess, operators);
    Assert.Contains(results.SelectMany(result => result.Obligations), source => source.Description.Contains("possible division by zero"));
    Assert.Contains(results.SelectMany(result => result.Obligations), source => source.Description.Contains("shift"));
    Assert.All(results, result => Assert.Empty(result.Program!.Axioms));
  }

  [Fact]
  public async Task ActualBv0RetainsIntAndTheConversionGuardRatherThanAssumingZero() {
    var results = await Dafny("""
      method Alias(x: bv0) { var sum := x + x; var bits := !x; assert sum == x; assert bits == x; }
      method Bad(i: int) { var x := i as bv0; assert false; }
      """);
    Assert.All(results, Validate);
    Assert.All(results.SelectMany(result => result.Program!.Unit.Variables), binding => Assert.DoesNotContain("#bv0", binding.Type));
    Assert.Contains(results.SelectMany(result => result.Obligations), source => source.Description.Contains("might not fit in bv0"));
    Assert.Contains(results.SelectMany(Checks), check => check.Condition is Ir.BooleanLiteral { Value: false });
  }

  private static string NativeCall(string? builtin, string input, string output, int arity, string? attributes = null) {
    attributes ??= "{:bvbuiltin \"" + builtin + "\"}";
    var formals = arity == 1 ? $"x: {input}" : $"x: {input}, y: {input}";
    var actuals = arity == 1 ? "x" : "x,y";
    return $"function {attributes} MisleadingName({formals}): {output}; " +
      $"procedure P({formals}); implementation P({formals}) {{ assert MisleadingName({actuals}) == MisleadingName({actuals}); }}";
  }

  private static DafnyOptions Options() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    return options;
  }
  private static Bpl.Program ParseBoogie(string text, DafnyOptions options) {
    Assert.Equal(0, Bpl.Parser.Parse(text, "B3BitvectorNormalizationTests.bpl", out var source));
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    return source;
  }
  private static Bpl.AssertCmd Assertion(Bpl.Program source) =>
    source.Implementations.Single().StructuredStmts!.BigBlocks[0].simpleCmds.OfType<Bpl.AssertCmd>().Single();
  private static B3NormalizationResult Normalize(Bpl.Program source, DafnyOptions options) =>
    B3Normalizer.Normalize(source, source.Implementations.Single(), options);
  private static B3NormalizationResult Boogie(string text) {
    var options = Options(); return Normalize(ParseBoogie(text, options), options);
  }
  private static string Emit(Bpl.Program source, DafnyOptions options) {
    using var output = new StringWriter(); using var writer = new Bpl.TokenTextWriter(output, options);
    source.Emit(writer); return output.ToString();
  }
  private static async Task<List<B3NormalizationResult>> Dafny(string text) {
    Microsoft.Dafny.Type.ResetScopes();
    var options = Options(); var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(text, new Uri("file:///B3BitvectorNormalizationTests.dfy"), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(message => message.Message)));
    var results = new List<B3NormalizationResult>();
    foreach (var (_, source) in BoogieGenerator.Translate(parsed.Program, reporter)) {
      Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
      foreach (var implementation in source.Implementations) { results.Add(B3Normalizer.Normalize(source, implementation, options)); }
    }
    Assert.NotEmpty(results); Assert.False(reporter.HasErrors); return results;
  }
  private static IEnumerable<Ir.Statement> Statements(Ir.Statement statement) {
    yield return statement;
    IEnumerable<Ir.Statement> children = statement switch {
      Ir.Block block => block.Statements, Ir.Choice choice => choice.Branches,
      Ir.Conditional conditional => new[] { conditional.Then, conditional.Else },
      Ir.Loop loop => new[] { loop.Body }, Ir.Labeled labeled => new[] { labeled.Body },
      _ => Array.Empty<Ir.Statement>()
    };
    foreach (var child in children.SelectMany(Statements)) { yield return child; }
  }
  private static IEnumerable<Ir.Check> Checks(B3NormalizationResult result) => Statements(result.Program!.Unit.Body).OfType<Ir.Check>();
  private static IEnumerable<Ir.Expression> AllExpressions(B3NormalizationResult result) =>
    Statements(result.Program!.Unit.Body).SelectMany(statement => statement switch {
      Ir.Assign assign => new[] { assign.Value }, Ir.Check check => new[] { check.Condition },
      Ir.Assume assume => new[] { assume.Condition }, Ir.Conditional conditional => new[] { conditional.Condition },
      _ => Array.Empty<Ir.Expression>()
    }).SelectMany(Expressions);
  private static Ir.Expression Left(B3NormalizationResult result) => Assert.IsType<Ir.Operation>(Checks(result).Single().Condition).Arguments[0];
  private static IEnumerable<Ir.Expression> Expressions(Ir.Expression expression) {
    yield return expression;
    IEnumerable<Ir.Expression> children = expression switch {
      Ir.Operation operation => operation.Arguments, Ir.BitvectorOperation operation => operation.Arguments,
      Ir.Application application => application.Arguments, Ir.Label label => new[] { label.Body },
      Ir.Quantifier quantifier => quantifier.Patterns.SelectMany(pattern => pattern).Append(quantifier.Body),
      Ir.Let let => new[] { let.Value, let.Body }, _ => Array.Empty<Ir.Expression>()
    };
    foreach (var child in children.SelectMany(Expressions)) { yield return child; }
  }
  private static void Unsupported(B3NormalizationResult result, string? code = null) {
    Assert.False(result.Success); Assert.Null(result.Program); Assert.Empty(result.Obligations);
    Assert.Contains(result.Diagnostics, diagnostic => code == null ? diagnostic.Code.StartsWith("b3_bitvector", StringComparison.Ordinal) : diagnostic.Code == code);
  }
  private static void Validate(B3NormalizationResult result) {
    Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
    var request = new Ir.Request(Ir.Protocol.Version, "bitvector-normalization-test", Ir.Protocol.NormalizerVersion,
      new string('0', 40), Ir.Protocol.GetProgramHash(result.Program!), result.Program!.Unit.Name, result.Program,
      new Ir.Configuration("z3", new[] { "-in", "-smt2" }, 1000, 10000, 100000, 2, "5.1.0", new string('d', 64)), result.Obligations, new string('f', 64));
    Ir.ProtocolValidation.ValidateRequest(request);
  }
}
