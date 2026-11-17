"""The persistent verification cache under --filter-position-batch
(https://github.com/erniecohen/dafny/issues/387).

A batch reads and writes the cache as separate --filter-position runs do: a warm batch
starts no solver, a repeated request in one batch reuses the result that its first
occurrence published, a single-filter run reuses a batch's results, and an edit that
changes a task's input still misses.  Every cached run reports exactly what the same
batch reports without the cache.
"""
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
lines = original.splitlines()


def line_of(text):
    [number] = [i + 1 for i, line in enumerate(lines) if text in line]
    return "input.dfy:%d" % number


good, bad, other = line_of("lemma Good(x"), line_of("lemma Bad("), line_of("lemma Good()")


def run(name, filters, cache=None, single=False):
    profile = destination / (name + ".jsonl")
    args = ["dotnet", str(exe), "verify", str(input_file), "--cores", "1", "--type-system-refresh",
            "--solver-path", str(solver), "--verification-time-limit", "0",
            "--resource-limit", "16000000", "--use-basename-for-filename",
            "--show-snippets:false", "--pipeline-profile", str(profile)]
    for f in filters:
        args += ["--filter-position" if single else "--filter-position-batch", f]
    if cache is not None:
        args += ["--verification-cache", str(destination / cache)]
    result = subprocess.run(args, env=os.environ.copy(), capture_output=True, timeout=300)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    stats = [e for e in events if e.get("ev") == "result-cache"]
    tasks = [e for e in events if e.get("ev") == "result-task"]
    if cache is None:
        assert not stats and not tasks, (stats, tasks)
    else:
        assert len(stats) == 1 and tasks, (name, stats, tasks, result.stderr)
        assert stats[0]["bypassReason"] is None, stats
        assert stats[0]["solverStarts"] == sum(not t["replayed"] for t in tasks), (name, stats, tasks)
        assert stats[0]["hits"] == sum(t["replayed"] for t in tasks), (name, stats, tasks)
    return {"output": (result.returncode, result.stdout, result.stderr),
            "stats": stats[0] if stats else {}, "tasks": tasks}


# A repeated request, as #173 allows, and one request that fails.
batch = [good, bad, good, other]
uncached = run("uncached", batch)
assert uncached["output"][0] != 0 and uncached["output"][1].count(b"Dafny program verifier finished") == 4

cold = run("cold", batch, "cache")
assert cold["output"] == uncached["output"]
# The third request repeats the first: it reuses what the first published, and starts no solver.
first = [t for t in cold["tasks"] if t["task"].startswith("M.Good ")]
assert len(first) == 2 and not first[0]["replayed"] and first[1]["replayed"], first
assert cold["stats"]["hits"] == 1 and cold["stats"]["publications"] == len(cold["tasks"]) - 1, cold["stats"]

warm = run("warm", batch, "cache")
assert warm["output"] == uncached["output"]
assert warm["stats"]["solverStarts"] == 0 and warm["stats"]["solverVersionProbes"] == 0, warm["stats"]
assert all(t["replayed"] for t in warm["tasks"])
assert [t["executionKey"] for t in warm["tasks"]] == [t["executionKey"] for t in cold["tasks"]]

# A single-filter run reuses the batch's result, and a batch a single-filter run's.
single = run("single", [other], "cache", single=True)
assert single["stats"]["solverStarts"] == 0 and single["stats"]["hits"] == len(single["tasks"]) == 1
single_cold = run("single-cold", [other], "single-cache", single=True)
assert single_cold["stats"]["solverStarts"] == 1
from_single = run("from-single", [other], "single-cache")
assert from_single["stats"]["solverStarts"] == 0 and from_single["stats"]["hits"] == 1
assert from_single["output"] == run("from-single-uncached", [other])["output"]

# An edit to F changes Good's input: Good misses and now fails, the others still hit, and
# the report is the uncached batch's on the edited program.
assert original.count("int { x + 1 }") == 1
input_file.write_text(original.replace("int { x + 1 }", "int { x + 2 }"), encoding="utf-8")
edited_uncached = run("edited-uncached", batch)
edited = run("edited", batch, "cache")
assert edited["output"] == edited_uncached["output"] and edited["output"] != uncached["output"]
assert edited["stats"]["solverStarts"] == 1, edited["stats"]
missed = [t for t in edited["tasks"] if not t["replayed"]]
assert len(missed) == 1 and missed[0]["task"].startswith("M.Good ") and missed[0]["outcome"] == "Invalid", missed

print("verification cache under --filter-position-batch: cold and warm batches report as uncached; "
      "repeated requests and single filters reuse results; an edited task misses")
