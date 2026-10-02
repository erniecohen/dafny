// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using Microsoft.Dafny;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DafnyCore.Test.Resolver.Cardinality;

public class CardinalityGraphTests {
  private static readonly CardinalityWeight P = CardinalityWeight.Preserving;
  private static readonly CardinalityWeight E = CardinalityWeight.Expanding;

  [Theory]
  [InlineData(false, false, false)]
  [InlineData(false, true, true)]
  [InlineData(true, false, true)]
  [InlineData(true, true, true)]
  public void ExpansionIsAbsorbing(bool left, bool right, bool expected) {
    Assert.Equal(expected ? E : P, CardinalityWeights.Join(left ? E : P, right ? E : P));
  }

  [Fact]
  public void ProfileAbsenceIsDifferentFromPreserving() {
    var declaration = Node("D", 0);
    var atom = CardinalityAtom.Head(declaration);
    var profile = new CardinalityProfile();
    Assert.False(profile.TryGet(atom, out _));
    profile.Add(atom, P, Reason(declaration));
    Assert.True(profile.TryGet(atom, out var dependency));
    Assert.Equal(P, dependency.Weight);
  }

  [Fact]
  public void JoiningCannotLoseAnExpansiveOccurrence() {
    var declaration = Node("D", 0);
    var atom = CardinalityAtom.Head(declaration);
    foreach (var reverse in new[] { false, true }) {
      var profile = new CardinalityProfile();
      profile.Add(atom, reverse ? E : P, Reason(declaration, "first"));
      profile.Add(atom, reverse ? P : E, Reason(declaration, "second"));
      Assert.True(profile.TryGet(atom, out var dependency));
      Assert.Equal(E, dependency.Weight);
    }
  }

  [Fact]
  public void AtomIdentityUsesOwnerAndPosition() {
    var first = Node("Same", 0);
    var second = Node("Same", 0);
    Assert.NotEqual(CardinalityAtom.Head(first), CardinalityAtom.Head(second));
    Assert.NotEqual(CardinalityAtom.Head(first), CardinalityAtom.Formal(first, 0));
    Assert.NotEqual(CardinalityAtom.Formal(first, 0), CardinalityAtom.Formal(first, 1));
    Assert.Equal(CardinalityAtom.Formal(first, 0), CardinalityAtom.Formal(first, 0));
  }

  [Fact]
  public void DuplicateEdgesKeepExpansionInEitherInsertionOrder() {
    var a = Node("A", 0);
    var b = Node("B", 1);
    foreach (var reverse in new[] { false, true }) {
      var graph = new CardinalityGraph(new[] { a, b });
      graph.AddEdge(Edge(a, b, reverse ? E : P));
      graph.AddEdge(Edge(a, b, reverse ? P : E));
      graph.AddEdge(Edge(b, a, P));
      Assert.Equal(2, graph.EdgeCount);
      Assert.Equal(E, graph.Edges.Single(edge => ReferenceEquals(edge.From, a)).Weight);
      Assert.Single(graph.FindExpansiveCycles().Cycles);
    }
  }

  [Fact]
  public void PreservingCyclesAndExpandingAcyclicEdgesRemainLegal() {
    var a = Node("A", 0);
    var b = Node("B", 1);
    var c = Node("C", 2);
    var graph = new CardinalityGraph(new[] { a, b, c });
    graph.AddEdge(Edge(a, a, P));
    graph.AddEdge(Edge(a, b, P));
    graph.AddEdge(Edge(b, a, P));
    graph.AddEdge(Edge(b, c, E));
    var result = graph.FindExpansiveCycles();
    Assert.Equal(2, result.ComponentCount);
    Assert.Empty(result.Cycles);
  }

  [Fact]
  public void ExpansiveSelfLoopIsAOneEdgeWitness() {
    var a = Node("A", 0);
    var graph = new CardinalityGraph(new[] { a });
    graph.AddEdge(Edge(a, a, E));
    var cycle = Assert.Single(graph.FindExpansiveCycles().Cycles);
    Assert.Single(cycle.Edges);
    Assert.Same(a, cycle.Edges[0].From);
    Assert.Same(a, cycle.Edges[0].To);
  }

  [Fact]
  public void WitnessesAreDeterministicUnderInputPermutations() {
    var nodes = Enumerable.Range(0, 4).Select(index => Node($"D{index}", index)).ToArray();
    var edges = new[] {
      Edge(nodes[0], nodes[1], E), Edge(nodes[1], nodes[0], P),
      Edge(nodes[1], nodes[2], P), Edge(nodes[2], nodes[0], E),
      Edge(nodes[1], nodes[3], P), Edge(nodes[3], nodes[0], P)
    };
    string? expected = null;
    for (var offset = 0; offset < edges.Length; offset++) {
      foreach (var reverse in new[] { false, true }) {
        var graph = new CardinalityGraph(reverse ? nodes.Reverse() : nodes);
        var orderedEdges = edges.Skip(offset).Concat(edges.Take(offset));
        foreach (var edge in reverse ? orderedEdges.Reverse() : orderedEdges) {
          graph.AddEdge(edge);
        }
        var cycle = Assert.Single(graph.FindExpansiveCycles().Cycles);
        var witness = string.Join(";", cycle.Edges.Select(edge => $"{edge.From.Name}>{edge.To.Name}:{edge.Weight}"));
        expected ??= witness;
        Assert.Equal(expected, witness);
        AssertClosedWitness(cycle);
      }
    }
  }

