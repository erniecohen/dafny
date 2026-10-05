#!/usr/bin/env python3
"""Paired old-input verification with retained raw artifacts and exact comparisons.

Only the run subcommand invokes Dafny; plan/check/aggregate are pure data operations.
Print/log observations occur in the same invocation that verifies the program.
"""
import argparse
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import signal
import subprocess
import threading
import time
from plan import HERE, fixed_options, digest
from prepare import components, inputs

LOCK = threading.Lock()
ERRLINE = re.compile(r"^(\S[^\n]*?\(\d+,\d+\)): (?:Error|Verification out of resource|Verification of .* timed out)", re.M)


def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def append(path, value):
    # Durable append-only result metadata; never modifies inputs or proof flags.
    with LOCK, path.open("a") as stream:
        stream.write(json.dumps(value, sort_keys=True) + "\n")
        stream.flush()
        os.fsync(stream.fileno())


def command(line, cohort, mode, row, executable, solver, scratch, axioms="absent"):
    own = row["own_options"]
    if any("extended-newtype-bases" in option for option in own):
        raise ValueError("Old default-off plan contains a feature-enable option")
    if line == "dev" and any("additional-axioms" in option for option in own):
        raise ValueError("Dev common plan cannot use the shipped-only option")
    refresh = []
    names = {token.split("=")[0].split(":")[0] for token in own if token.startswith("--")}
    if cohort != "std" and mode == "refresh" and "--type-system-refresh" not in names:
        refresh = ["--type-system-refresh"]
    fixed = ["--cores", "4", "--verification-time-limit", "0"] if cohort in ("std", "parser-focus") else fixed_options(line, own)
    if line == "dev" and axioms != "absent":
        raise ValueError("Dev must omit the absent AdditionalAxioms option")
    extra_axioms = ["--additional-axioms"] if axioms == "on" else []
    observations = ["--log-format", "csv;LogFileName=" + str(scratch / "results.csv"),
                    "--log-format", "json;LogFileName=" + str(scratch / "results.json"),
                    "--bprint", str(scratch / "program.bpl")]
    # Original options remain last, including original errors/missing arguments.
    # Default-off is the false default: no extended option on either compiler.
    # Shipped canonical AXon injects its original wrapper flag before own options;
    # AXoff omits it. Dev never receives this option.
    return [str(executable), "verify"] + row["args"] + ["--solver-path", str(solver)] + fixed + extra_axioms + observations + refresh + own


def execute(cmd, cwd, timeout):
    start = time.monotonic()
    process = subprocess.Popen(cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True, errors="replace", start_new_session=True)
    timed_out = False
    try:
        stdout, stderr = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        timed_out = True
        os.killpg(process.pid, signal.SIGKILL)
        stdout, stderr = process.communicate()
    return {"exit": process.returncode, "timed_out": timed_out,
            "seconds": time.monotonic() - start, "stdout": stdout, "stderr": stderr}


def verdict(row, result, cohort):
    text = result["stdout"] + result["stderr"]
    rc = "TIMEOUT" if result["timed_out"] else str(result["exit"])
    if cohort in ("std", "parser-focus"):
        summary = "; ".join(re.findall(r"verifier finished with ([^\n]*)", text))
        errors = " ".join(sorted(set(ERRLINE.findall(text))))
    else:
        summary = "".join(re.findall(r"verifier finished with ([^\n]*)", text))
        errors = ",".join(sorted(set(re.findall(r"\((\d+),\d+\): Error", text)), key=int))
        oor = len(re.findall(r"out of resource", text))
        if oor:
            summary += "; %d out of resource" % oor
    return [rc, summary, errors]


