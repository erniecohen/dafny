"""Key coverage and planted-entry soundness for --verification-cache.

Every changed input below must miss, and the record written by the changed run is compared
with the cold record of the same task to show which hashed component (solver input or
execution environment) carries the change.  The resource limit is not in the key: each
record keeps the resource use u it was produced with and the limit L it depends on, and the
limit boundaries u, u-1, L and L+1 are checked against a cold run at the same limit.  Keys are recomputed independently
by verification-cache-audit.py, and the cold records are re-proved by a fresh solver.
Planted entries under the wrong key must never be returned.
"""
import base64
import collections
import csv
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import sys

exe, solver, source, destination = map(lambda x: Path(x).resolve(), sys.argv[1:])
spec = importlib.util.spec_from_file_location("audit", Path(__file__).with_name("verification-cache-audit.py"))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)

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
        return {k: without_timing(v) for k, v in value.items() if k not in ("runTime", "timingReplayed")}
    if isinstance(value, list):
        return [without_timing(v) for v in value]
    return value


def run(name, directory, extra=(), solver_path=solver, resource_limit="16000000"):
    profile, log, csv_log = (destination / (name + suffix) for suffix in (".jsonl", ".json", ".csv"))
    args = ["dotnet", str(exe), "verify", str(input_file), "--cores", "1", "--type-system-refresh",
            "--solver-path", str(solver_path), "--verification-time-limit", "0",
            "--resource-limit", resource_limit, "--use-basename-for-filename",
            "--show-snippets:false", "--pipeline-profile", str(profile),
            "--log-format", "json;LogFileName=" + str(log),
            "--log-format", "csv;LogFileName=" + str(csv_log),
            "--verification-cache", str(directory)] + list(extra)
    result = subprocess.run(args, env=environment, capture_output=True, timeout=300)
    (destination / (name + ".stdout")).write_bytes(result.stdout)
    (destination / (name + ".stderr")).write_bytes(result.stderr)
    assert log.exists(), (name, result.returncode, result.stdout, result.stderr)
    with csv_log.open(newline="") as stream:
        rows = [{k: v for k, v in row.items() if k not in ("TestResult.Duration", "TimingReplayed")}
                for row in csv.DictReader(stream)]
    stats = audit.stats(profile)
    stats.pop("replayedKeys")
    tasks = {}
    for line in profile.read_text().splitlines():
        event = json.loads(line) if line.strip() else {}
        if event.get("ev") == "result-task":
            assert event["task"] not in tasks, (name, event["task"])
            tasks[event["task"]] = event
    return {"report": without_timing(json.loads(log.read_text())), "csv": rows,
            "output": (result.returncode, result.stdout.split(b"Results File:")[0], result.stderr),
            "stats": stats, "tasks": tasks}


def without_resources(value):
    if isinstance(value, dict):
        return {k: without_resources(v) for k, v in value.items() if k not in ("resourceCount", "TestResult.ResourceCount")}
    if isinstance(value, list):
        return [without_resources(v) for v in value]
    return value


def same_report(a, b, what):
    for field in ("report", "csv", "output"):
        assert a[field] == b[field], (what, field)


def by_task(directory):
    found = {}
    for key, record, problems in audit.records(directory):
        assert record is not None and not problems, (key, problems)
        assert audit.record_key(record) == key, key
        found[audit.problem_name(audit.exchange(record))] = (key, record)
    return found


def changed_components(old, new):
    # The time limit is keyed but unchanged here; the slot follows the new outcome.
    assert old["TimeLimit"] == new["TimeLimit"]
    return sorted(name for name, field in (("input", "Input"), ("environment", "Environment"))
                  if old[field] != new[field])


def decoded(record, field):
    return base64.b64decode(record[field])


cold = run("cold", cache)
assert cold["stats"]["solverStarts"] == 3 and cold["stats"]["publications"] == 3, cold["stats"]
cold_tasks = by_task(cache)
assert len(cold_tasks) == 3, sorted(cold_tasks)
assert audit.keys(cache)
assert audit.replay(cache, str(solver))
warm = run("warm", cache)
same_report(cold, warm, "warm")
assert warm["stats"]["hits"] == 3 and warm["stats"]["solverStarts"] == 0, warm["stats"]
summary = []


