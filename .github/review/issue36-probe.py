from pathlib import Path
import re
import subprocess
b = Path("Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues")
replacements = {"%baredafny": "out/dafny/Dafny", "%S": str(b), "%t": "results/github-issue-36.dfy.expect", "%review-z3-4.12.1": "z3h/z3-4.12.1", "%review-z3": "z3-5.1.0-x64-glibc-2.39/bin/z3"}
results = []
for line in (b / "github-issue-36.dfy").read_text().splitlines():
    if not line.startswith("// RUN: ") or "%diff" in line:
        continue
    command = line[8:]
    expected = re.search(r"%exits-with (\d+) ", command)
    if expected:
        command = command.replace(expected.group(), "")
    for old, new in replacements.items():
        command = command.replace(old, new)
    run = subprocess.run(command, shell=True, text=True, capture_output=True)
    results.append({"command": command, "exit": run.returncode, "expected": int(expected[1]) if expected else 0, "stderr": run.stderr})
import json
Path("results/commands.json").write_text(json.dumps(results, indent=2))
Path("results/probe-exit.txt").write_text(str(sum(r["exit"] != r["expected"] for r in results)) + "\n")
