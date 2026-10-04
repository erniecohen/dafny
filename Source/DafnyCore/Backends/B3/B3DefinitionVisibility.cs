// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

/// <summary>A bounded owned metadata analysis, with no VC generation or source mutation.</summary>
public sealed class B3DefinitionVisibility {
  // Transfer and merge rules ported from Boogie 73a0e214a87df85fc058270268c1d0706fd05bc9,
  // Source/VCGeneration/Prune/RevealedAnalysis.cs and DataflowAnalysis.cs (MIT).
  // The native mixed-mode merge and top-frame fixed-point comparison are intentional.
  public sealed record Frame(Bpl.HideRevealCmd.Modes Mode, IImmutableSet<Bpl.Function> Offset) {
    public bool IsRevealed(Bpl.Function function) =>
      (Mode == Bpl.HideRevealCmd.Modes.Hide) == Offset.Contains(function) || function.AlwaysRevealed;
    public static readonly Frame AllRevealed = new(Bpl.HideRevealCmd.Modes.Reveal, ImmutableHashSet<Bpl.Function>.Empty);
  }
  public sealed class Rejection : Exception {
    public Bpl.IToken Token { get; }
    public Rejection(string message, Bpl.IToken token) : base(message) { Token = token; }
  }
  private sealed class Node {
    public readonly Bpl.Absy Source;
    public readonly List<Node> Next = new();
    public readonly List<Node> Previous = new();
    public Node(Bpl.Absy source) { Source = source; }
  }
  private readonly Dictionary<Bpl.Absy, Node> nodes = new();
  private readonly Dictionary<Node, ImmutableStack<Frame>> input = new();
  private readonly Dictionary<Node, ImmutableStack<Frame>> output = new();
  private readonly Dictionary<Bpl.Absy, Node> nestedOrigins = new();
  public bool HasVisibilityCommands { get; }

  public B3DefinitionVisibility(Bpl.Implementation implementation) {
    var blocks = implementation.Blocks;
    Require(blocks.Count is > 0 and <= 256, "Visibility CFG exceeds the audited block bound", implementation.tok);
    Require(blocks.Distinct().Count() == blocks.Count && blocks.Sum(block => (long)block.Cmds.Count + 1) <= Ir.Protocol.MaximumNodes,
      "Visibility CFG has duplicate blocks or exceeds the node bound", implementation.tok);
    Require(blocks.Sum(block => block.TransferCmd is Bpl.GotoCmd jump ? (long)(jump.LabelTargets?.Count ?? 0) : 0) <= Ir.Protocol.MaximumNodes,
      "Visibility CFG exceeds the edge bound", implementation.tok);
    var ownedBlocks = new Dictionary<Bpl.Block, Node[]>();
    foreach (var block in blocks) {
      var commands = block.Cmds.Cast<Bpl.Absy>().Append(block.TransferCmd).ToArray();
      var wrappers = commands.Select(command => {
        Require(command != null && !nodes.ContainsKey(command), "Shared or missing CFG command has ambiguous source identity", block.tok);
        var node = new Node(command); nodes.Add(command, node); return node;
      }).ToArray();
      ownedBlocks.Add(block, wrappers);
      for (var i = 0; i + 1 < wrappers.Length; i++) { Edge(wrappers[i], wrappers[i + 1]); }
    }
    Require(nodes.Count <= Ir.Protocol.MaximumNodes, "Visibility CFG exceeds the node bound", implementation.tok);
    foreach (var block in blocks) {
      if (block.TransferCmd is not Bpl.GotoCmd jump) { continue; }
      Require(jump.LabelTargets != null && jump.LabelTargets.All(ownedBlocks.ContainsKey),
        "Visibility CFG has an unresolved or external successor", jump.tok);
      foreach (var successor in jump.LabelTargets) { Edge(ownedBlocks[block][^1], ownedBlocks[successor][0]); }
    }
    HasVisibilityCommands = nodes.Keys.Any(command => command is Bpl.HideRevealCmd or Bpl.ChangeScope);
    foreach (var node in nodes.Values) {
      if (node.Source is Bpl.StateCmd state) { AddNested(state.Cmds, node, 0); }
    }
    if (!HasVisibilityCommands) { return; }
    // A raw loop-header mask does not distinguish native initiation from preservation.
    // Until the induction CFG has its own visibility origins, reject visibility changes in cycles.
    var reachabilitySteps = 0;
    foreach (var node in nodes.Values.Where(node => node.Source is Bpl.HideRevealCmd)) {
      Require(!Reaches(node, node, ref reachabilitySteps), "Visibility changes on a CFG cycle require induction-role mapping", node.Source.tok);
    }
    Run();
    foreach (var node in nodes.Values.Where(node => node.Source is Bpl.ReturnCmd)) {
      if (output.TryGetValue(node, out var stack)) {
        Require(stack.Count() == 1, "Unbalanced visibility scope at an implementation return", node.Source.tok);
      }
    }
  }

