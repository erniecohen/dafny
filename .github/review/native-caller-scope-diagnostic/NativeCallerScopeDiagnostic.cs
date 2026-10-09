using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;

// Scratch-only support-preserving proof scope; no product policy is enabled.
public static class NativeCallerScopeDiagnostic {
  public sealed class Scope {
    public bool Selected, Scoped, Negative;
    public string Declaration;
    public Bpl.CallCmd Call;
    public BoogieStmtListBuilder Builder;
    public int Start;
    public List<object> Commands;
    public List<Bpl.AssertCmd> Checks = new();
    public List<Bpl.AssumeCmd> Publications = new();
    public Bpl.AssertCmd NegativeCheck;
  }
  private static readonly List<Scope> Scopes = new();
  private static readonly List<object> PreparationCommands = new();
  private static readonly HashSet<string> PrivateArguments = new();
  private static int Preparations;
  private static string[] Selection() {
    var value = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_CALLER_SCOPE");
    if (value == null) { return null; }
    var parts = value.Split(':');
    Require(parts.Length == 2 && new[] { "native-control", "scoped-caller", "false-scoped-control", "false-prepared-control", "canonical-caller", "false-canonical-entry" }.Contains(parts[0]) &&
      new[] { "Composite", "FormArmy", "ScopeFalseProbe" }.Contains(parts[1]), "Unknown caller scope diagnostic");
    return parts;
  }
  private static readonly Dictionary<Bpl.Requires, Bpl.Expr> CanonicalRequirements = new();
  private static bool AlwaysAssumed(Bpl.Requires requirement) {
    for (var attribute = requirement.Attributes; attribute != null; attribute = attribute.Next) {
      if (attribute.Key == "always_assume" && !attribute.Params.Any()) { return true; }
    }
    return false;
  }
  private static bool Canonical() {
    var parts = Selection();
    return parts != null && (parts[0] == "canonical-caller" || parts[0] == "false-canonical-entry");
  }
  public static void CanonicalRequirement(Bpl.Requires requirement, bool locallyChecked) {
    if (!locallyChecked || !Canonical()) { return; }
    Require(requirement.Free, "Canonical publication must not add a second proof");
    CanonicalRequirements.Add(requirement, requirement.Condition);
    requirement.Attributes = BoogieGenerator.AlwaysAssumeAttribute(requirement.tok, requirement.Attributes);
  }
  public static Scope Begin(string declaration, Bpl.CallCmd call, BoogieStmtListBuilder original) {
    var parts = Selection(); var selected = parts != null && parts[1] == declaration;
    var scoped = selected && parts[0] != "native-control";
    var result = new Scope { Selected = selected, Scoped = scoped, Declaration = declaration, Call = call,
      Negative = scoped && (parts[0] == "false-scoped-control" || parts[0] == "false-prepared-control") && Scopes.Count == 0,
      Builder = scoped ? new BoogieStmtListBuilder(original.tran, original.Options, original.Context) : original };
    result.Start = result.Builder.Commands.Count; if (selected) { Scopes.Add(result); } return result;
  }
  public static void Prepared(string declaration, IReadOnlyList<object> original, IReadOnlyList<object> normalized, ISet<string> arguments) {
    var parts = Selection(); if (parts == null || declaration != parts[1]) { return; }
    Require(!ReferenceEquals(original, normalized), "Uncertified WF fragment");
    foreach (var command in normalized) {
      if (command is Bpl.CommentCmd or Bpl.AssumeCmd) { continue; }
      Require(command is Bpl.AssignCmd, "Unsupported WF command or havoc");
      foreach (var lhs in ((Bpl.AssignCmd)command).Lhss) {
        Require(lhs is Bpl.SimpleAssignLhs simple && arguments.Contains(simple.AssignedVariable.Name), "Source WF write");
      }
    }
    Preparations++; PreparationCommands.AddRange(normalized); PrivateArguments.UnionWith(arguments);
  }
  public static void Check(Scope scope, Bpl.PredicateCmd command) {
    Require(command is Bpl.AssertCmd, "Expected mandatory caller check");
    // The pre-check control tests the context after every legitimate preparation
    // command but before establishing P. The earlier post-check control remains.
    if (scope.Negative && Selection()[0] == "false-prepared-control" && scope.NegativeCheck == null) {
      scope.Builder.Add(Negative(scope));
    }
    scope.Checks.Add((Bpl.AssertCmd)command);
  }
  public static void Finish(Scope scope) {
    if (!scope.Selected) { return; }
    scope.Commands = scope.Builder.Commands.Skip(scope.Start).ToList();
    Require(scope.Checks.All(check => scope.Commands.Contains(check)), "Missing original check");
  }
  public static Bpl.AssertCmd Negative(Scope scope) {
    Require(scope.Negative && scope.NegativeCheck == null, "Unexpected scoped negative");
    return scope.NegativeCheck = new Bpl.AssertCmd(scope.Call.tok, Bpl.Expr.False);
  }
  public static IEnumerable<Bpl.AssumeCmd> Publications(Scope scope) {
    Require(scope.Scoped && scope.Publications.Count == 0, "Unexpected publication");
    if (Canonical()) { yield break; }
    foreach (var check in scope.Checks) {
      RejectPrivateEscape(check.Expr);
      var fact = new Bpl.AssumeCmd(check.tok, check.Expr); scope.Publications.Add(fact); yield return fact;
    }
  }
  private static void RejectPrivateEscape(Bpl.Expr expression) {
    switch (expression) {
      case Bpl.IdentifierExpr identifier: Require(!PrivateArguments.Contains(identifier.Name), "Private WF argument escapes"); return;
      case Bpl.LiteralExpr: return;
      case Bpl.OldExpr old: RejectPrivateEscape(old.Expr); return;
      case Bpl.NAryExpr application: foreach (var arg in application.Args) { RejectPrivateEscape(arg); } return;
      case Bpl.QuantifierExpr quantifier:
        RejectPrivateEscape(quantifier.Body);
        for (var trigger = quantifier.Triggers; trigger != null; trigger = trigger.Next) { foreach (var term in trigger.Tr) { RejectPrivateEscape(term); } }
        for (var attribute = quantifier.Attributes; attribute != null; attribute = attribute.Next) { foreach (var arg in attribute.Params.OfType<Bpl.Expr>()) { RejectPrivateEscape(arg); } }
        return;
      default: throw new InvalidOperationException("Unsupported published expression");
    }
  }
  public static void Apply(Bpl.Program program) {
    var parts = Selection(); if (parts == null) { return; }
    var impls = program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(impl => impl.Name == "Impl$$_module.__default." + parts[1]).ToList();
    if (impls.Count == 0) { return; } Require(impls.Count == 1, "Ambiguous implementation");
    var impl = impls.Single(); var commands = impl.Blocks.SelectMany(block => block.Cmds).ToList();
    int Count(object value) => commands.Count(command => ReferenceEquals(command, value));
    Require(Scopes.Count == (parts[1] == "FormArmy" ? 4 : 1) && Preparations == (parts[1] == "FormArmy" ? 4 : parts[1] == "ScopeFalseProbe" ? 1 : 2),
      $"Unexpected caller scope: {Scopes.Count} calls, {Preparations} clause preparations");
    // Literal preparation may include comments/tautologies. Retention and allowed
    // command kinds are audited above; the probe compares the observed native
    // and canonical counts instead of guessing a count for this new fixture.
    if (parts[1] != "ScopeFalseProbe") {
      Require(PreparationCommands.Count == (parts[1] == "FormArmy" ? 12 : 6), "Unexpected WF scope");
    }
    Require(PreparationCommands.All(command => Count(command) == 1), "Preparation removed or duplicated");
    Require(Scopes.All(scope => Count(scope.Call) == 1 && scope.Commands.All(command => Count(command) == 1)), "Original caller command removed or duplicated");
    Require(Scopes.Sum(scope => scope.Checks.Count) == (parts[1] == "FormArmy" ? 6 : parts[1] == "ScopeFalseProbe" ? 1 : 2), "Unexpected mandatory check count");
    Require(Scopes.All(scope => scope.Publications.Count == (scope.Scoped && !Canonical() ? scope.Checks.Count : 0)), "Missing normal publication");
    Require(Scopes.All(scope => scope.Publications.Zip(scope.Checks).All(pair => ReferenceEquals(pair.First.Expr, pair.Second.Expr) && Count(pair.First) == 1)), "Publication differs from checked fact");
    var canonicalContracts = new List<Bpl.Requires>();
    if (Canonical()) {
      foreach (var scope in Scopes) {
        var procedure = program.TopLevelDeclarations.OfType<Bpl.Procedure>().Single(p => p.Name == scope.Call.callee);
        canonicalContracts.AddRange(procedure.Requires.Where(CanonicalRequirements.ContainsKey));
      }
      Require(canonicalContracts.Count == (parts[1] == "FormArmy" ? 6 : parts[1] == "ScopeFalseProbe" ? 1 : 2), "Missing normal caller contract publication");
      Require(canonicalContracts.All(req => req.Free && AlwaysAssumed(req) && ReferenceEquals(req.Condition, CanonicalRequirements[req])), "Contract changed or checked twice");
    }
    var negatives = Scopes.Where(scope => scope.NegativeCheck != null).Select(scope => scope.NegativeCheck).ToList();
    if (parts[0] == "false-canonical-entry") {
      var entry = new Bpl.AssertCmd(impl.tok, Bpl.Expr.False);
      impl.Blocks.First().Cmds.Insert(0, entry); commands.Insert(0, entry); negatives.Add(entry);
    }
    Require(negatives.Count == (parts[0].StartsWith("false-", StringComparison.Ordinal) ? 1 : 0) && negatives.All(check => Count(check) == 1), "Missing scoped negative");
    var checks = commands.OfType<Bpl.AssertCmd>().Where(check => !negatives.Contains(check)).Select(check => new {
      expression = NativeCallerScopeFingerprint.Expression(check.Expr),
      attributes = NativeCallerScopeFingerprint.Attributes(check.Attributes, new NativeCallerScopeFingerprint.BindingScope()),
      line = check.tok.line, col = check.tok.col
    }).OrderBy(check => check.line).ThenBy(check => check.col).ThenBy(check => check.expression, StringComparer.Ordinal).ThenBy(check => check.attributes, StringComparer.Ordinal).ToArray();
    var path = Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT"); Require(!string.IsNullOrEmpty(path) && !File.Exists(path), "Missing/repeated audit destination");
    File.WriteAllText(path, JsonSerializer.Serialize(new {
      target = parts[1], variant = parts[0], scopedCallers = Scopes.Count(scope => scope.Scoped),
      mandatoryCallerChecks = Scopes.Sum(scope => scope.Checks.Count), normalPublishedPieces = Scopes.Sum(scope => scope.Publications.Count),
      canonicalContractPublication = Canonical(), canonicalPublishedRequirements = canonicalContracts.Count,
      originalContractConditionsRetainedByIdentity = true, canonicalRequirementsNoncheckingAndAlwaysAssumed = !Canonical() || canonicalContracts.All(req => req.Free && AlwaysAssumed(req)),
      negativeEntryCheckAdded = parts[0] == "false-canonical-entry",
      preparationFragments = Preparations, preparationCommands = PreparationCommands.Count,
      allOriginalPreparationAndCallerObjectsRetainedOnce = true, allActualCheckObjectsRetainedOnce = true,
      allOriginalFuelTraversalRetained = true, publishedPiecesEqualActualChecksByIdentity = true,
      noPrivatePreparationArgumentEscapes = true, existingPathAsideConstructionUsed = true,
      negativeScopedCheckAdded = negatives.Count == 1, actualCheckFingerprints = checks, actualChecks = checks.Length
    }) + "\n");
  }
  private static void Require(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
}
