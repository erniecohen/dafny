using System.Numerics;
using System.Text.Json;
using DafnyB3Protocol;
using Xunit;
using Program = DafnyB3Protocol.Program;

namespace DafnyB3Protocol.Test;

public sealed class BitvectorProtocolTests {
  private static BitvectorLiteral Word(string value = "0", int width = 7) => new(value, width);
  private static BitvectorOperation Op(BitvectorOperator op, int width, string type, params Expression[] args) =>
    new(op, width, 0, 0, type, args);
  private static Request Request(Expression expression) => Request(new Program(
    Array.Empty<string>(), Array.Empty<Function>(), Array.Empty<Axiom>(),
    new Unit("sP0", Array.Empty<Binding>(), new Check("sO0",
      new Operation(Operator.Equal, "bool", new[] { expression, expression }), false))));
  private static Request Request(Program program) => new(Protocol.Version, "bitvector-protocol-test",
    Protocol.NormalizerVersion, new string('a', 40), Protocol.GetProgramHash(program), program.Unit.Name, program,
    new Configuration("z3", new[] { "-in", "-smt2" }, 1000, 10000, 100000, 2, "5.1.0", new string('d', 64)),
    new[] { new SourceIdentity("sO0", "program.dfy", 1, 1, "assertion") }, new string('f', 64));

  [Fact]
  public void ExactWideWordAndIndexedOperationRoundTrip() {
    var extract = new BitvectorOperation(BitvectorOperator.Extract, 3, 2, 5, "#bv3",
      new Expression[] { Word("73786976294838206464", 67) });
    var request = Request(extract);
    var restored = JsonSerializer.Deserialize<Request>(JsonSerializer.Serialize(request, Protocol.JsonOptions), Protocol.JsonOptions)!;
    ProtocolValidation.ValidateRequest(restored);
    Assert.Equal(request.ProgramHash, Protocol.GetProgramHash(restored.Program));
    var equality = Assert.IsType<Operation>(Assert.IsType<Check>(restored.Program.Unit.Body).Condition);
    var operation = Assert.IsType<BitvectorOperation>(equality.Arguments[0]);
    Assert.Equal(BitvectorOperator.Extract, operation.Operator);
    Assert.Equal(3, operation.Width);
    Assert.Equal(2, operation.Start);
    Assert.Equal(5, operation.End);
    Assert.Equal("#bv3", operation.Type);
    var literal = Assert.IsType<BitvectorLiteral>(Assert.Single(operation.Arguments));
    Assert.Equal(Word("73786976294838206464", 67), literal);
    Assert.Equal(BigInteger.One << 66, ProtocolValidation.ParseBitvectorLiteral(literal));
  }

  [Theory]
  [InlineData(1)]
  [InlineData(3)]
  [InlineData(7)]
  [InlineData(67)]
  [InlineData(4096)]
  public void NativeWidthNamesAndFullRangeAreExact(int width) {
    var type = Protocol.BitvectorTypeName(width);
    Assert.True(ProtocolValidation.TryBitvectorWidth(type, out var parsed));
    Assert.Equal(width, parsed);
    var maximum = (BigInteger.One << width) - 1;
    var word = Word(maximum.ToString(System.Globalization.CultureInfo.InvariantCulture), width);
    ProtocolValidation.ValidateRequest(Request(word));
    Assert.Equal(maximum, ProtocolValidation.ParseBitvectorLiteral(word));
  }

