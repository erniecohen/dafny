using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

// Scratch-only: inspect the fresh certified local fragment, never body terms.
public static class NativeDuplicateSupportDiagnostic {
  private static readonly object Gate = new();
  private static readonly List<Bpl.AssumeCmd> Retained = new();

  private static string[] Selection() {
    var value = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_DUPLICATE");
    if (value == null) { return null; }
    var parts = value.Split(':');
    Require(parts.Length == 2 &&
      new[] { "native-control", "omit-duplicate", "false-entry-control" }.Contains(parts[0]) &&
      new[] { "Composite", "RemoveFactor", "FormArmy" }.Contains(parts[1]), "Unknown duplicate-support diagnostic");
    return parts;
  }

  public static bool OmitDuplicate(string declaration, Bpl.AssumeCmd leading,
    IReadOnlyList<object> certifiedPreparation) {
    var parts = Selection();
    if (parts == null || parts[0] == "native-control" || parts[1] != declaration) { return false; }
    // Called only where the original support already commutes with accepted
    // preparation. Elide one adjacent identical unattributed assumption.
    // Unsupported expressions keep the original command; no general rewriting.
    if (leading.Attributes != null || certifiedPreparation.LastOrDefault() is not Bpl.AssumeCmd previous ||
        previous.Attributes != null || !Same(previous.Expr, leading.Expr)) { return false; }
    lock (Gate) { Retained.Add(previous); }
    return true;
  }

  private static bool Same(Bpl.Expr left, Bpl.Expr right) {
    if (ReferenceEquals(left, right)) { return true; }
    if (left == null || right == null || !Equals(left.Type, right.Type)) { return false; }
    if (left is Bpl.IdentifierExpr a && right is Bpl.IdentifierExpr b) {
      return a.Name == b.Name && ReferenceEquals(a.Decl, b.Decl);
    }
    if (left is Bpl.LiteralExpr x && right is Bpl.LiteralExpr y) { return x.Equals(y); }
    if (left is not Bpl.NAryExpr n || right is not Bpl.NAryExpr m || n.Args.Count != m.Args.Count) { return false; }
    bool sameOperator;
    if (n.Fun is Bpl.FunctionCall f && m.Fun is Bpl.FunctionCall g) {
      // Unresolved Boogie FunctionCall.Equals compares null Func references.
      // Include the function name, so distinct unresolved calls never match.
      sameOperator = f.FunctionName == g.FunctionName && ReferenceEquals(f.Func, g.Func);
    } else if (n.Fun is Bpl.BinaryOperator && m.Fun is Bpl.BinaryOperator ||
               n.Fun is Bpl.UnaryOperator && m.Fun is Bpl.UnaryOperator ||
               n.Fun is Bpl.TypeCoercion && m.Fun is Bpl.TypeCoercion) {
      sameOperator = n.Fun.Equals(m.Fun);
    } else { return false; }
    return sameOperator && n.Args.Zip(m.Args).All(pair => Same(pair.First, pair.Second));
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
    List<Bpl.AssumeCmd> retained;
    lock (Gate) { retained = Retained.ToList(); }
    Require(parts[0] == "native-control" ? retained.Count == 0 : retained.Count > 0,
      "Expected fresh local duplicate support");
    Require(retained.All(cmd => original.Contains(cmd)), "Identical support command was not retained");
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
      target = parts[1], variant = parts[0], omittedLeadingDuplicates = retained.Count,
      allMatchingSupportCommandsRetained = true, allActualCheckObjectsRetained = true,
      allPostTranslationCommandObjectsRetained = true, allBranchTransferObjectsRetained = true,
      negativeEntryCheckAdded = negative != null, actualChecks = checks.Count
    }) + "\n");
  }

  private static void Require(bool condition, string message) {
    if (!condition) { throw new InvalidOperationException(message); }
  }
}
