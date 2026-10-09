using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;

// Scratch-only: retain preparation, restore the sole procedure-requires check.
public static class NativeCallerPlacementDiagnostic {
  private sealed record Site(string Declaration, Bpl.CallCmd Call, Bpl.AssertCmd Check);
  private static readonly List<Site> Sites = new();
  private static readonly List<(string Declaration, Bpl.AssumeCmd Summary)> Summaries = new();
  private static string[] Selection() {
    var value = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_CALLER_PLACEMENT");
    if (value == null) { return null; }
    var parts = value.Split(':');
    Require(parts.Length == 2 && new[] { "native-control", "off-control", "preparation-legacy-check", "false-entry-control" }.Contains(parts[0]) && parts[1] == "FormArmy", "Unknown placement diagnostic");
    return parts;
  }
  private static bool Hybrid() => Selection() is { } parts && parts[0] is "preparation-legacy-check" or "false-entry-control";
  public static bool Enabled(string family, bool enabled) {
    Require(family is "prepare" or "check" or "other", "Unknown family");
    return Hybrid() ? enabled && family == "prepare" : enabled;
  }
  public static bool EmitCheck(string declaration, Bpl.CallCmd call, Bpl.AssertCmd check) {
    Sites.Add(new(declaration, call, check)); return !Hybrid();
  }
  public static bool EmitSummary(string declaration, Bpl.AssumeCmd summary) {
    Summaries.Add((declaration, summary)); return !Hybrid();
  }
  public static void Apply(Bpl.Program program) {
    var parts = Selection(); if (parts == null) { return; }
    var impls = program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(impl => impl.Name == "Impl$$_module.__default." + parts[1]).ToList();
    if (impls.Count == 0) { return; }
    Require(impls.Count == 1, "Ambiguous implementation"); var impl = impls.Single();
    var original = impl.Blocks.SelectMany(block => block.Cmds).ToList();
    var checks = original.OfType<Bpl.AssertCmd>().ToList();
    var transfers = impl.Blocks.Select(block => block.TransferCmd).ToList();
    var sites = Sites.Where(site => site.Declaration == parts[1]).ToList();
    var summaries = Summaries.Where(site => site.Declaration == parts[1]).ToList();
    var equal = new List<object>();
    if (Hybrid()) {
      Require(sites.Count == 6 && summaries.Count == 2, "Unexpected caller scope");
      Require(!checks.Any(check => check.Description is PreconditionSatisfied), "Duplicate local caller proof");
      foreach (var group in sites.GroupBy(site => site.Call, ReferenceEqualityComparer.Instance)) {
        var call = group.First().Call;
        Require(original.Contains(call) && !call.IsFree, "Missing or nonchecking actual call");
        var procedure = program.TopLevelDeclarations.OfType<Bpl.Procedure>().Single(p => p.Name == call.callee);
        Require(procedure.InParams.Count == call.Ins.Count, "Mismatched actuals");
        var map = new Dictionary<Bpl.Variable, Bpl.Expr>();
        for (int i = 0; i < call.Ins.Count; i++) { map[procedure.InParams[i]] = call.Ins[i]; }
        var substitution = Bpl.Substituter.SubstitutionFromDictionary(map);
        var requirements = procedure.Requires.Where(req => !req.Free).ToList();
        var generated = group.Select(site => site.Check).ToList();
        Require(requirements.Count == generated.Count, "Duplicate or missing procedure check");
        for (int i = 0; i < requirements.Count; i++) {
          var actual = NativeCallerPlacementFingerprint.Expression(Bpl.Substituter.Apply(substitution, requirements[i].Condition));
          var expected = NativeCallerPlacementFingerprint.Expression(generated[i].Expr);
          Require(actual == expected, "Actual check formula differs from substituted procedure require");
          Require(!original.Contains(generated[i]), "Omitted local check still emitted");
          equal.Add(new { callee = call.callee, expression = actual, equal = true });
        }
      }
      Require(summaries.All(site => !original.Contains(site.Summary)), "Omitted summary still emitted");
    } else {
      Require(sites.All(site => original.Contains(site.Check)), "Native local check missing");
      Require(summaries.All(site => original.Contains(site.Summary)), "Native summary missing");
    }
    var generatedChecks = sites.Select(site => new {
      expression = NativeCallerPlacementFingerprint.Expression(site.Check.Expr),
      attributes = NativeCallerPlacementFingerprint.Attributes(site.Check.Attributes, new NativeCallerPlacementFingerprint.BindingScope()),
      line = site.Check.tok.line, col = site.Check.tok.col
    }).ToArray();
    Bpl.AssertCmd negative = null;
    if (parts[0] == "false-entry-control") { negative = new Bpl.AssertCmd(impl.tok, Bpl.Expr.False); impl.Blocks[0].Cmds.Insert(0, negative); }
    var after = impl.Blocks.SelectMany(block => block.Cmds).Where(cmd => !ReferenceEquals(cmd, negative)).ToList();
    Require(original.SequenceEqual(after, ReferenceEqualityComparer.Instance), "Post-translation commands changed");
    Require(checks.SequenceEqual(after.OfType<Bpl.AssertCmd>(), ReferenceEqualityComparer.Instance), "Original remaining checks changed");
    Require(transfers.SequenceEqual(impl.Blocks.Select(block => block.TransferCmd), ReferenceEqualityComparer.Instance), "Transfers changed");
    var path = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");
    Require(!string.IsNullOrEmpty(path) && !File.Exists(path), "Missing or repeated audit destination");
    File.WriteAllText(path, JsonSerializer.Serialize(new {
      target = parts[1], variant = parts[0], hybrid = Hybrid(), localChecksGenerated = sites.Count,
      localCheckFingerprints = generatedChecks, substitutedProcedureComparisons = equal,
      allPrecheckPreparationRetained = true, originalCallerLoweringTraversed = true,
      soleCallerCheckThroughProcedure = Hybrid(), omittedSummaries = Hybrid() ? summaries.Count : 0,
      allPostTranslationCommandsRetained = true, allBranchTransfersRetained = true,
      negativeEntryCheckAdded = negative != null, originalRemainingChecks = checks.Count
    }) + "\n");
  }
  private static void Require(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
}
