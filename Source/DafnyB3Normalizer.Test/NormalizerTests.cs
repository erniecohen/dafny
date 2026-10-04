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
  public async Task ActualDafnyVisibilityAndLoopFeaturesFailClosed() {
    var visibility = await Dafny("function F(x: int): int { x + 1 } lemma V(x: int) { hide F; reveal F; assert F(x) == x + 1; }", false);
    Assert.Contains(visibility, r => !r.Success && r.Diagnostics.Any(d => d.Code == "b3_visibility"));
    var loops = await Dafny("method L(n: nat) { var i := 0; while i < n invariant 0 <= i <= n { i := i + 1; } }", false);
    Assert.Contains(loops, r => !r.Success && r.Diagnostics.Any(d => d.Code == "b3_structured_control"));
    Assert.All(visibility.Concat(loops).Where(r => !r.Success), r => { Assert.Null(r.Program); Assert.Empty(r.Obligations); });
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
  [InlineData("procedure P(); implementation P() { goto done; done: assert true; }", "b3_transfer")]
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
  public void TemporaryOutputWhereIsAssumedAtCallScopeEntryAndAfterHavoc() {
    var result = Boogie("""
      procedure Q(a: int) returns (x: int where x == a); ensures x == a;
      procedure P() returns (y: int);
      implementation P() returns (y: int) { call y := Q(1); }
      """);
    Validate(result);
    var statements = Statements(result.Program!.Unit.Body).ToArray();
    Assert.Equal(2, Assert.IsType<Ir.Havoc>(statements[0]).Variables.Count);
    var entryWhere = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Assume>(statements[1]).Condition);
    var inputSave = Assert.IsType<Ir.Assign>(statements[2]);
    Assert.IsType<Ir.IntegerLiteral>(inputSave.Value);
    Assert.Equal(inputSave.Variable, Assert.IsType<Ir.Variable>(entryWhere.Arguments[1]).Name);
    Assert.Single(Assert.IsType<Ir.Havoc>(statements[3]).Variables);
    var freshWhere = Assert.IsType<Ir.Operation>(Assert.IsType<Ir.Assume>(statements[4]).Condition);
    Assert.Equal(Ir.Protocol.GetProgramHash(new Ir.Program(result.Program.Types, result.Program.Functions, result.Program.Axioms,
      result.Program.Unit with { Body = new Ir.Assume(entryWhere) })),
      Ir.Protocol.GetProgramHash(new Ir.Program(result.Program.Types, result.Program.Functions, result.Program.Axioms,
      result.Program.Unit with { Body = new Ir.Assume(freshWhere) })));
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
  private static B3NormalizationResult Boogie(string text) {
    var options = Options(); var source = ParseBoogie(text, options);
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
