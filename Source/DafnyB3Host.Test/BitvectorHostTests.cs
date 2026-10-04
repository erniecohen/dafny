using System.Numerics;
using System.Security.Cryptography;
using DafnyB3Host;
using DafnyB3Protocol;
using Xunit;
using NormalizedProgram = DafnyB3Protocol.Program;

namespace DafnyB3Host.Test;

public sealed class BitvectorHostTests {
  private static string SolverPath => Environment.GetEnvironmentVariable("B3_TEST_SOLVER")
    ?? throw new InvalidOperationException("Set B3_TEST_SOLVER to the pinned solver executable");
  private static string Type(int width) => Protocol.BitvectorTypeName(width);
  private static BitvectorLiteral Word(BigInteger value, int width = 7) =>
    new(value.ToString(System.Globalization.CultureInfo.InvariantCulture), width);
  private static BitvectorOperation Op(BitvectorOperator op, int width, params Expression[] args) => new(
    op, width, 0, 0, op is BitvectorOperator.UnsignedLess or BitvectorOperator.UnsignedLessEqual ? "bool"
      : op == BitvectorOperator.BitvectorToUnsignedInt ? "int" : Type(width), args);
  private static Operation Equal(Expression left, Expression right) => new(Operator.Equal, "bool", new[] { left, right });
  private static Quantifier Forall(int width, Expression body, params string[] names) => new(true,
    names.Select(name => new Binding(name, Type(width))).ToArray(), Array.Empty<IReadOnlyList<Expression>>(), body);
  private static NormalizedProgram Unit(Statement body, params Binding[] variables) => new(
    Array.Empty<string>(), Array.Empty<Function>(), Array.Empty<Axiom>(), new Unit("sUnit", variables, body));
  private static Request Request(WorkerPackage package, NormalizedProgram program) => new(
    Protocol.Version, Guid.NewGuid().ToString("N"), Protocol.NormalizerVersion, WorkerPackage.UpstreamCommit,
    Protocol.GetProgramHash(program), program.Unit.Name, program,
    new Configuration(SolverPath, new[] { "-in", "-smt2" }, 10000, 1000000, 1048576, 2, "5.1.0", Digest(SolverPath)),
    new[] { new SourceIdentity("sO0", "program.dfy", 1, 1, "assertion") }, package.Fingerprint);
  private static string Digest(string file) {
    using var input = File.OpenRead(file);
    return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
  }
  private static async Task Expect(NormalizedProgram program, Outcome outcome) {
    using var package = new PackageFixture();
    var request = Request(package.Package, program);
    ProtocolValidation.ValidateRequest(request);
    var result = await new WorkerProcessClient("dotnet", new[] { package.Package.WorkerPath }).RunAsync(request, CancellationToken.None);
    Assert.True(result.TraversalCompleted, result.Error);
    Assert.Null(result.Error);
    Assert.Equal(outcome, result.Outcome);
    Assert.Equal(outcome, Assert.Single(result.Attempts).Outcome);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(3)]
  [InlineData(7)]
  [InlineData(67)]
  public Task ModularAdditionIdentityHoldsForEveryNativeWord(int width) {
    var x = new Variable("sX", Type(width));
    var condition = Forall(width, Equal(Op(BitvectorOperator.Add, width, x, Word(0, width)), x), "sX");
    return Expect(Unit(new Check("sO0", condition, false)), Outcome.Verified);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(3)]
  [InlineData(7)]
  [InlineData(67)]
  public Task FullCarrierIncludesSatisfiableUnsignedHighBitWords(int width) {
    var x = new Variable("sX", Type(width));
    var condition = Op(BitvectorOperator.UnsignedLessEqual, width, Word(BigInteger.One << (width - 1), width), x);
    return Expect(Unit(new Block(new Statement[] {
      new Assume(condition), new Check("sO0", new BooleanLiteral(false), false)
    }), new Binding("sX", Type(width))), Outcome.Failed);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(3)]
  [InlineData(7)]
  [InlineData(67)]
  public Task NativeShiftsAtOrBeyondTheWidthAreZeroForEveryWordAndWordCount(int width) {
    var x = new Variable("sX", Type(width));
    var c = new Variable("sC", Type(width));
    var beyond = Op(BitvectorOperator.UnsignedLessEqual, width, Word(width, width), c);
    var left = Equal(Op(BitvectorOperator.ShiftLeft, width, x, c), Word(0, width));
    var right = Equal(Op(BitvectorOperator.LogicalShiftRight, width, x, c), Word(0, width));
    var condition = Forall(width, new Operation(Operator.Implies, "bool", new Expression[] {
      beyond, new Operation(Operator.And, "bool", new Expression[] { left, right })
    }), "sX", "sC");
    return Expect(Unit(new Check("sO0", condition, false)), Outcome.Verified);
  }

