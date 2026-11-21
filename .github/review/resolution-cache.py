"""Cross-process controls for the persistent per-module resolution cache (issue 261).

Every cached run must agree with an uncached run of the same tree and options: exit code, output,
module Boogie and per-VC outcomes and resource counts. The profile's resolve.snapshot events show
which modules were reused: a warm run reuses every module, an edit re-resolves only the modules
whose results it can change, and --resolution-cache-check compares every reused module with a
fresh resolution of it."""
import json
from pathlib import Path
import re
import subprocess
import sys

exe, solver, source, destination = (Path(s).resolve() for s in sys.argv[1:5])
assert not destination.exists(), "use a fresh test directory"
destination.mkdir(parents=True)
tree = destination / "tree"
tree.mkdir()
cache = destination / "cache"
for command in ("verify", "resolve"):
    help_text = subprocess.run(["dotnet", str(exe), command, "--help"], capture_output=True, timeout=60)
    assert help_text.returncode == 0 and b"--resolution-cache" not in help_text.stdout, "the option must remain hidden"

# The modules live in separate files, so that an edit of one does not move the others.
A = """module A {
  function F(x: int): int { x + 1 }
}
"""
B = """include "A.dfy"
module B {
  import A
  lemma L(x: int) ensures A.F(x) == x + 1 {}
}
"""
C = """include "B.dfy"
module D {
  method Bad() { assert false; }
}
module C {
  import A
  import B
  lemma M() ensures A.F(1) == 2 { B.L(1); }
  method Shadow(x: int) { var y := x; if y > 0 { var y := 1; } }
}
"""


def write(a=A, b=B, c=C):
    for name, text in (("A", a), ("B", b), ("C", c)):
        (tree / (name + ".dfy")).write_text(text)


main = tree / "C.dfy"


runs = 0


def run(name, cached, *options):
    global runs
    runs += 1
    profile = destination / (name + ".jsonl")
    bpl = destination / name / "out.bpl"
    bpl.parent.mkdir()
    args = ["dotnet", str(exe), "verify", str(main), "--type-system-refresh", "--cores", "1",
            "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "1000000",
            "--allow-warnings", "--standard-libraries:false", "--use-basename-for-filename", "--show-snippets:false",
            "--bprint", str(bpl), "--pipeline-profile", str(profile),
            "--log-format", "json;LogFileName=" + str(destination / (name + ".proof.json"))] + list(options)
    if cached:
        args += ["--resolution-cache", str(cache)]
    result = subprocess.run(args, capture_output=True, timeout=180)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    snapshots = {e["module"]: e for e in events if e.get("stage") == "resolve.snapshot"}
    graphs = {p.name: p.read_bytes() for p in bpl.parent.iterdir()}
    assert graphs, "the control must emit actual Boogie"
    proofs = json.loads((destination / (name + ".proof.json")).read_text())["verificationResults"]
    costs = sorted((p["name"], v["vcNum"], v["outcome"], v["resourceCount"])
                   for p in proofs for v in p["vcResults"])
    assert costs, "the control must run actual verification tasks"
    return (result.returncode, result.stdout, result.stderr, graphs, costs), snapshots


def states(snapshots):
    return {module: re.sub(r" \(checked \d+ objects\)$", " (checked)", e["state"]) for module, e in snapshots.items()}


def reused(snapshots):
    return sorted(m for m, e in snapshots.items() if e["state"].startswith("hit"))


def control(name, *options, expect_reused, check=False):
    """An uncached run and a cached one of the current tree; returns the modules the cached one reused."""
    off, events = run(name + "-off", False, *options)
    assert not events, "without the option there are no snapshot events"
    on, snapshots = run(name, True, *options, *(["--resolution-cache-check"] if check else []))
    assert on == off, (name, "the cached run must preserve exit, output, Boogie and verification costs", on[:3], off[:3])
    for module, e in snapshots.items():
        hit = e["state"].startswith("hit")
        assert hit or e["state"] == "stored", (name, module, e["state"])
        assert e["resolverCalls"] == (0 if hit and not check else 1), (name, module, e)
        if check and hit:
            assert int(re.search(r"checked (\d+) objects", e["state"]).group(1)) > 0, (name, module, "vacuous check")
    assert reused(snapshots) == sorted(expect_reused), (name, states(snapshots))
    return snapshots, off


