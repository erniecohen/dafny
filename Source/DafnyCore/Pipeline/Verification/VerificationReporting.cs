#nullable enable
namespace Microsoft.Dafny;

/// <summary>Optional assertion counts. Missing counts do not mean zero checked assertions.</summary>
public interface IVerificationAssertionStatistics {
  int AssertionCount { get; }
  int FailedAssertionCount { get; }
}

/// <summary>Optional lazy diagnostic materialization, owned by the backend's result evidence.</summary>
public interface IVerificationDiagnosticReporting {
  /// <summary>Reporting compatibility only; this never changes the result's proof outcome.</summary>
  bool UsesLegacyDiagnosticPolicy => false;
  void ReportDiagnostics(IVerificationWork work, DafnyOptions options, string name, ErrorReporter reporter);
}

/// <summary>A delivered work/result pair; delivery is separate from the request's closure audit.</summary>
public record VerificationWorkItemResult(IVerificationWork Task, VerificationWorkResult Result) {
  public void ReportDiagnostics(DafnyOptions options, string name, ErrorReporter reporter) {
    var errorsBefore = reporter.ErrorCount;
    if (Result.Diagnostics != null) {
      foreach (var diagnostic in Result.Diagnostics) { reporter.MessageCore(diagnostic); }
    } else if (Result.Payload is IVerificationDiagnosticReporting diagnostics) {
      diagnostics.ReportDiagnostics(Task, options, name, reporter);
    }

    var usesLegacyPolicy = Result.Payload is IVerificationDiagnosticReporting { UsesLegacyDiagnosticPolicy: true };
    if (!Result.IsVerified && !usesLegacyPolicy && reporter.ErrorCount == errorsBefore) {
      reporter.Error(MessageSource.Verifier, Task.Source.Origin,
        $"Verification of {name} did not succeed ({Result.Outcome})");
    }
  }
}
