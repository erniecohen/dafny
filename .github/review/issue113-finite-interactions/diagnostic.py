#!/usr/bin/env python3
"""Current-only retained compiler diagnostic. No proof acceptance or oracle rewrite."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import tarfile
import urllib.request
import zipfile
from datetime import datetime, timezone

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def dump(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")

def spec():
    return json.loads((HERE / "spec.json").read_text())

def metadata():
    value = json.loads((HERE / "reused-compiler.json").read_text())
    if set(value["arms"]) != {"final"}:
        raise ValueError("Only the pinned current/final archive is admitted")
    return value, value["arms"]["final"]

def package_guard():
    value = json.loads((HERE / "manifest.json").read_text())
    actual = {p.relative_to(HERE).as_posix(): digest(p) for p in HERE.rglob("*")
              if p.is_file() and p.name != "manifest.json" and "__pycache__" not in p.parts}
    if actual != value["files_sha256"]:
        raise ValueError("Public source/spec/helper closure changed")
    if digest(REPO / ".github/workflows/review.yml") != value["workflow_sha256"]:
        raise ValueError("Workflow differs from the bound diagnostic")
    s = spec()
    if len(s["cases"]) != 22 or len({r["id"] for r in s["cases"]}) != 22:
        raise ValueError("Exact 22 source cases required")
    if [sum(r["group"] == group for r in s["cases"]) for group in ["datatype", "arrow"]] != [10, 12]:
        raise ValueError("Datatype/arrow source denominator differs")
    if s["AX_projections"] != ["false", "true"] or s["expected_physical_invocations"] != 44:
        raise ValueError("Exact 44 AX-derived commands required")
    for row in s["cases"]:
        source = HERE / row["file"]
        if digest(source) != row["source_sha256"] or row["actual_counts"] is not None or row["accepted_counts"] is not None:
            raise ValueError("Source bytes or unmeasured counts differ")
    return {"source_programs": 22, "requested_physical_invocations": 44,
            "spec_sha256": digest(HERE / "spec.json"),
            "manifest_sha256": digest(HERE / "manifest.json")}

def source_guard():
    if os.environ.get("GITHUB_REPOSITORY") != "erniecohen/dafny" or not os.environ.get("GITHUB_REF_NAME", "").startswith("scratch/"):
        raise ValueError("Public diagnostic requires the exact repository and a scratch branch")
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip()
    if head != os.environ.get("GITHUB_SHA"):
        raise ValueError("Checkout and dispatched head differ")
    source = spec()["compiler"]
    for path, key in [("Source", "source_tree"), ("Source/DafnyCore", "core_tree"), ("Source/DafnyDriver", "driver_tree")]:
        tree = subprocess.check_output(["git", "rev-parse", "HEAD:" + path], cwd=REPO, text=True).strip()
        if tree != source[key]:
            raise ValueError("Exact current Source/Core/Driver tree required: " + path)
    entries = subprocess.check_output(["git", "ls-tree", "-r", "-z", "HEAD", "Source"], cwd=REPO).split(b"\0")
    hashes = {}
    for entry in entries:
        if not entry:
            continue
        attrs, name = entry.split(b"\t", 1)
        mode, kind, blob = attrs.split()
        path = REPO / name.decode()
        if kind != b"blob" or path.is_symlink():
            raise ValueError("Unreviewed Source member")
        data = path.read_bytes()
        if hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest().encode() != blob:
            raise ValueError("Actual Source bytes differ from the pinned tree")
        hashes[name.decode()] = hashlib.sha256(data).hexdigest()
    if len(hashes) != spec()["source_files"]:
        raise ValueError("Actual Source file closure differs")
    if digest(REPO / spec()["verify_macro_source"]["path"]) != spec()["verify_macro_source"]["sha256"]:
        raise ValueError("Verify macro source differs")
    if subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=all", "--", "Source"], cwd=REPO):
        raise ValueError("Source has staged, unstaged or untracked changes")
    return {"scratch_head": head, "Source": source["source_tree"], "Core": source["core_tree"],
            "Driver": source["driver_tree"], "source_files": len(hashes), "source_files_sha256": hashes,
            "product": source["product"], "original_build_source_commit": source["original_build_source_commit"],
            "source_equivalence": "Original build commit and requested product have identical Source/Core/Driver trees"}

def checked_api(download):
    record, item = metadata()
    path = download / "artifact-api.json"
    actual = json.loads(path.read_text())
    run = actual.get("workflow_run", {})
    for key, expected in [("id", record["artifact_id"]), ("name", record["artifact_name"]),
                          ("digest", record["artifact_api_digest"]), ("size_in_bytes", record["artifact_size_in_bytes"]),
                          ("url", "https://api.github.com/repos/erniecohen/dafny/actions/artifacts/" + str(record["artifact_id"]))]:
        if actual.get(key) != expected:
            raise ValueError("Fresh public API artifact identity differs: " + key)
    for key, expected in [("id", record["public_run"]), ("head_sha", record["build_workflow_head"]),
                          ("repository_id", record["workflow_repository_id"]),
                          ("head_repository_id", record["workflow_head_repository_id"])]:
        if run.get(key) != expected:
            raise ValueError("Fresh artifact run/repository identity differs: " + key)
    expiry = actual.get("expires_at")
    if actual.get("expired") is not False or not isinstance(expiry, str) or datetime.fromisoformat(expiry.replace("Z", "+00:00")) <= datetime.now(timezone.utc):
        raise ValueError("Artifact is expired or unavailable")
    return {"api_receipt_sha256": digest(path), "id": actual["id"], "public_run": run["id"],
            "api_digest": actual["digest"], "unexpired_at_reuse": True, "expires_at": expiry}

def archive_guard(download):
    record, item = metadata()
    api = checked_api(download)
    archive = download / item["member"]
    if digest(archive) != item["archive_sha256"]:
        raise ValueError("Current compiler archive bytes differ")
    source = download / "final"
    identity = source / "compiler-components.json"
    if digest(identity) != item["identity_record_sha256"] or json.loads(identity.read_text()) != item["identity_record"]:
        raise ValueError("Original component receipt differs")
    if item["source_binding"] != spec()["compiler"] or item["identity_record"]["product_revision"] != spec()["compiler"]["product"]:
        raise ValueError("Current build/product source binding differs")
    for name, expected in item["provenance_files_sha256"].items():
        if digest(source / name) != expected:
            raise ValueError("Original current build provenance differs: " + name)
    if any((source / name).read_text().strip() != "0" for name in item["original_zero_exit_files"]):
        raise ValueError("Original required build/setup/version checks failed")
    if (source / "source.txt").read_text().strip() != spec()["compiler"]["original_build_source_commit"]:
        raise ValueError("Actual original build commit differs")
    if (source / "compiler-version.txt").read_text().strip() != spec()["compiler_version"]:
        raise ValueError("Original current compiler version differs")
    hashes, modes = {}, {}
    with tarfile.open(archive, "r:gz") as tf:
        for entry in tf.getmembers():
            path = Path(entry.name)
            if path.is_absolute() or ".." in path.parts or path.parts[:1] != ("dafny",) or not (entry.isdir() or entry.isfile()):
                raise ValueError("Unreviewed compiler archive entry")
            if entry.isdir():
                continue
            name = path.relative_to("dafny").as_posix()
            if name in hashes:
                raise ValueError("Duplicate compiler archive entry")
            hashes[name] = hashlib.sha256(tf.extractfile(entry).read()).hexdigest()
            modes[name] = entry.mode & 0o777
    if hashes != item["files_sha256"] or modes != item["files_mode"]:
        raise ValueError("Complete retained compiler byte/mode closure differs")
    if len(hashes) != 329 or len(item["identity_record"]["components_sha256"]) != 208 or len(item["identity_record"]["bundled_libraries_sha256"]) != 7:
        raise ValueError("Actual compiler/component/library denominator differs")
    for name, expected in {**item["identity_record"]["components_sha256"], **item["identity_record"]["bundled_libraries_sha256"]}.items():
        if hashes.get(name) != expected:
            raise ValueError("Actual managed/apphost/library member differs")
    return archive, {"api": api, "archive_sha256": digest(archive), "files": 329,
                     "components": 208, "libraries": 7, "new_build_performed": False,
                     "actual_original_build_source": spec()["compiler"]["original_build_source_commit"],
                     "product": spec()["compiler"]["product"]}

def bundle_guard(bundle):
    record, item = metadata()
    if bundle.is_symlink() or any(p.is_symlink() for p in bundle.rglob("*")):
        raise ValueError("Extracted compiler contains a link")
    actual = {p.relative_to(bundle).as_posix(): digest(p) for p in bundle.rglob("*") if p.is_file()}
    modes = {p.relative_to(bundle).as_posix(): p.stat().st_mode & 0o777 for p in bundle.rglob("*") if p.is_file()}
    if actual != item["files_sha256"] or modes != item["files_mode"]:
        raise ValueError("Extracted complete compiler bytes/modes differ")
    return {"files": actual, "modes": modes, "files_count": 329, "all_actual_bytes_match": True}

def captured(cmd, directory, stem, timeout=None):
    directory.mkdir(parents=True, exist_ok=True)
    dump(directory / (stem + "-command.json"), {"argv": cmd, "cwd": str(REPO), "wall_safety_seconds": timeout})
    timed_out = False
    with (directory / (stem + ".stdout")).open("wb") as stdout, (directory / (stem + ".stderr")).open("wb") as stderr:
        child = subprocess.Popen(cmd, cwd=REPO, stdout=stdout, stderr=stderr, start_new_session=True)
        try:
            code = child.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            os.killpg(child.pid, signal.SIGKILL)
            code = child.wait()
    (directory / (stem + "-exit.txt")).write_text(str(code) + "\n")
    receipt = {"exit": code, "timed_out": timed_out, "stdout_sha256": digest(directory / (stem + ".stdout")),
               "stderr_sha256": digest(directory / (stem + ".stderr"))}
    dump(directory / (stem + "-outcome.json"), receipt)
    return receipt

def install_solver(work, output):
    pin = spec()["solver"]
    archive = work / "solver.zip"
    urllib.request.urlretrieve(pin["url"], archive)
    if digest(archive) != pin["archive_sha256"]:
        raise ValueError("Pinned solver archive differs")
    target = work / "solver"
    with zipfile.ZipFile(archive) as z:
        for info in z.infolist():
            path = Path(info.filename)
            if path.is_absolute() or ".." in path.parts:
                raise ValueError("Unreviewed solver archive path")
        z.extractall(target)
    choices = sorted(target.rglob("bin/z3"))
    if len(choices) != 1:
        raise ValueError("Exact pinned 5.1 solver executable missing or ambiguous")
    solver = choices[0]
    solver.chmod(0o755)
    outcome = captured([str(solver), "--version"], output, "solver-version", timeout=60)
    actual = (output / "solver-version.stdout").read_text().strip()
    if outcome["exit"] != 0 or outcome["timed_out"] or actual != pin["expected_version_line"]:
        raise ValueError("Actual named solver/version check failed")
    receipt = {"archive_sha256": digest(archive), "binary_sha256": digest(solver), "version": actual, "exit": outcome["exit"]}
    dump(output / "solver-identity.json", receipt)
    dump(work / "solver.json", {"path": str(solver), "identity": receipt})
    return solver

def prepare(download, work, output):
    dump(output / "package-before.json", package_guard())
    dump(output / "Source-before.json", source_guard())
    archive, receipt = archive_guard(download)
    dump(output / "archive-provenance.json", receipt)
    work.mkdir(parents=True, exist_ok=False)
    with tarfile.open(archive, "r:gz") as tf:
        tf.extractall(work)
    bundle = work / "dafny"
    dump(output / "compiler-before.json", bundle_guard(bundle))
    shutil.copyfile(download / "artifact-api.json", output / "artifact-api.json")
    shutil.copyfile(download / "final/compiler-components.json", output / "original-compiler-components.json")
    record, item = metadata()
    for name in item["provenance_files_sha256"]:
        shutil.copyfile(download / "final" / name, output / ("original-" + name))
    outcome = captured([str(bundle / "Dafny"), "--version"], output, "compiler-version", timeout=60)
    if outcome["exit"] != 0 or outcome["timed_out"] or (output / "compiler-version.stdout").read_text().strip() != spec()["compiler_version"]:
        raise ValueError("Actual reused compiler version differs")
    install_solver(work, output)
    dump(work / "prepared.json", {"archive": receipt, "spec_sha256": digest(HERE / "spec.json")})
    dump(output / "prepare-result.json", {"prepared": True, "new_build": False, "engine_proofs_invoked": 0})

def command(row, axioms, work, directory):
    s = spec()
    solver = Path(json.loads((work / "solver.json").read_text())["path"])
    observations = ["--solver-path", str(solver), "--additional-axioms=" + axioms,
                    "--log-format", "csv;LogFileName=" + str(directory / "results.csv"),
                    "--log-format", "json;LogFileName=" + str(directory / "results.json"),
                    "--bprint", str(directory / "program.bpl")]
    return [str(work / "dafny/Dafny")] + s["verify_macro_defaults"] + observations + row["own_arguments_before_source"] + [str(HERE / row["file"])]

def summarize_case(row, axioms, directory, outcome, cmd):
    stdout = (directory / "verify.stdout").read_text(errors="replace")
    stderr = (directory / "verify.stderr").read_text(errors="replace")
    text = stdout + "\n" + stderr
    statuses = [{"verified": int(v), "errors": int(e)} for v, e in re.findall(r"(\d+) verified,\s*(\d+) errors?", text)]
    locations = []
    pattern = re.compile(r"(?m)^([^\r\n]+?)\((\d+),(\d+)\):\s*(Error|Warning):\s*(.*)$")
    for m in pattern.finditer(text):
        locations.append({"file": m[1], "line": int(m[2]), "column": int(m[3]), "kind": m[4], "message": m[5],
                          "is_fixture": Path(m[1]).name == Path(row["file"]).name})
    target_lines = {r["line"] for r in row["intended_rejection_locations"]}
    errors = [r for r in locations if r["kind"] == "Error"]
    targets = [r for r in errors if r["is_fixture"] and r["line"] in target_lines]
    earlier = [r for r in errors if not (r["is_fixture"] and r["line"] in target_lines)]
    warnings = bool(re.search(r"(?im)\bwarning\b", text))
    capacity = outcome["timed_out"] or bool(re.search(r"(?i)out of resource|out of rlimit|timed out|timeout", text))
    files = {p.name: digest(p) for p in directory.iterdir() if p.is_file()}
    emitted = {name: {"exists": (directory / name).is_file(), "sha256": files.get(name)}
               for name in ["results.csv", "results.json", "program.bpl"]}
    result = {"id": row["id"], "group": row["group"], "AX": axioms, "argv": cmd,
              "source_sha256": row["source_sha256"], "spec_sha256": digest(HERE / "spec.json"),
              "exit": outcome["exit"], "timed_out": outcome["timed_out"], "summary_counts_raw": statuses,
              "diagnostic_locations": locations, "warnings_observed": warnings, "capacity_failure_observed": capacity,
              "unmeasured_intended_exit": row["unmeasured_intended_exit"],
              "intended_exit_observed": outcome["exit"] == row["unmeasured_intended_exit"],
              "intended_rejection_locations": row["intended_rejection_locations"],
              "target_line_error_observed": bool(targets), "target_line_errors": targets,
              "other_or_earlier_errors": earlier, "negative_reach_classification": "Pending source/BPL review; line overlap alone is not semantic acceptance",
              "emitted_observations": emitted, "raw_files_sha256": files,
              "accepted": None, "actual_count_oracle": None}
    dump(directory / "result.json", result)
    return result

def run_case(case_id, work, output):
    s = spec()
    rows = [r for r in s["cases"] if r["id"] == case_id]
    if len(rows) != 1:
        raise ValueError("Unknown exact fixture")
    row = rows[0]
    prepared = json.loads((work / "prepared.json").read_text())
    if prepared["spec_sha256"] != digest(HERE / "spec.json"):
        raise ValueError("Prepared compiler/source spec differs")
    dump(output / "run-package-before.json", package_guard())
    dump(output / "run-Source-before.json", source_guard())
    dump(output / "run-compiler-before.json", bundle_guard(work / "dafny"))
    solver_info = json.loads((work / "solver.json").read_text())
    if digest(Path(solver_info["path"])) != solver_info["identity"]["binary_sha256"]:
        raise ValueError("Actual solver binary changed before run")
    results = []
    for axioms in s["AX_projections"]:
        directory = output / "cases" / case_id / ("AX-" + axioms)
        if directory.exists():
            raise ValueError("Observation directory must be fresh")
        directory.mkdir(parents=True)
        shutil.copyfile(HERE / row["file"], directory / Path(row["file"]).name)
        cmd = command(row, axioms, work, directory)
        outcome = captured(cmd, directory, "verify", timeout=s["wall_clock_safety_seconds"])
        results.append(summarize_case(row, axioms, directory, outcome, cmd))
    dump(output / "run-package-after.json", package_guard())
    dump(output / "run-Source-after.json", source_guard())
    dump(output / "run-compiler-after.json", bundle_guard(work / "dafny"))
    if digest(Path(solver_info["path"])) != solver_info["identity"]["binary_sha256"]:
        raise ValueError("Actual solver binary changed after run")
    dump(output / "run-solver-after.json", solver_info["identity"])
    dump(output / "case-report.json", {"case": case_id, "requested": 2, "observed_invocations": len(results),
                                      "post_byte_guards_passed": True, "results": results,
                                      "boundary": "Raw diagnostic observations, not acceptance"})

def report(artifacts, output, guard_errors):
    s = spec()
    rows = {r["id"]: r for r in s["cases"]}
    requested = {(row["id"], ax) for row in s["cases"] for ax in s["AX_projections"]}
    observed, reports, defects, case_reports = {}, [], list(guard_errors), {}
    for path in sorted(artifacts.rglob("case-report.json")):
        value = json.loads(path.read_text())
        reports.append({"file": path.relative_to(artifacts).as_posix(), "sha256": digest(path)})
        case_id = value.get("case")
        if case_id not in rows or case_id in case_reports:
            defects.append("Duplicate/unrequested case report: " + str(case_id))
        case_reports[case_id] = value
        if value.get("post_byte_guards_passed") is not True or value.get("requested") != 2 or value.get("observed_invocations") != 2:
            defects.append("Missing post-byte/pair closure: " + str(case_id))
    for path in sorted(artifacts.rglob("result.json")):
        if "cases" not in path.parts:
            continue
        result = json.loads(path.read_text())
        key = (result.get("id"), result.get("AX"))
        if key in observed or key not in requested:
            defects.append("Duplicate/unrequested case projection: " + str(key))
            continue
        observed[key] = result
        directory = path.parent
        row = rows[result["id"]]
        if result.get("source_sha256") != row["source_sha256"] or result.get("spec_sha256") != digest(HERE / "spec.json"):
            defects.append("Raw source/spec identity differs: " + str(key))
        for name, expected in result["raw_files_sha256"].items():
            file = directory / name
            if not file.is_file() or digest(file) != expected:
                defects.append("Raw file missing/changed: " + str(file))
        source_file = directory / Path(row["file"]).name
        if not source_file.is_file() or digest(source_file) != row["source_sha256"]:
            defects.append("Actual captured source missing/changed: " + str(key))
        pair = case_reports.get(result["id"])
        if pair is None or result not in pair.get("results", []):
            defects.append("Raw invocation lacks matching completed byte-guarded pair: " + str(key))
        outcome_file = directory / "verify-outcome.json"
        command_file = directory / "verify-command.json"
        exit_file = directory / "verify-exit.txt"
        if not all(p.is_file() for p in [outcome_file, command_file, exit_file]):
            defects.append("Physical invocation receipt missing: " + str(key))
        elif (json.loads(outcome_file.read_text())["exit"] != result["exit"] or
              int(exit_file.read_text()) != result["exit"] or
              json.loads(command_file.read_text())["argv"] != result["argv"]):
            defects.append("Physical argv/exit receipt differs: " + str(key))
    missing = sorted(requested - set(observed))
    value = {"diagnostic_complete": not defects and not missing and len(case_reports) == 22,
             "requested_source_programs": 22, "requested_physical_invocations": 44,
             "observed_case_reports": len(reports), "observed_physical_invocations": len(observed),
             "missing_projections": [list(key) for key in missing], "raw_observation_defects": defects,
             "case_reports": reports, "results": [observed[key] for key in sorted(observed)],
             "spec_sha256": digest(HERE / "spec.json"), "compiler": s["compiler"], "compiler_version": s["compiler_version"],
             "canonical_RUNs_executed": False, "count_oracles": None, "accepted": None,
             "boundary": "Actions success is diagnostic recording only. Raw exit/warnings/capacity/BPL/CSV/JSON omissions and intended-rejection reach remain visible; no semantic or cost acceptance."}
    dump(output / "report.json", value)
    (output / "report.md").write_text("Requested 22 source programs / 44 AX projections.\n\nObserved " + str(len(reports)) +
      " case reports / " + str(len(observed)) + " physical invocations. Observation closure: " + str(value["diagnostic_complete"]) +
      ".\n\nThis is a diagnostic record. Actual exits, diagnostics, proof counts, emitted BPL and resource logs require review; no accepted counts or proof-green claim is supplied.\n")
    return value

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["plan", "prepare", "run", "report"])
    parser.add_argument("--download", type=Path)
    parser.add_argument("--work", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--case")
    parser.add_argument("--artifacts", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    try:
        if args.mode == "plan":
            dump(args.output / "package.json", package_guard())
            dump(args.output / "Source.json", source_guard())
            matrix = {"include": [{"case": r["id"]} for r in spec()["cases"]]}
            dump(args.output / "matrix.json", matrix)
            print("matrix=" + json.dumps(matrix, separators=(",", ":")))
        elif args.mode == "prepare":
            prepare(args.download.resolve(), args.work.resolve(), args.output.resolve())
        elif args.mode == "run":
            run_case(args.case, args.work.resolve(), args.output.resolve())
        else:
            guard_errors = []
            for name, check in [("package", package_guard), ("Source", source_guard)]:
                try:
                    dump(args.output / (name + ".json"), check())
                except Exception as error:
                    guard_errors.append(name + " guard failed: " + type(error).__name__ + ": " + str(error))
            report(args.artifacts.resolve(), args.output.resolve(), guard_errors)
    except Exception as error:
        failure = {"mode": args.mode, "type": type(error).__name__, "message": str(error),
                   "accepted": False, "missing_or_failed_observations_remain": True}
        dump(args.output / "collector-error.json", failure)
        if args.mode == "report":
            dump(args.output / "report.json", {"diagnostic_complete": False,
                 "requested_source_programs": 22, "requested_physical_invocations": 44,
                 "observed_physical_invocations": None, "failure": failure,
                 "count_oracles": None, "accepted": None,
                 "boundary": "Failed collector/guard is an incomplete diagnostic, never acceptance."})
        print(type(error).__name__ + ": " + str(error), file=sys.stderr)
        return 1
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
