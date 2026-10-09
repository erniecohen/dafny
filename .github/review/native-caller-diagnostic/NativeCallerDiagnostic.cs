using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public static class NativeCallerDiagnostic {
  private static readonly HashSet<Bpl.AssumeCmd> Supports = new(ReferenceEqualityComparer.Instance);
  private static readonly HashSet<Bpl.AssumeCmd> Moved = new(ReferenceEqualityComparer.Instance);
  private static string[] Selection() {
    var value = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_CALLER");
    if (value == null) { return null; }
    var parts = value.Split(':');
    Require(parts.Length == 2 &&
      new[] { "native-control", "ordered-caller", "false-entry-control" }.Contains(parts[0]) &&
      new[] { "Composite", "FormArmy" }.Contains(parts[1]), "Unknown caller diagnostic");
    return parts;
  }
  public static bool Order(string declaration, Bpl.AssumeCmd support) {
    var parts = Selection();
    if (parts == null || parts[0] == "native-control" || parts[1] != declaration) { return false; }
    Require(Supports.Add(support), "Repeated caller support"); return true;
  }
  public static void Prepared(Bpl.AssumeCmd support, bool moved) {
    if (support != null && Supports.Contains(support) && moved) { Moved.Add(support); }
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
    Require(parts[0] == "native-control" ? Supports.Count == 0 : Moved.Count > 0,
      "Expected certified caller support movement");
    Require(Supports.All(support => original.Count(cmd => ReferenceEquals(cmd, support)) == 1),
      "Caller support deleted or duplicated");
    var actual = checks.Select(cmd => new {
      expression = NativeCallerFingerprint.Expression(cmd.Expr),
      attributes = NativeCallerFingerprint.Attributes(cmd.Attributes, new NativeCallerFingerprint.BindingScope()),
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
      target = parts[1], variant = parts[0], reorderedCallerSupport = Moved.Count,
      callerSupportsOffered = Supports.Count,
      allOriginalCallerSupportObjectsRetainedOnce = true,
      movedSupportFingerprints = Moved.Select(support => NativeCallerFingerprint.Expression(support.Expr)).ToArray(),
      actualCheckFingerprints = actual, allActualCheckObjectsRetained = true,
      allPostTranslationCommandObjectsRetained = true, allBranchTransferObjectsRetained = true,
      negativeEntryCheckAdded = negative != null, actualChecks = checks.Count
    }) + "\n");
  }
  private static void Require(bool condition, string message) {
    if (!condition) { throw new InvalidOperationException(message); }
  }
}
