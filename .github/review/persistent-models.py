"""Native model payload, source rebinding and equality-audit controls (issue 261)."""
import copy
from pathlib import Path
import runpy
import shutil
import sys

# Share the complete non-timing report comparison and cache-object helpers.
helpers = runpy.run_path(str(Path(__file__).with_name("persistent-counterexamples.py")))
globals().update({k: helpers[k] for k in ("run", "same", "records", "replace", "destination", "program", "cache", "original", "contract")})
extra = ("--extract-counterexample",)


def hit(actual):
    assert actual["stats"]["keyHits"] == 3 and actual["translated"] == actual["prepared"] == [], actual["stats"]
    assert actual["solver"] == 0 and all(r["TimingReplayed"] == "true" for r in actual["rows"]), actual["stats"]


fresh = run("fresh", destination / "fresh-cache", False, extra=extra)
uncached = run("uncached", None, False, extra=extra)
same(fresh, uncached)
assert b"Related counterexample:" in fresh["output"][1] and fresh["solver"] > 0
cold = run("cold", extra=extra)
same(fresh, cold)
assert cold["stats"]["manifestPublications"] == 3, cold["stats"]
for name in ("warm", "warm-again", "audit"):
    actual = run(name, extra=extra + (("--persistent-translation-audit",) if name == "audit" else ()))
    same(fresh, actual)
    if name != "audit": hit(actual)
    else: assert actual["stats"]["auditChecks"] > 0 and actual["stats"]["auditMismatches"] == 0
if len(sys.argv) == 6 and sys.argv[5] == "--smoke":
    print("persistent models smoke: PASS")
    sys.exit(0)

# Verify actual structured model state was published, including native values and variable bindings.
base = next((i, e, r) for i, e, r in records(cache) if any(t.get("Failure") for t in r["Tasks"]))
models = [m["Model"] for t in base[2]["Tasks"] if t.get("Failure") for m in t["Failure"]["Models"]]
assert models and any(m["States"][0]["Variables"] for m in models)
assert any(len(m["Elements"]) > 2 and any(f["Applications"] for f in m["Functions"]) for m in models)

moved = destination / "renamed.dfy"
(destination / "renamed-contract.dfy").write_text("\n\n" + contract, encoding="utf-8")
moved.write_text("\n\n\n" + original.replace('include "contract.dfy"', 'include "renamed-contract.dfy"'), encoding="utf-8")
current = run("relocated-fresh", destination / "relocated-fresh-cache", False, extra=extra, input_file=moved)
actual = run("relocated-warm", extra=extra, input_file=moved)
same(current, actual)
hit(actual)
assert b"renamed.dfy" in actual["output"][1]
assert b"input.dfy" not in actual["output"][1]

# Rendering variants must preserve full diagnostics as well as per-VC resource counts.
for name, flags in (("snippets", ("--show-snippets", "--show-proof-obligation-expressions")),
                    ("json", ("--json-diagnostics",)),
                    ("paths", ("--use-basename-for-filename:false",)),
                    ("multiple", ("--isolate-assertions:false", "--error-limit", "3"))):
    directory = destination / (name + "-cache")
    oracle = run(name + "-fresh", destination / (name + "-fresh-cache"), False, extra=extra + flags)
    for mode in ("cold", "warm", "audit"):
        actual = run(name + "-" + mode, directory, extra=extra + flags + (("--persistent-translation-audit",) if mode == "audit" else ()))
        same(oracle, actual)
        if mode == "warm": hit(actual)
        if mode == "audit": assert actual["stats"]["auditMismatches"] == 0
    if name == "multiple":
        packets = list(records(directory))
        assert any(len(m["Model"]["States"]) > 1 and any(s["Variables"] for s in m["Model"]["States"][1:])
                   for _, _, record in packets for task in record["Tasks"] if task.get("Failure")
                   for m in task["Failure"]["Models"])
        current = run("states-relocated-fresh", destination / "states-relocated-fresh-cache", False,
                      extra=extra + flags, input_file=moved)
        actual = run("states-relocated-warm", directory, extra=extra + flags, input_file=moved)
        same(current, actual)
        hit(actual)
        bad = destination / "bad-state-excerpt"
        shutil.copytree(directory, bad)
        index, entry, record = next((i, copy.deepcopy(e), copy.deepcopy(r)) for i, e, r in packets
                                   if any(len(m["Model"]["States"]) > 1 for t in r["Tasks"] if t.get("Failure")
                                          for m in t["Failure"]["Models"]))
        state = next(s for t in record["Tasks"] if t.get("Failure") for m in t["Failure"]["Models"]
                     for s in m["Model"]["States"][1:])
        state["Location"]["SourceOriginal"] = "changed"
        replace(bad, bad / index.relative_to(directory), entry, record)
        actual = run("stale-state-excerpt", bad, extra=extra + flags)
        same(oracle, actual)
        assert "Witness" in actual["prepared"], actual["stats"]

# Complete packets are mandatory. Coherently addressed malformed/stale objects must miss.
for name in ("missing-models", "missing-elements", "wrong-index", "wrong-kind", "missing-location", "stale-location", "missing-suffix", "wrong-model-count"):
    directory = destination / ("bad-" + name)
    shutil.copytree(cache, directory)
    index, entry, record = base[0], copy.deepcopy(base[1]), copy.deepcopy(base[2])
    failure = next(t["Failure"] for t in record["Tasks"] if t.get("Failure"))
    model = failure["Models"][0]["Model"]
    if name == "missing-models": failure.pop("Models")
    elif name == "missing-elements": model.pop("Elements")
    elif name == "wrong-index": model["States"][0]["Variables"][0]["Element"] = 999999
    elif name == "wrong-kind": model["Elements"][0]["Kind"] = 999
    elif name == "missing-location": model["States"][0].pop("Location")
    elif name == "stale-location": model["States"][0]["Location"] = dict(failure["Diagnostics"][0]["Range"], SourceOriginal="changed")
    elif name == "missing-suffix": model["States"][0].pop("Suffix")
    elif name == "wrong-model-count": failure["Models"] = []
    replace(directory, directory / index.relative_to(cache), entry, record)
    actual = run(name, directory, extra=extra)
    same(fresh, actual)
    assert "Witness" in actual["prepared"], actual["stats"]

# Valid but changed model values, native initialization and render decisions reach the independent oracle.
for name in ("value", "has-states", "render"):
    directory = destination / ("audit-" + name)
    shutil.copytree(cache, directory)
    index, entry, record = base[0], copy.deepcopy(base[1]), copy.deepcopy(base[2])
    failure = next(t["Failure"] for t in record["Tasks"] if t.get("Failure") and t["Failure"]["Models"])
    if name == "value":
        element = next(e for e in failure["Models"][0]["Model"]["Elements"] if e["Kind"] == 0 and e["Value"].lstrip("-").isdigit())
        element["Value"] = "987654321"
    elif name == "has-states": failure["Models"][0]["Model"]["HasStates"] = not failure["Models"][0]["Model"]["HasStates"]
    else: failure["Models"][0]["Render"] = not failure["Models"][0]["Render"]
    replace(directory, directory / index.relative_to(cache), entry, record)
    actual = run("audit-" + name, directory, extra=extra + ("--persistent-translation-audit",))
    assert actual["stats"]["auditMismatches"] > 0, actual["stats"]
    code, stdout, stderr = actual["output"]
    actual["output"] = (code, b"\n".join(line for line in stdout.split(b"\n") if b"persistent translation audit:" not in line), stderr)
    same(fresh, actual)

print("persistent models: PASS")
