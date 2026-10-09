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
    Require(parts.Length == 2 && new[] { "native-control", "off-control", "preparation-legacy-check", "without-caller-allocatedness", "false-entry-control" }.Contains(parts[0]) && parts[1] == "FormArmy", "Unknown placement diagnostic");
    return parts;
  }
  private static bool Hybrid() => Selection() is { } parts && parts[0] is "preparation-legacy-check" or "without-caller-allocatedness" or "false-entry-control";
  private static readonly List<Bpl.AssumeCmd> OmittedAllocatedness = new();
  private static int SelectedPreparations;
  public static IReadOnlyList<object> Preparation(string declaration, IReadOnlyList<object> original,
    IReadOnlyList<object> normalized, ISet<string> argumentTemporaries) {
    var parts = Selection(); if (parts == null || declaration != parts[1]) { return normalized; }
    SelectedPreparations++;
    if (parts[0] is not ("without-caller-allocatedness" or "false-entry-control")) { return normalized; }
    Require(!ReferenceEquals(original, normalized), "Uncertified WF fragment");
    foreach (var command in normalized) {
      if (command is Bpl.CommentCmd or Bpl.AssumeCmd) { continue; }
      Require(command is Bpl.AssignCmd, "Unsupported WF command or havoc");
      foreach (var lhs in ((Bpl.AssignCmd)command).Lhss) {
        Require(lhs is Bpl.SimpleAssignLhs simple && argumentTemporaries.Contains(simple.AssignedVariable.Name), "Non-private WF write");
      }
    }
    var retained = new List<object>();
    foreach (var command in normalized) {
      if (command is Bpl.AssumeCmd assumption && assumption.Expr is Bpl.NAryExpr application &&
          application.Fun is Bpl.FunctionCall function && function.ToString() == "$IsAlloc") {
        Require(application.Args.Count == 3 && application.Args[0] is Bpl.IdentifierExpr argument &&
          argumentTemporaries.Contains(argument.Name), "Allocatedness argument is not private");
        Require(application.Args[2] is Bpl.IdentifierExpr heap && heap.Name == "$Heap", "Unexpected allocatedness heap");
        Require(!OmittedAllocatedness.Contains(assumption), "Repeated allocatedness omission");
        OmittedAllocatedness.Add(assumption);
      } else { retained.Add(command); }
    }
    Require(retained.SequenceEqual(normalized.Where(command => !OmittedAllocatedness.Any(omitted => ReferenceEquals(omitted, command))), ReferenceEqualityComparer.Instance), "Other preparation objects changed");
    return retained;
  }
  public static bool Enabled(string family, bool enabled) {
    Require(family is "prepare" or "check" or "other", "Unknown family");
    return Hybrid() ? enabled && family == "prepare" : enabled;
  }
  public static bool EmitCheck(string declaration, Bpl.CallCmd call, Bpl.PredicateCmd command) {
    Require(command is Bpl.AssertCmd, "Expected mandatory caller assertion");
    Sites.Add(new(declaration, call, (Bpl.AssertCmd)command)); return !Hybrid();
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
        var map = new Dictionary<string, (Bpl.Variable Formal, Bpl.IdentifierExpr Actual)>();
        for (int i = 0; i < call.Ins.Count; i++) {
          Require(call.Ins[i] is Bpl.IdentifierExpr, "Only frozen identifier actuals are supported");
          var actual = (Bpl.IdentifierExpr)call.Ins[i];
          Require(actual.Type?.ToString() == procedure.InParams[i].TypedIdent.Type.ToString(), "Actual type differs");
          Require(map.TryAdd(procedure.InParams[i].Name, (procedure.InParams[i], actual)), "Repeated formal name");
        }
        var substitution = new UnresolvedFormalActualSubstitution(map);
        var requirements = procedure.Requires.Where(req => !req.Free).ToList();
        var generated = group.Select(site => site.Check).ToList();
        Require(requirements.Count == generated.Count, "Duplicate or missing procedure check");
        for (int i = 0; i < requirements.Count; i++) {
          var actual = NativeCallerPlacementFingerprint.Expression(substitution.Substitute(requirements[i].Condition));
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
    Require(SelectedPreparations == 4, "Unexpected selected preparation scope");
    var omitAllocatedness = parts[0] is "without-caller-allocatedness" or "false-entry-control";
    Require(OmittedAllocatedness.Count == (omitAllocatedness ? 2 : 0), "Unexpected allocatedness count");
    Require(OmittedAllocatedness.All(command => !original.Contains(command)), "Omitted allocatedness still emitted");
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
      allOtherPrecheckPreparationRetained = true, originalCallerLoweringTraversed = true,
      omittedAllocatedness = OmittedAllocatedness.Count, selectedPreparations = SelectedPreparations,
      omittedAllocatednessFingerprints = OmittedAllocatedness.Select(command => NativeCallerPlacementFingerprint.Expression(command.Expr)).ToArray(),
      soleCallerCheckThroughProcedure = Hybrid(), omittedSummaries = Hybrid() ? summaries.Count : 0,
      allPostTranslationCommandsRetained = true, allBranchTransfersRetained = true,
      negativeEntryCheckAdded = negative != null, originalRemainingChecks = checks.Count
    }) + "\n");
  }
  // This audit runs before resolution. Use a cloned, restricted substitution,
  // rejecting binder capture rather than resolving/mutating the native program.
  private sealed class UnresolvedFormalActualSubstitution : Bpl.Duplicator {
    private readonly Dictionary<string, (Bpl.Variable Formal, Bpl.IdentifierExpr Actual)> map;
    private readonly HashSet<string> protectedNames;
    public UnresolvedFormalActualSubstitution(Dictionary<string, (Bpl.Variable Formal, Bpl.IdentifierExpr Actual)> map) {
      this.map = map; protectedNames = new(map.Keys.Concat(map.Values.Select(value => value.Actual.Name)));
    }
    public Bpl.Expr Substitute(Bpl.Expr expression) {
      RejectCapture(expression); return (Bpl.Expr)Visit(expression);
    }
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node) {
      if (map.TryGetValue(node.Name, out var value)) {
        Require(node.Decl == null || ReferenceEquals(node.Decl, value.Formal), "Ambiguous resolved formal identity");
        return base.VisitIdentifierExpr(value.Actual);
      }
      return base.VisitIdentifierExpr(node);
    }
    private void Attributes(Bpl.QKeyValue attributes) {
      for (var item = attributes; item != null; item = item.Next) {
        foreach (var expression in item.Params.OfType<Bpl.Expr>()) { RejectCapture(expression); }
      }
    }
    private void Binders(IEnumerable<Bpl.Variable> variables) {
      Require(variables.All(variable => !protectedNames.Contains(variable.Name)), "Binder capture or shadowing unsupported in this audit");
    }
    private void RejectCapture(Bpl.Expr expression) {
      switch (expression) {
        case Bpl.IdentifierExpr or Bpl.LiteralExpr: return;
        case Bpl.OldExpr old: RejectCapture(old.Expr); return;
        case Bpl.NAryExpr application:
          foreach (var argument in application.Args) { RejectCapture(argument); } return;
        case Bpl.QuantifierExpr quantifier:
          Binders(quantifier.Dummies); Attributes(quantifier.Attributes); RejectCapture(quantifier.Body);
          for (var trigger = quantifier.Triggers; trigger != null; trigger = trigger.Next) {
            foreach (var term in trigger.Tr) { RejectCapture(term); }
          }
          return;
        case Bpl.LambdaExpr lambda:
          Binders(lambda.Dummies); Attributes(lambda.Attributes); RejectCapture(lambda.Body); return;
        case Bpl.LetExpr let:
          Binders(let.Dummies); foreach (var rhs in let.Rhss) { RejectCapture(rhs); } RejectCapture(let.Body); return;
        default: throw new InvalidOperationException("Unsupported expression in formal/actual audit");
      }
    }
  }
  private static void Require(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
}
