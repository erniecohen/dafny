#!/usr/bin/env python3
"""Focused existing run --build comparison; execute only on public scratch CI."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")

def execute(argv, cwd, target, extra_environment=None, timeout=900):
    target.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment.update(extra_environment or {})
    write_json(target / "command.json", {"argv": argv, "cwd": str(cwd),
                                         "extra_environment": extra_environment or {}})
    with (target / "stdout.txt").open("wb") as stdout, (target / "stderr.txt").open("wb") as stderr:
        try:
            result = subprocess.run(argv, cwd=cwd, env=environment, stdout=stdout,
                                    stderr=stderr, timeout=timeout, check=False)
            outcome = {"exit": result.returncode, "timed_out": False}
        except subprocess.TimeoutExpired:
            outcome = {"exit": None, "timed_out": True}
    write_json(target / "result.json", outcome)
    return outcome

def inventory(root):
    return [{"path": str(p.relative_to(root)), "bytes": p.stat().st_size, "sha256": digest(p)}
            for p in sorted(root.rglob("*")) if p.is_file()]

def snapshot(binary_dir, target):
    write_json(target / "inventory.json", inventory(binary_dir))
    for p in binary_dir.glob("*"):
        if p.is_file() and (p.name in ["Dafny.dll", "DafnyCore.dll", "DafnyDriver.dll",
                                      "DafnyRuntime.dll", "TestDafny.dll", "IntegrationTests.dll"] or
                           p.name.endswith((".deps.json", ".runtimeconfig.json"))):
            q = target / "components" / p.name
            q.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(p, q)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--label", choices=["original", "repaired"], required=True)
    parser.add_argument("--source-sha", required=True)
    args = parser.parse_args()
    if not re.fullmatch("[0-9a-f]{40}", args.source_sha):
        raise ValueError("A full immutable source SHA is required")
    repo, output = args.repo.resolve(), args.out.resolve()
    output.mkdir(parents=True, exist_ok=True)
    actual_sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
    if actual_sha != args.source_sha:
        raise ValueError("Source checkout does not match the matrix immutable SHA")
    package = Path(__file__).resolve().parent
    runner = package / "Inputs/github-issue-104-build-boogie.py"
    test_directory = Path("Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues")
    fixture = "github-issue-104-build-boogie.dfy"
    write_json(output / "source-identity.json", {
        "source_sha": actual_sha, "label": args.label, "runner_sha256": digest(runner),
        "scope": "16 existing-language C# CLI path controls; no verification or extended-base flag",
        "runtime_modes": "ordinary and --include-runtime:false; no backend exclusions"})
    shutil.copyfile(runner, output / "runner.py")
    info = execute(["dotnet", "--info"], repo, output / "setup/dotnet-info")
    if info["exit"] != 0:
        return 1
    # The Python fixture executes no solver. Preserve the source harness input
    # for unchanged LitTests initialization; this supplies no proof claim.
    version = re.search(r'DefaultZ3Version = "([^"]+)";',
                        (repo / "Source/DafnyCore/DafnyOptions.cs").read_text()).group(1)
    checksums = {"4.16.0": "87de9c9e86c877abfccb00cf24b946c4a1bda7b9cba6f363ea58311d2573c320"}
    if version not in checksums:
        raise ValueError("Unexpected baseline canonical solver version")
    archive = output / "setup" / ("z3-" + version + ".zip")
    url = ("https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03/"
           "z3-" + version + "-x64-ubuntu-24.04-bin.zip")
    fetch = execute(["curl", "-fsSLo", str(archive), url], repo, output / "setup/solver-download")
    if fetch["exit"] != 0 or digest(archive) != checksums[version]:
        write_json(output / "summary.json", {"capture_complete": False, "solver_download": fetch})
        return 1
    solver_dir = repo / "Binaries/z3/bin"
    solver_dir.mkdir(parents=True, exist_ok=True)
    unpack = execute(["unzip", "-q", str(archive), "-d", str(solver_dir)],
                     repo, output / "setup/solver-unpack")
    if unpack["exit"] != 0:
        return 1
    solver = solver_dir / ("z3-" + version)
    solver.chmod(solver.stat().st_mode | 0o111)
    write_json(output / "setup/solver-input.json", {
        "version": version, "archive_sha256": digest(archive), "binary_sha256": digest(solver),
        "boundary": "source canonical harness input; no verification executed by CLI controls"})
    dependency = None
    dependency_script = repo / "Scripts/fetch-boogie-packages.sh"
    if dependency_script.is_file():
        dependency = execute(["bash", str(dependency_script)], repo, output / "setup/fetch-boogie-packages")
        if dependency["exit"] != 0:
            write_json(output / "summary.json", {"capture_complete": False, "dependency_fetch": dependency})
            return 1
    build = execute(["dotnet", "build", "Source/IntegrationTests", "-c", "Release"],
                    repo, output / "compiler-build")
    if build["exit"] != 0:
        write_json(output / "summary.json", {"capture_complete": False, "compiler_build": build})
        return 1
    binary_dir = repo / "Binaries/net8.0"
    canonical_dir = repo / "Source/IntegrationTests/bin/Release/net8.0"
    assembly = binary_dir / "DafnyDriver.dll"
    if not assembly.is_file() or not (binary_dir / "DafnyRuntime.dll").is_file():
        raise ValueError("Existing canonical Binaries host/runtime is missing")
    snapshot(binary_dir, output / "input-binaries/distribution")
    snapshot(canonical_dir, output / "input-binaries/integration-tests")
    runtime = execute([sys.executable, str(runner), str(assembly), str(output / "cases")],
                      repo, output / "sixteen-controls")
    records = json.loads((output / "cases/results.json").read_text())
    if len(records) != 16:
        raise ValueError("Incomplete sixteen-case denominator")
    path_failures, unexpected = [], []
    for row in records:
        name, errors = row["name"], row["errors"]
        expected_failure = args.label == "original" and "-build-true-boogie-true-" in name
        if expected_failure:
            path_failures.append(name)
        if row["exit"] != 0 or "42\n" not in row["stdout"].replace("\r\n", "\n"):
            unexpected.append(name + ": compiler/Main failure")
        if expected_failure:
            allowed = ("missing requested output ", "generated output beside input")
            if len(errors) != 5 or not all(e.startswith(allowed) for e in errors):
                unexpected.append(name + ": expected only five output-path errors")
        elif errors:
            unexpected.append(name + ": unexpected " + "; ".join(errors))
    expected_runner_exit = 1 if args.label == "original" else 0
    if runtime["exit"] != expected_runner_exit or runtime["timed_out"]:
        unexpected.append("Sixteen-case raw runner result differs from expected actual outcome")
    write_json(output / "sixteen-controls/classification.json", {
        "raw_runner": runtime, "expected_original_path_failures": path_failures,
        "unexpected": unexpected, "case_count": len(records),
        "boundary": "original raw exit1 reproduces the defect; it is not a green regression verdict"})
    canonical, counters = None, None
    if args.label == "repaired":
        for rel in ["Inputs/github-issue-104-build-boogie.py", fixture, fixture + ".expect"]:
            source, expected = repo / test_directory / rel, package / rel
            if not source.is_file() or source.read_bytes() != expected.read_bytes():
                raise ValueError("Registered source/golden differs from reviewed control: " + rel)
        temp = output / "canonical-temporary-files"
        temp.mkdir()
        results = output / "canonical-results"
        canonical = execute(
            ["dotnet", "test", "Source/IntegrationTests", "-c", "Release", "--no-build",
             "--filter", "DisplayName~" + fixture, "--logger", "console;verbosity=normal",
             "--logger", "trx;LogFileName=issue104-build-alias.trx", "--results-directory", str(results)],
            repo, output / "canonical-strict",
            {"DAFNY_INTEGRATION_TESTS_ONLY_COMPILERS": "cs",
             "DAFNY_INTEGRATION_TESTS_UPDATE_EXPECT_FILE": "false", "TMPDIR": str(temp)})
        trx = results / "issue104-build-alias.trx"
        if trx.is_file():
            element = ET.parse(trx).find(".//{*}Counters")
            counters = dict(element.attrib) if element is not None else None
        if canonical["exit"] != 0 or not counters or counters.get("total") != "1" or counters.get("passed") != "1":
            unexpected.append("Repaired strict registration did not pass exactly one selected test")
        write_json(output / "canonical-generated-inventory.json", inventory(temp))
    snapshot(binary_dir, output / "after-binaries/distribution")
    snapshot(canonical_dir, output / "after-binaries/integration-tests")
    execute(["git", "status", "--short"], repo, output / "after-source-status")
    summary = {"source_sha": actual_sha, "label": args.label, "capture_complete": True,
               "compiler_build": build, "dependency_fetch": dependency,
               "actual_external_commands": len(records),
               "all_compiler_exits_zero": all(r["exit"] == 0 for r in records),
               "raw_sixteen_runner": runtime, "original_expected_path_failures": path_failures,
               "strict_registered_harness": canonical, "strict_trx_counters": counters,
               "unexpected": unexpected,
               "boundary": "focused alias evidence, not a full gate or historical runtime-race proof"}
    write_json(output / "summary.json", summary)
    return 1 if unexpected else 0

if __name__ == "__main__":
    sys.exit(main())
