"""Complete source-bound terminal counterexample reports for module replay (issue 261)."""
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
        args += ["--persistent-translation", "--persistent-counterexamples"]
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
    assert actual["stats"]["keyHits"] == 5 and actual["translated"] == [], actual["stats"]
    assert actual["prepared"] == [], actual["prepared"]
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


def main():
    # Independent cache-off and cold solvers establish every diagnostic and per-VC resource fact.
    fresh = run("fresh", destination / "fresh-cache", False)
    uncached = run("uncached", None, False)
    same(fresh, uncached)
    assert fresh["solver"] > 0 and b"3 errors" in fresh["output"][1], fresh["output"]
    cold = run("cold")
    same(fresh, cold)
    assert cold["stats"]["manifestPublications"] == 5, cold["stats"]
    warm = run("warm")
    same(fresh, warm)
    hit(warm)
    if len(sys.argv) == 6 and sys.argv[5] == "--smoke":
        print("persistent counterexamples smoke: PASS")
        sys.exit(0)
    audit = run("audit", extra=("--persistent-translation-audit",))
    same(fresh, audit)
    assert audit["stats"]["auditChecks"] > 0 and audit["stats"]["auditMismatches"] == 0, audit["stats"]

    # Turning the new opt-in off retains the previous failed-module path, even with its records present.
    off = run("opt-out", extra=("--persistent-counterexamples:false",))
    same(fresh, off)
    assert off["stats"]["keyHits"] == 4 and off["prepared"] == ["Witness"], off["stats"]

    # Rebuilt equal graphs can also restore the terminal diagnostics.
    graph_cache = destination / "graph-cache"
    shutil.copytree(cache, graph_cache)
    for entry in (graph_cache / "v1/artifacts/translations").glob("*/*"):
        if entry.is_file(): entry.unlink()
    graph = run("graph-replay", graph_cache)
    same(fresh, graph)
    assert graph["stats"]["graphHits"] == 5 and graph["prepared"] == [] and graph["solver"] == 0, graph

    # Both primary and related ranges must bind to the current file and line positions.
    moved = destination / "renamed.dfy"
    (destination / "renamed-contract.dfy").write_text("\n\n\n" + contract, encoding="utf-8")
    moved.write_text("\n\n" + original.replace('include "contract.dfy"', 'include "renamed-contract.dfy"'), encoding="utf-8")
    relocated_fresh = run("relocated-fresh", destination / "relocated-fresh-cache", False, input_file=moved)
    relocated = run("relocated-warm", input_file=moved)
    same(relocated_fresh, relocated)
    hit(relocated)
    assert b"renamed.dfy" in relocated["output"][1], relocated["output"]

    # A semantically changed failing module must not replay its old diagnostics or error count.
    edited = destination / "edited.dfy"
    edited.write_text(original.replace("assert false;", "assert true;"), encoding="utf-8")
    edited_cache = destination / "edited-cache"
    shutil.copytree(cache, edited_cache)
    edited_fresh = run("edited-fresh", destination / "edited-fresh-cache", False, input_file=edited)
    edited_cold = run("edited-cold", edited_cache, input_file=edited)
    same(edited_fresh, edited_cold)
    assert edited_cold["stats"]["keyHits"] == 4 and edited_cold["prepared"] == ["Witness"], edited_cold["stats"]
    edited_warm = run("edited-warm", edited_cache, input_file=edited)
    same(edited_fresh, edited_warm)
    hit(edited_warm)

    # Model payloads restore without translating or preparing the failing module.
    model_fresh = run("model-fresh", destination / "model-fresh-cache", False, extra=("--extract-counterexample",))
    model_cold = run("model-cold", destination / "model-cache", extra=("--extract-counterexample",))
    same(model_fresh, model_cold)
    model_warm = run("model-warm", destination / "model-cache", extra=("--extract-counterexample",))
    same(model_fresh, model_warm)
    hit(model_warm)

    # A timer-bounded failing task is not made replayable by its diagnostic packet.
    timed_cache = destination / "timed-cache"
    timed_fresh = run("timed-fresh", destination / "timed-fresh-cache", False, extra=("--verification-time-limit", "20"))
    timed_cold = run("timed-cold", timed_cache, extra=("--verification-time-limit", "20"))
    same(timed_fresh, timed_cold)
    timed_warm = run("timed-warm", timed_cache, extra=("--verification-time-limit", "20"))
    same(timed_fresh, timed_warm)
    assert "Witness" in timed_warm["prepared"], timed_warm["stats"]

    # Rendering choices and multiple counterexamples in one VC use the same complete packet.
    multi = destination / "multiple.dfy"
    multi.write_text(original.replace("lemma Bad() { assert false; }",
      "lemma Bad(x: int, y: int) { assert x > 0; assert y > 0; }"), encoding="utf-8")
    for name, extra, input_file in (
        ("snippets", ("--show-snippets", "--show-proof-obligation-expressions"), program),
        ("json-diagnostics", ("--json-diagnostics",), program),
        ("multiple", ("--isolate-assertions:false", "--error-limit", "3"), multi)):
        directory = destination / (name + "-cache")
        control = run(name + "-fresh", destination / (name + "-fresh-cache"), False, extra=extra, input_file=input_file)
        for mode in ("cold", "warm", "audit"):
            actual = run(name + "-" + mode, directory,
              extra=extra + (("--persistent-translation-audit",) if mode == "audit" else ()), input_file=input_file)
            same(control, actual)
            if mode == "warm": hit(actual)
            if mode == "audit": assert actual["stats"]["auditMismatches"] == 0, actual["stats"]
        if name == "multiple":
            assert max(t.get("Failure", {}).get("Counterexamples", 0) if t.get("Failure") else 0
              for _, _, record in records(directory) for t in record["Tasks"]) >= 2

    # Coherent content-addressed corruptions must miss, rather than manufacture a hit.
    import copy
    base = next((i, e, r) for i, e, r in records(cache) if any(t.get("Failure") for t in r["Tasks"]))
    for name in ("missing-packet", "missing-count", "wrong-count", "unknown-level", "wrong-source", "empty-message", "bad-excerpt", "bad-related", "missing-related"):
        directory = destination / ("bad-" + name)
        shutil.copytree(cache, directory)
        index, entry, record = base[0], copy.deepcopy(base[1]), copy.deepcopy(base[2])
        task = next(t for t in record["Tasks"] if t.get("Failure"))
        failure = task["Failure"]
        diagnostic = failure["Diagnostics"][0]
        if name == "missing-packet": task["Failure"] = None
        elif name == "missing-count": failure.pop("Counterexamples")
        elif name == "wrong-count": failure["Counterexamples"] += 1
        elif name == "unknown-level": diagnostic["Level"] = 99
        elif name == "wrong-source": diagnostic["Source"] = 0
        elif name == "empty-message": diagnostic["MessageParts"] = []
        elif name == "bad-excerpt": diagnostic["Range"]["SourceOriginal"] = "changed"
        elif name == "bad-related":
            diagnostic = next(d for t in record["Tasks"] if t.get("Failure") for d in t["Failure"]["Diagnostics"] if d["Related"])
            diagnostic["Related"][0]["Range"]["SourceOriginal"] = "changed"
        elif name == "missing-related": diagnostic.pop("Related")
        replace(directory, directory / index.relative_to(cache), entry, record)
        actual = run(name, directory)
        same(fresh, actual)
        assert "Witness" in actual["prepared"], actual["stats"]

    # The equality oracle includes prose and nested related information, not just verdicts.
    for name in ("message", "related-message"):
        directory = destination / ("audit-" + name)
        shutil.copytree(cache, directory)
        index, entry, record = base[0], copy.deepcopy(base[1]), copy.deepcopy(base[2])
        failure = next(t["Failure"] for t in record["Tasks"] if t.get("Failure"))
        if name == "message": failure["Diagnostics"][0]["MessageParts"][0] = "changed assertion message"
        else:
            diagnostic = next(d for t in record["Tasks"] if t.get("Failure") for d in t["Failure"]["Diagnostics"] if d["Related"])
            diagnostic["Related"][0]["MessageParts"][0] = "changed related message"
        replace(directory, directory / index.relative_to(cache), entry, record)
        actual = run("audit-" + name, directory, extra=("--persistent-translation-audit",))
        assert actual["stats"]["auditMismatches"] > 0, actual["stats"]
        code, stdout, stderr = actual["output"]
        assert b"persistent translation audit:" in stdout
        actual["output"] = (code, b"\n".join(line for line in stdout.split(b"\n")
          if b"persistent translation audit:" not in line), stderr)
        same(fresh, actual)

    invalid = subprocess.run([shutil.which("dotnet"), str(exe), "verify", str(program), "--persistent-counterexamples"], capture_output=True)
    assert invalid.returncode != 0 and b"requires --persistent-translation" in invalid.stdout + invalid.stderr, invalid
    print("persistent counterexamples: PASS")


if __name__ == "__main__":
    main()
