"""Persistent module translations and results (issue 261): warm runs translate nothing, early
cutoff replays modules whose regenerated Boogie is unchanged, and positive, negative, vacuity,
corruption and audit controls."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zlib

exe, solver, source, destination = map(lambda x: Path(x).resolve(), sys.argv[1:])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
input_file = destination / "input.dfy"
original = source.read_text(encoding="utf-8")
input_file.write_text(original, encoding="utf-8")
cache = destination / "cache"
MODULES = ["A", "B", "C", "H"]
# The runtime is resolved once, so that a run with another PATH order runs the same one.
DOTNET = shutil.which("dotnet")


def run(name, directory=cache, extra=(), persistent=True, resource_limit="16000000", program=None, environment=None, basename=True):
    profile = destination / (name + ".jsonl")
    args = [DOTNET, str(exe), "verify", str(program or input_file), "--cores", "1", "--type-system-refresh",
            "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", resource_limit,
            "--show-snippets:false", "--pipeline-profile", str(profile)] + (["--use-basename-for-filename"] if basename else [])
    if directory is not None:
        args += ["--verification-cache", str(directory)]
    if persistent:
        args += ["--persistent-translation"]
    args += list(extra)
    variables = {k: v for k, v in os.environ.items() if k not in ("DAFNY_CACHE_DIR", "DAFNY_CACHE_PARTITION")}
    variables.update(environment or {})
    result = subprocess.run(args, capture_output=True, timeout=300, env=variables)
    (destination / (name + ".stdout")).write_bytes(result.stdout)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    summary = [e for e in events if e.get("ev") == "persistent-translation"]
    modules = {}
    for e in events:
        if e.get("ev") == "persistent-module":
            modules[e["module"]] = e["hit"]
    observation = {
        "output": (result.returncode, result.stdout, result.stderr),
        "stats": summary[0] if summary else None,
        "solver": [e for e in events if e.get("ev") == "result-cache"][0]["solverStarts"],
        "probes": [e for e in events if e.get("ev") == "result-cache"][0]["solverVersionProbes"],
        "translated": sorted(e["module"] for e in events if e.get("ev") == "span" and e.get("stage") == "translate"),
        # Commits of the parallel translation path (--translation-workers), in ticket order.
        "commits": [e["module"] for e in sorted((e for e in events if e.get("ev") == "span" and e.get("stage") == "translate.commit"),
                                                key=lambda e: e["ticket"])],
        "modules": modules,
    }
    assert (observation["stats"] is not None) == persistent, summary
    return observation


def entries(directory):
    """The translation table's entries of the shared artifact store."""
    return sorted(e for e in (directory / "v1/artifacts/translations").glob("*/*") if not e.name.startswith("."))


def string(data, at):
    length = shift = 0
    while True:
        byte = data[at]
        at += 1
        length |= (byte & 0x7F) << shift
        shift += 7
        if byte < 0x80:
            return data[at:at + length].decode(), at + length


def decode_entry(path):
    """An artifact-store entry: DART, format, table, key, payload length, payload, then SHA-256."""
    data = path.read_bytes()
    assert data[:4] == b"DART" and hashlib.sha256(data[:-32]).digest() == data[-32:], path
    table, at = string(data, 8)
    key, at = string(data, at)
    length = int.from_bytes(data[at:at + 8], "little")
    return data[:at + 8], data[at + 8:at + 8 + length]


def encode_entry(path, payload):
    header, _ = decode_entry(path)
    body = header[:-8] + len(payload).to_bytes(8, "little") + payload
    path.write_bytes(body + hashlib.sha256(body).digest())


def same_report(a, b):
    assert a["output"] == b["output"], (a["output"], b["output"])


def check(observation, translated, hits, solver):
    assert observation["translated"] == translated, observation["translated"]
    assert observation["modules"] == hits, observation["modules"]
    assert observation["solver"] == solver, observation["solver"]


cold = run("cold")
check(cold, MODULES, {m: None for m in MODULES}, 5)
assert cold["output"][0] != 0 and b"4 verified, 1 error" in cold["output"][1]
# Every module's translation is recorded; only the modules whose tasks all verified get results.
assert cold["stats"]["translationPublications"] == 4 and cold["stats"]["manifestPublications"] == 3, cold["stats"]

