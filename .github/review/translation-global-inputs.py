"""Native/frozen module cache differentials, including failed obligations (issue 261)."""
import csv
import json
from pathlib import Path
import shutil
import subprocess
import sys

exe, solver, source, output = map(lambda value: Path(value).resolve(), sys.argv[1:5])
assert sys.argv[5:] in ([], ["--merged-optimizations"]), sys.argv[5:]
merged_optimizations = bool(sys.argv[5:])
if output.exists():
    shutil.rmtree(output)
output.mkdir(parents=True)
program = output / "input.dfy"
original = source.read_text()
program.write_text(original)
dotnet = shutil.which("dotnet")
invalid = subprocess.run([dotnet, str(exe), "verify", str(program), "--translation-global-inputs",
                          "--cores", "1", "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "1000000"],
                         capture_output=True, timeout=60)
assert invalid.returncode == 2 and b"--translation-global-inputs requires --type-system-refresh" in invalid.stdout, invalid


def run(name, enabled=True, persistent=True, store="cache", audit=False):
    profile, report = output / (name + ".jsonl"), output / (name + ".csv")
    args = [dotnet, str(exe), "verify", str(program), "--type-system-refresh", "--cores", "1",
            "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "1000000",
            "--show-snippets:false", "--use-basename-for-filename", "--allow-warnings", "--modular-termination",
            "--verification-cache", str(output / store), "--pipeline-profile", str(profile),
            "--log-format", "csv;LogFileName=" + str(report)]
    if merged_optimizations:
        # Keep native and cached solver histories identical. The fresh baseline uses
        # ordered translation and the original copier; cached modes compose the options.
        args += ["--boogie", "/restartProver"]
        if enabled:
            args += ["--optimize-translation-copy", "--ready-module-verification",
                     "--verification-module-window", "2", "--translation-workers", "2"]
    if enabled:
        args += ["--translation-global-inputs"]
    if persistent:
        args += ["--persistent-translation", "--persistent-counterexamples"]
    if audit:
        args += ["--persistent-translation-audit"]
    result = subprocess.run(args, capture_output=True, timeout=300)
    (output / (name + ".stdout")).write_bytes(result.stdout)
    (output / (name + ".stderr")).write_bytes(result.stderr)
    assert result.returncode == 4, (name, result.returncode, result.stdout, result.stderr)
    rows = sorted(({key: value for key, value in row.items()
                    if key not in ("TestResult.Duration", "TimingReplayed")}
                   for row in csv.DictReader(report.open())), key=repr)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    hits = {event["module"] for event in events if event.get("ev") == "persistent-module" and event.get("hit")}
    translated = {event["module"] for event in events
                  if event.get("ev") == "span" and event.get("stage") == "translate"}
    stats = next((event for event in events if event.get("ev") == "persistent-translation"), None)
    starts = next(event["solverStarts"] for event in events if event.get("ev") == "result-cache")
    stdout = b"\n".join(line for line in result.stdout.splitlines() if not line.startswith(b"Results File: "))
    return rows, (result.returncode, stdout, result.stderr), hits, translated, stats, starts


fresh = run("fresh", enabled=False, persistent=False, store="fresh-cache")
cold = run("cold")
warm = run("warm")
audit = run("audit", audit=True)
assert fresh[0] and any(row["TestResult.Outcome"] == "Failed" for row in fresh[0]), fresh[0]
outcomes = {row["TestResult.DisplayName"]: row["TestResult.Outcome"] for row in fresh[0]}
for name in ("A.Certify", "M.TooLow", "M.Bad"):
    assert outcomes[name + " (correctness)"] == "Failed", outcomes
assert outcomes["M.Good (correctness)"] == "Passed", outcomes
assert outcomes["A.Induction (correctness)"] == "Passed", outcomes
assert b"assertion might not hold" in fresh[1][1], fresh[1]
for observed in (cold, warm, audit):
    assert observed[:2] == fresh[:2], (observed[:2], fresh[:2])
assert cold[3] == {"A", "M"} and not cold[2] and cold[5] > 0, cold[2:]
assert warm[2] == {"A", "M"} and not warm[3] and warm[5] == 0, warm[2:]
assert audit[4]["auditChecks"] == 4 and audit[4]["auditMismatches"] == 0, audit[4]
assert audit[5] > 0, "audit must solve independently"

# The same source remains ineligible without the opt-in, even with populated module tables.
off = run("off", enabled=False, store="off-cache")
off_warm = run("off-warm", enabled=False, store="off-cache")
assert off[:2] == off_warm[:2] == fresh[:2]
assert not off_warm[2] and off_warm[3] == {"A", "M"}, off_warm[2:]

# Corruption cannot turn into a trusted hit. Regenerated identical graphs may still cut off.
entries = sorted((output / "cache/v1/artifacts/translations").glob("*/*"))
assert entries, "no translation records were published"
for entry in entries:
    data = bytearray(entry.read_bytes())
    data[-1] ^= 1
    entry.write_bytes(data)
corrupt = run("corrupt")
assert corrupt[:2] == fresh[:2] and corrupt[3] == {"A", "M"}, corrupt[2:]

# A current-source edit invalidates the old failed module, preserving the native verdict and RU.
program.write_text(original.replace("assert false;", "assert 1 == 0;"))
changed = run("changed")
changed_fresh = run("changed-fresh", enabled=False, persistent=False, store="changed-fresh-cache")
assert changed[:2] == changed_fresh[:2]
assert "M" in changed[3], changed[2:]  # A regenerated identical graph may still cut off safely.
print("global translation inputs: native/cold/warm/audit verdicts and per-VC resources agree; positive, negative, hit, corruption and stale controls passed")
