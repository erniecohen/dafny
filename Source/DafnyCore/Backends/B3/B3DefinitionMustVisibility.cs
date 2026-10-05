// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

public sealed partial class B3DefinitionVisibility {
  /// <summary>A conservative sufficient guard, separate from the exact pinned native analysis.</summary>
  public sealed record MustFrame(IImmutableSet<Bpl.Function> Revealed, bool AllReveal, bool MayReveal) {
    public bool IsRevealed(Bpl.Function function) => function.AlwaysRevealed || Revealed.Contains(function);
    public static MustFrame Merge(MustFrame first, MustFrame second) {
      var intersection = first.Revealed.Intersect(second.Revealed);
      return new MustFrame(intersection, first.AllReveal && second.AllReveal, first.MayReveal || second.MayReveal);
    }
    public static readonly MustFrame Unreachable = new(ImmutableHashSet<Bpl.Function>.Empty, false, false);
  }
  public MustAnalysis AnalyzeMust(IReadOnlyList<Bpl.Function> owners) => new(this, owners);

  public sealed class MustAnalysis {
    private readonly B3DefinitionVisibility graph;
    private readonly IImmutableSet<Bpl.Function> owners;
    private readonly IImmutableSet<Bpl.Function> always;
    private readonly MustFrame root;
    private readonly Dictionary<Node, ImmutableStack<MustFrame>> inputs = new();
    private readonly Dictionary<Node, ImmutableStack<MustFrame>> outputs = new();

    internal MustAnalysis(B3DefinitionVisibility graph, IReadOnlyList<Bpl.Function> owners) {
      Require(owners.Count <= 64 && owners.Distinct().Count() == owners.Count,
        "Must-visibility catalogue exceeds its owner bound", Bpl.Token.NoToken);
      this.graph = graph; this.owners = owners.ToImmutableHashSet();
      always = this.owners.Where(owner => owner.AlwaysRevealed).ToImmutableHashSet();
      root = new MustFrame(this.owners, true, true);
      if (graph.HasVisibilityCommands) { Run(); }
    }
    public MustFrame Before(Bpl.Absy source) => State(source, false);
    public MustFrame After(Bpl.Absy source) => State(source, true);
    // Include every original raw or nested assertion, even if it is not one of the
    // normalizer's generated check roles. Extra operands only weaken availability.
    public IReadOnlyList<MustFrame> NativeAssertionOperands() => graph.nodes.Keys.Concat(graph.nestedOrigins.Keys)
      .OfType<Bpl.AssertCmd>().Select(assertion => After(assertion)).ToArray();

    private MustFrame State(Bpl.Absy source, bool after) {
      var node = graph.Origin(source);
      if (!graph.reachable.Contains(node)) { return MustFrame.Unreachable; }
      if (!graph.HasVisibilityCommands) { return root; }
      var states = after ? outputs : inputs;
      Require(states.TryGetValue(node, out var stack) && !stack.IsEmpty,
        "Unreachable must-visibility operand has no source mask", source.tok);
      return stack.Peek();
    }
    private void Run() {
      var pending = new Stack<Node>(); pending.Push(graph.entry);
      var steps = 0;
      while (pending.Count > 0) {
        var node = pending.Pop();
        Require(++steps <= Ir.Protocol.MaximumNodes * 16, "Must-visibility worklist exceeds its step bound", node.Source.tok);
        var previous = node.Previous.Where(predecessor => graph.reachable.Contains(predecessor) && outputs.ContainsKey(predecessor))
          .Select(predecessor => outputs[predecessor]).ToList();
        if (node == graph.entry) { previous.Add(ImmutableStack<MustFrame>.Empty.Push(root)); }
        var incoming = previous.Count == 0 ? ImmutableStack<MustFrame>.Empty.Push(root) :
          previous.Aggregate((first, second) => Merge(first, second, node.Source.tok));
        if (inputs.TryGetValue(node, out var oldInput) && SameStack(incoming, oldInput)) { continue; }
        inputs[node] = incoming;
        var outgoing = Update(node.Source, incoming);
        if (!outputs.TryGetValue(node, out var oldOutput) || !SameStack(outgoing, oldOutput)) {
          outputs[node] = outgoing;
          foreach (var next in node.Next) { pending.Push(next); }
        }
      }
      foreach (var node in graph.nodes.Values) {
        var previous = node.Previous.Where(predecessor => graph.reachable.Contains(predecessor) && outputs.ContainsKey(predecessor)).Select(predecessor => outputs[predecessor]).ToArray();
        if (previous.Length > 1) { _ = previous.Aggregate((first, second) => Merge(first, second, node.Source.tok)); }
        if (node.Source is Bpl.ReturnCmd && outputs.TryGetValue(node, out var stack)) {
          Require(stack.Count() == 1, "Unbalanced complete must-visibility stack at return", node.Source.tok);
        }
      }
    }
    private ImmutableStack<MustFrame> Update(Bpl.Absy source, ImmutableStack<MustFrame> stack) {
      Require(!stack.IsEmpty, "Unbalanced must-visibility scope", source.tok);
      if (source is Bpl.ChangeScope scope) {
        if (scope.Mode == Bpl.ChangeScope.Modes.Push) {
          Require(stack.Count() < Ir.Protocol.MaximumDepth, "Must-visibility scope depth exceeds its bound", source.tok);
          return stack.Push(stack.Peek());
        }
        Require(scope.Mode == Bpl.ChangeScope.Modes.Pop && stack.Count() > 1,
          "Must-visibility pop has no matching push", source.tok); return stack.Pop();
      }
      if (source is not Bpl.HideRevealCmd command) { return stack; }
      var state = stack.Peek(); MustFrame next;
      if (command.Function == null) {
        next = command.Mode == Bpl.HideRevealCmd.Modes.Reveal ? root : new MustFrame(always, false, false);
      } else {
        if (!owners.Contains(command.Function)) { return stack; }
        next = state with { Revealed = command.Mode == Bpl.HideRevealCmd.Modes.Reveal || command.Function.AlwaysRevealed ?
          state.Revealed.Add(command.Function) : state.Revealed.Remove(command.Function) };
      }
      return SameFrame(next, state) ? stack : stack.Pop().Push(next);
    }
    private static ImmutableStack<MustFrame> Merge(ImmutableStack<MustFrame> first,
      ImmutableStack<MustFrame> second, Bpl.IToken token) {
      Require(!first.IsEmpty && !second.IsEmpty && SameStack(first.Pop(), second.Pop()),
        "Must-visibility joins require matching complete outer scope stacks", token);
      return first.Pop().Push(MustFrame.Merge(first.Peek(), second.Peek()));
    }
    private static bool SameFrame(MustFrame first, MustFrame second) => first.AllReveal == second.AllReveal &&
      first.MayReveal == second.MayReveal && first.Revealed.SetEquals(second.Revealed);
    private static bool SameStack(ImmutableStack<MustFrame> first, ImmutableStack<MustFrame> second) {
      var a = first.ToArray(); var b = second.ToArray();
      return a.Length == b.Length && a.Zip(b, SameFrame).All(equal => equal);
    }
  }
}
