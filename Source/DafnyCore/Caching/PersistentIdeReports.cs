#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Boogie;
using VC;

namespace Microsoft.Dafny.Caching;

/// <summary>Successful assertion display facts, independent of executable verifier objects.</summary>
public sealed record IdeAssertionReport(IOrigin Origin, string Kind, string Description, string MoreInformation,
  IReadOnlyList<IOrigin> RelatedOrigins);

/// <summary>A source-bound implementation report, with no split, assertion command or model graph.</summary>
public sealed record IdeTaskReport(string Name, string VerboseName, IOrigin Origin,
  IReadOnlyList<IdeAssertionReport> Assertions);

internal sealed record StoredIdeAssertion(
  [property: JsonRequired] StoredDiagnosticRange Range,
  [property: JsonRequired] string Kind, [property: JsonRequired] string Description,
  [property: JsonRequired] string MoreInformation,
  [property: JsonRequired] List<StoredDiagnosticRange> Related);
internal sealed record StoredIdeTaskReport(
  [property: JsonRequired] string Name, [property: JsonRequired] string VerboseName,
  [property: JsonRequired] StoredDiagnosticRange Range,
  [property: JsonRequired] List<StoredIdeAssertion> Assertions);

/// <summary>Only complete successful IDE reports are eligible. Other shapes keep native tasks.</summary>
internal static class PersistentIdeReports {
  internal static bool Same(StoredIdeTaskReport? stored, StoredIdeTaskReport? fresh) =>
    JsonSerializer.Serialize(stored) == JsonSerializer.Serialize(fresh);

  internal static StoredIdeTaskReport? Capture(Program program, IVerificationTask task, VerificationRunResult result) {
    if (result.Outcome != SolverOutcome.Valid ||
        result.CounterExamples.Count != 0) { return null; }
    try {
      var locations = SourceLocations.For(program);
      var implementation = task.Split.Implementation;
      var origin = BoogieGenerator.ToDafnyToken(implementation.tok);
      if (origin == null || CaptureRange(locations, origin) is not { } range ||
          !locations.PortableReportText(implementation.Name) || !locations.PortableReportText(implementation.VerboseName)) { return null; }
      var assertions = new List<StoredIdeAssertion>();
      foreach (var assertion in result.Asserts) {
        var token = assertion switch {
          AssertEnsuresCmd ensures => ensures.Ensures.tok,
          AssertRequiresCmd requires => requires.Call.tok,
          _ => assertion.tok
        };
        var kind = assertion is AssertEnsuresCmd ? "ensures" : assertion is AssertRequiresCmd ? "call" : "assert";
        if (CaptureRange(locations, token) is not { } assertionRange || assertion.Description == null) { return null; }
        var more = "";
        var info = assertion switch {
          AssertEnsuresCmd ensures => ensures.Ensures.tok,
          AssertRequiresCmd requires => requires.Requires.tok,
          _ => assertion.tok is NestedOrigin ? assertion.tok : null
        };
        while (info != null) {
          var current = info;
          if (info is NestedOrigin nested) { current = nested.Outer; info = nested.Inner; }
          else { info = null; }
          var source = BoogieGenerator.ToDafnyToken(current);
          if (source.IncludesRange) {
            var text = source.EntireRange?.PrintOriginal() ??
              program.FindNode<Node>(source.Uri, source.ToDafnyPosition())?.EntireRange.PrintOriginal();
            if (text == null) { return null; }
            more += "  \n" + (info == null ? "Did prove: " : "Inside ") + "`" + text + "`";
          }
        }
        if (!locations.PortableReportText(more) || !locations.PortableReportText(assertion.Description.SuccessDescription)) { return null; }
        var related = new List<StoredDiagnosticRange>();
        var nestedToken = assertion.tok;
        while (nestedToken is NestedOrigin nested) {
          nestedToken = nested.Inner;
          if (nestedToken.filename == assertion.tok.filename) {
            if (CaptureRange(locations, nestedToken) is not { } relatedRange) { return null; }
            related.Add(relatedRange);
          }
        }
        assertions.Add(new StoredIdeAssertion(assertionRange, kind, assertion.Description.SuccessDescription, more, related));
      }
      return new StoredIdeTaskReport(implementation.Name, implementation.VerboseName, range, assertions);
    } catch (Exception error) when (error is not OutOfMemoryException) { return null; }
  }

  private static StoredDiagnosticRange? CaptureRange(SourceLocations locations, IToken token) {
    while (token is NestedOrigin nested) { token = nested.Outer; }
    var origin = BoogieGenerator.ToDafnyToken(token);
    return origin == null ? null : PersistentFailureReports.CaptureRange(locations, origin.EntireRange ?? origin.ReportingRange);
  }

  internal static IdeTaskReport? Restore(SourceLocations locations, StoredIdeTaskReport? stored, int count) {
    if (stored?.Name == null || stored.VerboseName == null || stored.Assertions == null || stored.Assertions.Count != count ||
        PersistentFailureReports.RestoreRange(locations, stored.Range) is not { } range) { return null; }
    var assertions = new List<IdeAssertionReport>();
    foreach (var assertion in stored.Assertions) {
      if (assertion == null || assertion.Kind is not ("ensures" or "call" or "assert") || assertion.Description == null ||
          assertion.MoreInformation == null || assertion.Related == null || PersistentFailureReports.RestoreRange(locations, assertion.Range) is not { } current) { return null; }
      var related = new List<IOrigin>();
      foreach (var storedRange in assertion.Related) {
        if (PersistentFailureReports.RestoreRange(locations, storedRange) is not { } relatedRange) { return null; }
        related.Add(new SourceOrigin(relatedRange.StartToken, relatedRange.EndToken));
      }
      assertions.Add(new IdeAssertionReport(new SourceOrigin(current.StartToken, current.EndToken), assertion.Kind,
        assertion.Description, assertion.MoreInformation, related));
    }
    return new IdeTaskReport(stored.Name, stored.VerboseName, new SourceOrigin(range.StartToken, range.EndToken), assertions);
  }
}