# Two tiers: the translation records and the Boogie texts they name are entries of the shared
# artifact store's translation table (v1/artifacts), which can be regenerated; verdicts and resource
# counts are in the rest of v1.
assert len(entries(cache)) >= 5, entries(cache)
assert list((cache / "v1/module-results").glob("*/*/data")) and not (cache / "v1/translations").exists()

# The option changes no output, and an independent cache without it is the oracle.
oracle = run("oracle", destination / "oracle-cache", persistent=False)
same_report(cold, oracle)

# Warm: no module that verified is translated, prepared or solved again. The failing one is
# translated and prepared again, and its error is replayed by the task-result cache.
warm = run("warm")
same_report(cold, warm)
check(warm, ["H"], {"A": "translation-key", "B": "translation-key", "C": "translation-key", "H": None}, 0)
assert warm["stats"]["keyHits"] == 3 and warm["stats"]["translationPublications"] == 0, warm["stats"]

# An option that no stage keys (here, progress reporting) still hits every module.
progress = run("progress", extra=("--progress", "Symbol"))
assert progress["translated"] == ["H"] and progress["stats"]["keyHits"] == 3 and progress["solver"] == 0, progress["stats"]

# An option that resolution does not read but translation does (here, how the Boogie program names
# source files) misses every translation key and, since a module's results are keyed by every
# option its translation is, every module result. (The solver input names files too: the run is fresh.)
named = run("full-names", basename=False)
assert named["translated"] == MODULES and set(named["modules"].values()) == {None}, named["modules"]
assert named["stats"]["keyHits"] == 0 and named["stats"]["graphHits"] == 0, named["stats"]

# Printing the passive or split Boogie programs, or the solver input, needs every module prepared
# and solved afresh: nothing is replayed, and each file is written.
for option in ("--pprint", "--sprint", "--solver-log"):
    printed = destination / (option[2:] + ".out")
    bypassed = run(option[2:], extra=(option, str(printed)))
    reason = "solver logs" if option == "--solver-log" else "Boogie printing"
    assert bypassed["stats"]["keyHits"] == 0 and bypassed["stats"]["bypassReason"] == reason, bypassed["stats"]
    # Split programs are printed one file per split, named after the given file.
    written = [f for f in destination.glob(printed.name + "*") if f.stat().st_size > 0]
    assert bypassed["translated"] == MODULES and written, option

# Without --verification-cache, DAFNY_CACHE_DIR names the same root; the option takes precedence.
environment = run("environment", directory=None, environment={"DAFNY_CACHE_DIR": str(cache)})
same_report(cold, environment)
assert environment["stats"]["keyHits"] == 3 and environment["solver"] == 0, environment["stats"]
elsewhere = destination / "environment-cache"
precedence = run("precedence", environment={"DAFNY_CACHE_DIR": str(elsewhere)})
assert precedence["stats"]["keyHits"] == 3 and not elsewhere.exists(), precedence["stats"]
# Only the variables that can change a result are keyed (the dynamic loader's): another shell, with
# another PATH order, home directory and partition, hits; a set loader variable misses.
home = destination / "home"
home.mkdir()
shell = {"PATH": os.pathsep.join(reversed(os.environ["PATH"].split(os.pathsep))), "HOME": str(home),
         "DAFNY_CACHE_PARTITION": "another-shell", "UNRELATED_SETTING": "1"}
other_shell = run("other-shell", environment=shell)
same_report(cold, other_shell)
assert other_shell["stats"]["keyHits"] == 3 and other_shell["solver"] == 0, other_shell["stats"]
# The validated solver version is keyed the same way, so another shell does not probe it again.
assert other_shell["probes"] == 0, other_shell["probes"]
loader = run("loader-variable", environment={"DYLD_LIBRARY_PATH": str(destination / "no-libraries"),
                                             "LD_LIBRARY_PATH": str(destination / "no-libraries")})
same_report(cold, loader)
assert loader["stats"]["keyHits"] == 0 and loader["solver"] > 0, loader["stats"]
# A project file's value takes precedence over the variable too.
project = destination / "project"
project.mkdir()
(project / "input.dfy").write_text(original, encoding="utf-8")
(project / "dfyconfig.toml").write_text('includes = ["input.dfy"]\n\n[options]\nverification-cache = ' + json.dumps(str(cache)) + "\n")
projected = run("project", directory=None, program=project / "dfyconfig.toml", environment={"DAFNY_CACHE_DIR": str(elsewhere)})
# Its sources are in another directory: as for another checkout (below), resolution identities leave
# out where the source is, so each module that verified is replayed untranslated.
assert projected["translated"] == ["H"], projected["translated"]
assert projected["modules"] == {"A": "translation-key", "B": "translation-key", "C": "translation-key", "H": None}, projected["modules"]
assert projected["solver"] == 0 and not elsewhere.exists(), projected["stats"]

