"""Persistent proof-dependency reports, warnings and verification coverage (issue 261)."""
import csv
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import zlib
import xml.etree.ElementTree as ET

all_reports = True
exe, solver, source, destination = (Path(p).resolve() for p in sys.argv[1:5])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
original = source.read_text(encoding="utf-8")
program = destination / "input.dfy"
program.write_text(original, encoding="utf-8")
cache = destination / "cache"
report = destination / "report.csv"
modules = ["Contracts", "DependencyControls", "Mixed", "Other", "Witness"]
contract = "module Contracts { method Read(a: array<int>, i: int) returns (x: int) requires 0 <= i < a.Length { x := a[i]; } }\n"
(destination / "contract.dfy").write_text(contract, encoding="utf-8")
json_report = destination / "report.json"


def canonical(value):
    if isinstance(value, dict):
        return {k: canonical(v) for k, v in value.items() if k not in ("runTime", "timingReplayed")}
    if isinstance(value, list):
        result = [canonical(v) for v in value]
        if result and isinstance(result[0], dict):
            if "name" in result[0]:
                result.sort(key=lambda v: v["name"])
            elif "vcNum" in result[0]:
                result.sort(key=lambda v: (v["vcNum"], v.get("randomSeed", "")))
        return result
    return value


def text_facts(value):
    # Only presentation times and the explicit replay annotations are volatile.
    lines = [line for line in value.splitlines() if not re.fullmatch(
        r'  (?:Overall time|Maximum assertion batch time): .*|    Duration: .*|(?:  |    )Timing replayed: true', line)]
    scopes = '\n'.join(lines).split('\nResults for ')
    assert not scopes[0].strip(), scopes[0]
    return sorted(scopes[1:])


def trx_facts(path):
    root = ET.parse(path).getroot()
    for node in root.iter():
        tag = node.tag.rsplit('}', 1)[-1]
        volatile = {
            'TestRun': ('id', 'name'), 'Times': ('creation', 'queuing', 'start', 'finish'),
            'TestSettings': ('id',), 'Deployment': ('runDeploymentRoot',),
            'UnitTestResult': ('executionId', 'computerName', 'duration', 'startTime', 'endTime', 'relativeResultsDirectory'),
            'Execution': ('id',), 'TestEntry': ('executionId',),
        }.get(tag, ())
        for key in volatile:
            node.attrib.pop(key, None)
    def canonical(node):
        tag = node.tag.rsplit('}', 1)[-1]
        if tag == 'StdOut' and (node.text or '').strip() == 'Timing replayed: true':
            return None
        children = [v for child in node if (v := canonical(child)) is not None]
        if tag == 'Output' and not children and not node.attrib and not (node.text or '').strip():
            return None
        if tag in ('Results', 'TestDefinitions', 'TestEntries'):
            children.sort(key=repr)
        return (node.tag, sorted(node.attrib.items()), (node.text or '') if len(node) == 0 else (node.text or '').strip(), children)
    return canonical(root)


def run(name, directory=cache, persistent=True, extra=(), input_file=program, formats=None):
    formats = formats if formats is not None else (("json", "text", "trx") if all_reports else ("json",))
    text_report = destination / "report.txt"
    trx_report = destination / "report.trx"
    report.unlink(missing_ok=True)
    json_report.unlink(missing_ok=True)
    profile = destination / (name + ".jsonl")
    args = [shutil.which("dotnet"), str(exe), "verify", str(input_file), "--cores", "1", "--type-system-refresh",
            "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "1000000",
            "--verification-cache", str(directory), "--pipeline-profile", str(profile), "--show-snippets:false",
            "--use-basename-for-filename", "--verify-included-files", "--boogie", "/randomSeed:7", "--isolate-assertions",
            "--log-format", "csv;LogFileName=" + str(report)]
    for fmt, path in (("json", json_report), ("text", text_report), ("trx", trx_report)):
        if fmt in formats:
            path.unlink(missing_ok=True)
            args += ["--log-format", fmt + ";LogFileName=" + str(path)]
    if directory is None:
        at = args.index("--verification-cache")
        del args[at:at + 2]
    coverage_dir = destination / "coverage"
    shutil.rmtree(coverage_dir, ignore_errors=True)
    args += ["--warn-redundant-assumptions", "--warn-contradictory-assumptions", "--allow-warnings", "--allow-axioms",
             "--verification-coverage-report", str(coverage_dir), "--no-timestamp-for-coverage-report"]
    if persistent:
        args += ["--persistent-translation"]
    environment = {k: v for k, v in os.environ.items() if k not in ("DAFNY_CACHE_DIR", "DAFNY_CACHE_PARTITION")}
    result = subprocess.run(args + list(extra), capture_output=True, timeout=300, env=environment, cwd=destination)
    (destination / (name + ".stdout")).write_bytes(result.stdout)
    (destination / (name + ".stderr")).write_bytes(result.stderr)
    assert result.returncode == 4 and report.is_file(), (result.returncode, result.stdout, result.stderr)
    rows = list(csv.DictReader(io.StringIO(report.read_text())))
    (destination / (name + ".csv")).write_bytes(report.read_bytes())
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    return {
        "coverage": {p.relative_to(coverage_dir).as_posix(): p.read_bytes() for p in sorted(coverage_dir.rglob("*.html"))},
        "output": (result.returncode, result.stdout, result.stderr), "rows": rows,
        "json": canonical(json.loads(json_report.read_text())) if "json" in formats else None,
        "raw_json": json.loads(json_report.read_text()) if "json" in formats else None,
        "text": text_facts(text_report.read_text()) if "text" in formats else None,
        "raw_text": text_report.read_text() if "text" in formats else None,
        "trx": trx_facts(trx_report) if "trx" in formats else None,
        "trx_markers": [(n.text or "").strip() for n in ET.parse(trx_report).getroot().iter() if n.tag.endswith("}StdOut")] if "trx" in formats else None,
        "facts": sorted(({k: v for k, v in row.items() if k not in ("TestResult.Duration", "TimingReplayed")} for row in rows), key=lambda row: sorted(row.items())),
        "stats": next((e for e in events if e.get("ev") == "persistent-translation"), None),
        "solver": next((e for e in events if e.get("ev") == "result-cache"), {"solverStarts": None})["solverStarts"],
        "snapshots": [e for e in events if e.get("stage") == "resolve.snapshot"],
        "prepared": sorted(e["module"] for e in events if e.get("ev") == "span" and e.get("stage") == "prepare"),
        "translated": sorted(e["module"] for e in events if e.get("ev") == "span" and e.get("stage") == "translate"),
    }


