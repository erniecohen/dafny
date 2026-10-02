// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

public class RedirectingTypeDependencyTrackerTests {
  private sealed class Synonym : TypeSynonymDecl {
    public DafnyType Definition { get; set; }
    public override DafnyType Rhs => Definition;
    public override ModuleDefinition ContainingModule => EnclosingModuleDefinition;
    public override bool ShouldVerify => false;

    public Synonym(string name, ModuleDefinition module, DafnyType definition, List<TypeParameter>? parameters = null)
      : base(Token.NoToken, new Name(name), TypeParameterCharacteristics.Default(), parameters ?? [], module, null) {
      Definition = definition;
    }
  }

  private sealed class RawType : DafnyType {
    public override string TypeName(DafnyOptions options, ModuleDefinition context, bool parseAble = false) =>
      throw new InvalidOperationException("The raw collector must not print types");
    public override DafnyType Subst(IDictionary<TypeParameter, DafnyType> subst) =>
      throw new InvalidOperationException("The raw collector must not instantiate types");
    public override DafnyType ReplaceTypeArguments(List<DafnyType> arguments) =>
      throw new InvalidOperationException("The raw collector must not instantiate types");
    public override bool Equals(DafnyType that, bool keepConstraints = false) =>
      throw new InvalidOperationException("The raw collector must use reference identity");
    public override bool ComputeMayInvolveReferences(ISet<DatatypeDecl> visited) =>
      throw new InvalidOperationException("The raw collector must not query semantic reference properties");
  }

  private sealed class StopInference : Exception { }

  private static NewtypeDecl Newtype(string name, ModuleDefinition module, DafnyType baseType,
    List<TypeParameter>? parameters = null) {
    return new NewtypeDecl(Token.NoToken, new Name(name), parameters ?? [], module, baseType,
      SubsetTypeDecl.WKind.CompiledZero, null, [], [], null, false);
  }

  private static UserDefinedType Reference(TopLevelDecl declaration, params DafnyType[] arguments) {
    return UserDefinedType.FromTopLevelDecl(Token.NoToken, declaration, arguments.ToList());
  }

  private static TypeParameter Parameter(string name) {
    return new TypeParameter(Token.NoToken, new Name(name), TPVarianceSyntax.NonVariant_Strict);
  }

  private static void AssertConsistent(RedirectingTypeDependencyTracker tracker) {
    Assert.True(tracker.IsConsistentWithColdAnalysis());
  }

  [Fact]
  public void ProxyMergeTransfersOwnersToTheLaterAssignment() {
    var module = new DefaultModuleDefinition();
    var p = new InferredTypeProxy();
    var q = new InferredTypeProxy();
    var a = Newtype("A", module, p);
    var b = Newtype("B", module, Reference(a));
    var cycles = new List<IReadOnlyList<RedirectingTypeDecl>>();
    var tracker = new RedirectingTypeDependencyTracker([a, b], null, cycles.Add);
    Assert.Contains(p, tracker.Dependencies[a].UnassignedProxies);
    AssertConsistent(tracker);

    p.T = q;
    tracker.OnProxyAssigned(p);
    Assert.Empty(cycles);
    Assert.DoesNotContain(p, tracker.Dependencies[a].UnassignedProxies);
    Assert.Contains(q, tracker.Dependencies[a].UnassignedProxies);
    AssertConsistent(tracker);

    q.T = Reference(b);
    tracker.OnProxyAssigned(q);
    Assert.Equal(new[] { "A", "B" }, Assert.Single(cycles).Select(declaration => declaration.Name));
    Assert.True(a.IsCyclic);
    Assert.True(b.IsCyclic);
    Assert.Empty(tracker.Dependencies[a].UnassignedProxies);
    AssertConsistent(tracker);
    tracker.CheckAtRoundBoundary();
    Assert.Single(cycles);
    AssertConsistent(tracker);
  }

  [Fact]
  public void AssignedTypesSubscribeToUnresolvedArguments() {
    var module = new DefaultModuleDefinition();
    var p = new InferredTypeProxy();
    var argument = new InferredTypeProxy();
    var parameter = Parameter("T");
    var id = new Synonym("Id", module, new UserDefinedType(parameter), [parameter]);
    var a = Newtype("A", module, p);
    var b = Newtype("B", module, Reference(a));
    var cycles = new List<IReadOnlyList<RedirectingTypeDecl>>();
    var tracker = new RedirectingTypeDependencyTracker([a, b, id], null, cycles.Add);

    p.T = Reference(id, argument);
    tracker.OnProxyAssigned(p);
    Assert.Empty(cycles);
    Assert.Contains(id, tracker.Dependencies[a].RedirectingTypes);
    Assert.Contains(argument, tracker.Dependencies[a].UnassignedProxies);
    AssertConsistent(tracker);

    argument.T = Reference(b);
    tracker.OnProxyAssigned(argument);
    Assert.Equal(new[] { "A", "B" }, Assert.Single(cycles).Select(declaration => declaration.Name));
    Assert.False(id.IsCyclic);
    AssertConsistent(tracker);
  }

