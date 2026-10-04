// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Examines the finite graph of redirecting declarations, without normalizing or instantiating their definitions.
/// It is safe to use while proxies are unresolved and while the graph contains a cycle.
/// </summary>
public static class RedirectingTypeCycleAnalysis {
  public sealed class Dependencies {
    // Sharing immutable empty collections is allocation control, not a cache of mutable definition answers.
    internal static readonly Dependencies Empty = new(FrozenSet<RedirectingTypeDecl>.Empty,
      FrozenSet<TypeProxy>.Empty, FrozenSet<TypeProxy>.Empty);

    public IReadOnlySet<RedirectingTypeDecl> RedirectingTypes { get; }
    public IReadOnlySet<TypeProxy> UnassignedProxies { get; }
    public IReadOnlySet<TypeProxy> ObservedProxies { get; }

    internal Dependencies(IReadOnlySet<RedirectingTypeDecl> redirectingTypes, IReadOnlySet<TypeProxy> unassignedProxies,
      IReadOnlySet<TypeProxy> observedProxies) {
      RedirectingTypes = redirectingTypes;
      UnassignedProxies = unassignedProxies;
      ObservedProxies = observedProxies;
    }
  }

  public sealed class Cycle {
    public IReadOnlyList<RedirectingTypeDecl> Component { get; }
    // Consecutive declarations are connected by edges, including the last declaration back to the first.
    public IReadOnlyList<RedirectingTypeDecl> Witness { get; }

    internal Cycle(IReadOnlyList<RedirectingTypeDecl> component, IReadOnlyList<RedirectingTypeDecl> witness) {
      Component = component;
      Witness = witness;
    }
  }

  public sealed class Result {
    public IReadOnlyDictionary<RedirectingTypeDecl, Dependencies> Definitions { get; }
    internal Graph<RedirectingTypeDecl> Graph { get; }

    internal Result(Dictionary<RedirectingTypeDecl, Dependencies> definitions) {
      Definitions = definitions;
      Graph = CreateGraph(definitions);
    }

    public IReadOnlyList<Cycle> FindCycles() {
      return RedirectingTypeCycleAnalysis.FindCycles(Graph, Definitions);
    }
  }

  /// <summary>
  /// Collects direct declaration dependencies and raw proxy links. Type arguments are inspected, but encountering
  /// a declaration does not expand its definition. Hidden definitions and datatype constructors remain opaque.
  /// Reference identity, rather than semantic type equality, makes even malformed raw proxy/type-argument loops finite.
  /// </summary>
  public static Dependencies CollectDependencies(RedirectingTypeDecl declaration, VisibilityScope? scope = null) {
    if (declaration is not TopLevelDecl topLevelDecl || !topLevelDecl.IsRevealedInScope(scope)) {
      return Dependencies.Empty;
    }

    var definition = declaration is TypeSynonymDeclBase synonym ? synonym.Rhs : declaration.BaseType;
    if (definition == null) {
      return Dependencies.Empty;
    }

    // Most definitions are a scalar, an unassigned base, or a single nominal application, possibly reached through
    // one assigned proxy. These leaves cannot contain raw cycles and need no work stack or visitation set.
    // Longer proxy chains and composite definitions still use the reference-guarded walk below.
    var leafDefinition = definition;
    TypeProxy? assignedLeafProxy = null;
    if (definition is TypeProxy { T: { } leafAssignment } assignedProxy && definition.TypeArgs.Count == 0 &&
        leafAssignment is not TypeProxy && leafAssignment.TypeArgs.Count == 0) {
      leafDefinition = leafAssignment;
      assignedLeafProxy = assignedProxy;
    }
    if (leafDefinition.TypeArgs.Count == 0) {
      if (leafDefinition is TypeProxy { T: null } leafProxy) {
        var proxies = new HashSet<TypeProxy>(ReferenceEqualityComparer.Instance) { leafProxy };
        return new Dependencies(FrozenSet<RedirectingTypeDecl>.Empty, proxies, proxies);
      }
      if (leafDefinition is not TypeProxy) {
        IReadOnlySet<TypeProxy> proxies = assignedLeafProxy == null ? FrozenSet<TypeProxy>.Empty :
          new HashSet<TypeProxy>(ReferenceEqualityComparer.Instance) { assignedLeafProxy };
        if (leafDefinition is UserDefinedType { ResolvedClass: RedirectingTypeDecl leafTarget } &&
            leafTarget is TopLevelDecl leafDecl && leafDecl.IsVisibleInScope(scope)) {
          var targets = new HashSet<RedirectingTypeDecl>(ReferenceEqualityComparer.Instance) { leafTarget };
          return new Dependencies(targets, FrozenSet<TypeProxy>.Empty, proxies);
        }
        return assignedLeafProxy == null ? Dependencies.Empty :
          new Dependencies(FrozenSet<RedirectingTypeDecl>.Empty, FrozenSet<TypeProxy>.Empty, proxies);
      }
    }

    HashSet<RedirectingTypeDecl>? dependencies = null;
    HashSet<TypeProxy>? unassignedProxies = null;
    HashSet<TypeProxy>? observedProxies = null;
    var visited = new HashSet<Type>(ReferenceEqualityComparer.Instance);
    var work = new Stack<Type>();
    work.Push(definition);
    while (work.TryPop(out var type)) {
      if (!visited.Add(type)) {
        continue;
      }
      if (type is TypeProxy proxy) {
        (observedProxies ??= new HashSet<TypeProxy>(ReferenceEqualityComparer.Instance)).Add(proxy);
        if (proxy.T == null) {
          (unassignedProxies ??= new HashSet<TypeProxy>(ReferenceEqualityComparer.Instance)).Add(proxy);
        } else {
          work.Push(proxy.T);
        }
      } else if (type is UserDefinedType { ResolvedClass: RedirectingTypeDecl target } &&
                 target is TopLevelDecl targetDecl && targetDecl.IsVisibleInScope(scope)) {
        (dependencies ??= new HashSet<RedirectingTypeDecl>(ReferenceEqualityComparer.Instance)).Add(target);
      }
      foreach (var argument in type.TypeArgs) {
        work.Push(argument);
      }
    }
    return dependencies == null && observedProxies == null ? Dependencies.Empty : new Dependencies(
      dependencies == null ? FrozenSet<RedirectingTypeDecl>.Empty : dependencies,
      unassignedProxies == null ? FrozenSet<TypeProxy>.Empty : unassignedProxies,
      observedProxies == null ? FrozenSet<TypeProxy>.Empty : observedProxies);
  }