  [Fact]
  public void AllThreeNodeGraphsAgreeWithIndependentReachabilityOracle() {
    var nodes = Enumerable.Range(0, 3).Select(index => Node($"D{index}", index)).ToArray();
    for (var mask = 0; mask < 1 << 9; mask++) {
      var reach = new bool[3, 3];
      for (var from = 0; from < 3; from++) {
        reach[from, from] = true;
        for (var to = 0; to < 3; to++) {
          reach[from, to] |= (mask & (1 << (3 * from + to))) != 0;
        }
      }
      for (var middle = 0; middle < 3; middle++) {
        for (var from = 0; from < 3; from++) {
          for (var to = 0; to < 3; to++) {
            reach[from, to] |= reach[from, middle] && reach[middle, to];
          }
        }
      }
      var expectedComponents = Enumerable.Range(0, 3).Count(node =>
        !Enumerable.Range(0, node).Any(previous => reach[node, previous] && reach[previous, node]));
      // Mark each possible edge expansive separately; the topology oracle is
      // independent of the production SCC algorithm and edge weights.
      for (var expansive = -1; expansive < 9; expansive++) {
        var graph = new CardinalityGraph(nodes);
        for (var edge = 0; edge < 9; edge++) {
          if ((mask & (1 << edge)) != 0) {
            graph.AddEdge(Edge(nodes[edge / 3], nodes[edge % 3], edge == expansive ? E : P));
          }
        }
        var result = graph.FindExpansiveCycles();
        Assert.Equal(expectedComponents, result.ComponentCount);
        var rejected = expansive >= 0 && (mask & (1 << expansive)) != 0 &&
          reach[expansive % 3, expansive / 3];
        Assert.Equal(rejected ? 1 : 0, result.Cycles.Length);
        foreach (var cycle in result.Cycles) {
          AssertClosedWitness(cycle);
          Assert.All(cycle.Edges, edge => Assert.Contains(edge, graph.Edges));
        }
      }
    }
  }

  [Fact]
  public void LongChainAndLongCycleDoNotUseRecursiveGraphTraversal() {
    const int count = 6000;
    var nodes = Enumerable.Range(0, count).Select(index => Node($"D{index}", index)).ToArray();
    var graph = new CardinalityGraph(nodes);
    for (var index = 1; index < count; index++) {
      graph.AddEdge(Edge(nodes[index - 1], nodes[index], P));
    }
    var chain = graph.FindExpansiveCycles();
    Assert.Equal(count, chain.ComponentCount);
    Assert.Empty(chain.Cycles);
    graph.AddEdge(Edge(nodes[^1], nodes[0], E));
    var closed = graph.FindExpansiveCycles();
    Assert.Equal(1, closed.ComponentCount);
    var cycle = Assert.Single(closed.Cycles);
    Assert.Equal(count, cycle.Edges.Length);
    AssertClosedWitness(cycle);
  }

  [Fact]
  public void GraphCancellationDoesNotReturnSuccessfulAnalysis() {
    using var cancellation = new CancellationTokenSource();
    var a = Node("A", 0);
    var graph = new CardinalityGraph(new[] { a }, cancellation.Token);
    cancellation.Cancel();
    Assert.Throws<OperationCanceledException>(() => graph.AddEdge(Edge(a, a, E)));
    Assert.Throws<OperationCanceledException>(() => graph.FindExpansiveCycles());
  }

  private static void AssertClosedWitness(CardinalityCycle cycle) {
    Assert.NotEmpty(cycle.Edges);
    Assert.Equal(E, cycle.Edges[0].Weight);
    for (var index = 0; index < cycle.Edges.Length; index++) {
      Assert.Same(cycle.Edges[index].To, cycle.Edges[(index + 1) % cycle.Edges.Length].From);
    }
  }

  private static CardinalityReason Reason(TopLevelDecl declaration, string description = "payload") =>
    new(declaration.Origin, CardinalityReasonKind.ConstructorFormal, description);

  private static CardinalityEdge Edge(TopLevelDecl from, TopLevelDecl to, CardinalityWeight weight) =>
    new(from, to, weight, Reason(from));

  private static TopLevelDecl Node(string name, int index) => new TestDeclaration(name, index);

  private sealed class TestDeclaration : TopLevelDecl {
    internal TestDeclaration(string name, int index) : this(name,
      new Token(1, index + 1) { Uri = new Uri("untitled:cardinality-graph"), pos = index }) { }
    private TestDeclaration(string name, IOrigin origin) : base(origin, new Name(origin, name), null!, [], null!) { }
    public override string WhatKind => "test type";
    public override SymbolKind? Kind => null;
    public override string GetDescription(DafnyOptions options) => Name;
  }
}
