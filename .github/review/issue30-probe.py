import csv
import hashlib
import json
import os
from pathlib import Path
import subprocess

root = Path.cwd()
out = root / "probe"
out.mkdir(exist_ok=True)
results = []
for refresh in ["false", "true"]:
    for mode, extra in [("default", []), ("off", ["--boogie", "/deterministicLiteralHashes:0"]), ("on", ["--boogie", "/deterministicLiteralHashes:1"])]:
        runs = []
        for i in range(8):
            name = f"{refresh}-{mode}-{i}"
            smt = out / (name + ".smt2")
            csv_path = out / (name + ".csv")
            command = ["dotnet", "out/dafny/Dafny.dll", "verify", ".github/review/issue30-literals.dfy", "--solver-path", os.environ["Z3"], "--cores", "1", "--type-system-refresh:" + refresh, "--resource-limit", "16000000", "--boogie", "/proverLog:" + str(smt), "--log-format", "csv;LogFileName=" + str(csv_path)] + extra
            try:
                run = subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
                (out / (name + ".out")).write_text(run.stdout)
                resources = []
                if csv_path.exists():
                    with csv_path.open() as stream:
                        resources = [{k:v for k,v in row.items() if "resource" in k.lower() or "name" in k.lower() or "outcome" in k.lower()} for row in csv.DictReader(stream)]
                runs.append({"exit": run.returncode, "smt": hashlib.sha256(smt.read_bytes()).hexdigest() if smt.exists() else None, "resources": resources, "output": run.stdout})
            except subprocess.TimeoutExpired:
                runs.append({"exit": "timeout"})
        results.append({"resolver": refresh, "mode": mode, "runs": runs, "unique_smt": len({x.get("smt") for x in runs}), "unique_resources": len({json.dumps(x.get("resources"), sort_keys=True) for x in runs})})
(out / "results.json").write_text(json.dumps(results, indent=2))
with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as stream:
    for result in results:
        stream.write(f"Resolver refresh={result['resolver']}, mode={result['mode']}: exits={[x['exit'] for x in result['runs']]}, unique SMT logs={result['unique_smt']}, unique resource rows={result['unique_resources']}\n\n")
