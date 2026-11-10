#!/usr/bin/env python3
"""Census of the calls that need the smaller-bound rule of --modular-termination
(https://github.com/erniecohen/dafny/issues/211, milestones M1 and M2).

Resolves every program of a corpus with `--modular-termination --show-hints` and counts
the classifier's hints ("modular termination: in 'C', ..."): calls dispatched into
implementations that other modules may provide (smaller-bound rule, M1), calls to known code
that can reach such a dispatch (same-bound rule, M2), and calls through function values.  The
counts are static, one per call site; they need no solver.

The same-bound counts include a projection: if every dispatched trait member gets the bound that
M1 requires, its overrides inherit it, and then each unbounded helper that such a member, or a
helper that itself needs a bound, calls under the same-bound rule needs a bound of its own.  The
projection iterates that to a fixed point, per program.

  modular-termination-census.py DAFNY lit DIR OUT       every .dfy file under DIR
  modular-termination-census.py DAFNY project TOML OUT  one project file (the standard library)

DAFNY is the path of Dafny.dll or of the dafny executable.  A lit program is resolved
first with the default options; one that does not resolve is tried again with the refreshed
type system (--type-system-refresh --general-traits=datatype --general-newtypes), and one that
resolves in neither is counted as unresolved.  OUT gets census.json, with the totals and each
hint, and census.md, a summary.  Exits 0 unless the arguments are wrong.
"""
import collections
import concurrent.futures
import json
import os
import re
import subprocess
import sys

TIMEOUT = 300
# Dafny names may contain primes, so quoted names end at the text that follows them.
HINT = re.compile(r"^(?P<file>[^()\n]+)\((?P<line>\d+),(?P<col>\d+)\): Info: modular termination: in '(?P<caller>.*?)', (?P<rest>.*)$")
DISPATCH = re.compile(r"^dispatch of '(?P<callee>.*?)' to implementations .*needs the smaller-bound rule; (?P<status>.*)$")
SAME = re.compile(r"^a call of '(?P<callee>.*?)', which can reach dispatch .*\(same-bound rule\); the callee has (?P<cb>a|no) 'decreases \{:global\}' bound, (?P<caller>.*)$")
CALLER_STATUS = [
    ("checked:", "bounded"),
    ("so the call is rejected", "bounded"),
    ("and the caller has none until", "inherits"),
    ("and the caller has none,", "none"),
]
CALLBACK_KINDS = {
    "a call through a function value": "function value",
    "a sequence constructor's calls of its initializing function": "seq constructor",
    "an array allocation's calls of its initializing function": "array initializer",
}
REFRESH = ["--type-system-refresh", "--general-traits=datatype", "--general-newtypes"]


def command(dafny):
    return ["dotnet", dafny] if dafny.endswith(".dll") else [dafny]


def resolve(dafny, target, extra, cwd=None):
    cmd = command(dafny) + ["resolve", "--modular-termination", "--show-hints", "--show-snippets:false",
                            "--allow-warnings", "--use-basename-for-filename"] + extra + [target]
    try:
        p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=TIMEOUT)
        return p.returncode, p.stdout + p.stderr
    except subprocess.TimeoutExpired:
        return "timeout", ""


def hints(output, only_file=None):
    found = []
    for line in output.splitlines():
        m = HINT.match(line.strip())
        if not m or (only_file is not None and m.group("file") != only_file):
            continue
        hint = {"file": m.group("file"), "line": int(m.group("line")), "col": int(m.group("col")), "caller": m.group("caller")}
        d = DISPATCH.match(m.group("rest"))
        sb = SAME.match(m.group("rest"))
        if sb:
            status = next((v for k, v in CALLER_STATUS if sb.group("caller").startswith(k)), "none")
            hint.update(kind="same-bound", callee=sb.group("callee"), callee_bound=sb.group("cb") == "a",
                        caller_status=status)
        elif d:
            status = d.group("status")
            hint.update(kind="dispatch", callee=d.group("callee"),
                        callee_bound=not status.startswith("the callee has no"))
        else:
            what = m.group("rest").split(" needs the smaller-bound rule")[0]
            hint.update(kind="callback", callback=CALLBACK_KINDS.get(what, what))
        found.append(hint)
    return found


def lit_one(dafny, root, path):
    rel = os.path.relpath(path, root)
    code, out = resolve(dafny, path, ["--standard-libraries:false"])
    mode = "default"
    if code not in (0, 1, 4):
        code, out = resolve(dafny, path, ["--standard-libraries:false"] + REFRESH)
        mode = "refresh"
    if code not in (0, 1, 4):
        return {"program": rel, "mode": "unresolved", "exit": code, "hints": []}
    found = hints(out, os.path.basename(path))
    for h in found:
        h["file"] = rel
    return {"program": rel, "mode": mode, "exit": code, "hints": found}


