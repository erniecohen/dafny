#!/usr/bin/env python3
"""Run serial compiled mutations of the P1 CLI reporting seam; restore every source."""
import argparse
from pathlib import Path
import subprocess

CORE = "Source/DafnyCore/Pipeline/Verification/"
CLI = "Source/DafnyDriver/CliCompilation.cs"
VERIFY = "Source/DafnyDriver/Commands/VerifyCommand.cs"
INTEGRATION = "FullyQualifiedName~NeutralCli|DisplayName~git-issue-118-reporting.dfy"
CORE_TESTS = "FullyQualifiedName~VerificationReportingTests|FullyQualifiedName~BoogieVerificationBackendTests"
CASES = [
    ("default-diagnostic-policy", CORE + "VerificationReporting.cs", "UsesLegacyDiagnosticPolicy => false", "UsesLegacyDiagnosticPolicy => true", "core", 0),
    ("native-diagnostic-policy", CORE + "BoogieVerificationBackend.cs", "UsesLegacyDiagnosticPolicy => true", "UsesLegacyDiagnosticPolicy => false", "core", 0),
    ("legacy-diagnostic-policy", CORE + "VerificationReporting.cs", "&& !usesLegacyPolicy", "", "core", 0),
    ("bounded-counts-not-policy", VERIFY, "IVerificationDiagnosticReporting { UsesLegacyDiagnosticPolicy: true }", "IVerificationAssertionStatistics", "integration", 0),
    ("materialized-diagnostics", CORE + "VerificationReporting.cs", "if (Result.Diagnostics != null)", "if (false)", "core", 0),
    ("lazy-diagnostics", CORE + "VerificationReporting.cs", "diagnostics.ReportDiagnostics(Task, options, name, reporter);", "", "core", 0),
    ("non-success-diagnostics", CORE + "VerificationReporting.cs", "!Result.IsVerified", "false", "core", 0),
    ("per-part-error", CORE + "VerificationReporting.cs", "reporter.ErrorCount == errorsBefore", "reporter.ErrorCount == 0", "core", 0),
    ("native-diagnostics", CORE + "BoogieVerificationBackend.cs", "Compilation.ReportDiagnosticsInResult(options, name,", 'Compilation.ReportDiagnosticsInResult(options, "wrong",', "core", 0),
    ("native-assertions", CORE + "BoogieVerificationBackend.cs", "AssertionCount => NativeResult.Asserts.Count", "AssertionCount => 0", "integration", 0),
    ("native-errors", CORE + "BoogieVerificationBackend.cs", "FailedAssertionCount => NativeResult.CounterExamples.Count", "FailedAssertionCount => 0", "integration", 0),
    ("cli-native-projection", CLI, "BoogieVerificationReporting.GetTaskResult(part.Task, part.Result)", "(VerificationTaskResult)null!", "integration", 0),
    ("cli-delivery", CLI, "new VerificationWorkItemResult(c.Task, c.Result.Result)", "new VerificationWorkItemResult(c.Task, c.Result.Result with { Outcome = VerificationOutcome.Verified })", "integration", 0),
    ("cli-window-delivery", CLI, "new VerificationWorkItemResult(c.Task, c.Result.Result)", "new VerificationWorkItemResult(c.Task, c.Result.Result with { Outcome = VerificationOutcome.Verified })", "integration", 1),
    ("summary-outcome", VERIFY, "switch (runResult.Outcome)", "switch (VerificationOutcome.Verified)", "integration", 0),
    ("summary-resource", VERIFY, "(int)resources", "0", "integration", 0),
    ("summary-max-resource", VERIFY, "(int)resources", "0", "integration", 1),
    ("summary-missing-resources", VERIFY, "Interlocked.Exchange(ref missingResources, 1);", "", "integration", 0),
    ("summary-missing-counts", VERIFY, "Interlocked.Exchange(ref missingAssertionCounts, 1);", "", "integration", 0),
    ("summary-assertions", VERIFY, "counts.AssertionCount - counts.FailedAssertionCount", "counts.AssertionCount", "integration", 0),
    ("summary-errors", VERIFY, "counts?.FailedAssertionCount ?? 1", "0", "integration", 0),
    ("cli-diagnostics", VERIFY, "completed.ReportDiagnostics(compilation.Options, result.CanVerify.FullDafnyName, batchReporter);", "", "integration", 0),
]


def run(project, selected, log):
    command = ["dotnet", "test", project, "-c", "Release", "-m:1", "-p:NuGetAudit=false",
               "--settings", ".github/review/ci.runsettings", "--filter", selected]
    with log.open("w") as output:
        result = subprocess.run(command, stdout=output, stderr=subprocess.STDOUT)
    return result.returncode, log.read_text()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--case", action="append")
    args = parser.parse_args()
    args.work.mkdir(parents=True, exist_ok=False)
    for name, filename, old, new, kind, occurrence in CASES:
        if args.case and name not in args.case:
            continue
        path = Path(filename)
        original = path.read_bytes()
        source = original.decode()
        positions = [i for i in range(len(source)) if source.startswith(old, i)]
        assert len(positions) > occurrence, (name, "missing mutation site")
        position = positions[occurrence]
        try:
            path.write_text(source[:position] + new + source[position + len(old):])
            project, selected = ("Source/DafnyCore.Test", CORE_TESTS) if kind == "core" else ("Source/IntegrationTests", INTEGRATION)
            code, output = run(project, selected, args.work / (name + ".log"))
            assert code != 0 and "Failed!" in output and "error CS" not in output, (name, "not killed by executing controls")
            print(name + ": killed", flush=True)
        finally:
            path.write_bytes(original)
    for project, selected in [("Source/DafnyCore.Test", CORE_TESTS), ("Source/IntegrationTests", INTEGRATION)]:
        code, _ = run(project, selected, args.work / (Path(project).name + "-restored.log"))
        assert code == 0, "restored controls failed"
    print("restored controls: passed", flush=True)


if __name__ == "__main__":
    main()
