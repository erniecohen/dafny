// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// A resolution-local graph of redirecting declarations. Proxy assignments are inspected synchronously, before
/// inference can ask ancestry-dependent questions about the assigned type. No state survives a resolution attempt.
/// </summary>
public sealed class RedirectingTypeDependencyTracker {
  private readonly List<RedirectingTypeDecl> roots;
  private readonly VisibilityScope? scope;
  private readonly Action<IReadOnlyList<RedirectingTypeDecl>> onCycle;
  private readonly List<HashSet<RedirectingTypeDecl>> reportedComponents = [];
  private readonly Dictionary<TypeProxy, HashSet<RedirectingTypeDecl>> subscriptions =
    new(ReferenceEqualityComparer.Instance);
  private Dictionary<RedirectingTypeDecl, RedirectingTypeCycleAnalysis.Dependencies> definitions =
    new(ReferenceEqualityComparer.Instance);
  private Graph<RedirectingTypeDecl> graph = new();

  public IReadOnlyDictionary<RedirectingTypeDecl, RedirectingTypeCycleAnalysis.Dependencies> Dependencies => definitions;

  public RedirectingTypeDependencyTracker(IEnumerable<TopLevelDecl> declarations, VisibilityScope? scope,
    Action<IReadOnlyList<RedirectingTypeDecl>> onCycle) {
    roots = RedirectingTypeCycleAnalysis.Ordered(declarations.OfType<RedirectingTypeDecl>()).ToList();
    this.scope = scope;
    this.onCycle = onCycle;
    Install(RedirectingTypeCycleAnalysis.Analyze(roots, scope));
    ReportCycles();
  }

  public void OnProxyAssigned(TypeProxy proxy) {
    if (!subscriptions.TryGetValue(proxy, out var owners)) {
      // Expression-local proxies that do not occur in a redirecting definition require no graph work.
      return;
    }

    var affected = RedirectingTypeCycleAnalysis.Ordered(owners).ToList();
    List<(RedirectingTypeDecl Source, RedirectingTypeDecl Target)>? additions = null;
    Queue<RedirectingTypeDecl>? work = null;
    var removedEdge = false;
    foreach (var owner in affected) {
      var previous = definitions[owner];
      var current = RedirectingTypeCycleAnalysis.CollectDependencies(owner, scope);
      removedEdge |= !previous.RedirectingTypes.IsSubsetOf(current.RedirectingTypes);
      ReplaceDefinition(owner, current);
      foreach (var dependency in RedirectingTypeCycleAnalysis.Ordered(current.RedirectingTypes)) {
        if (!previous.RedirectingTypes.Contains(dependency)) {
          (additions ??= []).Add((owner, dependency));
        }
        if (!definitions.ContainsKey(dependency)) {
          (work ??= new Queue<RedirectingTypeDecl>()).Enqueue(dependency);
        }
      }
    }

    if (removedEdge) {
      // Legacy inference normally assigns an unresolved proxy once. Retaining assigned proxy links as subscriptions
      // also supports a replaced target: reconstruct the reachable graph so neither old edges nor orphan definitions
      // survive. This uncommon fallback does not run for monotone assignments or duplicate notifications.
      Install(RedirectingTypeCycleAnalysis.Analyze(roots, scope));
      ReportCycles();
      return;
    }

    while (work != null && work.TryDequeue(out var declaration)) {
      if (definitions.ContainsKey(declaration)) {
        continue;
      }
      var dependencies = RedirectingTypeCycleAnalysis.CollectDependencies(declaration, scope);
      ReplaceDefinition(declaration, dependencies);
      graph.AddVertex(declaration);
      foreach (var dependency in RedirectingTypeCycleAnalysis.Ordered(dependencies.RedirectingTypes)) {
        (additions ??= []).Add((declaration, dependency));
        if (!definitions.ContainsKey(dependency)) {
          (work ??= new Queue<RedirectingTypeDecl>()).Enqueue(dependency);
        }
      }
    }

    if (additions == null) {
      return;
    }
    // Graph.AddEdge does not deduplicate edges. Only dependency-set differences are inserted, including when several
    // raw occurrences or shared proxies expose the same declaration. A notification without new edges needs no SCC work.
    foreach (var (source, target) in additions) {
      graph.AddEdge(source, target);
    }
    if (additions.Any(edge => graph.Reaches(edge.Target, edge.Source))) {
      ReportCycles();
    }
  }

