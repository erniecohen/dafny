#!/usr/bin/env python3
"""Probe normal main standard-library archive builds without changing inputs.

Requires Python 3.11 or newer. Run from the baseline repository checkout with
explicit Dafny and Z3 executables and a new output directory. Project resource
and time limits remain unchanged. The second attempt runs only if the default
build returns a nonzero exit code. No archive is copied into the repository.
"""

import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import tomllib
from zipfile import BadZipFile, ZipFile


LIBRARY_PATH = "Source/DafnyStandardLibraries"
CSV_HEADER = [
    "TestResult.DisplayName", "TestResult.Outcome", "TestResult.Duration",
    "TestResult.ResourceCount", "RandomSeed",
]


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def git(cwd, *args):
    return subprocess.check_output(["git", *args], cwd=cwd, text=True).strip()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")


def executable(value):
    path = Path(value).expanduser().resolve(strict=True)
    if not path.is_file() or not os.access(path, os.X_OK):
        raise ValueError(f"Not an executable file: {path}")
    return path


def record_version(command, directory, label):
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=60)
    (directory / f"{label}-version.stdout.txt").write_bytes(result.stdout)
    (directory / f"{label}-version.stderr.txt").write_bytes(result.stderr)
    (directory / f"{label}-version.exit").write_text(f"{result.returncode}\n")
    if result.returncode:
        raise ValueError(f"{label} version command failed with exit {result.returncode}")
    return result.stdout.decode("utf-8", errors="replace").strip()


def resource_summary(path):
    if not path.is_file():
        return {"available": False}
    resources = []
    outcomes = {}
    with path.open(newline="", encoding="utf-8-sig") as stream:
        rows = csv.reader(stream)
        header = next(rows, None)
        if header != CSV_HEADER:
            raise ValueError(f"Unexpected resource CSV header: {header!r}")
        for row in rows:
            if not row:
                continue
            # The producer does not escape commas in display names. Resource
            # count and outcome are among the four fixed trailing columns.
            if len(row) < len(CSV_HEADER):
                raise ValueError(f"Incomplete resource CSV row: {row!r}")
            resource_count = int(row[-2])
            if resource_count < 0:
                raise ValueError("Negative resource count")
            resources.append(resource_count)
            outcome = row[-4]
            outcomes[outcome] = outcomes.get(outcome, 0) + 1
    return {
        "available": True,
        "proof_rows": len(resources),
        "total_resource_units": sum(resources),
        "max_resource_units": max(resources, default=0),
        "outcomes": outcomes,
        "sha256": sha256(path),
    }


def inspect_archive(path, directory):
    if not path.is_file():
        raise ValueError("Successful build did not produce its requested archive")
    with ZipFile(path) as archive:
        manifest_bytes = archive.read("manifest.toml")
        program_bytes = archive.read("program")
    (directory / "manifest.toml").write_bytes(manifest_bytes)
    manifest = tomllib.loads(manifest_bytes.decode("utf-8-sig"))
    options = manifest.get("options", {})
    required = {
        "Dafny 4.11.0": manifest.get("dafny_version") in {"4.11.0", "4.11.0.0"},
        "Z3 solver": manifest.get("solver_identifier") == "Z3",
        "Z3 5.1.0": manifest.get("solver_version") == "5.1.0",
        "verification enabled": options.get("no-verify") is False,
        "hidden bypass disabled": options.get("hidden-no-verify") is False,
    }
    failed = [name for name, passed in required.items() if not passed]
    result = {
        "archive_sha256": sha256(path),
        "program_sha256": hashlib.sha256(program_bytes).hexdigest(),
        "manifest_sha256": hashlib.sha256(manifest_bytes).hexdigest(),
        "manifest_checks": required,
        "manifest": manifest,
    }
    write_json(directory / "archive.json", result)
    if failed:
        raise ValueError("Archive manifest validation failed: " + ", ".join(failed))
    return result


