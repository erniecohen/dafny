#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.Boogie;
using VC;

namespace Microsoft.Dafny.Caching;

/// <summary>The assertion fields consumed by the ordinary JSON verification report.</summary>
public sealed record VerificationAssertionReport(string? Filename, int Line, int Column, string? Description);

internal enum ReportFilename { Literal, Basename, Path, Uri }

internal sealed record ReportPosition(
  [property: JsonRequired] string Anchor, [property: JsonRequired] string File,
  [property: JsonRequired] int Line, [property: JsonRequired] int Column, [property: JsonRequired] int Offset,
  [property: JsonRequired] ReportFilename Filename, [property: JsonRequired] string? Literal);

internal sealed record StoredAssertionReport(
  [property: JsonRequired] ReportPosition Position, [property: JsonRequired] string? Description);

internal static class PersistentAssertionReports {
  // Dependency-aware reports still need the fresh proof-dependency manager and are excluded.
  public static List<StoredAssertionReport>? Capture(SourceLocations locations, VerificationRunResult result) {
    try { return CaptureCore(locations, result); }
    catch (Exception error) when (error is not OutOfMemoryException) { return null; }
  }

  private static List<StoredAssertionReport>? CaptureCore(SourceLocations locations, VerificationRunResult result) {
    if (result.CoveredElements.Any()) { return null; }
    var assertions = new List<StoredAssertionReport>(result.Asserts.Count);
    foreach (var assertion in result.Asserts) {
      if (assertion.tok == null || assertion.Description == null || !locations.PortableReportText(assertion.Description.SuccessDescription) ||
          locations.Report(assertion.tok) is not { } position) { return null; }
      assertions.Add(new StoredAssertionReport(position, assertion.Description.SuccessDescription));
    }
    return assertions;
  }

  public static IReadOnlyList<VerificationAssertionReport>? Restore(SourceLocations locations,
    List<StoredAssertionReport>? assertions, int count) {
    if (assertions == null || assertions.Count != count) { return null; }
    var result = new List<VerificationAssertionReport>(count);
    foreach (var assertion in assertions) {
      if (assertion?.Position == null || locations.Report(assertion.Position, assertion.Description) is not { } current) {
        return null;
      }
      result.Add(current);
    }
    return result;
  }
}
