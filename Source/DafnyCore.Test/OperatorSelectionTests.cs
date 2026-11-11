using System.Collections.Immutable;
using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

[Collection("Obligation translation")]
public class OperatorSelectionTests {
  private const string Source = """
    datatype D<T> = D(value: T) {
      function twice(): D<T> { this + this }
      function {:operator "+"} plus(other: D<T>): D<T> { other }
    }
    function f(x: D<int>): D<int> { x + x }
    function chain(x: D<int>, y: D<int>): bool { x == y == x }
    """;

  private static async Task<(Program Program, BatchErrorReporter Reporter)> Parse(string source, bool resolve = true) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.UserDefinedOperators, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:operators.dfy"), reporter);
    if (resolve && !reporter.HasErrors) {
      await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    }
    return (parsed.Program, reporter);
  }

  private static Function Function(Program program, string name) =>
    program.DefaultModuleDef.DefaultClass.Members.OfType<Function>().Single(f => f.Name == name);

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task BothCloneModesAndRepeatedClonesKeepTheSelectedCall(bool resolved) {
    var (program, reporter) = await Parse(Source);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(m => m.Message)));
    var original = Assert.IsType<OperatorSyntaxExpr>(Function(program, "f").Body);
    var clone = Assert.IsType<OperatorSyntaxExpr>(new Cloner(false, resolved).CloneExpr(original));
    clone = Assert.IsType<OperatorSyntaxExpr>(new Cloner(false, resolved).CloneExpr(clone));
    var selected = Assert.IsType<MemberOperatorSelection>(clone.ReplaySelection());
    Assert.Same(Assert.IsType<MemberOperatorSelection>(original.Selection).Member, selected.Member);
    if (resolved) {
      Assert.Same(selected.Member, Assert.IsType<FunctionCallExpr>(clone.Resolved).Function);
    } else {
      Assert.Null(clone.ResolvedExpression);
    }
  }

  [Fact]
  public async Task FinalDeclarationMapDoesNotUseTheReferenceCache() {
    var (program, reporter) = await Parse(Source);
    Assert.False(reporter.HasErrors);
    var owner = program.DefaultModuleDef.TopLevelDecls.OfType<IndDatatypeDecl>().Single();
    var cloner = new Cloner();
    var oldPlus = owner.Members.OfType<Function>().Single(f => f.Name == "plus");
    cloner.CloneMember(oldPlus, true);
    var cloned = Assert.IsType<IndDatatypeDecl>(cloner.CloneDeclaration(owner, program.DefaultModuleDef));
    var twice = cloned.Members.OfType<Function>().Single(f => f.Name == "twice");
    var selection = Assert.IsType<MemberOperatorSelection>(Assert.IsType<OperatorSyntaxExpr>(twice.Body).ReplaySelection());
    var finalPlus = cloned.Members.OfType<Function>().Single(f => f.Name == "plus");
    Assert.NotSame(oldPlus, finalPlus);
    Assert.Same(finalPlus, selection.Member);
    Assert.Same(cloned, selection.Receiver.Head);
    Assert.Same(cloned.TypeArgs.Single(), selection.Receiver.Arguments.Single().Head);
  }

  [Fact]
  public async Task MissingFrozenSelectionIsRejectedWithoutLookingUpAgain() {
    var (program, reporter) = await Parse(Source, false);
    var op = Assert.IsType<OperatorSyntaxExpr>(Function(program, "f").Body);
    op.ResolutionStarted = true; // unit-injected stale resolved occurrence
    await new ProgramResolver(program).Resolve(CancellationToken.None);
    Assert.Contains(reporter.AllMessages, m => m.Message.Contains("selection is missing"));
  }

  [Fact]
  public async Task IntrinsicRankCannotBecomeAComparisonPredicate() {
    var (program, reporter) = await Parse("""
      datatype D = D { predicate {:operator "<"} less(b: D) { true } }
      predicate p(x: D) { x < x }
      """);
    Assert.False(reporter.HasErrors);
    var op = Assert.IsType<OperatorSyntaxExpr>(Function(program, "p").Body);
    var selected = Assert.IsType<IntrinsicOperatorSelection>(op.Selection);
    Assert.Equal(BinaryExpr.ResolvedOpcode.RankLt, selected.Opcode);
  }

  [Fact]
  public async Task ConcreteClonerRoutesPreserveTheReplayRecipe() {
    var (program, reporter) = await Parse(Source);
    Assert.False(reporter.HasErrors);
    var op = Assert.IsType<OperatorSyntaxExpr>(Function(program, "f").Body);
    var module = program.DefaultModuleDef;
    var k = new LiteralExpr(Token.NoToken, 1);
    var cloners = new Cloner[] {
      new Cloner(), new ClonerKeepParensExpressions(), new DeepModuleSignatureCloner(),
      new ClonerButDropMethodBodies(), new ScopeCloner(new VisibilityScope()),
      new AbstractSignatureCloner(new VisibilityScope()), new RefinementCloner(module),
      new SubstitutingCloner(new Dictionary<IVariable, IVariable>(), false),
      new ClonerButIVariablesAreKeptOnce(), QuantifiedVariableDomainCloner.Instance,
      QuantifiedVariableRangeCloner.Instance,
      new ExtremeLemmaSpecificationSubstituter(new HashSet<Expression>(), k, reporter, false)
    };
    foreach (var cloner in cloners) {
      var clone = Assert.IsType<OperatorSyntaxExpr>(cloner.CloneExpr(op));
      Assert.Same(op.Selection, clone.Selection);
      Assert.NotEmpty(clone.CloneContexts);
    }
    // ExtremeCloner is abstract; both concrete descendants share the same base handling.
    // The body descendant additionally needs an extreme-lemma context.
    var (extreme, extremeReporter) = await Parse("least lemma L() ensures true { }", false);
    var lemma = extreme.DefaultModuleDef.DefaultClass.Members.OfType<ExtremeLemma>().Single();
    var bodyCloner = new ExtremeLemmaBodyCloner(lemma, k, new HashSet<ExtremePredicate>(), new HashSet<CoDatatypeDecl>(), extremeReporter);
    Assert.IsType<OperatorSyntaxExpr>(bodyCloner.CloneExpr(op));
  }

  [Fact]
  public async Task HiddenExportRequiresFinalCorrespondence() {
    var (program, reporter) = await Parse(Source);
    Assert.False(reporter.HasErrors);
    var selected = Assert.IsType<OperatorSyntaxExpr>(Function(program, "f").Body).Selection;
    var context = new OperatorCloneContext();
    context.Require(Assert.IsType<MemberOperatorSelection>(selected).Member);
    Assert.Throws<InvalidOperationException>(() => selected.Transport(context));
  }
}
