using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

// Scratch-only: isolate exit gates while preserving matching declaration checks.
public static class NativeFamilyDiagnostic {
  private static readonly object Gate = new();
  private static readonly Dictionary<string, int> Gates = new();
  private static string[] Selection() {
    var value = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_FAMILY");
    if (value == null) { return null; }
    var parts = value.Split(':');
    Require(parts.Length == 2 &&
      new[] { "native-control", "off-control", "exit-only", "body-only", "false-entry-exit", "false-entry-body" }.Contains(parts[0]) &&
      new[] { "Composite", "FormArmy", "RemoveFactor" }.Contains(parts[1]), "Unknown source-clause diagnostic");
    return parts;
  }

  public static bool Enabled(string family, bool enabled) {
    var parts = Selection();
    if (parts == null || parts[0] == "native-control") { return enabled; }
    Require(family is "exit" or "other", "Unknown translation family");
    lock (Gate) { Gates[family] = Gates.GetValueOrDefault(family) + 1; }
    return parts[0] switch {
      "off-control" => false,
      "exit-only" or "false-entry-exit" => enabled && family == "exit",
      "body-only" or "false-entry-body" => enabled && family == "other",
      _ => throw new InvalidOperationException("Unknown native family variant")
    };
  }

  public static void Apply(Bpl.Program program) {
    var parts = Selection();
    if (parts == null) { return; }
    var implementations = program.TopLevelDeclarations.OfType<Bpl.Implementation>()
      .Where(impl => impl.Name == "Impl$$_module.__default." + parts[1]).ToList();
    if (implementations.Count == 0) { return; }
    Require(implementations.Count == 1, "Ambiguous diagnostic implementation");
    var impl = implementations[0];
    // Translation has not run resolution yet, so Implementation.Proc is unset.
    var procedures = program.TopLevelDeclarations.OfType<Bpl.Procedure>().Where(p => p.Name == impl.Name).ToList();
    Require(procedures.Count == 1, "Missing or ambiguous implementation procedure");
    var procedure = procedures.Single();
    var original = impl.Blocks.SelectMany(block => block.Cmds).ToList();
    var checks = original.OfType<Bpl.AssertCmd>().ToList();
    var transfers = impl.Blocks.Select(block => block.TransferCmd).ToList();
    Dictionary<string, int> gates;
    lock (Gate) { gates = new(Gates); }
    Require(parts[0] == "native-control" || gates.GetValueOrDefault("exit") > 0,
      "No exit-family decision observed");
    var actual = checks.Select(cmd => new {
      expression = NativeFamilyFingerprint.Expression(cmd.Expr),
      attributes = NativeFamilyFingerprint.Attributes(cmd.Attributes, new NativeFamilyFingerprint.BindingScope()),
      line = cmd.tok.line, col = cmd.tok.col
    }).ToArray();
    Bpl.AssertCmd negative = null;
    if (parts[0].StartsWith("false-entry-")) {
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
      target = parts[1], variant = parts[0], familyGateVisits = gates,
      checkedProcedureEnsures = procedure.Ensures.Count(e => !e.Free),
      freeProcedureEnsures = procedure.Ensures.Count(e => e.Free),
      checkedProcedurePostconditionExpressions = procedure.Ensures.Where(e => !e.Free).Select(e => NativeFamilyFingerprint.Expression(e.Condition)).ToArray(),
      actualCheckFingerprints = actual, allActualCheckObjectsRetained = true,
      allPostTranslationCommandObjectsRetained = true, allBranchTransferObjectsRetained = true,
      negativeEntryCheckAdded = negative != null, actualChecks = checks.Count
    }) + "\n");
  }
  private static void Require(bool condition, string message) {
    if (!condition) { throw new InvalidOperationException(message); }
  }
}
