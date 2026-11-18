#!/usr/bin/env python3
"""Capture actual Dafny first queries and replay with observational profiling.

Use the exact requested Dafny build in public CI. No assertion, trigger, guard,
fuel value, or resource limit is removed or rewritten in the baseline replay.
An additional replay gives each qid its existing skolemid suffix to attribute
same-source quantifiers; its result and summary statistics are compared with
the unchanged-qid replay. --mbqi-on is a separate configuration experiment.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess

SOURCE = """ghost function andNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (if a % 2 == 1 && b % 2 == 1 then 1 else 0) + 2*andNat(w-1,a/2,b/2) }
lemma {:induction false} control()

{ var z := andNat(8,5,3); assert z >= 0; assert false; }
"""
BOOGIE = "/normalizeDeclarationOrder:0 /restartProver /emitDebugInformation:1"
STAT_KEYS = ("quant-instantiations", "max-generation", "rlimit-count", "conflicts",
             "decisions", "final-checks", "arith-num-rows", "arith-row-summations")
QID = re.compile(r":qid \|([^|]*)\|(\s*:weight \d+)?\s*:skolemid \|(\d+)\|")


def sha(s):
    return hashlib.sha256(s.encode()).hexdigest()


def executable(s):
    found = shutil.which(s)
    if found is None:
        raise ValueError("executable not found: " + Path(s).name)
    return str(Path(found).resolve())


def replay(solver, prefix, destination, identify=False, mbqi=False):
    rewritten = prefix
    if identify:
        rewritten = QID.sub(lambda m: ":qid |" + m[1] + "#" + m[3] + "|" +
                            (m[2] or "") + "\n :skolemid |" + m[3] + "|", rewritten)
    if mbqi:
        rewritten += "\n(set-option :smt.mbqi true)\n"
    query = rewritten + "\n(set-option :smt.qi.profile true)\n(check-sat)\n" + \
            "(get-info :reason-unknown)\n(get-info :all-statistics)\n"
    query_path = Path(str(destination) + ".smt2")
    query_path.write_text(query)
    result = subprocess.run([solver, str(query_path)],
                            capture_output=True, text=True, timeout=45)
    Path(str(destination) + ".out").write_text(result.stdout)
    Path(str(destination) + ".err").write_text(result.stderr)
    assert result.returncode == 0 and "(error" not in result.stdout
    verdict = result.stdout.splitlines()[0]
    assert verdict in ("sat", "unsat", "unknown")
    reason = re.search(r"\(:reason-unknown (.*)\)", result.stdout)
    reason = json.loads(reason[1]) if verdict == "unknown" and reason else None
    stats = {k: int(v) for k, v in re.findall(r":([\w-]+)\s+(\d+)\b", result.stdout) if k in STAT_KEYS}
    profiles = [line for line in result.stderr.splitlines() if line.startswith("[quantifier_instances]")]
    return {"result": verdict, "reason_unknown": reason, "stats": stats,
            "quantifier_profiles": profiles, "query_sha256": sha(query)}


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dafny", required=True)
    ap.add_argument("--dotnet", help="dotnet executable when --dafny names a Dafny DLL")
    ap.add_argument("--z3", action="append", required=True)
    ap.add_argument("--controls", type=Path, help="also profile the actual control_A64")
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--mbqi-on", action="store_true")
    args = ap.parse_args()
    if args.dotnet:
        dafny_dll = Path(args.dafny).resolve()
        assert dafny_dll.is_file()
        dafny_command = [executable(args.dotnet), str(dafny_dll)]
    else:
        dafny_command = [executable(args.dafny)]
    solvers = [executable(s) for s in args.z3]
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    sources = [("transparent", SOURCE, "control"), ("opaque", "opaque " + SOURCE, "control")]
    if args.controls:
        sources.append(("actual-A64", args.controls.read_text(), "control_A64"))
    for name, source, _ in sources:
        (out / (name + ".dfy")).write_text(source)
    compiler_version = subprocess.check_output(dafny_command + ["--version"], text=True).strip()
    rows = []
    for solver in solvers:
        solver_version = re.search(r"\d+\.\d+\.\d+", subprocess.check_output([solver, "--version"], text=True))[0]
        for name, source, symbol in sources:
            tag = name + "-z3-" + solver_version
            log = tag + "-captured.smt2"
            # Relative source and log paths keep the retained artifacts portable.
            command = dafny_command + ["verify", name + ".dfy", "--solver-path", solver,
                       "--filter-symbol", symbol, "--cores", "1", "--additional-axioms",
                       "--allow-axioms", "--use-basename-for-filename", "--resource-limit", "200000",
                       "--verification-time-limit", "30", "--boogie", BOOGIE, "--solver-log", log]
            result = subprocess.run(command, cwd=out, capture_output=True, text=True, timeout=45)
            (out / (tag + "-dafny.txt")).write_text(result.stdout + result.stderr)
            assert result.returncode != 0 and (out / log).is_file(), (name, result.returncode)
            captured = (out / log).read_text()
            assert "(check-sat)" in captured
            prefix = captured.split("(check-sat)", 1)[0]
            baseline = replay(solver, prefix, out / (tag + "-profile"))
            identified = replay(solver, prefix, out / (tag + "-identified"), identify=True)
            same = all(baseline[k] == identified[k] for k in ("result", "reason_unknown", "stats"))
            mbqi_options = re.findall(r"\(set-option :smt.mbqi (true|false)\)", prefix)
            row = {"case": name, "source_sha256": sha(source), "solver": solver_version,
                   "dafny_exit_code": result.returncode, "first_query_only": True,
                   "captured_prefix_sha256": sha(prefix), "contains_bitvector_sort": "BitVec" in prefix,
                   "mbqi_in_captured_query": mbqi_options[-1] if mbqi_options else "unspecified",
                   "unchanged_qids": baseline, "identified_qids": identified,
                   "qid_renaming_preserves_result_and_summary_stats": same}
            if args.mbqi_on:
                row["separate_mbqi_enabled_experiment"] = replay(solver, prefix, out / (tag + "-mbqi"), mbqi=True)
            rows.append(row)
            print("Q1_PROFILE_RESULT " + json.dumps({"case": name, "solver": solver_version,
                  "mbqi_in_captured_query": row["mbqi_in_captured_query"],
                  "contains_bitvector_sort": row["contains_bitvector_sort"],
                  **{k: baseline[k] for k in ("result", "reason_unknown", "stats")}}), flush=True)
    report = {"compiler_version": compiler_version, "boogie_options": BOOGIE,
              "resource_limit": 200000, "timeout_seconds": 30, "additional_axioms": True,
              "note": "Dafny Errors/Invalid does not imply solver SAT; inspect actual solver result and reason.",
              "results": rows}
    (out / "profile-results.json").write_text(json.dumps(report, indent=2) + "\n")
    print("Q1_PROFILE_SUMMARY " + json.dumps({"captures": len(rows),
          "all_qid_renamings_observational": all(r["qid_renaming_preserves_result_and_summary_stats"] for r in rows)}))


if __name__ == "__main__":
    main()
