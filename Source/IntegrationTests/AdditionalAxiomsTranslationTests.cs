using System;
using System.Collections.Generic;
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
  public async Task ClosedIdentitiesStayInVerificationContexts(bool enabled, bool refresh) {
    const string source = """
      module A {
        function F(x: int): int { x }
        function OnlyInOtherBody(): int { F(777) }
        lemma Isolated(x: int) ensures F(x) == x { }
        lemma ThroughOtherBody() ensures OnlyInOtherBody() == 777 { }
        lemma Repeated()
          ensures F(42) + F(7) + F(42) + F(0) + F(-7) +
            F(340282366920938463463374607431768211457) +
            F(-340282366920938463463374607431768211457) == 84
        { }
        lemma Branches(b: bool) {
          if b { assert F(17) == 17; } else { assert F(17) == 17; }
        }
      }
      module B {
        function F(x: int): int { x }
        lemma Repeated() ensures F(7) + F(0) + F(7) == 14 { }
      }
      module Excluded {
        function F(x: int): int { x }
        function R(x: real): real { x }
        function G<T>(x: T): T { x }
        lemma Calls(x: int)
          ensures F(x) + F(x + 101) + G<int>(999) == 2 * x + 1100
          ensures R(1.25) == 1.25
        { }
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
      // Reparse the emitted program so nested control flow has ordinary blocks.
      var text = new StringWriter();
      program.Emit(new Bpl.TokenTextWriter("virtual", text, true, options));
      Assert.Equal(0, Bpl.Parser.Parse(text.ToString(), "virtual", out var emitted));
      foreach (var axiom in program.TopLevelDeclarations.OfType<Bpl.Axiom>()) {
        Assert.Empty(Identities(axiom.Expr)); // Includes shared quantified definitions/consequences.
      }
      var procedures = program.TopLevelDeclarations.OfType<Bpl.Procedure>().ToList();
      if (module == "Excluded" || !enabled) {
        foreach (var procedure in procedures) {
          Assert.Empty(procedure.Requires.SelectMany(require => Identities(require.Condition)));
          Assert.Empty(procedure.Ensures.SelectMany(ensure => Identities(ensure.Condition)));
        }
        foreach (var implementation in emitted.Implementations) {
          Assert.Empty(AssumedIdentities(implementation));
        }
        continue;
      }
      var repeated = procedures.Single(procedure => procedure.Name.StartsWith("Impl$") && procedure.Name.Contains("Repeated"));
      var values = repeated.Ensures.Where(ensure => ensure.Free)
        .SelectMany(ensure => Identities(ensure.Condition)).ToList();
      var expected = module == "A"
        ? new[] { -large, new BigInteger(-7), BigInteger.Zero, new BigInteger(7), new BigInteger(42), large }
        : new[] { BigInteger.Zero, new BigInteger(7) };
      Assert.Equal(expected, values); // One assumption: deduplicated, numerical order.
      if (module == "A") {
        foreach (var procedure in procedures.Where(p => p.Name.Contains("Isolated") || p.Name.Contains("ThroughOtherBody"))) {
          Assert.Empty(procedure.Requires.SelectMany(require => Identities(require.Condition)));
          Assert.Empty(procedure.Ensures.SelectMany(ensure => Identities(ensure.Condition)));
        }
        foreach (var implementation in emitted.Implementations.Where(i => i.Name.Contains("Isolated") || i.Name.Contains("ThroughOtherBody"))) {
          Assert.Empty(AssumedIdentities(implementation)); // Do not walk a callee's body.
        }
        var branches = emitted.Implementations.Single(i => i.Name.StartsWith("Impl$") && i.Name.Contains("Branches"));
        Assert.True(AssumedIdentities(branches).Count(value => value == 17) >= 2);
      }
    }
  }

  private static IEnumerable<BigInteger> AssumedIdentities(Bpl.Implementation implementation) {
    return implementation.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssumeCmd>()
      .SelectMany(assume => Identities(assume.Expr));
  }

  private static List<BigInteger> Identities(Bpl.Expr expr) {
    var visitor = new IdentityVisitor();
    visitor.VisitExpr(expr);
    return visitor.Values;
  }

  private class IdentityVisitor : Bpl.StandardVisitor {
    public List<BigInteger> Values { get; } = [];
    public override Bpl.Expr VisitNAryExpr(Bpl.NAryExpr expr) {
      if (expr.Fun is Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.Eq } &&
          expr.Args[0] is Bpl.NAryExpr application && application.Fun.FunctionName == "LitInt" &&
          application.Args.Count == 1 && application.Args[0] is Bpl.LiteralExpr { isBigNum: true } numeral &&
          expr.Args[1] is Bpl.LiteralExpr { isBigNum: true } right && numeral.asBigNum == right.asBigNum) {
        Values.Add(numeral.asBigNum.ToBigInteger);
      }
      return base.VisitNAryExpr(expr);
    }
  }
}
