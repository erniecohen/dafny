"""Scratch-only paired acceptance measurements; record failures and exit zero."""
from pathlib import Path
import csv
import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import traceback
import urllib.request
import zipfile

role = os.environ["GITHUB_REF_NAME"].rsplit("/", 1)[-1]
out = Path("issue75-probe") / role
out.mkdir(parents=True, exist_ok=True)
records = []


def phase(name, command, timeout=2100, cwd=None):
    folder = out / name
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "command.json").write_text(json.dumps(command, indent=2) + "\n")
    try:
        result = subprocess.run(command, cwd=cwd, text=True, capture_output=True, timeout=timeout)
        code, text = result.returncode, result.stdout + result.stderr
    except subprocess.TimeoutExpired as error:
        code, text = "TIMEOUT", str(error)
    (folder / "output.txt").write_text(text)
    records.append({"name": name, "exit": code})
    print(name, "actual exit", code, flush=True)
    return code


def load_table(path):
    return {row[0]: row[1:] for row in csv.reader(path.open(), delimiter="\t") if row}


def differences(left, right):
    return [{"key": key, "baseline": left.get(key), "candidate": right.get(key)}
        for key in sorted(set(left) | set(right)) if left.get(key) != right.get(key)]


try:
    mode = role.split("-")[1]
    enabled = mode == "on"
    kind = "inputs" if role == "std-inputs" else ("suite" if role.startswith("suite-") else "std")
    solver = Path("Binaries/z3/bin/z3-5.1.0").resolve()
    solver_version = "5.1.0"
    if kind == "std" and role.endswith("4.16"):
        archive = out / "z3-4.16.0.zip"
        urllib.request.urlretrieve("https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03/z3-4.16.0-x64-ubuntu-24.04-bin.zip", archive)
        assert hashlib.sha256(archive.read_bytes()).hexdigest() == "87de9c9e86c877abfccb00cf24b946c4a1bda7b9cba6f363ea58311d2573c320"
        with zipfile.ZipFile(archive) as zip_file:
            zip_file.extractall(out / "solver")
        solver = (out / "solver/z3-4.16.0").resolve()
        solver.chmod(0o755)
        solver_version = "4.16.0"
    actual_version = subprocess.check_output([str(solver), "--version"], text=True).strip()
    assert solver_version in actual_version, actual_version
    sources = {"baseline": "4e98f3f592310854ca1448d517b82dcbcc0f51f4",
        "candidate": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()}
    identities = {"role": role, "sources": sources, "solver": actual_version,
        "solver_sha256": hashlib.sha256(solver.read_bytes()).hexdigest(), "compilers": {}}
    if kind == "suite":
        phase("plan", ["python3", ".github/review/lit-verdicts.py", "plan",
            "Source/IntegrationTests/TestFiles/LitTests/LitTest", str(out / "plan.tsv")])
    for label, source in sources.items():
        subprocess.run(["git", "fetch", "--no-tags", "--depth=1", "origin", source], check=True)
        subprocess.run(["git", "worktree", "add", "--detach", "compiler", source], check=True)
        compiler = Path("compiler").resolve()
        product = (out / (label + "-compiler")).resolve()
        if phase(label + "-boogie", ["sh", "Scripts/fetch-boogie-packages.sh"], 300, compiler) != 0:
            raise RuntimeError("Boogie package acquisition failed")
        if phase(label + "-build", ["dotnet", "publish", str(compiler / "Source/Dafny/Dafny.csproj"),
            "-c", "Release", "-o", str(product), "-p:GeneratePackageOnBuild=false",
            "-p:SourceRevisionId=" + source[:8]], 1800) != 0:
            raise RuntimeError("Compiler build failed")
        dafny = str(product / "Dafny")
        identities["compilers"][label] = {"source": source,
            "version": subprocess.check_output([dafny, "--version"], text=True).strip(),
            "core_sha256": hashlib.sha256((product / "DafnyCore.dll").read_bytes()).hexdigest()}
        if kind == "suite":
            phase(label + "-suite", ["python3", ".github/review/lit-verdicts.py", "run",
                str(out / "plan.tsv"), "Source/IntegrationTests/TestFiles/LitTests/LitTest",
                dafny, str(solver), str(out / (label + ".tsv")), "--jobs", "4",
                "--additional-axioms", str(enabled).lower(), "--measurements", str(out / (label + "-measurements"))])
            phase(label + "-inventory", ["python3", ".github/review/additional-axioms.py", "inventory",
                str(out / (label + "-measurements")), str(out / (label + "-resources.json"))])
        elif kind == "inputs":
            library = Path("Source/DafnyStandardLibraries").resolve()
            targets = library / "src/Std/TargetSpecific"
            parts = [("Std", library, ["src/Std/dfyconfig.toml"])]
            for target in ["notarget", "cs", "java", "js", "go", "py"]:
                files = sorted("./" + str(file.relative_to(targets)) for file in targets.rglob("*" + "-" + target + "*.dfy"))
                parts.append((target, targets, ["dfyconfig.toml"] + files))
            for part, directory, inputs in parts:
                destination = (out / (label + "-inputs") / part).resolve()
                destination.mkdir(parents=True, exist_ok=True)
                phase(label + "-inputs-" + part, [dafny, "build"] + inputs + ["--target=lib", "--no-verify",
                    "--output=" + str(destination / "library"), "--bprint=" + str(destination / "program.bpl"),
                    "--additional-axioms=false", "--cores=4", "--allow-warnings"], 900, directory)
        else:
            phase(label + "-stdlib", ["python3", ".github/review/std-verdicts.py", "run",
                "Source/DafnyStandardLibraries", dafny, str(solver), str(out / (label + ".tsv")),
                "--cores", "4", "--additional-axioms", str(enabled).lower()])
        subprocess.run(["git", "worktree", "remove", "--force", "compiler"], check=True)
        shutil.rmtree(product)
    if kind == "inputs":
        inventories = {}
        for label in sources:
            directory = out / (label + "-inputs")
            files = sorted(directory.rglob("*.bpl"))
            inventories[label] = {str(file.relative_to(directory)): hashlib.sha256(file.read_bytes()).hexdigest() for file in files}
        report = {"diagnostic_only": True, "translation_only": True, "identities": identities,
            "coverage": {label: len(inventory) for label, inventory in inventories.items()},
            "generated_input_differences": differences(inventories["baseline"], inventories["candidate"])}
        (out / "comparison.json").write_text(json.dumps(report, indent=2) + "\n")
        raise SystemExit(0)
    tables = {label: load_table(out / (label + ".tsv")) for label in sources}
    verdict = lambda table: {key: row[:3] for key, row in table.items()}
    if kind == "suite":
        resources = {label: json.loads((out / (label + "-resources.json")).read_text()) for label in sources}
        counts = {label: inventory["batches"] for label, inventory in resources.items()}
        coverage = {label: {"programs": len(inventory["programs"]), "batches": len(inventory["batches"]),
            "unavailable": inventory["unavailable"]} for label, inventory in resources.items()}
    else:
        counts = {label: {key: [row[0], int(row[3]) if row[3].isdigit() else None] for key, row in table.items() if not key.startswith("run ")}
            for label, table in tables.items()}
        coverage = {label: {"rows": len(table), "declarations": len(counts[label])} for label, table in tables.items()}
    report = {"diagnostic_only": True, "kind": kind, "mode": mode, "identities": identities,
        "coverage": coverage, "verdict_differences": differences(verdict(tables["baseline"]), verdict(tables["candidate"])),
        "resource_differences": differences(counts["baseline"], counts["candidate"])}
    (out / "comparison.json").write_text(json.dumps(report, indent=2) + "\n")
except Exception:
    (out / "exception.txt").write_text(traceback.format_exc())
finally:
    (out / "phases.json").write_text(json.dumps(records, indent=2) + "\n")
    summary = "## Issue 97 paired diagnostic: " + role + "\n\nExpected failures are retained; wrapper success is not acceptance.\n\n" + "".join(
        "- " + row["name"] + ": actual exit " + str(row["exit"]) + "\n" for row in records)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        Path(os.environ["GITHUB_STEP_SUMMARY"]).write_text(summary)
