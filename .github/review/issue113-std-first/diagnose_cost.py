#!/usr/bin/env python3
"""Narrow Std query/session diagnostic. Only 'run' invokes a compiler.

Install beside the immutable Std-first helpers. This is a labeled diagnostic,
not an expected-table update or a replacement for the canonical cores-4 gate.
"""
import argparse
import json
import os
from pathlib import Path
import platform
import shutil
from plan import HERE, digest
from prepare import components, inputs
from run import append, bpl_receipt, command, compare, declarations, dump, execute, verdict
from solver_sessions import inspect_file

CASES = (("5.1.0", "TargetSpecific-cs"), ("5.1.0", "TargetSpecific-notarget"),
         ("4.16.0", "TargetSpecific-notarget"))
TAGS = ("z3-5-1-cs", "z3-5-1-notarget", "z3-4-16-notarget")


def samples(case):
    # Three paired canonical samples. Odd sample counts cannot balance within
    # each job exactly, so rotate starts between cases; controls balance 1:1.
    offset = CASES.index(case) % 2
    return [("canonical-cores4", n, 4, ("baseline", "final") if (n + offset) % 2 == 0 else ("final", "baseline"))
            for n in range(3)] + [("additional-cores1", n, 1, ("baseline", "final") if n == 0 else ("final", "baseline"))
                                 for n in range(2)]


def observed_command(row, executable, solver, scratch, cores):
    cmd = command("dev", "std", "project", row, executable, solver, scratch, "absent")
    # Preserve every original option; the cores-1 axis explicitly changes only
    # the existing cores value. Solver logging adds observation only.
    cmd[cmd.index("--cores") + 1] = str(cores)
    return cmd + ["--solver-log", str(scratch / "solver.smt2")]


