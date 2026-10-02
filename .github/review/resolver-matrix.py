#!/usr/bin/env python3
"""Run an explicit resolver matrix; do not reinterpret Lit RUN commands.

The existing Lit regression job still checks complete RUN sequences and solver
negative controls. This complementary matrix checks each listed resolution case
in both resolver modes, retaining exact diagnostics independently of verifier
verdict snapshots. See resolver-matrix.md for baseline review and probe usage.
"""
import argparse
import concurrent.futures
import difflib
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys

CRASH = re.compile(r"Unhandled exception|Stack overflow|(?:System\.)?\w+Exception:|"
                   r"Fatal error|Internal (?:error|exception)", re.IGNORECASE)


def read_manifest(path, litdir):
    cases = json.loads(Path(path).read_text())
    ids = set()
    for case in cases:
        if set(case) - {"id", "file", "options", "refresh_options", "seconds"}:
            raise ValueError(f"Unknown manifest keys: {case}")
        name = case["id"]
        if not re.fullmatch(r"[A-Za-z0-9_.-]+", name) or name in ids:
            raise ValueError(f"Invalid or duplicate case id: {name}")
        ids.add(name)
        source = Path(case["file"])
        if source.is_absolute() or ".." in source.parts or not (litdir / source).is_file():
            raise ValueError(f"Missing or unsafe source path: {source}")
        options = case.get("options", []) + case.get("refresh_options", [])
        if not all(isinstance(arg, str) for arg in options):
            raise ValueError(f"Options must be strings: {name}")
        if any(arg.startswith("--type-system-refresh") for arg in options):
            raise ValueError(f"Resolver mode is controlled by the matrix: {name}")
        if not 0 < case.get("seconds", 60) <= 300:
            raise ValueError(f"Deadline must be in (0, 300]: {name}")
    if not cases:
        raise ValueError("Empty resolver manifest")
    return cases


def classify(returncode, output, timed_out=False):
    if timed_out:
        return "timeout"
    if returncode < 0 or CRASH.search(output):
        return "crash"
    if returncode == 0:
        return "accepted"
    if returncode == 2 and re.search(r"(?:Error:|resolution/type errors?|parse errors?)", output):
        return "rejected"
    return "unexpected-exit"


def execute(command, cwd, seconds):
    # A session gives the runner ownership of the entire subprocess group,
    # including descendants that might otherwise survive a resolver timeout.
    with subprocess.Popen(command, cwd=cwd, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, start_new_session=True) as process:
        timed_out = False
        try:
            stdout, stderr = process.communicate(timeout=seconds)
        except subprocess.TimeoutExpired:
            timed_out = True
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                # The group may exit between the deadline and the kill.
                pass
            stdout, stderr = process.communicate()
        return process.returncode, stdout.decode("utf-8", "replace"), stderr.decode("utf-8", "replace"), timed_out


def run_case(case, refresh, litdir, dafny, outdir):
    key = case["id"] + (".refresh" if refresh else ".legacy")
    command = [str(dafny), "resolve", case["file"],
               "--type-system-refresh:" + str(refresh).lower(),
               "--use-basename-for-filename", "--show-snippets:false",
               "--standard-libraries:false"] + case.get("options", [])
    if refresh:
        command += case.get("refresh_options", [])
    rc, stdout, stderr, timed_out = execute(command, litdir, case.get("seconds", 60))
    # No timing or diagnostic content is discarded. Only cross-platform newlines
    # and the absolute checkout prefix are normalized in the compared record.
    def normalize(value):
        return value.replace("\r\n", "\n").replace(str(litdir) + os.sep, "")
    result = {"outcome": classify(rc, stdout + stderr, timed_out), "exit": rc,
              "stdout": normalize(stdout), "stderr": normalize(stderr)}
    (outdir / (key + ".stdout.txt")).write_text(stdout)
    (outdir / (key + ".stderr.txt")).write_text(stderr)
    (outdir / (key + ".command.json")).write_text(json.dumps(command, indent=2) + "\n")
    return key, result


def differences(expected, actual):
    changes = []
    for key in sorted(expected.keys() | actual.keys()):
        if expected.get(key) != actual.get(key):
            before = json.dumps(expected.get(key), indent=2, sort_keys=True).splitlines()
            after = json.dumps(actual.get(key), indent=2, sort_keys=True).splitlines()
            changes.extend(difflib.unified_diff(before, after, fromfile=key + " expected",
                                               tofile=key + " actual", lineterm=""))
    return "\n".join(changes)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--lit-dir", required=True, type=Path)
    parser.add_argument("--dafny", required=True, type=Path)
    parser.add_argument("--expected", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--jobs", type=int, default=4)
    parser.add_argument("--probe", action="store_true",
                        help="Record outcomes and differences; never approve or replace the baseline")
    args = parser.parse_args(argv)
    if args.jobs < 1:
        parser.error("--jobs must be positive")
    litdir = args.lit_dir.resolve()
    cases = read_manifest(args.manifest, litdir)
    args.output.mkdir(parents=True, exist_ok=True)
    tasks = [(case, refresh) for case in cases for refresh in (False, True)]
    with concurrent.futures.ThreadPoolExecutor(args.jobs) as executor:
        futures = [executor.submit(run_case, case, refresh, litdir, args.dafny.resolve(),
                                   args.output) for case, refresh in tasks]
        actual = dict(future.result() for future in futures)
    (args.output / "actual.json").write_text(json.dumps(actual, indent=2, sort_keys=True) + "\n")
    expected = json.loads(args.expected.read_text()) if args.expected.is_file() else {}
    diff = differences(expected, actual)
    (args.output / "differences.txt").write_text(diff + "\n")
    unsafe = {key: value["outcome"] for key, value in actual.items()
              if value["outcome"] not in ("accepted", "rejected")}
    summary = ["## Resolver matrix", "", f"{len(cases)} explicit cases, {len(actual)} executions.",
               "", "| Outcome | Executions |", "|---|---:|"]
    for outcome in ("accepted", "rejected", "crash", "timeout", "unexpected-exit"):
        summary.append(f"| {outcome} | {sum(r['outcome'] == outcome for r in actual.values())} |")
    summary.extend(["", "Baseline: " + ("DIFFERS" if diff else "matches") + "."])
    if args.probe:
        summary.extend(["", "PROBE ONLY: outcomes are recorded; this run is not a passing resolver gate."])
    if unsafe:
        summary.extend(["", "Failures:"] + [f"- `{key}`: {value}" for key, value in sorted(unsafe.items())])
    text = "\n".join(summary) + "\n"
    (args.output / "summary.md").write_text(text)
    print(text)
    if os.getenv("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as stream:
            stream.write(text)
    if diff:
        print(diff)
    # Crashes/timeouts can never be accepted by copying them into the baseline.
    return 0 if args.probe else int(bool(diff or unsafe))


if __name__ == "__main__":
    sys.exit(main())