  public void CheckAtRoundBoundary() {
    var cold = RedirectingTypeCycleAnalysis.Analyze(roots, scope);
    Debug.Assert(IsEquivalentTo(cold), "Incremental redirecting dependencies must agree with a cold reconstruction");
    // The cold check is a backstop, including mutations that do not notify this tracker. Normal inference detects
    // cycles synchronously in OnProxyAssigned, before it returns to constraint propagation.
    Install(cold);
    ReportCycles();
  }

  /// <summary>Available to direct tracker tests; compares declaration edges and proxy subscriptions by identity.</summary>
  public bool IsConsistentWithColdAnalysis() {
    return IsEquivalentTo(RedirectingTypeCycleAnalysis.Analyze(roots, scope));
  }

  private bool IsEquivalentTo(RedirectingTypeCycleAnalysis.Result cold) {
    if (definitions.Count != cold.Definitions.Count) {
      return false;
    }
    foreach (var (declaration, dependencies) in definitions) {
      if (!cold.Definitions.TryGetValue(declaration, out var reconstructed) ||
          !dependencies.RedirectingTypes.SetEquals(reconstructed.RedirectingTypes) ||
          !dependencies.UnassignedProxies.SetEquals(reconstructed.UnassignedProxies) ||
          !dependencies.ObservedProxies.SetEquals(reconstructed.ObservedProxies)) {
        return false;
      }
    }
    // Inspect the actual graph as well, so this check detects accidental duplicate insertion and stale adjacency.
    foreach (var vertex in graph.GetVertices()) {
      if (!definitions.TryGetValue(vertex.N, out var dependencies) ||
          vertex.Successors.Count != dependencies.RedirectingTypes.Count ||
          !dependencies.RedirectingTypes.SetEquals(vertex.Successors.Select(successor => successor.N))) {
        return false;
      }
    }
    foreach (var (proxy, owners) in subscriptions) {
      if (owners.Count == 0 || owners.Any(owner =>
            !definitions.TryGetValue(owner, out var dependencies) || !dependencies.ObservedProxies.Contains(proxy))) {
        return false;
      }
    }
    return definitions.All(pair => pair.Value.ObservedProxies.All(proxy =>
      subscriptions.TryGetValue(proxy, out var owners) && owners.Contains(pair.Key)));
  }

  private void ReplaceDefinition(RedirectingTypeDecl declaration, RedirectingTypeCycleAnalysis.Dependencies dependencies) {
    if (definitions.TryGetValue(declaration, out var previous)) {
      foreach (var proxy in previous.ObservedProxies) {
        if (dependencies.ObservedProxies.Contains(proxy)) {
          continue;
        }
        var owners = subscriptions[proxy];
        owners.Remove(declaration);
        if (owners.Count == 0) {
          subscriptions.Remove(proxy);
        }
      }
    }
    definitions[declaration] = dependencies;
    foreach (var proxy in dependencies.ObservedProxies) {
      if (previous?.ObservedProxies.Contains(proxy) == true) {
        continue;
      }
      if (!subscriptions.TryGetValue(proxy, out var owners)) {
        owners = new HashSet<RedirectingTypeDecl>(ReferenceEqualityComparer.Instance);
        subscriptions.Add(proxy, owners);
      }
      owners.Add(declaration);
    }
  }

  private void Install(RedirectingTypeCycleAnalysis.Result analysis) {
    subscriptions.Clear();
    definitions = new Dictionary<RedirectingTypeDecl, RedirectingTypeCycleAnalysis.Dependencies>(ReferenceEqualityComparer.Instance);
    graph = analysis.Graph;
    foreach (var (declaration, dependencies) in analysis.Definitions) {
      ReplaceDefinition(declaration, dependencies);
    }
  }

  private void ReportCycles() {
    foreach (var cycle in RedirectingTypeCycleAnalysis.FindCycles(graph, definitions)) {
      if (reportedComponents.Any(reported => reported.SetEquals(cycle.Component))) {
        continue;
      }
      reportedComponents.Add(new HashSet<RedirectingTypeDecl>(cycle.Component, ReferenceEqualityComparer.Instance));
      // In particular, synonym marks must be installed before the callback or another operation can normalize the
      // newly assigned type. Newtype marks support the defensive ancestry queries. A callback may abort inference.
      RedirectingTypeCycleAnalysis.MarkCyclic(cycle.Component);
      onCycle(cycle.Witness);
    }
  }
}