# Audit translates and verifies everything afresh, compares, and publishes nothing new.
audit = run("audit", extra=("--persistent-translation-audit",))
same_report(cold, audit)
check(audit, MODULES, {m: None for m in MODULES}, 0)
assert audit["stats"]["auditChecks"] == 7 and audit["stats"]["auditMismatches"] == 0, audit["stats"]
assert audit["stats"]["translationPublications"] == 0 and audit["stats"]["manifestPublications"] == 0, audit["stats"]


def edit(name, old, new, translated, hits, solver, directory=cache, extra=()):
    assert original.count(old) == 1
    input_file.write_text(original.replace(old, new), encoding="utf-8")
    edited = run(name, directory, extra)
    fresh = run(name + "-oracle", destination / (name + "-oracle-cache"), persistent=False)
    same_report(edited, fresh)
    # A translation key is a module's resolution identity and those of the modules it reads: a
    # module whose identity did not change is replayed untranslated, and one whose Boogie graph
    # did not change is translated again but neither prepared nor solved again.
    check(edited, translated, hits, solver)
    input_file.write_text(original, encoding="utf-8")
    return edited


# Positive control for early cutoff: a proof body of A changes; B imports A but sees only its
# specification, and C does not import A.
body = edit("body", "assert x + 0 == x;", "assert 0 + x == x;", ["A", "B", "H"],
            {"A": None, "B": "boogie-graph", "C": "translation-key", "H": None}, 1)
assert body["stats"]["graphHits"] == 1 and body["stats"]["keyHits"] == 1, body["stats"]
# Negative control: a function body of A that B reads changes B's Boogie program too. B's method
# hides the function, so its task is replayed by the task-result cache, not by B's module results;
# the stored results of a task that hides a function are compared by audit above.
edit("spec", "int { x + 1 }", "int { x + 2 }", ["A", "B", "H"],
     {"A": None, "B": None, "C": "translation-key", "H": None}, 1)
# A line inserted above every module moves every source position but changes no resolution identity,
# which leaves out where the source is: every module that verified is replayed untranslated, by
# results recorded under a graph whose positions are relative to their declarations.
edit("shift", "// https://", "\n// https://", ["H"],
     {"A": "translation-key", "B": "translation-key", "C": "translation-key", "H": None}, 0)
# Vacuity control: an edit of the failing module alone replays the other three untranslated.
edit("bad", "ensures x > 0 {}", "ensures x > 1 {}", ["H"],
     {"A": "translation-key", "B": "translation-key", "C": "translation-key", "H": None}, 1)

# A witness check must not change the declaration it checks. The constraint "true" does not
# mention its bound variable, so substituting the witness for it yields the constraint itself;
# had the check set that shared expression's origin, U's translation would depend on whether
# this process translated S first, and after an edit of U alone that changes its identity but not
# its Boogie graph (a comment; S replayed untranslated), U's Boogie graph would differ from the one
# recorded when S was translated first.
witnessed = destination / "witness" / "input.dfy"
witnessed.parent.mkdir()
witness_source = """module S {
  type T = v: int | true witness 1
}
module U {
  import S
  lemma UL(t: S.T) ensures t == t {}
}
"""
witnessed.write_text(witness_source, encoding="utf-8")
witness_cache = destination / "witness-cache"
first = run("witness-cold", directory=witness_cache, program=witnessed)
assert first["translated"] == ["S", "U"], first["translated"]
witnessed.write_text(witness_source.replace("  import S\n", "  import S\n  // a comment\n"), encoding="utf-8")
unshared = run("witness-shift", directory=witness_cache, program=witnessed)
assert unshared["modules"] == {"S": "translation-key", "U": "boogie-graph"}, unshared["modules"]
assert unshared["solver"] == 0, unshared["solver"]

# Two checkouts of the same sources, in different directories, share one cache: neither resolution
# identities nor graphs record where the source is.
other = destination / "other checkout" / "input.dfy"
other.parent.mkdir()
other.write_text(original, encoding="utf-8")
moved = run("other-checkout", program=other)
same_report(cold, moved)
assert moved["translated"] == ["H"], moved["translated"]
assert moved["modules"] == {"A": "translation-key", "B": "translation-key", "C": "translation-key", "H": None}, moved["modules"]
assert moved["solver"] == 0, moved["solver"]