  [Fact]
  public void SharedProxyAndDuplicateOccurrencesDoNotDuplicateEdgesOrReports() {
    var module = new DefaultModuleDefinition();
    var p = new InferredTypeProxy();
    var a = Newtype("A", module, p);
    var b = Newtype("B", module, Reference(a));
    var c = Newtype("C", module, new SeqType(p));
    var cycles = new List<IReadOnlyList<RedirectingTypeDecl>>();
    var tracker = new RedirectingTypeDependencyTracker([a, b, c], null, cycles.Add);

    // A raw type can contain the same nominal dependency at several positions.
    p.T = new MapType(true, Reference(b), Reference(b));
    tracker.OnProxyAssigned(p);
    Assert.Single(tracker.Dependencies[a].RedirectingTypes);
    Assert.Contains(b, tracker.Dependencies[c].RedirectingTypes);
    Assert.Single(cycles);
    Assert.False(c.IsCyclic);
    AssertConsistent(tracker);

    tracker.OnProxyAssigned(p);
    tracker.OnProxyAssigned(p);
    Assert.Single(cycles);
    AssertConsistent(tracker);
  }

  [Fact]
  public void ScalarAssignmentsRefreshOnlyTheirOwners() {
    var module = new DefaultModuleDefinition();
    var p = new InferredTypeProxy();
    var q = new InferredTypeProxy();
    var a = Newtype("A", module, p);
    var b = Newtype("B", module, q);
    var tracker = new RedirectingTypeDependencyTracker([a, b], null, _ => throw new InvalidOperationException("No cycle is present"));
    var originalA = tracker.Dependencies[a];
    var originalB = tracker.Dependencies[b];

    var unrelated = new InferredTypeProxy { T = DafnyType.Int };
    tracker.OnProxyAssigned(unrelated);
    Assert.Same(originalA, tracker.Dependencies[a]);
    Assert.Same(originalB, tracker.Dependencies[b]);

    p.T = DafnyType.Int;
    tracker.OnProxyAssigned(p);
    Assert.NotSame(originalA, tracker.Dependencies[a]);
    Assert.Same(originalB, tracker.Dependencies[b]);
    Assert.Empty(tracker.Dependencies[a].UnassignedProxies);
    AssertConsistent(tracker);

    q.T = DafnyType.Real;
    tracker.OnProxyAssigned(q);
    tracker.CheckAtRoundBoundary();
    AssertConsistent(tracker);
  }

  [Fact]
  public void ReplacedTargetsDiscardOldEdgesAndUnreachableDefinitions() {
    var module = new DefaultModuleDefinition();
    var p = new InferredTypeProxy();
    var a = Newtype("A", module, p);
    var b = Newtype("B", module, DafnyType.Int);
    var c = Newtype("C", module, DafnyType.Real);
    // B and C stand for visible imported definitions, added only when reachable from A.
    var tracker = new RedirectingTypeDependencyTracker([a], null, _ => throw new InvalidOperationException("No cycle is present"));
    p.T = Reference(b);
    tracker.OnProxyAssigned(p);
    Assert.Contains(b, tracker.Dependencies.Keys);
    AssertConsistent(tracker);

    p.T = Reference(c);
    tracker.OnProxyAssigned(p);
    Assert.DoesNotContain(b, tracker.Dependencies.Keys);
    Assert.Equal(c, Assert.Single(tracker.Dependencies[a].RedirectingTypes));
    AssertConsistent(tracker);
  }

  [Fact]
  public void MixedCyclesAreMarkedBeforeTheSynchronousCallback() {
    var module = new DefaultModuleDefinition();
    var p = new InferredTypeProxy();
    var a = Newtype("A", module, p);
    var alias = new Synonym("Alias", module, Reference(a));
    var bound = new BoundVar(Token.NoToken, "x", Reference(alias));
    var subset = new SubsetTypeDecl(Token.NoToken, new Name("Subset"), TypeParameterCharacteristics.Default(), [],
      module, bound, new LiteralExpr(Token.NoToken, true), SubsetTypeDecl.WKind.CompiledZero, null, null);
    var called = false;
    var tracker = new RedirectingTypeDependencyTracker([a, alias, subset], null, cycle => {
      called = true;
      Assert.True(a.IsCyclic);
      Assert.True(alias.IsCyclic);
      Assert.True(subset.IsCyclic);
      Assert.Equal(new[] { "A", "Subset", "Alias" }, cycle.Select(declaration => declaration.Name));
      throw new StopInference();
    });

    p.T = Reference(subset);
    Assert.Throws<StopInference>(() => tracker.OnProxyAssigned(p));
    Assert.True(called);
    Assert.Same(p, a.BaseType);
    Assert.Same(bound.Type, subset.Rhs);
    AssertConsistent(tracker);
  }

