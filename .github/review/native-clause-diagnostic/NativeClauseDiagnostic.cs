using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

// Scratch-only selector: test the general source-clause boundary at two targets.
public static class NativeClauseDiagnostic {
  private static readonly object Gate = new();
  private static readonly List<int[]> Selections = new();
  private static string[] Selection() {
    var value = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_CLAUSE");
    if (value == null) { return null; }
    var parts = value.Split(':');
    Require(parts.Length == 2 &&
      new[] { "native-control", "whole-clause", "false-entry-control" }.Contains(parts[0]) &&
      new[] { "Composite", "FormArmy" }.Contains(parts[1]), "Unknown source-clause diagnostic");
    return parts;
  }

  public static IEnumerable<AttributedExpression> Select(string declaration,
    List<AttributedExpression> clauses) {
    var expanded = BoogieGenerator.ConjunctsOf(clauses).ToList();
    var parts = Selection();
    if (parts == null || parts[1] != declaration) { return expanded; }
    lock (Gate) { Selections.Add(new[] { clauses.Count, expanded.Count }); }
    return parts[0] == "native-control" ? expanded : clauses;
  }

  public static void Apply(Bpl.Program program) {
    var parts = Selection();
    if (parts == null) { return; }
    var implementations = program.TopLevelDeclarations.OfType<Bpl.Implementation>()
      .Where(impl => impl.Name == "Impl$$_module.__default." + parts[1]).ToList();
    if (implementations.Count == 0) { return; }
    Require(implementations.Count == 1, "Ambiguous diagnostic implementation");
    var impl = implementations[0];
    var original = impl.Blocks.SelectMany(block => block.Cmds).ToList();
    var checks = original.OfType<Bpl.AssertCmd>().ToList();
    var transfers = impl.Blocks.Select(block => block.TransferCmd).ToList();
    int[][] selections;
    lock (Gate) { selections = Selections.ToArray(); }
    Require(selections.Length > 0 && selections.All(pair => pair[0] < pair[1]),
      "Expected source clause conjunctions at local exit boundary");
    var actual = checks.Select(cmd => new {
      expression = NativeClauseFingerprint.Expression(cmd.Expr),
      attributes = NativeClauseFingerprint.Attributes(cmd.Attributes, new NativeClauseFingerprint.BindingScope()),
      line = cmd.tok.line, col = cmd.tok.col
    }).ToArray();
    Bpl.AssertCmd negative = null;
    if (parts[0] == "false-entry-control") {
      negative = new Bpl.AssertCmd(impl.tok, Bpl.Expr.False);
      impl.Blocks[0].Cmds.Insert(0, negative);
    }
    var after = impl.Blocks.SelectMany(block => block.Cmds).Where(cmd => !ReferenceEquals(cmd, negative)).ToList();
    Require(original.SequenceEqual(after, ReferenceEqualityComparer.Instance), "Post-translation commands changed");
    Require(checks.SequenceEqual(after.OfType<Bpl.AssertCmd>(), ReferenceEqualityComparer.Instance), "Actual checks changed");
    Require(transfers.SequenceEqual(impl.Blocks.Select(block => block.TransferCmd), ReferenceEqualityComparer.Instance), "Transfers changed");
    var path = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");
    Require(!string.IsNullOrEmpty(path) && !File.Exists(path), "Missing or repeated audit destination");
    File.WriteAllText(path, JsonSerializer.Serialize(new {
      target = parts[1], variant = parts[0], sourceAndExpandedClauseCounts = selections,
      actualCheckFingerprints = actual, allActualCheckObjectsRetained = true,
      allPostTranslationCommandObjectsRetained = true, allBranchTransferObjectsRetained = true,
      negativeEntryCheckAdded = negative != null, actualChecks = checks.Count
    }) + "\n");
  }
  private static void Require(bool condition, string message) {
    if (!condition) { throw new InvalidOperationException(message); }
  }
}
