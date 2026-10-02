// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Microsoft.Dafny;

internal sealed record CardinalityEdge(TopLevelDecl From, TopLevelDecl To,
  CardinalityWeight Weight, CardinalityReason Reason);
internal sealed record CardinalityCycle(ImmutableArray<CardinalityEdge> Edges);
internal sealed record CardinalityGraphAnalysis(int ComponentCount, ImmutableArray<CardinalityCycle> Cycles);

/// <summary>A separate nominal graph. It does not participate in function or datatype-grounding SCCs.</summary>
internal sealed class CardinalityGraph {
  private readonly CancellationToken cancellationToken;
  private readonly HashSet<TopLevelDecl> nodes;
  private readonly Dictionary<(TopLevelDecl From, TopLevelDecl To), CardinalityEdge> edges = new();
  internal int EdgeCount => edges.Count;

  internal CardinalityGraph(IEnumerable<TopLevelDecl> declarations, CancellationToken cancellationToken = default) {
    this.cancellationToken = cancellationToken;
    nodes = declarations.ToHashSet();
  }

  internal void AddEdge(CardinalityEdge edge) {
    cancellationToken.ThrowIfCancellationRequested();
    nodes.Add(edge.From);
    nodes.Add(edge.To);
    var key = (edge.From, edge.To);
    if (!edges.TryGetValue(key, out var existing) || (byte)edge.Weight > (byte)existing.Weight ||
        edge.Weight == existing.Weight && CompareEdges(edge, existing) < 0) {
      edges[key] = edge;
    }
  }

  internal ImmutableArray<CardinalityEdge> Edges => edges.Values.OrderBy(edge => edge,
    Comparer<CardinalityEdge>.Create(CompareEdges)).ToImmutableArray();

  internal CardinalityGraphAnalysis FindExpansiveCycles() {
    cancellationToken.ThrowIfCancellationRequested();
    var orderedNodes = nodes.OrderBy(node => node, CardinalityOrder.Declarations).ToArray();
    var indices = new Dictionary<TopLevelDecl, int>();
    for (var index = 0; index < orderedNodes.Length; index++) {
      cancellationToken.ThrowIfCancellationRequested();
      indices.Add(orderedNodes[index], index);
    }
    var adjacency = Enumerable.Range(0, orderedNodes.Length).Select(_ => new List<CardinalityEdge>()).ToArray();
    var predecessors = Enumerable.Range(0, orderedNodes.Length).Select(_ => new List<int>()).ToArray();
    foreach (var edge in Edges) {
      cancellationToken.ThrowIfCancellationRequested();
      adjacency[indices[edge.From]].Add(edge);
      predecessors[indices[edge.To]].Add(indices[edge.From]);
    }
    foreach (var successors in adjacency) {
      cancellationToken.ThrowIfCancellationRequested();
      successors.Sort((left, right) => {
        var order = indices[left.To].CompareTo(indices[right.To]);
        return order != 0 ? order : CompareEdges(left, right);
      });
    }
    foreach (var incoming in predecessors) {
      cancellationToken.ThrowIfCancellationRequested();
      incoming.Sort();
    }

    // Iterative Kosaraju: both DFS traversals are safe for user-created long chains.
    var seen = new bool[orderedNodes.Length];
    var finish = new List<int>(orderedNodes.Length);
    var stack = new Stack<(int Node, int Next)>();
    for (var root = 0; root < orderedNodes.Length; root++) {
      cancellationToken.ThrowIfCancellationRequested();
      if (seen[root]) { continue; }
      seen[root] = true;
      stack.Push((root, 0));
      while (stack.TryPop(out var frame)) {
        cancellationToken.ThrowIfCancellationRequested();
        if (frame.Next == adjacency[frame.Node].Count) {
          finish.Add(frame.Node);
        } else {
          stack.Push((frame.Node, frame.Next + 1));
          var next = indices[adjacency[frame.Node][frame.Next].To];
          if (!seen[next]) {
            seen[next] = true;
            stack.Push((next, 0));
          }
        }
      }
    }
    var componentOf = Enumerable.Repeat(-1, orderedNodes.Length).ToArray();
    var componentCount = 0;
    var pending = new Stack<int>();
    for (var position = finish.Count - 1; position >= 0; position--) {
      cancellationToken.ThrowIfCancellationRequested();
      var root = finish[position];
      if (componentOf[root] != -1) { continue; }
      componentOf[root] = componentCount;
      pending.Push(root);
      while (pending.TryPop(out var node)) {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var previous in predecessors[node]) {
          if (componentOf[previous] == -1) {
            componentOf[previous] = componentCount;
            pending.Push(previous);
          }
        }
      }
      componentCount++;
    }

    var witnesses = new Dictionary<int, CardinalityEdge>();
    foreach (var edge in Edges) {
      cancellationToken.ThrowIfCancellationRequested();
      var component = componentOf[indices[edge.From]];
      if (edge.Weight == CardinalityWeight.Expanding && component == componentOf[indices[edge.To]] &&
          (!witnesses.TryGetValue(component, out var previous) || CompareEdges(edge, previous) < 0)) {
        witnesses[component] = edge;
      }
    }
    var cycles = ImmutableArray.CreateBuilder<CardinalityCycle>();
    foreach (var edge in witnesses.Values.OrderBy(edge => edge, Comparer<CardinalityEdge>.Create(CompareEdges))) {
      cancellationToken.ThrowIfCancellationRequested();
      var source = indices[edge.From];
      var target = indices[edge.To];
      var component = componentOf[source];
      var returnPath = new List<CardinalityEdge>();
      if (source != target) {
        // Restrict BFS to the already computed component; do not enumerate cycles.
        var queue = new Queue<int>();
        var reached = new HashSet<int> { target };
        var via = new Dictionary<int, CardinalityEdge>();
        queue.Enqueue(target);
        while (queue.TryDequeue(out var node) && !reached.Contains(source)) {
          cancellationToken.ThrowIfCancellationRequested();
          foreach (var successor in adjacency[node]) {
            var next = indices[successor.To];
            if (componentOf[next] == component && reached.Add(next)) {
              via.Add(next, successor);
              queue.Enqueue(next);
            }
          }
        }
        if (!reached.Contains(source)) {
          throw new InvalidOperationException("An SCC witness has no return path.");
        }
        for (var node = source; node != target;) {
          cancellationToken.ThrowIfCancellationRequested();
          var previous = via[node];
          returnPath.Add(previous);
          node = indices[previous.From];
        }
        returnPath.Reverse();
      }
      cycles.Add(new CardinalityCycle(new[] { edge }.Concat(returnPath).ToImmutableArray()));
    }
    return new CardinalityGraphAnalysis(componentCount, cycles.ToImmutable());
  }

  private static int CompareEdges(CardinalityEdge left, CardinalityEdge right) {
    var order = CardinalityOrder.CompareOrigins(left.Reason.Origin, right.Reason.Origin);
    if (order != 0) { return order; }
    order = CardinalityOrder.CompareDeclarations(left.From, right.From);
    if (order != 0) { return order; }
    order = CardinalityOrder.CompareDeclarations(left.To, right.To);
    return order != 0 ? order : CardinalityReason.Compare(left.Reason, right.Reason);
  }
}
