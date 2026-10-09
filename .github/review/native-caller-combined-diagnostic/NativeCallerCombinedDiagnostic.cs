using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;

// Deliberate support omission for causal diagnosis, never a product policy.
public static class NativeCallerCombinedDiagnostic {
  private static readonly List<IReadOnlyList<object>> Omitted = new();
  private static string[] Selection() {
    var value = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_CALLER_COMBINED");
    if (value == null) { return null; }
    var parts = value.Split(':');
    Require(parts.Length == 2 && new[] { "native-control", "without-caller-preparation", "without-preparation-and-summary", "without-preparation-summary-and-leading", "false-entry-control" }.Contains(parts[0]) &&
      parts[1] == "FormArmy", "Unknown caller preparation diagnostic");
    return parts;
  }
  private static readonly List<(Bpl.AssumeCmd Command, bool Omitted)> Leading = new();
  private static readonly List<(Bpl.AssumeCmd Command, bool Omitted)> Summaries = new();
  public static bool EmitLeading(string declaration, Bpl.AssumeCmd command) {
    var parts = Selection(); if (parts == null || declaration != parts[1]) { return true; }
    var omit = parts[0] is "without-preparation-summary-and-leading" or "false-entry-control";
    Leading.Add((command, omit)); return !omit;
  }
  public static bool EmitSummary(string declaration, Bpl.AssumeCmd command) {
    var parts = Selection(); if (parts == null || declaration != parts[1]) { return true; }
    var omit = parts[0] is "without-preparation-and-summary" or "without-preparation-summary-and-leading" or "false-entry-control";
    Summaries.Add((command, omit)); return !omit;
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
    Require(Leading.Count == 4 && Summaries.Count == 2, "Unexpected caller emission scope");
    Require(Leading.Concat(Summaries).All(item => original.Any(command => ReferenceEquals(item.Command, command)) == !item.Omitted), "Emission differs from audited objects");
    var actual = checks.Select(cmd => new {
      expression = NativeCallerCombinedFingerprint.Expression(cmd.Expr),
      attributes = NativeCallerCombinedFingerprint.Attributes(cmd.Attributes, new NativeCallerCombinedFingerprint.BindingScope()),
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
      omittedSummaries = Summaries.Count(item => item.Omitted), omittedLeading = Leading.Count(item => item.Omitted),
      allOriginalSummaryConstructionRetained = true, allOriginalLeadingConstructionRetained = true,
      summaryFingerprints = Summaries.Select(item => new { omitted = item.Omitted,
        expression = NativeCallerCombinedFingerprint.Expression(item.Command.Expr),
        attributes = NativeCallerCombinedFingerprint.Attributes(item.Command.Attributes, new NativeCallerCombinedFingerprint.BindingScope()) }).ToArray(),
      leadingFingerprints = Leading.Select(item => new { omitted = item.Omitted,
        expression = NativeCallerCombinedFingerprint.Expression(item.Command.Expr),
        attributes = NativeCallerCombinedFingerprint.Attributes(item.Command.Attributes, new NativeCallerCombinedFingerprint.BindingScope()) }).ToArray(),
      actualCheckFingerprints = actual, allActualCheckObjectsRetained = true,
      allPostTranslationCommandObjectsRetained = true, allBranchTransferObjectsRetained = true,
      negativeEntryCheckAdded = negative != null, actualChecks = checks.Count
    }) + "\n");
  }
  private static void Require(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
}