def summarize(programs):
    all_hints = [h for p in programs for h in p["hints"]]
    dispatch = [h for h in all_hints if h["kind"] == "dispatch"]
    callback = [h for h in all_hints if h["kind"] == "callback"]
    modes = collections.Counter(p["mode"] for p in programs)
    key = lambda h, f: (h["file"], h[f])
    site = lambda h: (h["file"], h["line"], h["col"])
    same = [h for h in all_hints if h["kind"] == "same-bound"]
    rejected = [h for h in same if h["caller_status"] == "bounded" and not h["callee_bound"]]
    need = projected_needs(same)
    return {
        "same_sites": len({site(h) for h in same}),
        "same_programs": len({h["file"] for h in same}),
        "same_callees": len({key(h, "callee") for h in same}),
        "same_rejected_sites": len({site(h) for h in rejected}),
        "same_rejected_callees": len({key(h, "callee") for h in rejected}),
        "same_rejected_programs": len({h["file"] for h in rejected}),
        "projected_helpers": len(need),
        "projected_programs": len({f for f, _ in need}),
        "programs": len(programs),
        "resolved_default": modes["default"],
        "resolved_refresh": modes["refresh"],
        "unresolved": modes["unresolved"],
        "dispatch_sites": len({site(h) for h in dispatch}),
        "dispatch_hints": len(dispatch),
        "dispatch_programs": len({h["file"] for h in dispatch}),
        "dispatch_callees": len({key(h, "callee") for h in dispatch}),
        "dispatch_callers": len({key(h, "caller") for h in dispatch}),
        "dispatch_sites_with_callee_bound": len({site(h) for h in dispatch if h["callee_bound"]}),
        "callback_sites": len({site(h) for h in callback}),
        "callback_hints": len(callback),
        "callback_sites_by_kind": dict(collections.Counter(h["callback"] for h in {site(h): h for h in callback}.values())),
        "callback_programs": len({h["file"] for h in callback}),
        "callback_callers": len({key(h, "caller") for h in callback}),
    }


def projected_needs(same):
    """The helpers that need a bound once every dispatched trait member has one (see the module doc)."""
    bounded = {(h["file"], h["caller"]) for h in same if h["caller_status"] in ("bounded", "inherits")}
    bounded |= {(h["file"], h["callee"]) for h in same if h["callee_bound"]}
    need = set()
    changed = True
    while changed:
        changed = False
        for h in same:
            callee = (h["file"], h["callee"])
            if (h["file"], h["caller"]) in bounded | need and not h["callee_bound"] and callee not in need:
                need.add(callee)
                changed = True
    return need


def markdown(title, totals, programs):
    rows = [f"### {title}", "", "| measure | count |", "|---|---|"]
    labels = [
        ("programs", "programs"),
        ("resolved_default", "resolved with the default options"),
        ("resolved_refresh", "resolved only with the refreshed type system"),
        ("unresolved", "not resolved (not counted)"),
        ("dispatch_sites", "calls dispatched to implementations that other modules may provide (source positions)"),
        ("dispatch_hints", "the same, counting each copy that module refinement makes"),
        ("dispatch_programs", "files with such a call"),
        ("dispatch_callees", "distinct trait members so called, by qualified name: each needs `decreases {:global}`"),
        ("dispatch_callers", "distinct callers of them"),
        ("dispatch_sites_with_callee_bound", "of the call sites, those whose callee has a global bound"),
        ("same_sites", "calls to known code that can reach such a dispatch (same-bound rule, M2; source positions)"),
        ("same_programs", "files with such a call"),
        ("same_callees", "distinct callees so called"),
        ("same_rejected_sites", "of those calls, ones rejected now: the caller has a bound and the callee none"),
        ("same_rejected_callees", "distinct callees in them, each needing a bound"),
        ("same_rejected_programs", "files with such a rejected call"),
        ("projected_helpers", "projected: helpers needing a bound once every dispatched trait member has one"),
        ("projected_programs", "projected: files with such a helper"),
        ("callback_sites", "calls through function values (source positions; not checked)"),
        ("callback_hints", "the same, counting each copy that module refinement makes"),
        ("callback_programs", "files with such a call"),
        ("callback_callers", "distinct callers with such a call"),
    ]
    rows += [f"| {label} | {totals[k]} |" for k, label in labels]
    for kind, n in sorted(totals["callback_sites_by_kind"].items()):
        rows.append(f"| function-value calls: {kind} | {n} |")
    per = collections.Counter()
    for p in programs:
        for h in {(h["file"], h["line"], h["col"]): h for h in p["hints"] if h["kind"] == "dispatch"}.values():
            per[h["file"]] += 1
    top = [(prog, n) for prog, n in per.most_common(15) if n]
    if top:
        rows += ["", "Files with the most dispatched calls:", "", "| file | calls |", "|---|---|"]
        rows += [f"| `{prog}` | {n} |" for prog, n in top]
    return "\n".join(rows) + "\n"


def main():
    if len(sys.argv) != 5 or sys.argv[2] not in ("lit", "project"):
        print(__doc__)
        return 2
    dafny, kind, target, out = sys.argv[1:]
    if os.path.exists(dafny):
        dafny = os.path.abspath(dafny)
    os.makedirs(out, exist_ok=True)
    if kind == "lit":
        files = sorted(os.path.join(d, f) for d, _, fs in os.walk(target) for f in fs if f.endswith(".dfy"))
        with concurrent.futures.ThreadPoolExecutor(max_workers=os.cpu_count() or 2) as pool:
            programs = list(pool.map(lambda f: lit_one(dafny, target, f), files))
        title = f"LitTests ({os.path.relpath(target)})"
    else:
        code, output = resolve(dafny, os.path.basename(target), [], cwd=os.path.dirname(target) or None)
        programs = [{"program": target, "mode": "default" if code in (0, 1, 4) and "resolution/type errors" not in output else "unresolved",
                     "exit": code, "hints": hints(output)}]
        title = f"Project {target}"
    totals = summarize(programs)
    with open(os.path.join(out, "census.json"), "w") as f:
        json.dump({"totals": totals, "programs": programs}, f, indent=1)
    md = markdown(title, totals, programs)
    with open(os.path.join(out, "census.md"), "w") as f:
        f.write(md)
    print(md)
    return 0


if __name__ == "__main__":
    sys.exit(main())
