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
    ("native-closure-audit", "BoogieVerificationBackend.cs",
     "VerificationWorkStream.Audit(source)", "source"),
    ("native-null-run", "BoogieVerificationBackend.cs",
     "source == null ? null : VerificationWorkStream.Audit(source)", "VerificationWorkStream.Audit(source!)"),
    ("native-immediate-publication", "VerificationWorkStream.cs",
     "if (publishBeforeClosure) { observer.OnNext(terminal); }", ""),
    ("closure-required-for-success", "VerificationRun.cs", "IsClosed && failures.Count == 0", "failures.Count == 0"),
    ("closure-all-identities", "VerificationRun.cs",
     "if (expected.Keys.All(id => closures.ContainsKey(id) || failures.ContainsKey(id)))",
     "if (expected.Keys.Any(id => closures.ContainsKey(id) || failures.ContainsKey(id)))"),
    ("closure-expected-count", "VerificationRun.cs",
     "return closures.Count == expected.Count;", "return closures.Count > 0;"),
    ("closure-result", "VerificationRun.cs", "!completed.ContainsKey(id)", "false"),
    ("closure-error", "VerificationRun.cs",
     "!Enum.IsDefined(kind) || !completed.ContainsKey(id) || failures.ContainsKey(id)",
     "!Enum.IsDefined(kind) || !completed.ContainsKey(id)"),
    ("closure-repeat", "VerificationRun.cs", "!closures.TryAdd(id, kind)", "(!closures.TryAdd(id, kind) && false)"),
    ("closure-kind", "VerificationRun.cs", "!Enum.IsDefined(kind)", "false"),
    ("closure-identity", "VerificationRun.cs",
     "var id = Check(work);\n      if (!Enum.IsDefined(kind)", "var id = work.Source.Id;\n      if (!Enum.IsDefined(kind)"),
    ("closure-snapshot", "VerificationRun.cs",
     "return new Dictionary<VerificationWorkId, VerificationCompletionKind>(closures);", "return closures;"),
    ("closure-audit-completion", "VerificationRun.cs", "auditCompletion.TrySetResult();", ""),
    ("schedule-stream-closure", "Source/DafnyCore/Pipeline/Compilation.cs",
     "run.Close(task, VerificationCompletionKind.StreamClosed);", ""),
    ("schedule-cache-closure", "Source/DafnyCore/Pipeline/Compilation.cs",
     "run.Close(task, VerificationCompletionKind.CachedResult);", "run.Close(task, VerificationCompletionKind.StreamClosed);"),
    ("closure-empty-settlement", "VerificationRun.cs",
     "    FinishAudit();\n  }\n\n  private VerificationWorkId Check", "  }\n\n  private VerificationWorkId Check"),
    ("closure-error-settlement", "VerificationRun.cs",
     "      failures.TryAdd(id, new(error is OperationCanceledException ? VerificationOutcome.Cancelled : VerificationOutcome.ToolError, error));\n      FinishAudit();",
     "      failures.TryAdd(id, new(error is OperationCanceledException ? VerificationOutcome.Cancelled : VerificationOutcome.ToolError, error));"),
    ("closure-stream-settlement", "VerificationRun.cs",
     "      FinishAudit();\n    }\n  }\n\n  private void FinishAudit()", "    }\n  }\n\n  private void FinishAudit()"),
    ("ide-accounting-events", "Source/DafnyLanguageServer/Workspace/IdeState.cs",
     "case ModuleVerificationWorkIdentified:\n      case VerificationRunStarted:\n        return this;", ""),
    ("manifest-identity", "VerificationRun.cs",
     "!identities.TryAdd(source.Id, task)", "!(identities.TryAdd(source.Id, task) || identities.ContainsKey(source.Id))"),
    ("manifest-owner", "VerificationRun.cs",
     "source.CanVerify == null || !ReferenceEquals(source.CanVerify.ContainingModule, module)", "false"),
    ("manifest-selection", "VerificationRun.cs",
     "!Expected.TryGetValue(task.Source.Id, out var original) || !ReferenceEquals(original.Source.CanVerify, owner)", "false"),
    ("run-identity", "VerificationRun.cs",
     "!ReferenceEquals(work.Source.CanVerify, Owner) || !expected.TryGetValue(id, out var task) || !ReferenceEquals(task, work)", "false"),
    ("run-outcome", "VerificationRun.cs",
     "!Enum.IsDefined(result.Outcome)", "false"),
    ("run-repeat", "VerificationRun.cs",
     "!completed.TryAdd(id, result)", "(!completed.TryAdd(id, result) && false)"),
    ("run-non-success", "VerificationRun.cs",
     "completed.Values.All(r => r.IsVerified)", "true"),
    ("run-error", "VerificationRun.cs",
     "failures.TryAdd(id, new(error is OperationCanceledException ? VerificationOutcome.Cancelled : VerificationOutcome.ToolError, error));", ""),
    ("run-cancel", "VerificationRun.cs",
     "error is OperationCanceledException ? VerificationOutcome.Cancelled : VerificationOutcome.ToolError", "VerificationOutcome.ToolError"),
    ("run-snapshot", "VerificationRun.cs",
     "return new Dictionary<VerificationWorkId, VerificationWorkResult>(completed);", "return completed;"),
    ("manifest-scheduling", "Source/DafnyCore/Pipeline/Compilation.cs",
     "VerificationWorkManifest.Check(containingModule, work)", "VerificationWorkManifest.Check(containingModule, [])"),
    ("run-seed", "Source/DafnyCore/Pipeline/Compilation.cs",
     "seeded.Source.Id != task.Source.Id || !ReferenceEquals(seeded.Source.CanVerify, task.Source.CanVerify)", "false"),
    ("run-started", "Source/DafnyCore/Pipeline/Compilation.cs",
     "updates.OnNext(new VerificationRunStarted(run));", ""),
    ("run-complete", "Source/DafnyCore/Pipeline/Compilation.cs",
     "run.Complete(verificationTask, completed.Result);", ""),
    ("run-failure", "Source/DafnyCore/Pipeline/Compilation.cs",
     "run.Fail(task, error);", ""),
    ("cli-request", "Source/DafnyDriver/CliCompilation.cs",
     "ReferenceEquals(canVerifyResult.Run, verificationException.Run)", "true"),
    ("cli-identity", "Source/DafnyDriver/CliCanVerifyState.cs",
     "Run == null || !Run.HasResult(task, result.Result) || !received.Add(task.Source.Id)", "Run == null || !received.Add(task.Source.Id)"),
    ("cli-repeat", "Source/DafnyDriver/CliCanVerifyState.cs",
     "!received.Add(task.Source.Id)", "(!received.Add(task.Source.Id) && false)"),
    ("stream-scheduling", "Source/DafnyCore/Pipeline/Compilation.cs",
     "task.TryRunForScheduling()", "task.TryRun()"),
    ("stream-default", "VerificationWork.cs", "VerificationWorkStream.Validate(source)", "source"),
    ("stream-native-timing", "BoogieVerificationBackend.cs",
     "VerificationWorkStream.Audit(source)", "VerificationWorkStream.Validate(source)"),
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
    ("stream-forward", "VerificationWorkStream.cs", "if (!publishBeforeClosure) { observer.OnNext(terminal); }", ""),
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
     "work.GroupBy(GetCanVerify)", "Array.Empty<IVerificationWork>().GroupBy(GetCanVerify)"),
    ("schedule-filter", "Source/DafnyCore/Pipeline/Compilation.cs", "tasks.Where(taskFilter)", "tasks"),
    ("schedule-seed", "Source/DafnyCore/Pipeline/Compilation.cs", "task.FromSeed(randomSeed.Value)", "task"),
    ("schedule-cache", "Source/DafnyCore/Pipeline/Compilation.cs",
     "HandleStatusUpdate(canVerify, task, completedCache, run);", ""),
    ("schedule-status", "Source/DafnyCore/Pipeline/Compilation.cs", "HandleStatusUpdate(canVerify, task, update, run);", ""),
    ("schedule-cancel", "Source/DafnyCore/Pipeline/Compilation.cs", "foreach (var (_, tasks) in tasksPerVerifiable) {\n      foreach (var task in tasks) {\n        task.Cancel();\n      }\n    }", ""),
    ("schedule-owner-guard", "Source/DafnyCore/Pipeline/Compilation.cs",
     'throw new InvalidOperationException("Verification work has no source owner")', "null!"),
    ("task-evidence", "BoogieVerificationBackend.cs", "Payload => new BoogieVerificationTask(NativeTask)", "Payload => null!"),
    ("schedule-name", "Source/DafnyCore/Pipeline/Compilation.cs", "task.Source.Id.Scope + task.Source.Id.Part", "task.Source.Id.Scope"),
    ("schedule-ranges", "Source/DafnyCore/Pipeline/Compilation.cs",
     "currentGroupRange.Intersects(currentTaskRange)", "false"),
    ("schedule-exception", "Source/DafnyCore/Pipeline/Compilation.cs",
     "updates.OnNext(new VerificationException(canVerify, task, error) { Run = run });", ""),
    ("refinement-filter", "Source/DafnyCore/Pipeline/Compilation.cs",
     "!g.Key.Origin.IsCopy || t.Source.IsReverifiedInheritedCheck", "!g.Key.Origin.IsCopy"),
    ("cli-filter", "Source/DafnyDriver/CliCompilation.cs",
     "range.Contains(task.Source.ScopeOrigin.line) || range.Contains(task.Source.Origin.line)", "true"),
    ("cli-completion", "Source/DafnyDriver/CliCanVerifyState.cs",
     "CompletedParts.Enqueue((task, result));", ""),
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
            "FullyQualifiedName~VerificationRunTests|FullyQualifiedName~VerificationWorkTests|FullyQualifiedName~VerificationWorkStreamTests|FullyQualifiedName~BoogieVerificationBackendTests|FullyQualifiedName~VerificationPreparationPipelineTests|FullyQualifiedName~VerificationSchedulingTests"]
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
            if name in ("pipeline", "refinement-filter", "cli-filter", "cli-completion", "cli-request", "cli-identity", "cli-repeat"):
                selected = "DisplayName~github-issue-404.dfy" if name == "refinement-filter" else (
                    "FullyQualifiedName~OneResolutionFreshRequestsAndNextInvocationReloadsSources" if name == "cli-filter" else "DisplayName~git-issue-118.dfy")
                if name in ("cli-request", "cli-identity", "cli-repeat"):
                    selected = "FullyQualifiedName~LateErrorsBelong|FullyQualifiedName~OneResolutionFresh"
                case_test = ["dotnet", "test", "Source/IntegrationTests", "-c", "Release", "-m:1", "-p:NuGetAudit=false",
                             "--settings", ".github/review/ci.runsettings", "--filter", selected]
            elif name in ("ide-outcome", "ide-accounting-events"):
                case_test = ["dotnet", "test", "Source/DafnyLanguageServer.Test", "-c", "Release", "-m:1", "-p:NuGetAudit=false",
                             "--settings", ".github/review/ci.runsettings", "--filter",
                             "FullyQualifiedName~RefinedLemmaNewEnsuresIsVerified" if name == "ide-accounting-events" else
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
