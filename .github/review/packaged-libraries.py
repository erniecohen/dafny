#!/usr/bin/env python3
"""Check the published executable's embedded general and C# libraries.

Uses the caller's pinned solver and ordinary CLI verification limits. Retains
stdout, stderr, proof-resource CSVs and a small result record for each command.
"""

import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import shutil
import subprocess

CSV_HEADER = ["TestResult.DisplayName", "TestResult.Outcome", "TestResult.Duration",
              "TestResult.ResourceCount", "RandomSeed"]


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(dafny, solver, fixture, output, target):
    directory = output / target
    directory.mkdir(parents=True, exist_ok=True)
    client = directory / fixture.name
    shutil.copyfile(fixture, client)
    resources = directory / "resources.csv"
    side_effect = directory / "package-smoke.bin"
    for path in (resources, side_effect):
        path.unlink(missing_ok=True)
    action = ["verify"] if target == "notarget" else ["run", "--target", target]
    argv = [str(dafny), *action, "--standard-libraries=true", "--solver-path", str(solver),
            "--cores", "2", "--log-format", "csv;LogFileName=" + str(resources), str(client)]
    with (directory / "stdout.txt").open("wb") as stdout, (directory / "stderr.txt").open("wb") as stderr:
        process = subprocess.Popen(argv, cwd=directory, stdout=stdout, stderr=stderr,
                                   start_new_session=True)
        try:
            exit_code = process.wait(timeout=300)
        except subprocess.TimeoutExpired:
            # The session belongs only to this invocation and its solver/runtime children.
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                pass
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            process.wait()
            exit_code = 124
    result = {"command": argv, "exit_code": exit_code, "errors": []}
    if exit_code != 0:
        result["errors"].append("Command did not finish successfully")
    if (directory / "stderr.txt").read_bytes():
        result["errors"].append("Unexpected stderr")
    text = (directory / "stdout.txt").read_text(encoding="utf-8")
    lines = [line for line in text.splitlines() if line.strip()]
    summaries = [line for line in lines if re.fullmatch(
        r"Dafny program verifier finished with [1-9][0-9]* verified, 0 errors", line)]
    reports = ["Results File: " + str(resources)]
    actual_output = [line for line in lines if line not in summaries + reports]
    expected_output = [] if target == "notarget" else ["7", "file ok"]
    if len(summaries) != 1 or lines.count(reports[0]) != 1 or actual_output != expected_output:
        result["errors"].append("Unexpected verifier or runtime output")
    result["runtime_output"] = actual_output
    try:
        with resources.open(newline="", encoding="utf-8-sig") as stream:
            reader = csv.reader(stream)
            if next(reader, None) != CSV_HEADER:
                raise ValueError("Unexpected CSV header")
            rows = list(reader)
        if not rows or any(len(row) != len(CSV_HEADER) or row[1] != "Passed" for row in rows):
            raise ValueError("Expected nonempty CSV containing only Passed proof rows")
        counts = [int(row[3]) for row in rows]
        if any(count < 0 for count in counts):
            raise ValueError("Negative proof-resource count")
        result["proof_rows"] = len(rows)
        result["resource_units"] = sum(counts)
    except (OSError, ValueError) as error:
        result["errors"].append(str(error))
    if target == "cs" and (not side_effect.is_file() or side_effect.read_bytes() != bytes([1, 2, 3])):
        result["errors"].append("Missing or incorrect file bytes")
    result["passed"] = not result["errors"]
    (directory / "result.json").write_text(json.dumps(result, indent=2) + "\n")
    print(f"Packaged libraries ({target}): {'PASS' if result['passed'] else 'FAIL'}")
    if not result["passed"]:
        print(text)
        print((directory / "stderr.txt").read_text(encoding="utf-8"))
        print("\n".join(result["errors"]))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("dafny", type=Path)
    parser.add_argument("solver", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    dafny, solver, output = args.dafny.resolve(), args.solver.resolve(), args.output.resolve()
    fixture = Path(__file__).resolve().with_name("PackagedLibraries.dfy")
    output.mkdir(parents=True, exist_ok=True)
    summary = {"dafny_sha256": sha256(dafny), "solver_sha256": sha256(solver),
               "fixture_sha256": sha256(fixture)}
    pipeline = dafny.with_name("DafnyPipeline.dll")
    if pipeline.is_file():
        summary["pipeline_sha256"] = sha256(pipeline)
    summary["checks"] = [check(dafny, solver, fixture, output, target) for target in ("notarget", "cs")]
    summary["passed"] = all(result["passed"] for result in summary["checks"])
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
