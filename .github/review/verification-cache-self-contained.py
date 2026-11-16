"""The persistent verification cache on a self-contained publish
(https://github.com/erniecohen/dafny/issues/387).

A self-contained build has no .NET installation with a host/fxr directory; its host is
the libhostfxr and libhostpolicy it bundles.  Two fresh runs of the published launcher
share a cache: the cold run publishes every task's result without a bypass, and the warm
run starts no solver and reports what the cold run reported.

Usage: verification-cache-self-contained.py DAFNY_LAUNCHER Z3 PROGRAM.dfy WORK_DIR
"""
import json
from pathlib import Path
import shutil
import subprocess
import sys

dafny, solver, source, destination = map(lambda x: Path(x).resolve(), sys.argv[1:])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
program = destination / "input.dfy"
shutil.copyfile(source, program)


def run(name):
    profile = destination / (name + ".jsonl")
    result = subprocess.run([str(dafny), "verify", str(program), "--cores", "1", "--type-system-refresh",
                             "--solver-path", str(solver), "--verification-time-limit", "0",
                             "--resource-limit", "16000000", "--use-basename-for-filename",
                             "--verification-cache", str(destination / "cache"),
                             "--pipeline-profile", str(profile)], capture_output=True, timeout=600)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    [stats] = [e for e in events if e.get("ev") == "result-cache"]
    tasks = [e for e in events if e.get("ev") == "result-task"]
    print(name, json.dumps(stats))
    return (result.returncode, result.stdout, result.stderr), stats, tasks


cold_output, cold, cold_tasks = run("cold")
assert cold["bypassReason"] is None, cold
assert cold_tasks and cold["publications"] == len(cold_tasks), (cold, cold_tasks)
warm_output, warm, warm_tasks = run("warm")
assert warm_output == cold_output, (cold_output, warm_output)
assert warm["bypassReason"] is None and warm["solverStarts"] == 0, warm
assert warm["hits"] == len(warm_tasks) == len(cold_tasks), (warm, warm_tasks)
print("self-contained verification cache: every task published cold and replayed warm")
