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
    ("stream-scheduling", "Source/DafnyCore/Pipeline/Compilation.cs",
     "VerificationWorkStream.Validate(statusUpdates).Subscribe(", "statusUpdates.Subscribe("),
    ("stream-terminal-count", "VerificationWorkStream.cs", "if (terminal != null)", "if (false)"),
    ("stream-result-validation", "VerificationWorkStream.cs",
     "completed.Result == null || !Enum.IsDefined(completed.Result.Outcome)", "false"),
    ("stream-result-delay", "VerificationWorkStream.cs", "terminal = completed;",
     "observer.OnNext(completed); terminal = completed;"),
    ("stream-status-kind", "VerificationWorkStream.cs",
     'Fail(new InvalidOperationException("Verification work emitted an unknown status"));',
     "observer.OnNext(status);"),
    ("stream-empty", "VerificationWorkStream.cs",
     'Fail(new InvalidOperationException("Verification work completed without a terminal result"));',
     "observer.OnCompleted();"),
    ("stream-error", "VerificationWorkStream.cs", "if (!stopped) { Fail(error); }",
     'if (!stopped) { Fail(new Exception("replaced error")); }'),
    ("stream-forward", "VerificationWorkStream.cs", "observer.OnNext(terminal);", ""),
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
     "preparationPipeline.PrepareAsync(resolution, moduleDefinition, request.InsertChecksums,\n        request.CreateBackend, cancellationToken, request.Capture)",
     "Task.FromResult(new ModuleVerificationPreparation(VerificationPreparation.Ready([]), new ProofDependencyManager()))"),
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
    ("native-request-checksums", "BoogieVerificationBackend.cs", "0 < engine.Options.VerifySnapshots", "false"),
    ("native-request-cache", "BoogieVerificationBackend.cs",
     "new BoogieVerificationBackend(engine, TaskCacheSession.For(resolution.ResolvedProgram.Options))",
     "new BoogieVerificationBackend(engine)"),
    ("native-request-print", "BoogieVerificationBackend.cs", "if (engine.Options.PrintFile != null)", "if (false)"),
    ("source-owner", "BoogieVerificationBackend.cs",
     "CanVerify = (NativeTask.ScopeToken as CanVerifyOrigin)?.CanVerify", "CanVerify = null"),
    ("refinement-metadata", "BoogieVerificationBackend.cs",
     "IsReverifiedInheritedCheck = (NativeTask.ScopeToken as CanVerifyOrigin)?.IsReverifiedInheritedCheck ?? false",
     "IsReverifiedInheritedCheck = false"),
    ("hidden-functions", "BoogieVerificationBackend.cs",
     "HiddenFunctions => NativeTask.Split.HiddenFunctions.Select(f => f.tok).\n    OfType<FromDafnyNode>().Select(n => n.Node).OfType<Function>().ToList()", "HiddenFunctions => []"),
    ("report-task", "BoogieVerificationBackend.cs", "native ? native.NativeTask :", "native ? null! :"),
    ("report-result", "BoogieVerificationBackend.cs", "native ? native.NativeResult :", "native ? null! :"),
    ("foreign-report-task", "BoogieVerificationBackend.cs",
     'throw new InvalidOperationException("Boogie reporting requires native task evidence")', "null!"),
    ("foreign-report-result", "BoogieVerificationBackend.cs",
     'throw new InvalidOperationException("Boogie reporting requires native result evidence")', "null!"),
    ("schedule-request", "Source/DafnyCore/Pipeline/Compilation.cs",
     "createPreparationRequest(resolution, containingModule)",
     "new VerificationPreparationRequest(false, () => throw new InvalidOperationException())"),
    ("schedule-failure", "Source/DafnyCore/Pipeline/Compilation.cs",
     "result.Preparation.GetWorkOrThrow().GroupBy(GetCanVerify)", "Array.Empty<IVerificationWork>().GroupBy(GetCanVerify)"),
    ("schedule-filter", "Source/DafnyCore/Pipeline/Compilation.cs", "tasks.Where(taskFilter)", "tasks"),
    ("schedule-seed", "Source/DafnyCore/Pipeline/Compilation.cs", "task.FromSeed(randomSeed.Value)", "task"),
    ("schedule-cache", "Source/DafnyCore/Pipeline/Compilation.cs",
     "HandleStatusUpdate(canVerify, task, completedCache);", ""),
    ("schedule-status", "Source/DafnyCore/Pipeline/Compilation.cs", "HandleStatusUpdate(canVerify, task, update);", ""),
    ("schedule-cancel", "Source/DafnyCore/Pipeline/Compilation.cs", "foreach (var (_, tasks) in tasksPerVerifiable) {\n      foreach (var task in tasks) {\n        task.Cancel();\n      }\n    }", ""),
    ("schedule-owner-guard", "Source/DafnyCore/Pipeline/Compilation.cs",
     'throw new InvalidOperationException("Verification work has no source owner")', "null!"),
    ("task-evidence", "BoogieVerificationBackend.cs", "Payload => new BoogieVerificationTask(NativeTask)", "Payload => null!"),
    ("schedule-name", "Source/DafnyCore/Pipeline/Compilation.cs", "task.Source.Id.Scope + task.Source.Id.Part", "task.Source.Id.Scope"),
    ("schedule-ranges", "Source/DafnyCore/Pipeline/Compilation.cs",
     "currentGroupRange.Intersects(currentTaskRange)", "false"),
    ("schedule-exception", "Source/DafnyCore/Pipeline/Compilation.cs",
     "updates.OnNext(new VerificationException(canVerify, task, e));", ""),
    ("refinement-filter", "Source/DafnyCore/Pipeline/Compilation.cs",
     "!g.Key.Origin.IsCopy || t.Source.IsReverifiedInheritedCheck", "!g.Key.Origin.IsCopy"),
    ("cli-filter", "Source/DafnyDriver/CliCompilation.cs",
     "range.Contains(task.Source.ScopeOrigin.line) || range.Contains(task.Source.Origin.line)", "true"),
    ("cli-completion", "Source/DafnyDriver/CliCompilation.cs",
     "canVerifyResult.CompletedParts.Enqueue((verificationUpdate.VerificationTask, completed));", ""),
    ("ide-outcome", "Source/DafnyLanguageServer/Workspace/IdeState.cs", "completed.Result.IsVerified", "true"),
    ("profile-resource", "Source/DafnyCore/Pipeline/PipelineProfile.cs",
     '["resourceCount"] = result.Metrics?.ResourceCount', '["resourceCount"] = 0'),
    ("profile-label", "Source/DafnyCore/Pipeline/PipelineProfile.cs",
     'result.OutcomeLabel',
     'result.Outcome.ToString()'),
]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--case", action="append")
    args = parser.parse_args()
    args.work.mkdir(parents=True, exist_ok=False)
    test = ["dotnet", "test", "Source/DafnyCore.Test", "-c", "Release", "-m:1", "-p:NuGetAudit=false",
            "--settings", ".github/review/ci.runsettings", "--filter",
            "FullyQualifiedName~VerificationWorkTests|FullyQualifiedName~VerificationWorkStreamTests|FullyQualifiedName~BoogieVerificationBackendTests|FullyQualifiedName~VerificationPreparationPipelineTests|FullyQualifiedName~VerificationSchedulingTests"]
    for name, filename, old, new in CASES:
        if args.case and name not in args.case:
            continue
        path = Path(filename) if "/" in filename else ROOT / filename
        original = path.read_bytes()
        source = original.decode()
        assert source.count(old) == 1, (name, "mutation site is not unique")
        try:
            path.write_text(source.replace(old, new))
            case_test = test
            if name in ("pipeline", "refinement-filter", "cli-filter", "cli-completion"):
                selected = "DisplayName~github-issue-404.dfy" if name == "refinement-filter" else (
                    "FullyQualifiedName~OneResolutionFreshRequestsAndNextInvocationReloadsSources" if name == "cli-filter" else "DisplayName~git-issue-118.dfy")
                case_test = ["dotnet", "test", "Source/IntegrationTests", "-c", "Release", "-m:1", "-p:NuGetAudit=false",
                             "--settings", ".github/review/ci.runsettings", "--filter", selected]
            elif name == "ide-outcome":
                case_test = ["dotnet", "test", "Source/DafnyLanguageServer.Test", "-c", "Release", "-m:1", "-p:NuGetAudit=false",
                             "--settings", ".github/review/ci.runsettings", "--filter",
                             "FullyQualifiedName~StatusesOfDifferentImplementationUnderOneNamedVerifiableAreCorrectlyMerged"]
            with (args.work / (name + ".log")).open("w") as log:
                result = subprocess.run(case_test, stdout=log, stderr=subprocess.STDOUT)
            output = (args.work / (name + ".log")).read_text()
            assert result.returncode != 0 and "Failed!" in output and "error CS" not in output, (name, "not killed by a running test")
            print(name + ": killed", flush=True)
        finally:
            path.write_bytes(original)
    # Leave executable outputs corresponding to the restored source.
    with (args.work / "restored.log").open("w") as log:
        subprocess.run(test, check=True, stdout=log, stderr=subprocess.STDOUT)
    print("restored controls: passed", flush=True)


if __name__ == "__main__":
    main()