def run(args):
    case = (args.solver_axis, args.part)
    if case not in CASES:
        raise ValueError("Diagnostic must select one of the three actually affected jobs")
    if args.output.exists():
        raise ValueError("Observation directory must be fresh")
    args.inputs = args.inputs.resolve()
    args.output = args.output.resolve()
    receipt = inputs("dev", args.inputs)
    plan = json.loads((HERE / "canonical/dev/plan.json").read_text())["std"]
    rows = [row for row in plan if row["id"] == args.part]
    if len(rows) != 1:
        raise ValueError("Selector must name one original canonical Std part")
    row = rows[0]
    spec = json.loads((HERE / "spec.json").read_text())["lines"]["dev"]
    identities, versions = {}, []
    for side in ("baseline", "final"):
        record = json.loads(getattr(args, side + "_identity").read_text())
        expected = spec["repaired_product" if side == "baseline" else "final_product"]
        if (record["line"], record["side"], record["product_revision"]) != ("dev", side, expected):
            raise ValueError("Compiler receipt does not match the frozen source pins")
        if components(getattr(args, side)) != record["components_sha256"]:
            raise ValueError("Actual compiler components differ from their receipt")
        identities[side] = record
        versions.extend(getattr(args, side + "_version").read_text().strip().splitlines())
    solver_version = args.solver_version.read_text().strip()
    if not solver_version.startswith("Z3 version " + args.solver_axis + " "):
        raise ValueError("Actual solver version does not match the selected observed axis")
    args.output.mkdir(parents=True)
    dump(args.output / "request.json", {"case": case, "samples": samples(case),
         "inputs": receipt, "plan": row, "compilers": identities,
         "solver_sha256": digest(args.solver), "solver_version": solver_version,
         "host": {"hostname": platform.node(), "platform": platform.platform(), "cpus": os.cpu_count()},
         "process_cap_seconds": 5400,
         "boundary": "Canonical cores4 repeated samples plus separately labeled cores1 controls; no changed seeds/budgets/order, no feature flag or AdditionalAxioms; SMT log observer appended"})
    records = []
    for axis, sample, cores, order in samples(case):
        directory = args.output / axis / str(sample)
        scratch = args.output / "scratch"
        arms = {}
        for side in order:
            if scratch.exists():
                shutil.rmtree(scratch)
            scratch.mkdir()
            destination = directory / side
            destination.mkdir(parents=True)
            cmd = observed_command(row, getattr(args, side).resolve(), args.solver.resolve(), scratch, cores)
            dump(destination / "command.json", {"argv": cmd, "cwd": str(args.inputs / row["cwd"]),
                "axis": axis, "sample": sample, "side": side, "order": order})
            append(args.output / "events.jsonl", {"event": "started", "axis": axis, "sample": sample, "side": side})
            try:
                execution = execute(cmd, args.inputs / row["cwd"], 5400)
            except OSError as error:
                execution = {"exit": 125, "timed_out": False, "seconds": 0, "stdout": "", "stderr": str(error)}
            (destination / "stdout.txt").write_text(execution["stdout"])
            (destination / "stderr.txt").write_text(execution["stderr"])
            for file in scratch.iterdir():
                if file.is_file():
                    shutil.copyfile(file, destination / file.name)
            session_manifest = [inspect_file(file) for file in sorted(destination.glob("solver.smt2*")) if file.is_file()]
            dump(destination / "solver-session-analysis.json", {"normalization": "none", "sessions": session_manifest})
            solver_logs = [{key: record.get(key) for key in ("file", "bytes", "raw_sha256", "parsed", "reason", "command_count", "vc_markers")}
                           for record in session_manifest]
            query_observations_complete = (bool(session_manifest) and all(record["parsed"] for record in session_manifest) and
                any(command["head"] in ("check-sat", "check-sat-assuming") for record in session_manifest
                    for command in record.get("command_manifest", [])))
            result = {k: v for k, v in execution.items() if k not in ("stdout", "stderr")}
            result.update({"verdict": verdict(row, execution, "std"),
                "warning_lines": [line for line in (execution["stdout"] + execution["stderr"]).splitlines() if "warning" in line.lower()],
                "declarations": declarations(destination / "results.json"),
                "bpl": bpl_receipt(destination, [(str(args.inputs), "<original-inputs>"), (str(args.output), "<observations>")], versions),
                "solver_logs": solver_logs,
                "query_observations_complete": query_observations_complete,
                "query_boundary": "Raw complete checker sessions retained; no normalization, reordering, replay or guessed per-query equality"})
            dump(destination / "result.json", result)
            append(args.output / "events.jsonl", {"event": "completed", "axis": axis, "sample": sample, "side": side, "exit": result["exit"], "timed_out": result["timed_out"]})
            arms[side] = result
        comparison = compare(arms["baseline"], arms["final"])
        comparison["query_observations_missing"] = any(not arm["query_observations_complete"] for arm in arms.values())
        comparison["incomplete"] |= comparison["query_observations_missing"]
        comparison["equal_observations"] &= not comparison["query_observations_missing"]
        sessions = {side: {record["file"]: record["raw_sha256"] for record in arm["solver_logs"]}
                    for side, arm in arms.items()}
        comparison["raw_session_files"] = sessions
        comparison["raw_session_files_equal"] = sessions["baseline"] == sessions["final"]
        comparison["equal_observations"] &= comparison["raw_session_files_equal"]
        records.append({"axis": axis, "sample": sample, "order": order, **comparison})
        dump(directory / "comparison.json", records[-1])
    inputs("dev", args.inputs)
    for side in identities:
        if components(getattr(args, side)) != identities[side]["components_sha256"]:
            raise ValueError("Compiler changed during measurement")
    dump(args.output / "summary.json", {"requested_pairs": 5, "completed_pairs": len(records),
        "records": records, "boundary": "Actual differences and incomplete observations preserved; no cost tolerance or gate substitution"})


