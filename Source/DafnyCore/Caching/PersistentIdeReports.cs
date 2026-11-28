#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Boogie;
using VC;

namespace Microsoft.Dafny.Caching;

/// <summary>Printed assertion syntax interpreted against the current parsed source by IDE code actions.</summary>
public sealed record IdeAssertionAction(
  [property: JsonRequired] string Kind, [property: JsonRequired] string First, [property: JsonRequired] string Second);

/// <summary>Assertion display facts, independent of executable verifier objects.</summary>
public sealed record IdeAssertionReport(IOrigin Origin, string Kind, string Description, string MoreInformation,
  IReadOnlyList<IOrigin> RelatedOrigins, SolverOutcome Outcome = SolverOutcome.Valid,
  IOrigin? SecondaryOrigin = null, string CounterexampleKind = "", string FailureDescription = "",
  string PostconditionFailureDescription = "", string PostconditionMoreInformation = "",
  IReadOnlyList<IOrigin>? DynamicOrigins = null, IdeAssertionAction? Action = null);

/// <summary>A source-bound implementation report, with no split or assertion command.</summary>
public sealed record IdeTaskReport(string Name, string VerboseName, IOrigin Origin,
  IReadOnlyList<IdeAssertionReport> Assertions, IReadOnlyList<DafnyModel>? Models = null);

internal sealed record StoredIdeAssertion(
  [property: JsonRequired] StoredDiagnosticRange Range,
  [property: JsonRequired] string Kind, [property: JsonRequired] string Description,
  [property: JsonRequired] string MoreInformation,
  [property: JsonRequired] List<StoredDiagnosticRange> Related,
  [property: JsonRequired] SolverOutcome Outcome = SolverOutcome.Valid,
  [property: JsonRequired] StoredDiagnosticRange? Secondary = null,
  [property: JsonRequired] string CounterexampleKind = "",
  [property: JsonRequired] string FailureDescription = "",
  [property: JsonRequired] string PostconditionFailureDescription = "",
  [property: JsonRequired] string PostconditionMoreInformation = "",
  [property: JsonRequired] List<StoredDiagnosticRange>? Dynamic = null,
  [property: JsonRequired] IdeAssertionAction? Action = null,
  [property: JsonRequired] ReportPosition? SecondaryPosition = null);
internal sealed record StoredIdeTaskReport(
  [property: JsonRequired] string Name, [property: JsonRequired] string VerboseName,
  [property: JsonRequired] StoredDiagnosticRange Range,
  [property: JsonRequired] List<StoredIdeAssertion> Assertions,
  [property: JsonRequired] StoredFailureReport? Failure = null);

/// <summary>Only complete IDE reports are eligible. Unsupported shapes keep native tasks.</summary>
internal static class PersistentIdeReports {
  internal static bool Same(StoredIdeTaskReport? stored, StoredIdeTaskReport? fresh) =>
    JsonSerializer.Serialize(stored) == JsonSerializer.Serialize(fresh);

