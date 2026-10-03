using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;

namespace IntegrationTests;

[CollectionDefinition("Literal identity translation", DisableParallelization = true)]
public class LiteralIdentityTranslationCollection { }

[Collection("Literal identity translation")]
public class AdditionalAxiomsTranslationTests {
  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task ClosedIdentitiesAreOptionGuardedOrderedAndLocalToEachModule(bool enabled, bool refresh) {
    const string source = """
      module A {
        function F(x: int): int { x }
        function Calls(): int {
          F(42) + F(7) + F(42) + F(0) + F(-7) +
          F(340282366920938463463374607431768211457) +
          F(-340282366920938463463374607431768211457)
        }
      }
      module B {
        function F(x: int): int { x }
        function Calls(): int { F(7) + F(0) + F(7) }
      }
      module Excluded {
        function F(x: int): int { x }
        function R(x: real): real { x }
        function G<T>(x: T): T { x }
        function Calls(x: int): int { F(x) + F(x + 101) + G<int>(999) }
        function RealCalls(): real { R(1.25) }
      }
      """;
    var output = new StringWriter();
    var options = new DafnyOptions(TextReader.Null, output, output);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.AdditionalAxioms, enabled);
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    Microsoft.Dafny.Type.ResetScopes();
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:literal-identities"), reporter);
    DafnyMain.Resolve(parsed.Program);
    Assert.Equal(0, reporter.ErrorCount);
    var programs = BoogieGenerator.Translate(parsed.Program, reporter).ToList();
    Assert.Equal(3, programs.Count);
    var large = BigInteger.Parse("340282366920938463463374607431768211457");
    foreach (var (module, program) in programs) {
      var identities = program.TopLevelDeclarations.OfType<Bpl.Axiom>()
        .Where(axiom => axiom.Expr is Bpl.NAryExpr equality && equality.Fun is Bpl.BinaryOperator op &&
          op.Op == Bpl.BinaryOperator.Opcode.Eq && equality.Args[0] is Bpl.NAryExpr application &&
          application.Fun.FunctionName == "LitInt" && application.Args[0] is Bpl.LiteralExpr { isBigNum: true })
        .ToList();
      var values = identities.Select(axiom =>
        ((Bpl.LiteralExpr)((Bpl.NAryExpr)((Bpl.NAryExpr)axiom.Expr).Args[0]).Args[0]).asBigNum.ToBigInteger).ToList();
      var expected = !enabled || module == "Excluded" ? Array.Empty<BigInteger>() : module == "A"
        ? new[] { -large, new BigInteger(-7), BigInteger.Zero, new BigInteger(7), new BigInteger(42), large }
        : new[] { BigInteger.Zero, new BigInteger(7) };
      Assert.Equal(expected, values);
      var literalFunction = program.Functions.Single(function => function.Name == "LitInt");
      foreach (var identity in identities) {
        Assert.Contains(identity, literalFunction.OtherDefinitionAxioms);
        var equality = (Bpl.NAryExpr)identity.Expr;
        Assert.Same(((Bpl.NAryExpr)equality.Args[0]).Args[0], equality.Args[1]);
      }
    }
  }
}