def same(oracle, actual):
    assert actual["output"] == oracle["output"], (actual["output"], oracle["output"])
    assert actual["facts"] == oracle["facts"], (actual["facts"], oracle["facts"])
    for fmt in ("json", "text", "trx", "coverage"):
        assert actual[fmt] == oracle[fmt], (fmt, actual[fmt], oracle[fmt])


def hit(actual):
    assert actual["stats"]["keyHits"] == 4 and actual["translated"] == ["Witness"], actual["stats"]
    assert actual["prepared"] == ["Witness"], actual["prepared"]
    assert actual["solver"] == 0 and all(r["TimingReplayed"] == "true" for r in actual["rows"]), actual
    if actual["raw_text"] is not None:
        assert actual["raw_text"].count("    Timing replayed: true") == len(actual["rows"]), actual["raw_text"]
        assert actual["raw_text"].count("  Overall outcome:") == actual["raw_text"].count("\n  Timing replayed: true"), actual["raw_text"]
    if actual["trx_markers"] is not None:
        assert actual["trx_markers"] == ["Timing replayed: true"] * len(actual["rows"]), actual["trx_markers"]


def records(directory):
    for index in sorted((directory / "v1/module-results").glob("*/*/data")):
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


# Independent solvers and cache-off controls: the failing assertion and exhausted task remain visible.
fresh = run("fresh", destination / "fresh-cache", False)
uncached = run("uncached", None, False)
same(fresh, uncached)
assert fresh["solver"] > 0 and b"1 error" in fresh["output"][1] and b"out of resource" in fresh["output"][1]
assert b"unnecessary requires clause" in fresh["output"][1]
assert b"proved using contradictory assumptions" in fresh["output"][1]
assert fresh["coverage"] and any("programElements" in scope for scope in fresh["json"]["verificationResults"])
cold = run("cold")
same(fresh, cold)
assert cold["stats"]["manifestPublications"] == 4 and cold["solver"] == fresh["solver"], cold["stats"]
warm = run("warm")
same(fresh, warm)
hit(warm)
assert all(v.get("randomSeed") == "7" for scope in warm["raw_json"]["verificationResults"] for v in scope["vcResults"])
if len(sys.argv) == 6 and sys.argv[5] == "--smoke":
    print("persistent dependencies smoke: PASS")
    sys.exit(0)
audit = run("audit", extra=("--persistent-translation-audit",))
same(fresh, audit)
assert audit["stats"]["auditChecks"] > 0 and audit["stats"]["auditMismatches"] == 0, audit["stats"]
assert audit["translated"] == modules

# Evict only translations: regenerated equal graphs must restore the dependency manager too.
graph_cache = destination / "graph-cache"
shutil.copytree(cache, graph_cache)
for entry in (graph_cache / "v1/artifacts/translations").glob("*/*"):
    if entry.is_file(): entry.unlink()
graph = run("graph-replay", graph_cache)
same(fresh, graph)
assert graph["stats"]["graphHits"] == 4 and graph["stats"]["keyHits"] == 0, graph["stats"]
assert graph["prepared"] == ["Witness"] and graph["solver"] == 0, graph

