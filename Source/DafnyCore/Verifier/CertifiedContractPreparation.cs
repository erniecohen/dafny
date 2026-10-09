using System.Collections.Generic;
using System.Linq;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

/// <summary>
/// Normalize only a newly generated, certified contract-WF fragment. Source
/// commands and the actual proposition check are never inputs to this helper.
/// </summary>
internal static class CertifiedContractPreparation {
  internal static IReadOnlyList<object> Normalize(IReadOnlyList<object> commands,
    ISet<string> argumentTemporaries) {
    var writes = new Dictionary<string, int>();
    var guards = new List<Bpl.Expr>();
    if (!Eligible(commands, false, argumentTemporaries, writes, guards) ||
        writes.Values.Any(count => count != 1)) {
      return commands;
    }
    // A guard must keep the value it had on entry to its branch. Argument
    // temporaries have unique writes, but need not have resolved declarations.
    var guardNames = new IdentifierNames();
    foreach (var guard in guards) { guardNames.VisitExpr(guard); }
    if (writes.Keys.Any(guardNames.Names.Contains)) { return commands; }
    var result = new List<object>();
    Flatten(commands, [], result);
    return result;
  }

  internal static bool CanScope(IReadOnlyList<object> normalized, ISet<string> argumentTemporaries) {
    foreach (var command in normalized) {
      switch (command) {
        case Bpl.CommentCmd or Bpl.AssumeCmd:
          break;
        case Bpl.AssignCmd assignment:
          if (assignment.Lhss.Any(lhs => lhs is not Bpl.SimpleAssignLhs simple ||
              !argumentTemporaries.Contains(simple.AssignedVariable.Name))) { return false; }
          break;
        default:
          // In particular, do not hide havocs, calls or reveal/hide commands.
          return false;
      }
    }
    return true;
  }

  internal static bool CanMoveLeadingSupport(Bpl.AssumeCmd support,
    IReadOnlyList<object> normalized, ISet<string> argumentTemporaries) {
    var reads = new IdentifierNames();
    reads.VisitExpr(support.Expr);
    foreach (var command in normalized) {
      switch (command) {
        case Bpl.CommentCmd or Bpl.AssumeCmd:
          break;
        case Bpl.HavocCmd havoc:
          if (havoc.Vars.Any(variable => reads.Names.Contains(variable.Name))) { return false; }
          break;
        case Bpl.AssignCmd assignment:
          foreach (var lhs in assignment.Lhss) {
            if (lhs is not Bpl.SimpleAssignLhs simple ||
                !argumentTemporaries.Contains(simple.AssignedVariable.Name) ||
                reads.Names.Contains(simple.AssignedVariable.Name)) { return false; }
          }
          break;
        default:
          return false;
      }
    }
    return true;
  }

  private static bool Eligible(IReadOnlyList<object> commands, bool conditional,
    ISet<string> argumentTemporaries, Dictionary<string, int> writes, List<Bpl.Expr> guards) {
    foreach (var command in commands) {
      switch (command) {
        case Bpl.CommentCmd or Bpl.AssumeCmd:
          break;
        case Bpl.HavocCmd when !conditional:
          break;
        case Bpl.AssignCmd assignment:
          foreach (var lhs in assignment.Lhss) {
            if (lhs is not Bpl.SimpleAssignLhs simple ||
                !argumentTemporaries.Contains(simple.AssignedVariable.Name)) { return false; }
            var name = simple.AssignedVariable.Name;
            writes[name] = writes.GetValueOrDefault(name) + 1;
          }
          break;
        case Bpl.IfCmd branch when branch.Guard != null && branch.Attributes == null &&
            branch.ElseIf == null && branch.ElseBlock == null:
          var body = Commands(branch.Thn);
          if (body == null || !Eligible(body, true, argumentTemporaries, writes, guards)) { return false; }
          guards.Add(branch.Guard);
          break;
        default:
          // Calls, real assertions, heap changes, nondeterministic branches,
          // scope changes and labels retain the original statement translation.
          return false;
      }
    }
    return true;
  }

  private static List<object> Commands(Bpl.StmtList statements) {
    if (statements.PrefixCommands is { Count: > 0 }) { return null; }
    var result = new List<object>();
    foreach (var block in statements.BigBlocks) {
      if (!block.Anonymous || block.tc != null) { return null; }
      result.AddRange(block.simpleCmds);
      if (block.ec != null) { result.Add(block.ec); }
    }
    return result;
  }

  private static Bpl.Expr Guard(IReadOnlyList<Bpl.Expr> guards) =>
    guards.Aggregate((left, right) => Bpl.Expr.And(left, right));

  private static void Flatten(IReadOnlyList<object> commands, IReadOnlyList<Bpl.Expr> guards,
    List<object> result) {
    foreach (var command in commands) {
      if (command is Bpl.IfCmd branch) {
        var nestedGuards = guards.Append(branch.Guard).ToList();
        var body = Commands(branch.Thn);
        if (body.All(item => item is Bpl.CommentCmd)) {
          // A certified empty branch imposes no condition. Retain its guard
          // expression and terms in a tautology without introducing a CFG split.
          result.AddRange(body);
          var guard = Guard(nestedGuards);
          result.Add(new Bpl.AssumeCmd(branch.tok, Bpl.Expr.Imp(guard, guard)));
        } else {
          Flatten(body, nestedGuards, result);
        }
      } else if (command is Bpl.AssumeCmd assumption && guards.Count != 0) {
        result.Add(new Bpl.AssumeCmd(assumption.tok,
          Bpl.Expr.Imp(Guard(guards), assumption.Expr), assumption.Attributes));
      } else {
        // Only private, fresh function-WF argument assignments can move out
        // of a branch. Their unique bindings never change source state, and
        // every fact that uses them retains the original branch guards.
        result.Add(command);
      }
    }
  }

  private sealed class IdentifierNames : Bpl.ReadOnlyVisitor {
    internal HashSet<string> Names { get; } = [];
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) {
      Names.Add(node.Name);
      return node;
    }
  }
}
