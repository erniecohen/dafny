#!/usr/bin/env python3
"""Bounded, recorded feature probes; diagnostic mode never blesses failures."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

CASES = {}

def main():
  parser = argparse.ArgumentParser()
  parser.add_argument("dafny")
  parser.add_argument("solver")
  parser.add_argument("output")
  parser.add_argument("--extended", action="store_true")
  args = parser.parse_args()
  out = Path(args.output)
  out.mkdir(parents=True, exist_ok=True)
  versions = {}
  for name, exe in (("dafny", args.dafny), ("solver", args.solver)):
    p = subprocess.run([exe, "--version"], capture_output=True, text=True)
    versions[name] = p.stdout.strip()
  rows = []
  for draft in sorted(Path(".github/review/issue113-probes").glob("*.dfy")):
    CASES[draft.stem] = draft.read_text()
  for name, source in CASES.items():
    case = out / name
    case.mkdir(exist_ok=True)
    path = case / "case.dfy"
    path.write_text(source)
    feature_enabled = args.extended and "existing-" not in name
    command = [args.dafny, "verify", str(path), "--general-newtypes=true", "--type-system-refresh=true",
               "--solver-path", args.solver, "--cores=1", "--resource-limit=16000000",
               "--verification-time-limit=60", "--show-snippets=false", "--allow-warnings", "--additional-axioms=false", "--rprint", str(case / "resolved.dfy"), "--bprint", str(case / "program.bpl"), "--boogie", "/normalizeDeclarationOrder:0", "--log-format", "csv;LogFileName=" + str(case / "resources.csv")]
    if "runtime" in name:
      command.append("--relax-definite-assignment")
    if "trait" in name:
      command.append("--general-traits=datatype")
    if feature_enabled:
      command.append("--extended-newtype-bases")
    try:
      p = subprocess.run(command, capture_output=True, text=True, timeout=120)
      code, stdout, stderr = p.returncode, p.stdout, p.stderr
    except subprocess.TimeoutExpired as exc:
      code, stdout, stderr = 124, str(exc.stdout), str(exc.stderr)
    (case / "stdout.txt").write_text(stdout)
    (case / "stderr.txt").write_text(stderr)
    row = {"case": name, "source_sha256": hashlib.sha256(source.encode()).hexdigest(),
           "command": command, "exit": code, "stdout": stdout, "stderr": stderr}
    if "runtime" in name and code == 0:
      runs = []
      for target, erase in [(target, erase) for target in ("cs", "js", "py", "go", "java")
                            for erase in ((True, False) if "datatype" in name else (True,))]:
        label = target + ("" if erase else "-materialized")
        runtime_path = case / label / "case.dfy"
        runtime_path.parent.mkdir(exist_ok=True)
        runtime_path.write_text(source)
        run_command = [args.dafny, "run", str(runtime_path), "--no-verify", "--target=" + target,
                       "--general-newtypes=true", "--type-system-refresh=true", "--relax-definite-assignment",
                       "--allow-warnings", "--show-snippets=false", "--spill-translation"]
        if feature_enabled:
          run_command.append("--extended-newtype-bases")
        if not erase:
          run_command.append("--optimize-erasable-datatype-wrapper=false")
        try:
          run = subprocess.run(run_command, capture_output=True, text=True, timeout=120)
          run_exit, run_stdout, run_stderr = run.returncode, run.stdout, run.stderr
        except subprocess.TimeoutExpired as exc:
          run_exit, run_stdout, run_stderr = 124, str(exc.stdout), str(exc.stderr)
        (case / (label + "-stdout.txt")).write_text(run_stdout)
        (case / (label + "-stderr.txt")).write_text(run_stderr)
        runs.append({"target": target, "wrapper_erasure": erase, "command": run_command, "exit": run_exit,
                     "stdout": run_stdout, "stderr": run_stderr})
        print(name, target, run_exit)
      row["runtime"] = runs
    rows.append(row)
    print(name, code, stdout.strip().splitlines()[-1:] or stderr.strip().splitlines()[-1:])
  (out / "results.json").write_text(json.dumps({"versions": versions, "extended": args.extended,
                                              "diagnostic_only": True, "cases": rows}, indent=2))

if __name__ == "__main__":
  main()
