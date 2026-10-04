// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[CollectionDefinition("B3 translation", DisableParallelization = true)]
public class TranslationCollection { }

[Collection("B3 translation")]
public class NormalizerTests {
  [Fact]
  public async Task RealDafnyChecksSurviveThePreludeAbstraction() {
    var results = await Dafny("method Valid() { assert true; } method Invalid() { assert false; }");
    Assert.NotEmpty(results);
    Assert.All(results, Validate);
    var checks = results.SelectMany(r => Statements(r.Program!.Unit.Body)).OfType<Ir.Check>().ToArray();
    Assert.Contains(checks, c => c.Condition is Ir.BooleanLiteral { Value: false });
    Assert.Contains(checks, c => c.Condition is Ir.BooleanLiteral { Value: true });
    Assert.All(results, r => Assert.Empty(r.Program!.Axioms));
    Assert.All(results.SelectMany(r => r.Obligations), o => Assert.Contains("B3NormalizerTests.dfy", o.Uri));
  }

  [Fact]
  public async Task RealIntegerContractsAndCallsNormalize() {
    var results = await Dafny("""
      method Inc(x: int) returns (y: int) ensures y == x + 1 { y := x + 1; }
      method Positive(x: int) requires x > 0 {}
      method BadCall() { Positive(0); }
      method Branch(b: bool) returns (x: int) ensures x >= 0 { if b { x := 1; } else { x := 0; } }
      """);
    Assert.All(results, Validate);
    Assert.Contains(results.SelectMany(r => Statements(r.Program!.Unit.Body)), s => s is Ir.Conditional);
    Assert.Contains(results.SelectMany(r => Statements(r.Program!.Unit.Body)).OfType<Ir.Check>(),
      c => c.Condition is Ir.Operation { Operator: Ir.Operator.Equal });
    Assert.Contains(results.SelectMany(r => r.Obligations), o => o.Description.Contains("precondition"));
  }

  [Fact]
  public async Task ActualDafnyVisibilityFailsClosed() {
    var visibility = await Dafny("function F(x: int): int { x + 1 } lemma V(x: int) { hide F; reveal F; assert F(x) == x + 1; }", false);
    Assert.Contains(visibility, r => !r.Success && r.Diagnostics.Any(d => d.Code == "b3_visibility"));
    Assert.All(visibility.Where(r => !r.Success), r => { Assert.Null(r.Program); Assert.Empty(r.Obligations); });
  }

  [Fact]
  public void HavocChoosesEveryValueBeforeWhereConstraints() {
    var result = Boogie("""
      var g: int where g == h;
      var h: int;
      procedure P(); modifies g, h;
      implementation P() { havoc g, h; assert g == h; }
      """);
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    var havoc = Array.FindIndex(statements, s => s is Ir.Havoc);
    Assert.Equal(2, ((Ir.Havoc)statements[havoc]).Variables.Count);
    Assert.IsType<Ir.Assume>(statements[havoc + 1]);
    Assert.IsType<Ir.Check>(statements[havoc + 2]);
  }

  [Fact]
  public void SubsumptionAndFreeExitOrderAreExplicit() {
    var result = Boogie("""
      procedure P();
        free ensures {:always_assume} false;
        ensures false;
      implementation P() { assert {:subsumption 0} true; }
      """);
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    Assert.False(statements.OfType<Ir.Check>().First().Learn);
    var assume = Array.FindIndex(statements, s => s is Ir.Assume { Condition: Ir.BooleanLiteral { Value: false } });
    Assert.True(assume >= 0);
    Assert.IsType<Ir.Check>(statements[assume + 1]);
    Assert.Equal(2, result.Obligations.Count);
  }

  [Theory]
  [InlineData("call", 1)]
  [InlineData("free call", 0)]
  public void FreeCallsSkipCheckedRequires(string command, int checks) {
    var result = Boogie($"procedure Q(); requires false; procedure P(); implementation P() {{ {command} Q(); }}");
    Validate(result);
    Assert.Equal(checks, result.Obligations.Count);
  }

