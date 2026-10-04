// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

#nullable disable

/// <summary>Read-only validation of the pinned structured-to-raw producer shape.</summary>
public static class B3StructuredCfgCorrespondence {
  public sealed class Rejection : Exception {
    public Bpl.IToken Token { get; }
    public Rejection(string message, Bpl.IToken token) : base(message) { Token = token; }
  }
  public static void Validate(Bpl.Implementation implementation) => new Validator(implementation).Run();

  // Source: Boogie 73a0e214a87df85fc058270268c1d0706fd05bc9 (MIT),
  // Core/AST/StructuredBoogie/BigBlocksResolutionContext.cs:316-607 and StmtList.cs:92-131.
  // These are descriptions, never native Blocks or commands. Generated labels bind by
  // deterministic producer block position; their spelling supplies no semantic evidence.
  private sealed record CommandShape(Bpl.Cmd Original, Bpl.Expr Guard = null, bool Negated = false);
  private sealed class BlockShape {
    public string Label;
    public IReadOnlyList<CommandShape> Commands;
    public Bpl.TransferCmd OriginalTransfer;
    public Bpl.QKeyValue TransferAttributes;
    public IReadOnlyList<Func<string>> Targets; // null denotes return
  }
  private sealed class Validator {
    private readonly Bpl.Implementation unit;
    private readonly List<BlockShape> expected = new();
    private readonly Dictionary<Bpl.BigBlock, Bpl.BigBlock> successors = new();
    private readonly HashSet<object> sourceNodes = new();
    private int sourceCount;
    private int commandCount;
    private int expectedCount;
    private int edgeCount;

    public Validator(Bpl.Implementation unit) { this.unit = unit; }
    public void Run() {
      Require(unit.StructuredStmts != null && unit.Blocks.Count is > 0 and <= 256,
        "Structured/raw CFG exceeds the audited block bound", unit.tok);
      Require(unit.Blocks.Distinct().Count() == unit.Blocks.Count &&
        unit.Blocks.All(block => block != null && block.Label != null && block.Cmds != null && block.TransferCmd != null) &&
        unit.Blocks.Select(block => block.Label).Distinct(StringComparer.Ordinal).Count() == unit.Blocks.Count,
        "Raw CFG has missing or ambiguous block identity", unit.tok);
      Require(unit.Blocks.Sum(block => (long)block.Cmds.Count + 1) <= Ir.Protocol.MaximumNodes &&
        unit.Blocks.Sum(block => block.TransferCmd is Bpl.GotoCmd jump ? (long)(jump.LabelNames?.Count ?? 0) : 0) <= Ir.Protocol.MaximumNodes,
        "Raw CFG command/edge inventory exceeds its bound", unit.tok);
      Index(unit.StructuredStmts, null, Array.Empty<Bpl.BigBlock>(), 0);
      Create(unit.StructuredStmts, null, null, false, false, 0);
      Require(expected.Count == unit.Blocks.Count, "Raw CFG contains missing or extra producer blocks", unit.tok);
      var byLabel = unit.Blocks.ToDictionary(block => block.Label, StringComparer.Ordinal);
      var commandIdentities = new HashSet<Bpl.Cmd>();
      for (var i = 0; i < expected.Count; i++) {
        var shape = expected[i]; var actual = unit.Blocks[i];
        Require(actual.Cmds.Count == shape.Commands.Count, "Raw block has inserted or missing ordinary commands", actual.tok);
        for (var j = 0; j < shape.Commands.Count; j++) {
          var command = shape.Commands[j];
          if (command.Original != null) {
            Require(ReferenceEquals(actual.Cmds[j], command.Original),
              "Raw ordinary command identity/order differs from structured source", actual.Cmds[j]?.tok ?? actual.tok);
          } else { MatchGuard(actual.Cmds[j], command.Guard, command.Negated); }
          UniqueCommandTree(actual.Cmds[j], commandIdentities);
        }
        if (shape.OriginalTransfer != null) {
          Require(ReferenceEquals(actual.TransferCmd, shape.OriginalTransfer),
            "Raw explicit transfer is not its structured source object", actual.tok);
        } else {
          Require(ReferenceEquals(actual.TransferCmd switch { Bpl.GotoCmd jump => jump.Attributes, Bpl.ReturnCmd returned => returned.Attributes, _ => null }, shape.TransferAttributes),
            "Generated transfer attributes differ from the pinned producer", actual.TransferCmd.tok);
        }
        if (shape.Targets == null) {
          Require(actual.TransferCmd.GetType() == typeof(Bpl.ReturnCmd),
            "Raw runoff/return topology differs from structured source", actual.TransferCmd.tok);
        } else {
          var names = shape.Targets.Select(target => target()).ToArray();
          Require(actual.TransferCmd is Bpl.GotoCmd jump && jump.GetType() == typeof(Bpl.GotoCmd) &&
            jump.LabelNames != null && jump.LabelTargets != null && names.SequenceEqual(jump.LabelNames) &&
            names.All(byLabel.ContainsKey) && jump.LabelTargets.SequenceEqual(names.Select(name => byLabel[name])),
            "Raw goto names/targets differ from lexical producer topology", actual.TransferCmd.tok);
        }
      }
    }