# Top-level declarations are recorded relative to themselves, as a module's are, and not by the
# file they are in: another file that holds the same top-level program under another comment is a
# translation-key hit, and audit finds the same graph; so is a line inserted above it.
top_cache = destination / "top-cache"
top_source = "lemma Top(x: int) ensures x + 1 > x { }\nfunction Twice(x: int): int { 2 * x }\nlemma UsesTwice() ensures Twice(3) == 6 { }\n"
first_top = destination / "top-first.dfy"
first_top.write_text("// one comment\n" + top_source, encoding="utf-8")
top = run("top-first", directory=top_cache, program=first_top)
assert top["modules"] == {"_module": None} and top["stats"]["manifestPublications"] == 1, (top["modules"], top["stats"])
second_top = destination / "top-second.dfy"
second_top.write_text("// another comment,\n// two lines long\n" + top_source, encoding="utf-8")
top_again = run("top-second", directory=top_cache, program=second_top)
assert top_again["modules"] == {"_module": "translation-key"} and top_again["solver"] == 0, top_again["modules"]
top_audit = run("top-second-audit", directory=top_cache, program=second_top, extra=("--persistent-translation-audit",))
assert top_audit["stats"]["auditChecks"] == 2 and top_audit["stats"]["auditMismatches"] == 0, top_audit["output"][1]

# A changed verification environment (here, the resource limit, which the task-result cache does
# not key) is not the one the module results were recorded under.
limit = run("limit", resource_limit="15999999")
same_report(cold, limit)
assert set(limit["modules"].values()) == {None} and limit["stats"]["keyHits"] == 0, limit["stats"]
assert limit["solver"] == 0, limit["solver"]


# Partitions bound the large tier and are never part of a key: a run records its uses in its
# partition, and a lookup hits what any partition stored.
partitioned = destination / "partitioned-cache"
first = run("partition-first", partitioned, extra=("--cache-partition", "first"))
assert first["stats"]["translationPublications"] == 4, first["stats"]
holds = partitioned / "v1/artifacts/partitions"
assert list((holds / "first/holds/translations").glob("*/*")), "the first partition holds nothing"
second = run("partition-second", partitioned, environment={"DAFNY_CACHE_PARTITION": "second"})
same_report(cold, second)
assert second["stats"]["keyHits"] == 3 and second["solver"] == 0, second["stats"]
assert list((holds / "second/holds/translations").glob("*/*")), "the second partition holds nothing"
# An evicted translation is only a miss: the modules are translated again, and their verdicts, never
# evicted, replay through early cutoff.
for entry in entries(partitioned):
    entry.unlink()
regenerated = run("partition-regenerated", partitioned, extra=("--cache-partition", "first"))
same_report(cold, regenerated)
check(regenerated, MODULES, {"A": "boogie-graph", "B": "boogie-graph", "C": "boogie-graph", "H": None}, 0)
assert regenerated["stats"]["translationPublications"] == 4, regenerated["stats"]


def tables(directory, table):
    for index in sorted((directory / "v1" / table).glob("*/*/data")):
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


# Reports that only a fresh run produces replay nothing, and record as usual.
logged = run("logged", extra=("--warn-redundant-assumptions", "--allow-warnings", "--log-format", "json;LogFileName=" + str(destination / "logged.json")))
assert logged["output"][0] == cold["output"][0] and logged["translated"] == MODULES
assert logged["stats"]["keyHits"] == 0 and logged["stats"]["bypassReason"] == "proof-dependency reporting", logged["stats"]

# Negative control: audit reports a stored module result that differs from a fresh one.
tampered = destination / "tampered-cache"
shutil.copytree(cache, tampered)
# The edits above stored results of A for other graphs too: each is altered.
for index, entry, manifest in [t for t in tables(tampered, "module-results") if t[2]["Module"] == "A"]:
    manifest["Tasks"][0]["ResourceCount"] += 1
    replace(tampered, index, entry, manifest)
caught = run("tampered-audit", tampered, extra=("--persistent-translation-audit",))
assert caught["stats"]["auditMismatches"] == 1, caught["stats"]
assert b"persistent translation audit: the stored results of module A differ" in caught["output"][1]

