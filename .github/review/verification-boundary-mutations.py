#!/usr/bin/env python3
"""Serial counterfactual controls for the P1 work/preparation seam.

Run from the repository root with no concurrent builds or tests in this checkout.
The source is restored after each counterfactual, including on failure.
"""
import argparse
from pathlib import Path
import subprocess

ROOT = Path("Source/DafnyCore/Pipeline/Verification")
CASES = [
    ("default-outcome", "VerificationWork.cs", "ToolError, Verified", "Verified, ToolError"),
    ("proof-success", "VerificationWork.cs", "Outcome == VerificationOutcome.Verified", "true"),
    ("preparation-access", "VerificationWork.cs", "if (Error != null)", "if (false)"),
    ("preparation-outcome", "VerificationWork.cs", "if (outcome == VerificationOutcome.Verified)", "if (false)"),
    ("null-preparation-error", "VerificationWork.cs", "ArgumentNullException.ThrowIfNull(error);", ""),
    ("admission", "BoogieVerificationBackend.cs",
     "VerificationPreparation.Failed(VerificationOutcome.ToolError, error)", "VerificationPreparation.Ready([])"),
    ("invalid-outcome", "BoogieVerificationBackend.cs",
     "SolverOutcome.Invalid => VerificationOutcome.Failed", "SolverOutcome.Invalid => VerificationOutcome.Verified"),
    ("bounded-outcome", "BoogieVerificationBackend.cs",
     "SolverOutcome.Bounded => VerificationOutcome.Bounded", "SolverOutcome.Bounded => VerificationOutcome.Verified"),
    ("unknown-outcome", "BoogieVerificationBackend.cs",
     "_ => VerificationOutcome.ToolError", "_ => VerificationOutcome.Verified"),
    ("seed", "BoogieVerificationBackend.cs", "NativeTask.FromSeed(seed)", "NativeTask"),
    ("cancel", "BoogieVerificationBackend.cs", "public void Cancel() => NativeTask.Cancel();", "public void Cancel() { }"),
    ("status", "BoogieVerificationBackend.cs", "Queued => new VerificationWorkQueued()", "Queued => new VerificationWorkRunning()"),
    ("invalid-status", "BoogieVerificationBackend.cs",
     'throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown Boogie verification status")',
     "new VerificationWorkStale()"),
    ("metrics", "BoogieVerificationBackend.cs", "new VerificationMetrics(completed.Result.ResourceCount,", "new VerificationMetrics(0,"),
    ("native-evidence", "BoogieVerificationBackend.cs", "new BoogieVerificationResult(completed.Result)",
     "new BoogieVerificationResult(completed.Result with { ResourceCount = 0 })"),
    ("foreign-work", "BoogieVerificationBackend.cs",
     'throw new InvalidOperationException("Expected Boogie verification work")', "null!"),
    ("cache-wrap", "BoogieVerificationBackend.cs", "if (resultCache != null)", "if (false)"),
    ("pipeline", "Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs",
     "var tasks = BoogieVerificationBackend.GetNativeTasks(prepared.Preparation);",
     "var tasks = Array.Empty<IVerificationTask>();"),
    ("capture", "VerificationPreparationPipeline.cs", "capture?.Invoke(boogieProgram);", ""),
    ("checksums", "VerificationPreparationPipeline.cs", "InsertChecksums = insertChecksums", "InsertChecksums = false"),
    ("translation-cache", "VerificationPreparationPipeline.cs",
     "if (program.Options.Get(CommonOptionBag.TranslationCache))", "if (false)"),
    ("dependency-owner", "VerificationPreparationPipeline.cs",
     "new ModuleVerificationPreparation(preparation, dependencies)",
     "new ModuleVerificationPreparation(preparation, new ProofDependencyManager())"),
    ("shared-serialization", "VerificationPreparationPipeline.cs", "await mutex.WaitAsync(cancellationToken);", ""),
    ("backend-failure", "VerificationPreparationPipeline.cs",
     "return new ModuleVerificationPreparation(preparation, dependencies);",
     "return new ModuleVerificationPreparation(VerificationPreparation.Ready([]), dependencies);"),
    ("profile-failure", "VerificationPreparationPipeline.cs",
     "preparation.IsReady ? preparation.GetWorkOrThrow().Count : (int?)null",
     "preparation.GetWorkOrThrow().Count"),
]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--case", action="append")
    args = parser.parse_args()
    args.work.mkdir(parents=True, exist_ok=False)
    test = ["dotnet", "test", "Source/DafnyCore.Test", "-c", "Release", "-m:1",
            "--settings", ".github/review/ci.runsettings", "--filter",
            "FullyQualifiedName~VerificationWorkTests|FullyQualifiedName~BoogieVerificationBackendTests|FullyQualifiedName~VerificationPreparationPipelineTests"]
    for name, filename, old, new in CASES:
        if args.case and name not in args.case:
            continue
        path = Path(filename) if "/" in filename else ROOT / filename
        original = path.read_bytes()
        source = original.decode()
        assert source.count(old) == 1, (name, "mutation site is not unique")
        try:
            path.write_text(source.replace(old, new))
            case_test = test if name != "pipeline" else [
                "dotnet", "test", "Source/IntegrationTests", "-c", "Release", "-m:1",
                "--settings", ".github/review/ci.runsettings", "--filter", "DisplayName~git-issue-118.dfy"]
            with (args.work / (name + ".log")).open("w") as log:
                result = subprocess.run(case_test, stdout=log, stderr=subprocess.STDOUT)
            output = (args.work / (name + ".log")).read_text()
            assert result.returncode != 0 and "Failed!" in output and "error CS" not in output, (name, "not killed by a running test")
            print(name + ": killed", flush=True)
        finally:
            path.write_bytes(original)
    # Leave executable outputs corresponding to the restored source.
    subprocess.run(test, check=True)


if __name__ == "__main__":
    main()
