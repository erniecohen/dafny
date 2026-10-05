#!/usr/bin/env python3
"""Record paired diagnostics, including expected failures; never rebase verdicts."""
import csv, hashlib, json, pathlib, subprocess, sys
exe, solver, output = map(lambda x: pathlib.Path(x).resolve(), sys.argv[1:])
output.mkdir(parents=True, exist_ok=True)
def capture(command):
    return subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
metadata = {"commit": capture(["git", "rev-parse", "HEAD"]).stdout.strip(),
 "dafny": capture([str(exe), "--version"]).stdout.strip(),
 "solver": capture([str(solver), "--version"]).stdout.strip(),
 "solver_sha256": hashlib.sha256(solver.read_bytes()).hexdigest(), "cases": []}
for source in sorted(pathlib.Path(".github/review/issue100-baseline").glob("*.dfy")):
 for refresh in [False, True]:
  for axioms in [False, True]:
   name = f"{source.stem}-refresh-{refresh}-axioms-{axioms}"
   directory = output / name
   directory.mkdir(exist_ok=True)
   command = [str(exe), "verify", str(source), "--solver-path", str(solver),
     "--cores", "1", "--resource-limit", "16000000", "--verification-time-limit", "30",
     f"--type-system-refresh:{str(refresh).lower()}", f"--additional-axioms:{str(axioms).lower()}",
     "--boogie", "/normalizeDeclarationOrder:0", "--boogie", f"/print:{directory / 'program.bpl'}",
     "--log-format", f"csv;LogFileName={directory / 'resources.csv'}"]
   result = capture(command)
   (directory / "output.txt").write_text(result.stdout)
   row = {"name": name, "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
     "command": command, "exit": result.returncode}
   (directory / "result.json").write_text(json.dumps(row, indent=2))
   metadata["cases"].append(row)
   print(name, result.returncode, result.stdout.splitlines()[-1:] or [])
(output / "summary.json").write_text(json.dumps(metadata, indent=2))