def declarations(file):
    if not file.exists():
        return {"available": False, "reason": "Verification JSON was not emitted", "rows": []}
    try:
        raw = json.loads(file.read_text())
        rows = []
        for result in raw["verificationResults"]:
            if not isinstance(result.get("name"), str) or not isinstance(result.get("outcome"), str) or type(result.get("resourceCount")) is not int or result["resourceCount"] < 0:
                raise ValueError("Missing or invalid declaration identity/outcome/resource count")
            for batch in result.get("vcResults", []):
                if not isinstance(batch.get("outcome"), str) or type(batch.get("resourceCount")) is not int or batch["resourceCount"] < 0:
                    raise ValueError("Missing or invalid batch outcome/resource count")
            if sum(batch["resourceCount"] for batch in result.get("vcResults", [])) != result["resourceCount"]:
                raise ValueError("Declaration resource count differs from its logged batch sum")
            batches = [{"vcNum": batch.get("vcNum"), "outcome": batch.get("outcome"),
                        "resourceCount": batch.get("resourceCount"), "randomSeed": batch.get("randomSeed"), "randomSeed_present": "randomSeed" in batch}
                       for batch in result.get("vcResults", [])]
            rows.append({"name": result["name"], "outcome": result["outcome"],
                         "resourceCount": result.get("resourceCount"),
                         "vcResults": sorted(batches, key=lambda batch: (batch["vcNum"] or 0, str(batch["randomSeed"])))})
        return {"available": True, "rows": sorted(rows, key=lambda row: row["name"])}
    except (KeyError, TypeError, ValueError) as error:
        return {"available": False, "reason": "Malformed verification JSON: " + str(error), "rows": []}


def normalized_bpl(text, roots, versions):
    """Normalize only identified version/location metadata; keep all semantic text.

    Preserve declaration names/order, attributes/IDs, checksums, literals, triggers,
    axioms and expressions. Never substitute paths in program string literals.
    """
    operations = []
    lines = text.splitlines(keepends=True)
    for index, line in enumerate(lines):
        for version in versions:
            if line.rstrip("\r\n") == "// dafny " + version:
                lines[index] = "// dafny <matched-version-metadata>" + ("\n" if line.endswith("\n") else "")
                operations.append({"line": index + 1, "kind": "reported compiler version comment"})
                line = lines[index]
                break
        if line.lstrip().startswith("//"):
            for root, label in roots:
                if root in line:
                    line = line.replace(root, label)
                    operations.append({"line": index + 1, "kind": "comment location", "root": label})
        else:
            # captureState is diagnostic metadata; preserve the state/line/column.
            def capture(match):
                value = match.group(1)
                for root, label in roots:
                    if value.startswith(root + "/"):
                        operations.append({"line": index + 1, "kind": "captureState location", "root": label})
                        value = label + value[len(root):]
                return '{:captureState "' + value + '"}'
            line = re.sub(r'\{:captureState "([^"\n]*)"\}', capture, line)
        lines[index] = line
    return "".join(lines), operations


def bpl_receipt(directory, roots, versions):
    result = []
    for file in sorted(directory.glob("*.bpl")):
        if file.name.endswith(".normalized.bpl"):
            continue
        raw = file.read_bytes()
        receipt = {"file": file.name, "raw_sha256": hashlib.sha256(raw).hexdigest()}
        try:
            normalized, operations = normalized_bpl(raw.decode("utf-8"), roots, versions)
            normalized_file = file.with_suffix(".normalized.bpl")
            normalized_file.write_text(normalized)
            receipt.update({"normalized_sha256": digest(normalized_file), "normalizations": operations})
        except UnicodeError as error:
            receipt.update({"normalized_sha256": None, "normalization_error": str(error)})
        result.append(receipt)
    return {"available": bool(result), "files": result,
            "boundary": "translated Boogie program before engine VC generation; not a solver-query hash"}


def compare(baseline, final):
    differences = {}
    for field in ("verdict", "warning_lines"):
        if baseline[field] != final[field]:
            differences[field] = {"baseline": baseline[field], "final": final[field]}
    a, b = baseline["declarations"], final["declarations"]
    if a["available"] != b["available"] or a["rows"] != b["rows"]:
        differences["declarations_outcomes_resources_batches"] = {"baseline": a, "final": b}
    def hashes(arm):
        return {file["file"]: file["normalized_sha256"] for file in arm["bpl"]["files"]}
    if hashes(baseline) != hashes(final):
        differences["normalized_bpl"] = {"baseline": hashes(baseline), "final": hashes(final)}
    incomplete = (baseline["timed_out"] or final["timed_out"] or
                  baseline["exit"] == 125 or final["exit"] == 125 or
                  baseline["exit"] < 0 or final["exit"] < 0)
    # A translation/verification summary requires the corresponding observation.
    for arm in (baseline, final):
        if arm["verdict"][1] and (not arm["bpl"]["available"] or not arm["declarations"]["available"]):
            incomplete = True
        if any(file["normalized_sha256"] is None for file in arm["bpl"]["files"]):
            incomplete = True
    return {"differences": differences, "incomplete": incomplete,
            "translated_bpl_comparable": baseline["bpl"]["available"] and final["bpl"]["available"],
            "resources_comparable": a["available"] and b["available"],
            "equal_observations": not differences and not incomplete}


