"""Fresh-process result replay, exact changed-key invalidation, and negative controls."""
import collections
import csv
import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

exe, solver, source, destination = map(lambda x: Path(x).resolve(), sys.argv[1:])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
input_file = destination / "input.dfy"
original = source.read_text(encoding="utf-8")
input_file.write_text(original, encoding="utf-8")
environment = os.environ.copy()
cache = destination / "cache"


def without_timing(value):
    if isinstance(value, dict):
        return {k: without_timing(v) for k, v in value.items()
                if k not in ("runTime", "timingReplayed")}
    if isinstance(value, list):
        return [without_timing(v) for v in value]
    return value


def run(name, directory=cache, extra=(), time_limit="0", resource_limit="16000000", enabled=True, expect_keyed=True):
    profile = destination / "current.jsonl"
    log = destination / "current.json"
    csv_log = destination / "current.csv"
    for file in (profile, log, csv_log):
        file.unlink(missing_ok=True)
    args = ["dotnet", str(exe), "verify", str(input_file), "--cores", "1",
            "--solver-path", str(solver), "--verification-time-limit", time_limit,
            "--resource-limit", resource_limit, "--use-basename-for-filename",
            "--show-snippets:false", "--pipeline-profile", str(profile),
            "--log-format", "json;LogFileName=" + str(log),
            "--log-format", "csv;LogFileName=" + str(csv_log)]
    if enabled:
        args += ["--verification-cache", str(directory)]
    args += list(extra)
    result = subprocess.run(args, env=environment, capture_output=True, timeout=300)
    assert log.exists() and csv_log.exists(), (result.returncode, result.stdout, result.stderr)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    stats = [e for e in events if e.get("ev") == "result-cache"]
    tasks = [e for e in events if e.get("ev") == "result-task"]
    for extension, file in (("jsonl", profile), ("json", log), ("csv", csv_log)):
        shutil.copyfile(file, destination / (name + "." + extension))
    (destination / (name + ".stdout")).write_bytes(result.stdout)
    (destination / (name + ".stderr")).write_bytes(result.stderr)
    with csv_log.open(newline="") as stream:
        csv_rows = list(csv.DictReader(stream))
    observation = {
        "report": without_timing(json.loads(log.read_text())),
        "csv": [{k: v for k, v in row.items() if k not in ("TestResult.Duration", "TimingReplayed")}
                for row in csv_rows],
        "output": (result.returncode, result.stdout, result.stderr),
        "keys": collections.Counter(t["executionKey"] for t in tasks),
        "stats": stats[0] if stats else {}, "tasks": tasks,
    }
    if enabled:
        assert len(stats) == 1 and tasks and (not expect_keyed or None not in observation["keys"]), (stats, tasks)
    else:
        assert not stats and not tasks
    return observation


def same_report(a, b):
    for field in ("report", "csv", "output"):
        assert a[field] == b[field], field


cold = run("cold")
assert cold["stats"]["solverStarts"] == len(cold["tasks"]) == 3, cold["stats"]
assert cold["stats"]["publications"] == 3
warm = run("warm")
same_report(cold, warm)
assert warm["stats"]["hits"] == 3 and warm["stats"]["solverStarts"] == 0, warm["stats"]
assert warm["stats"]["solverVersionProbes"] == 0
assert all(t["replayed"] for t in warm["tasks"])
assert all(vc["timingReplayed"] for r in json.loads((destination / "warm.json").read_text())["verificationResults"] for vc in r["vcResults"])

# A well-formed candidate can still diverge after lookup. Rehash the intentionally
# changed exchange so the lifecycle retry, rather than the checksum check, is tested.
divergent_cache = destination / "divergent-cache"
shutil.copytree(cache, divergent_cache)
for index in (divergent_cache / "v1/results").glob("*/*/data"):
    entry = json.loads(index.read_text())
    blob = divergent_cache / "v1/blobs/sha256" / entry["Blob"][:2] / entry["Blob"] / "data"
    record = json.loads(blob.read_text())
    if record["Outcome"] != 1:  # Errors
        continue
    exchange = json.loads(base64.b64decode(record["ReportData"]))
    assert exchange["Steps"][-1]["Kind"] == 2  # final idle Ping
    exchange["Steps"][-1]["Command"] = "intentional replay divergence"
    record["ReportData"] = base64.b64encode(json.dumps(exchange).encode()).decode()
    payload = json.dumps(record).encode()
    digest = hashlib.sha256(payload).hexdigest()
    replacement = divergent_cache / "v1/blobs/sha256" / digest[:2] / digest / "data"
    replacement.parent.mkdir(parents=True)
    replacement.write_bytes(payload)
    entry.update(Blob=digest, Length=len(payload))
    index.write_text(json.dumps(entry))
    break
