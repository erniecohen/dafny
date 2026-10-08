using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

// Scratch-only diagnostic, linked into the driver after the pristine build.
// It operates on native blocks; no Boogie printing/parsing is used as input.
internal static class NativeSupportOrderDiagnostic {
  internal static void Apply(Bpl.Program program) {
    var selection = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_SUPPORT_ORDER");
    if (selection == null) { return; }
    var parts = selection.Split(':');
    if (parts.Length != 2 || !new[] { "native-control", "move-prefix", "false-entry-control" }.Contains(parts[0]) ||
        !new[] { "Composite", "RemoveFactor" }.Contains(parts[1])) {
      throw new InvalidOperationException("Unknown native diagnostic selection");
    }
    var implementations = program.TopLevelDeclarations.OfType<Bpl.Implementation>()
      .Where(impl => impl.Name == "Impl$$_module.__default." + parts[1]).ToList();
    if (implementations.Count == 0) { return; }
    Require(implementations.Count == 1, "Ambiguous implementation");
    var impl = implementations[0];
    var before = impl.Blocks.SelectMany(block => block.Cmds).ToList();
    var checks = before.OfType<Bpl.AssertCmd>().ToList();
    var transfers = impl.Blocks.Select(block => block.TransferCmd).ToList();
    var argument = parts[1] == "Composite" ? "##n#3" : "##s#3";
    var function = "_module.__default." + (parts[1] == "Composite" ? "IsPrime" : "product") + "#canCall";
    var locations = impl.Blocks.SelectMany(block => block.Cmds.Select((cmd, index) => (block, cmd, index)))
      .Where(location => location.cmd is Bpl.AssignCmd assignment && assignment.Lhss.Count == 1 &&
        assignment.Lhss[0] is Bpl.SimpleAssignLhs lhs && lhs.AssignedVariable.Name == argument).ToList();
    Require(locations.Count == 1, "Expected unique final private argument");
    var location = locations[0];
    var commands = location.block.Cmds;
    var prefix = commands.FindLastIndex(location.index - 1, cmd =>
      cmd is Bpl.AssumeCmd { Expr: Bpl.NAryExpr { Fun: Bpl.FunctionCall call } } && call.FunctionName == function);
    var check = commands.FindIndex(location.index, cmd => cmd is Bpl.AssertCmd);
    Require(prefix >= 0 && check > location.index, "Expected local support and actual check");
    var moved = commands[prefix];
    var span = commands.Skip(prefix + 1).Take(check - prefix - 1).ToList();
    var writes = new HashSet<string>();
    foreach (var cmd in span) {
      if (cmd is Bpl.AssumeCmd or Bpl.CommentCmd) { continue; }
      Require(cmd is Bpl.AssignCmd, "Preparation is not a pure linear fragment");
      foreach (var lhs in ((Bpl.AssignCmd)cmd).Lhss) {
        Require(lhs is Bpl.SimpleAssignLhs, "Non-private assignment");
        var name = ((Bpl.SimpleAssignLhs)lhs).AssignedVariable.Name;
        Require(name.StartsWith("##", StringComparison.Ordinal) && writes.Add(name), "Non-unique private assignment");
      }
    }
    var names = new Names();
    names.VisitExpr(((Bpl.AssumeCmd)moved).Expr);
    Require(!writes.Any(names.Identifiers.Contains), "Support reads a moved private binding");
    Bpl.AssertCmd negative = null;
    if (parts[0] == "move-prefix") {
      commands.RemoveAt(prefix);
      commands.Insert(check - 1, moved);
    } else if (parts[0] == "false-entry-control") {
      negative = new Bpl.AssertCmd(impl.tok, Bpl.Expr.False);
      impl.Blocks[0].Cmds.Insert(0, negative);
    }
    var after = impl.Blocks.SelectMany(block => block.Cmds).Where(cmd => !ReferenceEquals(cmd, negative)).ToList();
    Require(checks.SequenceEqual(after.OfType<Bpl.AssertCmd>(), ReferenceEqualityComparer.Instance), "Actual checks changed");
    Require(transfers.SequenceEqual(impl.Blocks.Select(block => block.TransferCmd), ReferenceEqualityComparer.Instance), "Branch metadata changed");
    var counts = new Dictionary<Bpl.Cmd, int>(ReferenceEqualityComparer.Instance);
    foreach (var cmd in before) { counts[cmd] = counts.GetValueOrDefault(cmd) + 1; }
    foreach (var cmd in after) {
      Require(counts.ContainsKey(cmd) && counts[cmd] > 0, "A command was replaced");
      counts[cmd]--;
    }
    Require(counts.Values.All(count => count == 0), "A command was lost");
    var auditPath = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");
    Require(!string.IsNullOrEmpty(auditPath), "Missing diagnostic audit destination");
    Require(!File.Exists(auditPath), "Diagnostic selected more than one module");
    File.WriteAllText(auditPath, JsonSerializer.Serialize(new {
      target = parts[1], variant = parts[0], originalCommands = before.Count,
      actualChecks = checks.Count, supportFunction = function, privateArgument = argument,
      allOriginalCommandObjectsRetained = true, allActualCheckObjectsRetained = true,
      allBranchTransferObjectsRetained = true, negativeEntryCheckAdded = negative != null
    }) + "\n");
  }

  private static void Require(bool condition, string message) {
    if (!condition) { throw new InvalidOperationException(message); }
  }
  private sealed class Names : Bpl.ReadOnlyVisitor {
    internal readonly HashSet<string> Identifiers = new();
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) {
      Identifiers.Add(node.Name);
      return node;
    }
  }
}