  internal static StoredIdeTaskReport? Capture(Program program, IVerificationTask task, VerificationRunResult result) {
    if (result.Outcome is not (SolverOutcome.Valid or SolverOutcome.Invalid or SolverOutcome.OutOfResource)) { return null; }
    try {
      var locations = SourceLocations.For(program);
      var implementation = task.Split.Implementation;
      var origin = BoogieGenerator.ToDafnyToken(implementation.tok);
      if (origin == null || CaptureRange(locations, origin) is not { } range ||
          !locations.PortableReportText(implementation.Name) || !locations.PortableReportText(implementation.VerboseName)) { return null; }
      var failure = result.Outcome == SolverOutcome.Invalid
        ? PersistentFailureReports.Capture(program, task, result, requireModels: true) : null;
      if (result.Outcome == SolverOutcome.Invalid && failure?.Models?.Count != result.CounterExamples.Count) { return null; }
      result.ComputePerAssertOutcomes(out var outcomes, out var counterexamples);
      var assertions = new List<StoredIdeAssertion>();
      foreach (var (assertion, outcome) in outcomes) {
        counterexamples.TryGetValue(assertion, out var counterexample);
        var token = assertion switch {
          AssertEnsuresCmd ensures => ensures.Ensures.tok,
          AssertRequiresCmd requires => requires.Call.tok,
          _ => assertion.tok
        };
        var kind = assertion is AssertEnsuresCmd ? "ensures" : assertion is AssertRequiresCmd ? "call" : "assert";
        if (CaptureRange(locations, token) is not { } assertionRange || assertion.Description == null) { return null; }
        var description = assertion.Description;
        IToken? info = assertion switch {
          AssertEnsuresCmd ensures => ensures.Ensures.tok,
          AssertRequiresCmd requires => requires.Requires.tok,
          _ => assertion.tok is NestedOrigin ? assertion.tok : null
        };
        IToken? secondary = null;
        var counterexampleKind = "";
        if (counterexample is ReturnCounterexample returned) {
          counterexampleKind = "return";
          secondary = returned.FailingReturn.tok;
          description = returned.FailingEnsures.Description.SuccessDescription != "assertion always holds"
            ? returned.FailingEnsures.Description : returned.FailingReturn.Description;
          info = returned.FailingEnsures.tok;
        } else if (counterexample is CallCounterexample call) {
          counterexampleKind = "call";
          secondary = call.FailingRequires.tok;
          description = call.FailingRequires.Description.SuccessDescription != "assertion always holds"
            ? call.FailingRequires.Description : call.FailingCall.Description;
          info = call.FailingRequires.tok;
        }
        var secondaryRange = secondary == null ? null : CaptureRange(locations, secondary);
        var secondaryPosition = secondary == null ? null : locations.ProofPosition(BoogieGenerator.ToDafnyToken(secondary).Center);
        if (secondary != null && (secondaryRange == null || secondaryPosition == null)) { return null; }
        var prefix = outcome == SolverOutcome.Valid ? "Did prove: " : outcome == SolverOutcome.Undetermined
          ? "Not able to prove: " : "Could not prove: ";
        string More(bool hoveringPostcondition) {
          var more = "";
          var next = info;
          while (next != null) {
            var current = next;
            if (next is NestedOrigin nested) { current = nested.Outer; next = nested.Inner; }
            else { next = null; }
            var source = BoogieGenerator.ToDafnyToken(current);
            if (source.IncludesRange && !hoveringPostcondition) {
              var text = source.EntireRange?.PrintOriginal() ??
                program.FindNode<Node>(source.Uri, source.ToDafnyPosition())?.EntireRange.PrintOriginal();
              if (text == null) { throw new ArgumentException("missing assertion source"); }
              more += "  \n" + (next == null ? prefix : "Inside ") + "`" + text + "`";
            }
            hoveringPostcondition = false;
          }
          return more;
        }
        var failureDescription = description is EnsuresDescription ensuresDescription
          ? ensuresDescription.FailureAtPathDescription : description.FailureDescription;
        var postconditionDescription = description is EnsuresDescription postcondition
          ? postcondition.FailureDescriptionSingle : description.FailureDescription;
        var related = new List<StoredDiagnosticRange>();
        var nestedToken = assertion.tok;
        while (nestedToken is NestedOrigin nested) {
          nestedToken = nested.Inner;
          if (nestedToken.filename == assertion.tok.filename) {
            related.Add(CaptureRange(locations, nestedToken) ?? throw new ArgumentException("missing related source"));
          }
        }
        if (counterexample is ReturnCounterexample returnedRelated && returnedRelated.FailingReturn.tok.filename == assertion.tok.filename) {
          related.Add(PersistentFailureReports.CaptureRange(locations,
            BoogieGenerator.ToDafnyToken(returnedRelated.FailingReturn.tok).ReportingRange) ?? throw new ArgumentException("missing return source"));
        }
        var dynamic = new List<StoredDiagnosticRange>();
        if (counterexample is CallCounterexample dynamicCall) {
          dynamic.Add(CaptureRange(locations, dynamicCall.FailingRequires.tok) ?? throw new ArgumentException("missing precondition source"));
        }
        IdeAssertionAction? action = null;
        if (outcome != SolverOutcome.Valid && assertion.Description is ProofObligationDescription dafnyDescription &&
            dafnyDescription.GetAssertedExpr(program.Options) is { } expression) {
          string Print(Expression e) => Printer.ExprToString(program.Options, e, new PrintFlags(UseOriginalDafnyNames: true));
          action = dafnyDescription.IsImplicit ? new("assert", Print(expression), "") : expression switch {
            BinaryExpr { Op: BinaryExpr.Opcode.Eq } binary => new("calc", Print(binary.E0), Print(binary.E1)),
            BinaryExpr { Op: BinaryExpr.Opcode.Iff } binary => new("iff", Print(binary.E0), Print(binary.E1)),
            ForallExpr forall => new("forall", Printer.ForallExprRangeToString(program.Options, forall), Print(forall.Term)),
            _ => null
          };
        }
        var more = More(false);
        var postconditionMore = More(true);
        if (new[] { more, postconditionMore, description.SuccessDescription, failureDescription, postconditionDescription,
              action?.First ?? "", action?.Second ?? "" }.Any(text => !locations.PortableReportText(text))) { return null; }
        assertions.Add(new StoredIdeAssertion(assertionRange, kind, description.SuccessDescription, more, related,
          outcome, secondaryRange, counterexampleKind, failureDescription, postconditionDescription, postconditionMore, dynamic, action, secondaryPosition));
      }
      if (assertions.Count != result.Asserts.Count) { return null; }
      return new StoredIdeTaskReport(implementation.Name, implementation.VerboseName, range, assertions, failure);
    } catch (Exception error) when (error is not OutOfMemoryException) { return null; }
  }