# Negative control: audit reports a stored translation that differs from a fresh one.
mistranslated = destination / "mistranslated-cache"
shutil.copytree(cache, mistranslated)
# The edits above stored translations under other keys too: every stored one is altered, and
# each of the four this program reads is reported.
for path in entries(mistranslated):
    _, payload = decode_entry(path)
    try:
        record = json.loads(payload)
    except (ValueError, UnicodeDecodeError):
        continue
    if isinstance(record, dict) and "Fingerprint" in record:
        record["Fingerprint"] = "0" * 64
        encode_entry(path, json.dumps(record).encode())
caught = run("mistranslated-audit", mistranslated, extra=("--persistent-translation-audit",))
assert caught["stats"]["auditMismatches"] == 4, caught["stats"]
assert b"persistent translation audit: the stored translation differs" in caught["output"][1]

# Parallel translation (--translation-workers) with the persistent tables, in a cache of its own.
# The stress schedule makes each group of four translations finish in reverse arrival order.
workers_cache = destination / "workers-cache"
WORKERS = ("--translation-workers", "4", "--translation-workers-stress", "-1")
parallel_cold = run("workers-cold", workers_cache, WORKERS)
same_report(cold, parallel_cold)
check(parallel_cold, MODULES, {m: None for m in MODULES}, 5)
# Positive control: the parallel path ran (every translation committed through the cursor), and it
# recorded what one worker records: the same translations and Boogie texts, under the same keys.
assert sorted(parallel_cold["commits"]) == MODULES, parallel_cold["commits"]
assert parallel_cold["stats"]["translationPublications"] == 4 and parallel_cold["stats"]["manifestPublications"] == 3
assert {e.name for e in entries(workers_cache)} <= {e.name for e in entries(cache)}, "a parallel translation differs"
# One worker replays what four recorded, and four replay what they recorded: no module that verified
# is translated or committed, and the failing one is translated and committed alone.
serial_warm = run("workers-serial-warm", workers_cache)
same_report(cold, serial_warm)
check(serial_warm, ["H"], {"A": "translation-key", "B": "translation-key", "C": "translation-key", "H": None}, 0)
parallel_warm = run("workers-warm", workers_cache, WORKERS)
same_report(cold, parallel_warm)
check(parallel_warm, ["H"], {"A": "translation-key", "B": "translation-key", "C": "translation-key", "H": None}, 0)
assert parallel_warm["commits"] == ["H"] and parallel_warm["stats"]["keyHits"] == 3, parallel_warm
# Early cutoff with four workers: a proof edit of A replays B by its regenerated graph (positive
# control); a function edit that B reads misses B (negative control).
edit("workers-body", "assert x + 0 == x;", "assert 0 + x == x;", ["A", "B", "H"],
     {"A": None, "B": "boogie-graph", "C": "translation-key", "H": None}, 1, workers_cache, WORKERS)
edit("workers-spec", "int { x + 1 }", "int { x + 2 }", ["A", "B", "H"],
     {"A": None, "B": None, "C": "translation-key", "H": None}, 1, workers_cache, WORKERS)
# With the in-process translation cache too, the three caches compose: a warm run is all key hits.
both = run("workers-both", workers_cache, WORKERS + ("--translation-cache",))
same_report(cold, both)
check(both, ["H"], {"A": "translation-key", "B": "translation-key", "C": "translation-key", "H": None}, 0)

# Corrupt every stored translation and module result: no false hits, the same report.
for index in (cache / "v1/module-results").glob("*/*/data"):
    blob = json.loads(index.read_text())["Blob"]
    (cache / "v1/blobs/sha256" / blob[:2] / blob / "data").write_bytes(b"truncated")
for path in entries(cache):
    path.write_bytes(b"truncated")
corrupt = run("corrupt")
same_report(cold, corrupt)
assert set(corrupt["modules"].values()) == {None} and corrupt["translated"] == MODULES
assert corrupt["stats"]["keyHits"] == 0 and corrupt["stats"]["graphHits"] == 0, corrupt["stats"]

# Without the option, no table of it is created (the resolution cache's own tables may be).
plain = destination / "plain-cache"
run("plain", plain, persistent=False)
assert not (plain / "v1/artifacts/translations").exists() and not (plain / "v1/module-results").exists()
print("persistent translation: warm runs translate nothing; early cutoff across edits, line shifts, checkouts and eviction; shared partitions; parallel translation; audit, environment, corruption and vacuity controls")