  [Fact]
  public Task ExtractRecoversBothPartsOfAnArbitraryConcat() {
    var x = new Variable("sX", "#bv3");
    var y = new Variable("sY", "#bv4");
    var concat = Op(BitvectorOperator.Concat, 7, x, y);
    var high = new BitvectorOperation(BitvectorOperator.Extract, 3, 4, 7, "#bv3", new[] { concat });
    var low = new BitvectorOperation(BitvectorOperator.Extract, 4, 0, 4, "#bv4", new[] { concat });
    var condition = new Quantifier(true, new[] { new Binding("sX", "#bv3"), new Binding("sY", "#bv4") },
      Array.Empty<IReadOnlyList<Expression>>(), new Operation(Operator.And, "bool", new Expression[] {
        Equal(high, x), Equal(low, y)
      }));
    return Expect(Unit(new Check("sO0", condition, false)), Outcome.Verified);
  }

  public static IEnumerable<object[]> PrimitiveValues() {
    var x = Word(127), y = Word(3);
    yield return Row(Op(BitvectorOperator.And, 7, x, y), Word(3));
    yield return Row(Op(BitvectorOperator.Or, 7, Word(64), y), Word(67));
    yield return Row(Op(BitvectorOperator.Xor, 7, x, y), Word(124));
    yield return Row(Op(BitvectorOperator.Not, 7, x), Word(0));
    yield return Row(Op(BitvectorOperator.Add, 7, x, Word(1)), Word(0));
    yield return Row(Op(BitvectorOperator.Subtract, 7, Word(0), Word(1)), Word(127));
    yield return Row(Op(BitvectorOperator.Multiply, 7, Word(64), Word(2)), Word(0));
    yield return Row(Op(BitvectorOperator.UnsignedDivide, 7, x, Word(0)), Word(127));
    yield return Row(Op(BitvectorOperator.UnsignedRemainder, 7, x, Word(0)), Word(127));
    yield return Row(Op(BitvectorOperator.UnsignedLess, 7, Word(64), x), new BooleanLiteral(true));
    yield return Row(Op(BitvectorOperator.UnsignedLessEqual, 7, x, x), new BooleanLiteral(true));
    yield return Row(Op(BitvectorOperator.ShiftLeft, 7, Word(1), Word(6)), Word(64));
    yield return Row(Op(BitvectorOperator.LogicalShiftRight, 7, Word(64), x), Word(0));
    yield return Row(new BitvectorOperation(BitvectorOperator.Extract, 3, 2, 5, "#bv3", new[] { x }), Word(7, 3));
    yield return Row(Op(BitvectorOperator.Concat, 7, Word(3, 3), Word(15, 4)), Word(63));
    yield return Row(Op(BitvectorOperator.IntToBitvector, 7, new IntegerLiteral("-1")), Word(127));
    yield return Row(Op(BitvectorOperator.BitvectorToUnsignedInt, 67, Word(BigInteger.One << 66, 67)), new IntegerLiteral("73786976294838206464"));
  }
  private static object[] Row(Expression operation, Expression expected) => new object[] { operation, expected };

  [Theory]
  [MemberData(nameof(PrimitiveValues))]
  public Task EveryTypedPrimitiveHasItsExactNativeInterpretation(Expression operation, Expression expected) =>
    Expect(Unit(new Check("sO0", Equal(operation, expected), false)), Outcome.Verified);

  [Fact]
  public Task AnIncorrectModularIdentityFailsWithoutAnyAssumptions() =>
    Expect(Unit(new Check("sO0", Equal(Op(BitvectorOperator.Add, 7, Word(127), Word(1)), Word(1)), false)), Outcome.Failed);

  [Fact]
  public Task NativeFunctionsQuantifiersLetsAndStateKeepTheWordSort() {
    var x = new Variable("sX", "#bv7");
    var fx = new Application("sF", "#bv7", new[] { x });
    var condition = Forall(7, new Let(new Binding("sY", "#bv7"), fx, Equal(new Variable("sY", "#bv7"), fx)), "sX");
    var program = Unit(new Block(new Statement[] {
      new Assign("sV", Word(127)), new Check("sO0", condition, false)
    }), new Binding("sV", "#bv7")) with {
      Functions = new[] { new Function("sF", new[] { new Binding("sArg", "#bv7") }, "#bv7") }
    };
    return Expect(program, Outcome.Verified);
  }