def expect_miss(name, extra=(), solver_path=solver, expected=None, edit=None):
    """A changed input: every task misses into a copy of the cold cache, and its new record
    differs from the cold record of the same task in the expected hashed component."""
    directory = destination / (name + "-cache")
    shutil.copytree(cache, directory)
    if edit:
        input_file.write_text(edit(original), encoding="utf-8")
    try:
        changed = run(name, directory, extra, solver_path)
    finally:
        input_file.write_text(original, encoding="utf-8")
    cold_keys = {key for key, record in cold_tasks.values()}
    new = {}
    for key, record, problems in audit.records(directory):
        assert record is not None and not problems and audit.record_key(record) == key, (name, key, problems)
        if key not in cold_keys:
            task = audit.problem_name(audit.exchange(record))
            assert task not in new, (name, task)
            new[task] = (key, record)
    components = {task: changed_components(cold_tasks[task][1], record) for task, (key, record) in new.items()}
    misses = changed["stats"]["solverStarts"]
    assert changed["stats"]["publications"] == misses == len(new), (name, changed["stats"], sorted(new))
    assert changed["stats"]["hits"] == 3 - misses, (name, changed["stats"])
    assert all(c == expected for c in components.values()), (name, components)
    summary.append("%s: %d of 3 missed (%s)" % (name, misses, "+".join(expected)))
    return changed, misses, new


# A changed solver seed reaches the solver input; every task misses.
seed, misses, seeded = expect_miss("seed", ("--boogie", "/randomSeed:7"), expected=["environment", "input"])
assert misses == 3
assert all(b"(set-option :smt.random_seed 7)" in decoded(record, "Input") for key, record in seeded.values())
# A changed solver option: every task misses.
option, misses, _ = expect_miss("option", ("--solver-option", "O:smt.arith.solver=2"), expected=["environment", "input"])
assert misses == 3
# An option that leaves the solver input unchanged but changes the rest of the interaction
# (how many counterexamples are requested, and so the final resource count) is in the environment.
errors, misses, _ = expect_miss("error-limit", ("--error-limit", "1"), expected=["environment"])
assert misses == 3
# The same solver bytes at another path: the solver image path is part of the identity.
copy_dir = destination / "solver-copy"
copy_dir.mkdir()
solver_copy = copy_dir / solver.name
shutil.copy2(solver, solver_copy)
moved, misses, _ = expect_miss("solver-path", solver_path=solver_copy, expected=["environment"])
assert misses == 3
same_report(cold, moved, "solver-path")
# A program edit misses exactly the task whose VC contains the edited body, through its solver input.
assert original.count("int { x + 1 }") == 1
edited, misses, changed_tasks = expect_miss("edit", edit=lambda text: text.replace("int { x + 1 }", "int { x + 2 }"),
                                            expected=["input"])
assert misses == 1 and list(changed_tasks) == ["Impl$$M.__default.Good_split0"], sorted(changed_tasks)
old_input = decoded(cold_tasks["Impl$$M.__default.Good_split0"][1], "Input")
new_input = decoded(changed_tasks["Impl$$M.__default.Good_split0"][1], "Input")
assert old_input != new_input


def reusable(record, limit):
    """The reuse rule for a cold result {outcome, u, L} under the resource limit `limit`."""
    outcome, used, produced = record
    if outcome == "Valid":
        return limit == 0 or limit >= used
    if outcome == "Invalid":
        return limit > 0 and limit >= used
    if outcome == "OutOfResource":
        return 0 < limit <= produced
    return False


def boundaries(name, directory, produced, limits):
    """At each limit, a warm run on a copy of `directory` reports exactly what a cold run at
    that limit reports, and replays exactly the tasks the reuse rule admits."""
    checked = []
    for limit in limits:
        oracle = run("%s-oracle-%d" % (name, limit), destination / ("%s-oracle-%d-cache" % (name, limit)), resource_limit=str(limit))
        copy = destination / ("%s-%d-cache" % (name, limit))
        shutil.copytree(directory, copy)
        warm = run("%s-%d" % (name, limit), copy, resource_limit=str(limit))
        expected = sorted(task for task, result in produced.items() if reusable(result, limit))
        # Out of resources at L, reused under a smaller limit, reports the resource count
        # recorded at L, while a fresh run stops sooner: the verdicts agree, the counts do not.
        recounted = [task for task in expected if produced[task][0] == "OutOfResource" and limit < produced[task][2]]
        if recounted:
            for field in ("report", "csv"):
                assert without_resources(oracle[field]) == without_resources(warm[field]), (name, limit, field)
            assert oracle["output"] == warm["output"], (name, limit)
        else:
            same_report(oracle, warm, (name, limit))
        replayed = sorted(task for task, event in warm["tasks"].items() if event["replayed"])
        assert replayed == expected, (name, limit, replayed, expected)
        assert warm["stats"]["solverStarts"] == 3 - len(expected), (name, limit, warm["stats"])
        differ = []
        for task, event in warm["tasks"].items():
            cold_event = oracle["tasks"][task]
            assert event["outcome"] == cold_event["outcome"], (name, limit, task, event, cold_event)
            if task in recounted:
                assert event["resourceCount"] == produced[task][1], (name, limit, task, event)
                if event["resourceCount"] != cold_event["resourceCount"]:
                    differ.append(task)
            else:
                assert event["resourceCount"] == cold_event["resourceCount"], (name, limit, task, event, cold_event)
        checked.append("%d:%d%s" % (limit, len(expected), " (%d recorded counts)" % len(differ) if differ else ""))
    summary.append("%s limits (limit:replayed) %s" % (name, " ".join(checked)))


