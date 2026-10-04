#!/usr/bin/env python3
"""Bounded, recorded benchmark draft. Run only after reviewing source and compiler options."""
import argparse
import csv
from decimal import Decimal
import fnmatch
import hashlib
import json
import os
import platform
import random
import re
import signal
import shutil
import statistics
import subprocess
import threading
import time
from pathlib import Path

import cs_profile

MAX_CASES = 32
MAX_COMMANDS = 120

def measured_command(command, directory, stem):
    if platform.system() == "Linux" and Path("/usr/bin/time").exists():
        return ["/usr/bin/time", "-v", "-o", str(directory / (stem + "-process.txt")), "--"] + command
    return command

def process_metrics(directory, stem):
    path = directory / (stem + "-process.txt")
    if not path.exists():
        return {"available": False}
    text = path.read_text()
    def field(label):
        match = re.search(re.escape(label) + r":\s*([0-9.]+)", text)
        return None if match is None else float(match.group(1))
    return {"available": True, "peak_rss_kib": field("Maximum resident set size (kbytes)"),
            "user_cpu_seconds": field("User time (seconds)"), "system_cpu_seconds": field("System time (seconds)"),
            "boundary": "GNU time command and child-process accounting; includes startup"}


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def compiler_component_hashes(executable):
    """Identify the actual managed compiler bundle, not only its native apphost."""
    apphost = Path(executable).resolve()
    root = apphost.parent
    required = ("DafnyCore.dll", "DafnyDriver.dll", "Dafny.dll",
                "Dafny.deps.json", "Dafny.runtimeconfig.json")
    missing = [name for name in required if not (root / name).is_file()]
    if missing:
        raise ValueError("Compiler bundle is missing identity components: " + ", ".join(missing))
    components = {path.relative_to(root).as_posix(): digest(path)
                  for path in sorted(root.rglob("*"))
                  if path.is_file() and path.suffix in (".dll", ".json")}
    components[apphost.name] = digest(apphost)
    return dict(sorted(components.items()))

def contains_warning(text):
    return any(re.search(r"\bwarning\b", line, re.I)
               and not re.search(r"\b0\s+warnings?(?:\(s\))?(?!\w)", line, re.I)
               for line in text.splitlines())

def execute(command, directory, stem, timeout):
    start = time.perf_counter()
    process = subprocess.Popen(measured_command(command, directory, stem), cwd=directory, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True, start_new_session=True)
    try:
        stdout, stderr = process.communicate(timeout=timeout)
        code = process.returncode
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGKILL)
        stdout, stderr = process.communicate()
        code = 124
    elapsed = time.perf_counter() - start
    (directory / (stem + "-stdout.txt")).write_text(stdout)
    (directory / (stem + "-stderr.txt")).write_text(stderr)
    return {"command": command, "exit": code, "seconds": elapsed, "stdout": stdout, "stderr": stderr,
            "warnings": contains_warning(stdout + "\n" + stderr), "process_metrics": process_metrics(directory, stem)}

def csv_cost(path):
    if not path.exists():
        return {"available": False, "rows": [], "total_resource_count": None}
    with path.open() as stream:
        rows = list(csv.DictReader(stream))
    costs = []
    for row in rows:
        field = next((key for key in row if key.endswith("ResourceCount")), None)
        if field is not None and row[field]:
            value = Decimal(row[field])
            if value != value.to_integral_value():
                raise ValueError("Non-integral solver resource count: " + row[field])
            costs.append(int(value))
    durations = []
    for row in rows:
        value = row.get("TestResult.Duration")
        if value:
            h, m, sec = value.split(":")
            durations.append(int(h) * 3600 + int(m) * 60 + float(sec))
    return {"available": True, "rows": rows, "vc_batches": len(rows),
            "sum_batch_solver_seconds": sum(durations) if durations else None,
            "timing_boundary": "sum of verifier-recorded batch durations; not process wall time",
            "total_resource_count": sum(costs) if costs else None}

