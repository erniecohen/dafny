"""The shared cache store through the command line (issue 261): its root from the environment or a project file,
two checkouts sharing entries, partitions, and `dafny cache keep|release|gc|status`.

Which modules a run reused is read from the resolve.snapshot events of --pipeline-profile."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

exe, solver, source, destination = (Path(s).resolve() for s in sys.argv[1:5])
assert not destination.exists(), "use a fresh test directory"
destination.mkdir(parents=True)
store = destination / "store"
artifacts = store / "v1" / "artifacts"
environment = {k: v for k, v in os.environ.items() if not k.startswith("DAFNY_CACHE_")}

A = "module A {\n  function F(x: int): int { x + 1 }\n}\n"
B = "include \"A.dfy\"\nmodule B {\n  import A\n  lemma L(x: int) ensures A.F(x) == x + 1 {}\n}\n"


def checkout(name, a=A):
    tree = destination / name
    tree.mkdir()
    (tree / "A.dfy").write_text(a)
    (tree / "B.dfy").write_text(B)
    return tree


profiles = 0


def resolve(tree, *options, env=None, target="B.dfy"):
    """Resolves the tree from its own directory; returns the snapshot state of each module."""
    global profiles
    profiles += 1
    profile = destination / ("run%d.jsonl" % profiles)
    result = subprocess.run(["dotnet", str(exe), "resolve", target, "--type-system-refresh",
                             "--standard-libraries:false", "--pipeline-profile", str(profile)] + list(options),
                            cwd=tree, capture_output=True, timeout=120, env=dict(environment, **(env or {})))
    assert result.returncode == 0, (result.stdout, result.stderr)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    return {e["module"]: e["state"] for e in events if e.get("stage") == "resolve.snapshot"}


def reused(states):
    return sorted(m for m, s in states.items() if s.startswith("hit"))


def cache(*args, env=None, cwd=destination):
    result = subprocess.run(["dotnet", str(exe), "cache"] + list(args), cwd=cwd, capture_output=True, timeout=60,
                            env=dict(environment, **(env or {})))
    return result.returncode, result.stdout.decode().replace(str(destination), "DIR")


def partitions():
    return sorted(p.name for p in (artifacts / "partitions").iterdir())


def entries():
    return sorted(p.name for p in (artifacts / "resolutions").glob("*/*"))


by_environment = {"DAFNY_CACHE_DIR": str(store)}

# The environment sets the root. Two checkouts of the same sources share every entry, each in its own partition;
# a checkout whose A differs misses A, and reuses nothing that depends on it.
first, second, edited = checkout("first"), checkout("second"), checkout("edited", A.replace("x + 1", "1 + x"))
cold = resolve(first, env=by_environment)
modules = sorted(cold)
assert reused(cold) == [] and len(modules) == 4, cold
assert reused(resolve(second, env=by_environment)) == modules
assert len(partitions()) == 2, partitions()
assert reused(resolve(edited, env=by_environment)) == [], "a differing input misses, and so does all that depends on it"
shared = len(entries())
assert shared == 2 * len(modules), entries()
# Without --type-system-refresh, a root from the environment alone caches nothing.
legacy = subprocess.run(["dotnet", str(exe), "resolve", "B.dfy", "--type-system-refresh:false",
                         "--standard-libraries:false"], cwd=second, capture_output=True, timeout=120,
                        env=dict(environment, DAFNY_CACHE_DIR=str(destination / "unused")))
assert legacy.returncode == 0 and not (destination / "unused").exists(), legacy

# A project file sets the root and the partition, for runs and for `dafny cache`.
(first / "dfyconfig.toml").write_text('includes = ["B.dfy"]\n[options]\ntype-system-refresh = true\n'
                                      'verification-cache = "../store"\ncache-partition = "main"\n')
assert reused(resolve(first, target="dfyconfig.toml")) == modules
assert "main" in partitions()
code, out = cache("status", "dfyconfig.toml", cwd=first)
assert code == 0 and "this run's partition: main" in out and "  main: 4 entries" in out, out

# A tag keeps the partition's entries when every partition has dropped them; releasing it lets them go.
code, out = cache("keep", "--tag", "release", "dfyconfig.toml", cwd=first)
assert code == 0 and out.strip() == "tag release holds the 4 entries of partition main", out


def age_entries():
    # Collection leaves entries younger than ten minutes, which may be in the middle of being published.
    for entry in (artifacts / "resolutions").glob("*/*"):
        os.utime(entry, (entry.stat().st_atime, entry.stat().st_mtime - 3600))


# A budget of 1 MB holds these small entries: gc with it drops nothing.
assert cache("gc", "--cache-dir", str(store), "--cache-partition", "main", "--cache-partition-budget", "1")[0] == 0
age_entries()
assert cache("gc", "--cache-dir", str(store))[0] == 0
assert len(entries()) == shared, entries()
for name in partitions():
    # Every partition drops its holds (a gc run's own partition, from its directory, holds nothing).
    if (artifacts / "partitions" / name / "holds").exists():
        shutil.rmtree(artifacts / "partitions" / name / "holds")
code, out = cache("gc", "--cache-dir", str(store))
assert code == 0 and len(entries()) == len(modules), (out, entries())
code, out = cache("release", "--tag", "release", "--cache-dir", str(store))
assert code == 0 and out.strip() == "released tag release (4 entries)", out
code, out = cache("gc", "--cache-dir", str(store))
assert code == 0 and entries() == [], (out, entries())
assert reused(resolve(second, env=by_environment)) == [], "nothing holds the entries any more"

# Errors: no store, no tag, a source file instead of a project file, an unknown partition.
assert cache("status")[0] == 1
assert cache("keep", "--cache-dir", str(store))[0] == 1
assert cache("status", "B.dfy", "--cache-dir", str(store), cwd=first)[0] == 1
assert cache("keep", "--tag", "t", "--cache-dir", str(store), "--cache-partition", "nobody")[0] == 2

print("cache command: %d runs; two checkouts share entries, a differing input misses; a project file sets the root "
      "and partition; a tag outlives budgets until released" % profiles)
