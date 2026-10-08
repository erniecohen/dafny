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
  public async Task SynthesizedConstraintChecksKeepSourceDiagnosticLocations() {
    const string source="lemma L() { var n:nat := -1; }";
    var programs=await ObligationLoweringTests.Translate(source,true);
    var checks=programs.SelectMany(p=>p.Implementations).SelectMany(p=>p.Blocks)
      .SelectMany(b=>b.Cmds).OfType<Microsoft.Boogie.AssertCmd>().ToList();
    Assert.NotEmpty(checks);
    foreach (var check in checks) {
      var origin=BoogieGenerator.ToDafnyToken(check.tok);
      Assert.NotNull(origin.Uri);
      Assert.All(ErrorReporterExtensions.CreateDiagnosticRelatedInformationFor(origin,true),
        related=>Assert.NotNull(related.Range.Uri));
    }
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

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task DeclaredPreparationRetainsTheIndependentSpecificationDomainProof(bool refresh) {
    const string source = "ghost predicate F(x:int) requires x>0 { x<0 } " +
      "lemma L(x:int) requires x>0 ensures F(x) {}";
    var programs = await ObligationLoweringTests.Translate(source, true, refresh);
    var implementations = programs.SelectMany(p => p.Implementations).ToList();
    var specification = implementations.Single(p => p.Name.Contains("CheckWellformed") && p.Name.EndsWith(".L"));
    Assert.Contains(specification.Blocks.SelectMany(b => b.Cmds).OfType<Microsoft.Boogie.AssertCmd>(),
      check => check.Description is PreconditionSatisfied);
    var body = implementations.Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".L"));
    var checks = body.Blocks.SelectMany(b => b.Cmds).OfType<Microsoft.Boogie.AssertCmd>().ToList();
    Assert.DoesNotContain(checks, check => check.Description is PreconditionSatisfied);
    Assert.Single(checks.Where(check => check.Description is EnsuresDescription));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task AFalseEarlierPostconditionRemainsMandatoryBeforeCertifiedSupport(bool refresh) {
    const string source = "ghost predicate F(x:int) requires x>0 { true } " +
      "lemma L(x:int) requires x>0 ensures false ensures F(x) {}";
    var programs = await ObligationLoweringTests.Translate(source, true, refresh);
    var body = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".L"));
    var checks = body.Blocks.SelectMany(b => b.Cmds).OfType<Microsoft.Boogie.AssertCmd>()
      .Where(check => check.Description is EnsuresDescription).ToList();
    Assert.NotEmpty(checks);
    Assert.Equal(Microsoft.Boogie.Expr.False.ToString(), checks[0].Expr.ToString());
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CertifiedCallPreparationDoesNotAssumeAConditionalReadsBound(bool refresh) {
    const string source = "class C { const flag:bool " +
      "constructor(b:bool) ensures flag==b { flag:=b; } " +
      "ghost predicate F() reads if flag then {this} else {} { !flag } " +
      "ghost method M() requires F() reads {} {} " +
      "ghost method Bad() requires flag reads {} { M(); } }";
    var programs = await ObligationLoweringTests.Translate(source, true, refresh);
    var body = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".Bad"));
    var commands = body.Blocks.SelectMany(b => b.Cmds).ToList();
    Assert.Single(commands.OfType<Microsoft.Boogie.AssertCmd>()
      .Where(check => check.Description is PreconditionSatisfied));
    Assert.DoesNotContain(commands.OfType<Microsoft.Boogie.AssumeCmd>(), assumption =>
      assumption.Expr.ToString().Contains("$_ReadsFrame") && assumption.Expr.ToString().Contains(".flag"));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SuppressingReadsAssertionsRetainsExistentialTraversalFuel(bool refresh) {
    const string proposition = "A(c) == ((exists x:int :: (exists y:int :: P(x+y)) && P(x)) == " +
      "(exists z:int :: P(z)))";
    const string declarations = "class C {} ghost predicate P(i:int) decreases i { i<=0 || P(i-1) } " +
      "ghost predicate A(c:C) reads if exists j:int :: P(j) then {c} else {} { true } ";
    var packages = new List<BoogieGenerator.PropositionLowering>();
    await ObligationLoweringTests.Translate(declarations + "lemma L(c:C) reads {} ensures " +
      proposition + " { assert " + proposition + "; }", true, refresh, packages.Add);
    var explicitCheck = Assert.Single(packages.Where(p => p.Source.Resolved is BinaryExpr &&
      p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.CheckedExpression));
    var implicitChecks = packages.Where(p => p.Source.Resolved is BinaryExpr &&
      p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.DeclaredContract).ToList();
    Assert.NotEmpty(implicitChecks);
    Assert.All(implicitChecks, p => Assert.Equal(ObligationFingerprint.Content(explicitCheck),
      ObligationFingerprint.Content(p)));
    Assert.Contains("$LS", ObligationFingerprint.Content(explicitCheck));
  }
}
