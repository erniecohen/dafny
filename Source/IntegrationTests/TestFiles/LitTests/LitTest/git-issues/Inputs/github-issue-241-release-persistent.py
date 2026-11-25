"""--release-verified-modules with --persistent-translation and --translation-workers (issues 241
and 261): every run reports what a run without either option reports, verified modules' bodies are
released in every run, a warm run replays every module by its translation key, and an edit of a
proof body that no other module reads replays its dependents by their regenerated Boogie graph.

Negative control: before method child symbols skipped released bodies, the warm run's persistent
lookup walked a released body and the run failed with an internal error."""
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
cache = destination / "cache"
MODULES = ["Impl", "Late1", "Late2", "Late3", "Late4", "Library"]
DOTNET = shutil.which("dotnet")
RELEASE = ["--release-verified-modules", "--verification-module-window", "2", "--translation-workers", "4"]


def run(name, persistent=True):
    profile = destination / (name + ".jsonl")
    args = [DOTNET, str(exe), "verify", str(input_file), "--cores", "4", "--type-system-refresh",
            "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "16000000",
            "--show-snippets:false", "--use-basename-for-filename", "--pipeline-profile", str(profile)]
    if persistent:
        args += ["--verification-cache", str(cache), "--persistent-translation"] + RELEASE
    variables = {k: v for k, v in os.environ.items() if k not in ("DAFNY_CACHE_DIR", "DAFNY_CACHE_PARTITION")}
    result = subprocess.run(args, capture_output=True, timeout=300, env=variables)
    (destination / (name + ".stdout")).write_bytes(result.stdout)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    hits = {e["module"]: e["hit"] for e in events if e.get("ev") == "persistent-module"}
    return {
        "output": (result.returncode, result.stdout, result.stderr),
        "released": {e["module"]: e["bodies"] for e in events if e.get("ev") == "release"},
        "hits": hits,
        "translated": sorted(e["module"] for e in events if e.get("ev") == "span" and e.get("stage") == "translate"),
        "commits": sorted(e["module"] for e in events if e.get("ev") == "span" and e.get("stage") == "translate.commit"),
    }


def check(observation, oracle, hits, translated):
    assert observation["output"] == oracle["output"], (observation["output"], oracle["output"])
    # Positive control: every module, replayed or not, had its body released.
    released = observation["released"]
    assert sorted(released) == MODULES and all(released[m] > 0 for m in MODULES), released
    assert observation["hits"] == hits, observation["hits"]
    assert observation["translated"] == translated, observation["translated"]
    # The parallel translation path ran: every translated module committed through it.
    assert observation["commits"] == translated, observation["commits"]


def report(name, observation):
    print(name, "exit", observation["output"][0], "hits",
          ", ".join(f"{m}={observation['hits'][m]}" for m in MODULES))


oracle = run("oracle", persistent=False)
assert oracle["output"][0] == 0 and b"12 verified, 0 errors" in oracle["output"][1], oracle["output"]
cold = run("cold")
check(cold, oracle, {m: None for m in MODULES}, MODULES)
report("cold", cold)
warm = run("warm")
check(warm, oracle, {m: "translation-key" for m in MODULES}, [])
report("warm", warm)

# A proof-body edit of Library changes the resolution identity every other module's key includes,
# but not their Boogie: Library is translated afresh and the others replay by their regenerated graph.
old, new = "var m := n;", "var m := n + 0;"
assert original.count(old) == 1
input_file.write_text(original.replace(old, new), encoding="utf-8")
edited_oracle = run("edited-oracle", persistent=False)
edited = run("edited")
check(edited, edited_oracle, {m: (None if m == "Library" else "boogie-graph") for m in MODULES}, MODULES)
report("edited", edited)
edited_warm = run("edited-warm")
check(edited_warm, edited_oracle, {m: "translation-key" for m in MODULES}, [])
report("edited-warm", edited_warm)
