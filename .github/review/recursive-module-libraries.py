#!/usr/bin/env python3
"""Archive round-trip and integrity controls for issue 466, stage 6."""
import copy
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import tomllib
import zipfile

dll, solver, fixture, directory = map(Path, sys.argv[1:])
dll, solver, fixture, directory = (p.resolve() for p in (dll, solver, fixture, directory))
directory.mkdir(parents=True, exist_ok=True)
count = 0


def run(label, command, files, expected=0, extra=(), recursive=True, threads=0):
    global count
    args = ["dotnet", str(dll), command, "--standard-libraries:false", "--type-system-refresh",
            "--member-invariants", "--use-basename-for-filename", "--show-snippets:false"]
    if threads:
        args += ["--resolution-threads", str(threads)]
    if recursive:
        args.append("--recursive-modules")
    args += ["--solver-path", str(solver)]
    if command != "resolve":
        args += ["--cores", "2", "--verification-time-limit", "30"]
    args += list(map(str, files)) + list(map(str, extra))
    result = subprocess.run(args, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
    if result.returncode != expected:
        raise AssertionError(f"{label}: expected {expected}, got {result.returncode}\n{result.stdout}")
    count += 1
    return result.stdout


def text(name, source):
    path = directory / name
    path.write_text(source)
    return path


library = directory / "group.doo"
client = text("client.dfy", """
module Client {
  import A
  lemma Facts(t: A.T) { assert t.n >= 0; assert A.Count(0) == 0; }
}
""")
bad = text("false.dfy", "module Client { import A lemma Bad(t: A.T) { assert false; } }")
run("producer", "build", [fixture], extra=["-t:lib", "--output", library])
with zipfile.ZipFile(library) as archive:
    original = {name: archive.read(name).decode("utf-8-sig") for name in archive.namelist()}
manifest = tomllib.loads(original["manifest.toml"])
record = manifest["recursive_modules"]
assert record["groups"] == [{"abstract": False, "members": ["A", "B"]}]
unchecked = directory / "unchecked.doo"
# Keep the ordinary .doo trust model, including its explicit --no-verify workflow.
run("ordinary unchecked workflow", "build", [fixture], extra=["-t:lib", "--no-verify", "--output", unchecked])
source_text = fixture.read_text()
bad_termination = text("bad-termination.dfy", source_text.replace("n - 1", "n"))
bad_witness = text("bad-witness.dfy", source_text.replace("witness T(0)", "witness T(-1)"))
for source in [bad_termination, bad_witness]:
    run("producer checks whole group", "build", [source], 4,
        ["-t:lib", "--output", source.with_suffix(".doo")])


def changed(name, source=None, update=None, seal=False, drop=False, manifest_edit=None):
    entries = original.copy()
    current = copy.deepcopy(record)
    if source is not None:
        entries["program"] = source
    if update:
        update(current)
    if seal:
        current["program_sha256"] = hashlib.sha256(entries["program"].encode()).hexdigest()
        canonical = {"Schema": current["schema"], "ProgramSha256": current["program_sha256"],
                     "ManifestSha256": current["manifest_sha256"],
                     "Groups": [{"Members": g["members"], "Abstract": g["abstract"]}
                                for g in current["groups"]]}
        current["record_sha256"] = hashlib.sha256(json.dumps(canonical, separators=(",", ":")).encode()).hexdigest()
    prefix = original["manifest.toml"].split("[recursive_modules]")[0]
    if not drop:
        prefix += "[recursive_modules]\n"
        for key in ["schema", "program_sha256", "manifest_sha256", "record_sha256"]:
            prefix += f"{key} = {json.dumps(current[key])}\n"
        groups = ["{abstract = " + str(g["abstract"]).lower() + ", members = " + json.dumps(g["members"]) + "}"
                  for g in current["groups"]]
        prefix += "groups = [" + ", ".join(groups) + "]\n"
    entries["manifest.toml"] = manifest_edit(prefix) if manifest_edit else prefix
    path = directory / (name + ".doo")
    with zipfile.ZipFile(path, "w") as archive:
        for key, value in entries.items():
            archive.writestr(key, value)
    return path


for threads in [0, 3]:
    run("round trip", "verify", [client], extra=["--library", library], threads=threads)
    run("unchecked archive round trip", "resolve", [client], extra=["--library", unchecked], threads=threads)
    run("reachable false", "verify", [bad], 4, ["--library", library], threads=threads)
    run("option required", "resolve", [client], 1, ["--library", library], recursive=False, threads=threads)
    # Integrity is independent of who wrote an unchanged record.
    valid = changed("resealed", seal=True)
    run("integrity positive", "resolve", [client], extra=["--library", valid], threads=threads)
    attacks = [
        changed("unknown-schema", update=lambda r: r.update(schema=2), seal=True),
        changed("modified-source", source=original["program"] + "\n// edited\n"),
        changed("modified-record", update=lambda r: r.update(record_sha256="0" * 64)),
        changed("modified-options", manifest_edit=lambda t: t.replace("enforce-determinism = false", "enforce-determinism = true")),
    ]
    for attack in attacks:
        run(attack.stem, "resolve", [client], 1, ["--library", attack], threads=threads)
    no_options = changed("missing-options", manifest_edit=lambda t: re.sub(
        r"\[options\][\s\S]*?(?=\[recursive_modules\])", "", t))
    message = run("missing options", "resolve", [client], 1, ["--library", no_options], threads=threads)
    assert "no options table" in message
    forged = changed("modified-proof", source=original["program"].replace(
        "module A {", "module A { lemma False() ensures false { assert false; }", 1))
    false_client = text("false-client.dfy", "module Client { import A lemma Bad() { A.False(); assert false; } }")
    run("modified proof", "verify", [false_client], 1, ["--library", forged], threads=threads)
    missing = changed("missing-record", drop=True)
    run("missing record", "resolve", [client], 2, ["--library", missing], threads=threads)
    split = changed("wrong-partition", update=lambda r: r.update(groups=[]), seal=True)
    run("recomputed partition", "resolve", [client], 2, ["--library", split], threads=threads)
    a = changed("partial-a", source="module A { import B }", seal=True)
    b = changed("partial-b", source="module B { import A }", seal=True)
    empty = text("empty.dfy", "module Empty {}")
    old = changed("older-acyclic", source="module Plain {}", drop=True)
    run("older acyclic library", "resolve", [empty], extra=["--library", old], threads=threads)
    run("split archives", "resolve", [empty], 2, ["--library", a, "--library", b], threads=threads)
    completing = text("complete.dfy", "module B { import A }")
    run("client joins partial archive", "resolve", [completing], 2, ["--library", a], threads=threads)
    joining = changed("joining", source="module A { import C } module B { import A }",
                      update=lambda r: r["groups"][0].update(members=["A", "B", "C"]), seal=True)
    bridge = text("bridge.dfy", "module C { import B }")
    run("client extends group", "resolve", [bridge], 2, ["--library", joining], threads=threads)
    refinement = text("refinement.dfy", "module Refined refines A {} module User { import Refined }")
    run("refinement stays above group", "resolve", [refinement], extra=["--library", library], threads=threads)
    co = text("codatatype.dfy", "module User { import A codatatype Co = Co(next: Co) }")
    run("group restrictions cross archive", "resolve", [co], 2, ["--library", library], threads=threads)

# Both abstract groups and a three-member group keep their partition through serialization.
abstract = text("abstract.dfy", "abstract module X { import Y type T } abstract module Y { import X }")
three = text("three.dfy", "module X { import Y } module Y { import Z } module Z { import X }")
for source in [abstract, three]:
    target = source.with_suffix(".doo")
    run("other producer", "build", [source], extra=["-t:lib", "--output", target])
    for threads in [0, 3]:
        run("other round trip", "resolve", [empty], extra=["--library", target], threads=threads)

print(f"recursive module archives: {count} controls passed")
