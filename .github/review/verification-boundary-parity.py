#!/usr/bin/env python3
"""Capture and compare unnormalized CLI bytes, solver-input bytes and proof RU.

Run capture twice with different --dafny assemblies and the same --work directory,
then compare. Use a single core and serial cases; no wall-time performance claim.
Execution receipts belong in --work, never in the source tree's public documents.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


LIT = "Source/IntegrationTests/TestFiles/LitTests/LitTest/"
LIB = "Source/DafnyStandardLibraries/src/Std/"


def corpus():
    cases = [
        ("basics", [LIT + "dafny0/Basics.dfy", "--relax-definite-assignment", "--allow-axioms", "--allow-deprecation"]),
        ("loop", [LIT + "dafny1/FindZero.dfy"]),
        ("heap", [LIT + "dafny2/MonotonicHeapstate.dfy"]),
        ("coinduction", [LIT + "dafny3/SimpleCoinduction.dfy"]),
        ("refinement", [LIT + "dafny4/ClassRefinement.dfy"]),
        ("native-theory", [LIT + "git-issues/Inputs/github-issue-147-portable.dfy"]),
        ("negative", [LIT + "git-issues/Inputs/github-issue-147-false.dfy"]),
        ("old-resolver", [LIT + "git-issues/Inputs/github-issue-147-true.dfy", "--type-system-refresh:false", "--general-newtypes:false"]),
        ("additional-axioms", [LIT + "git-issues/Inputs/github-issue-147-true.dfy", "--additional-axioms"]),
        ("seed", [LIT + "git-issues/Inputs/github-issue-147-true.dfy", "--boogie", "/randomSeed:73"]),
        ("resource-exhaustion", [LIT + "git-issues/Inputs/github-issue-147-true.dfy", "--resource-limit", "1"]),
        ("standard-library", [LIB + "dfyconfig.toml"]),
    ]
    target = Path(LIB + "TargetSpecific")
    for language in ("notarget", "cs", "java", "js", "go", "py"):
        cases.append(("library-" + language, [str(target / "dfyconfig.toml")] +
                      sorted(str(p) for p in target.rglob("*-" + language + "*.dfy"))))
    return cases


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return {"sha256": result.hexdigest(), "bytes": path.stat().st_size}


def capture(args):
    work = args.work.resolve()
    destination = work / args.name
    destination.mkdir(parents=True, exist_ok=True)
    active = work / "active"
    for name, inputs in corpus():
        if args.case and name not in args.case:
            continue
        if (destination / (name + ".json")).exists():
            continue
        if active.exists():
            shutil.rmtree(active)
        active.mkdir(parents=True, exist_ok=False)
        command = ["dotnet", str(args.dafny.resolve()), "verify", "--cores", "1",
                   "--verification-time-limit", "120", "--resource-limit", "16000000",
                   "--type-system-refresh", "--general-traits=datatype", "--general-newtypes",
                   "--allow-warnings", "--show-snippets:false", "--use-basename-for-filename",
                   "--solver-path", str(args.solver.resolve()),
                   "--boogie", "/deterministicLiteralHashes:1", "--boogie", "/restartProver",
                   "--solver-log", str(active / "@PROC@.smt2"),
                   "--log-format", "csv;LogFileName=" + str(active / "resources.csv")] + inputs
        print(args.name, name, flush=True)
        with (active / "stdout").open("wb") as stdout, (active / "stderr").open("wb") as stderr:
            result = subprocess.run(command, stdout=stdout, stderr=stderr, timeout=1800)
        logs = {p.name: digest(p) for p in sorted(active.glob("*.smt2"))}
        # Empty-task success is not evidence that solver inputs agree.
        if not logs:
            raise RuntimeError(name + ": no solver input captured")
        with (active / "resources.csv").open(newline="") as stream:
            rows = list(csv.DictReader(stream))
        resources = sorted((row["TestResult.DisplayName"], row["TestResult.Outcome"],
                            row["TestResult.ResourceCount"], row["RandomSeed"]) for row in rows)
        receipt = {"command": command, "exit": result.returncode,
                   "stdout": digest(active / "stdout"), "stderr": digest(active / "stderr"),
                   "solver-input": logs, "resources": resources}
        (destination / (name + ".json")).write_text(json.dumps(receipt, indent=2) + "\n")
        for stream in ("stdout", "stderr"):
            shutil.copyfile(active / stream, destination / (name + "." + stream))
        shutil.rmtree(active)


def compare(args):
    before = {p.name: json.loads(p.read_text()) for p in (args.work / args.before).glob("*.json")}
    after = {p.name: json.loads(p.read_text()) for p in (args.work / args.after).glob("*.json")}
    assert before and before.keys() == after.keys(), "capture scope differs or is empty"
    for name in sorted(before):
        for field in ("exit", "stdout", "stderr", "solver-input", "resources"):
            assert before[name][field] == after[name][field], (name, field)
    print(f"{len(before)} cases: identical CLI bytes, exit codes, solver-input bytes and resource counts")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    run = commands.add_parser("capture")
    run.add_argument("--dafny", type=Path, required=True)
    run.add_argument("--solver", type=Path, required=True)
    run.add_argument("--work", type=Path, required=True)
    run.add_argument("--name", required=True)
    run.add_argument("--case", action="append")
    diff = commands.add_parser("compare")
    diff.add_argument("--work", type=Path, required=True)
    diff.add_argument("--before", default="before")
    diff.add_argument("--after", default="after")
    args = parser.parse_args()
    (capture if args.command == "capture" else compare)(args)


if __name__ == "__main__":
    main()
