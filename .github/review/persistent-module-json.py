"""Complete ordinary JSON assertion reports from persistent module results."""
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

all_reports = len(sys.argv) == 6 and sys.argv[5] == "--text-trx"
exe, solver, source, destination = (Path(p).resolve() for p in sys.argv[1:5])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
original = source.read_text(encoding="utf-8")
program = destination / "input.dfy"
program.write_text(original, encoding="utf-8")
cache = destination / "cache"
report = destination / "report.csv"
modules = ["Contracts", "Mixed", "Other", "Witness"]
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
        "output": (result.returncode, result.stdout, result.stderr), "rows": rows,
        "json": canonical(json.loads(json_report.read_text())) if "json" in formats else None,
        "raw_json": json.loads(json_report.read_text()) if "json" in formats else None,
        "text": text_facts(text_report.read_text()) if "text" in formats else None,
        "raw_text": text_report.read_text() if "text" in formats else None,
        "trx": trx_facts(trx_report) if "trx" in formats else None,
        "trx_markers": [(n.text or "").strip() for n in ET.parse(trx_report).getroot().iter() if n.tag.endswith("}StdOut")] if "trx" in formats else None,
        "facts": [{k: v for k, v in row.items() if k not in ("TestResult.Duration", "TimingReplayed")} for row in rows],
        "stats": next((e for e in events if e.get("ev") == "persistent-translation"), None),
        "solver": next(e for e in events if e.get("ev") == "result-cache")["solverStarts"],
        "snapshots": [e for e in events if e.get("stage") == "resolve.snapshot"],
        "prepared": sorted(e["module"] for e in events if e.get("ev") == "span" and e.get("stage") == "prepare"),
        "translated": sorted(e["module"] for e in events if e.get("ev") == "span" and e.get("stage") == "translate"),
    }


def same(oracle, actual):
    assert actual["output"] == oracle["output"], (actual["output"], oracle["output"])
    assert actual["facts"] == oracle["facts"], (actual["facts"], oracle["facts"])
    for fmt in ("json", "text", "trx"):
        assert actual[fmt] == oracle[fmt], (fmt, actual[fmt], oracle[fmt])


def hit(actual):
    assert actual["stats"]["keyHits"] == 3 and actual["translated"] == ["Witness"], actual["stats"]
    assert actual["prepared"] == ["Witness"], actual["prepared"]
    assert actual["solver"] == 0 and all(r["TimingReplayed"] == "true" for r in actual["rows"]), actual
    if actual["raw_text"] is not None:
        assert actual["raw_text"].count("    Timing replayed: true") == len(actual["rows"]), actual["raw_text"]
        assert actual["raw_text"].count("  Overall outcome:") == actual["raw_text"].count("\n  Timing replayed: true"), actual["raw_text"]
    if actual["trx_markers"] is not None:
        assert actual["trx_markers"] == ["Timing replayed: true"] * len(actual["rows"]), actual["trx_markers"]


# Independent fresh resolution/translation/solver results, with no module-result table.
fresh = run("fresh", destination / "fresh-cache", False)
assert fresh["output"][0] == 4 and b"1 error" in fresh["output"][1] and b"out of resource" in fresh["output"][1], fresh
assert fresh["solver"] > 0 and any(r["RandomSeed"] == "7" for r in fresh["rows"])
cold = run("cold")
same(fresh, cold)
assert cold["stats"]["manifestPublications"] == 3 and cold["solver"] == fresh["solver"], cold["stats"]
warm = run("warm")
same(fresh, warm)
hit(warm)
audited = run("audit", extra=("--persistent-translation-audit",))
same(fresh, audited)
assert audited["translated"] == modules and audited["stats"]["keyHits"] == 0, audited["stats"]
assert audited["stats"]["auditChecks"] >= 7 and audited["stats"]["auditMismatches"] == 0, audited["stats"]