def existing_solver_statistics(resources, stdout, stderr):
    """Record only counters already emitted; do not request new prover statistics."""
    observations = []
    metric_names = {"quantinstantiations", "quantifierinstantiations"}
    for batch, row in enumerate(resources.get("rows", [])):
        for key, value in row.items():
            metric = re.sub(r"[^a-z]", "", re.split(r"[.:]", key)[-1].strip().lower())
            if metric in metric_names and value and re.fullmatch(r"[0-9]+", value):
                observations.append({"source": "resources.csv", "batch": batch,
                                     "field": key, "value": int(value)})
    pattern = re.compile(r"^\s*\(?\s*:?(quant-instantiations|quantifier-instantiations)\s+([0-9]+)\s*\)?\s*$", re.I)
    for label, text in (("stdout", stdout), ("stderr", stderr)):
        for line, value in enumerate(text.splitlines(), 1):
            match = pattern.fullmatch(value)
            if match:
                observations.append({"source": label, "line": line,
                                     "field": match.group(1), "value": int(match.group(2))})
    return {"available": bool(observations), "observations": observations,
            "aggregation": "none; separate recorded counters may overlap across batches/streams",
            "boundary": "existing CSV/stdout/stderr only; prover flags and seeds unchanged",
            "unavailable_reason": None if observations else "No quantifier-instantiation counter in existing recorded output"}


def boogie_counts(path):
    if not path.exists():
        return None
    raw = path.read_bytes()
    text = raw.decode(errors="replace")
    return {"bytes": len(raw), "sha256": hashlib.sha256(raw).hexdigest(),
            "axioms": len(re.findall(r"(?m)^\s*axiom\b", text)),
            "functions": len(re.findall(r"(?m)^\s*function\b", text)),
            "procedures": len(re.findall(r"(?m)^\s*procedure\b", text)),
            "implementations": len(re.findall(r"(?m)^\s*implementation\b", text)),
            "quantifier_tokens": len(re.findall(r"\b(?:forall|exists)\b", text)),
            "assert_tokens": len(re.findall(r"\bassert\b", text)),
            "interpretation": "syntactic counts, not exact VC or instantiation counts"}

def runtime_command(target, directory):
    if target == "cs":
        path = directory / "bench.dll"
        command = ["dotnet", str(path)]
    elif target == "java":
        path = directory / "bench.jar"
        command = ["java", "-Dfile.encoding=UTF-8", "-jar", str(path)]
    elif target == "js":
        path = directory / "bench.js"
        command = ["node", str(path)]
    elif target == "py":
        path = directory / "bench-py/__main__.py"
        command = ["python3", "-u", str(path)]
    else:
        path = directory / "bench"
        command = [str(path)]
    if not path.exists():
        raise RuntimeError("Missing emitted entry point: " + str(path))
    executable = shutil.which(command[0])
    if executable is None:
        raise RuntimeError("Missing target runtime: " + command[0])
    command[0] = str(Path(executable).resolve())
    return command

def target_version_commands(targets, stage):
    proposed = {"cs": {"dotnet": ["dotnet", "--version"]},
                "js": {"node": ["node", "--version"]},
                "py": {"python": ["python3", "--version"]},
                "go": {"go": ["go", "version"]},
                "java": {"java": ["java", "-version"]}}
    commands = {name: command for target in targets for name, command in proposed[target].items()}
    if "java" in targets and stage == "build":
        commands["javac"] = ["javac", "-version"]
    return commands

def artifact_files(directory):
    excluded = {"bench.dfy", "build-stdout.txt", "build-stderr.txt"}
    return [{"path": str(path.resolve()), "bytes": path.stat().st_size, "sha256": digest(path)}
            for path in sorted(directory.rglob("*")) if path.is_file() and path.name not in excluded]

def require_artifacts(record):
    artifacts = record.get("artifact_files")
    if not artifacts:
        raise RuntimeError("Build report has no emitted artifact digests")
    for artifact in artifacts:
        if digest(artifact["path"]) != artifact["sha256"]:
            raise RuntimeError("Emitted artifact changed: " + artifact["path"])
    command = record["runtime_command"]
    if digest(command[0]) != record["runtime_binary_sha256"]:
        raise RuntimeError("Target runtime executable changed")

