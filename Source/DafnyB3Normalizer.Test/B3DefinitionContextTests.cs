// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Text.Json;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3DefinitionContextTests {
  [Fact]
  public void LearnThenHideReplaysTheSameUfConditionWithoutReloadingTheDefinition() {
    var result = Owned("""
      assume Guard(); assert F() == 7;
      hide F; assert F() == 7; assert F() != F();
      """);
    Assert.True(result.Success, Errors(result));
    Assert.Equal(2, result.Contexts!.Count);
    Assert.Equal(3, result.Obligations.Count);
    var original = Checks(result.Program!.Unit.Body).ToArray();
    var visible = result.Contexts.Single(context => context.Definitions.Count > 0);
    var hidden = result.Contexts.Single(context => context.Definitions.Count == 0);
    Assert.Equal(new[] { original[0].ObligationId }, visible.Obligations.Select(identity => identity.Id));
    Assert.Equal(original.Skip(1).Select(check => check.ObligationId), hidden.Obligations.Select(identity => identity.Id));
    Assert.Contains(Assumptions(hidden.Program.Unit.Body), assumption => ReferenceEquals(assumption.Condition, original[0].Condition));
    Assert.Equal(1, visible.Program.Axioms.Count);
    Assert.Equal(Ir.Operator.Implies, Assert.IsType<Ir.Operation>(visible.Program.Axioms[0].Condition).Operator);
    B3DefinitionContexts.ValidatePartition(result.Program, result.Obligations, result.Contexts, Bpl.Token.NoToken);
  }

  [Fact]
  public void NonlearningCheckDoesNotBecomeAnAssumptionInAnotherMask() {
    var result = Owned("assume Guard(); assert {:subsumption 0} F() == 7; hide F; assert F() == 7;");
    Assert.True(result.Success, Errors(result));
    var first = Checks(result.Program!.Unit.Body).First(); Assert.False(first.Learn);
    var hidden = result.Contexts!.Single(context => context.Definitions.Count == 0);
    Assert.DoesNotContain(Assumptions(hidden.Program.Unit.Body), assumption => ReferenceEquals(assumption.Condition, first.Condition));
  }

  [Fact]
  public void SourceGuardIsNotReplacedByAnUnconditionalEquation() {
    var result = Owned("assert F() == 7;");
    Assert.True(result.Success, Errors(result));
    var axiom = Assert.Single(Assert.Single(result.Contexts!).Program.Axioms);
    var implication = Assert.IsType<Ir.Operation>(axiom.Condition);
    Assert.Equal(Ir.Operator.Implies, implication.Operator);
    Assert.IsType<Ir.Application>(implication.Arguments[0]);
    Assert.DoesNotContain(Assumptions(result.Program!.Unit.Body), assumption =>
      ReferenceEquals(assumption.Condition, implication.Arguments[0]));
  }

  [Fact]
  public void MixedNativeMergeCannotLoadAHiddenOwnersDefinition() {
    var result = Owned("hide *; reveal F; assert F() == 7; reveal *; hide F; assert F() == 7;");
    Assert.True(result.Success, Errors(result));
    Assert.All(result.Contexts!, context => Assert.Empty(context.Definitions));
    Assert.All(result.Contexts!, context => Assert.Empty(context.Program.Axioms));
  }

  [Fact]
  public void ARevealMergeCannotEstablishACheckOnAHiddenIncomingPath() {
    var result = Owned("assume Guard(); if (*) { reveal *; } else { hide *; } assert F() == 7;");
    Assert.True(result.Success, Errors(result));
    Assert.All(result.Contexts!, context => Assert.Empty(context.Definitions));
  }

  [Fact]
  public void EveryIncomingPathRevealedCanRetainTheOwnedDefinition() {
    var result = Owned("assume Guard(); hide F; if (*) { reveal F; } else { reveal F; } assert F() == 7;");
    Assert.True(result.Success, Errors(result));
    Assert.Single(Assert.Single(result.Contexts!).Definitions);
  }

  [Fact]
  public void ChangingTheTargetsPossibleModeCannotHideAnotherRevealOperand() {
    var result = Owned("assume Guard(); hide *; reveal F; if (*) { reveal *; } else { } assert F() == 7; reveal *; hide F; assert F() == 7;");
    Assert.True(result.Success, Errors(result));
    Assert.All(result.Contexts!, context => Assert.Empty(context.Definitions));
  }

  [Fact]
  public void TrueAssertionsAlsoContributeTheirPossibleRevealModes() {
    var result = Owned("assume Guard(); hide *; reveal F; assert F() == 7; if (*) { reveal *; hide F; } else { reveal *; } assert true;");
    Assert.True(result.Success, Errors(result));
    Assert.All(result.Contexts!, context => Assert.Empty(context.Definitions));
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void ActiveTypedBoolLiteralDefinitionKeepsItsEntireGuardedIffFormula(bool value) {
    var literal = value ? "true" : "false";
    var (source, options) = B3VisibilityTests.Parse("function F(): bool; function Guard(): bool; axiom Guard() ==> F() == " + literal +
      "; procedure P(); implementation P() { assume Guard(); assert F() == " + literal + "; hide F; assert F() == " + literal + "; }");
    OwnAxiom(source);
    var axiom = Assert.Single(source.TopLevelDeclarations.OfType<Bpl.Axiom>());
    var implication = Assert.IsType<Bpl.NAryExpr>(axiom.Expr);
    var equality = Assert.IsType<Bpl.NAryExpr>(implication.Args[1]);
    Assert.Equal(Bpl.BinaryOperator.Opcode.Iff, Assert.IsType<Bpl.BinaryOperator>(equality.Fun).Op);
    var result = Normalize(source, options); Assert.True(result.Success, Errors(result));
    var visible = Assert.Single(result.Contexts!.Where(context => context.Definitions.Count > 0));
    var full = Assert.IsType<Ir.Operation>(Assert.Single(visible.Program.Axioms).Condition);
    Assert.Equal(Ir.Operator.Implies, full.Operator); Assert.IsType<Ir.Application>(full.Arguments[0]);
    var defining = Assert.IsType<Ir.Operation>(full.Arguments[1]); Assert.Equal(Ir.Operator.Equiv, defining.Operator);
    Assert.Equal(value, Assert.IsType<Ir.BooleanLiteral>(defining.Arguments[1]).Value);
    Assert.All(result.Contexts.Where(context => context.Obligations.Any(identity => identity.Id == result.Obligations.Last().Id)),
      context => Assert.Empty(context.Definitions));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ActualDafnyOpaqueBoolDefinitionRevealAndFalseControlsReachSourceOwnedContexts(bool negative) {
    var body = negative ? "reveal F(); assert F(); assert F() != F(); hide F; assert true;" :
      "reveal F(); assert F(); hide F; assert F();";
    var results = await Dafny("opaque function F(): bool { true } lemma Use() { " + body + " }");
    Assert.All(results, result => Assert.True(result.Success, Errors(result)));
    var axioms = results.SelectMany(result => result.Contexts!).SelectMany(context => context.Program.Axioms).ToArray();
    Assert.NotEmpty(axioms);
    Assert.Contains(axioms, axiom => Applications(axiom.Condition).Any(application => application.ResultType == "bool" &&
      application.Arguments.Count == 1 && application.Arguments[0] is Ir.BooleanLiteral { Value: true }));
    Assert.All(results.SelectMany(result => result.Contexts!).SelectMany(context => context.Definitions),
      origin => Assert.True(origin.AxiomOrdinal >= 0 && origin.FormulaHash.Length == 64));
  }

  [Fact]
  public void DetachedAxiomMetadataCannotSupplyANamedVisibilityPremise() {
    var (source, options) = Source("hide F; assert F() == 7;");
    var axiom = Assert.Single(source.TopLevelDeclarations.OfType<Bpl.Axiom>());
    source.RemoveTopLevelDeclaration(axiom);
    var result = Normalize(source, options);
    Assert.False(result.Success); Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "b3_visibility");
  }

  [Fact]
  public void FiniteBoolInstancesRetainEachEntireGuardedFormula() {
    var (source, options) = B3VisibilityTests.Parse("""
      function F(): int; function Guard(b: bool): bool;
      axiom (forall b: bool :: Guard(b) ==> F() == 7);
      procedure P(); implementation P() { assert F() == 7; }
      """);
    OwnAxiom(source);
    var result = Normalize(source, options); Assert.True(result.Success, Errors(result));
    var context = Assert.Single(result.Contexts!); Assert.Equal(2, context.Definitions.Count);
    var guards = context.Program.Axioms.Select(axiom => Assert.IsType<Ir.Operation>(axiom.Condition))
      .Select(implication => Assert.IsType<Ir.Application>(implication.Arguments[0])).ToArray();
    Assert.Equal(new[] { false, true }, guards.Select(guard => Assert.IsType<Ir.BooleanLiteral>(Assert.Single(guard.Arguments)).Value).OrderBy(value => value));
    Assert.All(context.Program.Axioms, axiom => Assert.Equal(Ir.Operator.Implies, Assert.IsType<Ir.Operation>(axiom.Condition).Operator));
  }

  [Theory]
  [InlineData("axiom (forall i: int :: F() == i);", "hide F; assert F() == 7;")]
  [InlineData("const C: int; axiom F() == C;", "hide F; assert F() == 7;")]
  [InlineData("axiom Guard() || F() == 7;", "hide F; assert F() == 7;")]
  public void UnreviewedDefinitionShapesFailClosedAtNamedVisibility(string axiom, string body) {
    var (source, options) = B3VisibilityTests.Parse("function F(): int; function Guard(): bool; " + axiom +
      " procedure P(); implementation P() { " + body + " }");
    OwnAxiom(source);
    var result = Normalize(source, options);
    Assert.False(result.Success); Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "b3_visibility");
  }

  [Fact]
  public void MutableGlobalDefinitionIsASourceResolutionRejection() {
    const string text = "function F(): int; var g: int; axiom F() == g; procedure P(); implementation P() { assert true; }";
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    Assert.Equal(0, Bpl.Parser.Parse(text, "B3VisibilityRejectedSource.bpl", out var source));
    Assert.Equal(1, source.Resolve(options));
    // No typed artifact exists; this is not a normalizer rejection or an accepted global axiom.
  }

  [Fact]
  public void ParametricSourceIdentityAllowsTheResolvedBoolLiteralInTheWholeOwnedFormula() {
    var (source, options) = B3VisibilityTests.Parse("""
      revealed function Identity<T>(x: T): T { x }
      function F(): bool; function Guard(): bool;
      axiom Guard() ==> F() == Identity(true);
      procedure P(); implementation P() { assume Guard(); assert F(); hide F; assert F(); }
      """);
    var owner = source.Functions.Single(function => function.Name == "F");
    var identity = source.Functions.Single(function => function.Name == "Identity");
    Assert.NotNull(identity.DefinitionAxiom); Assert.Contains(identity.DefinitionAxiom, source.TopLevelDeclarations);
    Assert.Equal(2, source.TopLevelDeclarations.OfType<Bpl.Axiom>().Count());
    var owned = source.TopLevelDeclarations.OfType<Bpl.Axiom>().Single(axiom => !ReferenceEquals(axiom, identity.DefinitionAxiom));
    owned.CanHide = true; owner.OtherDefinitionAxioms.Add(owned);
    var result = Normalize(source, options); Assert.True(result.Success, Errors(result));
    var visible = Assert.Single(result.Contexts!.Where(context => context.Definitions.Count > 0));
    var implication = Assert.IsType<Ir.Operation>(Assert.Single(visible.Program.Axioms).Condition);
    Assert.Equal(Ir.Operator.Implies, implication.Operator); Assert.IsType<Ir.Application>(implication.Arguments[0]);
    var equation = Assert.IsType<Ir.Operation>(implication.Arguments[1]);
    Assert.Equal(Ir.Operator.Equiv, equation.Operator);
    Assert.True(Assert.IsType<Ir.BooleanLiteral>(equation.Arguments[1]).Value);
    Assert.Single(visible.Definitions);
  }

  [Fact]
  public void ClaimedGenericIdentityWithoutAnActiveDefinitionCannotEnableTheOwnedBoolFormula() {
    var (source, options) = B3VisibilityTests.Parse("""
      revealed function {:identity} Identity<T>(x: T): T;
      function F(): bool; function Guard(): bool;
      axiom Guard() ==> F() == Identity(true);
      procedure P(); implementation P() { hide F; assert F(); }
      """);
    OwnAxiom(source);
    var result = Normalize(source, options);
    Assert.False(result.Success); Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "b3_visibility");
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, true)]
  public void DisconnectedSourceGoalsAndTrailingPopRetainCoverageWithZeroDefinitionPremises(bool withVisibility, bool withTrailingPop) {
    var (source, options) = Source("assume Guard(); " + (withVisibility ? "hide *; reveal F; " : "") +
      "assert F() == 7; return; " + (withTrailingPop ? "pop; " : "") + "assert false;");
    Assert.Equal(withVisibility || withTrailingPop, new B3DefinitionVisibility(source.Implementations.Single()).HasVisibilityCommands);
    // An AlwaysRevealed owner must not bypass the explicit zero-premise rule at a known unreachable goal.
    source.Functions.Single(function => function.Name == "F").AlwaysRevealed = true;
    var result = Normalize(source, options); Assert.True(result.Success, Errors(result));
    Assert.Equal(2, result.Obligations.Count);
    var original = Checks(result.Program!.Unit.Body).ToArray(); Assert.Equal(2, original.Length);
    Assert.False(Assert.IsType<Ir.BooleanLiteral>(original[1].Condition).Value);
    var disconnected = Assert.Single(result.Contexts!.Where(context => context.Obligations.Any(identity => identity.Id == original[1].ObligationId)));
    Assert.Empty(disconnected.Definitions); Assert.Empty(disconnected.Program.Axioms);
    Assert.Contains(Checks(disconnected.Program.Unit.Body), check => ReferenceEquals(check, original[1]));
    Assert.Contains(result.Contexts!, context => context.Definitions.Count > 0);
    B3DefinitionContexts.ValidatePartition(result.Program, result.Obligations, result.Contexts, Bpl.Token.NoToken);
  }

  [Fact]
  public void ActiveOpaqueDeclarationIdentityCannotAliasADetachedSameNameFunction() {
    var (source, options) = B3VisibilityTests.Parse("function F(): int; procedure P(); implementation P() { assert F() == F(); }");
    var assertion = source.Implementations.Single().Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    var call = (Bpl.FunctionCall)((Bpl.NAryExpr)((Bpl.NAryExpr)assertion.Expr).Args[1]).Fun;
    call.Func = (Bpl.Function)source.Functions.Single().Clone();
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    var result = Normalize(source, options);
    Assert.False(result.Success); Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "b3_declaration_identity");
  }

  [Fact]
  public void DetachedNativeBodyRetainsPinnedObjectExpansionSemantics() {
    var (source, options) = B3VisibilityTests.Parse("function {:inline} D(x: int, y: int): int { x div y } procedure P(); implementation P() { assert D(8, 2) == 4; }");
    var assertion = source.Implementations.Single().Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    var call = (Bpl.FunctionCall)((Bpl.NAryExpr)((Bpl.NAryExpr)assertion.Expr).Args[0]).Fun;
    Assert.NotNull(source.Functions.Single().Body);
    call.Func = (Bpl.Function)source.Functions.Single().Clone();
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    var result = Normalize(source, options); Assert.True(result.Success, Errors(result));
    var condition = Assert.IsType<Ir.Operation>(Assert.Single(Checks(result.Program!.Unit.Body)).Condition);
    Assert.Equal(Ir.Operator.Divide, Assert.IsType<Ir.Operation>(condition.Arguments[0]).Operator);
  }

  [Fact]
  public void ActiveOpaqueDeclarationIdentityCannotAliasADetachedSameNameConstant() {
    var (source, options) = B3VisibilityTests.Parse("const C: int; procedure P(); implementation P() { assert C == C; }");
    var assertion = source.Implementations.Single().Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    ((Bpl.IdentifierExpr)((Bpl.NAryExpr)assertion.Expr).Args[1]).Decl =
      (Bpl.Constant)source.TopLevelDeclarations.OfType<Bpl.Constant>().Single().Clone();
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    var result = Normalize(source, options);
    Assert.False(result.Success); Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "b3_declaration_identity");
  }

  [Fact]
  public void CheckedCallAndPostconditionsRetainOriginalHiddenSourceMasks() {
    var (source, options) = B3VisibilityTests.Parse("""
      function F(): int; function Guard(): bool; axiom Guard() ==> F() == 7;
      procedure Q(); requires F() == 7;
      procedure P(); ensures F() == 7;
      implementation P() { assume Guard(); assert F() == 7; hide F; call Q(); return; }
      """);
    OwnAxiom(source); var result = Normalize(source, options);
    Assert.True(result.Success, Errors(result));
    Assert.Contains(result.Obligations, identity => identity.Id.StartsWith("sOcall"));
    Assert.Contains(result.Obligations, identity => identity.Id.StartsWith("sOpost"));
    Assert.All(result.Contexts!.Where(context => context.Obligations.Any(identity =>
      identity.Id.StartsWith("sOcall") || identity.Id.StartsWith("sOpost"))), context => Assert.Empty(context.Definitions));
    B3DefinitionContexts.ValidatePartition(result.Program!, result.Obligations, result.Contexts, Bpl.Token.NoToken);
  }

  [Fact]
  public void UnchangedVisibilityPreservesBothInductionRolesAndTheirHiddenMasks() {
    var result = Owned("assume Guard(); assert F() == 7; hide F; while (*) invariant F() == 7; { push; pop; } assert F() == 7;");
    Assert.True(result.Success, Errors(result));
    Assert.Single(result.Obligations.Where(identity => identity.Id.StartsWith("sOinit")));
    Assert.Single(result.Obligations.Where(identity => identity.Id.StartsWith("sOmaint")));
    Assert.All(result.Contexts!.Where(context => context.Obligations.Any(identity =>
      identity.Id.StartsWith("sOinit") || identity.Id.StartsWith("sOmaint"))), context => Assert.Empty(context.Definitions));
  }

  [Fact]
  public void UnbalancedScopeCannotSupplyARevealedDefinitionPremise() {
    var result = Owned("pop; assume Guard(); assert F() == 7;");
    Assert.False(result.Success); Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "b3_visibility");
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void RawOnlyFalseAssertionCannotDisappearFromTheNormalizedInventory(bool nested) {
    var (source, options) = Source("assume Guard(); assert true;");
    var unit = source.Implementations.Single();
    var assertion = new Bpl.AssertCmd(Bpl.Token.NoToken, Bpl.Expr.False);
    Bpl.Cmd inserted = nested ? new Bpl.StateCmd(Bpl.Token.NoToken, new List<Bpl.Variable>(),
      new List<Bpl.Cmd> { assertion }) : assertion;
    // Break the producer's shared list deliberately; typed native Blocks now contain a real false goal.
    unit.Blocks[0].Cmds = unit.Blocks[0].Cmds.Append(inserted).ToList();
    Assert.DoesNotContain(inserted, unit.StructuredStmts!.BigBlocks[0].simpleCmds);
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    var result = Normalize(source, options);
    Assert.False(result.Success);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "b3_cfg_correspondence");
  }

  [Fact]
  public void PartitionRejectsMissingChecksAndOrdinaryStateChanges() {
    var result = Owned("assume Guard(); assert F() == 7; hide F; assert F() == 7;");
    Assert.True(result.Success, Errors(result));
    Assert.Throws<B3DefinitionVisibility.Rejection>(() => B3DefinitionContexts.ValidatePartition(result.Program!, result.Obligations,
      result.Contexts!.Take(1).ToArray(), Bpl.Token.NoToken));
    var context = result.Contexts!.First();
    var modified = context with { Program = context.Program with { Unit = context.Program.Unit with {
      Body = new Ir.Block(new Ir.Statement[] { new Ir.Assume(new Ir.BooleanLiteral(false)), context.Program.Unit.Body }) } } };
    Assert.Throws<B3DefinitionVisibility.Rejection>(() => B3DefinitionContexts.ValidatePartition(result.Program!, result.Obligations,
      result.Contexts.Select(original => original == context ? modified : original).ToArray(), Bpl.Token.NoToken));
  }

  [Fact]
  public async Task ActualDafnyClosedLiteralDefinitionsHaveActiveGuardedSourceOrigins() {
    var results = await Dafny("""
      function F(): int { 7 }
      lemma LearnThenHide() { assert F() == 7; hide F; assert F() == 7; }
      """);
    Assert.All(results, result => Assert.True(result.Success, Errors(result)));
    Assert.Contains(results, result => result.Contexts!.Any(context => context.Definitions.Count > 0));
    Assert.Contains(results, result => result.Contexts!.Count > 1);
    Assert.All(results.SelectMany(result => result.Contexts!), context => {
      Assert.Equal(context.Definitions.Count, context.Program.Axioms.Count);
      Assert.All(context.Definitions, origin => Assert.True(origin.AxiomOrdinal >= 0 && origin.FormulaHash.Length == 64));
    });
  }

  [Fact]
  public async Task ActualDafnyOpaqueLiteralDefinitionKeepsItsRevealGuardAndStableApplication() {
    var results = await Dafny("""
      opaque function F(): int { 7 }
      lemma UseOpaque() { reveal F(); assert F() == 7; hide F; assert F() == 7; }
      """);
    Assert.All(results, result => Assert.True(result.Success, Errors(result)));
    Assert.Contains(results, result => result.Contexts!.Any(context => context.Definitions.Count > 0));
    Assert.Contains(results.SelectMany(result => result.Contexts!).SelectMany(context => context.Program.Axioms),
      axiom => Applications(axiom.Condition).Any(application => application.ResultType == "int" &&
        application.Arguments.Count == 1 && application.Arguments[0] is Ir.BooleanLiteral { Value: true }));
  }

  internal static async Task<List<B3NormalizationResult>> Dafny(string text) {
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(text, new Uri("file:///B3VisibilityTests.dfy"), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(message => message.Message)));
    var results = new List<B3NormalizationResult>();
    foreach (var (_, source) in BoogieGenerator.Translate(parsed.Program, reporter)) {
      Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
      foreach (var implementation in source.Implementations) { results.Add(Normalize(source, options, implementation)); }
    }
    return results;
  }
  private static (Bpl.Program, DafnyOptions) Source(string body) {
    var (source, options) = B3VisibilityTests.Parse("function F(): int; function Guard(): bool; axiom Guard() ==> F() == 7; " +
      "procedure P(); implementation P() { " + body + " }");
    OwnAxiom(source); return (source, options);
  }
  private static void OwnAxiom(Bpl.Program source) {
    var owner = source.Functions.Single(function => function.Name == "F");
    var axiom = Assert.Single(source.TopLevelDeclarations.OfType<Bpl.Axiom>());
    axiom.CanHide = true; owner.OtherDefinitionAxioms.Add(axiom);
  }
  private static B3NormalizationResult Owned(string body) { var (source, options) = Source(body); return Normalize(source, options); }
  private static B3NormalizationResult Normalize(Bpl.Program source, DafnyOptions options, Bpl.Implementation? implementation = null) =>
    B3Normalizer.Normalize(source, implementation ?? source.Implementations.Single(), options);
  private static string Errors(B3NormalizationResult result) => string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message));
  internal static IEnumerable<Ir.Check> Checks(Ir.Statement root) => Statements(root).OfType<Ir.Check>();
  internal static IEnumerable<Ir.Assume> Assumptions(Ir.Statement root) => Statements(root).OfType<Ir.Assume>();
  private static IEnumerable<Ir.Statement> Statements(Ir.Statement root) {
    yield return root;
    var children = root switch {
      Ir.Block block => block.Statements, Ir.Choice choice => choice.Branches,
      Ir.Conditional conditional => new[] { conditional.Then, conditional.Else },
      Ir.Loop loop => new[] { loop.Body }, Ir.Labeled labeled => new[] { labeled.Body }, _ => Array.Empty<Ir.Statement>()
    };
    foreach (var child in children) { foreach (var statement in Statements(child)) { yield return statement; } }
  }
  private static IEnumerable<Ir.Application> Applications(Ir.Expression expression) {
    if (expression is Ir.Application rootApplication) { yield return rootApplication; }
    var children = expression switch {
      Ir.Application application => application.Arguments, Ir.Operation operation => operation.Arguments,
      Ir.Quantifier quantifier => new[] { quantifier.Body }, Ir.Let let => new[] { let.Value, let.Body },
      Ir.Label label => new[] { label.Body }, _ => Array.Empty<Ir.Expression>()
    };
    foreach (var child in children) { foreach (var application in Applications(child)) { yield return application; } }
  }
}