  public static Result Analyze(IEnumerable<RedirectingTypeDecl> roots, VisibilityScope? scope = null) {
    var definitions = new Dictionary<RedirectingTypeDecl, Dependencies>(ReferenceEqualityComparer.Instance);
    var work = new Stack<RedirectingTypeDecl>(Ordered(roots).Reverse());
    while (work.TryPop(out var declaration)) {
      if (definitions.ContainsKey(declaration)) {
        continue;
      }
      var dependencies = CollectDependencies(declaration, scope);
      definitions.Add(declaration, dependencies);
      if (dependencies.RedirectingTypes.Count == 0) {
        continue;
      }
      foreach (var dependency in Ordered(dependencies.RedirectingTypes).Reverse()) {
        work.Push(dependency);
      }
    }
    return new Result(definitions);
  }

  public static IReadOnlyList<RedirectingTypeDecl>? TryFindCycle(RedirectingTypeDecl root,
    VisibilityScope? scope = null) {
    return Analyze(new[] { root }, scope).FindCycles().FirstOrDefault()?.Witness;
  }

  internal static IEnumerable<RedirectingTypeDecl> Ordered(IEnumerable<RedirectingTypeDecl> declarations) {
    // Empty and singleton dependency sets have no order to establish.
    if (declarations is IReadOnlyCollection<RedirectingTypeDecl> { Count: < 2 }) {
      return declarations;
    }
    // Only names and origins are used here: printing a malformed type could itself recurse.
    return declarations.OrderBy(declaration => declaration.Name, StringComparer.Ordinal)
      .ThenBy(declaration => declaration.Module.Name, StringComparer.Ordinal)
      .ThenBy(declaration => declaration.Origin.line)
      .ThenBy(declaration => declaration.Origin.col);
  }

  internal static Graph<RedirectingTypeDecl> CreateGraph(
    IReadOnlyDictionary<RedirectingTypeDecl, Dependencies> definitions) {
    var graph = new Graph<RedirectingTypeDecl>();
    foreach (var declaration in Ordered(definitions.Keys)) {
      graph.AddVertex(declaration);
      if (definitions[declaration].RedirectingTypes.Count == 0) {
        continue;
      }
      foreach (var dependency in Ordered(definitions[declaration].RedirectingTypes)) {
        graph.AddEdge(declaration, dependency);
      }
    }
    return graph;
  }

  internal static IReadOnlyList<Cycle> FindCycles(Graph<RedirectingTypeDecl> graph,
    IReadOnlyDictionary<RedirectingTypeDecl, Dependencies> definitions) {
    List<Cycle>? cycles = null;
    foreach (var representative in graph.TopologicallySortedComponents()) {
      // Successful graphs have singleton SCCs. Do not allocate or sort a component list unless it contains a cycle.
      if (graph.GetSCCSize(representative) == 1 &&
          !definitions[representative].RedirectingTypes.Contains(representative)) {
        continue;
      }
      var component = Ordered(graph.GetSCC(representative)).ToList();
      (cycles ??= []).Add(new Cycle(component, FindWitness(component, definitions)));
    }
    if (cycles == null) {
      return Array.Empty<Cycle>();
    }
    return cycles.Count == 1 ? cycles : cycles.OrderBy(cycle => cycle.Component[0].Name, StringComparer.Ordinal).ToList();
  }

  private static IReadOnlyList<RedirectingTypeDecl> FindWitness(IReadOnlyList<RedirectingTypeDecl> component,
    IReadOnlyDictionary<RedirectingTypeDecl, Dependencies> definitions) {
    var members = new HashSet<RedirectingTypeDecl>(component, ReferenceEqualityComparer.Instance);
    var start = component[0];
    if (definitions[start].RedirectingTypes.Contains(start)) {
      return new[] { start };
    }

    // Find a deterministic path from an outgoing neighbor back to start. The component is strongly connected,
    // so such a path exists. This walk does not use recursive call frames, even for a long cycle.
    var predecessors = new Dictionary<RedirectingTypeDecl, RedirectingTypeDecl>(ReferenceEqualityComparer.Instance);
    var work = new Queue<RedirectingTypeDecl>();
    foreach (var successor in Ordered(definitions[start].RedirectingTypes.Where(members.Contains))) {
      predecessors.Add(successor, start);
      work.Enqueue(successor);
    }
    while (work.TryDequeue(out var current)) {
      foreach (var successor in Ordered(definitions[current].RedirectingTypes.Where(members.Contains))) {
        if (ReferenceEquals(successor, start)) {
          var witness = new List<RedirectingTypeDecl>();
          for (var member = current; !ReferenceEquals(member, start); member = predecessors[member]) {
            witness.Add(member);
          }
          witness.Add(start);
          witness.Reverse();
          return witness;
        }
        if (predecessors.TryAdd(successor, current)) {
          work.Enqueue(successor);
        }
      }
    }
    throw new InvalidOperationException("A cyclic component must contain a cycle witness");
  }
}
