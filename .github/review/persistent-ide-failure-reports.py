"""Failed whole-module IDE results retain models, diagnostics, hovers and code-action edits."""
import hashlib
import json
import zlib
from pathlib import Path
import runpy

helper = runpy.run_path(str(Path(__file__).with_name("persistent-ide.py")))
run = helper["run"]
destination = helper["destination"]
extra = ("--persistent-ide-reports",)
offset = helper["text"].rindex("assert")
positions = ((helper["text"][:offset].count("\n"), offset - helper["text"].rfind("\n", 0, offset) - 1),)

def invoke(name, **kwargs):
    return run(name, extra=extra, expected_outcome="Invalid", failure_positions=positions, **kwargs)

def messages(name):
    return [json.loads(line) for line in (destination / (name + ".protocol.jsonl")).read_text().splitlines()]

def reports(name):
    protocol = messages(name)
    replies = [m["result"] for m in protocol if isinstance(m.get("id"), int) and m["id"] >= 20 and "result" in m]
    assert replies and replies[0], "counterexample request returned no model states"
    assert any("Insert a calc statement" in json.dumps(reply) for reply in replies), "no code action"
    def clean(value):
        if isinstance(value, dict):
            return {k: clean(v) for k, v in value.items() if k not in ("data",)}
        if isinstance(value, list):
            return [clean(v) for v in value]
        return value
    return clean(replies)

def events(name):
    return [json.loads(line) for line in (destination / (name + ".jsonl")).read_text().splitlines()]

cold = invoke("cold")
warm = invoke("warm", module_reports=True)
audit = run("audit", extra=(*extra, "--persistent-translation-audit"),
    expected_outcome="Invalid", failure_positions=positions)
assert cold[0] == warm[0] == audit[0]
assert cold[1] == audit[1], "fresh audit changed per-VC verdicts or resources"
assert reports("cold") == reports("warm") == reports("audit"), "IDE failure reports changed"
for name in ("warm",):
    assert any(e.get("ev") == "persistent-module" and e.get("hit") for e in events(name)), "no module hit"
    assert not any(e.get("ev") == "span" and e.get("stage") in ("translate", "prepare") or
        e.get("ev") == "result-task" for e in events(name)), "warm did native work"
records = list((helper["cache"] / "v1" / "artifacts" / "translations").glob("*/*"))
assert records
for record in records:
    if record.is_file(): record.unlink()
graph = invoke("graph", module_reports=True)
assert graph[0] == cold[0] and reports("graph") == reports("cold")
assert any(e.get("ev") == "persistent-module" and e.get("hit") == "boogie-graph" for e in events("graph"))
assert not any(e.get("ev") == "span" and e.get("stage") == "prepare" or e.get("ev") == "result-task" for e in events("graph"))
# A valid storage envelope with incomplete IDE models must also miss.
changed_records = 0
for index in (helper["cache"] / "v1/module-results").glob("*/*/data"):
    entry = json.loads(index.read_text())
    blob = helper["cache"] / "v1/blobs/sha256" / entry["Blob"][:2] / entry["Blob"] / "data"
    payload = blob.read_bytes()
    record = json.loads(zlib.decompress(payload[10:]) if payload.startswith(b"\0dcz") else payload)
    for task in record["Tasks"]:
        report = task.get("IdeReport")
        if report and report.get("Failure"):
            report["Failure"]["Models"] = None
            changed_records += 1
    payload = json.dumps(record).encode()
    digest = hashlib.sha256(payload).hexdigest()
    blob = helper["cache"] / "v1/blobs/sha256" / digest[:2] / digest / "data"
    blob.parent.mkdir(parents=True, exist_ok=True)
    blob.write_bytes(payload)
    entry.update(Blob=digest, Length=len(payload))
    index.write_text(json.dumps(entry))
assert changed_records
incomplete = invoke("incomplete")
assert incomplete[:2] == cold[:2] and reports("incomplete") == reports("cold")
assert not any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("incomplete"))
# Corrupted records must miss instead of producing a partial failure report.
records = list((helper["cache"] / "v1" / "module-results").rglob("data"))
assert records
for record in records: record.write_bytes(b"corrupt report")
corrupt = invoke("corrupt")
assert corrupt[:2] == cold[:2] and reports("corrupt") == reports("cold")
assert not any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("corrupt"))
changed = helper["text"].replace("x == 0", "x == 1")
assert changed != helper["text"]
stale = invoke("stale", overlay=changed)
oracle = invoke("changed-oracle", overlay=changed, directory=destination / "changed-oracle-store")
assert stale[:2] == oracle[:2] and reports("stale") == reports("changed-oracle")
assert not any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("stale"))
assert stale[2] and all(not e["replayed"] for e in stale[2]), "stale evidence reused a result"
print("Failed IDE whole-module key/graph hits preserve models, diagnostics, hovers and code actions")