if all_reports:
    # Each newly admitted format must work without JSON supplying its restoration guard.
    for fmt in ("text", "trx"):
        format_fresh = run(fmt + "-fresh", destination / (fmt + "-fresh-cache"), False, formats=(fmt,))
        format_warm = run(fmt + "-warm", formats=(fmt,))
        same(format_fresh, format_warm)
        hit(format_warm)
        bare = run("bare-" + fmt, formats=(fmt,), extra=("--log-format", fmt))
        hit(bare)
        assert bare["facts"] == format_fresh["facts"] and bare[fmt] == format_fresh[fmt]
        if fmt == "text":
            assert b"Timing replayed: true" in bare["output"][1]
        else:
            auto_reports = list((destination / "TestResults").glob("*.trx"))
            assert len(auto_reports) == 1, auto_reports
            assert trx_facts(auto_reports[0]) == format_fresh["trx"]
            assert all((n.text or "").strip() == "Timing replayed: true" for n in
                       ET.parse(auto_reports[0]).getroot().iter() if n.tag.endswith("}StdOut"))
        secondary = destination / ("second." + fmt)
        repeated = run("two-" + fmt, formats=(fmt,), extra=("--log-format", fmt + ";LogFileName=" + str(secondary)))
        hit(repeated)
        assert repeated["facts"] == format_fresh["facts"] and repeated[fmt] == format_fresh[fmt]
        assert (text_facts(secondary.read_text()) if fmt == "text" else trx_facts(secondary)) == format_fresh[fmt]
    assert "Timing replayed: true" not in fresh["raw_text"]
    assert fresh["trx_markers"] == []

# JSON replay also composes with checked resolution snapshots, rather than only fresh resolution.
resolution_cache = destination / "resolution-cache"
combined_cold = run("resolution-cold", extra=("--cache-dir", str(resolution_cache)))
same(fresh, combined_cold)
combined = run("resolution-warm", extra=("--cache-dir", str(resolution_cache), "--resolution-cache-check"))
same(fresh, combined)
hit(combined)
checked_hits = [e for e in combined["snapshots"] if e["state"].startswith("hit")]
assert checked_hits and all(int(re.search(r"checked (\d+) objects", e["state"]).group(1)) > 0 for e in checked_hits), checked_hits

# An unchanged snapshot relocates its resource diagnostic to the current declaration.
moved = destination / "moved/renamed.dfy"
moved.parent.mkdir()
moved.write_text("\n\n\n" + original.replace('"contract.dfy"', '"contract-renamed.dfy"'), encoding="utf-8")
(moved.parent / "contract-renamed.dfy").write_text("\n\n" + contract, encoding="utf-8")
relocated = run("relocated", input_file=moved)
relocated_fresh = run("relocated-fresh", destination / "relocated-fresh-cache", False, input_file=moved)
same(relocated_fresh, relocated)
assert relocated["stats"]["keyHits"] + relocated["stats"]["graphHits"] == 3, relocated["stats"]
assert b"renamed.dfy" in json.dumps(relocated["json"]).encode()

# Limits in a method attribute are effective limits, even when the command time limit is zero.
timed_cache = destination / "timed-cache"
program.write_text(original.replace("{:resource_limit 1}", "{:resource_limit 1} {:timeLimit 1}"), encoding="utf-8")
timed = run("timed-cold", timed_cache)
timed_warm = run("timed-warm", timed_cache)
same(timed, timed_warm)
assert timed_warm["stats"]["keyHits"] == 2 and timed_warm["translated"] == ["Mixed", "Witness"], timed_warm["stats"]
assert timed_warm["solver"] > 0, timed_warm["solver"]

# Raising the actual resource bound must not replay the previous exhausted outcome.
program.write_text(original.replace("{:resource_limit 1}", "{:resource_limit 1000000}"), encoding="utf-8")
raised = run("raised-limit")
raised_fresh = run("raised-fresh", destination / "raised-fresh-cache", False)
same(raised_fresh, raised)
assert raised["stats"]["keyHits"] == 2 and raised["solver"] > 0, raised["stats"]
assert b"out of resource" not in raised["output"][1]
program.write_text(original, encoding="utf-8")


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


