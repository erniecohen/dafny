// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Text.Json;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3WordContextTraversalTests {
  [Fact]
  public void GuardedDefinitionDemandInsideNativeIntToWordKeepsItsExactVisibleAndHiddenReplays() {
    var result = NormalizeOwned("""
      assume Guard(); assert Pack(F()) == 7bv3;
      hide F; assert Pack(F()) == 7bv3; assert false;
      """);
    Assert.True(result.Success, Errors(result));
    var original = B3DefinitionContextTests.Checks(result.Program!.Unit.Body).ToArray();
    Assert.Equal(3, original.Length); Assert.Equal(3, result.Obligations.Count);
    var visible = Assert.Single(result.Contexts!.Where(context => context.Definitions.Count > 0));
    var hidden = Assert.Single(result.Contexts!.Where(context => context.Definitions.Count == 0));
    Assert.Equal(new[] { original[0].ObligationId }, visible.Obligations.Select(identity => identity.Id));
    Assert.Equal(original.Skip(1).Select(check => check.ObligationId), hidden.Obligations.Select(identity => identity.Id));
    var word = WordOperand(original[0]); Assert.Equal(Ir.BitvectorOperator.IntToBitvector, word.Operator);
    var owner = Assert.IsType<Ir.Application>(Assert.Single(word.Arguments)); Assert.Equal("int", owner.Type);
    Assert.Equal(owner.Name, Assert.IsType<Ir.Application>(Assert.Single(WordOperand(original[1]).Arguments)).Name);
    var whole = Assert.IsType<Ir.Operation>(Assert.Single(visible.Program.Axioms).Condition);
    Assert.Equal(Ir.Operator.Implies, whole.Operator); Assert.IsType<Ir.Application>(whole.Arguments[0]);
    var equality = Assert.IsType<Ir.Operation>(whole.Arguments[1]); Assert.Equal(Ir.Operator.Equal, equality.Operator);
    Assert.Equal(owner.Name, Assert.IsType<Ir.Application>(equality.Arguments[0]).Name);
    Assert.Equal(new Ir.IntegerLiteral("7"), equality.Arguments[1]);
    Assert.Equal(B3DefinitionContexts.FormulaHash(whole), Assert.Single(visible.Definitions).FormulaHash);
    Assert.Empty(hidden.Program.Axioms);
    Assert.Contains(B3DefinitionContextTests.Assumptions(hidden.Program.Unit.Body),
      assumption => ReferenceEquals(assumption.Condition, original[0].Condition));
    Assert.Contains(B3DefinitionContextTests.Checks(hidden.Program.Unit.Body),
      check => check.ObligationId == original[2].ObligationId && check.Condition is Ir.BooleanLiteral { Value: false });
    B3DefinitionContexts.ValidatePartition(result.Program, result.Obligations, result.Contexts!, Bpl.Token.NoToken);
  }

  [Fact]
  public void AWordDemandDoesNotSupplyTheMissingSourceGuardOrRemoveTheFalseGoal() {
    var result = NormalizeOwned("assert Pack(F()) == 7bv3; assert false;");
    Assert.True(result.Success, Errors(result)); Assert.Equal(2, result.Obligations.Count);
    var visible = Assert.Single(result.Contexts!.Where(context => context.Definitions.Count > 0));
    var whole = Assert.IsType<Ir.Operation>(Assert.Single(visible.Program.Axioms).Condition);
    Assert.Equal(Ir.Operator.Implies, whole.Operator);
    var guard = Assert.IsType<Ir.Application>(whole.Arguments[0]);
    Assert.Empty(B3DefinitionContextTests.Assumptions(result.Program!.Unit.Body));
    Assert.All(result.Contexts!, context => Assert.DoesNotContain(B3DefinitionContextTests.Assumptions(context.Program.Unit.Body),
      assumption => assumption.Condition is Ir.Application application && application.Name == guard.Name));
    Assert.Single(result.Contexts!.SelectMany(context => B3DefinitionContextTests.Checks(context.Program.Unit.Body))
      .Where(check => check.Condition is Ir.BooleanLiteral { Value: false }));
    B3DefinitionContexts.ValidatePartition(result.Program, result.Obligations, result.Contexts!, Bpl.Token.NoToken);
  }

  [Theory]
  [InlineData(-1, 199992, true)]
  [InlineData(0, 200000, true)]
  [InlineData(1, 200008, false)]
  public void NestedWordArgumentsChargeTheAggregateNodeAdmissionBoundary(int offset, int expectedEstimate, bool admitted) {
    // Eight distinct masks. Body: one Block, an Assume with a two-argument
    // word comparison (tree + literal), and eight two-node Check(true) goals.
    // Hence body nodes = wordNodes + 20, original nodes add eight identities,
    // and three one-node true formulas each add their axiom node: +6.
    var wordNodes = B3DefinitionContexts.MaximumAggregateNodes / 8 - 34 + offset;
    Assert.Equal(expectedEstimate, 8 * (wordNodes + 34));
    Assert.InRange(wordNodes + 28, 1, Ir.Protocol.MaximumNodes - 1);
    var comparison = new Ir.BitvectorOperation(Ir.BitvectorOperator.UnsignedLessEqual, 1, 0, 0, "bool",
      new Ir.Expression[] { WordTree(wordNodes), new Ir.BitvectorLiteral("1", 1) });
    // Every bv1 value is <= 1; the shared ordinary context is satisfiable.
    var ordinary = new Ir.Assume(comparison);
    var checks = Enumerable.Range(0, 8).Select(i => new Ir.Check("sO" + i, new Ir.BooleanLiteral(true), false)).ToArray();
    var program = new Ir.Program(Array.Empty<string>(), Array.Empty<Ir.Function>(), Array.Empty<Ir.Axiom>(),
      new Ir.Unit("sUnit", Array.Empty<Ir.Binding>(), new Ir.Block(new Ir.Statement[] { ordinary }.Concat(checks).ToArray())));
    var obligations = checks.Select((check, i) => new Ir.SourceIdentity(check.ObligationId, "WordContext.bpl", i + 1, 1, "assertion")).ToArray();
    var formulas = Enumerable.Range(0, 3).Select(i => new B3DefinitionContexts.Formula(
      new B3DefinitionOrigin("definition-" + i, "F" + i, i,
        B3DefinitionContexts.FormulaHash(new Ir.BooleanLiteral(true)), "ground"), new Ir.BooleanLiteral(true))).ToArray();
    var selected = obligations.Select((identity, mask) => new KeyValuePair<string, IReadOnlyList<B3DefinitionContexts.Formula>>(
      identity.Id, formulas.Where((_, bit) => (mask & (1 << bit)) != 0).ToArray())).ToDictionary(pair => pair.Key, pair => pair.Value);
    var request = new Ir.Request(Ir.Protocol.Version, "word-context-bound", Ir.Protocol.NormalizerVersion,
      new string('0', 40), Ir.Protocol.GetProgramHash(program), program.Unit.Name, program,
      new Ir.Configuration("z3", new[] { "-in", "-smt2" }, 1000, 10000, 100000, 2, "5.1.0", new string('d', 64)),
      obligations, new string('f', 64));
    // All three inputs independently pass the per-program typed node/depth
    // admission. This does not launch a worker or solver.
    Ir.ProtocolValidation.ValidateRequest(request);
    Assert.True(Bytes(request) < Ir.Protocol.MaximumMessageBytes / 2);
    var aggregateBytes = 8 * (Bytes(program) + Bytes(obligations) +
      formulas.Sum(formula => Bytes(formula.Condition) + 256L) + obligations.Length * 128L + 16384);
    Assert.True(aggregateBytes < B3DefinitionContexts.MaximumAggregateBytes / 2);
    if (!admitted) {
      var error = Assert.Throws<B3DefinitionVisibility.Rejection>(() =>
        B3DefinitionContexts.Create(program, obligations, selected, Bpl.Token.NoToken));
      Assert.Equal("Definition context replay exceeds its aggregate node bound", error.Message);
      return;
    }
    var contexts = B3DefinitionContexts.Create(program, obligations, selected, Bpl.Token.NoToken);
    Assert.Equal(8, contexts.Count);
    Assert.Equal(obligations.Select(identity => identity.Id).OrderBy(id => id, StringComparer.Ordinal),
      contexts.SelectMany(context => context.Obligations).Select(identity => identity.Id).OrderBy(id => id, StringComparer.Ordinal));
    Assert.All(contexts, context => Assert.Contains(B3DefinitionContextTests.Assumptions(context.Program.Unit.Body),
      assumption => ReferenceEquals(assumption.Condition, comparison)));
    B3DefinitionContexts.ValidatePartition(program, obligations, contexts, Bpl.Token.NoToken);
  }

  private static Ir.Expression WordTree(int nodes) {
    Assert.True(nodes > 0);
    if (nodes % 2 == 0) {
      return new Ir.BitvectorOperation(Ir.BitvectorOperator.Not, 1, 0, 0, "#bv1", new[] { WordTree(nodes - 1) });
    }
    return BalancedWordTree((nodes + 1) / 2);
  }
  private static Ir.Expression BalancedWordTree(int leaves) {
    if (leaves == 1) { return new Ir.BitvectorLiteral("0", 1); }
    return new Ir.BitvectorOperation(Ir.BitvectorOperator.Or, 1, 0, 0, "#bv1",
      new[] { BalancedWordTree(leaves / 2), BalancedWordTree(leaves - leaves / 2) });
  }
  private static int Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Ir.Protocol.JsonOptions).Length;
  private static Ir.BitvectorOperation WordOperand(Ir.Check check) =>
    Assert.IsType<Ir.BitvectorOperation>(Assert.IsType<Ir.Operation>(check.Condition).Arguments[0]);
  private static B3NormalizationResult NormalizeOwned(string body) {
    var (source, options) = B3VisibilityTests.Parse("""
      function {:bvbuiltin "(_ int2bv 3)"} Pack(x: int): bv3;
      function F(): int; function Guard(): bool; axiom Guard() ==> F() == 7;
      procedure P(); implementation P() {
      """ + body + " }");
    var owner = source.Functions.Single(function => function.Name == "F");
    var axiom = Assert.Single(source.TopLevelDeclarations.OfType<Bpl.Axiom>());
    axiom.CanHide = true; owner.OtherDefinitionAxioms.Add(axiom);
    return B3Normalizer.Normalize(source, source.Implementations.Single(), options);
  }
  private static string Errors(B3NormalizationResult result) =>
    string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message));
}