write()
modules = sorted(["_module", "_module.A", "_module.B", "_module.C", "_module.D", "B.A", "C.A", "C.B"])
# A cold run stores every module, and the off run fails on D only.
cold, off = control("cold", expect_reused=[])
assert off[0] == 4 and b"1 verified, 1 error" in off[1], off[:3]
assert sorted(cold) == modules, sorted(cold)
assert len(list(cache.glob("*.dres"))) == len(modules)
# A warm run reuses every module, resolves none, and each reused module equals a fresh resolution of it.
control("warm", expect_reused=modules)
control("warm-check", expect_reused=modules, check=True)

# A comment inside B's lemma changes B's source but not its result: B is resolved again, and its dependents
# are still reused (early cutoff).
commented = B.replace("ensures", "/* a comment */ ensures")
write(b=commented)
control("comment", expect_reused=[m for m in modules if m != "_module.B"], check=True)
# Lines inserted above the modules of a file move them without changing their text: every module is reused,
# with the tokens of the current parse.
write(b=commented, c="// a comment\n\n" + C.replace("module C {", "\n\nmodule C {"))
control("lines-above", expect_reused=modules, check=True)

# A new declaration in A changes A's result: A and the modules that see it are resolved again.
write(a=A.replace("}\n", "}\n  function G(): int { 0 }\n", 1), b=commented)
control("signature", expect_reused=["_module.D"], check=True)

# A new use of B's lemma changes C, and the default module, which contains C.
write(a=A.replace("}\n", "}\n  function G(): int { 0 }\n", 1), b=commented,
      c=C.replace("B.L(1); }", "B.L(1); B.L(2); }"))
control("body", expect_reused=["_module.A", "_module.B", "_module.D", "B.A", "C.A", "C.B"], check=True)

# A resolution option is part of every key: --warn-shadowing reuses nothing, and warns in C.
_, shadow = control("option", "--warn-shadowing", expect_reused=[])
assert b"Shadowed local-variable name: y" in shadow[1], shadow[1]
control("option-warm", "--warn-shadowing", expect_reused=modules, check=True)

# A corrupt entry is a miss: the module is resolved again and its entry rewritten.
write()
control("rewarm", expect_reused=modules)
key = cold["_module.B"]["key"]
(cache / (key + ".dres")).write_bytes(b"DRES\x01")
off, events = run("corrupt-off", False)
on, snapshots = run("corrupt", True)
assert on == off, "corruption must re-resolve rather than restore any cached facts"
assert snapshots["_module.B"]["state"].startswith("invalid: "), snapshots["_module.B"]
assert reused(snapshots) == [m for m in modules if m != "_module.B"], states(snapshots)
control("repaired", expect_reused=modules, check=True)

# The rest of the key, in a second tree with its own cache. Q's text and place do not depend on its parent's
# name, and the prefix of the generated names of N17608 changes when a module whose name digest starts the
# same way (N67866) joins the program.
tree = destination / "tree2"
tree.mkdir()
cache = destination / "cache2"
main = tree / "X.dfy"
X = """module P {
  module Q { lemma R(x: int) ensures (var y := x; y) == x {} }
}
module N17608 {
  lemma S(x: int) ensures (var y := x; y) == x {}
  method Bad() { assert false; }
}
"""
main.write_text(X)
modules = sorted(["_module", "_module.P", "P.Q", "_module.N17608"])
control("cold2", expect_reused=[])
control("warm2", expect_reused=modules, check=True)
# Moved text keeps every key: the modules are reused at their new positions.
main.write_text("\n\n" + X)
control("move", expect_reused=modules, check=True)
# Another checkout of the same tree, under another directory, shares the entries of the first.
other = destination / "second-checkout" / "tree2"
other.mkdir(parents=True)
(other / "X.dfy").write_text("\n\n" + X)
main = other / "X.dfy"
control("checkout", expect_reused=modules, check=True)
# Changed text in Q misses Q and the modules that contain it, in either checkout.
(other / "X.dfy").write_text("\n\n" + X.replace("(var y := x; y) == x {} }", "(var y := x + 0; y) == x {} }"))
control("changed", expect_reused=["_module.N17608"], check=True)
main = tree / "X.dfy"
# A renamed parent re-resolves its unchanged child, whose name changes.
main.write_text(X.replace("module P", "module T"))
control("rename", expect_reused=["_module.N17608"], check=True)
# A new module can change another's naming prefix, and so the names its resolution generates.
main.write_text(X + "module N67866 { }\n")
control("prefix", expect_reused=["_module.P", "P.Q"], check=True)
print("resolution cache: %d runs; cold stores, warm reuses all and checks equal; a comment re-resolves one module, "
      "a signature its dependents, an option all; a corrupt entry is a miss; lines above, a move and another "
      "checkout reuse all; changed text, a rename and a new naming prefix re-resolve the modules they change" % runs)
