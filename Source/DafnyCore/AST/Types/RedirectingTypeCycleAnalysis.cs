// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Examines the finite graph of redirecting declarations, without normalizing or instantiating their definitions.
/// It is safe to use while proxies are unresolved and while the graph contains a cycle.
/// </summary>
public static class RedirectingTypeCycleAnalysis {
  public sealed class Dependencies {
    public IReadOnlySet<RedirectingTypeDecl> RedirectingTypes { get; }
    public IReadOnlySet<TypeProxy> UnassignedProxies { get; }
    public IReadOnlySet<TypeProxy> ObservedProxies { get; }

    internal Dependencies(HashSet<RedirectingTypeDecl> redirectingTypes, HashSet<TypeProxy> unassignedProxies,
      HashSet<TypeProxy> observedProxies) {
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
    var dependencies = new HashSet<RedirectingTypeDecl>(ReferenceEqualityComparer.Instance);
    var unassignedProxies = new HashSet<TypeProxy>(ReferenceEqualityComparer.Instance);
    var observedProxies = new HashSet<TypeProxy>(ReferenceEqualityComparer.Instance);
    if (declaration is not TopLevelDecl topLevelDecl || !topLevelDecl.IsRevealedInScope(scope)) {
      return new Dependencies(dependencies, unassignedProxies, observedProxies);
    }

    var definition = declaration is TypeSynonymDeclBase synonym ? synonym.Rhs : declaration.BaseType;
    if (definition == null) {
      return new Dependencies(dependencies, unassignedProxies, observedProxies);
    }

    var visited = new HashSet<Type>(ReferenceEqualityComparer.Instance);
    var work = new Stack<Type>();
    work.Push(definition);
    while (work.TryPop(out var type)) {
      if (!visited.Add(type)) {
        continue;
      }
      if (type is TypeProxy proxy) {
        observedProxies.Add(proxy);
        if (proxy.T == null) {
          unassignedProxies.Add(proxy);
        } else {
          work.Push(proxy.T);
        }
      } else if (type is UserDefinedType { ResolvedClass: RedirectingTypeDecl target } &&
                 target is TopLevelDecl targetDecl && targetDecl.IsVisibleInScope(scope)) {
        dependencies.Add(target);
      }
      foreach (var argument in type.TypeArgs) {
        work.Push(argument);
      }
    }
    return new Dependencies(dependencies, unassignedProxies, observedProxies);
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

  public static void MarkCyclic(IEnumerable<RedirectingTypeDecl> declarations) {
    foreach (var declaration in declarations) {
      switch (declaration) {
        case NewtypeDecl newtype:
          newtype.IsCyclic = true;
          break;
        case TypeSynonymDecl synonym:
          synonym.IsCyclic = true;
          break;
      }
    }
  }

  internal static IEnumerable<RedirectingTypeDecl> Ordered(IEnumerable<RedirectingTypeDecl> declarations) {
    // Only names and origins are used here: printing a malformed type could itself recurse.
    return declarations.OrderBy(declaration => declaration.Name, StringComparer.Ordinal)
      .ThenBy(declaration => declaration.Module.Name, StringComparer.Ordinal)
      .ThenBy(declaration => declaration.Tok.line)
      .ThenBy(declaration => declaration.Tok.col);
  }

  internal static Graph<RedirectingTypeDecl> CreateGraph(
    IReadOnlyDictionary<RedirectingTypeDecl, Dependencies> definitions) {
    var graph = new Graph<RedirectingTypeDecl>();
    foreach (var declaration in Ordered(definitions.Keys)) {
      graph.AddVertex(declaration);
      foreach (var dependency in Ordered(definitions[declaration].RedirectingTypes)) {
        graph.AddEdge(declaration, dependency);
      }
    }
    return graph;
  }

  internal static IReadOnlyList<Cycle> FindCycles(Graph<RedirectingTypeDecl> graph,
    IReadOnlyDictionary<RedirectingTypeDecl, Dependencies> definitions) {
    var cycles = new List<Cycle>();
    foreach (var representative in graph.TopologicallySortedComponents()) {
      var component = Ordered(graph.GetSCC(representative)).ToList();
      if (component.Count > 1 || definitions[representative].RedirectingTypes.Contains(representative)) {
        cycles.Add(new Cycle(component, FindWitness(component, definitions)));
      }
    }
    return cycles.OrderBy(cycle => cycle.Component[0].Name, StringComparer.Ordinal).ToList();
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