# Reporting sidecars also compose with actually reused and checked resolution snapshots.
resolution_cache = destination / "resolution-cache"
resolution_cold = run("resolution-cold", extra=("--cache-dir", str(resolution_cache)))
same(fresh, resolution_cold)
combined = run("resolution-warm", extra=("--cache-dir", str(resolution_cache), "--resolution-cache-check"))
same(fresh, combined)
hit(combined)
checked_hits = [e for e in combined["snapshots"] if e["state"].startswith("hit")]
assert {e["module"] for e in checked_hits} >= {"_module." + m for m in modules}, checked_hits
assert all(int(re.search(r"checked (\d+) objects", e["state"]).group(1)) > 0 for e in checked_hits), checked_hits

# Rebind both endpoints and source excerpts after moving the primary and included source.
moved = destination / "moved/renamed.dfy"
moved.parent.mkdir()
moved.write_text("\n\n\n" + original.replace('"contract.dfy"', '"contract-renamed.dfy"'))
(moved.parent / "contract-renamed.dfy").write_text("\n\n" + contract)
relocated = run("relocated", input_file=moved)
relocated_fresh = run("relocated-fresh", destination / "relocated-fresh-cache", False, input_file=moved)
same(relocated_fresh, relocated)
assert relocated["stats"]["keyHits"] + relocated["stats"]["graphHits"] == 4, relocated["stats"]

# A self-consistent unsupported or incomplete payload must miss as a whole module.
for kind in ("old-schema", "missing-inventory", "missing-task", "unknown-kind", "missing-original", "bad-original",
             "missing-warning", "missing-warning-field", "bad-start", "bad-end", "bad-index", "bad-scope", "duplicate-member"):
    broken = destination / (kind + "-cache")
    shutil.copytree(cache, broken)
    changed = 0
    for index, entry, record in records(broken):
        if record["Module"] != "DependencyControls":
            continue
        inventory = record["Proof"]
        if kind == "old-schema": record["Schema"] = 2
        elif kind == "missing-inventory": record["Proof"] = None
        elif kind == "missing-task":
            for task in record["Tasks"]: task["Proof"] = None
        elif kind == "bad-index":
            for task in record["Tasks"]: task["Proof"]["Covered"] = [-1]
        elif kind == "bad-scope":
            for i, scope in enumerate(inventory["Scopes"]): scope["Name"] = "no-current-scope-" + str(i)
        elif kind == "duplicate-member":
            scope = next(s for s in inventory["Scopes"] if s["Dependencies"])
            scope["Dependencies"].append(scope["Dependencies"][0])
        else:
            dep = inventory["Dependencies"][0]
            if kind == "unknown-kind": dep["Kind"] = "unsupported"
            elif kind == "missing-original": dep.pop("Original")
            elif kind == "bad-original": dep["SourceOriginal"] = "different source excerpt"
            elif kind == "missing-warning": dep["Warnings"] = None
            elif kind == "missing-warning-field": dep["Warnings"].pop("Vacuous")
            elif kind == "bad-start": dep["Start"]["Column"] += 1
            elif kind == "bad-end": dep["End"]["Offset"] = 2147483647
        replace(broken, index, entry, record)
        changed += 1
    assert changed > 0
    fallback = run(kind, broken)
    same(fresh, fallback)
    assert fallback["stats"]["keyHits"] == 3 and fallback["translated"] == ["DependencyControls", "Witness"], fallback["stats"]

# The audit compares report content and membership, beyond ordinary verdict/RU facts.
for kind in ("warning-fact", "coverage-membership", "dependency-description", "reported-excerpt"):
    broken = destination / (kind + "-cache")
    shutil.copytree(cache, broken)
    for index, entry, record in records(broken):
        if record["Module"] != "DependencyControls": continue
        if kind == "warning-fact": record["Proof"]["Dependencies"][0]["Warnings"]["Vacuous"] ^= True
        elif kind == "coverage-membership":
            task = next(t for t in record["Tasks"] if t["Proof"]["Covered"])
            task["Proof"]["Covered"].pop()
        elif kind == "reported-excerpt": record["Proof"]["Dependencies"][0]["Original"] = "different reported excerpt"
        else: record["Proof"]["Dependencies"][0]["Description"] = "different dependency description"
        replace(broken, index, entry, record)
    caught = run(kind, broken, extra=("--persistent-translation-audit",))
    assert caught["stats"]["auditMismatches"] > 0, caught["stats"]

# Analysis/refactoring still needs pruned-axiom data and follows the fresh route.
for flag in ("--suggest-proof-refactoring", "--analyze-proofs"):
    ordinary = run(flag[2:] + "-fresh", destination / (flag[2:] + "-fresh-cache"), False, extra=(flag,))
    fallback = run(flag[2:], extra=(flag,))
    same(ordinary, fallback)
    assert fallback["stats"]["keyHits"] == 0 and fallback["stats"]["bypassReason"] == "proof-refactoring reporting", fallback["stats"]
print("persistent dependencies: complete reports, warnings, coverage, real hits, current ranges, malformed records and audit controls: PASS")
