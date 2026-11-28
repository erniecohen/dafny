"""Resolution equality and whole-module IDE failure replay must work together."""
import json
from pathlib import Path
import runpy

helper = runpy.run_path(str(Path(__file__).with_name("persistent-ide.py")))
destination, cache = helper["destination"], helper["cache"]
text = helper["text"]
offset = text.rindex("assert")
position = (text[:offset].count("\n"), offset - text.rfind("\n", 0, offset) - 1)


def run(name, **kwargs):
    return helper["run"](name, extra=("--persistent-ide-reports", *kwargs.pop("extra", ())),
        expected_outcome="Invalid", failure_positions=(position,), **kwargs)


def events(name):
    return [json.loads(line) for line in (destination / (name + ".jsonl")).read_text().splitlines()]


def reports(name):
    protocol = [json.loads(line) for line in (destination / (name + ".protocol.jsonl")).read_text().splitlines()]
    replies = [m["result"] for m in protocol if isinstance(m.get("id"), int) and m["id"] >= 20 and "result" in m]
    assert replies and replies[0], "no counterexample model states"
    assert "Insert a forall statement" in json.dumps(replies), "no quantified code action"

    def clean(value):
        if isinstance(value, dict):
            return {k: clean(v) for k, v in value.items() if k != "data"}
        if isinstance(value, list):
            return [clean(v) for v in value]
        return value
    return clean(replies)


fresh = run("fresh", directory=destination / "fresh-store")
cold = run("cold")
warm = run("warm", module_reports=True)
audit = run("audit", extra=("--persistent-translation-audit",))
assert fresh[0] == cold[0] == warm[0] == audit[0]
assert fresh[1] == cold[1] == audit[1], "per-VC verdicts or resources changed"
assert reports("fresh") == reports("cold") == reports("warm") == reports("audit")
for name in ("warm", "audit"):
    snapshots = [e for e in events(name) if e.get("stage") == "resolve.snapshot"]
    assert snapshots and all(e.get("state", "").startswith("hit (checked ") for e in snapshots), snapshots
assert any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("warm")), "no module hit"
assert not any(e.get("ev") == "result-task" or
    e.get("ev") == "span" and e.get("stage") in ("translate", "prepare") for e in events("warm")), "native warm work"
# A damaged resolution entry must resolve again, even when later report records remain intact.
records = list(cache.glob("v1/artifacts/resolutions/*/*"))
assert records
for record in records:
    record.write_bytes(b"corrupt resolution")
corrupt = run("corrupt", module_reports=True)
snapshots = [e for e in events("corrupt") if e.get("stage") == "resolve.snapshot"]
assert snapshots and all(not e.get("state", "").startswith("hit") for e in snapshots)
assert corrupt[0] == cold[0] and reports("corrupt") == reports("cold")
# A changed quantified body must use current evidence, not a previous failure's report.
changed = text.replace("s[i] == 0", "s[i] == 1")
assert changed != text
stale = run("stale", overlay=changed)
oracle = run("changed-oracle", overlay=changed, directory=destination / "changed-store")
assert stale[:2] == oracle[:2] and reports("stale") == reports("changed-oracle")
assert stale[2] and all(not task["replayed"] for task in stale[2]), "stale result hit"
print("Checked resolution and whole-module quantified IDE replay preserve current evidence")
