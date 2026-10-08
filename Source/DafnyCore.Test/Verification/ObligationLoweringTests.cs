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
    Assert.True(reporter.ErrorCount == 0, string.Join("\n", reporter.AllMessages.Select(message => message.Message)));
    return BoogieGenerator.Translate(result.Program, reporter, new BoogieGenerator.TranslatorFlags(options) {
      ObligationLowered = observer
    }).Select(pair => pair.Item2).ToList();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task TerminalForallProofPreservesLegacyRevealScopes(bool refresh) {
    const string body = "ghost function F(i:int):int { i } lemma L() { hide *; " +
      "forall x: int ensures F(x)==x { calc { F(x); == { { reveal F; } } x; } } }";
    var legacy = ObligationFingerprint.Emit(await Translate(body, false, refresh));
    var enabled = ObligationFingerprint.Emit(await Translate(body, true, refresh));
    Assert.Contains("reveal ", legacy);
    Assert.DoesNotContain("push;", legacy);
    Assert.DoesNotContain("pop;", legacy);
    Assert.DoesNotContain("push;", enabled);
    Assert.DoesNotContain("pop;", enabled);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task MethodBodyPreservesLegacyRevealScopes(bool refresh, bool terminal) {
    var source = "ghost function F(i:int):int { i } lemma L(i:int,b:bool) { hide *; " +
      "if b { calc { F(i); == { { reveal F; } } i; } } " +
      "else { calc { F(i); == { { reveal F; } } i; } } " +
      (terminal ? "}" : "assert true; }");
    async Task<string[]> ScopeCommands(bool enabled) {
      var text = ObligationFingerprint.Emit(await Translate(source, enabled, refresh));
      return text.Split('\n').Select(line => line.Trim())
        .Where(line => line is "push;" or "pop;" ||
          line.StartsWith("hide ") || line.StartsWith("reveal ")).ToArray();
    }
    var legacy = await ScopeCommands(false);
    Assert.Contains(legacy, command => command.StartsWith("reveal "));
    if (terminal) {
      Assert.DoesNotContain("pop;", legacy);
    } else {
      Assert.Contains("push;", legacy);
      Assert.Contains("pop;", legacy);
    }
    Assert.Equal(legacy, await ScopeCommands(true));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task LocalExitIncludesAssertionWellformednessPreparationAndOnePostconditionCheck(bool refresh) {
    const string source = "ghost predicate P(x:int) { x>=0 } lemma L() ensures forall x:int :: P(x) {}";
    var programs = await Translate(source, true, refresh);
    var body = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".L"));
    var commands = body.Blocks.SelectMany(b => b.Cmds).ToList();
    Assert.Contains(commands, command => command is Bpl.HavocCmd);
    Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(check => check.Description is EnsuresDescription));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task InheritedExitWellformednessRetainsReverificationGuard(bool refresh) {
    const string source = "module A { predicate P(x:int) { x>=0 } " +
      "lemma L() ensures forall x:int :: P(x) {} } module B refines A { lemma L ... { ...; assert true; } }";
    var text = ObligationFingerprint.Emit(await Translate(source, true, refresh));
    Assert.Contains("if ($_reverifyPost)", text);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task InitializerConstraintRetainsItsFrozenApplicationDomainGuard(bool refresh) {
    const string source = "type S = x:int | x>=0 witness 0 function G(i:int):int { i } " +
      "method L(n:nat) returns(a:array<S>) { a := new S[n](G); }";
    var programs = await Translate(source, true, refresh);
    var body = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".L"));
    var check = Assert.Single(body.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(command => command.Description is SubrangeCheck));
    Assert.Contains("Requires1", ObligationFingerprint.Expression(check.Expr));
    Assert.Equal(0, Bpl.QKeyValue.FindIntAttribute(check.Attributes, "subsumption", -1));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task QuantifiedTypeChecksRetainTheExplicitUniversalPredicateLayers(bool refresh) {
    const string source = "ghost predicate P(x:int) decreases x { x<=0 || P(x-1) } type S = x:int | P(x) witness 0 " +
      "function G(i:int):int { i } method L(n:nat) returns(a:array<S>) " +
      "{ assert forall i:int {:induction false} | 0<=i<n :: P(G(i)); a := new S[n](G); }";
    var programs = await Translate(source, true, refresh);
    var body = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".L"));
    var checks = body.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>().ToList();
    static IEnumerable<Bpl.Expr> Expressions(Bpl.Expr expression) {
      yield return expression;
      var children = expression switch {
        Bpl.NAryExpr application => application.Args,
        Bpl.QuantifierExpr quantifier => new List<Bpl.Expr> { quantifier.Body },
        _ => new List<Bpl.Expr>()
      };
      foreach (var child in children) {
        foreach (var nested in Expressions(child)) { yield return nested; }
      }
    }
    static string[] Layers(IEnumerable<Bpl.AssertCmd> commands) => commands
      .SelectMany(command => Expressions(command.Expr)).OfType<Bpl.NAryExpr>()
      .Where(application => application.Fun is Bpl.FunctionCall && application.Fun.ToString()!.EndsWith(".P"))
      .Select(application => ObligationFingerprint.Expression(application.Args[0])).Distinct().Order().ToArray();
    var explicitLayers = Layers(checks.Where(check => check.Description is AssertStatementDescription));
    var implicitLayers = Layers(checks.Where(check => check.Description is SubrangeCheck));
    Assert.NotEmpty(explicitLayers);
    Assert.NotEmpty(implicitLayers);
    Assert.Equal(explicitLayers, implicitLayers);
  }

  [Fact]
  public async Task VisibleSubsetChecksConstraintAndDerivesMembershipWithoutDuplicateProof() {
    const string source = "datatype D = D(i: int) ghost predicate P(d: D) { d.i >= 0 } type S = d: D | P(d) witness D(0) ghost function F(i: nat): S { D(i) }";
    var text = ObligationFingerprint.Emit(await Translate(source, true));
    Assert.Contains("P#canCall", text);
    Assert.Contains("$Is", text);
    var programs = await Translate(source, true);
    var implementation = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("CheckWellformed") && p.Name.EndsWith(".F"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is SubrangeCheck).ToList();
    Assert.NotEmpty(checks);
    Assert.DoesNotContain(checks, check => ObligationFingerprint.Expression(check.Expr).Contains("Tclass._module.S"));
  }

  [Fact]
  public async Task QuantifiedInitializerChecksVisibleConstraintWithinItsIndexRange() {
    const string source = "type S = x:int | x>=0 witness 0 " +
      "function G(i:int):int { i } method F(n:nat) returns (a:array<S>) { a := new S[n](G); }";
    var programs = await Translate(source, true);
    var implementation = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".F"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is SubrangeCheck).ToList();
    Assert.NotEmpty(checks);
    Assert.Contains(checks, c => c.Expr is Bpl.ForallExpr);
    Assert.DoesNotContain(checks, c => ObligationFingerprint.Expression(c.Expr).Contains("Tclass._module.S"));
    Assert.All(checks.Where(c => c.Expr is Bpl.ForallExpr), c =>
      Assert.Equal(0, Bpl.QKeyValue.FindIntAttribute(c.Attributes, "subsumption", -1)));
  }

  [Fact]
  public async Task SequenceResultsDeriveMembershipFromRangeBoundElementChecks() {
    const string source = "type S = x:int | x>=0 witness 0 " +
      "ghost function G(i:int):int { i } ghost function F(n:nat):seq<S> { seq(n, G) }";
    var programs = await Translate(source, true);
    var implementation = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("CheckWellformed") && p.Name.EndsWith(".F"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is SubrangeCheck).ToList();
    Assert.NotEmpty(checks);
    Assert.Contains(checks, c => c.Expr is Bpl.ForallExpr);
    Assert.DoesNotContain(checks, c => ObligationFingerprint.Expression(c.Expr).Contains("Tclass._module.S"));
  }

  [Theory]
  [InlineData("lemma L(x:int) requires x>=0 { assert P(x); while false invariant P(x) {} }")]
  [InlineData("lemma L(x:int) requires x>=0 { assert P(x); opaque ensures P(x) {} }")]
  [InlineData("lemma L(x:int) requires x>=0 { forall i | i==x ensures P(i) { assert P(i); } }")]
  public async Task InvariantAndOpaqueChecksUseImmediateAssertionContent(string body) {
    var packages = new List<BoogieGenerator.PropositionLowering>();
    await Translate("ghost predicate P(x:int) { x>=0 } " + body, true, observer: packages.Add);
    var explicitCheck = packages.Single(p => p.Source.Resolved is FunctionCallExpr { Function.Name: "P" } &&
      p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.CheckedExpression);
    var implicitChecks = packages.Where(p => p.Source.Resolved is FunctionCallExpr { Function.Name: "P" } &&
      p.Inputs.Preparation != BoogieGenerator.ObligationPreparation.CheckedExpression).ToList();
    Assert.NotEmpty(implicitChecks);
    Assert.All(implicitChecks, p => Assert.Equal(ObligationFingerprint.Content(explicitCheck), ObligationFingerprint.Content(p)));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task YieldPreparationPreservesIteratorReadsPolicyAndOldHeap(bool refresh) {
    const string source = "iterator I() yields (r:int) yield ensures r>=old(r) { yield 0; }";
    var programs = await Translate(source, true, refresh);
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    foreach (var program in programs) {
      Assert.Equal(0, program.Resolve(options));
      Assert.Equal(0, program.Typecheck(options));
    }
    var body = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".I"));
    var checks = body.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().ToList();
    Assert.Contains(checks, check => ObligationFingerprint.Expression(check.Expr).Contains("$_OldIterHeap"));
    Assert.DoesNotContain(checks, check => ObligationFingerprint.Expression(check.Expr).Contains("$_ReadsFrame"));
  }

  [Fact]
  public async Task IteratorExitsCheckLocallyWithoutDuplicateProcedureEnsures() {
    const string source = "iterator I() yields (x:int) ensures true { yield 0; }";
    var programs = await Translate(source, true);
    var implementation = programs.SelectMany(p => p.Implementations)
      .Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".I"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is EnsuresDescription).ToList();
    Assert.Single(checks);
    var procedure = programs.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == implementation.Name);
    Assert.All(procedure.Ensures, ensures => Assert.True(ensures.Free || ensures.Description is not EnsuresDescription));
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
  public void FingerprintDistinguishesOperatorIdentities() {
    var x = new Bpl.IdentifierExpr(Token.NoToken, "x", Bpl.Type.Bool);
    var y = new Bpl.IdentifierExpr(Token.NoToken, "y", Bpl.Type.Bool);
    Assert.NotEqual(ObligationFingerprint.Expression(Bpl.Expr.Imp(x, y)),
      ObligationFingerprint.Expression(Bpl.Expr.And(x, y)));
    var n = new Bpl.IdentifierExpr(Token.NoToken, "n", Bpl.Type.Int);
    Assert.NotEqual(ObligationFingerprint.Expression(Bpl.Expr.Add(n, Bpl.Expr.Literal(1))),
      ObligationFingerprint.Expression(Bpl.Expr.Sub(n, Bpl.Expr.Literal(1))));
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

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void FingerprintDistinguishesLexicalShadowing(bool resolved) {
    var token = Token.NoToken;
    Bpl.Expr Formula(bool reflexive, string outerName, string middleName, string innerName) {
      Bpl.Variable Bound(string name) => new Bpl.BoundVariable(token,
        new Bpl.TypedIdent(token, name, Bpl.Type.Int));
      var outer = Bound(outerName); var middle = Bound(middleName); var inner = Bound(innerName);
      Bpl.Expr Id(Bpl.Variable variable) => resolved
        ? new Bpl.IdentifierExpr(token, variable)
        : new Bpl.IdentifierExpr(token, variable.Name, Bpl.Type.Int);
      Bpl.Expr Quantify(Bpl.Variable variable, Bpl.Expr body) => new Bpl.ForallExpr(token,
        new List<Bpl.TypeVariable>(), new List<Bpl.Variable> { variable }, null, null, body);
      return Quantify(outer, Quantify(middle, Quantify(inner,
        Bpl.Expr.Eq(Id(reflexive ? inner : middle), Id(inner)))));
    }
    var shadowed = Formula(false, "x", "x", "y");
    var reflexive = Formula(true, "x", "x", "y");
    Assert.NotEqual(ObligationFingerprint.Expression(shadowed), ObligationFingerprint.Expression(reflexive));
    Assert.Equal(ObligationFingerprint.Expression(shadowed),
      ObligationFingerprint.Expression(Formula(false, "a", "b", "c")));
  }

  [Fact]
  public void FingerprintKeepsResolvedOuterReferencesUnderShadowing() {
    var token = Token.NoToken;
    var outer = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int));
    var inner = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int));
    Bpl.Expr Formula(Bpl.Variable used) => new Bpl.ForallExpr(token,
      new List<Bpl.TypeVariable>(), new List<Bpl.Variable> { outer }, null, null,
      new Bpl.ForallExpr(token, new List<Bpl.TypeVariable>(), new List<Bpl.Variable> { inner }, null, null,
        Bpl.Expr.Eq(new Bpl.IdentifierExpr(token, used), Bpl.Expr.Literal(0))));
    Assert.NotEqual(ObligationFingerprint.Expression(Formula(outer)),
      ObligationFingerprint.Expression(Formula(inner)));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task MethodExitPreservesTheCheckedEnsuresPublicationPolicy(bool refresh) {
    const string source = "ghost predicate P(x:int) { x>=0 && x<=100 } " +
      "lemma L(x:int) requires 0<=x<=100 ensures P(x) {}";
    var enabled = await Translate(source, true, refresh);
    var implementation = enabled.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var localChecks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is EnsuresDescription).ToList();
    Assert.True(localChecks.Count >= 2);
    Assert.All(localChecks, check =>
      Assert.Equal(-1, Bpl.QKeyValue.FindIntAttribute(check.Attributes, "subsumption", -1)));

    var procedure = enabled.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == implementation.Name);
    var userEnsures = procedure.Ensures.Where(ensures => ensures.Description is EnsuresDescription).ToList();
    Assert.NotEmpty(userEnsures);
    Assert.All(userEnsures, ensures => Assert.True(ensures.Free));
    Assert.Equal(2, localChecks.Count);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ExplicitSplitAssertionsRetainTheirCheckAndForgetPolicy(bool refresh) {
    const string source = "ghost predicate P(x:int) { x>=0 && x<=100 } " +
      "lemma L(x:int) requires 0<=x<=100 { assert P(x); }";
    foreach (var enabled in new[] { false, true }) {
      var programs = await Translate(source, enabled, refresh);
      var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
      var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
        .Where(c => c.Description is AssertStatementDescription).ToList();
      Assert.True(checks.Count >= 2);
      Assert.All(checks, check =>
        Assert.Equal(0, Bpl.QKeyValue.FindIntAttribute(check.Attributes, "subsumption", -1)));
    }
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

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ExitPreparationPreservesTheImmediateAssertionsExistentialFuelState(bool refresh) {
    const string proposition = "exists x:int :: (exists y:int :: P(x+y)) && P(x)";
    var source = "ghost predicate P(i:int) { i>=0 } lemma L() ensures " + proposition +
      " { assert " + proposition + "; }";
    var packages = new List<BoogieGenerator.PropositionLowering>();
    await Translate(source, true, refresh, packages.Add);
    var explicitCheck = Assert.Single(packages.Where(p => p.Source.Resolved is ExistsExpr &&
      p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.CheckedExpression));
    var implicitChecks = packages.Where(p => p.Source.Resolved is ExistsExpr &&
      p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.DeclaredContract).ToList();
    Assert.NotEmpty(implicitChecks);
    Assert.All(implicitChecks, p => Assert.Equal(ObligationFingerprint.Content(explicitCheck),
      ObligationFingerprint.Content(p)));
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task PreparedExistentialEqualityRetainsImmediateAssertionFuel(bool refresh, bool opaque) {
    const string proposition = "(exists x:int :: (exists y:int :: P(x+y)) && P(x)) == " +
      "(exists z:int :: P(z))";
    const string declarations = "ghost predicate P(i:int) decreases i { i<=0 || P(i-1) } ";
    var source = declarations + (opaque
      ? "lemma L() { opaque ensures " + proposition + " { assert " + proposition + "; } }"
      : "lemma L() ensures " + proposition + " { assert " + proposition + "; }");
    var packages = new List<BoogieGenerator.PropositionLowering>();
    await Translate(source, true, refresh, packages.Add);
    var explicitCheck = Assert.Single(packages.Where(p => p.Source.Resolved is BinaryExpr &&
      p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.CheckedExpression));
    var preparation = opaque
      ? BoogieGenerator.ObligationPreparation.GuardedIntroduction
      : BoogieGenerator.ObligationPreparation.DeclaredContract;
    var implicitChecks = packages.Where(p => p.Source.Resolved is BinaryExpr &&
      p.Inputs.Preparation == preparation).ToList();
    Assert.NotEmpty(implicitChecks);
    Assert.All(implicitChecks, p => Assert.Equal(ObligationFingerprint.Content(explicitCheck),
      ObligationFingerprint.Content(p)));
    Assert.Contains("$LS", ObligationFingerprint.Content(explicitCheck));
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
    Assert.All(implicitChecks, c => Assert.Equal(explicitChecks[0], ObligationFingerprint.Expression(c.Expr)));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OldArrayAccessChecksOneAssertionEquivalentAllocationPredicate(bool refresh) {
    const string source = "lemma L(a:array<int>,i:nat) requires i<a.Length " +
      "{ assert old(allocated(a)); var x := old(a[i]); }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.Contains("Impl") && p.Name.EndsWith(".L"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>().ToList();
    var explicitCheck = Assert.Single(checks.Where(c => c.Description is AssertStatementDescription));
    var allocation = Assert.Single(checks.Where(c => c.Description is IsAllocated));
    Assert.Equal(ObligationFingerprint.Expression(explicitCheck.Expr),
      ObligationFingerprint.Expression(allocation.Expr));
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
  public async Task CastChecksUseOneGuardedAssertionStyleProof() {
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
    Assert.Equal(2, original.Length); // Legacy rechecked the target-typed converted result.
    Assert.Single(enriched);
    Assert.Contains("binary:Imp(", enriched[0]);
  }

  [Fact]
  public async Task AllocationChecksUseOneCanonicalRepresentation() {
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
    Assert.Equal(original.Length, enriched.Length);
    Assert.All(enriched, check => Assert.Contains("$IsAllocBox", check));
  }


  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task MethodCallsHaveOneLocalPreconditionProof(bool refresh) {
    const string source = "ghost predicate P(x:int) { x>=0 } lemma Use(x:int) requires P(x) {} " +
      "lemma L(x:int) requires x>=0 { Use(x); }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is PreconditionSatisfied).ToList();
    Assert.Single(checks);
    var calls = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.CallCmd>()
      .Where(c => c.callee.EndsWith(".Use")).ToList();
    Assert.Single(calls);
    var procedure = programs.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == calls[0].callee);
    Assert.DoesNotContain(procedure.Requires, requirement => !requirement.Free &&
      ObligationFingerprint.Expression(requirement.Condition).Contains(".P"));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task UnrelatedBodyTermsDoNotChangeImplicitCheckSupport(bool refresh) {
    const string declarations = "ghost predicate P(x:int) { x>=0 } ghost function Q(x:int):int { x } ";
    async Task<string[]> Support(string prefix) {
      var packages = new List<BoogieGenerator.PropositionLowering>();
      await Translate(declarations + "lemma L(x:int) requires x>=0 ensures P(x) { " + prefix + " }",
        true, refresh, packages.Add);
      return packages.Where(p => p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.DeclaredContract &&
        p.Source.Resolved is FunctionCallExpr { Function.Name: "P" })
        .Select(ObligationFingerprint.Content).ToArray();
    }
    var empty = await Support("");
    Assert.NotEmpty(empty);
    Assert.Equal(empty, await Support("assert Q(1)==1; assert forall z:int {:trigger Q(z)} :: Q(z)==z;"));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task EarlyReturnsAndFallthroughKeepOneLocalPostconditionSite(bool refresh) {
    const string source = "ghost predicate P(x:int) { x>=0 } lemma L(x:int,b:bool) " +
      "requires x>=0 ensures P(x) { if b { return; } }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is EnsuresDescription).ToList();
    Assert.Equal(2, checks.Count);
    var procedure = programs.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == implementation.Name);
    Assert.DoesNotContain(procedure.Ensures, ensures => !ensures.Free && ensures.Description is EnsuresDescription);
  }

}