  [Fact]
  public void ColdAnalysisDistinguishesFiniteGenericNestingFromChangingArgumentCycles() {
    var module = new DefaultModuleDefinition();
    var parameter = Parameter("T");
    var id = new Synonym("Id", module, new UserDefinedType(parameter), [parameter]);
    var u = new Synonym("U", module, Reference(id, Reference(id, DafnyType.Int)));
    var legal = RedirectingTypeCycleAnalysis.Analyze(new RedirectingTypeDecl[] { u });
    Assert.Empty(legal.FindCycles());
    Assert.Equal(2, legal.Definitions.Count);
    Assert.Equal(id, Assert.Single(legal.Definitions[u].RedirectingTypes));
    Assert.Empty(legal.Definitions[id].RedirectingTypes);

    var recursive = Newtype("Recursive", module, DafnyType.Int, [parameter]);
    recursive.BaseType = Reference(recursive, new SeqType(new UserDefinedType(parameter)));
    var witness = RedirectingTypeCycleAnalysis.TryFindCycle(recursive);
    Assert.Equal(recursive, Assert.Single(witness!));
    // Detecting a cycle is separate from recording resolution-derived error metadata.
    Assert.False(recursive.IsCyclic);
  }

  [Fact]
  public void RawMalformedProxyAndArgumentLoopsAreFinite() {
    var module = new DefaultModuleDefinition();
    var p = new InferredTypeProxy();
    var q = new InferredTypeProxy();
    p.T = q;
    q.T = p;
    var raw = new RawType();
    raw.TypeArgs.Add(raw);
    raw.TypeArgs.Add(p);
    var a = Newtype("A", module, raw);
    var dependencies = RedirectingTypeCycleAnalysis.CollectDependencies(a);
    Assert.Empty(dependencies.RedirectingTypes);
    Assert.Empty(dependencies.UnassignedProxies);
    Assert.Equal(2, dependencies.ObservedProxies.Count);
    Assert.Null(RedirectingTypeCycleAnalysis.TryFindCycle(a));
  }

  [Fact]
  public void DatatypeRecursionDoesNotBecomeRedirectingRecursion() {
    var module = new DefaultModuleDefinition();
    var alias = new Synonym("ListAlias", module, DafnyType.Int);
    var constructor = new DatatypeCtor(Token.NoToken, new Name("Cons"), false,
      [new Formal(Token.NoToken, "tail", Reference(alias), true, false, null)], null);
    var list = new IndDatatypeDecl(Token.NoToken, new Name("List"), module, [], [constructor], [], [], null, false);
    alias.Definition = Reference(list);
    Assert.Empty(RedirectingTypeCycleAnalysis.CollectDependencies(alias).RedirectingTypes);
    Assert.Null(RedirectingTypeCycleAnalysis.TryFindCycle(alias));

    var genericList = new IndDatatypeDecl(Token.NoToken, new Name("Box"), module, [Parameter("T")],
      [new DatatypeCtor(Token.NoToken, new Name("BoxCtor"), false, [], null)], [], [], null, false);
    alias.Definition = Reference(genericList, Reference(alias));
    Assert.Equal(alias, Assert.Single(RedirectingTypeCycleAnalysis.CollectDependencies(alias).RedirectingTypes));
    Assert.Equal(alias, Assert.Single(RedirectingTypeCycleAnalysis.TryFindCycle(alias)!));
  }

  [Fact]
  public void HiddenImportedDefinitionsRemainOpaque() {
    var module = new DefaultModuleDefinition();
    var scope = new VisibilityScope("Observer");
    var a = Newtype("A", module, DafnyType.Int);
    var b = Newtype("B", module, Reference(a));
    a.BaseType = Reference(b);
    a.AddVisibilityScope(scope, false);
    b.AddVisibilityScope(scope, true);
    var visible = RedirectingTypeCycleAnalysis.Analyze(new RedirectingTypeDecl[] { a }, scope);
    Assert.Empty(visible.FindCycles());
    Assert.Contains(b, visible.Definitions[a].RedirectingTypes);
    Assert.Empty(visible.Definitions[b].RedirectingTypes);
    Assert.Null(RedirectingTypeCycleAnalysis.TryFindCycle(a, scope));
    Assert.NotNull(RedirectingTypeCycleAnalysis.TryFindCycle(a));
    Assert.False(a.IsCyclic);
    Assert.False(b.IsCyclic);
  }

  [Fact]
  public void InitialCyclesHaveDeterministicWitnessesAndFreshAttemptsHaveFreshState() {
    var module = new DefaultModuleDefinition();
    var a = Newtype("A", module, DafnyType.Int);
    var b = Newtype("B", module, Reference(a));
    a.BaseType = Reference(b);
    var firstCycles = new List<IReadOnlyList<RedirectingTypeDecl>>();
    var first = new RedirectingTypeDependencyTracker([b, a], null, firstCycles.Add);
    Assert.Equal(new[] { "A", "B" }, Assert.Single(firstCycles).Select(declaration => declaration.Name));
    first.CheckAtRoundBoundary();
    Assert.Single(firstCycles);
    AssertConsistent(first);

    var freshA = Newtype("A", module, DafnyType.Int);
    var freshB = Newtype("B", module, Reference(freshA));
    var fresh = new RedirectingTypeDependencyTracker([freshA, freshB], null,
      _ => throw new InvalidOperationException("The fresh resolution is acyclic"));
    Assert.False(freshA.IsCyclic);
    Assert.False(freshB.IsCyclic);
    AssertConsistent(fresh);
  }
}
