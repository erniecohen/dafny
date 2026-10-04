#!/usr/bin/env python3
"""Recorded dependency diagnostics: every verifier outcome is retained, none is blessed."""
import argparse
import hashlib
import json
import os
import signal
import subprocess
import traceback
from pathlib import Path

CANDIDATE = "1e415f026102940e0a6cee822f131925c7243bbf"

def execute(command, timeout):
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True, start_new_session=True)
    try:
        stdout, stderr = process.communicate(timeout=timeout)
        return process.returncode, stdout, stderr
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGKILL)
        stdout, stderr = process.communicate()
        return 124, stdout, stderr

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("dafny")
    parser.add_argument("solver")
    parser.add_argument("output")
    args = parser.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)
    data = {"candidate": CANDIDATE, "diagnostic_only": True, "cases": [], "versions": {}}
    for name, exe in (("dafny", args.dafny), ("solver", args.solver)):
        code, stdout, stderr = execute([exe, "--version"], 30)
        data["versions"][name] = {"exit": code, "stdout": stdout.strip(), "stderr": stderr.strip(),
                                  "binary_sha256": hashlib.sha256(Path(exe).read_bytes()).hexdigest()}
    valid = data["versions"]["dafny"]["exit"] == 0 and data["versions"]["dafny"]["stdout"].startswith("4.11.0")
    valid = valid and data["versions"]["solver"]["exit"] == 0 and "Z3 version 5.1.0" in data["versions"]["solver"]["stdout"]
    data["toolchain_matches"] = valid
    if not valid:
        (out / "results.json").write_text(json.dumps(data, indent=2) + "\n")
        print("Toolchain mismatch; no verifier cases were run.")
        return
    root = Path(".github/review/dependency-132-probes")
    manifest = json.loads((root / "manifest.json").read_text())
    for name, metadata in sorted(manifest.items()):
        source = (root / (name + ".dfy")).read_bytes()
        for additional in (False, True):
            case = out / (name + ("-additional" if additional else "-default"))
            case.mkdir(exist_ok=True)
            path = case / "case.dfy"
            path.write_bytes(source)
            command = [args.dafny, "verify", str(path), "--general-newtypes=true", "--type-system-refresh=true",
                       "--additional-axioms=" + str(additional).lower(), "--solver-path", args.solver, "--cores=1",
                       "--resource-limit=16000000", "--verification-time-limit=60", "--show-snippets=false",
                       "--allow-warnings", "--bprint", str(case / "program.bpl"),
                       "--boogie", "/normalizeDeclarationOrder:0", "--log-format", "csv;LogFileName=" + str(case / "resources.csv")]
            try:
                code, stdout, stderr = execute(command, 90)
            except Exception:
                code, stdout, stderr = 125, "", traceback.format_exc()
            (case / "stdout.txt").write_text(stdout)
            (case / "stderr.txt").write_text(stderr)
            row = {"case": name, "kind": metadata["kind"], "additional_axioms": additional,
                   "source_sha256": hashlib.sha256(source).hexdigest(), "command": command,
                   "exit": code, "stdout": stdout, "stderr": stderr}
            data["cases"].append(row)
            (out / "results.json").write_text(json.dumps(data, indent=2) + "\n")
            print(name, "additional=" + str(additional).lower(), "exit=" + str(code), flush=True)
    summary = ["| Case | Category | Additional axioms | Exit |", "| --- | --- | --- | --- |"]
    for row in data["cases"]:
        summary.append("| {case} | {kind} | {additional_axioms} | {exit} |".format(**row))
    summary_text = "\n".join(summary) + "\n"
    (out / "summary.md").write_text(summary_text)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as stream:
            stream.write("Diagnostic outcomes only; exit zero does not establish a sound gate.\n\n" + summary_text)

if __name__ == "__main__":
    main()
