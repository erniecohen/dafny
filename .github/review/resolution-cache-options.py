"""Option sensitivity of the persistent resolution cache (issue 261), through the command line.

A cache is warmed with every option at its default. A run that changes an option the resolution key leaves out
must reuse every module, and --resolution-cache-check must find each reused module equal to a fresh resolution
under the changed option: that is the semantic control that the option cannot change a result. A run that
changes an option in the key, whether on the command line, in a project file, through --boogie, as a hidden
option or as a --legacy-* option, must reuse no module."""
import shutil
import json
import os
from pathlib import Path
import subprocess
import sys

exe, solver, source, destination = (Path(s).resolve() for s in sys.argv[1:5])
# A rerun reuses the test's output directory: start from an empty one.
shutil.rmtree(destination, ignore_errors=True)
destination.mkdir(parents=True)
store = destination / "store"
environment = {k: v for k, v in os.environ.items() if not k.startswith("DAFNY_CACHE_")}

# Warnings (shadowing, an unused variable) and a failing proof give the reporting options something to change.
(destination / "A.dfy").write_text("module A {\n  function F(x: int): int { x + 1 }\n}\n")
(destination / "B.dfy").write_text("""include "A.dfy"
module B {
  import A
  lemma L(x: int) ensures A.F(x) == x + 1 {}
  method Shadow(x: int) { var y := x; if y > 0 { var y := 1; } }
  method Bad() { assert false; }
}
""")
runs = 0


def verify(*options, target="B.dfy", check=False):
    global runs
    runs += 1
    profile = destination / ("run%d.jsonl" % runs)
    args = ["dotnet", str(exe), "verify", target, "--type-system-refresh", "--cache-dir", str(store),
            "--solver-path", str(solver), "--resource-limit", "1000000", "--allow-warnings",
            "--standard-libraries:false", "--pipeline-profile", str(profile)] + list(options)
    if check:
        args.append("--resolution-cache-check")
    result = subprocess.run(args, cwd=destination, capture_output=True, timeout=180, env=environment)
    # One proof fails, so a completed run exits 4; anything else is an option or resolution error.
    assert result.returncode == 4, (options, result.returncode, result.stdout, result.stderr)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    return {e["module"]: e["state"] for e in events if e.get("stage") == "resolve.snapshot"}


cold = verify()
modules = sorted(cold)
assert len(modules) >= 3 and all(s == "stored" for s in cold.values()), cold
assert all(s.startswith("hit") for s in verify().values()), "a warm run reuses every module"

# Options the key leaves out: every module is reused, and equals a fresh resolution under the option.
ignored = [
    ["--progress", "Symbol"], ["--progress", "Batch"], ["--resolution-threads", "4"], ["--cores", "2"],
    ["--show-snippets:false"], ["--use-basename-for-filename"], ["--log-format", "csv"],
    ["--verification-coverage-report", str(destination / "coverage")], ["--bprint", str(destination / "out.bpl")],
    ["--cache-partition", "elsewhere"], ["--cache-partition-budget", "5"],
]
for options in ignored:
    states = verify(*options, check=True)
    assert sorted(states) == modules, (options, states)
    assert all(s.startswith("hit (checked") for s in states.values()), (options, states)

# Options in the key: no module is reused.
(destination / "dfyconfig.toml").write_text('includes = ["B.dfy"]\n[options]\nwarn-shadowing = true\n')
in_key = [
    ([], "dfyconfig.toml"),
    (["--allow-axioms"], "B.dfy"),
    (["--boogie", "/proverOpt:O:smt.random_seed=1"], "B.dfy"),
    (["--legacy-decreases-order"], "B.dfy"),
    (["--ignore-indentation"], "B.dfy"),
]
for options, target in in_key:
    states = verify(*options, target=target)
    assert sorted(states) == modules and not any(s.startswith("hit") for s in states.values()), (options, states)

# The same option from the command line gives the same key as from the project file.
assert all(s.startswith("hit") for s in verify("--warn-shadowing").values())

print("resolution cache options: %d runs; %d ignored options reuse every module and check equal to a fresh "
      "resolution; %d key options (command line, project file, --boogie, legacy, hidden) reuse none, and a project file option keys as on the command line"
      % (runs, len(ignored), len(in_key)))
