"""Declared audited solver digests for --verification-cache (https://github.com/erniecohen/dafny/issues/574).

A declared digest is accepted in addition to the built-in ones, but the solver image is still
hashed on every run: replay needs the actual image's digest to be built in or declared, and
the cache key keeps that digest.  Two rebuilt solvers stand in for a reproducible source build:
copies of the release binary with one trailing byte each, so each runs as the release does but
has a digest that is not built in.

    verification-cache-solver-digest.py <Dafny.dll> <z3> <source.dfy> <output dir>

Prints one line on success.  Every control is an assertion.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

exe, solver, source, destination = (Path(s).resolve() for s in sys.argv[1:5])
shutil.rmtree(destination, ignore_errors=True)
destination.mkdir(parents=True)
input_file = destination / "input.dfy"
input_file.write_text(source.read_text(encoding="utf-8"), encoding="utf-8")


def rebuilt(name, suffix):
    """A copy of the solver executable, in a directory of its own, with one extra trailing byte."""
    directory = destination / name
    directory.mkdir()
    executable = directory / solver.name
    shutil.copy2(solver, executable)  # a copy of the bytes: the original is never written
    with executable.open("ab") as stream:
        stream.write(suffix)
    digest = hashlib.sha256(executable.read_bytes()).hexdigest()
    return executable, digest


solver_a, digest_a = rebuilt("solver-a", b"a")
solver_b, digest_b = rebuilt("solver-b", b"b")
assert len({digest_a, digest_b, hashlib.sha256(solver.read_bytes()).hexdigest()}) == 3


def run(name, executable, cache, digests=(), target=None, extra=()):
    profile = destination / (name + ".jsonl")
    args = ["dotnet", str(exe), "verify", str(target or input_file), "--cores", "1", "--type-system-refresh",
            "--solver-path", str(executable), "--verification-time-limit", "0",
            "--resource-limit", "16000000", "--use-basename-for-filename", "--show-snippets:false",
            "--pipeline-profile", str(profile), "--verification-cache", str(destination / cache)]
    for digest in digests:
        args += ["--verification-cache-solver-digest", digest]
    args += list(extra)
    result = subprocess.run(args, capture_output=True, timeout=300)
    (destination / (name + ".stdout")).write_bytes(result.stdout)
    (destination / (name + ".stderr")).write_bytes(result.stderr)
    events = [json.loads(line) for line in profile.read_text().splitlines()] if profile.exists() else []
    stats = [e for e in events if e.get("ev") == "result-cache"]
    tasks = [e for e in events if e.get("ev") == "result-task"]
    return result, (stats[0] if stats else {}), tasks


def verified(result):
    # The program: two lemmas that verify and one that does not.
    assert result.returncode == 4, (result.returncode, result.stdout, result.stderr)
    assert b"2 verified, 1 error" in result.stdout, result.stdout


WARNING = b"is neither built in nor declared by --verification-cache-solver-digest"

# Negative control: a rebuilt solver that nobody declared is not cached, and, with no
# declaration, nothing is printed: the default behaviour is unchanged.
for name in ("undeclared-cold", "undeclared-warm"):
    result, stats, _ = run(name, solver_a, "undeclared")
    verified(result)
    assert stats["hits"] == 0 and stats["publications"] == 0 and stats["solverStarts"] == 3, stats
    assert stats["bypassReason"] == "unaudited native solver execution profile", stats
    assert WARNING not in result.stdout + result.stderr, result.stderr

# Positive control: the same solver, declared, stores its results and then replays them.
result, stats, _ = run("declared-cold", solver_a, "declared", [digest_a.upper()])
verified(result)
assert stats["publications"] == 3 and stats["solverStarts"] == 3 and stats["bypassReason"] is None, stats
result, stats, tasks = run("declared-warm", solver_a, "declared", [digest_b, digest_a])
verified(result)
# Vacuity: the replay happened.  Every task was served from the cache and no solver ran.
assert stats["hits"] == 3 and stats["solverStarts"] == 0, stats
assert len(tasks) == 3 and all(t["replayed"] for t in tasks), tasks
assert WARNING not in result.stdout + result.stderr, result.stderr

# Negative control: a declared digest does not admit a different binary.  The other rebuilt
# solver, under solver-a's declaration, against solver-a's results: nothing is replayed or
# stored, and the run says why, once.
result, stats, _ = run("mismatch", solver_b, "declared", [digest_a])
verified(result)
assert stats["hits"] == 0 and stats["publications"] == 0 and stats["solverStarts"] == 3, stats
assert stats["bypassReason"].startswith("unaudited native solver execution profile: the solver's SHA-256 digest "
                                        + digest_b), stats
assert result.stderr.count(WARNING) == 1, result.stderr

# The key keeps the digest: solver-b, declared, finds none of solver-a's results.
result, stats, _ = run("other-declared", solver_b, "declared", [digest_b])
verified(result)
assert stats["hits"] == 0 and stats["publications"] == 3 and stats["solverStarts"] == 3, stats

# A project file declares digests too.
project = destination / "dfyconfig.toml"
project.write_text('includes = ["input.dfy"]\n[options]\nverification-cache-solver-digest = ["' + digest_a + '"]\n')
result, stats, tasks = run("project-warm", solver_a, "declared", target=project)
verified(result)
assert stats["hits"] == 3 and stats["solverStarts"] == 0, stats

# A value that is not a SHA-256 digest is an option error.
result, stats, _ = run("malformed", solver_a, "malformed", ["not-a-digest"])
assert result.returncode == 1 and not stats, (result.returncode, result.stdout, result.stderr)
assert b"--verification-cache-solver-digest must be a SHA-256 digest" in result.stdout + result.stderr, result.stdout

print("verification cache solver digests: declared replays; undeclared and mismatched binaries do not; key keeps the digest")
