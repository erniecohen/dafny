"""Whole-module IDE replay must be observable and preserve current reports."""
import json
from pathlib import Path
import runpy
import shutil
import subprocess

helper = runpy.run_path(str(Path(__file__).with_name("persistent-ide.py")))
run = helper["run"]
destination = helper["destination"]
extra = ("--persistent-ide-reports",)

def events(name):
    return [json.loads(line) for line in (destination / (name + ".jsonl")).read_text().splitlines()]

def hover(name):
    messages = [json.loads(line) for line in (destination / (name + ".protocol.jsonl")).read_text().splitlines()]
    return next(m["result"] for m in messages if m.get("id") == 2)

invalid = subprocess.run([shutil.which("dotnet"), str(helper["exe"]), "verify", str(helper["source"]),
    "--type-system-refresh", "--persistent-ide-reports", "--cores", "2"],
    capture_output=True, timeout=60)
assert invalid.returncode == 2 and b"--persistent-ide-reports requires --persistent-translation" in invalid.stdout

cold = run("cold", extra=extra)
warm = run("warm", extra=extra, module_reports=True)
audit = run("audit", extra=(*extra, "--persistent-translation-audit"))
assert cold[0] == warm[0] == audit[0]
assert cold[1] == audit[1], "audit changed per-VC verdicts or resource counts"
assert hover("cold") == hover("warm") == hover("audit"), "method hover changed"
assert any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("warm")), "no whole-module hit"
assert not any(e.get("ev") == "span" and e.get("stage") in ("translate", "prepare") or
               e.get("ev") == "result-task" for e in events("warm")), "warm did native work"
assert not any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("audit")), "audit reused a whole module"
# Remove translation records only. The graph cutoff must enforce the same IDE capability.
translations = list((helper["cache"] / "v1" / "artifacts" / "translations").glob("*/*"))
assert translations, "no translation records"
for record in translations:
    if record.is_file():
        record.unlink()
graph = run("graph", extra=extra, module_reports=True)
assert graph[0] == cold[0] and hover("graph") == hover("cold")
assert any(e.get("ev") == "persistent-module" and e.get("hit") == "boogie-graph" for e in events("graph"))
assert not any(e.get("ev") == "span" and e.get("stage") == "prepare" or e.get("ev") == "result-task" for e in events("graph"))
# Disable the capability: the IDE must retain real native tasks, even with this store populated.
native = run("native")
assert native[:2] == cold[:2]
assert not any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("native"))
# A modified overlay must miss; a current fresh preparation supplies its new proof.
changed = helper["text"].replace("x + 1", "x + 2")
stale = run("stale", overlay=changed, extra=extra)
fresh = run("changed-fresh", overlay=changed, directory=destination / "changed-cache", extra=extra)
assert stale[:2] == fresh[:2]
assert not any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("stale"))
# Corrupt whole-module records, leaving the independently validated task transport intact.
records = list((helper["cache"] / "v1" / "module-results").rglob("data"))
assert records, "no module reports were published"
for record in records:
    record.write_bytes(b"corrupt module report")
corrupt = run("corrupt", extra=extra)
assert corrupt[:2] == cold[:2]
assert not any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("corrupt"))
# With the IDE capability disabled, failed reports retain native evidence after restart.
failed_source = "module Cached { lemma Positive() { assert false; } }"
failed_cache = destination / "failed-cache"
failed_cold = run("failed-cold", overlay=failed_source, directory=failed_cache,
    extra=extra, expected_outcome="Invalid")
failed_warm = run("failed-warm", overlay=failed_source, directory=failed_cache,
    expected_outcome="Invalid")
assert failed_cold[:2] == failed_warm[:2]
assert all(e["replayed"] for e in failed_warm[2]), "failed native task transport did not hit"
assert not any(e.get("ev") == "persistent-module" and e.get("hit") for e in events("failed-warm"))
print("Whole-module IDE hits, native fallback, audit, hovers and stale/corrupt controls pass")