  [Fact]
  public void CallsSaveTheirOwnOldGlobalState() {
    var result = Boogie("""
      var g: int;
      procedure Q(); modifies g; ensures g == old(g) + 1;
      procedure P(); modifies g;
      implementation P() { g := 4; call Q(); assert g == 5; }
      """);
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    var havoc = Array.FindIndex(statements, s => s is Ir.Havoc);
    var save = Assert.IsType<Ir.Assign>(statements[havoc - 1]);
    var post = Assert.IsType<Ir.Assume>(statements[havoc + 1]);
    var equality = Assert.IsType<Ir.Operation>(post.Condition);
    var addition = Assert.IsType<Ir.Operation>(equality.Arguments[1]);
    Assert.Equal(save.Variable, Assert.IsType<Ir.Variable>(addition.Arguments[0]).Name);
  }

  [Fact]
  public void MapsHaveOpaqueSortsAndNoExtensionalityAxioms() {
    var result = Boogie("procedure P(m: [int]int); implementation P(m: [int]int) { assert m == m; }");
    Validate(result);
    Assert.Single(result.Program!.Types);
    Assert.Empty(result.Program.Axioms);
    Assert.DoesNotContain(result.Program.Types, t => t.Contains("Array"));
  }

  [Theory]
  [InlineData("procedure P(x: real); implementation P(x: real) { assert x == x; }", "b3_primitive_type")]
  [InlineData("procedure P(x: int); implementation P(x: int) { assert x div 2 == x; }", "b3_arithmetic")]
  [InlineData("procedure P(); implementation P() { again: assert true; goto again; }", "b3_transfer")]
  [InlineData("procedure P(); implementation P() { goto a, b; a: return; b: return; }", "b3_transfer")]
  [InlineData("procedure P(); implementation P() { goto {:unreviewed} done; done: assert true; }", "b3_attribute")]
  [InlineData("procedure P(); implementation P() { return {:unreviewed}; }", "b3_attribute")]
  public void UnsupportedFeaturesNeverProduceAPartialProgram(string source, string diagnostic) {
    var result = Boogie(source);
    Assert.False(result.Success);
    Assert.Null(result.Program);
    Assert.Empty(result.Obligations);
    Assert.Contains(result.Diagnostics, d => d.Code == diagnostic);
  }

  [Fact]
  public void NormalizationDoesNotMutateTheTypedSource() {
    var options = Options();
    var source = ParseBoogie("var g: int; procedure P(); modifies g; implementation P() { call Q(); } procedure Q(); modifies g; ensures g == old(g);", options);
    var before = Emit(source, options);
    var first = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
    var second = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
    Validate(first); Validate(second);
    Assert.Equal(before, Emit(source, options));
    Assert.Equal(Ir.Protocol.GetProgramHash(first.Program!), Ir.Protocol.GetProgramHash(second.Program!));
  }

  [Fact]
  public async Task RealIntegerLiteralIdentitiesUseTheirDefiningEquality() {
    var results = await Dafny("method Arithmetic() { assert 1 + 1 == 2; }");
    Assert.All(results, Validate);
    Assert.Contains(results.SelectMany(r => Statements(r.Program!.Unit.Body)).OfType<Ir.Check>(),
      c => c.Condition is Ir.Operation { Operator: Ir.Operator.Equal, Arguments: var args } &&
        args[0] is Ir.Operation { Operator: Ir.Operator.Add, Arguments: var summands } &&
        summands.All(a => a is Ir.IntegerLiteral) && args[1] is Ir.IntegerLiteral);
    var claimed = Boogie("function {:identity} Pretend(x: int): int { x + 1 } procedure P(); implementation P() { assert Pretend(0) == 0; }");
    Validate(claimed);
    var equality = Assert.IsType<Ir.Operation>(Statements(claimed.Program!.Unit.Body).OfType<Ir.Check>().Single().Condition);
    Assert.IsType<Ir.Application>(equality.Arguments[0]);
  }

  [Theory]
  [InlineData("smt.arith.solver", "6")]
  [InlineData("smt.arith.nl", "false")]
  public void PerUnitSolverOverridesFailClosed(string name, string value) {
    var result = Boogie($"procedure P(); implementation {{:smt_option \"{name}\", \"{value}\"}} P() {{ assert true; }}");
    Assert.False(result.Success); Assert.Null(result.Program);
    Assert.Contains(result.Diagnostics, d => d.Code == "b3_solver_attribute");
  }

  [Fact]
  public void RequiresAndProcedureOutputWhereRejectOldDuringResolution() {
    var options = Options();
    Assert.Equal(0, Bpl.Parser.Parse("var g: int; procedure Q() returns (x: int where x == old(g)); requires g == old(g);", "B3NormalizerTests.bpl", out var source));
    Assert.Equal(2, source.Resolve(options));
  }

