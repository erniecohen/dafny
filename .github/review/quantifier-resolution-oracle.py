"""Quantified cache replay preserves native Boogie, verdicts and per-VC resources."""
import json
from pathlib import Path
import shutil
import subprocess
import sys

exe, solver, source, destination = (Path(value).resolve() for value in sys.argv[1:5])
shutil.rmtree(destination, ignore_errors=True)
destination.mkdir(parents=True)
baseline = None
for phase in ("fresh", "cold", "warm", "checked", "checked-again"):
    output = destination / phase
    output.mkdir()
    profile = output / "profile.jsonl"
    report = output / "proof.json"
    command = ["dotnet", str(exe), "verify", str(source), "--type-system-refresh", "--cores", "2",
        "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "1000000",
        "--allow-warnings", "--standard-libraries:false", "--use-basename-for-filename", "--show-snippets:false",
        "--bprint", str(output / "out.bpl"), "--pipeline-profile", str(profile),
        "--log-format", "json;LogFileName=" + str(report)]
    if phase != "fresh":
        command += ["--cache-dir", str(destination / "cache"), "--resolution-cache"]
    if phase.startswith("checked"):
        command += ["--resolution-cache-check"]
    run = subprocess.run(command, capture_output=True, timeout=120)
    assert run.returncode == 4, (phase, run.returncode, run.stdout, run.stderr)
    proofs = json.loads(report.read_text())["verificationResults"]
    costs = sorted((p["name"], v["vcNum"], v["outcome"], v["resourceCount"])
        for p in proofs for v in p["vcResults"])
    assert any(c[2] == "Valid" for c in costs), "positive proof was not checked"
    assert any(c[2] == "Invalid" for c in costs), "negative proof was not rejected"
    graphs = {p.name: p.read_bytes() for p in output.glob("*.bpl")}
    assert graphs, "no actual solver input"
    facts = (run.returncode, run.stdout, run.stderr, costs, graphs)
    baseline = baseline or facts
    assert facts == baseline, (phase, "changed verdict, diagnostics, Boogie or resources")
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    snapshots = [e for e in events if e.get("stage") == "resolve.snapshot"]
    if phase in ("warm", "checked", "checked-again"):
        assert snapshots and all(e["state"].startswith("hit") for e in snapshots), (phase, snapshots)
        if phase.startswith("checked"):
            assert all(e["state"].startswith("hit (checked ") for e in snapshots), "oracle did not run"
print("Quantifier resolution preserves Boogie, verdicts and per-VC resources with checked warm hits")