# The resource limit is compared with each record's resource use u rather than keyed, so
# the cold cache, produced under L = 16000000, answers every limit from each task's u up.
LIMIT = 16000000
cold_results = {task: (event["outcome"], event["resourceCount"], LIMIT) for task, event in cold["tasks"].items()}
assert sorted(r[0] for r in cold_results.values()) == ["Invalid", "Valid", "Valid"], cold_results
used = sorted({r[1] for r in cold_results.values()})
boundaries("limit", cache, cold_results,
           sorted({u for u in used} | {u - 1 for u in used} | {LIMIT, LIMIT + 1}))
# Out of resources at a small limit L is reused at every limit up to L, and at no larger one.
SMALL = 100
oor_cache = destination / "oor-cache"
oor = run("oor", oor_cache, resource_limit=str(SMALL))
oor_results = {task: (event["outcome"], event["resourceCount"], SMALL) for task, event in oor["tasks"].items()}
assert "OutOfResource" in {r[0] for r in oor_results.values()}, oor_results
assert oor["stats"]["publications"] == 3, oor["stats"]
oor_used = sorted({r[1] for r in oor_results.values() if r[0] == "OutOfResource"})
boundaries("oor", oor_cache, oor_results,
           sorted({SMALL - 1, SMALL, SMALL + 1} | set(oor_used) | {u - 1 for u in oor_used}))

# A solver that is not one of the audited Z3 5.1.0 release binaries bypasses the cache:
# same verdicts, no lookups, no publications.
modified = copy_dir / "z3-modified"
modified.write_bytes(solver.read_bytes() + b"\0")
modified.chmod(modified.stat().st_mode | stat.S_IXUSR)
launcher = copy_dir / "z3-launcher"
launcher.write_text('#!/bin/sh\nexec "%s" "$@"\n' % solver)
launcher.chmod(0o755)
for name, path, reason in (("modified-solver", modified, "unaudited native solver"),
                           ("launcher", launcher, "launch scripts")):
    directory = destination / (name + "-cache")
    shutil.copytree(cache, directory)
    before = sorted(p.relative_to(directory) for p in (directory / "v1/results").rglob("*"))
    bypassed = run(name, directory, solver_path=path)
    same_report(cold, bypassed, name)
    assert reason in (bypassed["stats"].get("bypassReason") or ""), (name, bypassed["stats"])
    assert bypassed["stats"]["hits"] == 0 and bypassed["stats"]["publications"] == 0, bypassed["stats"]
    # No result is read or written; only the solver's version probe may be memoized, under
    # the digest of its bytes.
    assert before == sorted(p.relative_to(directory) for p in (directory / "v1/results").rglob("*")), name
    summary.append("%s: bypassed" % name)


def publish(directory, record):
    payload = json.dumps(record).encode()
    digest = hashlib.sha256(payload).hexdigest()
    target = directory / "v1/blobs/sha256" / digest[:2] / digest / "data"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(payload)
    return digest, len(payload)


def plant(name, make_record):
    """Point the failing task's index entry at a record that must not answer it."""
    directory = destination / (name + "-cache")
    shutil.copytree(cache, directory)
    bad_key, bad = cold_tasks["Impl$$M.__default.Bad_split0"]
    good_key, good = cold_tasks["Impl$$M.__default.Good_split0"]
    digest, length = publish(directory, make_record(bad, good))
    index = directory / "v1/results" / bad_key[:2] / bad_key / "data"
    entry = json.loads(index.read_text())
    entry.update(Blob=digest, Length=length)
    index.write_text(json.dumps(entry))
    planted = run(name, directory)
    same_report(cold, planted, name)
    assert planted["stats"]["hits"] == 2 and planted["stats"]["solverStarts"] == 1, (name, planted["stats"])
    assert b"Error" in planted["output"][1] or b"error" in planted["output"][1], name
    summary.append("%s: rejected" % name)


# (i) The verified task's whole record under the failing task's key.
plant("planted-record", lambda bad, good: good)
# (ii) The failing task's own input, environment and limits, with the verified task's outcome
# and solver exchange: the replay transport must reject an exchange for another program.
plant("planted-exchange", lambda bad, good: dict(bad, Outcome=good["Outcome"], ResourceCount=good["ResourceCount"],
                                                  ReportData=good["ReportData"]))
for directory in sorted(destination.glob("*-cache")):
    # The independent key check flags exactly the planted caches.
    assert audit.keys(directory) == (not directory.name.startswith("planted")), directory
print("verification cache keys: " + "; ".join(summary))