  private static SolverConfiguration._IConfiguration AbsentConfiguration() =>
    SolverConfiguration.Configuration.create(RawAstBuilder.S("b3-absent-solver"),
      Dafny.Sequence<Dafny.ISequence<Dafny.Rune>>.Empty, 1000, 10000, 1048576,
      SolverConfiguration.SolverKind.create_Z3(), 2);
  private static RawAst._IProgram RawUnit(RawAst._IExpr condition) {
    var procedure = RawAst.Procedure.create(RawAstBuilder.S("sUnit"),
      Dafny.Sequence<RawAst._IPParameter>.Empty, Dafny.Sequence<RawAst._IAExpr>.Empty,
      Dafny.Sequence<RawAst._IAExpr>.Empty,
      Std.Wrappers.Option<RawAst._IStmt>.create_Some(RawAst.Stmt.create_Check(condition)));
    var empty = RawAstBuilder.Build(Unit(new Check("sO0", new BooleanLiteral(true), false)));
    return RawAst.Program.create_Program(empty.dtor_signatureTypes, empty.dtor_domains, empty.dtor_types,
      empty.dtor_taggers, empty.dtor_functions, empty.dtor_axioms,
      Dafny.Sequence<RawAst._IProcedure>.FromArray(new[] { procedure }));
  }
  private static RawAst._IExpr RawEqual(RawAst._IExpr left, RawAst._IExpr right) =>
    RawAst.Expr.create_OperatorExpr(RawAst.Operator.create_Eq(), Dafny.Sequence<RawAst._IExpr>.FromArray(new[] { left, right }));
  private static void RejectedBeforeAnySolverAttempt(RawAst._IProgram raw) {
    var result = B3Library.__default.CheckAndVerify(raw, RawAstBuilder.S("sUnit"), AbsentConfiguration());
    Assert.False(result.dtor_complete);
    Assert.True(result.dtor_error.is_Some);
    var error = result.dtor_error.dtor_value.ToVerbatimString(false);
    Assert.True(error.StartsWith("unsupported:") || error.StartsWith("invalid input:"), error);
    Assert.Empty(result.dtor_attempts);
  }

  [Theory]
  [InlineData(0, 0)]
  [InlineData(0, -1)]
  [InlineData(0, 4097)]
  [InlineData(-1, 7)]
  [InlineData(128, 7)]
  public void MalformedRawWordCannotBypassExecutableAdmission(int value, int width) {
    var bad = RawAst.Expr.create_BvLiteral(value, width);
    RejectedBeforeAnySolverAttempt(RawUnit(RawEqual(bad, bad)));
  }

  [Fact]
  public void MalformedRawMetadataCannotBypassExecutableAdmission() {
    var word = RawAst.Expr.create_BvLiteral(0, 7);
    var bad = RawAst.Expr.create_OperatorExpr(RawAst.Operator.create_Bv(RawAst.BitvectorOperator.create_BvExtract(), 3, 5, 2),
      Dafny.Sequence<RawAst._IExpr>.FromArray(new[] { word }));
    RejectedBeforeAnySolverAttempt(RawUnit(RawEqual(bad, bad)));
  }

  [Fact]
  public void MixedRawWordWidthsCannotBypassTypeChecking() {
    var bad = RawAst.Expr.create_OperatorExpr(RawAst.Operator.create_Bv(RawAst.BitvectorOperator.create_BvAdd(), 7, 0, 0),
      Dafny.Sequence<RawAst._IExpr>.FromArray(new[] { RawAst.Expr.create_BvLiteral(0, 7), RawAst.Expr.create_BvLiteral(0, 3) }));
    RejectedBeforeAnySolverAttempt(RawUnit(RawEqual(bad, bad)));
  }

  private sealed class PackageFixture : IDisposable {
    private readonly string directory = Path.Combine(Path.GetTempPath(), "b3-word-host-" + Guid.NewGuid().ToString("N"));
    public WorkerPackage Package { get; }
    public PackageFixture() {
      var source = Environment.GetEnvironmentVariable("B3_TEST_WORKER_PACKAGE")
        ?? throw new InvalidOperationException("Set B3_TEST_WORKER_PACKAGE to the published worker package");
      Directory.CreateDirectory(directory);
      foreach (var file in Directory.GetFiles(source)) { File.Copy(file, Path.Combine(directory, Path.GetFileName(file))); }
      Package = WorkerPackage.Load(Path.Combine(directory, "DafnyB3Host.dll"));
    }
    public void Dispose() => Directory.Delete(directory, recursive: true);
  }
}
