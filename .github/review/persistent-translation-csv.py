"""CSV-only module replay: independent oracle, real hits, report eligibility and fallback."""
import csv
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

exe, solver, source, destination = (Path(p).resolve() for p in sys.argv[1:])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
original = source.read_text(encoding="utf-8")
program = destination / "input.dfy"
program.write_text(original, encoding="utf-8")
cache = destination / "cache"
report = destination / "report.csv"
other_report = destination / "other-report"
dotnet = shutil.which("dotnet")
modules = ["A", "B", "H"]


def run(name, directory=cache, persistent=True, extra=(), input_file=program):
    report.unlink(missing_ok=True)
    profile = destination / (name + ".jsonl")
    args = [dotnet, str(exe), "verify", str(input_file), "--cores", "1", "--type-system-refresh",
            "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "16000000",
            "--verification-cache", str(directory), "--pipeline-profile", str(profile), "--show-snippets:false",
            "--use-basename-for-filename", "--boogie", "/randomSeed:7", "--isolate-assertions",
            "--log-format", "csv;LogFileName=" + str(report)]
    if persistent:
        args += ["--persistent-translation"]
    args += list(extra)
    environment = {k: v for k, v in os.environ.items() if k not in ("DAFNY_CACHE_DIR", "DAFNY_CACHE_PARTITION")}
    result = subprocess.run(args, capture_output=True, timeout=300, env=environment, cwd=destination)
    (destination / (name + ".stdout")).write_bytes(result.stdout)
    (destination / (name + ".stderr")).write_bytes(result.stderr)
    assert report.is_file(), (name, result.returncode, result.stdout, result.stderr)
    rows = list(csv.DictReader(io.StringIO(report.read_text())))
    (destination / (name + ".csv")).write_bytes(report.read_bytes())
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    return {
        "output": (result.returncode, result.stdout, result.stderr),
        "rows": rows,
        "facts": [{k: v for k, v in row.items() if k not in ("TestResult.Duration", "TimingReplayed")} for row in rows],
        "stats": next((e for e in events if e.get("ev") == "persistent-translation"), None),
        "solver": next(e for e in events if e.get("ev") == "result-cache")["solverStarts"],
        "translated": sorted(e["module"] for e in events if e.get("ev") == "span" and e.get("stage") == "translate"),
    }


def same(oracle, actual):
    assert actual["output"] == oracle["output"], (actual["output"], oracle["output"])
    assert actual["facts"] == oracle["facts"], (actual["facts"], oracle["facts"])


def hit(actual):
    # Solver-result hits alone are insufficient: the module path must skip translation.
    assert actual["stats"]["keyHits"] == 2, actual["stats"]
    assert actual["translated"] == ["H"], actual["translated"]
    assert actual["solver"] == 0, actual["solver"]
    assert all(row["TimingReplayed"] == "true" for row in actual["rows"]), actual["rows"]


# A genuinely independent, populated task cache; no persistent module path is enabled.
oracle = run("oracle", destination / "oracle-cache", persistent=False)
assert oracle["output"][0] == 4 and b"1 error" in oracle["output"][1], oracle["output"]
assert oracle["solver"] > 0 and any(row["TestResult.Outcome"] == "Failed" for row in oracle["rows"])
assert len(oracle["rows"]) > 3, oracle["rows"]  # Multiple assertion batches, not just one scope each.
assert all(row["RandomSeed"] == "7" and row["TimingReplayed"] == "false" for row in oracle["rows"])
cold = run("cold")
same(oracle, cold)
assert cold["stats"]["manifestPublications"] == 2 and cold["solver"] == oracle["solver"], cold["stats"]
warm = run("warm")
same(oracle, warm)
hit(warm)

# Multiple CSV loggers are eligible, including parameters parsed by the existing logger.
second = run("two-csv", extra=("--log-format", "csv;LogFileName=" + str(other_report)))
assert second["facts"] == oracle["facts"] and second["output"][0] == oracle["output"][0]
hit(second)
assert list(csv.DictReader(other_report.open())) == second["rows"]

# A bare CSV name uses the same admission rule; the logger still chooses its own filename.
bare = run("bare-csv", extra=("--log-format", "csv"))
hit(bare)
assert bare["facts"] == oracle["facts"]
auto_reports = list((destination / "TestResults").glob("*.csv"))
assert len(auto_reports) == 1 and list(csv.DictReader(auto_reports[0].open())) == bare["rows"]

# Audit regenerates translations and task results; it must compare rather than silently hit.
audited = run("audit", extra=("--persistent-translation-audit",))
same(oracle, audited)
assert audited["stats"]["keyHits"] == 0 and audited["translated"] == modules, audited["stats"]
assert audited["stats"]["auditChecks"] >= 5 and audited["stats"]["auditMismatches"] == 0, audited["stats"]

# Report eligibility is conjunctive. These loggers need a normal task graph; CSV remains equal.
for logger in ("text", "trx"):
    mixed = run("mixed-" + logger, extra=("--log-format", logger + ";LogFileName=" + str(other_report)))
    # TRX includes a results-file status line, so compare the CSV and exit independently.
    assert mixed["facts"] == oracle["facts"] and mixed["output"][0] == oracle["output"][0], mixed
    assert mixed["translated"] == modules and mixed["stats"]["keyHits"] == 0, mixed["stats"]
    assert mixed["stats"]["bypassReason"] == "verification logs", mixed["stats"]
    assert other_report.stat().st_size > 0

# Relocation and inserted lines bind reports/diagnostics to the current compilation.
moved = destination / "moved" / "input.dfy"
moved.parent.mkdir()
moved.write_text("\n\n" + original, encoding="utf-8")
relocated = run("relocated", input_file=moved)
relocated_oracle = run("relocated-oracle", destination / "relocated-oracle-cache", False, input_file=moved)
same(relocated_oracle, relocated)
hit(relocated)

# A real source change cannot reuse yesterday's successful module result.
program.write_text(original.replace("x + 1", "x - 1"), encoding="utf-8")
stale = run("stale")
stale_oracle = run("stale-oracle", destination / "stale-oracle-cache", False)
same(stale_oracle, stale)
assert stale["solver"] > 0 and stale["stats"]["keyHits"] == 0, stale["stats"]
program.write_text(original, encoding="utf-8")

# A truncated translation entry is a miss; retranslation may still use graph early cutoff.
corrupt_cache = destination / "corrupt-cache"
shutil.copytree(cache, corrupt_cache)
for entry in (corrupt_cache / "v1/artifacts/translations").glob("*/*"):
    if not entry.name.startswith("."):
        entry.write_bytes(b"truncated")
corrupt = run("corrupt", corrupt_cache)
same(oracle, corrupt)
assert corrupt["stats"]["keyHits"] == 0 and corrupt["translated"] == modules, corrupt["stats"]
assert corrupt["stats"]["graphHits"] == 2 and corrupt["solver"] == 0, corrupt["stats"]

print("persistent CSV: fresh/cold/warm equality, hits, batches/seeds, audit, mixed reports, rebinding, stale/corrupt fallback: PASS")