def runtime_sample(command, directory, stem, case, timeout, profile):
    start = time.perf_counter()
    timed_out = threading.Event()
    with (directory / (stem + "-stderr.txt")).open("w") as errors:
        process = subprocess.Popen(measured_command(command, directory, stem), cwd=directory, stdout=subprocess.PIPE, stderr=errors,
                                   text=True, bufsize=1, start_new_session=True)
        def stop():
            timed_out.set()
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
        timer = threading.Timer(timeout, stop)
        timer.start()
        warm = None
        lines = []
        try:
            for line in process.stdout:
                lines.append(line)
                if line.startswith("warmup:") and warm is None:
                    warm = time.perf_counter()
            process.wait()
        finally:
            timer.cancel()
        end = time.perf_counter()
    stdout = "".join(lines)
    (directory / (stem + "-stdout.txt")).write_text(stdout)
    expected = f"warmup:{case['warmup_checksum']}\nresult:{case['measured_checksum']}\n"
    ordinary = stdout.replace("\r\n", "\n")
    measurements = None
    profile_error = None
    if profile == "cs-allocated":
        try:
            ordinary, measurements = cs_profile.parse_output(stdout)
        except ValueError as exc:
            profile_error = str(exc)
    return {"command": command, "exit": 124 if timed_out.is_set() else process.returncode,
            "output_matches": ordinary == expected and profile_error is None,
            "startup_and_warmup_seconds": None if warm is None else warm - start,
            "post_warmup_seconds": None if warm is None else end - warm,
            "total_seconds": end - start, "allocation_count": None,
            "allocation_bytes": None if measurements is None else measurements[1]["managed_allocation_bytes"],
            "work_seconds": None if measurements is None else measurements[1]["seconds"],
            "work_profile": measurements, "profile_error": profile_error,
            "process_metrics": process_metrics(directory, stem), "timing_boundary": "post-warmup includes final output and process teardown; optional Work profile excludes both"}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest")
    parser.add_argument("output")
    parser.add_argument("--dafny", required=True)
    parser.add_argument("--expected-dafny", required=True)
    parser.add_argument("--solver", required=True)
    parser.add_argument("--solver-version", default="5.1.0", choices=("5.1.0", "4.12.1", "4.16.0"))
    parser.add_argument("--solver-seed", type=int, default=0)

    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--executor", required=True, help="Public CI worker or queue-selected executor identifier")
    parser.add_argument("--execution-environment", required=True, choices=("public-ci", "trusted-queue"))
    parser.add_argument("--select", required=True, help="Comma-separated case glob patterns; no default sweep")
    parser.add_argument("--stage", choices=("resolve", "translate", "verify", "build", "run"), default="verify")
    parser.add_argument("--proof-report", help="Matching verified report required before building")
    parser.add_argument("--build-report", help="Matching build report required before timing")
    parser.add_argument("--targets", default="cs,js,py,go,java")
    parser.add_argument("--profile", choices=("none", "cs-allocated"), default="none",
                        help="Optional generated-C# Work instrumentation; requires --targets=cs for build/run")
    parser.add_argument("--samples", type=int, default=5)
    parser.add_argument("--resource-limit", type=int, default=16000000)
    parser.add_argument("--seed", type=int, default=113132, help="Arm order seed; solver seed remains the recorded compiler default")
    args = parser.parse_args()
    manifest_path = Path(args.manifest).resolve()
    manifest = json.loads(manifest_path.read_text())
    selectors = args.select.split(",")
    cases = [case for case in manifest["cases"] if any(fnmatch.fnmatchcase(case["id"], s) for s in selectors)]
    if not cases or len(cases) > MAX_CASES or not 1 <= args.samples <= 9:
        parser.error("select 1..32 cases and 1..9 runtime samples")
    if not 0 < args.resource_limit <= 32000000:
        parser.error("resource limit must be in 1..32000000")
    targets = args.targets.split(",")
    if len(set(targets)) != len(targets) or any(target not in ("cs", "js", "py", "go", "java") for target in targets):
        parser.error("only reviewed proposed backend entry points are allowed")
    if args.profile == "cs-allocated" and args.stage in ("build", "run") and targets != ["cs"]:
        parser.error("C# allocation profiling requires the explicit single target cs")
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=True)
    data = {"draft_runner": True, "solver_version": args.solver_version, "solver_seed": args.solver_seed, "source_sha": args.source_sha, "stage": args.stage,
            "executor": args.executor, "execution_environment": args.execution_environment,
            "manifest_sha256": digest(manifest_path), "order_seed": args.seed, "cases": [],
            "platform": {"system": platform.system(), "release": platform.release(), "machine": platform.machine()},
            "profile": args.profile,
            "profiler_script_sha256": digest(Path(cs_profile.__file__)) if args.profile != "none" else None,
            "allocation_measurements": ("current-thread managed allocated bytes inside Work" if args.profile == "cs-allocated"
                                        else "unavailable; ordinary runs do not infer allocation counts")}
    try:
        dafny_components = compiler_component_hashes(args.dafny)
    except (OSError, ValueError) as error:
        parser.error(str(error))
    for label, executable in (("dafny", args.dafny), ("solver", args.solver)):
        data[label] = execute([executable, "--version"], out, label + "-version", 30)
        data[label]["binary_sha256"] = digest(executable)
    data["dafny"]["components_sha256"] = dafny_components
    if data["dafny"]["exit"] != 0 or data["solver"]["exit"] != 0 or data["dafny"]["stdout"].strip() != args.expected_dafny or data["solver"]["stdout"].strip() != "Z3 version " + args.solver_version + " - 64 bit":
        (out / "results.json").write_text(json.dumps(data, indent=2) + "\n")
        parser.error("exact compiler/solver version mismatch; no benchmark was executed")
    if args.execution_environment == "trusted-queue" and args.solver_version != "5.1.0":
        parser.error("trusted proof work requires Z3 5.1.0")
    proof = json.loads(Path(args.proof_report).read_text()) if args.proof_report else None
    build = json.loads(Path(args.build_report).read_text()) if args.build_report else None
    if args.stage == "build" and not proof:
        parser.error("--proof-report is required before building")
    if args.stage == "run" and not build:
        parser.error("--build-report is required before timing")
    for report in (proof, build):
        if report and (report["source_sha"] != args.source_sha or report["dafny"]["binary_sha256"] != data["dafny"]["binary_sha256"]
                       or report["dafny"].get("components_sha256") != data["dafny"]["components_sha256"]
                       or report["solver"]["binary_sha256"] != data["solver"]["binary_sha256"]
                       or report["manifest_sha256"] != data["manifest_sha256"]):
            parser.error("prior report does not match source/compiler/solver bytes")
    jobs = []
    for case in cases:
        source = manifest_path.parent / case["path"]
        if digest(source) != case["sha256"]:
            parser.error("source checksum changed: " + case["id"])
        if args.stage in ("resolve", "translate", "verify"):
            for enabled in ((False, True) if case["off_supported"] else (True,)):
                jobs.extend((case, enabled, None, sample) for sample in range(args.samples))
        elif case["runtime"]:
            for target in targets:
                for sample in range(args.samples if args.stage == "run" else 1):
                    jobs.append((case, True, target, sample))
    versions = target_version_commands(targets, args.stage) if args.stage in ("build", "run") else {}
    rebuilds = len(jobs) if args.stage == "build" and args.profile == "cs-allocated" else 0
    if len(jobs) + rebuilds + 2 + len(versions) > MAX_COMMANDS:
        parser.error("command ceiling exceeded; split the selection into smaller explicit runs")
    data["target_tools"] = {}
    for label, command in versions.items():
        try:
            executable = shutil.which(command[0])
            if executable is None:
                raise RuntimeError("Missing target tool: " + command[0])
            command[0] = str(Path(executable).resolve())
            result = execute(command, out, label + "-version", 30)
            result["binary_sha256"] = digest(command[0])
            data["target_tools"][label] = result
        except Exception as exc:
            data["target_tools"][label] = {"exit": 125, "error": str(exc)}
    random.Random(args.seed).shuffle(jobs)
    for case, enabled, target, sample in jobs:
        directory = out / (case["id"].replace("/", "-") + ("-on" if enabled else "-off") + ("-" + target if target else ""))
        if args.stage in ("resolve", "translate", "verify") and args.samples > 1:
            directory = directory / ("sample-" + str(sample))
        directory.mkdir(parents=True, exist_ok=True)
        source = manifest_path.parent / case["path"]
        common = ["--general-newtypes=true", "--type-system-refresh=true", "--unicode-char=true",
                  "--extended-newtype-bases=" + str(enabled).lower(),
                  "--show-snippets=false"]
        row = {"id": case["id"], "family": case["family"], "arm": case["arm"], "source_sha256": case["sha256"],
               "feature_enabled": enabled, "erasure": case["erasure"], "target": target, "sample": sample,
               "profile": args.profile}
        row["common_flags"] = common
        try:
            if args.stage in ("resolve", "translate", "verify"):
                command = [args.dafny, "verify" if args.stage == "translate" else args.stage, str(source)] + common
                if args.stage == "translate":
                    command = [args.dafny, str(source), "/compile:0", "/noVerify", "/functionSyntax:4", "/unicodeChar:1",
                               "/generalNewtypes:1", "/typeSystemRefresh:1", "/extendedNewtypeBases:" + ("1" if enabled else "0"),
                               "/print:" + str(directory / "program.bpl")]
                    row["translation_boundary"] = "legacy parse/resolution plus Boogie translation and preparation, no solver verification"
                if args.stage == "verify":
                    command += ["--solver-path", args.solver, "--cores=1", "--resource-limit=" + str(args.resource_limit),
                                "--verification-time-limit=60", "--boogie", "/normalizeDeclarationOrder:0", "--boogie", "/proverOpt:O:smt.random_seed=" + str(args.solver_seed),
                                "--bprint", str(directory / "program.bpl"),
                                "--log-format", "csv;LogFileName=" + str(directory / "resources.csv")]
                row.update(execute(command, directory, args.stage, 90))
                if args.stage == "translate":
                    notice = "Warning: this way of using the CLI is deprecated. Use 'dafny --help' to see help for the new Dafny CLI format"
                    row["cli_deprecation_notice"] = notice in row["stdout"].splitlines()
                    row["warnings"] = contains_warning("\n".join(line for line in row["stdout"].splitlines() if line != notice) + "\n" + row["stderr"])
                row["resources"] = csv_cost(directory / "resources.csv")
                row["solver_statistics"] = existing_solver_statistics(row["resources"], row["stdout"], row["stderr"])
                if args.stage == "resolve":
                    row["resolution_process_memory"] = {
                        "available": row["process_metrics"].get("available", False),
                        "peak_rss_kib": row["process_metrics"].get("peak_rss_kib"),
                        "boundary": "entire resolve command: runtime startup, parsing and resolution; not an isolated resolver phase",
                        "measurement": "existing GNU time peak RSS; no additional process or sampling"}
                row["boogie"] = boogie_counts(directory / "program.bpl")
            elif args.stage == "build":
                matching = [p for p in proof["cases"] if p["id"] == case["id"] and p["feature_enabled"]
                            and p["source_sha256"] == case["sha256"] and p["exit"] == 0 and not p["warnings"]
                            and p.get("common_flags") == common]
                if not matching or proof["stage"] != "verify":
                    raise RuntimeError("No matching positive verification result")
                local_source = directory / "bench.dfy"
                local_source.write_bytes(source.read_bytes())
                command = [args.dafny, "build", str(local_source), "--no-verify", "--target=" + target,
                           "--spill-translation", "--output", str(directory / "bench"), "--optimize-erasable-datatype-wrapper=" + str(case["erasure"]).lower()] + common
                row.update(execute(command, directory, "build", 120))
                if row["exit"] == 0 and args.profile == "cs-allocated":
                    row["profile_instrumentation"] = cs_profile.instrument(directory)
                    dotnet = shutil.which("dotnet")
                    if dotnet is None:
                        raise RuntimeError("Missing C# profiling rebuild tool")
                    profile_command = [str(Path(dotnet).resolve()), "build", str(directory / "bench.csproj"),
                                       "--no-restore", "--output", str(directory)]
                    row["profile_rebuild"] = execute(profile_command, directory, "profile-rebuild", 120)
                    row["exit"] = row["profile_rebuild"]["exit"]
                    row["warnings"] = row["warnings"] or row["profile_rebuild"]["warnings"]
                if row["exit"] == 0:
                    row["runtime_command"] = runtime_command(target, directory)
                    row["runtime_binary_sha256"] = digest(row["runtime_command"][0])
                    row["artifact_files"] = artifact_files(directory)
                row["generated_source"] = [{"path": str(p.relative_to(directory)), "bytes": p.stat().st_size,
                                            "sha256": digest(p)} for p in directory.rglob("*")
                                           if p.suffix in (".cs", ".js", ".py", ".go", ".java")]
            else:
                matching = [p for p in build["cases"] if p["id"] == case["id"] and p["target"] == target
                            and p["source_sha256"] == case["sha256"] and p["exit"] == 0 and p.get("runtime_command")
                            and p.get("common_flags") == common and not p["warnings"]
                            and p.get("profile", "none") == args.profile]
                if not matching or build["stage"] != "build":
                    raise RuntimeError("No matching emitted entry point from a positive build")
                command = matching[0]["runtime_command"]
                require_artifacts(matching[0])
                if args.profile != "none" and build.get("profiler_script_sha256") != data["profiler_script_sha256"]:
                    raise RuntimeError("Profiling script changed after the recorded build")
                row.update(runtime_sample(command, directory, "sample-" + str(sample), case, 60, args.profile))
        except Exception as exc:
            row.update({"exit": 125, "error": str(exc)})
        data["cases"].append(row)
        (out / "results.json").write_text(json.dumps(data, indent=2) + "\n")
    if args.stage == "run":
        data["summary"] = []
        for identifier, target in sorted({(row["id"], row["target"]) for row in data["cases"]}):
            values = [row["post_warmup_seconds"] for row in data["cases"] if row["id"] == identifier and row["target"] == target
                      and row["exit"] == 0 and row.get("output_matches") and row.get("post_warmup_seconds") is not None]
            median = statistics.median(values) if values else None
            mad = statistics.median(abs(v - median) for v in values) if values else None
            profile_values = [row for row in data["cases"] if row["id"] == identifier and row["target"] == target
                              and row["exit"] == 0 and row.get("output_matches") and row.get("work_profile")]
            allocations = [row["allocation_bytes"] for row in profile_values]
            work_times = [row["work_seconds"] for row in profile_values]
            allocation_median = statistics.median(allocations) if allocations else None
            work_median = statistics.median(work_times) if work_times else None
            data["summary"].append({"id": identifier, "target": target, "successful_samples": len(values),
                                    "requested_samples": args.samples, "median_seconds": median, "mad_seconds": mad,
                                    "profile": args.profile, "profile_samples": len(profile_values),
                                    "median_work_seconds": work_median,
                                    "mad_work_seconds": statistics.median(abs(v - work_median) for v in work_times) if work_times else None,
                                    "median_managed_allocation_bytes": allocation_median,
                                    "mad_managed_allocation_bytes": statistics.median(abs(v - allocation_median) for v in allocations) if allocations else None})
    (out / "results.json").write_text(json.dumps(data, indent=2) + "\n")
    print("Recorded diagnostic benchmark rows:", len(data["cases"]))

if __name__ == "__main__":
    main()