# Self-consistent but unsupported records must miss, not restore a fabricated outcome.
for kind in ("old-schema", "unknown-outcome", "timed-resource", "missing-location", "bad-location",
             "missing-assertions", "wrong-assertion-count", "missing-description", "missing-position-column",
             "missing-filename-spelling", "bad-anchor", "bad-filename-kind"):
    broken = destination / (kind + "-cache")
    shutil.copytree(cache, broken)
    changed = 0
    for index, entry, record in records(broken):
        if record["Module"] != "Mixed":
            continue
        exhausted = next(t["Outcome"] for t in record["Tasks"] if "Limited" in t["ScopeId"])
        if kind == "old-schema":
            record["Schema"] = 2
        else:
            for task in record["Tasks"]:
                if task["Scheduled"]:
                    if kind == "unknown-outcome":
                        task["Outcome"] = -1
                    elif kind == "timed-resource":
                        task.update(Outcome=exhausted, TimeLimit=1, ResourceLimit=1)
                    elif kind == "missing-assertions":
                        task["ReportAssertions"] = None
                    elif kind == "wrong-assertion-count":
                        if task.get("ReportAssertions"):
                            task["ReportAssertions"].pop()
                    elif kind in ("missing-description", "missing-position-column", "missing-filename-spelling", "bad-anchor", "bad-filename-kind"):
                        for assertion in task.get("ReportAssertions") or []:
                            if kind == "missing-description":
                                assertion.pop("Description", None)
                            elif kind == "missing-position-column":
                                assertion["Position"].pop("Column", None)
                            elif kind == "missing-filename-spelling":
                                assertion["Position"].pop("Filename", None)
                            elif kind == "bad-anchor":
                                assertion["Position"]["Anchor"] = "no-current-declaration"
                            else:
                                assertion["Position"]["Filename"] = -1
                    elif "Limited" in task["ScopeId"]:
                        if kind == "missing-location":
                            task["Diagnostic"] = None
                        elif task.get("Diagnostic"):
                            task["Diagnostic"]["Column"] += 1
        replace(broken, index, entry, record)
        changed += 1
    assert changed > 0
    fallback = run(kind, broken)
    same(fresh, fallback)
    if all_reports and kind == "missing-assertions":
        text_missing = run("text-missing-metadata", broken, formats=("text",))
        text_fresh = run("text-missing-fresh", destination / "text-missing-fresh-cache", False, formats=("text",))
        same(text_fresh, text_missing)
        assert text_missing["stats"]["keyHits"] == 2, text_missing["stats"]
        trx_missing = run("trx-without-assertions", broken, formats=("trx",))
        trx_fresh = run("trx-without-assertions-fresh", destination / "trx-without-assertions-fresh-cache", False, formats=("trx",))
        same(trx_fresh, trx_missing)
        hit(trx_missing)
    assert fallback["stats"]["keyHits"] == 2 and fallback["translated"] == ["Mixed", "Witness"], fallback["stats"]

# The equality oracle must notice an exhausted outcome changed into a success.
tampered = destination / "tampered-cache"
shutil.copytree(cache, tampered)
changed = 0
for index, entry, record in records(tampered):
    if record["Module"] != "Mixed":
        continue
    valid = next(t["Outcome"] for t in record["Tasks"] if "Good" in t["ScopeId"])
    for task in record["Tasks"]:
        if "Limited" in task["ScopeId"]:
            task["Outcome"] = valid
            changed += 1
    replace(tampered, index, entry, record)
assert changed > 0
caught = run("tampered-audit", tampered, extra=("--persistent-translation-audit",))
assert caught["stats"]["auditMismatches"] > 0 and b"stored results of module Mixed differ" in caught["output"][1], caught["stats"]
# The audit also compares the new assertion report fields, not only outcomes/counts.
wrong_report = destination / "wrong-report-cache"
shutil.copytree(cache, wrong_report)
changed = 0
for index, entry, record in records(wrong_report):
    if record["Module"] == "Mixed":
        for task in record["Tasks"]:
            for assertion in task.get("ReportAssertions") or []:
                assertion["Description"] = "different assertion description"
                changed += 1
        replace(wrong_report, index, entry, record)
assert changed > 0
caught_report = run("wrong-report-audit", wrong_report, extra=("--persistent-translation-audit",))
assert caught_report["stats"]["auditMismatches"] > 0, caught_report["stats"]

# Nonzero seeds and the full assertion schema must survive a warm module hit.
vc_results = [v for scope in warm["raw_json"]["verificationResults"] for v in scope["vcResults"]]
assert all(v.get("randomSeed") == "7" for v in vc_results)
assert any(v.get("timingReplayed") for v in vc_results)
assert all(set(a) == {"filename", "line", "col", "description"} for v in vc_results for a in v["assertions"])
assert any(a["filename"] == "contract.dfy" for v in vc_results for a in v["assertions"])

# Refactoring-aware JSON retains every dependency field through the ordinary path.
coverage = run("coverage-cold", destination / "coverage-cache", extra=("--suggest-proof-refactoring", "--allow-warnings"))
coverage_warm = run("coverage-warm", destination / "coverage-cache", extra=("--suggest-proof-refactoring", "--allow-warnings"))
same(coverage, coverage_warm)
assert coverage_warm["stats"]["keyHits"] == 0 and coverage_warm["translated"] == modules, coverage_warm["stats"]
assert coverage_warm["stats"]["bypassReason"] == "proof-refactoring reporting", coverage_warm["stats"]
assert any("programElements" in scope for scope in coverage_warm["json"]["verificationResults"])
print("persistent text/TRX: complete reports, timing markers, hits, current locations, malformed records and dependency fallback: PASS" if all_reports else
      "persistent JSON: complete fresh/cold/warm/audit reports, hits, current locations, limits, malformed records and dependency fallback: PASS")
