"""Cross-process controls for the closed direct-alias resolution snapshot schema."""
import json
from pathlib import Path
import subprocess
import sys
import zipfile

exe, solver, source, destination = (Path(s).resolve() for s in sys.argv[1:5])
assert not destination.exists(), "use a fresh test directory"
destination.mkdir(parents=True)
cache = destination / "cache"
for command in ("verify", "resolve"):
    help_text = subprocess.run(["dotnet", str(exe), command, "--help"], capture_output=True, timeout=60)
    assert help_text.returncode == 0 and b"--resolution-cache" not in help_text.stdout, "the option must remain hidden"


def run(name, enabled=True):
    profile = destination / (name + ".jsonl")
    args = ["dotnet", str(exe), "verify", str(source), "--type-system-refresh", "--cores", "1",
            "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "1000000",
            "--allow-warnings", "--standard-libraries:false", "--use-basename-for-filename", "--show-snippets:false",
            "--bprint", str(destination / (name + ".bpl")), "--pipeline-profile", str(profile),
            "--log-format", "json;LogFileName=" + str(destination / (name + ".proof.json"))]
    if enabled:
        args += ["--resolution-cache", str(cache)]
    result = subprocess.run(args, capture_output=True, timeout=120)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    snapshots = [e for e in events if e.get("stage") == "resolve.snapshot"]
    graphs = {p.name[len(name):]: p.read_bytes() for p in destination.glob(name + "_*.bpl")}
    assert graphs, "the control must emit actual module Boogie"
    proofs = json.loads((destination / (name + ".proof.json")).read_text())["verificationResults"]
    costs = sorted((p["name"], v["vcNum"], v["outcome"], v["resourceCount"])
                   for p in proofs for v in p["vcResults"])
    assert costs, "the control must run actual verification tasks"
    return (result.returncode, result.stdout, result.stderr, graphs, costs), snapshots


off, events = run("off", False)
assert off[0] == 4 and b"1 error" in off[1], off[:3]
assert not events and not cache.exists()
cold, events = run("cold")
assert cold == off, "cold lookup must preserve diagnostics, exit and module Boogie"
aliases = [e for e in events if e["state"] == "miss"]
assert len(aliases) == 1 and aliases[0]["module"] == "Client.D", events
warm, events = run("warm")
assert warm == off, "typed load must preserve diagnostics, exit and module Boogie"
assert {e["module"] for e in events if e["state"] == "hit"} == {"Client.D"}, events
assert all(e["resolverCalls"] == (0 if e["state"] == "hit" else 1) for e in events), events
archive, = cache.glob("*.dres")
with zipfile.ZipFile(archive) as stored:
    assert stored.namelist() == ["manifest.toml", "module.cbor"]
    assert not stored.read("module.cbor").startswith(source.read_bytes()), "source is not a typed snapshot"
archive.write_bytes(b"PK\x03\x04")
rejected, events = run("corrupt")
assert rejected == off, "corruption must re-resolve rather than restore any cached facts"
assert {e["module"] for e in events if e["state"] == "rejected"} == {"Client.D"}, events
print("resolution snapshots: cold miss, warm typed hit, corrupt miss; identical Boogie, per-VC resource counts and positive/negative verdicts")
