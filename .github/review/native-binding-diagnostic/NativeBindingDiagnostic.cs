using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public static class NativeBindingDiagnostic {
  private static readonly object Gate = new();
  private static readonly HashSet<string> Eliminated = new();
  private static int Fragments;
  private static string[] Selection() {
    var value = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_BINDING");
    if (value == null) { return null; }
    var parts = value.Split(':');
    Require(parts.Length == 2 &&
      new[] { "native-control", "inline-bindings", "false-entry-control" }.Contains(parts[0]) &&
      new[] { "Composite", "FormArmy", "RemoveFactor" }.Contains(parts[1]), "Unknown binding diagnostic");
    return parts;
  }

  // Inputs are only an accepted, newly generated contract-WF fragment.
  public static IReadOnlyList<object> Rewrite(string declaration,
    IReadOnlyList<object> commands, ISet<string> argumentTemporaries) {
    var parts = Selection();
    if (parts == null || parts[0] == "native-control" || parts[1] != declaration) { return commands; }
    var defined = new HashSet<string>();
    foreach (var command in commands) {
      switch (command) {
        case Bpl.CommentCmd: break;
        case Bpl.AssignCmd assignment when assignment.Lhss.Count == 1 && assignment.Rhss.Count == 1 &&
            assignment.Lhss[0] is Bpl.SimpleAssignLhs simple:
          var name = simple.AssignedVariable.Name;
          if (!argumentTemporaries.Contains(name) || defined.Contains(name) ||
              !Ground(assignment.Rhss[0], argumentTemporaries, defined)) { return commands; }
          defined.Add(name); break;
        case Bpl.AssumeCmd assumption:
          if (!Ground(assumption.Expr, argumentTemporaries, defined)) { return commands; }
          break;
        default: return commands;
      }
    }
    if (defined.Count == 0) { return commands; }
    var values = new Dictionary<string, Bpl.Expr>();
    var result = new List<object>();
    foreach (var command in commands) {
      if (command is Bpl.AssignCmd assignment) {
        var name = ((Bpl.SimpleAssignLhs)assignment.Lhss[0]).AssignedVariable.Name;
        values.Add(name, new Replace(values).VisitExpr(assignment.Rhss[0]));
      } else if (command is Bpl.AssumeCmd assumption) {
        result.Add(new Replace(values).VisitAssumeCmd(assumption));
      } else { result.Add(command); }
    }
    Require(values.Count == defined.Count, "Incomplete argument substitution");
    lock (Gate) {
      Require(!Eliminated.Overlaps(defined), "Fresh argument binding reused across fragments");
      Eliminated.UnionWith(defined); Fragments++;
    }
    return result;
  }

  private static bool Ground(Bpl.Expr expression, ISet<string> marked, ISet<string> defined) => expression switch {
    Bpl.IdentifierExpr id => !marked.Contains(id.Name) || defined.Contains(id.Name),
    Bpl.LiteralExpr => true,
    Bpl.NAryExpr application => application.Args.All(child => Ground(child, marked, defined)),
    _ => false // Reject binders, old, code expressions and unsupported encodings.
  };

  private sealed class Replace(Dictionary<string, Bpl.Expr> values) : Bpl.Duplicator {
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) =>
      values.TryGetValue(node.Name, out var value) ? new Bpl.Duplicator().VisitExpr(value) : base.VisitIdentifierExpr(node);
  }
  private sealed class Uses : Bpl.ReadOnlyVisitor {
    internal readonly HashSet<string> Names = new();
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) {
      Names.Add(node.Name); return node;
    }
  }

  public static void Apply(Bpl.Program program) {
    var parts = Selection(); if (parts == null) { return; }
    var implementations = program.TopLevelDeclarations.OfType<Bpl.Implementation>()
      .Where(impl => impl.Name == "Impl$$_module.__default." + parts[1]).ToList();
    if (implementations.Count == 0) { return; }
    Require(implementations.Count == 1, "Ambiguous diagnostic implementation");
    var impl = implementations[0];
    var original = impl.Blocks.SelectMany(block => block.Cmds).ToList();
    var checks = original.OfType<Bpl.AssertCmd>().ToList();
    var transfers = impl.Blocks.Select(block => block.TransferCmd).ToList();
    string[] eliminated; int fragments;
    lock (Gate) { eliminated = Eliminated.Order().ToArray(); fragments = Fragments; }
    Require(parts[0] == "native-control" ? eliminated.Length == 0 : eliminated.Length > 0,
      "Expected certified private argument bindings");
    var uses = new Uses(); uses.VisitImplementation(impl);
    Require(!eliminated.Any(uses.Names.Contains), "Eliminated binding escapes fresh local preparation");
    Require(impl.LocVars.Where(v => eliminated.Contains(v.Name)).All(v => v.TypedIdent.WhereExpr == null),
      "Eliminated variable has an implicit where clause");
    var actual = checks.Select(cmd => new {
      expression = NativeBindingFingerprint.Expression(cmd.Expr),
      attributes = NativeBindingFingerprint.Attributes(cmd.Attributes, new NativeBindingFingerprint.BindingScope()),
      line = cmd.tok.line, col = cmd.tok.col
    }).ToArray();
    Bpl.AssertCmd negative = null;
    if (parts[0] == "false-entry-control") {
      negative = new Bpl.AssertCmd(impl.tok, Bpl.Expr.False); impl.Blocks[0].Cmds.Insert(0, negative);
    }
    var after = impl.Blocks.SelectMany(block => block.Cmds).Where(cmd => !ReferenceEquals(cmd, negative)).ToList();
    Require(original.SequenceEqual(after, ReferenceEqualityComparer.Instance), "Post-translation commands changed");
    Require(checks.SequenceEqual(after.OfType<Bpl.AssertCmd>(), ReferenceEqualityComparer.Instance), "Actual checks changed");
    Require(transfers.SequenceEqual(impl.Blocks.Select(block => block.TransferCmd), ReferenceEqualityComparer.Instance), "Transfers changed");
    var path = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");
    Require(!string.IsNullOrEmpty(path) && !File.Exists(path), "Missing or repeated audit destination");
    File.WriteAllText(path, JsonSerializer.Serialize(new {
      target = parts[1], variant = parts[0], inlinedArgumentBindings = eliminated.Length,
      inlinedFragments = fragments, allEliminatedBindingsHaveNoUses = true,
      actualCheckFingerprints = actual, allActualCheckObjectsRetained = true,
      allPostTranslationCommandObjectsRetained = true, allBranchTransferObjectsRetained = true,
      negativeEntryCheckAdded = negative != null, actualChecks = checks.Count
    }) + "\n");
  }
  private static void Require(bool condition, string message) {
    if (!condition) { throw new InvalidOperationException(message); }
  }
}
