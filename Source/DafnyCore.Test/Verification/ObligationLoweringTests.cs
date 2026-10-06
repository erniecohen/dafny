using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;

namespace DafnyCore.Test.Verification;

[CollectionDefinition("Obligation translation", DisableParallelization = true)]
public class ObligationTranslationCollection { }

[Collection("Obligation translation")]
public class ObligationLoweringTests {
  internal static async Task<List<Bpl.Program>> Translate(string source, bool enabled, bool refresh = false, Action<BoogieGenerator.PropositionLowering>? observer = null) {
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.Set(CommonOptionBag.GeneralNewtypes, refresh);
    options.Set(CommonOptionBag.ConsistentObligationChecks, enabled);
    var reporter = new BatchErrorReporter(options);
    var result = await ProgramParser.Parse(source, new Uri("untitled:obligation.dfy"), reporter);
    await new ProgramResolver(result.Program).Resolve(CancellationToken.None);
    Assert.Equal(0, reporter.ErrorCount);
    return BoogieGenerator.Translate(result.Program, reporter, new BoogieGenerator.TranslatorFlags(options) {
      ObligationLowered = observer
    }).Select(pair => pair.Item2).ToList();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ForallProofBodyHasAnObligationContinuation(bool refresh) {
    const string body = "lemma L() ensures true { forall x: int ensures true { if x == 0 {} } }";
    var legacy = ObligationFingerprint.Emit(await Translate(body, false, refresh));
    var enabled = ObligationFingerprint.Emit(await Translate(body, true, refresh));
    Assert.DoesNotContain("push;", legacy);
    Assert.Contains("push;", enabled);
    Assert.Contains("pop;", enabled);
  }

  [Fact]
  public async Task VisibleSubsetRetainsMembershipBridgeAndAddsGuardedInlining() {
    const string source = "datatype D = D(i: int) ghost predicate P(d: D) { d.i >= 0 } type S = d: D | P(d) witness D(0) ghost function F(i: nat): S { D(i) }";
    var text = ObligationFingerprint.Emit(await Translate(source, true));
    Assert.Contains("P#canCall", text);
    Assert.Contains("$Is", text);
    var legacy = ObligationFingerprint.Emit(await Translate(source, false));
    Assert.True(text.Split("assert ").Length > legacy.Split("assert ").Length);
  }

  [Fact]
  public async Task ExplicitAndImplicitVisiblePredicateUseTheSameContent() {
    const string source = "datatype D = D(i: int) ghost predicate P(d: D) { d.i >= 0 } type S = d: D | P(d) witness D(0) lemma L(x: D) requires x.i >= 0 { assert P(x); var y: S := x; }";
    var packages = new List<BoogieGenerator.PropositionLowering>();
    await Translate(source, true, observer: packages.Add);
    var explicitCheck = packages.Single(p => p.Source.Resolved is FunctionCallExpr { Function.Name: "P" } &&
      p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.CheckedExpression);
    var implicitChecks = packages.Where(p => p.Source.Resolved is FunctionCallExpr { Function.Name: "P" } &&
      p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.GuardedIntroduction).ToList();
    Assert.Contains(implicitChecks, p => ObligationFingerprint.Content(p) == ObligationFingerprint.Content(explicitCheck));
    Assert.All(implicitChecks, p => Assert.NotNull(p.Inputs.Guard));
  }

  [Fact]
  public async Task QuantifiedPackageDoesNotDependOnEarlierOccurrences() {
    const string declarations = "ghost predicate P(i: int) { i >= 0 } ";
    const string one = "lemma One() ensures exists x: int :: P(x) { assert exists x: int :: P(x); } ";
    const string two = "lemma Two() ensures !(forall y: int :: !P(y)) { assert !(forall y: int :: !P(y)); } ";
    async Task<string[]> Contents(string source) {
      var packages = new List<BoogieGenerator.PropositionLowering>();
      await Translate(declarations+source,true,observer:packages.Add);
      return packages.Where(p => p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.CheckedExpression)
        .Select(ObligationFingerprint.Content).Order().ToArray();
    }
    Assert.Equal(await Contents(one+two),await Contents(two+one));
    Assert.Equal(await Contents(one+two),await Contents(one+two));
  }

  [Fact]
  public void FingerprintPreservesGroundTermsAndAllocationHeaps() {
    var token=Token.NoToken;
    var current=new Bpl.IdentifierExpr(token,"$Heap",Bpl.Type.Int);
    var old=new Bpl.OldExpr(token,current);
    Assert.NotEqual(ObligationFingerprint.Expression(current),ObligationFingerprint.Expression(old));
    var lit = new Bpl.NAryExpr(token,new Bpl.FunctionCall(new Bpl.IdentifierExpr(token,"LitInt",Bpl.Type.Int)),
      new List<Bpl.Expr>{Bpl.Expr.Literal(1)});
    Assert.NotEqual(ObligationFingerprint.Expression(lit),ObligationFingerprint.Expression(Bpl.Expr.Literal(1)));
  }

  [Fact]
  public void FingerprintNormalizesLambdaBindersWithoutChangingTheirBodies() {
    var token = Token.NoToken;
    Bpl.LambdaExpr Lambda(string name, bool body) {
      var variable = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, name, Bpl.Type.Bool));
      return new Bpl.LambdaExpr(token, new List<Bpl.TypeVariable>(), new List<Bpl.Variable> { variable },
        null, body ? new Bpl.IdentifierExpr(token, variable) : Bpl.Expr.False);
    }
    Assert.Equal(ObligationFingerprint.Expression(Lambda("x", true)),
      ObligationFingerprint.Expression(Lambda("y", true)));
    Assert.NotEqual(ObligationFingerprint.Expression(Lambda("x", true)),
      ObligationFingerprint.Expression(Lambda("y", false)));
  }

  [Fact]
  public async Task MethodExitChecksTheSamePropositionAsAnImmediateAssertion() {
    const string source = "ghost predicate P(x:int) { x>=0 } lemma L(x:int) requires x>=0 ensures P(x) { assert P(x); }";
    var packages = new List<BoogieGenerator.PropositionLowering>();
    await Translate(source,true,observer:packages.Add);
    var explicitPackage=packages.Single(p => p.Source.Resolved is FunctionCallExpr { Function.Name: "P" } &&
      p.Inputs.Preparation==BoogieGenerator.ObligationPreparation.CheckedExpression);
    var implicitPackages=packages.Where(p => p.Source.Resolved is FunctionCallExpr { Function.Name: "P" } &&
      p.Inputs.Preparation==BoogieGenerator.ObligationPreparation.DeclaredContract).ToList();
    Assert.NotEmpty(implicitPackages);
    Assert.All(implicitPackages,p=>Assert.Equal(ObligationFingerprint.Content(explicitPackage),ObligationFingerprint.Content(p)));
  }

  [Fact]
  public async Task QuantifiedOldHeapPolicyIsIndependentOfMethodOrder() {
    const string declarations="class C { var i:int } ghost predicate P(c:C,x:int) reads c { c.i==x } ";
    const string one="lemma One(c:C) requires exists x:int {:trigger P(c,x)} :: P(c,x) ensures old(exists x:int {:trigger P(c,x)} :: P(c,x)) { assert old(exists x:int {:trigger P(c,x)} :: P(c,x)); } ";
    const string two="lemma Two(c:C) requires !(forall x:int {:trigger P(c,x)} :: !P(c,x)) ensures old(!(forall x:int {:trigger P(c,x)} :: !P(c,x))) { assert old(!(forall x:int {:trigger P(c,x)} :: !P(c,x))); } ";
    async Task<string[]> Contents(string source) {
      var packages=new List<BoogieGenerator.PropositionLowering>();
      await Translate(declarations+source,true,observer:packages.Add);
      return packages.Where(p=>p.Inputs.Preparation==BoogieGenerator.ObligationPreparation.CheckedExpression)
        .Select(ObligationFingerprint.Content).Order().ToArray();
    }
    Assert.Equal(await Contents(one+two),await Contents(two+one));
  }

  [Fact]
  public async Task CustomFuelAndHiddenBodiesRemainPartOfThePackage() {
    const string source="ghost predicate {:fuel 0,1} P(i:int) { i>=0 } lemma L(i:int) requires P(i) { hide P; assert P(i); reveal P(); assert {:fuel P,2,3} P(i); }";
    var packages=new List<BoogieGenerator.PropositionLowering>();
    await Translate(source,true,observer:packages.Add);
    var assertions=packages.Where(p=>p.Inputs.Preparation==BoogieGenerator.ObligationPreparation.CheckedExpression).ToList();
    Assert.Equal(2,assertions.Count);
    Assert.NotEqual(ObligationFingerprint.Content(assertions[0]),ObligationFingerprint.Content(assertions[1]));
  }

  [Fact]
  public async Task LocalCallCheckRetainsTheExplicitAssertionsInductionPolicy() {
    const string source="ghost predicate P(n:nat) { true } lemma Use() requires forall n:nat {:induction n} :: P(n) {} lemma L() { assert forall n:nat {:induction n} :: P(n); Use(); }";
    var programs=await Translate(source,true);
    var implementation=programs.SelectMany(p=>p.Implementations).Single(p=>p.Name.EndsWith(".L"));
    var checks=implementation.Blocks.SelectMany(b=>b.Cmds).OfType<Bpl.AssertCmd>().ToList();
    var explicitChecks=checks.Where(c=>c.Description is AssertStatementDescription).Select(c=>ObligationFingerprint.Expression(c.Expr)).ToList();
    var implicitChecks=checks.Where(c=>c.Description is PreconditionSatisfied).ToList();
    Assert.NotEmpty(implicitChecks);
    Assert.All(implicitChecks,c=>Assert.Contains(ObligationFingerprint.Expression(c.Expr),explicitChecks));
  }

  [Theory]
  [InlineData("Identity(exists n:int :: P(n))", false)]
  [InlineData("f(exists n:int :: P(n))", false)]
  [InlineData("[exists n:int :: P(n)][0]", false)]
  [InlineData("(var b := exists n:int :: P(n); b)", false)]
  [InlineData("true in (set b:bool | b == (exists n:int :: P(n)))", false)]
  [InlineData("Identity(exists n:int :: P(n))", true)]
  [InlineData("f(exists n:int :: P(n))", true)]
  [InlineData("[exists n:int :: P(n)][0]", true)]
  [InlineData("(var b := exists n:int :: P(n); b)", true)]
  [InlineData("true in (set b:bool | b == (exists n:int :: P(n)))", true)]
  public async Task ExplicitAssertionsRetainTheirOriginalBooleanValueFuel(string expression, bool refresh) {
    var source = "ghost predicate P(n:int) decreases n { n<=0 || P(n-1) } " +
      "ghost predicate Identity(b:bool) { b } lemma L(f:bool->bool) { assert " + expression + "; }";
    async Task<string[]> Checks(bool enabled) {
      var programs = await Translate(source, enabled, refresh);
      var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
      return implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
        .Where(c => c.Description is AssertStatementDescription)
        .Select(c => ObligationFingerprint.Expression(c.Expr)).ToArray();
    }
    var original = await Checks(false);
    Assert.NotEmpty(original);
    Assert.Equal(original, await Checks(true));
  }

  [Fact]
  public async Task ExplicitAndImplicitOldAllocationUseTheSameTypedPredicate() {
    const string source = "class C {} twostate lemma Use(c:C) {} lemma L(c:C) { assert old(allocated(c)); Use(c); }";
    var programs = await Translate(source, true);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>().ToList();
    var explicitChecks = checks.Where(c => c.Description is AssertStatementDescription)
      .Select(c => ObligationFingerprint.Expression(c.Expr)).ToList();
    Assert.Single(explicitChecks);
    var implicitChecks = checks.Where(c => c.Description is IsAllocated).ToList();
    Assert.NotEmpty(implicitChecks);
    Assert.All(implicitChecks, c => Assert.Contains(explicitChecks[0], ObligationFingerprint.Expression(c.Expr)));
  }

  [Fact]
  public async Task ExplicitAndImplicitHigherOrderRequiresUseTheSameHeapAndActuals() {
    const string source = "lemma L(f:int-->int,i:int) requires f.requires(i) { assert f.requires(i); var v := f(i); }";
    var programs = await Translate(source, true);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>().ToList();
    var explicitChecks = checks.Where(c => c.Description is AssertStatementDescription)
      .Select(c => ObligationFingerprint.Expression(c.Expr)).ToList();
    Assert.Single(explicitChecks);
    var implicitChecks = checks.Where(c => c.Description is PreconditionSatisfied).ToList();
    Assert.NotEmpty(implicitChecks);
    Assert.All(implicitChecks, c => Assert.Equal(explicitChecks[0], ObligationFingerprint.Expression(c.Expr)));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task NegativeUniversalAntecedentsRetainOriginalAssertionFuel(bool refresh) {
    const string source = "ghost predicate P(n:int) decreases n { n<=0 || P(n-1) } " +
      "lemma L(q:bool) { assert (forall n:int {:trigger P(n)} :: P(n)) ==> q; " +
      "assert !(forall n:int {:trigger P(n)} :: P(n)); " +
      "assert (exists n:int {:trigger P(n)} :: P(n)) ==> q; }";
    async Task<string[]> Checks(bool enabled) {
      var programs = await Translate(source, enabled, refresh);
      var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
      return implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
        .Where(c => c.Description is AssertStatementDescription)
        .Select(c => ObligationFingerprint.Expression(c.Expr)).ToArray();
    }
    var original = await Checks(false);
    Assert.NotEmpty(original);
    Assert.Equal(original, await Checks(true));
  }

  [Fact]
  public async Task CastChecksRetainTheirOriginalGuardedFormula() {
    const string source = "ghost predicate P(i:int) { i>=0 } type S = i:int | P(i) witness 0 " +
      "lemma L(i:int) { var s := i as S; }";
    async Task<string[]> Checks(bool enabled) {
      var programs = await Translate(source, enabled);
      var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
      return implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
        .Where(c => c.Description is ConversionSatisfiesConstraints)
        .Select(c => ObligationFingerprint.Expression(c.Expr)).ToArray();
    }
    var original = await Checks(false);
    var enriched = await Checks(true);
    Assert.NotEmpty(original);
    Assert.All(original, check => Assert.Contains(check, enriched));
    Assert.True(enriched.Length > original.Length);
  }

  [Fact]
  public async Task AllocationChecksRetainTheirOriginalTypedFormula() {
    const string source = "class C {} twostate lemma Use(c:C) {} lemma L(c:C) { Use(c); }";
    async Task<string[]> Checks(bool enabled) {
      var programs = await Translate(source, enabled);
      var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
      return implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
        .Where(c => c.Description is IsAllocated)
        .Select(c => ObligationFingerprint.Expression(c.Expr)).ToArray();
    }
    var original = await Checks(false);
    var enriched = await Checks(true);
    Assert.NotEmpty(original);
    Assert.All(original, check => Assert.Contains(enriched, added => added.Contains(check)));
  }

}