    private void Visit(object node, int depth, Bpl.IToken token) {
      Require(node != null && depth < Ir.Protocol.MaximumDepth && ++sourceCount <= Ir.Protocol.MaximumNodes && sourceNodes.Add(node),
        "Structured CFG has shared/cyclic/missing nodes or exceeds its bound", token);
    }
    private void CountCommands(IEnumerable<Bpl.Cmd> commands, Bpl.IToken token) {
      var pending = new Stack<(Bpl.Cmd Command, int Depth)>();
      foreach (var command in commands) {
        Require(++commandCount <= Ir.Protocol.MaximumNodes, "Structured command inventory exceeds its bound", token);
        pending.Push((command, 0));
      }
      while (pending.Count > 0) {
        var (command, depth) = pending.Pop();
        Require(command != null && depth < Ir.Protocol.MaximumDepth, "Structured nested command exceeds its bound", token);
        if (command is not Bpl.StateCmd state) { continue; }
        foreach (var child in state.Cmds) {
          Require(++commandCount <= Ir.Protocol.MaximumNodes, "Structured nested command inventory exceeds its bound", token);
          pending.Push((child, depth + 1));
        }
      }
    }
    private void Index(Bpl.StmtList list, Bpl.BigBlock next, IReadOnlyList<Bpl.BigBlock> ancestors, int depth) {
      Visit(list, depth, list?.EndCurly ?? unit.tok);
      Require(list.BigBlocks.Count > 0, "Empty structured list is not a producer shape", list.EndCurly);
      if (list.PrefixCommands != null) { CountCommands(list.PrefixCommands, list.EndCurly); }
      for (var i = list.BigBlocks.Count - 1; i >= 0; i--) {
        var block = list.BigBlocks[i]; Visit(block, depth + 1, block?.tok ?? list.EndCurly);
        Require(block.LabelName != null && block.simpleCmds != null && (block.ec == null || block.tc == null),
          "Structured block has incomplete producer metadata", block.tok);
        successors.Add(block, next); CountCommands(block.simpleCmds, block.tok);
        if (block.ec != null) {
          Visit(block.ec, depth + 2, block.ec.tok);
          var nested = ancestors.Append(block).ToArray();
          switch (block.ec) {
            case Bpl.WhileCmd loop:
              CountCommands(loop.Yields.Cast<Bpl.Cmd>().Concat(loop.Invariants), loop.tok);
              Index(loop.Body, block, nested, depth + 3); break;
            case Bpl.IfCmd conditional:
              var chainDepth = depth;
              for (var current = conditional; current != null; current = current.ElseIf) {
                if (!ReferenceEquals(current, conditional)) { Visit(current, ++chainDepth + 2, current.tok); }
                Require(current.ElseBlock == null || current.ElseIf == null, "Conditional has two else alternatives", current.tok);
                Index(current.Thn, next, nested, chainDepth + 3);
                if (current.ElseBlock != null) { Index(current.ElseBlock, next, nested, chainDepth + 3); }
              }
              break;
            case Bpl.BreakCmd broken:
              var enclosure = ancestors.Reverse().FirstOrDefault(parent => broken.Label == null ? parent.ec is Bpl.WhileCmd : parent.LabelName == broken.Label);
              Require(enclosure != null && (broken.Label == null || enclosure.simpleCmds.Count == 0) &&
                ReferenceEquals(broken.BreakEnclosure, enclosure), "Break enclosure differs from its lexical producer target", broken.tok);
              break;
            default: throw new Rejection("Unknown structured producer shape", block.ec.tok);
          }
        }
        next = block;
      }
    }
    private static Func<string> SourceTarget(Bpl.BigBlock block) => () => block.LabelName;
    private static Func<string> GeneratedTarget(BlockShape block) => () => block.Label;
    private IReadOnlyList<Func<string>> Successor(Bpl.BigBlock block) => successors[block] == null ? null : new[] { SourceTarget(successors[block]) };
    private IReadOnlyList<Func<string>> Ending(Bpl.BigBlock block, bool last, BlockShape runoff) =>
      last && runoff != null ? new[] { GeneratedTarget(runoff) } : Successor(block);
    private static IReadOnlyList<CommandShape> Guards(Bpl.Expr guard, bool negated) =>
      guard == null ? Array.Empty<CommandShape>() : new[] { new CommandShape(null, guard, negated) };
    private static bool Inlined(Bpl.StmtList list, Bpl.Expr guard) => guard == null || list.BigBlocks[0].Anonymous;
    private void Prefix(Bpl.StmtList list, Bpl.Expr guard, bool negated, bool inlined) {
      if (guard == null || !inlined) {
        Require(list.PrefixCommands == null, "Unexpected structured prefix commands", list.EndCurly); return;
      }
      Require(list.PrefixCommands?.Count == 1, "Missing or extra producer guard prefix", list.EndCurly);
      MatchGuard(list.PrefixCommands[0], guard, negated);
    }
    private BlockShape Add(BlockShape shape, string sourceLabel = null) {
      Require(expected.Count < unit.Blocks.Count && expected.Count < 256,
        "Raw CFG has missing producer block", unit.tok);
      var actual = unit.Blocks[expected.Count];
      Require(sourceLabel == null || sourceLabel == actual.Label, "Raw source block order/label differs from the producer", actual.tok);
      shape.Label = actual.Label;
      Require((expectedCount += shape.Commands.Count + 1) <= Ir.Protocol.MaximumNodes &&
        (edgeCount += shape.Targets?.Count ?? 0) <= Ir.Protocol.MaximumNodes,
        "Expected CFG inventory exceeds its bound", actual.tok);
      expected.Add(shape); return shape;
    }
    private void Create(Bpl.StmtList list, BlockShape runoff, Bpl.Expr prefixGuard, bool prefixNegated, bool prefixInlined, int depth) {
      Require(depth < Ir.Protocol.MaximumDepth, "Producer reconstruction exceeds nesting bound", list.EndCurly);
      Prefix(list, prefixGuard, prefixNegated, prefixInlined);
      for (var i = 0; i < list.BigBlocks.Count; i++) {
        var block = list.BigBlocks[i]; var last = i + 1 == list.BigBlocks.Count;
        var commands = (i == 0 ? list.PrefixCommands ?? new List<Bpl.Cmd>() : new List<Bpl.Cmd>())
          .Concat(block.simpleCmds).Select(command => new CommandShape(command)).ToArray();
        if (block.tc != null) {
          var targets = block.tc is Bpl.GotoCmd jump ? jump.LabelNames?.Select<string, Func<string>>(name => () => name).ToArray() : null;
          Require(block.tc.GetType() == typeof(Bpl.ReturnCmd) || block.tc.GetType() == typeof(Bpl.GotoCmd) && targets != null,
            "Unknown explicit transfer producer shape", block.tc.tok);
          Add(new BlockShape { Commands = commands, OriginalTransfer = block.tc, Targets = targets }, block.LabelName);
        } else if (block.ec == null) {
          Add(new BlockShape { Commands = commands, Targets = Ending(block, last, runoff) }, block.LabelName);
        } else if (block.ec is Bpl.BreakCmd broken) {
          Add(new BlockShape { Commands = commands, Targets = Successor(broken.BreakEnclosure) }, block.LabelName);
        } else if (block.ec is Bpl.WhileCmd loop) {
          var head = new BlockShape(); var body = new BlockShape(); var done = new BlockShape();
          var inline = Inlined(loop.Body, loop.Guard);
          Add(new BlockShape { Commands = commands, Targets = new[] { GeneratedTarget(head) } }, block.LabelName);
          head.Commands = loop.Yields.Cast<Bpl.Cmd>().Concat(loop.Invariants).Select(command => new CommandShape(command)).ToArray();
          head.Targets = new[] { GeneratedTarget(done), inline ? SourceTarget(loop.Body.BigBlocks[0]) : GeneratedTarget(body) }; Add(head);
          if (!inline) {
            body.Commands = Guards(loop.Guard, false); body.Targets = new[] { SourceTarget(loop.Body.BigBlocks[0]) }; Add(body);
          }
          Create(loop.Body, head, loop.Guard, false, inline, depth + 1);
          done.Commands = Guards(loop.Guard, true); done.Targets = Ending(block, last, runoff); Add(done);
        } else if (block.ec is Bpl.IfCmd conditional) {
          var predecessor = new BlockShape { Commands = commands };
          var sourceLabel = block.LabelName; var chainDepth = depth;
          for (var current = conditional; current != null; current = current.ElseIf) {
            Require(chainDepth++ < Ir.Protocol.MaximumDepth, "Else-if producer chain exceeds its bound", current.tok);
            var then = new BlockShape(); var other = new BlockShape();
            var inlineThen = Inlined(current.Thn, current.Guard);
            var inlineElse = current.ElseBlock != null && Inlined(current.ElseBlock, current.Guard);
            predecessor.Targets = new[] { inlineThen ? SourceTarget(current.Thn.BigBlocks[0]) : GeneratedTarget(then),
              inlineElse ? SourceTarget(current.ElseBlock.BigBlocks[0]) : GeneratedTarget(other) };
            predecessor.TransferAttributes = current.Attributes; Add(predecessor, sourceLabel); sourceLabel = null;
            if (!inlineThen) {
              then.Commands = Guards(current.Guard, false); then.Targets = new[] { SourceTarget(current.Thn.BigBlocks[0]) }; Add(then);
            }
            Create(current.Thn, last ? runoff : null, current.Guard, false, inlineThen, depth + 1);
            if (current.ElseBlock != null) {
              if (!inlineElse) {
                other.Commands = Guards(current.Guard, true); other.Targets = new[] { SourceTarget(current.ElseBlock.BigBlocks[0]) }; Add(other);
              }
              Create(current.ElseBlock, last ? runoff : null, current.Guard, true, inlineElse, depth + 1);
            } else if (current.ElseIf != null) {
              other.Commands = Guards(current.Guard, true); predecessor = other;
            } else {
              other.Commands = Guards(current.Guard, true); other.Targets = Ending(block, last, runoff); Add(other);
            }
          }
        } else { throw new Rejection("Unknown structured producer shape", block.ec.tok); }
      }
    }
    private static void MatchGuard(Bpl.Cmd command, Bpl.Expr guard, bool negated) {
      Require(command is Bpl.AssumeCmd && command.GetType() == typeof(Bpl.AssumeCmd),
        "Generated guard is not the pinned AssumeCmd shape", command?.tok ?? Bpl.Token.NoToken);
      var assumed = (Bpl.AssumeCmd)command;
      var matches = !negated ? ReferenceEquals(assumed.Expr, guard) : MatchNegation(assumed.Expr, guard);
      Require(matches && assumed.Attributes is { Key: "partition", Params.Count: 0, Next: null },
        "Generated guard expression/partition differs from the pinned producer", command.tok);
    }
    // Expr.Not is a constructor with these pinned simplifications (AbsyExpr.cs:275-333).
    // Match its exact operation/operand identities without invoking it or mutating source expressions.
    private static bool MatchNegation(Bpl.Expr actual, Bpl.Expr guard) {
      if (ReferenceEquals(guard, Bpl.Expr.True)) { return ReferenceEquals(actual, Bpl.Expr.False); }
      if (ReferenceEquals(guard, Bpl.Expr.False)) { return ReferenceEquals(actual, Bpl.Expr.True); }
      if (guard is Bpl.NAryExpr source) {
        if (source.Fun is Bpl.UnaryOperator { Op: Bpl.UnaryOperator.Opcode.Not } && source.Args.Count == 1) {
          return ReferenceEquals(actual, source.Args[0]);
        }
        if (source.Fun is Bpl.BinaryOperator op && source.Args.Count == 2) {
          // Producer Not(Eq/Neq) precedes Typecheck. The shared guard and its
          // complement then rewrite separately: Eq(a,b) -> Iff(a,b), and
          // Neq(a,b) -> Iff(a,Not(b)) (AbsyExpr.cs:2483-2502).
          // With typed Bool operands these two exact forms are complements.
          if (op.Op == Bpl.BinaryOperator.Opcode.Iff &&
              actual is Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.Iff }, Args.Count: 2 } iff &&
              ReferenceEquals(iff.Args[0], source.Args[0])) {
            return DirectNotOf(iff.Args[1], source.Args[1]) || DirectNotOf(source.Args[1], iff.Args[1]);
          }
          var replacement = op.Op switch {
            Bpl.BinaryOperator.Opcode.Eq => Bpl.BinaryOperator.Opcode.Neq,
            Bpl.BinaryOperator.Opcode.Neq => Bpl.BinaryOperator.Opcode.Eq,
            Bpl.BinaryOperator.Opcode.Lt => Bpl.BinaryOperator.Opcode.Le,
            Bpl.BinaryOperator.Opcode.Le => Bpl.BinaryOperator.Opcode.Lt,
            Bpl.BinaryOperator.Opcode.Ge => Bpl.BinaryOperator.Opcode.Gt,
            Bpl.BinaryOperator.Opcode.Gt => Bpl.BinaryOperator.Opcode.Ge,
            _ => (Bpl.BinaryOperator.Opcode?)null
          };
          if (replacement.HasValue) {
            var reverse = op.Op is not (Bpl.BinaryOperator.Opcode.Eq or Bpl.BinaryOperator.Opcode.Neq);
            return actual is Bpl.NAryExpr { Fun: Bpl.BinaryOperator changed, Args.Count: 2 } binary && changed.Op == replacement.Value &&
              ReferenceEquals(binary.Args[0], source.Args[reverse ? 1 : 0]) && ReferenceEquals(binary.Args[1], source.Args[reverse ? 0 : 1]);
          }
        }
      }
      return actual is Bpl.NAryExpr { Fun: Bpl.UnaryOperator unary, Args.Count: 1 } application &&
        unary.Op == Bpl.UnaryOperator.Opcode.Not && ReferenceEquals(application.Args[0], guard);
    }
    private static bool DirectNotOf(Bpl.Expr expression, Bpl.Expr operand) =>
      expression is Bpl.NAryExpr { Fun: Bpl.UnaryOperator { Op: Bpl.UnaryOperator.Opcode.Not }, Args.Count: 1 } negation &&
      ReferenceEquals(negation.Args[0], operand);
    private static void UniqueCommandTree(Bpl.Cmd root, HashSet<Bpl.Cmd> identities) {
      var pending = new Stack<(Bpl.Cmd Command, int Depth)>(); pending.Push((root, 0));
      while (pending.Count > 0) {
        var (command, depth) = pending.Pop();
        Require(command != null && depth < Ir.Protocol.MaximumDepth && identities.Count < Ir.Protocol.MaximumNodes && identities.Add(command),
          "Raw command has ambiguous identity or exceeds the nesting bound", command?.tok ?? Bpl.Token.NoToken);
        if (command is Bpl.StateCmd state) { foreach (var child in state.Cmds) { pending.Push((child, depth + 1)); } }
      }
    }
  }
  private static void Require(bool condition, string message, Bpl.IToken token) {
    if (!condition) { throw new Rejection(message, token); }
  }
}
