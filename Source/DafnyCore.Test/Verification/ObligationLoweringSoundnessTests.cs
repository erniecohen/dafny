using Microsoft.Dafny;

namespace DafnyCore.Test.Verification;

[Collection("Obligation translation")]
public class ObligationLoweringSoundnessTests {
  [Fact]
  public async Task GuardedSubsetIntroductionKeepsTheGuard() {
    const string source = "ghost predicate P(i: int) requires i != 0 { 10 / i > 0 } type S = i: int | i != 0 && P(i) witness 1 ghost function F(): S { 0 }";
    var text = ObligationFingerprint.Emit(await ObligationLoweringTests.Translate(source, true));
    Assert.Contains("P#canCall", text);
    Assert.Contains("==>", text);
    Assert.Contains("$Is", text);
  }

  [Fact]
  public async Task OwnFunctionPostconditionDoesNotBecomeAFreeSelfPermission() {
    const string source = "ghost function F(i: int): (r: int) ensures var unused := 1; F(i) == 0 { 1 }";
    var text = ObligationFingerprint.Emit(await ObligationLoweringTests.Translate(source, true));
    Assert.DoesNotContain("free ensures {:always_assume} _module.__default.F#canCall(i#0);", text);
  }
  [Fact]
  public async Task BooleanFunctionResultsKeepTheirValueFuelInterface() {
    const string source = "ghost predicate P(n:int) decreases n { n<=0 || P(n-1) } ghost function F(): (r:bool) ensures r { exists n:int :: P(n) }";
    async Task<string[]> ResultBindings(bool enabled) {
      var programs = await ObligationLoweringTests.Translate(source, enabled);
      var implementation = programs.SelectMany(p => p.Implementations)
        .Single(p => p.Name.Contains("CheckWellformed") && p.Name.EndsWith(".F"));
      return implementation.Blocks.SelectMany(b => b.Cmds).OfType<Microsoft.Boogie.AssumeCmd>()
        .Select(c => ObligationFingerprint.Expression(c.Expr))
        .Where(e => e.Contains(".F") && e.Contains("ExistsExpr")).ToArray();
    }
    var legacy = await ResultBindings(false);
    Assert.NotEmpty(legacy);
    Assert.Equal(legacy, await ResultBindings(true));
  }

  [Fact]
  public async Task RecursiveEquivalenceRetainsItsDefinitionFuelBridge() {
    const string source = "ghost function pred(i:int):int { i-1 } ghost predicate f(a:int,s:int) { a<=0 || exists s0:int :: f(pred(a),s0) } lemma L(a:int,s:int) { assert f(a,s) <==> (a<=0 || exists s0:int :: f(pred(a),s0)); }";
    async Task<string[]> Checks(bool enabled) {
      var programs = await ObligationLoweringTests.Translate(source, enabled);
      var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
      return implementation.Blocks.SelectMany(b => b.Cmds).OfType<Microsoft.Boogie.AssertCmd>()
        .Where(c => c.Description is AssertStatementDescription)
        .Select(c => ObligationFingerprint.Expression(c.Expr)).ToArray();
    }
    var legacy = await Checks(false);
    Assert.Single(legacy);
    Assert.Equal(legacy, await Checks(true));
  }
}
