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
    Assert.All(results, result => Assert.Empty(result.Program!.Axioms));
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

  public static IEnumerable<object[]> MapTheoryInputs() {
    var directory = Path.Combine(AppContext.BaseDirectory, "MapTheoryInputs");
    using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "cases.json")));
    foreach (var entry in manifest.RootElement.GetProperty("cases").EnumerateArray()) {
      yield return new object[] { entry.GetProperty("file").GetString()!, entry.GetProperty("expectedAxioms").GetInt32(),
        entry.GetProperty("expectedChecks").GetInt32(), entry.GetProperty("expectedHelpers").GetInt32(),
        entry.GetProperty("expectedObservations").GetInt32(),
        entry.GetProperty("containsLiteralFalse").GetBoolean() };
    }
  }

  [Theory]
  [MemberData(nameof(MapTheoryInputs))]
  public void TypedMapTheoryInputsPreserveEveryCheckAndValidateOwnedHelpers(string file, int axiomCount,
    int checkCount, int helperCount, int observationCount, bool containsLiteralFalse) {
    // The manifest's verdict targets require the separate worker/native gate; this test normalizes only.
    var result = Boogie(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MapTheoryInputs", file)));
    Validate(result);
    Assert.Equal(axiomCount, result.Program!.Axioms.Count);
    Assert.Equal(checkCount, result.Obligations.Count);
    Assert.Equal(helperCount, result.Approximations.Count(a => a.StartsWith("Monomorphic map helper origin:")));
    Assert.Equal(observationCount, result.Approximations.Count(a => a.StartsWith("Map observation equality abstraction:")));
    Assert.Empty(result.Program.Axioms);
    if (containsLiteralFalse) {
      Assert.Contains(Statements(result.Program.Unit.Body).OfType<Ir.Check>(), check => check.Condition is Ir.BooleanLiteral { Value: false });
    }
    if (file == "polymorphic-opaque.bpl") {
      Assert.Contains(result.Approximations, a => a.StartsWith("Polymorphic map sort "));
    }
  }

  [Fact]
  public async Task RealDafnyMapReadsDemandClosedHelpersWithoutCollectionDefinitions() {
    var results = await Dafny("method Read(m: imap<int, int>, key: int) requires key in m { assert m[key] == m[key]; }");
    Assert.All(results, Validate);
    Assert.Contains(results, r => r.Approximations.Any(a => a.StartsWith("Monomorphic map helper origin:")));
    Assert.All(results, r => {
      Assert.Contains(r.Approximations, a => a.Contains("Outside reviewed guarded literal-definition contexts, source axioms"));
      Assert.All(r.Contexts!, context => Assert.Empty(context.Definitions));
    });
  }

  [Fact]
  public void MapReadLoweringDoesNotMutateSourceAndHasDeterministicOrigins() {
    var options = Options();
    var source = ParseBoogie("type M = [int, bool]int; procedure P(m: M); implementation P(m: M) { assert m[0, true := 1][0, false] == m[0, false]; }", options);
    var before = Emit(source, options);
    var first = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
    var second = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
    Validate(first); Validate(second);
    Assert.Equal(before, Emit(source, options));
    Assert.Equal(Ir.Protocol.GetProgramHash(first.Program!), Ir.Protocol.GetProgramHash(second.Program!));
    Assert.Equal(first.Approximations, second.Approximations);
    Assert.Contains(first.Approximations, a => a.Contains("indices=(int,bool)") && a.Contains("encoding=direct-read-store-ITE; global-axioms=0"));
  }

  [Fact]
  public void ExcessiveTupleReadDepthDoesNotProducePartialIr() {
    var indexes = string.Join(",", Enumerable.Repeat("0", 200));
    var mapType = "[" + string.Join(",", Enumerable.Repeat("int", 200)) + "]int";
    var result = Boogie("procedure P(m: " + mapType + "); implementation P(m: " + mapType + ") { assert m[" + indexes + " := 1][" + indexes + "] == 1; }");
    Assert.False(result.Success); Assert.Null(result.Program); Assert.Empty(result.Obligations);
    Assert.Contains(result.Diagnostics, d => d.Code == "b3_owned_ir_limit");
  }

  [Fact]
  public void DirectTupleReadTestsEveryCoordinateAndNestedStoresPeelWithoutAxioms() {
    var tuple = Boogie("procedure P(m: [int,bool]int, i: int, v: int); implementation P(m: [int,bool]int, i: int, v: int) { assert m[i,true := v][i,false] == m[i,false]; }");
    Validate(tuple);
    var check = Assert.Single(Statements(tuple.Program!.Unit.Body).OfType<Ir.Check>());
    var identity = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(check.Condition).Arguments[0]);
    Assert.Equal(Ir.Operator.IfThenElse, identity.Operator);
    var conjunction = Assert.IsType<Ir.Operation>(identity.Arguments[0]);
    Assert.Equal(Ir.Operator.And, conjunction.Operator);
    var booleanCoordinate = Assert.IsType<Ir.Operation>(conjunction.Arguments[1]);
    Assert.Equal(new Ir.BooleanLiteral(true), booleanCoordinate.Arguments[0]);
    Assert.Equal(new Ir.BooleanLiteral(false), booleanCoordinate.Arguments[1]);
    Assert.IsType<Ir.Application>(identity.Arguments[2]);
    Assert.Empty(tuple.Program.Axioms);

    var nested = Boogie("procedure P(m: [int]int); implementation P(m: [int]int) { assert m[0 := 1][1 := 2][0] == 1; }");
    Validate(nested);
    var nestedCheck = Assert.Single(Statements(nested.Program!.Unit.Body).OfType<Ir.Check>());
    Assert.Equal(2, Expressions(nestedCheck.Condition).OfType<Ir.Operation>().Count(op => op.Operator == Ir.Operator.IfThenElse));
    Assert.Empty(nested.Program.Axioms);
  }

  [Fact]
  public void ReadsThroughAssignedVariablesHaveNoGuessedStoreProvenance() {
    var result = Boogie("procedure P(m: [int]int); implementation P(m: [int]int) { var n: [int]int; n := m[0 := 1]; assert n[0] == 1; n := m; assert n[0] == 1; }");
    Validate(result);
    var checks = Statements(result.Program!.Unit.Body).OfType<Ir.Check>().ToArray();
    Assert.Equal(2, checks.Length);
    foreach (var check in checks) {
      var read = Assert.IsType<Ir.Application>(Assert.IsType<Ir.Operation>(check.Condition).Arguments[0]);
      Assert.IsType<Ir.Variable>(read.Arguments[0]);
      Assert.DoesNotContain(Expressions(check.Condition).OfType<Ir.Operation>(), op => op.Operator == Ir.Operator.IfThenElse);
    }
    Assert.Empty(result.Program.Axioms);
  }

  [Fact]
  public void DirectReadIdentityRetainsTheLexicalIndexBinder() {
    var result = Boogie("procedure P(m: [int]int); implementation P(m: [int]int) { assert (forall i: int :: m[i := 1][i] == 1); }");
    Validate(result);
    var quantifier = Assert.IsType<Ir.Quantifier>(Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Check>()).Condition);
    var identity = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Operation>(quantifier.Body).Arguments[0]);
    Assert.Equal(Ir.Operator.IfThenElse, identity.Operator);
    Assert.Contains(Expressions(identity).OfType<Ir.Variable>(), variable => variable.Name == quantifier.Bindings[0].Name);
    Assert.Empty(result.Program.Axioms);
  }

  [Fact]
  public void ObservationEqualityCapturesMapValuesAndIsConsistentAcrossBothPolarities() {
    var result = Boogie("type M = [int]int; procedure P(m: M, n: M); requires (forall i: int :: m[i] == n[i]); implementation P(m: M, n: M) { assert !(forall j: int :: m[j] == n[j]); assert m != n; }");
    Validate(result);
    var assumption = Assert.IsType<Ir.Application>(Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Assume>()).Condition);
    Assert.Equal("bool", assumption.Type);
    Assert.Equal(2, assumption.Arguments.Count);
    Assert.All(assumption.Arguments, argument => Assert.IsType<Ir.Variable>(argument));
    var checks = Statements(result.Program.Unit.Body).OfType<Ir.Check>().ToArray();
    var negated = Assert.IsType<Ir.Operation>(checks[0].Condition);
    Assert.Equal(Ir.Operator.Not, negated.Operator);
    var observation = Assert.IsType<Ir.Application>(negated.Arguments[0]);
    Assert.Equal(assumption.Name, observation.Name);
    Assert.Equal(assumption.Arguments.ToArray(), observation.Arguments.ToArray());
    Assert.Equal(Ir.Operator.NotEqual, Assert.IsType<Ir.Operation>(checks[1].Condition).Operator);
    Assert.Contains(result.Approximations, note => note.StartsWith("Map observation equality abstraction:"));
    Assert.Empty(result.Program.Axioms);
  }

  [Theory]
  [InlineData("==")]
  [InlineData("<==>")]
  public void BooleanObservationEqualityUsesTheResolvedEquivalenceForm(string equality) {
    var result = Boogie("procedure P(m: [bool]bool, n: [bool]bool); requires (forall i: bool :: m[i] " + equality + " n[i]); implementation P(m: [bool]bool, n: [bool]bool) { assert !(forall j: bool :: m[j] " + equality + " n[j]); }");
    Validate(result);
    var assumption = Assert.IsType<Ir.Application>(Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Assume>()).Condition);
    var negated = Assert.IsType<Ir.Operation>(Assert.Single(Statements(result.Program.Unit.Body).OfType<Ir.Check>()).Condition);
    Assert.Equal(Ir.Operator.Not, negated.Operator);
    var observation = Assert.IsType<Ir.Application>(negated.Arguments[0]);
    Assert.Equal(assumption.Name, observation.Name);
    Assert.Equal(assumption.Arguments.ToArray(), observation.Arguments.ToArray());
    Assert.Single(result.Approximations.Where(note => note.StartsWith("Map observation equality abstraction:")));
    Assert.Empty(result.Program.Axioms);
  }

  [Theory]
  [InlineData("forall i,j: int :: m[i,j] == n[i,j]", true)]
  [InlineData("forall i: int :: m[i,0] == n[i,0]", false)]
  [InlineData("forall i,j: int :: m[i,i] == n[i,i]", false)]
  [InlineData("forall i,j: int :: m[j,i] == n[j,i]", false)]
  [InlineData("forall i,j: int :: m[i,j] == n[j,i]", false)]
  [InlineData("forall i,j,k: int :: m[i,j] == n[i,j]", false)]
  [InlineData("forall i,j: int :: i >= 0 ==> m[i,j] == n[i,j]", false)]
  [InlineData("forall i,j: int :: m[i+g,j] == n[i+g,j]", false)]
  [InlineData("exists i,j: int :: m[i,j] == n[i,j]", false)]
  [InlineData("forall i,j: int :: { m[i,j] } m[i,j] == n[i,j]", false)]
  [InlineData("forall i,j: int :: {:weight 1} m[i,j] == n[i,j]", false)]
  public void ObservationMatchingRequiresExactlyTheCompleteOrderedTuple(string formula, bool abstracted) {
    var result = Boogie("procedure P(m: [int,int]int, n: [int,int]int, g: int); implementation P(m: [int,int]int, n: [int,int]int, g: int) { assert (" + formula + "); }");
    Validate(result);
    var condition = Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Check>()).Condition;
    if (abstracted) { Assert.IsType<Ir.Application>(condition); }
    else { Assert.IsType<Ir.Quantifier>(condition); }
    Assert.Equal(abstracted, result.Approximations.Any(note => note.StartsWith("Map observation equality abstraction:")));
    Assert.Empty(result.Program.Axioms);
  }

  [Fact]
  public void AMapExpressionDependingOnTheIndexBinderIsNotAbstracted() {
    var result = Boogie("function F(i: int): [int]int; procedure P(n: [int]int); implementation P(n: [int]int) { assert (forall i: int :: F(i)[i] == n[i]); }");
    Validate(result);
    Assert.IsType<Ir.Quantifier>(Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Check>()).Condition);
    Assert.DoesNotContain(result.Approximations, note => note.StartsWith("Map observation equality abstraction:"));
  }

  [Fact]
  public void ObservationAbstractionDoesNotGuessMapOperationsFromFunctionNames() {
    var result = Boogie("function select(m: [int]int, i: int): int; procedure P(m: [int]int, n: [int]int); implementation P(m: [int]int, n: [int]int) { assert (forall i: int :: select(m,i) == select(n,i)); }");
    Validate(result);
    Assert.IsType<Ir.Quantifier>(Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Check>()).Condition);
    Assert.DoesNotContain(result.Approximations, note => note.StartsWith("Map observation equality abstraction:"));
  }

  [Fact]
  public void ObservationAbstractionKeepsTypePolymorphicMapsOpaque() {
    var result = Boogie("procedure P(m: <T>[T]bool, n: <T>[T]bool); implementation P(m: <T>[T]bool, n: <T>[T]bool) { assert (forall i: int :: m[i] == n[i]); }");
    Validate(result);
    Assert.IsType<Ir.Quantifier>(Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Check>()).Condition);
    Assert.DoesNotContain(result.Approximations, note => note.StartsWith("Map observation equality abstraction:"));
    Assert.Contains(result.Approximations, note => note.StartsWith("Polymorphic map sort "));
  }

  [Fact]
  public void ObservationAbstractionPreservesNotForQuantifiersSubsumption() {
    var result = Boogie("procedure P(m: [int]int, n: [int]int); implementation P(m: [int]int, n: [int]int) { assert {:subsumption 1} (forall i: int :: m[i] == n[i]); }");
    Validate(result);
    var check = Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Check>());
    Assert.IsType<Ir.Application>(check.Condition);
    Assert.False(check.Learn);
  }

  [Fact]
  public void ObservationEqualityHasNoPointwiseOrReverseEqualityPremises() {
    var result = Boogie("procedure P(m: [int]int, n: [int]int); requires (forall i: int :: m[i] == n[i]); implementation P(m: [int]int, n: [int]int) { assert m[0] == n[0]; assert m == n; }");
    Validate(result);
    Assert.IsType<Ir.Application>(Assert.Single(Statements(result.Program!.Unit.Body).OfType<Ir.Assume>()).Condition);
    var checks = Statements(result.Program.Unit.Body).OfType<Ir.Check>().ToArray();
    var pointwise = Assert.IsType<Ir.Operation>(checks[0].Condition);
    Assert.All(pointwise.Arguments, argument => Assert.IsType<Ir.Application>(argument));
    Assert.All(Assert.IsType<Ir.Operation>(checks[1].Condition).Arguments, argument => Assert.IsType<Ir.Variable>(argument));
    Assert.Empty(result.Program.Axioms);
    Assert.Equal(2, result.Obligations.Count);
  }

  [Fact]
  public void ObservationEqualityKeepsOldAndCurrentMapCapturesSeparateAfterAssignment() {
    var result = Boogie("var g: [int]int; procedure P(m: [int]int); modifies g; implementation P(m: [int]int) { assert (forall i: int :: old(g)[i] == g[i]); g := m; assert (forall i: int :: old(g)[i] == g[i]); }");
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    var snapshot = statements.OfType<Ir.Assign>().First();
    var captures = statements.OfType<Ir.Check>().Select(check => Assert.IsType<Ir.Application>(check.Condition)).ToArray();
    Assert.Equal(2, captures.Length);
    Assert.Equal(captures[0].Name, captures[1].Name);
    foreach (var observation in captures) {
      var old = Assert.IsType<Ir.Variable>(observation.Arguments[0]);
      var current = Assert.IsType<Ir.Variable>(observation.Arguments[1]);
      Assert.Equal(snapshot.Variable, old.Name);
      Assert.NotEqual(old.Name, current.Name);
    }
    Assert.Contains(statements.OfType<Ir.Assign>(), assignment => assignment.Variable == ((Ir.Variable)captures[1].Arguments[1]).Name);
  }

  [Fact]
  public void CallObservationChecksCaptureTheSavedActualMapInputs() {
    var result = Boogie("procedure Q(a: [int]int, b: [int]int); requires (forall i: int :: a[i] == b[i]); procedure P(m: [int]int, n: [int]int); implementation P(m: [int]int, n: [int]int) { call Q(m,n); }");
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    var observation = Assert.IsType<Ir.Application>(Assert.Single(statements.OfType<Ir.Check>()).Condition);
    var captured = observation.Arguments.Select(argument => Assert.IsType<Ir.Variable>(argument).Name).ToArray();
    Assert.Equal(2, captured.Length);
    Assert.All(captured, name => Assert.Contains(statements.OfType<Ir.Assign>(), assignment => assignment.Variable == name));
    Assert.Empty(result.Program.Axioms);
  }

  [Theory]
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
  public void IdentityAxiomAtAnIntegerInstanceCannotProjectBooleanInstances() {
    var options = Options();
    var source = ParseBoogie("""
      revealed function Identity<T>(x: T): T;
      axiom (forall x: int :: Identity(x) == x);
      procedure P(b: bool);
      implementation P(b: bool) { assert Identity(b) == b; assert Identity(1) == 1; }
      """, options);
    // The public typed-IR API permits attaching a typed, specialized axiom as definition metadata.
    // The reviewed Dafny producer uses CreateDefinitionAxiom instead, quantifying every type parameter.
    source.TopLevelDeclarations.OfType<Bpl.Function>().Single().DefinitionAxiom =
      source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single();
    var result = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
    Validate(result);
    Assert.All(Statements(result.Program!.Unit.Body).OfType<Ir.Check>(), check => {
      var equality = Assert.IsType<Ir.Operation>(check.Condition);
      Assert.IsType<Ir.Application>(equality.Arguments[0]);
    });
  }

  [Theory]
  [InlineData("revealed function Identity<T>(x: T): T { x }", "Identity(b)", "Identity(1)")]
  [InlineData("revealed function Identity<A, B>(x: A, y: B): A { x }", "Identity(b, 1)", "Identity(1, b)")]
  public void UniversallyParametricIdentityProjectsIntegerAndBooleanInstances(string definition, string booleanCall, string integerCall) {
    var result = Boogie(definition + " procedure P(b: bool); implementation P(b: bool) { assert " +
      booleanCall + " == b; assert " + integerCall + " == 1; }");
    Validate(result);
    var equalities = Statements(result.Program!.Unit.Body).OfType<Ir.Check>()
      .Select(check => Assert.IsType<Ir.Operation>(check.Condition)).ToArray();
    Assert.Equal(2, equalities.Length);
    Assert.Equal(equalities[0].Arguments[1], Assert.IsType<Ir.Variable>(equalities[0].Arguments[0]));
    Assert.Equal(new Ir.IntegerLiteral("1"), equalities[1].Arguments[0]);
  }

  [Fact]
  public void DirectTypedBodyIsActiveWithoutAnInlineAttribute() {
    var options = Options();
    var source = ParseBoogie("""
      revealed function Identity<T>(x: T): T;
      procedure P(b: bool);
      implementation P(b: bool) { assert Identity(b) == b; assert Identity(1) == 1; }
      """, options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    var input = function.InParams.Single();
    function.Body = new Bpl.IdentifierExpr(input.tok, input);
    Assert.False(Bpl.QKeyValueExtensions.FindBoolAttribute(function.Attributes, "inline"));
    Assert.Equal(0, source.Typecheck(options));
    var result = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
    Validate(result);
    Assert.All(Statements(result.Program!.Unit.Body).OfType<Ir.Check>(), check => {
      var equality = Assert.IsType<Ir.Operation>(check.Condition);
      Assert.Equal(equality.Arguments[1], equality.Arguments[0]);
    });
  }

  [Fact]
  public void DetachedUniversalIdentityMetadataCannotSupplyAProjectionPremise() {
    var options = Options();
    var source = ParseBoogie("""
      revealed function Identity<T>(x: T): T { x }
      procedure P(b: bool);
      implementation P(b: bool) { assert Identity(b) == b; }
      """, options);
    var function = source.TopLevelDeclarations.OfType<Bpl.Function>().Single();
    var definition = function.DefinitionAxiom;
    Assert.NotNull(definition);
    source.RemoveTopLevelDeclaration(definition);
    Assert.Same(definition, function.DefinitionAxiom);
    var result = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
    Validate(result);
    var equality = Assert.IsType<Ir.Operation>(Statements(result.Program!.Unit.Body).OfType<Ir.Check>().Single().Condition);
    Assert.IsType<Ir.Application>(equality.Arguments[0]);
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
    Assert.Contains(result.Diagnostics, d => d.Code == "b3_cfg_correspondence");
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
  private static IEnumerable<Ir.Expression> Expressions(Ir.Expression expression) {
    yield return expression;
    IEnumerable<Ir.Expression> children = expression switch {
      Ir.Application application => application.Arguments,
      Ir.Operation operation => operation.Arguments,
      Ir.Quantifier quantifier => new[] { quantifier.Body }.Concat(quantifier.Patterns.SelectMany(pattern => pattern)),
      Ir.Let let => new[] { let.Value, let.Body },
      Ir.Label label => new[] { label.Body },
      _ => Array.Empty<Ir.Expression>()
    };
    foreach (var child in children.SelectMany(Expressions)) { yield return child; }
  }

  private static void Validate(B3NormalizationResult result) {
    Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
    var request = new Ir.Request(Ir.Protocol.Version, "normalizer-test", Ir.Protocol.NormalizerVersion,
      new string('0', 40), Ir.Protocol.GetProgramHash(result.Program!), result.Program!.Unit.Name, result.Program,
      new Ir.Configuration("z3", new[] { "-in", "-smt2" }, 1000, 10000, 100000, 2, "5.1.0", new string('d', 64)), result.Obligations, new string('f', 64));
    Ir.ProtocolValidation.ValidateRequest(request);
  }
}
