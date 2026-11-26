#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.Boogie;
using VC;

namespace Microsoft.Dafny.Caching;

internal sealed record StoredDiagnosticRange(
  [property: JsonRequired] ReportPosition Start, [property: JsonRequired] ReportPosition End,
  [property: JsonRequired] bool InclusiveEnd, [property: JsonRequired] string SourceOriginal);
internal sealed record StoredDiagnosticRelated(
  [property: JsonRequired] StoredDiagnosticRange Range, [property: JsonRequired] string? ErrorId,
  [property: JsonRequired] List<string> MessageParts);
internal sealed record StoredFailureDiagnostic(
  [property: JsonRequired] MessageSource Source, [property: JsonRequired] ErrorLevel Level,
  [property: JsonRequired] StoredDiagnosticRange Range, [property: JsonRequired] string? ErrorId,
  [property: JsonRequired] List<string> MessageParts, [property: JsonRequired] List<StoredDiagnosticRelated> Related);
internal sealed record StoredFailureReport(
  [property: JsonRequired] int Counterexamples, [property: JsonRequired] List<StoredFailureDiagnostic> Diagnostics);

/// <summary>Terminal diagnostics, not executable counterexamples or models.</summary>
internal static class PersistentFailureReports {
  private static StoredDiagnosticRange? CaptureRange(SourceLocations locations, TokenRange range) {
    if (locations.ProofPosition(range.StartToken) is not { } start ||
        locations.ProofPosition(range.EndToken) is not { } end ||
        locations.ProofToken(start) is not { } first || locations.ProofToken(end) is not { } last ||
        range.InclusiveEnd && range.EndToken.val.Length != last.val.Length) { return null; }
    return new StoredDiagnosticRange(start, end, range.InclusiveEnd,
      new TokenRange(first, range.InclusiveEnd ? last : null).PrintOriginal());
  }

  private static TokenRange? RestoreRange(SourceLocations locations, StoredDiagnosticRange stored) {
    if (stored?.SourceOriginal == null || locations.ProofToken(stored.Start) is not { } first ||
        locations.ProofToken(stored.End) is not { } last || first.Uri != last.Uri || last.pos < first.pos ||
        !stored.InclusiveEnd && first != last) { return null; }
    var range = new TokenRange(first, stored.InclusiveEnd ? last : null);
    return range.PrintOriginal() == stored.SourceOriginal ? range : null;
  }

  private static bool Text(SourceLocations locations, List<string>? parts) =>
    parts is { Count: > 0 } && parts.All(p => p != null && locations.PortableReportText(p));

  internal static StoredFailureReport? Capture(SourceLocations locations, int counterexamples, IEnumerable<DafnyDiagnostic> diagnostics) {
    var stored = new List<StoredFailureDiagnostic>();
    foreach (var diagnostic in diagnostics) {
      if (diagnostic.Source != MessageSource.Verifier || !Enum.IsDefined(diagnostic.Level) ||
          CaptureRange(locations, diagnostic.Range) is not { } range ||
          !Text(locations, diagnostic.MessageParts.ToList())) { return null; }
      var related = new List<StoredDiagnosticRelated>();
      foreach (var info in diagnostic.RelatedInformation) {
        if (CaptureRange(locations, info.Range) is not { } relatedRange || !Text(locations, info.MessageParts.ToList())) { return null; }
        related.Add(new StoredDiagnosticRelated(relatedRange, info.ErrorId, info.MessageParts.ToList()));
      }
      stored.Add(new StoredFailureDiagnostic(diagnostic.Source, diagnostic.Level, range, diagnostic.ErrorId,
        diagnostic.MessageParts.ToList(), related));
    }
    return counterexamples > 0 && stored.Count(d => d.Level == ErrorLevel.Error) == counterexamples
      ? new StoredFailureReport(counterexamples, stored) : null;
  }

  internal static StoredFailureReport? Capture(Program program, IVerificationTask task, VerificationRunResult result) {
    if (program.Options.ExtractCounterexample || result.CounterExamples.Any(c => c.IsAuxiliaryCexForDiagnosingTimeouts)) { return null; }
    try {
      var reporter = new BatchErrorReporter(program.Options);
      var origin = (CanVerifyOrigin)task.ScopeToken;
      Compilation.ReportDiagnosticsInResult(program.Options, origin.CanVerify.FullDafnyName,
        BoogieGenerator.ToDafnyToken(task.Token), task.Split.Implementation.GetTimeLimit(program.Options), result, reporter);
      return Capture(SourceLocations.For(program), result.CounterExamples.Count, reporter.AllMessages);
    } catch (Exception error) when (error is not OutOfMemoryException) { return null; }
  }

  internal static IReadOnlyList<DafnyDiagnostic>? Restore(SourceLocations locations, StoredFailureReport? stored) {
    if (stored?.Counterexamples is not > 0 || stored.Diagnostics == null ||
        stored.Diagnostics.Count(d => d?.Level == ErrorLevel.Error) != stored.Counterexamples) { return null; }
    var diagnostics = new List<DafnyDiagnostic>();
    foreach (var diagnostic in stored.Diagnostics) {
      if (diagnostic == null || diagnostic.Source != MessageSource.Verifier || !Enum.IsDefined(diagnostic.Level) ||
          RestoreRange(locations, diagnostic.Range) is not { } range || !Text(locations, diagnostic.MessageParts) ||
          diagnostic.Related == null) { return null; }
      var related = new List<DafnyRelatedInformation>();
      foreach (var info in diagnostic.Related) {
        if (info == null || RestoreRange(locations, info.Range) is not { } relatedRange || !Text(locations, info.MessageParts)) { return null; }
        related.Add(new DafnyRelatedInformation(relatedRange, info.ErrorId!, info.MessageParts));
      }
      diagnostics.Add(new DafnyDiagnostic(diagnostic.Source, diagnostic.ErrorId!, range, diagnostic.MessageParts, diagnostic.Level, related));
    }
    return diagnostics;
  }
}
