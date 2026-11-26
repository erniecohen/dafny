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
  [property: JsonRequired] int Counterexamples, [property: JsonRequired] List<StoredFailureDiagnostic> Diagnostics,
  [property: JsonRequired] List<StoredFailureModel>? Models = null);

/// <summary>Source-bound diagnostics and optional detached native models, without executable counterexample graphs.</summary>
internal static class PersistentFailureReports {
  internal static StoredDiagnosticRange? CaptureRange(SourceLocations locations, TokenRange range) {
    if (locations.ProofPosition(range.StartToken) is not { } start ||
        locations.ProofPosition(range.EndToken) is not { } end ||
        locations.ProofToken(start) is not { } first || locations.ProofToken(end) is not { } last ||
        range.InclusiveEnd && range.EndToken.val.Length != last.val.Length) { return null; }
    return new StoredDiagnosticRange(start, end, range.InclusiveEnd,
      new TokenRange(first, range.InclusiveEnd ? last : null).PrintOriginal());
  }

  internal static TokenRange? RestoreRange(SourceLocations locations, StoredDiagnosticRange stored) {
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
    if (result.CounterExamples.Any(c => c.IsAuxiliaryCexForDiagnosingTimeouts)) { return null; }
    var originalOrder = result.CounterExamples.ToList();
    try {
      var reporter = new BatchErrorReporter(program.Options);
      var origin = (CanVerifyOrigin)task.ScopeToken;
      Compilation.ReportDiagnosticsInResult(program.Options, origin.CanVerify.FullDafnyName,
        BoogieGenerator.ToDafnyToken(task.Token), task.Split.Implementation.GetTimeLimit(program.Options), result, reporter, includeModels: false);
      var locations = SourceLocations.For(program);
      var stored = Capture(locations, result.CounterExamples.Count, reporter.AllMessages);
      if (stored == null || !program.Options.ExtractCounterexample) { return stored; }
      var bindings = new Dictionary<string, (TokenRange Range, string Suffix)>(StringComparer.Ordinal);
      foreach (var command in task.Split.Implementation.Blocks.SelectMany(b => b.Cmds).OfType<AssumeCmd>()) {
        if (QKeyValue.FindStringAttribute(command.Attributes, "captureState") is not { } name) { continue; }
        var token = BoogieGenerator.ToDafnyToken(command.tok);
        var prefix = token.OriginToString(program.Options);
        var description = program.Options.CapturedStateDescription(name);
        if (!description.StartsWith(prefix, StringComparison.Ordinal)) { return null; }
        var binding = (token.ReportingRange, description[prefix.Length..]);
        if (bindings.TryGetValue(name, out var previous) && previous != binding) { return null; }
        bindings[name] = binding;
      }
      var models = new List<StoredFailureModel>();
      // ReportDiagnosticsInResult sorted these counterexamples in diagnostic order.
      foreach (var counterexample in result.CounterExamples) {
        if (PersistentModels.Capture(locations, counterexample.Model, bindings) is not { } model) { return null; }
        var description = counterexample.FailingAssert.Description as ProofObligationDescription;
        var render = !program.Options.Get(CommonOptionBag.JsonOutput) ||
          description?.GetDiagnostic(BoogieGenerator.ToDafnyToken(counterexample.FailingAssert.tok).ReportingRange) == null;
        models.Add(new StoredFailureModel(model, render));
      }
      return stored with { Models = models };
    } catch (Exception error) when (error is not OutOfMemoryException) { return null; }
    finally {
      // Rendering sorts the list. Leave the native consumer its original input, so equal
      // diagnostic locations do not undergo an additional, potentially unstable sort.
      result.CounterExamples.Clear();
      result.CounterExamples.AddRange(originalOrder);
    }
  }

  internal static IReadOnlyList<DafnyDiagnostic>? Restore(SourceLocations locations, StoredFailureReport? stored, DafnyOptions? options = null) {
    return Restore(locations, stored, options, out _);
  }

  internal static IReadOnlyList<DafnyDiagnostic>? Restore(SourceLocations locations, StoredFailureReport? stored,
    DafnyOptions? options, out IReadOnlyList<DafnyModel>? models) {
    models = null;
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
    if (stored.Models != null) {
      if (options?.ExtractCounterexample != true || stored.Models.Count != stored.Counterexamples) { return null; }
      var index = 0;
      var restored = new List<DafnyModel>();
      for (var i = 0; i < diagnostics.Count; i++) {
        if (diagnostics[i].Level != ErrorLevel.Error) { continue; }
        var entry = stored.Models[index++];
        if (entry == null || PersistentModels.Restore(locations, entry.Model, options) is not { } model) { return null; }
        restored.Add(model);
        if (entry.Render) {
          var diagnostic = diagnostics[i];
          try {
            diagnostics[i] = diagnostic with { MessageParts = new[] { diagnostic.Message + "\nRelated counterexample:\n" + model } };
          } catch (Exception error) when (error is not OutOfMemoryException) { return null; }
        }
      }
      models = restored;
    } else if (options?.ExtractCounterexample == true) { return null; }
    return diagnostics;
  }
}