def original_expected(args, row, results):
    if args.cohort == "parser-focus":
        return {"available": False, "reason": "Focused parser symbol selection from the full old Std project; it is not a canonical full-part oracle."}
    if args.cohort == "semantic-unicode" or (args.cohort == "canonical" and args.mode != "default"):
        return {"available": False, "reason": "Additional labelled cohort; original canonical table is not its expected verdict"}
    suffix = "-additional-axioms" if args.axioms == "on" else ""
    if args.cohort == "std":
        spec = json.loads((HERE / "spec.json").read_text())["lines"][args.line]
        solver = "5.1.0" if "5.1.0" in args.solver_version.read_text() else spec["reference_solver"]
        filename = "expected-std-verdicts-z3-" + solver + suffix + ".tsv"
        key = "run " + row["id"]
    else:
        filename = "expected-verdicts" + suffix + ".tsv"
        key = row["id"]
    table = {line.partition("\t")[0]: (line.split("\t") + [""] * 4)[1:4]
             for line in (HERE / "canonical" / args.line / filename).read_text().splitlines() if line.strip()}
    expected = table.get(key)
    result = {"available": expected is not None, "file": filename, "run_verdict": expected,
              "run_movements_from_unrepaired_table": {side: arm["verdict"] != expected for side, arm in results.items()},
              "boundary": "Original unrepaired table is context. Paired baseline/final equality is separate; independent repair changes require issue-specific attribution."}
    if args.cohort == "std":
        result["declaration_movements_from_unrepaired_table"] = {}
        for side, arm in results.items():
            actual = {row["id"] + " " + declaration["name"]: declaration["outcome"]
                      for declaration in arm["declarations"]["rows"]}
            old = {name: value[0] for name, value in table.items() if name.startswith(row["id"] + " ")}
            result["declaration_movements_from_unrepaired_table"][side] = {
                name: {"original": old.get(name), "actual": actual.get(name)}
                for name in sorted(set(old) | set(actual)) if old.get(name) != actual.get(name)}
    return result


def bundle_libraries(executable):
    root = executable.resolve().parent
    return {file.relative_to(root).as_posix(): digest(file) for file in sorted(root.rglob("*.doo"))}


def effective_bprint(cmd, cwd):
    value = None
    for index, token in enumerate(cmd):
        if token.startswith("--bprint=") or token.startswith("--bprint:"):
            value = re.split("[=:]", token, 1)[1]
        elif token == "--bprint" and index + 1 < len(cmd):
            value = cmd[index + 1]
    if value is None:
        return None
    file = Path(value)
    return file if file.is_absolute() else cwd / file


def file_observation(file):
    if not file.is_file():
        return None
    stat = file.stat()
    return {"sha256": digest(file), "size": stat.st_size, "mtime_ns": stat.st_mtime_ns, "inode": stat.st_ino}