def attempt(label, extra_options, args, library, output):
    directory = output / label
    directory.mkdir()
    archive = directory / "DafnyStandardLibraries.doo"
    csv_path = directory / "resources.csv"
    command = [
        str(args.dafny), "build", "-t:lib", "--hidden-no-verify=false",
        "src/Std/dfyconfig.toml", "--solver-path", str(args.z3),
        "--cores", str(args.cores), "--output", str(archive),
        "--log-format", f"csv;LogFileName={csv_path}",
        *extra_options,
    ]
    write_json(directory / "command.json", {"cwd": str(library), "argv": command})
    print(f"Starting {label} main archive build", flush=True)
    start = time.monotonic()
    timed_out = False
    with (directory / "stdout.txt").open("wb") as stdout, (directory / "stderr.txt").open("wb") as stderr:
        process = subprocess.Popen(command, cwd=library, stdout=stdout, stderr=stderr, start_new_session=True)
        try:
            exit_code = process.wait(timeout=args.timeout)
        except subprocess.TimeoutExpired:
            # This is a safety cap, not a proof-cost measurement. Stop the
            # process group so a timed-out compiler does not leave solvers alive.
            timed_out = True
            import signal
            os.killpg(process.pid, signal.SIGTERM)
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
            exit_code = 124
    (directory / "exit").write_text(f"{exit_code}\n")
    result = {
        "label": label,
        "exit_code": exit_code,
        "timed_out": timed_out,
        "seconds_safety_observation": time.monotonic() - start,
        "archive_present": archive.is_file(),
    }
    try:
        result["resources"] = resource_summary(csv_path)
        if exit_code == 0:
            if not result["resources"].get("proof_rows") or not result["resources"].get("total_resource_units"):
                raise ValueError("Successful build has no resource log proof work")
            result["archive"] = inspect_archive(archive, directory)
            if result["resources"]["outcomes"] != {"Passed": result["resources"]["proof_rows"]}:
                raise ValueError("Successful build resource log contains non-passing proofs")
            result["validated_success"] = True
        else:
            result["validated_success"] = False
    except (OSError, ValueError, KeyError, BadZipFile) as error:
        result["validated_success"] = False
        result["validation_error"] = str(error)
    write_json(directory / "result.json", result)
    print(f"Completed {label}: exit {exit_code}; validated archive {result['validated_success']}", flush=True)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dafny", required=True, type=executable)
    parser.add_argument("--z3", required=True, type=executable)
    parser.add_argument("--output", required=True, type=Path,
                        help="New directory for attempt logs and archives; must not already exist")
    parser.add_argument("--cores", type=int, default=4)
    parser.add_argument("--timeout", type=int, default=5400,
                        help="Per-attempt wall-clock safety cap in seconds; proof cost comes from CSV resource units")
    args = parser.parse_args()
    if args.cores < 1 or args.timeout < 1:
        parser.error("--cores and --timeout must be positive")
    repository = Path(git(Path.cwd(), "rev-parse", "--show-toplevel"))
    library = repository / LIBRARY_PATH
    if not (library / "src/Std/dfyconfig.toml").is_file():
        parser.error("Current checkout does not contain the standard-library project")
    status = git(repository, "status", "--porcelain", "--untracked-files=normal", "--", LIBRARY_PATH)
    if status:
        parser.error("Baseline standard-library inputs are dirty; refusing to probe changed sources")
    output = args.output.expanduser().resolve()
    if output == library or library in output.parents:
        parser.error("--output must be outside the standard-library inputs directory")
    output.mkdir(parents=True, exist_ok=False)
    metadata = {
        "source_revision": git(repository, "rev-parse", "HEAD"),
        "library_tree_revision": git(repository, "rev-parse", f"HEAD:{LIBRARY_PATH}"),
        "library_status_before": status,
        "project_sha256": sha256(library / "src/Std/dfyconfig.toml"),
        "committed_archive_sha256": sha256(library / "binaries/DafnyStandardLibraries.doo"),
        "dafny_executable_sha256": sha256(args.dafny),
        "z3_executable_sha256": sha256(args.z3),
        "cores": args.cores,
        "wall_clock_safety_cap_seconds": args.timeout,
        "resource_and_time_limits": "Unchanged project configuration",
        "probe_script_sha256": sha256(Path(__file__).resolve()),
    }
    write_json(output / "inputs.json", metadata)
    metadata["dafny_version_stdout"] = record_version([str(args.dafny), "--version"], output, "dafny")
    metadata["z3_version_stdout"] = record_version([str(args.z3), "--version"], output, "z3")
    write_json(output / "inputs.json", metadata)
    if not re.search(r"\b4\.11\.0(?:\.0)?(?:\b|\+)", metadata["dafny_version_stdout"]):
        parser.error("Dafny executable must report version 4.11.0")
    if not re.search(r"\bZ3\s+version\s+5\.1\.0\b", metadata["z3_version_stdout"]):
        parser.error("Z3 executable must report version 5.1.0")
    results = [attempt("default", [], args, library, output)]
    if results[0]["exit_code"] != 0:
        results.append(attempt("arith2", ["--solver-option", "O:smt.arith.solver=2"], args, library, output))
    status_after = git(repository, "status", "--porcelain", "--untracked-files=normal", "--", LIBRARY_PATH)
    metadata["library_status_after"] = status_after
    metadata["inputs_unchanged"] = status_after == status
    write_json(output / "inputs.json", metadata)
    summary = {
        "source_revision": metadata["source_revision"],
        "library_tree_revision": metadata["library_tree_revision"],
        "inputs_unchanged": metadata["inputs_unchanged"],
        "attempts": results,
        "successful_attempt": next((r["label"] for r in results if r["validated_success"]), None),
    }
    write_json(output / "summary.json", summary)
    return 0 if summary["successful_attempt"] and metadata["inputs_unchanged"] else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"Probe failed: {error}", file=sys.stderr)
        sys.exit(2)
