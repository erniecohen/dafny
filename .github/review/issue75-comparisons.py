"""Record exact runtime-migration resource comparisons; probes always exit zero."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess

out = Path("issue75-comparisons").resolve()
out.mkdir(exist_ok=True)
report = {}
def run(name, command, timeout=3600):
    try:
        result = subprocess.run(command, capture_output=True, text=True, timeout=timeout)
        (out / (name + ".txt")).write_text(result.stdout + result.stderr)
        report[name] = result.returncode
    except Exception as error:
        report[name] = str(error)

try:
    binaries = {name: str(Path(name + "/dafny/Dafny").resolve()) for name in ["baseline", "candidate"]}
    common = ["--baseline", binaries["baseline"], "--candidate", binaries["candidate"]]
    run("library-translation", ["python3", ".github/review/issue33-translation.py"] + common +
        ["--output", str(out / "translation")])
    sample = "dafny0/ForLoops.dfy,dafny0/SubsetTypes.dfy,dafny0/MultiSets.dfy,dafny1/Queue.dfy,dafny1/ExtensibleArray.dfy,dafny1/SchorrWaite.dfy,dafny2/SnapshotableTrees.dfy,dafny4/Ackermann.dfy,dafny4/Lucas-up.dfy"
    run("paired-sample", ["python3", ".github/review/issue74-corpus.py"] + common +
        ["--z3", str(Path("z3-5.1.0-x64-glibc-2.39/bin/z3").resolve()), "--output", str(out / "sample"),
         "--only", sample, "--jobs", "4"])
    for mode in ["off", "on"]:
        batches = {}
        unavailable = {}
        for build in ["baseline", "candidate"]:
            rows, missing = {}, {}
            for path in Path("measurements", build).glob("verdicts-" + mode + "-*/resources-*.json"):
                data = json.loads(path.read_text())
                overlap = set(rows) & set(data["batches"])
                assert not overlap, overlap
                rows.update(data["batches"])
                missing.update(data["unavailable"])
            assert rows, "No suite resources found: " + build
            batches[build], unavailable[build] = rows, missing
        changes = [{"batch": key, "baseline": batches["baseline"].get(key), "candidate": batches["candidate"].get(key)}
                   for key in sorted(set(batches["baseline"]) | set(batches["candidate"]))
                   if batches["baseline"].get(key) != batches["candidate"].get(key)]
        report["suite-" + mode] = {"baseline_batches": len(batches["baseline"]), "candidate_batches": len(batches["candidate"]),
                                   "changes": changes, "unavailable": unavailable}
    spec = importlib.util.spec_from_file_location("std", ".github/review/std-verdicts.py")
    std = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(std)
    for mode in ["off", "on"]:
        rows = {build: std.load(str(Path("measurements", build, "std-verdicts-" + mode + "-z3-5.1.0", "std-verdicts.tsv")))
                for build in ["baseline", "candidate"]}
        differences, costs = [], []
        for key in sorted(set(rows["baseline"]) | set(rows["candidate"])):
            old, new = rows["baseline"].get(key), rows["candidate"].get(key)
            if old is None or new is None or std.verdict(old) != std.verdict(new):
                differences.append({"declaration": key, "baseline": old, "candidate": new})
            elif not key.startswith("run ") and old[4] != new[4]:
                costs.append({"declaration": key, "baseline": old[4], "candidate": new[4]})
        report["library-" + mode] = {"baseline_rows": len(rows["baseline"]), "candidate_rows": len(rows["candidate"]),
                                    "verdict_changes": differences, "resource_changes": costs}
except Exception as error:
    report["exception"] = str(error)
finally:
    (out / "summary.json").write_text(json.dumps(report, indent=2))
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a") as stream:
            stream.write("## .NET runtime comparison probe\n\nInspect the artifact summary, paired sample and translation reports before acceptance.\n")
    print(json.dumps(report, indent=2))
