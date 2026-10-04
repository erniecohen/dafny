using System.Numerics;
using System.Text.Json;
using DafnyB3Protocol;
using Xunit;
using Program = DafnyB3Protocol.Program;

namespace DafnyB3Protocol.Test;

public class RealProtocolTests {
  private static Request Request(Expression expression) {
    var program = new Program(Array.Empty<string>(), Array.Empty<Function>(), Array.Empty<Axiom>(),
      new Unit("sP0", Array.Empty<Binding>(), new Check("sO0",
        new Operation(Operator.Equal, "bool", new[] { expression, expression }), false)));
    return new Request(Protocol.Version, "real-protocol-test", Protocol.NormalizerVersion, new string('a', 40),
      Protocol.GetProgramHash(program), "sP0", program,
      new Configuration("z3", new[] { "-in", "-smt2" }, 1000, 10000, 100000, 2, "5.1.0", new string('d', 64)),
      new[] { new SourceIdentity("sO0", "program.dfy", 1, 1, "assertion") }, new string('f', 64));
  }

  [Fact]
  public void RationalRoundTripPreservesEveryExactDigitAndNativeSort() {
    var literal = new RationalLiteral("-9007199254740993125", "1000");
    var request = Request(literal);
    var restored = JsonSerializer.Deserialize<Request>(JsonSerializer.Serialize(request, Protocol.JsonOptions), Protocol.JsonOptions)!;
    ProtocolValidation.ValidateRequest(restored);
    Assert.Equal(request.ProgramHash, Protocol.GetProgramHash(restored.Program));
    var equality = Assert.IsType<Operation>(Assert.IsType<Check>(restored.Program.Unit.Body).Condition);
    Assert.Equal(literal, equality.Arguments[0]);
    var (n, d) = ProtocolValidation.ParseRationalLiteral(literal);
    Assert.Equal(BigInteger.Parse(literal.Numerator), n);
    Assert.Equal(new BigInteger(1000), d);
  }

  [Theory]
  [InlineData("1", "0")]
  [InlineData("1", "-10")]
  [InlineData("", "1")]
  [InlineData("1", "")]
  [InlineData("1.3", "10")]
  [InlineData("1", "10.0")]
  [InlineData("(assert false)", "1")]
  public void InvalidRationalComponentsAreRejectedBeforeConstruction(string n, string d) {
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(new RationalLiteral(n, d))));
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void EachRationalComponentHasItsOwnByteBound(bool numerator) {
    var huge = new string('7', Protocol.MaximumIntegerCharacters + 1);
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(
      new RationalLiteral(numerator ? huge : "1", numerator ? "1" : huge))));
  }

  public static IEnumerable<object[]> Signatures() {
    Expression i = new IntegerLiteral("1"), r = new RationalLiteral("1", "1"), b = new BooleanLiteral(true);
    yield return Row(Operator.Add, "real", true, r, r);
    yield return Row(Operator.Subtract, "real", true, r, r);
    yield return Row(Operator.Multiply, "real", true, r, r);
    yield return Row(Operator.Negate, "real", true, r);
    yield return Row(Operator.Less, "bool", true, r, r);
    yield return Row(Operator.LessEqual, "bool", true, r, r);
    yield return Row(Operator.RealDivide, "real", true, r, r);
    yield return Row(Operator.ToReal, "real", true, i);
    yield return Row(Operator.ToInt, "int", true, r);
    yield return Row(Operator.Divide, "int", true, i, i);
    yield return Row(Operator.Modulo, "int", true, i, i);
    yield return Row(Operator.Add, "real", false, i, r);
    yield return Row(Operator.Multiply, "real", false, b, r);
    yield return Row(Operator.Less, "bool", false, i, r);
    yield return Row(Operator.RealDivide, "real", false, i, r);
    yield return Row(Operator.Divide, "real", false, r, r);
    yield return Row(Operator.Modulo, "real", false, r, r);
    yield return Row(Operator.ToReal, "real", false, r);
    yield return Row(Operator.ToInt, "int", false, i);
    yield return Row(Operator.ToReal, "int", false, i);
    yield return Row(Operator.ToInt, "real", false, r);
    yield return Row(Operator.Negate, "int", false, r);
    yield return Row(Operator.ToReal, "real", false, i, i);
  }
  private static object[] Row(Operator op, string type, bool valid, params Expression[] args) =>
    new object[] { new Operation(op, type, args), valid };

  [Theory]
  [MemberData(nameof(Signatures))]
  public void EveryNumericOperatorRequiresItsExactSignature(Operation operation, bool valid) {
    if (valid) { ProtocolValidation.ValidateRequest(Request(operation)); }
    else { Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(Request(operation))); }
  }

  [Fact]
  public void RealIsAReservedPrimitiveRatherThanANewUninterpretedSort() {
    var request = Request(new RationalLiteral("1", "1"));
    var program = request.Program with { Types = new[] { "real" } };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(request with {
      Program = program, ProgramHash = Protocol.GetProgramHash(program) }));
  }

  [Theory]
  [InlineData(1, "experimental-3")]
  [InlineData(2, "experimental-3")]
  [InlineData(3, "experimental-2")]
  [InlineData(3, "experimental-1")]
  public void PreviousProtocolOrNormalizerCannotReachRealVerification(int version, string normalizer) {
    var request = Request(new RationalLiteral("1", "1")) with { Version = version, NormalizerVersion = normalizer };
    Assert.Throws<InvalidDataException>(() => ProtocolValidation.ValidateRequest(request));
  }
}