def pair(args, row, receipts, versions):
    safe_id = str(row["index"]).zfill(4)
    destination = args.output / "cases" / safe_id
    destination.mkdir(parents=True, exist_ok=True)
    scratch = args.output / "scratch" / safe_id
    # Balanced deterministic order; pair stays on this one runner and input cwd.
    sides = list(getattr(args, "sides", ("baseline", "final")))
    offset = row["index"] % len(sides)
    order = sides[offset:] + sides[:offset]
    dump(destination / "plan.json", row)
    results = {}
    for side in order:
        if scratch.exists():
            shutil.rmtree(scratch)
        scratch.mkdir(parents=True)
        executable = getattr(args, side).resolve()
        cmd = command(args.line, args.cohort, args.mode, row, executable, args.solver.resolve(), scratch.resolve(), args.axioms)
        arm = destination / side
        arm.mkdir()
        dump(arm / "command.json", {"argv": cmd, "cwd": str(args.inputs / row["cwd"]),
                                  "side": side, "order": list(order), "timeout_seconds": args.timeout,
                                  "compiler_identity": receipts[side]})
        printed = effective_bprint(cmd, args.inputs / row["cwd"])
        before_print = file_observation(printed) if printed else None
        append(args.output / "events.jsonl", {"event": "started", "case": row["id"], "side": side})
        try:
            execution = execute(cmd, args.inputs / row["cwd"], args.timeout)
        except OSError as error:
            execution = {"exit": 125, "timed_out": False, "seconds": 0, "stdout": "", "stderr": str(error)}
        (arm / "stdout.txt").write_text(execution["stdout"])
        (arm / "stderr.txt").write_text(execution["stderr"])
        for file in scratch.iterdir():
            if file.is_file():
                shutil.copyfile(file, arm / file.name)
        after_print = file_observation(printed) if printed else None
        fresh_print = after_print is not None and before_print != after_print
        literal_print = None
        if printed and not printed.is_relative_to(scratch) and fresh_print:
            # A literal own --bprint supersedes the observer destination. Observe
            # that exact fresh file; do not expand %t or change its argument.
            literal_print = arm / "literal-program.bpl"
            shutil.copyfile(printed, literal_print)
        dump(arm / "effective-bprint.json", {"path": str(printed) if printed else None,
             "before": before_print, "after": after_print, "fresh": fresh_print,
             "copied_literal_sha256": digest(literal_print) if literal_print else None})
        result = {k: v for k, v in execution.items() if k not in ("stdout", "stderr")}
        result.update({"verdict": verdict(row, execution, args.cohort),
                       "stdout_sha256": digest(arm / "stdout.txt"), "stderr_sha256": digest(arm / "stderr.txt"),
                       "warning_lines": [line for line in (execution["stdout"] + execution["stderr"]).splitlines()
                                         if re.search(r"\bwarning\b", line, re.I)],
                       "resources_csv_sha256": digest(arm / "results.csv") if (arm / "results.csv").is_file() else None,
                       "resources_json_sha256": digest(arm / "results.json") if (arm / "results.json").is_file() else None,
                       "declarations": declarations(arm / "results.json"),
                       "bpl": bpl_receipt(arm, [(str(args.inputs), "<original-inputs>"),
                                               (str(args.output.resolve()), "<observations>")], versions)})
        dump(arm / "result.json", result)
        append(args.output / "events.jsonl", {"event": "completed", "case": row["id"], "side": side,
                                             "exit": result["exit"], "timed_out": result["timed_out"]})
        results[side] = result
    result = {"id": row["id"], "index": row["index"], **compare(results["baseline"], results["final"]),
              "original_expected_context": original_expected(args, row, results)}
    if "candidate" in results:
        result["candidate_comparisons"] = {
            "baseline/candidate": compare(results["baseline"], results["candidate"]),
            "final/candidate": compare(results["final"], results["candidate"])}
    if args.cohort in ("std", "parser-focus") and any(
        not arm["bpl"]["available"] or not arm["declarations"]["available"] for arm in results.values()):
        # Every old Std part requests translation and verification. A completed
        # exit alone does not provide its mandatory cost/translation observations.
        result["incomplete"] = True
        result["equal_observations"] = False
    dump(destination / "comparison.json", result)
    return result


