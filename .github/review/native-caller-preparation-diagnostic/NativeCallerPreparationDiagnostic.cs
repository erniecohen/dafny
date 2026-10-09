using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;

// Deliberate support omission for causal diagnosis, never a product policy.
public static class NativeCallerPreparationDiagnostic {
  private static readonly List<IReadOnlyList<object>> Omitted = new();
  private static string[] Selection() {
    var value = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_CALLER_PREPARATION");
    if (value == null) { return null; }
    var parts = value.Split(':');
    Require(parts.Length == 2 && new[] { "native-control", "without-caller-preparation", "false-entry-control" }.Contains(parts[0]) &&
      new[] { "Composite", "FormArmy" }.Contains(parts[1]), "Unknown caller preparation diagnostic");
    return parts;
  }
  public static bool Omit(string declaration) {
    var parts = Selection();
    return parts != null && parts[0] != "native-control" && parts[1] == declaration;
  }
  public static void Prepared(IReadOnlyList<object> original, IReadOnlyList<object> normalized,
    ISet<string> argumentTemporaries, bool omit) {
    if (!omit) { return; }
    Require(!ReferenceEquals(original, normalized), "Unsupported WF fragment");
    foreach (var command in normalized) {
      if (command is Bpl.CommentCmd or Bpl.AssumeCmd) { continue; }
      Require(command is Bpl.AssignCmd, "Non-pure preparation or havoc");
      foreach (var lhs in ((Bpl.AssignCmd)command).Lhss) {
        Require(lhs is Bpl.SimpleAssignLhs simple && argumentTemporaries.Contains(simple.AssignedVariable.Name),
          "Non-private preparation write");
      }
    }
    Require(normalized.Count > 0, "Empty omission");
    Omitted.Add(normalized.ToList());
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
    Require(parts[0] == "native-control" ? Omitted.Count == 0 : Omitted.Count > 0,
      "Expected caller preparation omissions");
    Require(Omitted.SelectMany(fragment => fragment).All(cmd => !original.Any(item => ReferenceEquals(item, cmd))),
      "Omitted command is still emitted");
    var actual = checks.Select(cmd => new {
      expression = NativeCallerPreparationFingerprint.Expression(cmd.Expr),
      attributes = NativeCallerPreparationFingerprint.Attributes(cmd.Attributes, new NativeCallerPreparationFingerprint.BindingScope()),
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
      target = parts[1], variant = parts[0], omittedCallerFragments = Omitted.Count,
      omittedCommands = Omitted.Sum(fragment => fragment.Count), completeOriginalPreparationTraversed = true,
      actualCheckFingerprints = actual, allActualCheckObjectsRetained = true,
      allPostTranslationCommandObjectsRetained = true, allBranchTransferObjectsRetained = true,
      negativeEntryCheckAdded = negative != null, actualChecks = checks.Count
    }) + "\n");
  }
  private static void Require(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
}