  private static StoredDiagnosticRange? CaptureRange(SourceLocations locations, IToken token) {
    while (token is NestedOrigin nested) { token = nested.Outer; }
    var origin = BoogieGenerator.ToDafnyToken(token);
    return origin == null ? null : PersistentFailureReports.CaptureRange(locations, origin.EntireRange ?? origin.ReportingRange);
  }

  internal static IdeTaskReport? Restore(SourceLocations locations, StoredIdeTaskReport? stored, int count,
    DafnyOptions? options = null, SolverOutcome outcome = SolverOutcome.Valid, int counterexamples = 0) {
    if (stored?.Name == null || stored.VerboseName == null || stored.Assertions == null || stored.Assertions.Count != count ||
        PersistentFailureReports.RestoreRange(locations, stored.Range) is not { } range) { return null; }
    IReadOnlyList<DafnyModel>? models = null;
    if (outcome == SolverOutcome.Invalid) {
      if (stored.Failure?.Counterexamples != counterexamples || counterexamples <= 0 || options == null ||
          PersistentFailureReports.Restore(locations, stored.Failure, options, out models, requireModels: true) == null ||
          models?.Count != counterexamples) { return null; }
    } else if (stored.Failure != null) { return null; }
    var assertions = new List<IdeAssertionReport>();
    IOrigin? Origin(StoredDiagnosticRange? value) => value != null && PersistentFailureReports.RestoreRange(locations, value) is { } r
      ? new SourceOrigin(r.StartToken, r.EndToken) : null;
    foreach (var assertion in stored.Assertions) {
      if (assertion == null) { return null; }
      var secondaryRange = assertion.Secondary == null ? null : PersistentFailureReports.RestoreRange(locations, assertion.Secondary);
      var secondaryToken = assertion.SecondaryPosition == null ? null : locations.ProofToken(assertion.SecondaryPosition);
      if (assertion.Kind is not ("ensures" or "call" or "assert") || assertion.Description == null ||
          assertion.MoreInformation == null || assertion.Related == null || assertion.Dynamic == null || Origin(assertion.Range) is not { } current ||
          !Enum.IsDefined(assertion.Outcome) || assertion.CounterexampleKind is not ("" or "return" or "call") ||
          assertion.FailureDescription == null || assertion.PostconditionFailureDescription == null || assertion.PostconditionMoreInformation == null ||
          assertion.CounterexampleKind != "" && assertion.Secondary == null ||
          assertion.Secondary != null && (secondaryRange == null || secondaryToken == null ||
            secondaryToken.Uri != secondaryRange.StartToken.Uri || secondaryToken.pos < secondaryRange.StartToken.pos ||
            secondaryToken.pos > secondaryRange.EndToken.pos) ||
          assertion.Secondary == null && assertion.SecondaryPosition != null ||
          assertion.Action is { } action && (action.Kind is not ("assert" or "calc" or "iff" or "forall") || action.First == null || action.Second == null)) { return null; }
      var related = assertion.Related.Select(Origin).ToList();
      var dynamic = (assertion.Dynamic ?? []).Select(Origin).ToList();
      if (related.Any(o => o == null) || dynamic.Any(o => o == null)) { return null; }
      assertions.Add(new IdeAssertionReport(current, assertion.Kind, assertion.Description, assertion.MoreInformation,
        related.Select(o => o!).ToList(), assertion.Outcome, secondaryRange == null ? null :
          new SourceOrigin(secondaryRange, new TokenRange(secondaryToken!, secondaryToken)),
        assertion.CounterexampleKind, assertion.FailureDescription,
        assertion.PostconditionFailureDescription, assertion.PostconditionMoreInformation, dynamic.Select(o => o!).ToList(), assertion.Action));
    }
    if (outcome == SolverOutcome.Valid && assertions.Any(a => a.Outcome != SolverOutcome.Valid) ||
        outcome == SolverOutcome.Invalid && !assertions.Any(a => a.Outcome == SolverOutcome.Invalid)) { return null; }
    return new IdeTaskReport(stored.Name, stored.VerboseName, new SourceOrigin(range.StartToken, range.EndToken), assertions, models);
  }
}