  [Theory]
  [InlineData("#bv0")]
  [InlineData("#bv01")]
  [InlineData("#bv-1")]
  [InlineData("#bv+7")]
  [InlineData("#bv4097")]
  [InlineData("#bv10000000000000000000")]
  [InlineData("#bv7x")]
  public void NoncanonicalOrUnboundedWidthCannotBecomeANativeSort(string type) {
    Assert.False(ProtocolValidation.TryBitvectorWidth(type, out _));
    var request = Request(Word());
    var program = request.Program with { Unit = request.Program.Unit with { Variables = new[] { new Binding("sX", type) } } };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(program)));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [InlineData(4097)]
  [InlineData(int.MinValue)]
  [InlineData(int.MaxValue)]
  public void LiteralWidthIsRejectedBeforeAnyPowerOrShift(int width) {
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ParseBitvectorLiteral(Word("0", width)));
  }

  [Theory]
  [InlineData("")]
  [InlineData("-1")]
  [InlineData("+1")]
  [InlineData("01")]
  [InlineData(" 1")]
  [InlineData("1.0")]
  [InlineData("(assert false)")]
  [InlineData("128")]
  public void WordNumeralMustBeCanonicalUnsignedAndFitTheWidth(string value) {
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(Word(value))));
  }

  [Fact]
  public void AWordLiteralCannotForgeItsResultSort() {
    var word = Word("1", 7) with { Type = "#bv3" };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(word)));
  }

  [Fact]
  public void WordNumeralsHaveABoundIndependentOfTheirClaimedWidth() {
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ParseBitvectorLiteral(
      Word(new string('9', Protocol.MaximumBitvectorLiteralCharacters + 1), 4096)));
  }

  public static IEnumerable<object[]> PrimitiveSignatures() {
    Expression x = Word("127"), y = Word("0"), narrow = Word("0", 3), i = new IntegerLiteral("-1");
    var sameWord = new[] { BitvectorOperator.And, BitvectorOperator.Or, BitvectorOperator.Xor,
      BitvectorOperator.Add, BitvectorOperator.Subtract, BitvectorOperator.Multiply,
      BitvectorOperator.UnsignedDivide, BitvectorOperator.UnsignedRemainder,
      BitvectorOperator.ShiftLeft, BitvectorOperator.LogicalShiftRight };
    foreach (var op in sameWord) {
      yield return Row(Op(op, 7, "#bv7", x, y), true);
      yield return Row(Op(op, 7, "#bv7", x, narrow), false);
    }
    yield return Row(Op(BitvectorOperator.Not, 7, "#bv7", x), true);
    yield return Row(Op(BitvectorOperator.Not, 7, "#bv7", x, y), false);
    yield return Row(Op(BitvectorOperator.UnsignedLess, 7, "bool", x, y), true);
    yield return Row(Op(BitvectorOperator.UnsignedLessEqual, 7, "bool", x, y), true);
    yield return Row(Op(BitvectorOperator.UnsignedLess, 7, "#bv7", x, y), false);
    yield return Row(Op(BitvectorOperator.UnsignedLessEqual, 7, "bool", x, narrow), false);
    yield return Row(Op(BitvectorOperator.IntToBitvector, 7, "#bv7", i), true);
    yield return Row(Op(BitvectorOperator.IntToBitvector, 7, "#bv7", new RationalLiteral("1", "1")), false);
    yield return Row(Op(BitvectorOperator.BitvectorToUnsignedInt, 7, "int", x), true);
    yield return Row(Op(BitvectorOperator.BitvectorToUnsignedInt, 7, "int", narrow), false);
    yield return Row(new BitvectorOperation(BitvectorOperator.Extract, 3, 2, 5, "#bv3", new[] { x }), true);
    yield return Row(new BitvectorOperation(BitvectorOperator.Extract, 3, 5, 8, "#bv3", new[] { x }), false);
    yield return Row(new BitvectorOperation(BitvectorOperator.Extract, 2, 2, 5, "#bv2", new[] { x }), false);
    yield return Row(new BitvectorOperation(BitvectorOperator.Extract, 3, -1, 2, "#bv3", new[] { x }), false);
    yield return Row(Op(BitvectorOperator.Concat, 7, "#bv7", narrow, Word("0", 4)), true);
    yield return Row(Op(BitvectorOperator.Concat, 7, "#bv7", narrow, narrow), false);
    yield return Row(Op(BitvectorOperator.Add, 7, "#bv7", x), false);
    yield return Row(Op(BitvectorOperator.And, 0, "#bv7", x, y), false);
    yield return Row(Op(BitvectorOperator.ShiftLeft, 7, "#bv7", x, new IntegerLiteral("7")), false);
    yield return Row(new BitvectorOperation(BitvectorOperator.Add, 7, 1, 1, "#bv7", new[] { x, y }), false);
  }
  private static object[] Row(BitvectorOperation operation, bool valid) => new object[] { operation, valid };

  [Theory]
  [MemberData(nameof(PrimitiveSignatures))]
  public void EachTypedPrimitiveRequiresItsExactWidthAndSignature(BitvectorOperation operation, bool valid) {
    if (valid) { ProtocolValidation.ValidateRequest(Request(operation)); }
    else { Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(operation))); }
  }

  [Fact]
  public void ArbitraryOpcodeAndSMTTextCannotCrossTheTypedBoundary() {
    var unknown = Op((BitvectorOperator)int.MaxValue, 7, "#bv7", Word(), Word());
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateBitvectorOperation(unknown));
    var text = JsonSerializer.Serialize(Request(Op(BitvectorOperator.Add, 7, "#bv7", Word(), Word())), Protocol.JsonOptions);
    Assert.Contains("\"Add\"", text);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Request>(text.Replace("\"Add\"", "\"(_ rotate_left 1)\""), Protocol.JsonOptions));
  }

  [Theory]
  [InlineData("#bv7")]
  [InlineData("#bv01")]
  public void NativeNamespaceCannotBeDeclaredAsAnOpaqueType(string type) {
    var request = Request(Word());
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(request.Program with { Types = new[] { type } })));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void AggregateBitBudgetIncludesUnusedFunctionParametersResultsAndUnitBindings(bool nativeResult) {
    var request = Request(new BooleanLiteral(true));
    var parameters = Enumerable.Range(0, nativeResult ? 1023 : 1024).Select(i => new Binding("sP" + i, "#bv4096")).ToArray();
    var program = request.Program with { Functions = new[] { new Function("sF", parameters, nativeResult ? "#bv4096" : "bool") } };
    ProtocolValidation.ValidateRequest(Request(program)); // Exactly the admitted aggregate ceiling.
    program = program with { Unit = program.Unit with { Variables = new[] { new Binding("sX", "#bv1") } } };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(program)));
  }

  [Fact]
  public void QuantifiedBindingsCannotHideAggregateNativeCost() {
    var request = Request(new BooleanLiteral(true));
    var variables = Enumerable.Range(0, 1024).Select(i => new Binding("sP" + i, "#bv4096")).ToArray();
    var condition = new Quantifier(true, new[] { new Binding("sQ", "#bv1") },
      Array.Empty<IReadOnlyList<Expression>>(), new BooleanLiteral(true));
    var program = request.Program with { Unit = new Unit("sP0", variables, new Check("sO0", condition, false)) };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(program)));
  }

  [Fact]
  public void RepeatedExpressionResultsCountEvenWithoutBindings() {
    var condition = new Operation(Operator.Equal, "bool", new Expression[] { Word("0", 4096), Word("0", 4096) });
    var request = Request(new BooleanLiteral(true));
    var checks = Enumerable.Repeat<Statement>(new Check("sO0", condition, false), 513).ToArray();
    var program = request.Program with { Unit = new Unit("sP0", Array.Empty<Binding>(), new Block(checks)) };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(program)));
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void AssignmentAndHavocTargetsCountTowardTheAggregate(bool assign) {
    var request = Request(new BooleanLiteral(true));
    var variables = Enumerable.Range(0, assign ? 1023 : 1024).Select(i => new Binding("sP" + i, "#bv4096")).ToArray();
    Statement update = assign ? new Assign("sP0", Word("0", 4096)) : new Havoc(new[] { "sP0" });
    var program = request.Program with { Unit = new Unit("sP0", variables,
      new Block(new Statement[] { update, new Check("sO0", new BooleanLiteral(true), false) })) };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(program)));
  }

  [Theory]
  [InlineData(Operator.Add)]
  [InlineData(Operator.Divide)]
  [InlineData(Operator.RealDivide)]
  public void NumericOperatorsNeverSilentlyPromoteWords(Operator op) {
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(
      new Operation(op, "#bv7", new Expression[] { Word(), Word() }))));
  }
}