  [Fact]
  public void LocalHavocWhereUsesUpdatedGlobalsAndAssertionsRetainEnclosingOld() {
    var result = Boogie("""
      var g: int;
      procedure Q(); modifies g; ensures g == old(g);
      procedure P(); modifies g;
      implementation P() {
        var x: int where x == g;
        g := g + 1;
        call Q();
        havoc x;
        assert x == old(g);
      }
      """);
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    var entrySave = Assert.IsType<Ir.Assign>(statements[0]);
    var firstHavoc = Array.FindIndex(statements, s => s is Ir.Havoc);
    var callEnsures = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Assume>(statements[firstHavoc + 1]).Condition);
    Assert.NotEqual(entrySave.Variable, Assert.IsType<Ir.Variable>(callEnsures.Arguments[1]).Name);
    var lastHavoc = Array.FindLastIndex(statements, s => s is Ir.Havoc);
    var where = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Assume>(statements[lastHavoc + 1]).Condition);
    Assert.Equal(Assert.IsType<Ir.Variable>(callEnsures.Arguments[0]).Name, Assert.IsType<Ir.Variable>(where.Arguments[1]).Name);
    var assertion = statements.OfType<Ir.Check>().Single();
    Assert.Equal(entrySave.Variable, Assert.IsType<Ir.Variable>(Assert.IsType<Ir.Operation>(assertion.Condition).Arguments[1]).Name);
  }

  [Fact]
  public void NondeterministicChoiceKeepsBothBranchesAndTheirChecks() {
    var result = Boogie("procedure P(); implementation P() { if (*) { assert false; } else { assert true; } }");
    Validate(result);
    var choice = Statements(result.Program!.Unit.Body).OfType<Ir.Choice>().Single();
    Assert.Equal(2, choice.Branches.Count);
    Assert.Equal(2, result.Obligations.Count);
  }

  [Fact]
  public void LambdaClosuresCaptureCurrentAndOldGlobalsSeparately() {
    var result = Boogie("""
      var g: int;
      procedure P(); modifies g;
      implementation P() {
        var m: [int]bool;
        g := g + 1;
        m := (lambda x: int :: x == g + old(g));
        assert m == m;
      }
      """);
    Validate(result);
    var assignments = Statements(result.Program!.Unit.Body).OfType<Ir.Assign>().ToArray();
    var lambda = assignments.Select(a => a.Value).OfType<Ir.Application>().Single();
    Assert.Equal(2, lambda.Arguments.Count);
    Assert.NotEqual(Assert.IsType<Ir.Variable>(lambda.Arguments[0]).Name,
      Assert.IsType<Ir.Variable>(lambda.Arguments[1]).Name);
    Assert.Contains(lambda.Arguments.OfType<Ir.Variable>(), v => v.Name == assignments[0].Variable);
  }

  [Fact]
  public void TemporaryOutputWhereIsOmittedAtEntryAndPreservedAfterHavoc() {
    var result = Boogie("""
      procedure Q(a: int) returns (x: int where x == a); ensures x == a;
      procedure P() returns (y: int);
      implementation P() returns (y: int) { call y := Q(1); }
      """);
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    Assert.Equal(2, Assert.IsType<Ir.Havoc>(statements[0]).Variables.Count);
    var inputSave = Assert.IsType<Ir.Assign>(statements[1]);
    Assert.IsType<Ir.IntegerLiteral>(inputSave.Value);
    Assert.Single(Assert.IsType<Ir.Havoc>(statements[2]).Variables);
    var freshWhere = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Assume>(statements[3]).Condition);
    Assert.Equal(inputSave.Variable, Assert.IsType<Ir.Variable>(freshWhere.Arguments[1]).Name);
    Assert.Equal(2, statements.OfType<Ir.Assume>().Count()); // post-havoc where and callee ensures
    Assert.Contains(result.Approximations, a => a.Contains("scope-entry where predicates are omitted"));
  }

  [Fact]
  public void MutableGlobalOutputWhereCannotHideAFalseCallPrecondition() {
    // The pinned StateCmd entry appends raw g == 0; evaluating it as current g == 0 here is unsound.
    var result = Boogie("""
      var g: int;
      procedure Q() returns (x: int where g == 0); requires false;
      procedure P(); requires g == 0; modifies g;
      implementation P() { var x: int; g := 1; call x := Q(); }
      """);
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    var callEntry = Array.FindIndex(statements, s => s is Ir.Havoc);
    var precondition = Array.FindIndex(statements, s => s is Ir.Check);
    Assert.True(callEntry >= 0 && precondition == callEntry + 1);
    Assert.Equal(new Ir.BooleanLiteral(false), Assert.IsType<Ir.Check>(statements[precondition]).Condition);
    Assert.IsType<Ir.Havoc>(statements[precondition + 1]);
    Assert.IsType<Ir.Assume>(statements[precondition + 2]); // current g == 0 belongs only after havoc
    Assert.Single(result.Obligations);
  }

  [Fact]
  public void MutableGlobalStateWhereCannotHideAFalseScopedAssertion() {
    // StateCmd has no surface syntax. Reuse a resolved/typechecked where expression from a local template.
    var options = Options();
    var source = ParseBoogie("""
      var g: int;
      procedure P(); requires g == 0; modifies g;
      implementation P() { var template: int where g == 0; g := 1; }
      """, options);
    var implementation = source.Implementations.Single();
    var template = implementation.LocVars.Single();
    var token = template.tok;
    var scoped = new Bpl.LocalVariable(token, new Bpl.TypedIdent(token, "scoped", Bpl.Type.Int,
      template.TypedIdent.WhereExpr));
    var assertion = new Bpl.AssertCmd(token, Bpl.Expr.False);
    var state = new Bpl.StateCmd(token, new List<Bpl.Variable> { scoped }, new List<Bpl.Cmd> { assertion });
    // The pinned structured-to-CFG conversion shares this command list. Insertion updates both views.
    implementation.StructuredStmts!.BigBlocks[0].simpleCmds.Add(state);
    Assert.Contains(state, implementation.Blocks[0].Cmds);
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    var before = Emit(source, options);
    var result = B3Normalizer.Normalize(source, implementation, options);
    Validate(result);
    Assert.Equal(before, Emit(source, options));
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    var entry = Array.FindIndex(statements, s => s is Ir.Havoc);
    Assert.True(entry >= 0);
    Assert.Equal(new Ir.BooleanLiteral(false), Assert.IsType<Ir.Check>(statements[entry + 1]).Condition);
    Assert.Single(result.Obligations);
  }

  [Fact]
  public async Task ActualDafnyDefaultDivisionFailsClosedThroughItsDefiningFunction() {
    var results = await Dafny("method Division(x: int) { assert x / 2 == x; }", false);
    Assert.Contains(results, r => !r.Success && r.Diagnostics.Any(d => d.Code == "b3_arithmetic"));
    Assert.All(results.Where(r => !r.Success), r => { Assert.Null(r.Program); Assert.Empty(r.Obligations); });
  }

  [Fact]
  public async Task RealDafnyGoodAndBadLoopChecksReachNormalization() {
    var results = await Dafny("""
      method Good(n: nat) {
        var i := 0;
        while i < n invariant 0 <= i <= n { i := i + 1; }
        assert i == n;
      }
      method BadInitialization() {
        var i := 0;
        while i < 2 invariant false { i := i + 1; }
      }
      method BadPreservation(n: nat) {
        var i := 0;
        while i < n invariant 0 <= i <= n { i := i + 2; }
      }
      """);
    Assert.All(results, Validate);
    var loops = results.SelectMany(r => Statements(r.Program!.Unit.Body)).OfType<Ir.Loop>().ToArray();
    Assert.Equal(3, loops.Length); Assert.All(loops, loop => Assert.Empty(loop.Invariants));
    var manifest = results.SelectMany(r => r.Obligations).ToArray();
    Assert.Contains(manifest, o => o.Id.StartsWith("sOinit") && o.Description == "loop invariant initialization");
    Assert.Contains(manifest, o => o.Id.StartsWith("sOmaint") && o.Description == "loop invariant preservation");
    Assert.Contains(results.SelectMany(r => Statements(r.Program!.Unit.Body)).OfType<Ir.Check>(),
      c => c.ObligationId.StartsWith("sOinit") && c.Condition is Ir.Operation { Operator: Ir.Operator.Implies });
  }

  [Fact]
  public async Task RealDafnyReturnNestedBreakAndContinueUseLexicalExits() {
    var results = await Dafny("""
      method Paths(n: nat) returns (r: int) ensures r >= 0 {
        r := 0;
        label Outer:
        while r < n invariant 0 <= r <= n {
          var j := 0;
          while j < n invariant 0 <= j <= n {
            if j == 1 { break Outer; }
            j := j + 1;
            if j == 2 { continue; }
          }
          r := r + 1;
          if r == 2 { return; }
        }
      }
      """);
    Assert.All(results, Validate);
    var statements = results.SelectMany(r => Statements(r.Program!.Unit.Body)).ToArray();
    Assert.Equal(2, statements.OfType<Ir.Loop>().Count());
    Assert.True(statements.OfType<Ir.Exit>().Count() >= 4);
    Assert.Contains(results.SelectMany(r => r.Obligations), o => o.Description.Contains("postcondition"));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void FreeInvariantsHaveThePinnedCheckingAndHeaderSchedule(bool alwaysAssume) {
    var options = Options(); options.AlwaysAssumeFreeLoopInvariants = alwaysAssume;
    var result = Boogie("""
      procedure P(); implementation P() {
        var i: int;
        i := 0;
        while (i < 2)
          free invariant false;
          invariant {:subsumption 0} i >= 0;
        { i := i + 1; }
      }
      """, options);
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    var falseAssumptions = statements.OfType<Ir.Assume>().Count(a => a.Condition is Ir.BooleanLiteral { Value: false });
    // One header free predicate and one cut edge; optional init/maintenance copies add two.
    Assert.Equal(alwaysAssume ? 4 : 2, falseAssumptions);
    Assert.Equal(2, statements.OfType<Ir.Check>().Count());
    Assert.All(statements.OfType<Ir.Check>(), c => Assert.False(c.Learn));
    var initialization = Array.FindIndex(statements, s => s is Ir.Check c && c.ObligationId.StartsWith("sOinit"));
    var loopIndex = Array.FindIndex(statements, s => s is Ir.Loop);
    Assert.True(initialization < loopIndex);
    var preservation = Array.FindIndex(statements, s => s is Ir.Check c && c.ObligationId.StartsWith("sOmaint"));
    Assert.True(preservation > loopIndex);
    Assert.IsType<Ir.Assume>(statements[preservation + 1]);
  }

  [Fact]
  public void CheckedFalseInvariantIsCheckedBeforeItIsAssumed() {
    var result = Boogie("procedure P(); implementation P() { while (*) invariant false; { } }");
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    var first = statements.First(s => s is Ir.Check or Ir.Assume);
    Assert.IsType<Ir.Check>(first);
    Assert.Equal(new Ir.BooleanLiteral(false), ((Ir.Check)first).Condition);
    Assert.Equal(2, result.Obligations.Count);
  }

  [Theory]
  [InlineData("while (*) invariant true; { break; } assert false;")]
  [InlineData("OUT: while (*) invariant true; { while (*) invariant true; { break OUT; } }")]
  [InlineData("OUT: if (*) { break OUT; } assert false;")]
  public void StructuredBreaksNameTheirResolvedLexicalEnclosures(string body) {
    var result = Boogie("procedure P(); implementation P() { " + body + " }");
    Validate(result);
    Assert.Contains(Statements(result.Program!.Unit.Body), s => s is Ir.Exit);
  }

  [Fact]
  public void ForwardGotoSkipsChecksButKeepsThemInTheStaticManifest() {
    var result = Boogie("procedure P(); implementation P() { goto done; assert false; done: assert true; }");
    Validate(result);
    var labels = Statements(result.Program!.Unit.Body).OfType<Ir.Labeled>().ToArray();
    Assert.Single(labels);
    Assert.Contains(Statements(labels[0].Body), s => s is Ir.Exit e && e.Label == labels[0].Name);
    Assert.Equal(2, result.Obligations.Count);
  }

  [Theory]
  [InlineData("while (*) invariant true; { goto outer; } outer: assert true;")]
  [InlineData("while (*) invariant true; { goto again; again: assert true; }")]
  public void ForwardGotoCanExitTheLoopOrContinueItsBody(string body) {
    var result = Boogie("procedure P(); implementation P() { " + body + " }");
    Validate(result);
    Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Loop>());
  }

  [Fact]
  public void BreakOnlyTargetsDoNotReceiveHeaderWhereAssumptions() {
    var result = Boogie("""
      var g: int;
      procedure P(); modifies g;
      implementation P() {
        var x: int where x == g;
        while (*) invariant true; {
          assert x == g;
          if (*) { x := 0; break; } else { g := g + 1; }
        }
      }
      """);
    Validate(result);
    var loop = Statements(result.Program!.Unit.Body).OfType<Ir.Loop>().Single();
    var header = Assert.IsType<Ir.Block>(loop.Body);
    Assert.Equal(2, header.Statements.Count); // invariant + choice; no x where constraint.
    Assert.IsType<Ir.Choice>(header.Statements[1]);
  }

  [Fact]
  public void NaturalLoopTargetsReceiveWhereBeforeHeaderInvariants() {
    var result = Boogie("""
      procedure P(); implementation P() {
        var x: int where x >= 0;
        x := 0;
        while (x < 2) invariant true; { x := x + 1; }
      }
      """);
    Validate(result);
    var loop = Statements(result.Program!.Unit.Body).OfType<Ir.Loop>().Single();
    var header = Assert.IsType<Ir.Block>(loop.Body);
    Assert.Equal(3, header.Statements.Count);
    Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Assume>(header.Statements[0]).Condition);
    Assert.Equal(new Ir.BooleanLiteral(true), Assert.IsType<Ir.Assume>(header.Statements[1]).Condition);
  }

  [Fact]
  public void LoopNormalizationLeavesTheTypedSourceUntouched() {
    var options = Options();
    var source = ParseBoogie("procedure P(); implementation P() { var i: int; i := 0; while (i < 2) invariant i >= 0; { if (*) { break; } i := i + 1; } }", options);
    var before = Emit(source, options);
    var first = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
    var second = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
    Validate(first); Validate(second);
    Assert.Equal(before, Emit(source, options));
    Assert.Equal(Ir.Protocol.GetProgramHash(first.Program!), Ir.Protocol.GetProgramHash(second.Program!));
  }

  [Theory]
  [InlineData("function Identity(x: int): int { x }")]
  [InlineData("function {:inline} Identity(x: int): int { x }")]
  [InlineData("function {:identity} Identity(x: int): int { x }")]
  public void DefinitionsWithoutAlwaysRevealedRemainOpaque(string definition) {
    var result = Boogie(definition + " procedure P(); implementation P() { assert Identity(1) == 1; }");
    Validate(result);
    var equality = Assert.IsType<Ir.Operation>(Statements(result.Program!.Unit.Body).OfType<Ir.Check>().Single().Condition);
    Assert.IsType<Ir.Application>(equality.Arguments[0]);
  }

  [Theory]
  [InlineData("revealed function Identity(x: int): int { x }")]
  [InlineData("revealed function {:inline} Identity(x: int): int { x }")]
  public void AlwaysRevealedDirectIdentitiesCanBeProjected(string definition) {
    var result = Boogie(definition + " procedure P(); implementation P() { assert Identity(1) == 1; }");
    Validate(result);
    var equality = Assert.IsType<Ir.Operation>(Statements(result.Program!.Unit.Body).OfType<Ir.Check>().Single().Condition);
    Assert.Equal(new Ir.IntegerLiteral("1"), equality.Arguments[0]);
  }

  [Fact]
  public void HideAnywherePreventsIdentityExpansionAndNoPartialProgramEscapes() {
    var result = Boogie("function {:inline} Identity(x: int): int { x } procedure P(); implementation P() { assert Identity(1) == 1; hide Identity; }");
    Assert.False(result.Success); Assert.Null(result.Program); Assert.Empty(result.Obligations);
    Assert.Contains(result.Diagnostics, d => d.Code == "b3_visibility");
  }

  [Fact]
  public void PureVisibilityScopesCanBeOmittedAfterWholeUnitInspection() {
    var result = Boogie("procedure P(); implementation P() { push; assert true; pop; assert false; }");
    Validate(result); Assert.Equal(2, result.Obligations.Count);
  }

  [Theory]
  [InlineData("unroll")]
  [InlineData("induction")]
  [InlineData("houdini")]
  public void OtherLoopModesFailClosed(string mode) {
    var options = Options();
    if (mode == "unroll") { options.LoopUnrollCount = 2; }
    if (mode == "induction") { options.KInductionDepth = 1; }
    if (mode == "houdini") { options.ConcurrentHoudini = true; }
    var result = Boogie("procedure P(); implementation P() { while (*) invariant true; { } }", options);
    Assert.False(result.Success); Assert.Null(result.Program); Assert.Empty(result.Obligations);
    Assert.Contains(result.Diagnostics, d => d.Code == "b3_loop_mode");
  }

  [Fact]
  public void LargeLoopGraphsRejectWithoutProducingPartialIr() {
    var labels = string.Join(" ", Enumerable.Range(0, 257).Select(i => "next" + i + ": assert true;"));
    var result = Boogie("procedure P(); implementation P() { while (*) invariant true; { } " + labels + " }");
    Assert.False(result.Success); Assert.Null(result.Program); Assert.Empty(result.Obligations);
    Assert.Contains(result.Diagnostics, d => d.Code == "b3_cfg_limit");
  }

  [Fact]
  public void ExcessiveLambdaCaptureTraversalFailsBeforeRecursing() {
    var body = string.Join(" + ", Enumerable.Repeat("g", Ir.Protocol.MaximumDepth + 1));
    var result = Boogie("var g: int; procedure P(); implementation P() { var m: [int]int; m := (lambda x: int :: " + body + "); }");
    Assert.False(result.Success); Assert.Null(result.Program);
    Assert.Contains(result.Diagnostics, d => d.Code == "b3_lambda_capture_limit");
  }

  private static DafnyOptions Options() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    return options;
  }
  private static async Task<List<B3NormalizationResult>> Dafny(string text, bool expectSuccess = true) {
    Microsoft.Dafny.Type.ResetScopes();
    var options = Options();
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(text, new Uri("file:///B3NormalizerTests.dfy"), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(m => m.Message)));
    var results = new List<B3NormalizationResult>();
    foreach (var (_, boogie) in BoogieGenerator.Translate(parsed.Program, reporter)) {
      Assert.Equal(0, boogie.Resolve(options)); Assert.Equal(0, boogie.Typecheck(options));
      foreach (var implementation in boogie.Implementations) {
        var result = B3Normalizer.Normalize(boogie, implementation, options);
        if (expectSuccess) { Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Code + ": " + d.Message))); }
        results.Add(result);
      }
    }
    Assert.False(reporter.HasErrors);
    return results;
  }
  private static B3NormalizationResult Boogie(string text, DafnyOptions? options = null) {
    options ??= Options(); var source = ParseBoogie(text, options);
    return B3Normalizer.Normalize(source, source.Implementations.Single(), options);
  }
  private static Bpl.Program ParseBoogie(string text, DafnyOptions options) {
    Assert.Equal(0, Bpl.Parser.Parse(text, "B3NormalizerTests.bpl", out var source));
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options)); return source;
  }
  private static string Emit(Bpl.Program source, DafnyOptions options) {
    using var output = new StringWriter(); using var writer = new Bpl.TokenTextWriter(output, options);
    source.Emit(writer); return output.ToString();
  }
  private static IEnumerable<Ir.Statement> Statements(Ir.Statement statement) {
    if (statement is Ir.Block block) {
      foreach (var child in block.Statements.SelectMany(Statements)) { yield return child; }
      yield break;
    }
    yield return statement;
    if (statement is Ir.Choice choice) {
      foreach (var child in choice.Branches.SelectMany(Statements)) { yield return child; }
    } else if (statement is Ir.Conditional conditional) {
      foreach (var child in Statements(conditional.Then).Concat(Statements(conditional.Else))) { yield return child; }
    } else if (statement is Ir.Loop loop) {
      foreach (var child in Statements(loop.Body)) { yield return child; }
    } else if (statement is Ir.Labeled labeled) {
      foreach (var child in Statements(labeled.Body)) { yield return child; }
    }
  }
  private static void Validate(B3NormalizationResult result) {
    Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
    var request = new Ir.Request(Ir.Protocol.Version, "normalizer-test", Ir.Protocol.NormalizerVersion,
      new string('0', 40), Ir.Protocol.GetProgramHash(result.Program!), result.Program!.Unit.Name, result.Program,
      new Ir.Configuration("z3", new[] { "-in", "-smt2" }, 1000, 10000, 100000, 2, "5.1.0", new string('d', 64)), result.Obligations, new string('f', 64));
    Ir.ProtocolValidation.ValidateRequest(request);
  }
}
