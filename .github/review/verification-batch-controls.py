"""Generic verification-batch controls. Returns nonzero on failed assertions."""
import json
import re
from pathlib import Path
import subprocess
import sys


def main():
    dafny, z3, output = sys.argv[1:]
    root = Path(output).resolve()
    root.mkdir(parents=True, exist_ok=True)
    (root / "first.dfy").write_text("lemma Good(x: int) { assert x + 1 > x; }\n")
    (root / "second.dfy").write_text("lemma Bad() { assert false; }\n")
    (root / "dfyconfig.toml").write_text('includes = ["*.dfy"]\n')
    common = [dafny, "verify", "dfyconfig.toml", "--solver-path", z3, "--cores", "1",
              "--resource-limit", "16000000", "--verification-time-limit", "30",
              "--use-basename-for-filename", "--show-snippets:false"]
    records = []
    failures = []

    def run(label, extra, expected, counts=None, solver=None):
        command = common + extra + ["--log-format", "json;LogFileName=" + label + ".json"]
        if solver is not None:
            command[command.index("--solver-path") + 1] = solver
        result = subprocess.run(command, cwd=root, text=True, capture_output=True, timeout=90)
        (root / (label + ".stdout")).write_text(result.stdout)
        (root / (label + ".stderr")).write_text(result.stderr)
        log = root / (label + ".json")
        proofs = json.loads(log.read_text()).get("verificationResults", []) if log.exists() else []
        outcomes = [proof["outcome"] for proof in proofs]
        good = (result.returncode != 0 if expected is None else result.returncode == expected) and (counts is None or outcomes == counts)
        if not good:
            failures.append(label)
        records.append({"label": label, "command": command, "exit": result.returncode,
                        "outcomes": outcomes, "passes": good})
        return result

    for refresh in ["false", "true"]:
        resolver = ["--type-system-refresh:" + refresh, "--general-newtypes:" + refresh]
        run("off-" + refresh, resolver + ["--filter-position", "first.dfy"], 0, ["Correct"])
        run("batch-" + refresh, resolver + ["--filter-position-batch", "first.dfy",
            "--filter-position-batch", "first.dfy:1", "--filter-position-batch", "second.dfy",
            "--filter-position-batch", "first.dfy"], 4, ["Correct", "Correct", "Errors", "Correct"])
        run("false-twice-" + refresh, resolver + ["--filter-position-batch", "second.dfy",
            "--filter-position-batch", "second.dfy"], 4, ["Errors", "Errors"])
        run("symbols-" + refresh, resolver + ["--filter-symbol", "Good.",
            "--filter-position-batch", ".dfy", "--filter-position-batch", "first.dfy"], 0, ["Correct", "Correct"])
        run("isolate-" + refresh, resolver + ["--isolate-assertions",
            "--filter-position-batch", "first.dfy:1", "--filter-position-batch", "first.dfy:1"], 0, ["Correct", "Correct"])
    broken_solver = root / "broken-solver"
    broken_solver.write_text('#!/bin/sh\ncase "$*" in *version*) echo "Z3 version 5.1.0";; *) exit 1;; esac\n')
    broken_solver.chmod(0o755)
    result = run("solver-failure", ["--filter-position-batch", "first.dfy", "--filter-position-batch", "first.dfy"], None,
                 solver=str(broken_solver))
    if "Starting verification request 1/2" not in result.stdout:
        failures.append("solver-failure-not-reached")
    if "Starting verification request 2/2" in result.stdout:
        failures.append("solver-failure-continued")
    run("coverage", ["--verification-coverage-report", "coverage", "--filter-position-batch", "first.dfy",
        "--filter-position-batch", "second.dfy", "--filter-position-batch", "first.dfy"], 4, ["Correct", "Errors", "Correct"])
    coverage = json.loads((root / "coverage.json").read_text())["verificationResults"]
    good = [proof for proof in coverage if proof["outcome"] == "Correct"]
    def dependencies(proof):
        return {"programElements": proof.get("programElements"), "vcResults": [
            {key: vc.get(key) for key in ["coveredElements", "uncoveredElements"]}
            for vc in proof["vcResults"]]}
    if len(good) != 2 or not good[0].get("programElements") or dependencies(good[0]) != dependencies(good[1]):
        failures.append("coverage-dependencies-contaminated")
    run("coverage-earlier-result", ["--verification-coverage-report", "coverage-earlier",
        "--filter-position-batch", "first.dfy", "--filter-position-batch", "second.dfy"], 4, ["Correct", "Errors"])
    for report in ["coverage", "coverage-earlier"]:
        pages = list((root / report).rglob("first.dfy_verification.html"))
        spans = re.findall(r'class="([a-z]+)" id="([0-9:]+)"', pages[0].read_text()) if len(pages) == 1 else []
        if ("fc", "1:22") not in spans or any(kind in ["nc", "pc"] for kind, _ in spans):
            failures.append(report + "-stale-html")
    (root / "warnings.dfy").write_text("lemma Needed(x: int) requires x > 0 { assert x >= 1; }\n"
        "lemma Redundant(x: int) requires x > 0 { assert x + 1 > x; }\n")
    warnings = ["--warn-contradictory-assumptions", "--warn-redundant-assumptions"]
    for refresh in ["false", "true"]:
        resolver = ["--type-system-refresh:" + refresh, "--general-newtypes:" + refresh]
        run("dependency-warnings-" + refresh, warnings + resolver + [
            "--filter-position-batch", "warnings.dfy:1", "--filter-position-batch", "warnings.dfy:1"], 0, ["Correct", "Correct"])
        control = run("dependency-warning-control-" + refresh, warnings + resolver + [
            "--filter-position-batch", "warnings.dfy:2", "--filter-position-batch", "warnings.dfy:2"], 2, ["Correct", "Correct"])
        if control.stdout.count("unnecessary requires clause") != 2:
            failures.append("dependency-warning-control-count-" + refresh)
    (root / "warnings.dfy").unlink()
    run("conflicting-filters", ["--filter-position", "first.dfy", "--filter-position-batch", "first.dfy"], 1, [])
    result = run("conflicting-filters-json", ["--json-output", "--filter-position", "first.dfy", "--filter-position-batch", "first.dfy"], 1, [])
    for line in result.stdout.splitlines():
        if line.strip():
            json.loads(line)
    (root / "second.dfy").write_text("lemma Bad() { assert missingName; }\n")
    result = run("resolution-error", ["--filter-position-batch", "first.dfy"], 2, [])
    if "Starting verification request" in result.stdout:
        failures.append("resolution-error-started-verification")
    (root / "second.dfy").write_text("lemma Bad() { assert ; }\n")
    result = run("parse-error", ["--filter-position-batch", "first.dfy"], 2, [])
    if "Starting verification request" in result.stdout:
        failures.append("parse-error-started-verification")
    (root / "second.dfy").write_text("lemma Bad() { assert false; }\n")
    run("before-edit", ["--filter-position-batch", "first.dfy"], 0, ["Correct"])
    (root / "first.dfy").write_text("lemma Good() { assert false; }\n")
    run("after-edit", ["--filter-position-batch", "first.dfy"], 4, ["Errors"])
    (root / "added.dfy").write_text("lemma Added() { assert newlyMissing; }\n")
    run("added-glob-input", ["--filter-position-batch", "first.dfy"], 2, [])
    (root / "dfyconfig.toml").write_text('includes = ["first.dfy"]\n')
    run("edited-project", ["--filter-position-batch", "first.dfy"], 4, ["Errors"])
    (root / "first.dfy").write_text('include "dependency.dfy"\nlemma Good(x: int) { assert x + 1 > x; }\n')
    (root / "dependency.dfy").write_text("lemma Dependency() { assert false; }\n")
    (root / "dfyconfig.toml").write_text('includes = ["first.dfy"]\n[options]\nverify-included-files = false\n')
    run("before-option-edit", ["--filter-position-batch", ".dfy"], 0, ["Correct"])
    (root / "dfyconfig.toml").write_text('includes = ["first.dfy"]\n[options]\nverify-included-files = true\n')
    result = run("after-option-edit", ["--filter-position-batch", ".dfy"], 4)
    if "Errors" not in records[-1]["outcomes"] or "Correct" not in records[-1]["outcomes"]:
        failures.append("after-option-edit-outcomes")
    (root / "first.dfy").write_text("lemma Good() { assert Truth(); }\n")
    (root / "library.dfy").write_text("function Truth(): bool { true }\n")
    (root / "dfyconfig.toml").write_text('includes = ["first.dfy"]\n')
    run("before-library-edit", ["--allow-warnings", "--library", "library.dfy", "--filter-position-batch", ".dfy"], 0, ["Correct"])
    (root / "library.dfy").write_text("function Truth(): bool { false }\n")
    run("after-library-edit", ["--allow-warnings", "--library", "library.dfy", "--filter-position-batch", ".dfy"], 4, ["Errors"])
    (root / "summary.json").write_text(json.dumps({"failures": failures, "records": records}, indent=2) + "\n")
    (root / "summary.md").write_text("## Batch controls\n\n" + str(len(records) - len(failures)) + "/" + str(len(records)) + " controls passed.\n" + "\nFailures: " + (", ".join(failures) or "none") + "\n")
    return bool(failures)

if __name__ == "__main__":
    sys.exit(main())