def run(args):
    if not 1 <= args.jobs <= 4:
        raise ValueError("At most four concurrent matched pairs")
    if args.output.exists():
        raise ValueError("Observation directory must be fresh; no cached/mixed results")
    if args.line != "shipped":
        raise ValueError("This current composition measures shipping only")
    args.inputs = args.inputs.resolve()
    args.output = args.output.resolve()
    input_receipt = inputs(args.line, args.inputs)
    plan = json.loads((HERE / "canonical" / args.line / "plan.json").read_text())[args.cohort]
    shard, shards = map(int, args.shard.split("/"))
    if not 0 <= shard < shards <= 16:
        raise ValueError("Invalid bounded shard")
    mine = [row for row in plan if row["index"] % shards == shard]
    if args.part:
        mine = [row for row in mine if row["id"] == args.part]
        if len(mine) != 1:
            raise ValueError("Part selector must select one original part")
    args.timeout = 5400 if args.cohort == "std" else 900
    receipts = {}
    versions = []
    spec = json.loads((HERE / "spec.json").read_text())["lines"][args.line]
    for side in ("baseline", "final"):
        identity_file = getattr(args, side + "_identity")
        receipt = json.loads(identity_file.read_text())
        if receipt["line"] != args.line or receipt["side"] != side:
            raise ValueError("Mismatched compiler identity")
        if receipt["product_revision"] != spec["repaired_product" if side == "baseline" else "final_product"]:
            raise ValueError("Compiler identity does not name the exact requested product revision")
        if components(getattr(args, side)) != receipt["components_sha256"]:
            raise ValueError("Actual managed compiler bytes differ from their build receipt")
        if bundle_libraries(getattr(args, side)) != receipt.get("bundled_libraries_sha256"):
            raise ValueError("Actual packaged library bytes differ from their build receipt")
        receipts[side] = receipt
        versions += getattr(args, side + "_version").read_text().strip().splitlines()
    args.output.mkdir(parents=True)
    dump(args.output / "request.json", {"line": args.line, "cohort": args.cohort, "mode": args.mode, "axioms": args.axioms,
                                        "shard": args.shard, "part": args.part, "plan": mine,
                                        "total_plan": len(plan), "requested": len(mine), "jobs": args.jobs,
                                        "input_receipt": input_receipt, "solver_sha256": digest(args.solver),
                                        "solver_version": args.solver_version.read_text(),
                                        "host": {"hostname": platform.node(), "platform": platform.platform(),
                                                 "cpus": os.cpu_count()},
                                        "proof_flags_boundary": "original canonical budgets/order/seeds; print+JSON only appended",
                                        "feature": "extended-newtype-bases omitted on both sides; default false",
                                        "additional_axioms": "absent option on dev; original shipped wrapper mode " + args.axioms})
    dump(args.output / "compiler-pair.json", {"baseline": receipts["baseline"], "final": receipts["final"],
                                              "bundled_libraries_equal": receipts["baseline"].get("bundled_libraries_sha256") == receipts["final"].get("bundled_libraries_sha256"),
                                              "library_boundary": "Std TargetSpecific uses identical committed old-source .doo inputs. Cases selecting packaged standard libraries also retain actual bundle-library identity."})
    solver_identity_before = digest(args.solver)
    with concurrent.futures.ThreadPoolExecutor(args.jobs) as pool:
        results = list(pool.map(lambda row: pair(args, row, receipts, versions), mine))
    # Detect source/bundle changes during the measurement rather than trusting logs.
    inputs(args.line, args.inputs)
    for side in receipts:
        if components(getattr(args, side)) != receipts[side]["components_sha256"]:
            raise ValueError("Compiler bundle changed during measurement")
        if bundle_libraries(getattr(args, side)) != receipts[side]["bundled_libraries_sha256"]:
            raise ValueError("Packaged library changed during measurement")
    if digest(args.solver) != solver_identity_before:
        raise ValueError("Actual solver changed during measurement")
    summary = {"requested": len(mine), "completed_pairs": len(results), "results": results,
               "differences": sum(bool(row["differences"]) for row in results),
               "verdict_movements": sum("verdict" in row["differences"] for row in results),
               "resource_or_batch_movements": sum("declarations_outcomes_resources_batches" in row["differences"] for row in results),
               "translated_bpl_movements": sum("normalized_bpl" in row["differences"] for row in results),
               "resources_comparable": sum(row["resources_comparable"] for row in results),
               "translated_bpl_comparable": sum(row["translated_bpl_comparable"] for row in results),
               "incomplete": sum(row["incomplete"] for row in results),
               "boundary": "equal verdicts/resources/translated BPL do not claim all programs verified"}
    dump(args.output / "summary.json", summary)
    print(json.dumps({k: v for k, v in summary.items() if k != "results"}))


