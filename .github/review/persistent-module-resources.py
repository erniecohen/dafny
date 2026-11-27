"""Qualified resource-exhausted module replay, including current diagnostics and rejection controls."""
import csv
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zlib

exe, solver, source, destination = (Path(p).resolve() for p in sys.argv[1:])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
original = source.read_text(encoding="utf-8")
program = destination / "input.dfy"
program.write_text(original, encoding="utf-8")
cache = destination / "cache"
report = destination / "report.csv"
modules = ["Mixed", "Other", "Witness"]


def run(name, directory=cache, persistent=True, extra=(), input_file=program):
    report.unlink(missing_ok=True)
    profile = destination / (name + ".jsonl")
    args = [shutil.which("dotnet"), str(exe), "verify", str(input_file), "--cores", "1", "--type-system-refresh",
            "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "1000000",
            "--verification-cache", str(directory), "--pipeline-profile", str(profile), "--show-snippets:false",
            "--use-basename-for-filename", "--boogie", "/randomSeed:7", "--isolate-assertions",
            "--log-format", "csv;LogFileName=" + str(report)]
    if persistent:
        args += ["--persistent-translation"]
    environment = {k: v for k, v in os.environ.items() if k not in ("DAFNY_CACHE_DIR", "DAFNY_CACHE_PARTITION")}
    result = subprocess.run(args + list(extra), capture_output=True, timeout=300, env=environment, cwd=destination)
    (destination / (name + ".stdout")).write_bytes(result.stdout)
    (destination / (name + ".stderr")).write_bytes(result.stderr)
    assert result.returncode == 4 and report.is_file(), (result.returncode, result.stdout, result.stderr)
    rows = list(csv.DictReader(io.StringIO(report.read_text())))
    (destination / (name + ".csv")).write_bytes(report.read_bytes())
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    return {
        "output": (result.returncode, result.stdout, result.stderr), "rows": rows,
        "facts": [{k: v for k, v in row.items() if k not in ("TestResult.Duration", "TimingReplayed")} for row in rows],
        "stats": next((e for e in events if e.get("ev") == "persistent-translation"), None),
        "solver": next(e for e in events if e.get("ev") == "result-cache")["solverStarts"],
        "translated": sorted(e["module"] for e in events if e.get("ev") == "span" and e.get("stage") == "translate"),
    }


def same(oracle, actual):
    assert actual["output"] == oracle["output"], (actual["output"], oracle["output"])
    assert actual["facts"] == oracle["facts"], (actual["facts"], oracle["facts"])


def hit(actual):
    assert actual["stats"]["keyHits"] == 2 and actual["translated"] == ["Witness"], actual["stats"]
    assert actual["solver"] == 0 and all(r["TimingReplayed"] == "true" for r in actual["rows"]), actual


# Independent fresh resolution/translation/solver results, with no module-result table.
fresh = run("fresh", destination / "fresh-cache", False)
assert fresh["output"][0] == 4 and b"1 error" in fresh["output"][1] and b"out of resource" in fresh["output"][1], fresh
assert fresh["solver"] > 0 and any(r["RandomSeed"] == "7" for r in fresh["rows"])
cold = run("cold")
same(fresh, cold)
assert cold["stats"]["manifestPublications"] == 2 and cold["solver"] == fresh["solver"], cold["stats"]
warm = run("warm")
same(fresh, warm)
hit(warm)
audited = run("audit", extra=("--persistent-translation-audit",))
same(fresh, audited)
assert audited["translated"] == modules and audited["stats"]["keyHits"] == 0, audited["stats"]
assert audited["stats"]["auditChecks"] >= 5 and audited["stats"]["auditMismatches"] == 0, audited["stats"]

# An unchanged snapshot relocates its resource diagnostic to the current declaration.
moved = destination / "moved/input.dfy"
moved.parent.mkdir()
moved.write_text("\n\n\n" + original, encoding="utf-8")
relocated = run("relocated", input_file=moved)
relocated_fresh = run("relocated-fresh", destination / "relocated-fresh-cache", False, input_file=moved)
same(relocated_fresh, relocated)
hit(relocated)

