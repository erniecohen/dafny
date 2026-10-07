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
    foreach (var original in originalChecks) {
      Assert.Contains(enabledProcedure.Ensures, check => !check.Free &&
        ObligationFingerprint.Expression(check.Condition) == ObligationFingerprint.Expression(original.Condition));
      Assert.Contains(enabledProcedure.Ensures, check => check.Free &&
        ObligationFingerprint.Expression(check.Condition) == ObligationFingerprint.Expression(original.Condition));
    }
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task OriginalProcedurePostconditionGoalsRemainAlongsideTheirLocalCopies(bool refresh, bool quantified) {
    var source = quantified
      ? "ghost function F(x:int):int { x } lemma L(b:bool) " +
        "ensures forall x:int {:trigger F(x)} :: F(x)==x { if b { return; } }"
      : "lemma L(x:int,b:bool) returns (r:int) ensures r==x { r:=x; if b { return; } }";
    var legacy = await Translate(source, false, refresh);
    var enabled = await Translate(source, true, refresh);
    var implementation = enabled.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
    var original = legacy.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == implementation.Name);
    var retained = enabled.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == implementation.Name);
    var localChecks = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description is EnsuresDescription).ToList();
    Assert.NotEmpty(original.Ensures.Where(e => !e.Free));
    foreach (var goal in original.Ensures.Where(e => !e.Free)) {
      var fingerprint = ObligationFingerprint.Expression(goal.Condition);
      Assert.Contains(retained.Ensures, e => !e.Free &&
        ObligationFingerprint.Expression(e.Condition) == fingerprint);
      Assert.Contains(retained.Ensures, e => e.Free &&
        ObligationFingerprint.Expression(e.Condition) == fingerprint);
      Assert.True(localChecks.Count(c => ObligationFingerprint.Expression(c.Expr) == fingerprint) >= 2);
    }
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
  public async Task GroundContractFuelSupportDoesNotExtractBoundApplications(bool refresh) {
    const string source = "ghost function F(n:nat):int { if n==0 then 0 else F(n-1)+1 } " +
      "lemma L() ensures forall n:nat :: F(n)==n {}";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    Assert.DoesNotContain(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>(),
      c => c.Description?.ShortDescription == "fuel layer agreement");
  }

  [Theory]
  [InlineData(false, false, false)]
  [InlineData(false, false, true)]
  [InlineData(false, true, false)]
  [InlineData(false, true, true)]
  [InlineData(true, false, false)]
  [InlineData(true, false, true)]
  [InlineData(true, true, false)]
  [InlineData(true, true, true)]
  public async Task ContractContextFuelSupportIncludesExistingLoopInvariants(bool refresh, bool call, bool quantified) {
    var invariant = quantified ? "forall m:nat :: F(m)==m" : "F(i)==i";
    var source = "ghost function F(n:nat):int { if n==0 then 0 else F(n-1)+1 } " +
      "lemma Use() requires true {} lemma L(n:nat) " + (call ? "" : "ensures true ") +
      "{ var i:=0; while i<n invariant " + invariant + " { i:=i+1; } " + (call ? "Use();" : "") + " }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var agreement = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "context fuel layer agreement"));
    Assert.Equal(-1, Bpl.QKeyValue.FindIntAttribute(agreement.Attributes, "subsumption", -1));
    void Check(Bpl.Expr expression) {
      if (expression is Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And } } conjunction) {
        Assert.All(conjunction.Args, Check);
        return;
      }
      if (quantified) {
        var closure = Assert.IsType<Bpl.ForallExpr>(expression);
        Assert.Single(closure.Dummies);
        var referenced = new ScopedFuelTestReferences();
        referenced.VisitExpr(closure.Body);
        Assert.All(referenced.Variables.OfType<Bpl.BoundVariable>(), variable => Assert.Contains(variable, closure.Dummies));
        expression = closure.Body;
      }
      var equality = Assert.IsType<Bpl.NAryExpr>(expression);
      Assert.Equal(Bpl.BinaryOperator.Opcode.Eq, Assert.IsType<Bpl.BinaryOperator>(equality.Fun).Op);
      var upper = Assert.IsType<Bpl.NAryExpr>(equality.Args[0]);
      var lower = Assert.IsType<Bpl.NAryExpr>(equality.Args[1]);
      Assert.EndsWith(".F", Assert.IsType<Bpl.FunctionCall>(upper.Fun).FunctionName);
      var successor = Assert.IsType<Bpl.NAryExpr>(upper.Args[0]);
      Assert.Equal("$LS", Assert.IsType<Bpl.FunctionCall>(successor.Fun).FunctionName);
      Assert.Equal(ObligationFingerprint.Expression(successor.Args[0]), ObligationFingerprint.Expression(lower.Args[0]));
      Assert.Equal(upper.Args.Count, lower.Args.Count);
      for (var i = 1; i < upper.Args.Count; i++) {
        Assert.Equal(ObligationFingerprint.Expression(upper.Args[i]), ObligationFingerprint.Expression(lower.Args[i]));
      }
    }
    Check(agreement.Expr);
    var fingerprint = ObligationFingerprint.Expression(agreement.Expr);
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), c => ObligationFingerprint.Expression(c.Expr) == fingerprint);
    var checkingBlock = Assert.Single(implementation.Blocks.Where(block => block.Cmds.Contains(agreement)));
    Assert.Contains(checkingBlock.Cmds.OfType<Bpl.AssumeCmd>(), c => c.Expr.Equals(Bpl.Expr.False));
    Assert.DoesNotContain(checkingBlock.Cmds.OfType<Bpl.AssumeCmd>(), c => ObligationFingerprint.Expression(c.Expr) == fingerprint);
    // The original invariant check remains; support does not replace it.
    Assert.Contains(commands.OfType<Bpl.AssertCmd>(), c => c.Description is LoopInvariant);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ContractContextFuelSupportClosesInvariantLets(bool refresh) {
    const string source = "ghost function F(n:nat):int { if n==0 then 0 else F(n-1)+1 } " +
      "lemma L(n:nat) ensures true { var i:=0; while i<n " +
      "invariant forall m:nat :: (var k:=m; F(k)==m) { i:=i+1; } }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
    var agreement = Assert.Single(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "context fuel layer agreement"));
    void Check(Bpl.Expr expression) {
      if (expression is Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And } } conjunction) {
        Assert.All(conjunction.Args, Check);
        return;
      }
      var closure = Assert.IsType<Bpl.ForallExpr>(expression);
      Assert.Single(closure.Dummies);
      var referenced = new ScopedFuelTestReferences();
      referenced.VisitExpr(closure.Body);
      Assert.All(referenced.Variables.OfType<Bpl.BoundVariable>(), variable => Assert.Contains(variable, closure.Dummies));
      var equality = Assert.IsType<Bpl.NAryExpr>(closure.Body);
      var application = Assert.IsType<Bpl.NAryExpr>(equality.Args[0]);
      Assert.Same(closure.Dummies[0], Assert.IsType<Bpl.IdentifierExpr>(application.Args.Last()).Decl);
    }
    Check(agreement.Expr);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task ContractContextFuelSupportDoesNotTransportRevealedApplications(bool refresh, bool nested) {
    var source = "opaque ghost function F(n:int):int { n } " +
      "ghost function G(n:int):int decreases if n>0 then n else 0 { if n<=0 then 0 else G(n-1)+1 } " +
      "lemma L(n:int) ensures true { assert true by { reveal F; assert " +
      (nested ? "G(F(n))>=0" : "F(n)==n") + "; } }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
    Assert.DoesNotContain(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>(), c =>
      c.Description?.ShortDescription == "context fuel layer agreement");
    async Task<string[]> ScopeCommands(bool enabled) => ObligationFingerprint.Emit(await Translate(source, enabled, refresh))
      .Split('\n').Select(line => line.Trim()).Where(line =>
        line is "push;" or "pop;" || line.StartsWith("hide ") || line.StartsWith("reveal ")).ToArray();
    Assert.Equal(await ScopeCommands(false), await ScopeCommands(true));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ContractContextFuelSupportRetainsImplementationLocalTerms(bool refresh) {
    const string source = "ghost function F(n:nat):int { if n==0 then 0 else F(n-1)+1 } " +
      "lemma L(n:nat) ensures true { if n>0 { var x:=n; assert F(x)==x; x:=0; } }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var agreement = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "context fuel layer agreement"));
    Assert.Contains(".F(", ObligationFingerprint.Expression(agreement.Expr));
    Assert.Contains(commands.OfType<Bpl.AssertCmd>(), c => c.Description is AssertStatementDescription);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ContractContextFuelCollectionDoesNotRevisitGeneratedProofCuts(bool refresh) {
    const string source = "ghost function F(n:nat):int { if n==0 then 0 else F(n-1)+1 } " +
      "lemma Use() requires true {} lemma L(n:nat) ensures true { var i:=0; " +
      "while i<n invariant forall m:nat :: F(m)==m { i:=i+1; } Use(); Use(); }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var agreements = commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "context fuel layer agreement").ToList();
    Assert.Equal(3, agreements.Count);
    int Instances(Bpl.Expr expression) => expression is Bpl.NAryExpr {
      Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And }
    } conjunction ? conjunction.Args.Sum(Instances) : 1;
    var count = Instances(agreements[0].Expr);
    Assert.True(count > 0);
    Assert.All(agreements, agreement => Assert.Equal(count, Instances(agreement.Expr)));
    Assert.Equal(2, commands.OfType<Bpl.CallCmd>().Count(call => call.callee.EndsWith(".Use")));
    Assert.Contains(commands.OfType<Bpl.AssertCmd>(), c => c.Description is LoopInvariant);
    foreach (var agreement in agreements) {
      var fingerprint = ObligationFingerprint.Expression(agreement.Expr);
      Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), c => ObligationFingerprint.Expression(c.Expr) == fingerprint);
    }
  }

  [Theory]
  [InlineData(false, false, false)]
  [InlineData(false, false, true)]
  [InlineData(false, true, false)]
  [InlineData(false, true, true)]
  [InlineData(true, false, false)]
  [InlineData(true, false, true)]
  [InlineData(true, true, false)]
  [InlineData(true, true, true)]
  public async Task ContractContextFuelSupportRetainsOldHeapArguments(bool refresh, bool call, bool quantified) {
    var invariant = quantified ? "forall m:nat :: old(F(c,m))==old(c.x)+m" : "old(F(c,0))==old(c.x)";
    var source = "class C { ghost var x:int } ghost function F(c:C,n:nat):int reads c " +
      "{ if n==0 then c.x else F(c,n-1)+1 } lemma Use() requires true {} " +
      "lemma L(c:C,n:nat) " + (call ? "" : "ensures true ") +
      "{ var i:=0; while i<n invariant " + invariant + " { i:=i+1; } " + (call ? "Use();" : "") + " }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var agreement = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "context fuel layer agreement"));
    var closed = 0;
    void Check(Bpl.Expr expression) {
      if (expression is Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And } } conjunction) {
        Assert.All(conjunction.Args, Check);
        return;
      }
      if (expression is Bpl.ForallExpr closure) {
        closed++;
        Assert.Single(closure.Dummies);
        var referenced = new ScopedFuelTestReferences();
        referenced.VisitExpr(closure.Body);
        Assert.All(referenced.Variables.OfType<Bpl.BoundVariable>(), variable => Assert.Contains(variable, closure.Dummies));
        expression = closure.Body;
      }
      var equality = Assert.IsType<Bpl.NAryExpr>(expression);
      Assert.Equal(Bpl.BinaryOperator.Opcode.Eq, Assert.IsType<Bpl.BinaryOperator>(equality.Fun).Op);
      var upper = Assert.IsType<Bpl.NAryExpr>(equality.Args[0]);
      var lower = Assert.IsType<Bpl.NAryExpr>(equality.Args[1]);
      Assert.EndsWith(".F", Assert.IsType<Bpl.FunctionCall>(upper.Fun).FunctionName);
      Assert.IsType<Bpl.OldExpr>(upper.Args[1]);
      Assert.IsType<Bpl.OldExpr>(lower.Args[1]);
      var successor = Assert.IsType<Bpl.NAryExpr>(upper.Args[0]);
      Assert.Equal("$LS", Assert.IsType<Bpl.FunctionCall>(successor.Fun).FunctionName);
      Assert.Equal(ObligationFingerprint.Expression(successor.Args[0]), ObligationFingerprint.Expression(lower.Args[0]));
      for (var i = 1; i < upper.Args.Count; i++) {
        Assert.Equal(ObligationFingerprint.Expression(upper.Args[i]), ObligationFingerprint.Expression(lower.Args[i]));
      }
    }
    Check(agreement.Expr);
    Assert.Equal(quantified, closed > 0);
    Assert.Contains(commands.OfType<Bpl.AssertCmd>(), c => c.Description is LoopInvariant);
    var fingerprint = ObligationFingerprint.Expression(agreement.Expr);
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), c => ObligationFingerprint.Expression(c.Expr) == fingerprint);
  }

  [Theory]
  [InlineData(false, false, false, false)]
  [InlineData(false, false, false, true)]
  [InlineData(false, false, true, false)]
  [InlineData(false, false, true, true)]
  [InlineData(false, true, false, false)]
  [InlineData(false, true, false, true)]
  [InlineData(false, true, true, false)]
  [InlineData(false, true, true, true)]
  [InlineData(true, false, false, false)]
  [InlineData(true, false, false, true)]
  [InlineData(true, false, true, false)]
  [InlineData(true, false, true, true)]
  [InlineData(true, true, false, false)]
  [InlineData(true, true, false, true)]
  [InlineData(true, true, true, false)]
  [InlineData(true, true, true, true)]
  public async Task RefinedContextFuelSupportUsesResolvedSignatures(bool refresh, bool call, bool quantified, bool generic) {
    var typeParameter = generic ? "<T>" : "";
    var parameter = generic ? ",u:T" : "";
    var argument = generic ? ",u" : "";
    var invariant = quantified ? "forall m:nat :: old(F(c,m" + argument + "))==old(c.x)+m" :
      "old(F(c,0" + argument + "))==old(c.x)";
    var source = "module M0 { class C { ghost var x:int } ghost function F" + typeParameter +
      "(c:C,n:nat" + parameter + "):int reads c { if n==0 then c.x else F(c,n-1" + argument + ")+1 } " +
      "lemma Use() requires true {} lemma L" + typeParameter + "(c:C,n:nat" + parameter + ") " +
      (call ? "" : "ensures true ") + "} module M1 refines M0 { lemma L ... " +
      "{ var i:=0; while i<n invariant " + invariant + " { i:=i+1; } " + (call ? "Use();" : "") + " } }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$M1.") && p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var agreement = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "context fuel layer agreement"));
    var closed = 0;
    void Check(Bpl.Expr expression) {
      if (expression is Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And } } conjunction) {
        Assert.All(conjunction.Args, Check);
        return;
      }
      if (expression is Bpl.ForallExpr closure) {
        closed++;
        Assert.Single(closure.Dummies);
        var referenced = new ScopedFuelTestReferences();
        referenced.VisitExpr(closure.Body);
        Assert.All(referenced.Variables.OfType<Bpl.BoundVariable>(), variable => Assert.Contains(variable, closure.Dummies));
        expression = closure.Body;
      }
      var equality = Assert.IsType<Bpl.NAryExpr>(expression);
      Assert.Equal(Bpl.BinaryOperator.Opcode.Eq, Assert.IsType<Bpl.BinaryOperator>(equality.Fun).Op);
      var upper = Assert.IsType<Bpl.NAryExpr>(equality.Args[0]);
      var lower = Assert.IsType<Bpl.NAryExpr>(equality.Args[1]);
      Assert.Equal("M1.__default.F", Assert.IsType<Bpl.FunctionCall>(upper.Fun).FunctionName);
      var position = generic ? 1 : 0;
      Assert.IsType<Bpl.OldExpr>(upper.Args[position + 1]);
      Assert.IsType<Bpl.OldExpr>(lower.Args[position + 1]);
      var successor = Assert.IsType<Bpl.NAryExpr>(upper.Args[position]);
      Assert.Equal("$LS", Assert.IsType<Bpl.FunctionCall>(successor.Fun).FunctionName);
      Assert.Equal(ObligationFingerprint.Expression(successor.Args[0]), ObligationFingerprint.Expression(lower.Args[position]));
      for (var i = 0; i < upper.Args.Count; i++) {
        if (i != position) {
          Assert.Equal(ObligationFingerprint.Expression(upper.Args[i]), ObligationFingerprint.Expression(lower.Args[i]));
        }
      }
    }
    Check(agreement.Expr);
    Assert.Equal(quantified, closed > 0);
    Assert.Contains(commands.OfType<Bpl.AssertCmd>(), c => c.Description is LoopInvariant);
    var fingerprint = ObligationFingerprint.Expression(agreement.Expr);
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), c => ObligationFingerprint.Expression(c.Expr) == fingerprint);
  }

  [Theory]
  [InlineData(false, false, false)]
  [InlineData(false, false, true)]
  [InlineData(false, true, false)]
  [InlineData(false, true, true)]
  [InlineData(true, false, false)]
  [InlineData(true, false, true)]
  [InlineData(true, true, false)]
  [InlineData(true, true, true)]
  public async Task ContractDefinitionSupportChecksGuardedRecursiveEqualities(bool refresh, bool call, bool quantified) {
    var condition = quantified ? "forall k:nat :: F(k)==k" : "F(n)==n";
    var source = "ghost function F(n:nat):int { if n==0 then 0 else F(n-1)+1 } " +
      (call ? $"lemma Use(n:nat) requires {condition} {{}} lemma L(n:nat) {{ Use(n); }}"
            : $"lemma L(n:nat) ensures {condition} {{}}");
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var agreement = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "guarded definition agreement"));
    Assert.Equal(-1, Bpl.QKeyValue.FindIntAttribute(agreement.Attributes, "subsumption", -1));
    Assert.Contains("F#canCall", agreement.Expr.ToString());
    void Check(Bpl.Expr expression) {
      if (expression is Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And } } conjunction) {
        Assert.All(conjunction.Args, Check);
        return;
      }
      if (expression is Bpl.ForallExpr closure) {
        Assert.NotEmpty(closure.Dummies);
        var references = new ScopedFuelTestReferences();
        references.VisitExpr(closure.Body);
        Assert.All(references.Variables.OfType<Bpl.BoundVariable>(), variable => Assert.Contains(variable, closure.Dummies));
        expression = closure.Body;
      }
      var implication = Assert.IsType<Bpl.NAryExpr>(expression);
      Assert.Equal(Bpl.BinaryOperator.Opcode.Imp, Assert.IsType<Bpl.BinaryOperator>(implication.Fun).Op);
      var equality = Assert.IsType<Bpl.NAryExpr>(implication.Args[1]);
      Assert.Equal(Bpl.BinaryOperator.Opcode.Eq, Assert.IsType<Bpl.BinaryOperator>(equality.Fun).Op);
      var application = Assert.IsType<Bpl.NAryExpr>(equality.Args[0]);
      Assert.EndsWith(".F", Assert.IsType<Bpl.FunctionCall>(application.Fun).FunctionName);
      Assert.Contains("$LS", application.ToString());
    }
    Check(agreement.Expr);
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), c =>
      ObligationFingerprint.Expression(c.Expr) == ObligationFingerprint.Expression(agreement.Expr));
    Assert.Contains(commands.OfType<Bpl.AssertCmd>(), c => c.Description?.ShortDescription is "ensures" or "precondition");
  }

  [Theory]
  [InlineData(false, "n==0")]
  [InlineData(true, "n==0")]
  [InlineData(false, "n<0")]
  [InlineData(true, "n<0")]
  [InlineData(false, "(forall k:int :: k>=n)")]
  [InlineData(true, "(forall k:int :: k>=n)")]
  public async Task DefinitionInstancesKeepTheirChecksWithLegalNewTriggerHints(bool refresh, string argument) {
    var source = "ghost function T(n:int):int { n } ghost function F(n:int,p:bool):int { n } " +
      $"lemma L() ensures forall n:int {{:trigger T(n)}} :: F(n,{argument})==n {{}}";
    var programs = await Translate(source, true, refresh);
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    foreach (var backend in programs) {
      Assert.Equal(0, backend.Resolve(options));
      Assert.Equal(0, backend.Typecheck(options));
    }
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var support = commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "guarded definition agreement").ToList();
    Assert.NotEmpty(support);
    Assert.All(support, check => Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), fact =>
      ObligationFingerprint.Expression(fact.Expr) == ObligationFingerprint.Expression(check.Expr)));
    Assert.Contains(commands.OfType<Bpl.AssertCmd>(), c => c.Description is EnsuresDescription);
    // The source's valid pattern remains in the original procedure goal.
    var procedure = programs.SelectMany(p => p.TopLevelDeclarations).OfType<Bpl.Procedure>()
      .Single(p => p.Name == implementation.Name);
    Assert.Contains(procedure.Ensures, e => !e.Free && e.Condition.ToString().Contains(".T("));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SubstitutedDefinitionBodyPatternsRemainLegalWithoutDroppingTheProposition(bool refresh) {
    const string source = "ghost function G(n:int,p:bool):int { n } " +
      "ghost predicate F(n:int,p:bool) { forall k:int {:trigger G(k,p)} :: G(k,p)==k } " +
      "lemma L(n:int) ensures F(n,n<0) {}";
    var programs = await Translate(source, true, refresh);
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    foreach (var backend in programs) {
      Assert.Equal(0, backend.Resolve(options));
      Assert.Equal(0, backend.Typecheck(options));
    }
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var support = implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description?.ShortDescription == "guarded definition agreement").ToList();
    Assert.NotEmpty(support);
    Assert.Contains(support, check => check.Expr.ToString().Contains("forall"));
    Assert.Contains(support, check => check.Expr.ToString().Contains("F#canCall"));
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task ContractDefinitionSupportDoesNotTransportOpaqueOrHiddenDefinitions(bool refresh, bool hidden) {
    var source = (hidden ? "ghost" : "opaque ghost") + " function F(n:nat):int { if n==0 then 0 else F(n-1)+1 } " +
      "lemma L(n:nat) ensures F(n)==n { " + (hidden ? "hide *;" : "") + " }";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    Assert.DoesNotContain(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>(),
      c => c.Description?.ShortDescription == "guarded definition agreement");
    Assert.DoesNotContain(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssumeCmd>(),
      c => Bpl.QKeyValue.FindStringAttribute(c.Attributes, "contractDefinitionInstance") != null);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ContractDefinitionSupportDoesNotExportAnOpaqueDependency(bool refresh) {
    const string source = "opaque ghost function G(n:nat):int { n } " +
      "ghost function F(n:nat):int { G(n) } lemma L(n:nat) ensures F(n)==n {}";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    Assert.DoesNotContain(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>(),
      c => c.Description?.ShortDescription == "guarded definition agreement");
    Assert.DoesNotContain(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssumeCmd>(),
      c => Bpl.QKeyValue.FindStringAttribute(c.Attributes, "contractDefinitionInstance") != null);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task NativeDefinitionInstancesKeepGuardsChecksAndContinuationFacts(bool refresh, bool quantified) {
    var condition = quantified ? "forall x:int {:trigger F(x)} :: F(x)==x" : "F(n)==n";
    var source = "ghost function G(x:int):int { x } ghost function F(x:int):int { G(x) } " +
      $"lemma L(n:int) ensures {condition} {{}}";
    var programs = await Translate(source, true, refresh);
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    foreach (var backend in programs) {
      Assert.Equal(0, backend.Resolve(options));
      Assert.Equal(0, backend.Typecheck(options));
    }
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var instances = commands.OfType<Bpl.AssumeCmd>().Where(c =>
      Bpl.QKeyValue.FindStringAttribute(c.Attributes, "contractDefinitionInstance") != null).ToList();
    Assert.NotEmpty(instances);
    // The whole axiom supplies both the dependency's permission and the
    // definition equality, still under the native can-call premise.
    Assert.Contains(instances, instance => instance.Expr.ToString().Contains("F#canCall") &&
      instance.Expr.ToString().Contains("G#canCall") && instance.Expr.ToString().Contains("=="));
    var support = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "guarded definition agreement"));
    Assert.True(instances.All(instance => commands.IndexOf(instance) < commands.IndexOf(support)));
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), fact =>
      ObligationFingerprint.Expression(fact.Expr) == ObligationFingerprint.Expression(support.Expr));
    Assert.Contains(commands.OfType<Bpl.AssertCmd>(), c => c.Description is EnsuresDescription);
    if (quantified) {
      Assert.Contains(instances, instance => instance.Expr is Bpl.ForallExpr);
    }
  }

  [Fact]
  public void NativeDefinitionInstanceAuditRejectsConflictingBindingsAndRetainsCompleteBody() {
    var token = Token.NoToken;
    var outer = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int));
    var shadow = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int));
    var escaped = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "y", Bpl.Type.Int));
    var globals = new Dictionary<string, Bpl.Variable>();
    var guard = Bpl.Expr.Gt(new Bpl.IdentifierExpr(token, outer), Bpl.Expr.Literal(0));
    var body = Bpl.Expr.Imp(guard, Bpl.Expr.Eq(new Bpl.IdentifierExpr(token, outer), Bpl.Expr.Literal(1)));
    var axiom = new Bpl.ForallExpr(token, new List<Bpl.Variable> { outer }, null, body);
    var bindings = new Dictionary<Bpl.Variable, Bpl.Expr> { [outer] = Bpl.Expr.Literal(3) };
    Assert.True(BoogieGenerator.TryInstantiateContractDefinition(axiom, bindings, [], globals, out var instance));
    Assert.Equal(Bpl.Expr.Imp(Bpl.Expr.Gt(Bpl.Expr.Literal(3), Bpl.Expr.Literal(0)),
      Bpl.Expr.Eq(Bpl.Expr.Literal(3), Bpl.Expr.Literal(1))).ToString(), instance.ToString());
    var invalid = new Bpl.ForallExpr(token, new List<Bpl.Variable> { outer }, null,
      Bpl.Expr.Eq(new Bpl.IdentifierExpr(token, shadow), Bpl.Expr.Literal(1)));
    Assert.False(BoogieGenerator.TryInstantiateContractDefinition(invalid, bindings, [], globals, out _));
    bindings[outer] = new Bpl.IdentifierExpr(token, escaped);
    Assert.False(BoogieGenerator.TryInstantiateContractDefinition(axiom, bindings, [], globals, out _));
    bindings[outer] = Bpl.Expr.True;
    Assert.False(BoogieGenerator.TryInstantiateContractDefinition(axiom, bindings, [], globals, out _));
    bindings.Clear();
    Assert.False(BoogieGenerator.TryInstantiateContractDefinition(axiom, bindings, [], globals, out _));
  }

  [Fact]
  public void NativeDefinitionInstanceSubstitutionPreservesShadowingAndLetRightHandSides() {
    var token = Token.NoToken;
    var outer = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int));
    var shadow = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int));
    var nested = new Bpl.ForallExpr(token, new List<Bpl.Variable> { shadow }, null,
      Bpl.Expr.Eq(new Bpl.IdentifierExpr(token, shadow), new Bpl.IdentifierExpr(token, shadow)));
    var let = new Bpl.LetExpr(token, new List<Bpl.Variable> { shadow },
      new List<Bpl.Expr> { new Bpl.IdentifierExpr(token, outer) }, null,
      Bpl.Expr.Eq(new Bpl.IdentifierExpr(token, shadow), Bpl.Expr.Literal(3)));
    var axiom = new Bpl.ForallExpr(token, new List<Bpl.Variable> { outer }, null, Bpl.Expr.And(nested, let));
    var bindings = new Dictionary<Bpl.Variable, Bpl.Expr> { [outer] = Bpl.Expr.Literal(3) };
    Assert.True(BoogieGenerator.TryInstantiateContractDefinition(axiom, bindings, [],
      new Dictionary<string, Bpl.Variable>(), out var instance));
    var conjunction = Assert.IsType<Bpl.NAryExpr>(instance);
    var copiedQuantifier = Assert.IsType<Bpl.ForallExpr>(conjunction.Args[0]);
    var copiedLet = Assert.IsType<Bpl.LetExpr>(conjunction.Args[1]);
    Assert.NotSame(shadow, copiedQuantifier.Dummies[0]);
    Assert.NotSame(shadow, copiedLet.Dummies[0]);
    Assert.Equal("3", copiedLet.Rhss[0].ToString());
    var equality = Assert.IsType<Bpl.NAryExpr>(copiedLet.Body);
    Assert.Same(copiedLet.Dummies[0], Assert.IsType<Bpl.IdentifierExpr>(equality.Args[0]).Decl);
  }

  [Fact]
  public void NativeDefinitionInstancesAvoidCapturingActualNamesUnderNestedBinders() {
    var token = Token.NoToken;
    var outer = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "a", Bpl.Type.Int));
    var nested = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int));
    var actual = new Bpl.Formal(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int), true);
    var body = new Bpl.ForallExpr(token, new List<Bpl.Variable> { nested }, null,
      Bpl.Expr.Eq(new Bpl.IdentifierExpr(token, outer), new Bpl.IdentifierExpr(token, nested)));
    var axiom = new Bpl.ForallExpr(token, new List<Bpl.Variable> { outer }, null, body);
    Assert.True(BoogieGenerator.TryInstantiateContractDefinition(axiom,
      new Dictionary<Bpl.Variable, Bpl.Expr> { [outer] = new Bpl.IdentifierExpr(token, actual) }, [],
      new Dictionary<string, Bpl.Variable>(), out var instance));
    var closed = Assert.IsType<Bpl.ForallExpr>(instance);
    Assert.NotEqual(actual.Name, closed.Dummies[0].Name);
    var equality = Assert.IsType<Bpl.NAryExpr>(closed.Body);
    Assert.Same(actual, Assert.IsType<Bpl.IdentifierExpr>(equality.Args[0]).Decl);
    Assert.Same(closed.Dummies[0], Assert.IsType<Bpl.IdentifierExpr>(equality.Args[1]).Decl);
  }

  [Fact]
  public void GuardedNativeConsequenceCertificateKeepsEveryImplicationPremise() {
    var token = Token.NoToken;
    var a = new Bpl.Formal(token, new Bpl.TypedIdent(token, "a", Bpl.Type.Bool), true);
    var b = new Bpl.Formal(token, new Bpl.TypedIdent(token, "b", Bpl.Type.Bool), true);
    var x = new Bpl.Formal(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int), true);
    var pa = new Bpl.IdentifierExpr(token, a);
    var pb = new Bpl.IdentifierExpr(token, b);
    var equality = Bpl.Expr.Eq(new Bpl.IdentifierExpr(token, x), Bpl.Expr.Literal(1));
    var body = Bpl.Expr.Imp(pa, Bpl.Expr.And(Bpl.Expr.True, Bpl.Expr.Imp(pb, equality)));
    Assert.True(BoogieGenerator.ContractConsequenceHasNativePath(body,
      Bpl.Expr.Imp(Bpl.Expr.And(pa, pb), equality)));
    Assert.False(BoogieGenerator.ContractConsequenceHasNativePath(body, Bpl.Expr.Imp(pb, equality)));
    Assert.False(BoogieGenerator.ContractConsequenceHasNativePath(body, equality));
    var detached = Bpl.Expr.Eq(new Bpl.IdentifierExpr(token, x), Bpl.Expr.Literal(1));
    Assert.False(BoogieGenerator.ContractConsequenceHasNativePath(body,
      Bpl.Expr.Imp(Bpl.Expr.And(pa, pb), detached)));
  }

  [Fact]
  public void GuardedNativeInstancesRejectCapturedUnresolvedActuals() {
    var token = Token.NoToken;
    var nested = new Bpl.BoundVariable(token, new Bpl.TypedIdent(token, "x", Bpl.Type.Int));
    var unresolved = new Bpl.IdentifierExpr(token, "x", Bpl.Type.Int);
    var captured = new Bpl.ForallExpr(token, new List<Bpl.Variable> { nested }, null,
      Bpl.Expr.Eq(unresolved, new Bpl.IdentifierExpr(token, nested)));
    var globals = new Dictionary<string, Bpl.Variable>();
    Assert.False(BoogieGenerator.ContractGuardedInstanceIsScoped(captured, new[] { unresolved }, globals));
    var escaped = new Bpl.IdentifierExpr(token, nested);
    Assert.False(BoogieGenerator.ContractGuardedInstanceIsScoped(Bpl.Expr.Eq(escaped, escaped), [], globals));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task GuardedNativeInstancesRetainCompleteInstancesAndSupportChecks(bool refresh) {
    const string source = "ghost function G(n:int):int { n } ghost function F(n:int):int { G(n) } " +
      "lemma L(n:int) ensures F(n)==n {}";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var guarded = commands.OfType<Bpl.AssumeCmd>().Where(c =>
      Bpl.QKeyValue.FindStringAttribute(c.Attributes, "contractGuardedDefinitionInstance") != null).ToList();
    Assert.NotEmpty(guarded);
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), c =>
      Bpl.QKeyValue.FindStringAttribute(c.Attributes, "contractDefinitionInstance") != null);
    var check = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "guarded definition agreement"));
    Assert.All(guarded, instance => Assert.True(commands.IndexOf(instance) < commands.IndexOf(check)));
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), fact =>
      ObligationFingerprint.Expression(fact.Expr) == ObligationFingerprint.Expression(check.Expr));
    Assert.Contains(commands.OfType<Bpl.AssertCmd>(), c => c.Description is EnsuresDescription);
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    foreach (var backend in programs) {
      Assert.Equal(0, backend.Resolve(options));
      Assert.Equal(0, backend.Typecheck(options));
    }
  }

  private sealed class ScopedFuelTestReferences : Bpl.Duplicator {
    public readonly HashSet<Bpl.Variable> Variables = new(ReferenceEqualityComparer.Instance);
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) {
      if (node.Decl != null) { Variables.Add(node.Decl); }
      if (node.Decl is Bpl.BoundVariable) {
        // Both names participate in Boogie emission. Checking only Decl identity
        // would miss a fresh binder whose printed uses retain the source name.
        Assert.Equal(node.Decl.TypedIdent.Name, node.Decl.Name);
        Assert.Equal(node.Decl.Name, node.Name);
      }
      return base.VisitIdentifierExpr(node);
    }
  }

  [Theory]
  [InlineData(false, false, false, false, false)]
  [InlineData(false, false, false, false, true)]
  [InlineData(false, false, false, true, false)]
  [InlineData(false, false, false, true, true)]
  [InlineData(false, false, true, false, false)]
  [InlineData(false, false, true, false, true)]
  [InlineData(false, false, true, true, false)]
  [InlineData(false, false, true, true, true)]
  [InlineData(false, true, false, false, false)]
  [InlineData(false, true, false, false, true)]
  [InlineData(false, true, false, true, false)]
  [InlineData(false, true, false, true, true)]
  [InlineData(false, true, true, false, false)]
  [InlineData(false, true, true, false, true)]
  [InlineData(false, true, true, true, false)]
  [InlineData(false, true, true, true, true)]
  [InlineData(true, false, false, false, false)]
  [InlineData(true, false, false, false, true)]
  [InlineData(true, false, false, true, false)]
  [InlineData(true, false, false, true, true)]
  [InlineData(true, false, true, false, false)]
  [InlineData(true, false, true, false, true)]
  [InlineData(true, false, true, true, false)]
  [InlineData(true, false, true, true, true)]
  [InlineData(true, true, false, false, false)]
  [InlineData(true, true, false, false, true)]
  [InlineData(true, true, false, true, false)]
  [InlineData(true, true, false, true, true)]
  [InlineData(true, true, true, false, false)]
  [InlineData(true, true, true, false, true)]
  [InlineData(true, true, true, true, false)]
  [InlineData(true, true, true, true, true)]
  public async Task ContractScopedFuelSupportClosesBothQuantifierPolaritiesAndLets(
    bool refresh, bool call, bool existential, bool let, bool generic) {
    var parameters = generic ? "<T>" : "";
    var body = let ? $"(var m:=n; F{parameters}(m)==m)" : $"F{parameters}(n)==n";
    var condition = (existential ? "exists" : "forall") + " n:nat :: " + body;
    var source = $"ghost function F{parameters}(n:nat):int {{ if n==0 then 0 else F{parameters}(n-1)+1 }} " +
      (call ? $"lemma Use{parameters}() requires {condition} {{}} lemma L{parameters}() {{ Use{parameters}(); }}"
            : $"lemma L{parameters}() ensures {condition} {{}}");
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).ToList();
    var agreement = Assert.Single(commands.OfType<Bpl.AssertCmd>().Where(c =>
      c.Description?.ShortDescription == "scoped fuel layer agreement"));
    Assert.Equal(-1, Bpl.QKeyValue.FindIntAttribute(agreement.Attributes, "subsumption", -1));
    void Check(Bpl.Expr expression) {
      if (expression is Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And } } conjunction) {
        Assert.All(conjunction.Args, Check);
        return;
      }
      var closure = Assert.IsType<Bpl.ForallExpr>(expression);
      Assert.Single(closure.Dummies);
      Assert.StartsWith("$contractFuel#", closure.Dummies[0].TypedIdent.Name);
      var referenced = new ScopedFuelTestReferences();
      referenced.VisitExpr(closure.Body);
      Assert.All(referenced.Variables.OfType<Bpl.BoundVariable>(), variable => Assert.Contains(variable, closure.Dummies));
      var equality = Assert.IsType<Bpl.NAryExpr>(closure.Body);
      Assert.Equal(Bpl.BinaryOperator.Opcode.Eq, Assert.IsType<Bpl.BinaryOperator>(equality.Fun).Op);
      var upper = Assert.IsType<Bpl.NAryExpr>(equality.Args[0]);
      var lower = Assert.IsType<Bpl.NAryExpr>(equality.Args[1]);
      Assert.EndsWith(".F", Assert.IsType<Bpl.FunctionCall>(upper.Fun).FunctionName);
      var position = generic ? 1 : 0;
      var successor = Assert.IsType<Bpl.NAryExpr>(upper.Args[position]);
      Assert.Equal("$LS", Assert.IsType<Bpl.FunctionCall>(successor.Fun).FunctionName);
      Assert.Equal(ObligationFingerprint.Expression(successor.Args[0]), ObligationFingerprint.Expression(lower.Args[position]));
      for (var i = 0; i < upper.Args.Count; i++) {
        if (i != position) {
          Assert.Equal(ObligationFingerprint.Expression(upper.Args[i]), ObligationFingerprint.Expression(lower.Args[i]));
        }
      }
      Assert.Same(closure.Dummies[0], Assert.IsType<Bpl.IdentifierExpr>(upper.Args.Last()).Decl);
      Assert.Equal(ObligationFingerprint.Expression(upper), ObligationFingerprint.Expression(Assert.Single(closure.Triggers.Tr)));
    }
    Check(agreement.Expr);
    Assert.Contains(commands.OfType<Bpl.AssumeCmd>(), c =>
      ObligationFingerprint.Expression(c.Expr) == ObligationFingerprint.Expression(agreement.Expr));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ContractScopedFuelSupportRetainsNestedBindings(bool refresh) {
    const string source = "ghost function G(n:nat,m:nat):int { if n==0 then m else G(n-1,m)+1 } " +
      "lemma L() ensures forall n:nat :: exists m:nat :: G(n,m)==n+m {}";
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p =>
      p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
    var agreement = Assert.Single(implementation.Blocks.SelectMany(b => b.Cmds).OfType<Bpl.AssertCmd>()
      .Where(c => c.Description?.ShortDescription == "scoped fuel layer agreement"));
    void Check(Bpl.Expr expression) {
      if (expression is Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.And } } conjunction) {
        Assert.All(conjunction.Args, Check);
        return;
      }
      var closure = Assert.IsType<Bpl.ForallExpr>(expression);
      Assert.Equal(2, closure.Dummies.Count);
      Assert.Equal(2, closure.Dummies.Select(v => v.TypedIdent.Name).Distinct().Count());
      var referenced = new ScopedFuelTestReferences();
      referenced.VisitExpr(closure.Body);
      Assert.All(referenced.Variables.OfType<Bpl.BoundVariable>(), variable => Assert.Contains(variable, closure.Dummies));
      var equality = Assert.IsType<Bpl.NAryExpr>(closure.Body);
      var application = Assert.IsType<Bpl.NAryExpr>(equality.Args[0]);
      Assert.Same(closure.Dummies[0], Assert.IsType<Bpl.IdentifierExpr>(application.Args[^2]).Decl);
      Assert.Same(closure.Dummies[1], Assert.IsType<Bpl.IdentifierExpr>(application.Args[^1]).Decl);
    }
    Check(agreement.Expr);
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
      ? (old ? "twostate " : "") + $"lemma Use(c:C) requires {condition} {{}} lemma L(c:C) requires 0<=c.i<=100 {{ Use(c); }}"
      : $"lemma L(c:C) requires 0<=c.i<=100 ensures {condition} {{}}");
    var programs = await Translate(source, true, refresh);
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
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
    var implementation = programs.SelectMany(p => p.Implementations).Single(p => p.Name.StartsWith("Impl$$") && p.Name.EndsWith(".L"));
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