  public Frame Before(Bpl.Absy source) => State(source, false);
  public Frame After(Bpl.Absy source) => State(source, true);
  private Frame State(Bpl.Absy source, bool after) {
    if (!HasVisibilityCommands) { return Frame.AllRevealed; }
    Require(source != null && (nodes.ContainsKey(source) || nestedOrigins.ContainsKey(source)),
      "No exact CFG origin for a visibility-sensitive check", source?.tok ?? Bpl.Token.NoToken);
    var node = nodes.TryGetValue(source, out var direct) ? direct : nestedOrigins[source];
    var states = after ? output : input;
    Require(states.TryGetValue(node, out var stack) && !stack.IsEmpty,
      "Unreachable or unbalanced visibility-sensitive check has no mask", source.tok);
    return stack.Peek();
  }
  private void AddNested(IEnumerable<Bpl.Cmd> commands, Node parent, int depth) {
    Require(depth < Ir.Protocol.MaximumDepth, "Nested command visibility exceeds its bound", parent.Source.tok);
    foreach (var command in commands) {
      Require(command is not Bpl.HideRevealCmd && command is not Bpl.ChangeScope,
        "StateCmd visibility or scope changes require a separate CFG mapping", command.tok);
      Require(nodes.Count + nestedOrigins.Count < Ir.Protocol.MaximumNodes && !nodes.ContainsKey(command) && nestedOrigins.TryAdd(command, parent),
        "Shared nested command has ambiguous visibility origin", command.tok);
      if (command is Bpl.StateCmd nested) { AddNested(nested.Cmds, parent, depth + 1); }
    }
  }
  private static void Edge(Node first, Node second) { first.Next.Add(second); second.Previous.Add(first); }
  private static bool Reaches(Node from, Node target, ref int steps) {
    var pending = new Stack<Node>(from.Next); var seen = new HashSet<Node>();
    while (pending.Count > 0) {
      var node = pending.Pop();
      Require(++steps <= Ir.Protocol.MaximumNodes * 16, "Visibility cycle traversal exceeds its bound", target.Source.tok);
      if (node == target) { return true; }
      if (!seen.Add(node)) { continue; }
      foreach (var next in node.Next) { pending.Push(next); }
    }
    return false;
  }
  private void Run() {
    var pending = new Stack<Node>(nodes.Values.Where(node => node.Previous.Count == 0));
    var steps = 0;
    while (pending.Count > 0) {
      var node = pending.Pop();
      Require(++steps <= Ir.Protocol.MaximumNodes * 16, "Visibility fixed-point traversal exceeds its bound", node.Source.tok);
      var previous = node.Previous.Where(output.ContainsKey).Select(predecessor => output[predecessor]).ToArray();
      var incoming = previous.Length == 0 ? ImmutableStack<Frame>.Empty.Push(Frame.AllRevealed) :
        previous.Aggregate((first, second) => Merge(first, second, node.Source.tok));
      if (input.TryGetValue(node, out var oldInput) && NativeEquals(incoming, oldInput)) {
        Require(EqualTail(incoming, oldInput), "Changing outer visibility frames are outside the pinned top-frame fixed-point boundary", node.Source.tok);
        continue;
      }
      input[node] = incoming;
      var outgoing = Update(node.Source, incoming);
      if (!output.TryGetValue(node, out var oldOutput) || !NativeEquals(outgoing, oldOutput)) {
        output[node] = outgoing;
        foreach (var next in node.Next) { pending.Push(next); }
      } else {
        Require(EqualTail(outgoing, oldOutput), "Changing outer visibility frames are outside the pinned top-frame fixed-point boundary", node.Source.tok);
      }
    }
    // Verify every completed join, including predecessors discovered after a fixed-point comparison.
    foreach (var node in nodes.Values) {
      var previous = node.Previous.Where(output.ContainsKey).Select(predecessor => output[predecessor]).ToArray();
      if (previous.Length > 1) { _ = previous.Aggregate((first, second) => Merge(first, second, node.Source.tok)); }
    }
  }
  private static bool NativeEquals(ImmutableStack<Frame> first, ImmutableStack<Frame> second) =>
    !first.IsEmpty && !second.IsEmpty && first.Peek().Equals(second.Peek());
  private static bool SameFrame(Frame first, Frame second) => first.Mode == second.Mode && first.Offset.SetEquals(second.Offset);
  private static bool EqualTail(ImmutableStack<Frame> first, ImmutableStack<Frame> second) {
    if (first.IsEmpty || second.IsEmpty) { return first.IsEmpty && second.IsEmpty; }
    var a = first.Pop().ToArray(); var b = second.Pop().ToArray();
    return a.Length == b.Length && a.Zip(b, SameFrame).All(equal => equal);
  }
  private static ImmutableStack<Frame> Merge(ImmutableStack<Frame> first, ImmutableStack<Frame> second, Bpl.IToken token) {
    Require(!first.IsEmpty && !second.IsEmpty && EqualTail(first, second),
      "Visibility joins require equal complete outer scope stacks", token);
    return first.Pop().Push(MergeFrames(first.Peek(), second.Peek()));
  }
  public static Frame MergeFrames(Frame first, Frame second) {
    if (first.Mode == Bpl.HideRevealCmd.Modes.Reveal && second.Mode == Bpl.HideRevealCmd.Modes.Reveal) {
      var intersection = first.Offset.Intersect(second.Offset);
      return intersection.Count == first.Offset.Count ? first : new Frame(Bpl.HideRevealCmd.Modes.Reveal, intersection);
    }
    if (first.Mode == Bpl.HideRevealCmd.Modes.Reveal) { return first; }
    if (second.Mode == Bpl.HideRevealCmd.Modes.Reveal) { return second; }
    var union = first.Offset.Union(second.Offset);
    return union.Count == first.Offset.Count ? first : new Frame(Bpl.HideRevealCmd.Modes.Hide, union);
  }
  private static ImmutableStack<Frame> Update(Bpl.Absy source, ImmutableStack<Frame> stack) {
    Require(!stack.IsEmpty, "Unbalanced visibility scope", source.tok);
    if (source is Bpl.ChangeScope scope) {
      Require(scope.Mode is Bpl.ChangeScope.Modes.Push or Bpl.ChangeScope.Modes.Pop, "Unknown visibility scope mode", source.tok);
      Require(scope.Mode != Bpl.ChangeScope.Modes.Pop || stack.Count() > 1, "Visibility scope pop has no matching push", source.tok);
      return scope.Mode == Bpl.ChangeScope.Modes.Push ? stack.Push(stack.Peek()) : stack.Pop();
    }
    if (source is Bpl.HideRevealCmd hide) {
      Require(hide.Mode is Bpl.HideRevealCmd.Modes.Hide or Bpl.HideRevealCmd.Modes.Reveal, "Unknown hide/reveal mode", source.tok);
      var state = stack.Peek();
      var next = hide.Function == null ? new Frame(hide.Mode, ImmutableHashSet<Bpl.Function>.Empty) :
        state with { Offset = hide.Mode == state.Mode ? state.Offset.Remove(hide.Function) : state.Offset.Add(hide.Function) };
      return next.Equals(state) ? stack : stack.Pop().Push(next);
    }
    return stack;
  }
  private static void Require(bool condition, string message, Bpl.IToken token) {
    if (!condition) { throw new Rejection(message, token); }
  }
}