else:
    raise AssertionError("an actual counterexample record is required")
divergent = run("divergent", divergent_cache, expect_keyed=False)
same_report(cold, divergent)
assert divergent["stats"]["fallbacks"] == 1 and divergent["stats"]["solverStarts"] == 1, divergent["stats"]
assert divergent["stats"]["hits"] == 2
quarantined = run("quarantined", divergent_cache)
same_report(cold, quarantined)
assert quarantined["stats"]["fallbacks"] == 0 and quarantined["stats"]["solverStarts"] == 1
assert quarantined["stats"]["hits"] == 2

# The independent edited cold run supplies the exact current-key oracle.
assert original.count("int { x + 1 }") == 1
input_file.write_text(original.replace("int { x + 1 }", "int { x + 2 }"), encoding="utf-8")
edited = run("edit")
oracle = run("edit-oracle", destination / "oracle")
same_report(edited, oracle)
assert edited["keys"] == oracle["keys"]
expected_misses = sum((oracle["keys"] - cold["keys"]).values())
assert expected_misses == 1, (cold["keys"], oracle["keys"])
assert edited["stats"]["solverStarts"] == expected_misses and edited["stats"]["hits"] == 2, edited["stats"]

# Corrupt every task payload, preserving well-formed lookup records: no false hits.
for index in (cache / "v1/results").glob("*/*/data"):
    record = json.loads(index.read_text())
    blob = record["Blob"]
    (cache / "v1/blobs/sha256" / blob[:2] / blob / "data").write_bytes(b"truncated")
corrupt = run("corrupt")
same_report(oracle, corrupt)
assert corrupt["stats"]["hits"] == 0 and corrupt["stats"]["solverStarts"] == 3

# An actual raw SMT timeout override disqualifies errors even with CLI time=0.
limited_cache = destination / "timed"
timed_cold = run("timed-cold", limited_cache, extra=("--solver-option", "O:timeout=30000"))
timed_warm = run("timed-warm", limited_cache, extra=("--solver-option", "O:timeout=30000"))
same_report(timed_cold, timed_warm)
assert timed_warm["stats"]["hits"] == 1 and timed_warm["stats"]["solverStarts"] == 2, timed_warm["stats"]
assert {t["outcome"] for t in timed_warm["tasks"] if t["replayed"]} == {"Valid"}

# A zero effective RU bound must not reuse Invalid, even with time=0.
unbounded_cache = destination / "unbounded"
unbounded_cold = run("unbounded-cold", unbounded_cache, resource_limit="0")
unbounded_warm = run("unbounded-warm", unbounded_cache, resource_limit="0")
same_report(unbounded_cold, unbounded_warm)
assert unbounded_warm["stats"]["hits"] == 1 and unbounded_warm["stats"]["solverStarts"] == 2, unbounded_warm["stats"]

# Resource settings are part of the descriptor, rather than an eligibility-only flag.
changed_limit = run("limit", destination / "oracle", resource_limit="15999999")
assert changed_limit["stats"]["hits"] == 0 and changed_limit["stats"]["solverStarts"] == 3
assert not set(changed_limit["keys"]) & set(oracle["keys"])

# Disabled caching constructs no result table and preserves the old CSV schema.
disabled_dir = destination / "disabled"
disabled = run("disabled", disabled_dir, enabled=False)
assert not disabled_dir.exists()
assert "TimingReplayed" not in (destination / "disabled.csv").read_text().splitlines()[0]
print("verification cache: exact replay; zero warm solver calls; changed-key oracle; corruption and limit controls")