def aggregate(args):
    spec = json.loads((HERE / "spec.json").read_text())
    report = {"source_spec_sha256": digest(HERE / "spec.json"), "groups": {}, "missing_or_incomplete": []}
    for directory in sorted(args.directory.glob("**/request.json")):
        request = json.loads(directory.read_text())
        key = "/".join((request["line"], request["cohort"], request["mode"], request["axioms"], request["solver_version"].strip()))
        group = report["groups"].setdefault(key, {"expected": request["total_plan"], "cases": {}, "duplicate_ids": []})
        for row in request["plan"]:
            file = directory.parent / "cases" / str(row["index"]).zfill(4) / "comparison.json"
            if row["id"] in group["cases"]:
                group["duplicate_ids"].append(row["id"])
            group["cases"][row["id"]] = json.loads(file.read_text()) if file.exists() else {"id": row["id"], "incomplete": True, "reason": "Missing paired result"}
    for key, group in report["groups"].items():
        group["completed_denominator"] = len(group["cases"])
        group["differences"] = sum(bool(row.get("differences")) for row in group["cases"].values())
        group["verdict_movements"] = sum("verdict" in row.get("differences", {}) for row in group["cases"].values())
        group["resource_or_batch_movements"] = sum("declarations_outcomes_resources_batches" in row.get("differences", {}) for row in group["cases"].values())
        group["translated_bpl_movements"] = sum("normalized_bpl" in row.get("differences", {}) for row in group["cases"].values())
        group["resources_comparable"] = sum(row.get("resources_comparable", False) for row in group["cases"].values())
        group["translated_bpl_comparable"] = sum(row.get("translated_bpl_comparable", False) for row in group["cases"].values())
        group["incomplete"] = sum(bool(row.get("incomplete")) for row in group["cases"].values())
        if group["completed_denominator"] != group["expected"] or group["incomplete"] or group["duplicate_ids"]:
            report["missing_or_incomplete"].append(key)
    # Completely absent jobs/groups cannot disappear from the requested denominator.
    if args.workflow_request.exists():
        expected_groups = json.loads(args.workflow_request.read_text())["groups"]
    else:
        from matrix import workload
        expected_groups = workload()["groups"]
        report["missing_or_incomplete"].append("Workflow request is absent; optional selection unmeasured, required canonical groups still listed")
    for entry in expected_groups:
        matching = [key for key in report["groups"] if key.startswith(entry["line"] + "/" + entry["cohort"] + "/" + entry["mode"] + "/" + entry["axioms"] + "/")
                    and (entry["solver"] in key if entry["solver"] != "reference" else spec["lines"][entry["line"]]["reference_solver"] in key)]
        if not matching:
            report["missing_or_incomplete"].append(entry)
    repair82 = [json.loads(file.read_text()) for file in args.directory.glob("**/repair82-report.json")]
    report["repair82_literal_cohort"] = {"shards": repair82, "separate_from_original_denominator": True}
    if len(repair82) != 2 or any(not item.get("complete") for item in repair82):
        report["missing_or_incomplete"].append("Separate repair82 literal cohort missing/incomplete")
    elif len({item["shard"] for item in repair82}) != 2 or sum(item["literal_sources"] for item in repair82) != 5:
        report["missing_or_incomplete"].append("Separate repair82 literal source coverage is not exact")
    report["interpretation"] = "Compare repaired baseline with final on old bytes. Std default order can move RU between runs; report every movement. Any absent job, timeout or missing observation remains incomplete. Reference expected failures are retained, not greened."
    report["complete_observations"] = not report["missing_or_incomplete"]
    report["diagnostic_only"] = True
    report["proof_acceptance_claimed"] = False
    report["default_off_resource_parity_claimed"] = False
    report["source_pins"] = spec["lines"]
    report["unchanged_development_reuse"] = spec["development_reuse"]
    dump(args.output, report)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    run_parser = sub.add_parser("run")
    run_parser.add_argument("--line", choices=("dev", "shipped"), required=True)
    run_parser.add_argument("--cohort", choices=("canonical", "semantic-unicode", "std"), required=True)
    run_parser.add_argument("--axioms", choices=("absent", "off", "on"), required=True)
    run_parser.add_argument("--mode", choices=("default", "refresh", "project"), required=True)
    for name in ("inputs", "baseline", "final", "baseline-identity", "final-identity", "baseline-version", "final-version", "solver", "solver-version", "output"):
        run_parser.add_argument("--" + name, type=Path, required=True)
    run_parser.add_argument("--shard", default="0/1")
    run_parser.add_argument("--part")
    run_parser.add_argument("--jobs", type=int, default=4)
    aggregate_parser = sub.add_parser("aggregate")
    aggregate_parser.add_argument("directory", type=Path)
    aggregate_parser.add_argument("output", type=Path)
    aggregate_parser.add_argument("--workflow-request", type=Path, required=True)
    args = parser.parse_args()
    (run if args.command == "run" else aggregate)(args)


if __name__ == "__main__":
    main()