# Limits in a method attribute are effective limits, even when the command time limit is zero.
timed_cache = destination / "timed-cache"
program.write_text(original.replace("{:resource_limit 1}", "{:resource_limit 1} {:timeLimit 1}"), encoding="utf-8")
timed = run("timed-cold", timed_cache)
timed_warm = run("timed-warm", timed_cache)
same(timed, timed_warm)
assert timed_warm["stats"]["keyHits"] == 1 and timed_warm["translated"] == ["Mixed", "Witness"], timed_warm["stats"]
assert timed_warm["solver"] > 0, timed_warm["solver"]

# Raising the actual resource bound must not replay the previous exhausted outcome.
program.write_text(original.replace("{:resource_limit 1}", "{:resource_limit 1000000}"), encoding="utf-8")
raised = run("raised-limit")
raised_fresh = run("raised-fresh", destination / "raised-fresh-cache", False)
same(raised_fresh, raised)
assert raised["stats"]["keyHits"] == 1 and raised["solver"] > 0, raised["stats"]
assert b"out of resource" not in raised["output"][1]
program.write_text(original, encoding="utf-8")


def records(directory):
    for index in sorted((directory / "v1/module-results").glob("*/*/data")):
        entry = json.loads(index.read_text())
        blob = directory / "v1/blobs/sha256" / entry["Blob"][:2] / entry["Blob"] / "data"
        stored = blob.read_bytes()
        yield index, entry, json.loads(zlib.decompress(stored[10:]) if stored.startswith(b"\0dcz") else stored)


def replace(directory, index, entry, record):
    payload = json.dumps(record).encode()
    digest = hashlib.sha256(payload).hexdigest()
    blob = directory / "v1/blobs/sha256" / digest[:2] / digest / "data"
    blob.parent.mkdir(parents=True, exist_ok=True)
    blob.write_bytes(payload)
    entry.update(Blob=digest, Length=len(payload))
    index.write_text(json.dumps(entry))


# Self-consistent but unsupported records must miss, not restore a fabricated outcome.
for kind in ("old-schema", "unknown-outcome", "timed-resource", "missing-location", "bad-location"):
    broken = destination / (kind + "-cache")
    shutil.copytree(cache, broken)
    changed = 0
    for index, entry, record in records(broken):
        if record["Module"] != "Mixed":
            continue
        exhausted = next(t["Outcome"] for t in record["Tasks"] if "Limited" in t["ScopeId"])
        if kind == "old-schema":
            record["Schema"] = 1
        else:
            for task in record["Tasks"]:
                if task["Scheduled"]:
                    if kind == "unknown-outcome":
                        task["Outcome"] = -1
                    elif kind == "timed-resource":
                        task.update(Outcome=exhausted, TimeLimit=1, ResourceLimit=1)
                    elif "Limited" in task["ScopeId"]:
                        if kind == "missing-location":
                            task["Diagnostic"] = None
                        elif task.get("Diagnostic"):
                            task["Diagnostic"]["Column"] += 1
        replace(broken, index, entry, record)
        changed += 1
    assert changed > 0
    fallback = run(kind, broken)
    same(fresh, fallback)
    assert fallback["stats"]["keyHits"] == 1 and fallback["translated"] == ["Mixed", "Witness"], fallback["stats"]

# The equality oracle must notice an exhausted outcome changed into a success.
tampered = destination / "tampered-cache"
shutil.copytree(cache, tampered)
changed = 0
for index, entry, record in records(tampered):
    if record["Module"] != "Mixed":
        continue
    valid = next(t["Outcome"] for t in record["Tasks"] if "Good" in t["ScopeId"])
    for task in record["Tasks"]:
        if "Limited" in task["ScopeId"]:
            task["Outcome"] = valid
            changed += 1
    replace(tampered, index, entry, record)
assert changed > 0
caught = run("tampered-audit", tampered, extra=("--persistent-translation-audit",))
assert caught["stats"]["auditMismatches"] > 0 and b"stored results of module Mixed differ" in caught["output"][1], caught["stats"]
print("persistent resource modules: fresh/cold/warm/audit equality, hits, limits, witness, current diagnostics and malformed-record controls: PASS")