def aggregate(args):
    results, missing, malformed = {}, [], []
    for case, tag in zip(CASES, TAGS):
        root = args.inputs / ("std-cost-" + tag)
        try:
            request = json.loads((root / "measurements/request.json").read_text())
            if tuple(request["case"]) != case or request["samples"] != json.loads(json.dumps(samples(case))):
                raise ValueError("Request changed the exact diagnostic case/sample plan")
            state = json.loads((root / "run-metadata/runner-state.json").read_text())
            summary = json.loads((root / "measurements/summary.json").read_text())
            if summary["requested_pairs"] != 5 or summary["completed_pairs"] != len(summary["records"]):
                raise ValueError("Summary has a mismatched diagnostic denominator")
            wanted = {(axis, sample): list(order) for axis, sample, cores, order in samples(case)}
            seen = {}
            for row in summary["records"]:
                key = (row["axis"], row["sample"])
                if key not in wanted or key in seen or row["order"] != wanted[key]:
                    raise ValueError("Duplicate, extra or altered sample record")
                seen[key] = row
            results[tag] = {"case": case, "runner_state": state, "observed_pairs": len(seen),
                "complete_pairs": sum(not row["incomplete"] for row in seen.values()),
                "missing_pairs": [list(key) for key in wanted if key not in seen],
                "canonical": [row for key, row in seen.items() if key[0] == "canonical-cores4"],
                "additional_cores1": [row for key, row in seen.items() if key[0] == "additional-cores1"]}
        except OSError as error:
            missing.append({"tag": tag, "reason": str(error)})
        except (ValueError, KeyError, TypeError) as error:
            malformed.append({"tag": tag, "reason": str(error)})
    canonical = [row for result in results.values() for row in result["canonical"]]
    controls = [row for result in results.values() for row in result["additional_cores1"]]
    complete = (len(results) == 3 and not missing and not malformed and
                all(result["runner_state"]["complete"] and result["observed_pairs"] == result["complete_pairs"] == 5
                    for result in results.values()))
    report = {"report": False, "diagnostic_complete": complete, "expected_jobs": 3,
        "expected_canonical_pairs": 9, "expected_additional_control_pairs": 6, "expected_invocations": 30,
        "observed_jobs": len(results), "observed_pairs": len(canonical) + len(controls),
        "canonical_exact_observations": complete and all(row["equal_observations"] for row in canonical),
        "canonical_resource_movements": sum("declarations_outcomes_resources_batches" in row["differences"] for row in canonical),
        "canonical_verdict_movements": sum("verdict" in row["differences"] for row in canonical),
        "canonical_session_byte_movements": sum(not row["raw_session_files_equal"] for row in canonical),
        "missing_jobs": missing, "malformed_jobs": malformed, "jobs": results,
        "boundary": "Diagnostic report only; report=false is deliberate. Actual exactness/complete fields are separate, controls do not replace canonical cores4 and no full-gate acceptance follows."}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    dump(args.output, report)
    print(json.dumps({key: value for key, value in report.items() if key != "jobs"}))


def check():
    for case in CASES:
        ss = samples(case)
        assert len(ss) == 5 and sum(s[2] == 4 for s in ss) == 3 and sum(s[2] == 1 for s in ss) == 2
        assert {ss[-2][3][0], ss[-1][3][0]} == {"baseline", "final"}
        row = {"args": ["dfyconfig.toml", "original.dfy"], "own_options": []}
        ordinary = command("dev", "std", "project", row, Path("compiler"), Path("z3"), Path("scratch"), "absent")
        for cores in (4, 1):
            cmd = observed_command(row, Path("compiler"), Path("z3"), Path("scratch"), cores)
            stripped = cmd[:-2]
            stripped[stripped.index("--cores") + 1] = "4"
            assert stripped == ordinary
            assert not any("seed" in v or "extended-newtype" in v or "additional-axioms" in v for v in cmd)
    print(json.dumps({"boundary": "Pure generated-command checks; no engine invocation", "jobs": 3, "canonical_pairs": 9, "additional_control_pairs": 6, "compiler_invocations_if_dispatched": 30}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("check", "run", "aggregate"))
    parser.add_argument("--solver-axis", choices=("5.1.0", "4.16.0"))
    parser.add_argument("--part")
    for name in ("inputs", "baseline", "final", "baseline-identity", "final-identity", "baseline-version", "final-version", "solver", "solver-version", "output"):
        parser.add_argument("--" + name, type=Path)
    args = parser.parse_args()
    if args.command == "check":
        check()
    elif args.command == "aggregate":
        if args.inputs is None or args.output is None:
            parser.error("aggregate requires --inputs and --output")
        aggregate(args)
    else:
        if any(getattr(args, name.replace("-", "_")) is None for name in ("solver-axis", "part", "inputs", "baseline", "final", "baseline-identity", "final-identity", "baseline-version", "final-version", "solver", "solver-version", "output")):
            parser.error("run requires every frozen source/compiler/solver/input/output argument")
        run(args)


if __name__ == "__main__":
    main()
