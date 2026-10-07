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

    var legacy = await Translate(source, false, refresh);
    var procedure = legacy.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == implementation.Name);
    var originalChecks = procedure.Ensures.Where(ensures => !ensures.Free).ToList();
    Assert.All(originalChecks, check =>
      Assert.Equal(-1, Bpl.QKeyValue.FindIntAttribute(check.Attributes, "subsumption", -1)));
    Assert.All(originalChecks, original => Assert.Contains(localChecks,
      check => ObligationFingerprint.Expression(check.Expr) == ObligationFingerprint.Expression(original.Condition)));
    var enabledProcedure = enabled.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == implementation.Name);
    Assert.All(enabledProcedure.Ensures, check => Assert.True(check.Free));
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

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OriginalPostconditionFormulasAreCheckedAtExplicitReturnsAndFallthrough(bool refresh) {
    const string source = "lemma L(x:int) returns (r:int) ensures r==x { if x==0 { r:=x; return; } r:=x; }";
    var legacy = await Translate(source, false, refresh);
    var enabled = await Translate(source, true, refresh);
    var implementation = enabled.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var procedure = legacy.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == implementation.Name);
    var localChecks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is EnsuresDescription).ToList();
    foreach (var original in procedure.Ensures.Where(e => !e.Free)) {
      Assert.True(localChecks.Count(c => ObligationFingerprint.Expression(c.Expr) ==
        ObligationFingerprint.Expression(original.Condition)) >= 2);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task InheritedOriginalPostconditionsStillHaveCheckedLocalGoals(bool refresh) {
    const string source = "module A { method L() returns (r:int) ensures r==0 { r:=0; } } " +
      "module B refines A { method L... { ...; } }";
    var legacy = await Translate(source, false, refresh);
    var enabled = await Translate(source, true, refresh);
    var implementations = enabled.SelectMany(p => p.Implementations).Where(p => p.Name.EndsWith(".L")).ToList();
    Assert.NotEmpty(implementations);
    foreach (var implementation in implementations) {
      var original = legacy.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
        .Single(p => p.Name == implementation.Name);
      var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>().ToList();
      Assert.All(original.Ensures.Where(e => !e.Free), goal => Assert.Contains(checks,
        check => ObligationFingerprint.Expression(check.Expr) == ObligationFingerprint.Expression(goal.Condition)));
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task RelocatedQuantifiersRetainTheirLexicalBindings(bool refresh) {
    const string source = "ghost function F(x:int):int { x } lemma L() " +
      "ensures forall x:int {:trigger F(x)} :: F(x)==x {}";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    void Check(Bpl.Expr expression, HashSet<Bpl.Variable> bound) {
      switch (expression) {
        case Bpl.IdentifierExpr { Decl: Bpl.BoundVariable variable }:
          Assert.Contains(variable, bound);
          break;
        case Bpl.NAryExpr application:
          foreach (var arg in application.Args) { Check(arg, bound); }
          break;
        case Bpl.OldExpr old:
          Check(old.Expr, bound);
          break;
        case Bpl.QuantifierExpr quantifier:
          var scoped = new HashSet<Bpl.Variable>(bound.Concat(quantifier.Dummies));
          Check(quantifier.Body, scoped);
          for (var trigger = quantifier.Triggers; trigger != null; trigger = trigger.Next) {
            foreach (var term in trigger.Tr) { Check(term, scoped); }
          }
          break;
      }
    }
    var goals = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is EnsuresDescription).ToList();
    Assert.NotEmpty(goals);
    foreach (var goal in goals) { Check(goal.Expr, new HashSet<Bpl.Variable>()); }
  }

  [Fact]
  public async Task LocalMethodCallChecksPublishTheirCheckedPieces() {
    const string source = "ghost predicate P(x:int) { x>=0 && x<=100 } lemma Use(x:int) requires P(x) {} " +
      "lemma L(x:int) requires 0<=x<=100 { Use(x); }";
    var programs = await Translate(source, true);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var checks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is PreconditionSatisfied).ToList();
    Assert.True(checks.Count >= 2);
    Assert.All(checks, check => Assert.Equal(-1, Bpl.QKeyValue.FindIntAttribute(check.Attributes, "subsumption", -1)));
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task MethodContractProofCutRetainsEveryCheckedPublicationFact(bool refresh, bool call) {
    var source = "ghost predicate P(x:int) { x>=0 && x<=100 } " +
      (call ? "lemma Use(x:int) requires P(x) {} lemma L(x:int) requires 0<=x<=100 { Use(x); }"
            : "lemma L(x:int) requires 0<=x<=100 ensures P(x) {}");
    var packages = new List<BoogieGenerator.PropositionLowering>();
    var programs = await Translate(source, true, refresh, packages.Add);
    var summary = packages.Single(p => p.Source.Resolved is FunctionCallExpr { Function.Name: "P" } &&
      p.Inputs.Preparation == BoogieGenerator.ObligationPreparation.DeclaredContract).Summary;
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var fingerprint = ObligationFingerprint.Expression(summary);
    var check = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      ObligationFingerprint.Expression(c.Expr) == fingerprint));
    Assert.Equal(-1, Bpl.QKeyValue.FindIntAttribute(check.Attributes, "subsumption", -1));
    var publicationFacts = commands.OfType<Bpl.AssumeCmd>().Select(c =>
      ObligationFingerprint.Expression(c.Expr)).ToList();
    var goals = commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description is EnsuresDescription or PreconditionSatisfied).ToList();
    Assert.All(goals, goal => Assert.Contains(ObligationFingerprint.Expression(goal.Expr), publicationFacts));
    var verificationBlock = Assert.Single(implementation.Blocks.Where(b => b.Cmds.Contains(check)));
    Assert.Contains(verificationBlock.Cmds.OfType<Bpl.AssumeCmd>(), c => c.Expr.Equals(Bpl.Expr.False));
    Assert.DoesNotContain(verificationBlock.Cmds.OfType<Bpl.AssumeCmd>(), c =>
      ObligationFingerprint.Expression(c.Expr) == fingerprint);
  }

  [Theory]
  [InlineData(false, false, false)]
  [InlineData(false, true, false)]
  [InlineData(true, false, false)]
  [InlineData(true, true, false)]
  [InlineData(false, false, true)]
  [InlineData(false, true, true)]
  [InlineData(true, false, true)]
  [InlineData(true, true, true)]
  public async Task ContractFuelSupportChecksExistingSuccessorInstances(bool refresh, bool call, bool generic) {
    var parameters = generic ? "<T>" : "";
    var source = $"ghost function F{parameters}(n:nat):int {{ if n==0 then 0 else F{parameters}(n-1)+1 }} " +
      (call ? $"lemma Use{parameters}(n:nat) requires F{parameters}(n)==n {{}} lemma L{parameters}(n:nat) {{ Use{parameters}(n); }}"
            : $"lemma L{parameters}(n:nat) ensures F{parameters}(n)==n {{}}");
    var fuelPosition = generic ? 1 : 0;
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var agreement = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "fuel layer agreement"));
    Assert.Equal(-1, Bpl.QKeyValue.FindIntAttribute(agreement.Attributes, "subsumption", -1));
    void Check(Bpl.Expr expression) {
      var operation = Assert.IsType<Bpl.NAryExpr>(expression);
      if (operation.Fun is Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And }) {
        Assert.All(operation.Args, Check);
        return;
      }
      Assert.IsType<Bpl.BinaryOperator>(operation.Fun);
      Assert.Equal(Bpl.BinaryOperator.Opcode.Eq, ((Bpl.BinaryOperator)operation.Fun).Op);
      var upper = Assert.IsType<Bpl.NAryExpr>(operation.Args[0]);
      var lower = Assert.IsType<Bpl.NAryExpr>(operation.Args[1]);
      Assert.EndsWith(".F", Assert.IsType<Bpl.FunctionCall>(upper.Fun).FunctionName);
      Assert.Equal(upper.Fun, lower.Fun);
      var successor = Assert.IsType<Bpl.NAryExpr>(upper.Args[fuelPosition]);
      Assert.Equal("$LS", Assert.IsType<Bpl.FunctionCall>(successor.Fun).FunctionName);
      Assert.Equal(ObligationFingerprint.Expression(successor.Args[0]), ObligationFingerprint.Expression(lower.Args[fuelPosition]));
      Assert.Equal(upper.Args.Count, lower.Args.Count);
      for (var i = 0; i < upper.Args.Count; i++) {
        if (i != fuelPosition) { Assert.Same(upper.Args[i], lower.Args[i]); }
      }
    }
    Check(agreement.Expr);
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), c =>
      ObligationFingerprint.Expression(c.Expr) == ObligationFingerprint.Expression(agreement.Expr));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ContractFuelSupportDoesNotExtractBoundApplications(bool refresh) {
    const string source = "ghost function F(n:nat):int { if n==0 then 0 else F(n-1)+1 } " +
      "lemma L() ensures forall n:nat :: F(n)==n {}";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    Assert.DoesNotContain(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>(),
      c => c.Description?.ShortDescription == "fuel layer agreement");
  }

  [Theory]
  [InlineData(false, false, false)]
  [InlineData(false, true, false)]
  [InlineData(true, false, false)]
  [InlineData(true, true, false)]
  [InlineData(false, false, true)]
  [InlineData(false, true, true)]
  [InlineData(true, false, true)]
  [InlineData(true, true, true)]
  public async Task ContractBodyAgreementRetainsTheActualHeapAndGuard(bool refresh, bool call, bool old) {
    const string declarations = "class C { var i:int } ghost predicate P(c:C) reads c { c.i>=0 && c.i<=100 } ";
    var condition = old ? "old(P(c))" : "P(c)";
    var source = declarations + (call
      ? $"lemma Use(c:C) requires {condition} {{}} lemma L(c:C) requires 0<=c.i<=100 {{ Use(c); }}"
      : $"lemma L(c:C) requires 0<=c.i<=100 ensures {condition} {{}}");
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var agreement = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "predicate body agreement"));
    Assert.Equal(-1, Bpl.QKeyValue.FindIntAttribute(agreement.Attributes, "subsumption", -1));
    var heapGuard = Assert.IsType<Bpl.NAryExpr>(agreement.Expr);
    Assert.Equal(Bpl.BinaryOperator.Opcode.Imp, Assert.IsType<Bpl.BinaryOperator>(heapGuard.Fun).Op);
    var heap = Assert.IsType<Bpl.NAryExpr>(heapGuard.Args[0]);
    Assert.Equal("$IsGoodHeap", Assert.IsType<Bpl.FunctionCall>(heap.Fun).FunctionName);
    var permissionGuard = Assert.IsType<Bpl.NAryExpr>(heapGuard.Args[1]);
    var permission = Assert.IsType<Bpl.NAryExpr>(permissionGuard.Args[0]);
    Assert.EndsWith(".P#canCall", Assert.IsType<Bpl.FunctionCall>(permission.Fun).FunctionName);
    var equality = Assert.IsType<Bpl.NAryExpr>(permissionGuard.Args[1]);
    Assert.Equal(Bpl.BinaryOperator.Opcode.Eq, Assert.IsType<Bpl.BinaryOperator>(equality.Fun).Op);
    var application = Assert.IsType<Bpl.NAryExpr>(equality.Args[0]);
    Assert.EndsWith(".P", Assert.IsType<Bpl.FunctionCall>(application.Fun).FunctionName);
    Assert.Same(application.Args[0], heap.Args[0]);
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), c =>
      ObligationFingerprint.Expression(c.Expr) == ObligationFingerprint.Expression(agreement.Expr));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ContractBodyAgreementDoesNotExpandBlindContexts(bool refresh) {
    const string source = "ghost predicate P(x:int) { x>=0 && x<=100 } " +
      "lemma Use(x:int) requires P(x) {} lemma L(x:int) requires P(x) ensures P(x) { hide P; Use(x); }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    Assert.DoesNotContain(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>(),
      c => c.Description?.ShortDescription == "predicate body agreement");
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
    var packages = new List<BoogieGenerator.PropositionLowering>();
    var programs=await Translate(source,true,observer:packages.Add);
    var implementation=programs.SelectMany(p=>p.Implementations).Single(p=>p.Name.EndsWith(".L"));
    var checks=implementation.Blocks.SelectMany(b=>b.Cmds).OfType<Bpl.AssertCmd>().ToList();
    var explicitChecks=checks.Where(c=>c.Description is AssertStatementDescription).Select(c=>ObligationFingerprint.Expression(c.Expr)).ToList();
    var implicitChecks=checks.Where(c=>c.Description is PreconditionSatisfied).Select(c=>ObligationFingerprint.Expression(c.Expr)).ToList();
    Assert.NotEmpty(implicitChecks);
    // Every assertion-style induction goal remains. The additional checked
    // summary must match the explicit assertion's publication form too.
    Assert.All(explicitChecks, check => Assert.Contains(check, implicitChecks));
    var publication = packages.Single(p => p.Inputs.Preparation ==
      BoogieGenerator.ObligationPreparation.CheckedExpression).Summary;
    var permitted = explicitChecks.Append(ObligationFingerprint.Expression(publication)).ToList();
    Assert.All(implicitChecks, check => Assert.Contains(check, permitted));
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
