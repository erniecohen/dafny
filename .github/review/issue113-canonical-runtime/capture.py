#!/usr/bin/env python3
"""Bounded C# ordinary/external-runtime diagnostic; run only on public CI."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

def sha256(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1048576), b""):
            h.update(chunk)
    return h.hexdigest()

def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")

def execute(command, cwd, directory, extra_env=None):
    directory.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment.update(extra_env or {})
    write_json(directory / "command.json", {"argv": command, "cwd": str(cwd),
                                            "extra_environment": extra_env or {}})
    with (directory / "stdout.txt").open("wb") as out, (directory / "stderr.txt").open("wb") as err:
        try:
            process = subprocess.run(command, cwd=cwd, env=environment,
                                     stdout=out, stderr=err, timeout=900, check=False)
            result = {"exit": process.returncode, "timed_out": False}
        except subprocess.TimeoutExpired:
            result = {"exit": None, "timed_out": True}
    write_json(directory / "result.json", result)
    return result

def inventory(directory, dlls_only=False):
    return [{"path": str(path.relative_to(directory)), "bytes": path.stat().st_size,
             "sha256": sha256(path)}
            for path in sorted(directory.rglob("*")) if path.is_file() and
            (not dlls_only or path.suffix.lower() == ".dll")]

def compiler_defaults(repo):
    source = repo / "Source/DafnyDriver/DafnyCliTests.cs"
    text = source.read_text(encoding="utf-8-sig")
    declaration = re.search(r"NewDefaultArgumentsForTesting\s*=\s*(?:new\[\]\s*)?[\[\{](.*?)[\]\}];",
                            text, re.S)
    if not declaration:
        raise ValueError("Canonical testing defaults were not found; do not guess flags")
    # Remove comments before matching string literals.
    body = re.sub(r"//[^\n]*", "", declaration.group(1))
    return re.findall(r'"([^"]*)"', body)

def normalized_output(stdout):
    # Exact canonical MultiBackendTest RunWithCompiler trailer handling.
    trailer = re.search(r"\r?\nDafny program verifier[^\r\n]*\r?\n", stdout)
    return stdout[trailer.end():] if trailer else stdout

def compiler_snapshot(binary_dir, directory):
    write_json(directory / "dll-inventory.json", inventory(binary_dir, dlls_only=True))
    # Keep the exact dependencies/metadata used by the TestDafny and Dafny hosts.
    selected = ["Dafny.dll", "DafnyCore.dll", "DafnyDriver.dll", "TestDafny.dll", "DafnyRuntime.dll"]
    selected += [path.name for path in binary_dir.glob("*.deps.json")]
    selected += [path.name for path in binary_dir.glob("*.runtimeconfig.json")]
    for name in sorted(set(selected)):
        source = binary_dir / name
        if source.is_file():
            target = directory / "components" / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
    execute(["dotnet", str(binary_dir / "Dafny.dll"), "--version"], binary_dir, directory / "dafny-version")

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--label", required=True)
    args = parser.parse_args()
    repo, output = args.repo.resolve(), args.out.resolve()
    output.mkdir(parents=True, exist_ok=True)
    source_sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
    if source_sha != args.source_sha:
        raise ValueError("Compiler source SHA does not match the matrix")
    names = ["git-issue-113-extended-newtypes-generic-companion-runtime.dfy",
             "git-issue-113-extended-newtypes-datatype-generic-witness-runtime.dfy"]
    fixture_dir = Path("Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues")
    original_inputs = []
    for name in names:
        for suffix in ["", ".expect", ".verify.expect"]:
            source = repo / fixture_dir / (name + suffix)
            target = output / "original-fixtures" / (name + suffix)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            original_inputs.append({"path": str(fixture_dir / (name + suffix)),
                                    "sha256": sha256(source), "bytes": source.stat().st_size})
    write_json(output / "source-identity.json", {
        "label": args.label, "source_sha": source_sha, "original_fixture_files": original_inputs,
        "canonical_inherited_flags": compiler_defaults(repo),
        "scope": "exact two original registered fixtures, both source RUN wrapper settings, cs ordinary/external-runtime harness configurations",
        "temporary_root_adjustment": "per-phase TMPDIR retains actual canonical random build projects; no input or expected bytes changed",
    })
    execute(["dotnet", "--info"], repo, output / "dotnet-info")
    dependency = None
    dependency_script = repo / "Scripts/fetch-boogie-packages.sh"
    if dependency_script.is_file():
        dependency = execute(["bash", str(dependency_script)], repo, output / "fetch-boogie-packages")
        if dependency["exit"] != 0:
            write_json(output / "summary.json", {"capture_complete": False, "dependency_fetch": dependency})
            return 1
    # Literal canonical IntegrationTests build: no additional SourceRevisionId override.
    build = execute(["dotnet", "build", "Source/IntegrationTests", "-c", "Release"],
                    repo, output / "compiler-build")
    if build["exit"] != 0:
        write_json(output / "summary.json", {"capture_complete": False, "compiler_build": build})
        return 1
    binary_dir = repo / "Source/IntegrationTests/bin/Release/net8.0"
    if not (binary_dir / "DafnyRuntime.dll").is_file():
        raise ValueError("Canonical test output has no external runtime library")
    phases, preludes = [], []
    for phase in ["before-prelude", "after-prelude"]:
        if phase == "after-prelude":
            steps = [
                ("core", ["dotnet", "test", "Source/DafnyCore.Test", "-c", "Release", "--filter",
                 "FullyQualifiedName~Cardinality|FullyQualifiedName~ExtendedNewtype|FullyQualifiedName~NewtypeReferenceCharacteristicTests"]),
                ("language-server", ["dotnet", "test", "Source/DafnyLanguageServer.Test", "-c", "Release", "--filter",
                 "FullyQualifiedName~Cardinality|FullyQualifiedName~ExtendedNewtypeCachingTest"]),
                ("integration", ["dotnet", "test", "Source/IntegrationTests", "-c", "Release", "--no-build", "--filter",
                 "FullyQualifiedName~CardinalityEmissionTests|FullyQualifiedName~ExtendedNewtypeOptionTests"]),
            ]
            for label, command in steps:
                result = execute(command, repo, output / "prelude" / label)
                preludes.append({"stage": label, **result})
        directory = output / phase
        compiler_snapshot(binary_dir, directory / "input-binaries")
        actual_inputs = []
        for name in names:
            for suffix in ["", ".expect", ".verify.expect"]:
                source = binary_dir / "TestFiles/LitTests/LitTest/git-issues" / (name + suffix)
                actual_inputs.append({"path": str(source.relative_to(binary_dir)), "sha256": sha256(source)})
                original = next(row for row in original_inputs if row["path"].endswith("/" + name + suffix))
                if sha256(source) != original["sha256"]:
                    raise ValueError("Copied canonical fixture/golden differs from unchanged source: " + name + suffix)
        write_json(directory / "actual-harness-inputs.json", actual_inputs)
        temporary = directory / "generated-temporary-files"
        temporary.mkdir(parents=True, exist_ok=True)
        test_filter = "|".join("DisplayName~" + name for name in names)
        result = execute(["dotnet", "test", "Source/IntegrationTests", "-c", "Release", "--no-build",
                          "--logger", "console;verbosity=normal", "--filter", test_filter],
                         repo, directory / "canonical-harness",
                         {"DAFNY_INTEGRATION_TESTS_ONLY_COMPILERS": "cs",
                          "DAFNY_INTEGRATION_TESTS_UPDATE_EXPECT_FILE": "false",
                          "TMPDIR": str(temporary)})
        # Also retain output files if this compiler ignores the temporary build path.
        native_roots = [("copied-test-files", binary_dir / "TestFiles"),
                        ("source-test-files", repo / "Source/IntegrationTests/TestFiles")]
        native_records = []
        for scope, native_root in native_roots:
            if not native_root.is_dir():
                continue
            for native in sorted(native_root.rglob("*")):
                if native.is_file() and (native.suffix.lower() in [".cs", ".csproj", ".dll"] or
                                        native.name.endswith((".deps.json", ".runtimeconfig.json"))):
                    relative = native.relative_to(native_root)
                    target = directory / "native-working-directory-files" / scope / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(native, target)
                    native_records.append({"scope": scope, "path": str(relative), "sha256": sha256(native)})
        write_json(directory / "native-working-directory-inventory.json", native_records)
        files = inventory(temporary)
        write_json(directory / "generated-inventory.json", files)
        projects = [file for file in files if file["path"].endswith(".csproj")]
        dependencies = [file for file in files if file["path"].endswith(".deps.json")]
        write_json(directory / "project-dependency-inventory.json",
                   {"projects": projects, "dependencies": dependencies,
                    "dlls": [file for file in files if file["path"].lower().endswith(".dll")],
                    "runtime_input_sha256": sha256(binary_dir / "DafnyRuntime.dll")})
        # Keep the post-harness compiler/runtime state too; tests must not silently change it.
        compiler_snapshot(binary_dir, directory / "after-harness-binaries")
        phases.append({"phase": phase, **result,
                       "projects": len(projects), "deps_json": len(dependencies),
                       "runtime_input_sha256": sha256(binary_dir / "DafnyRuntime.dll")})
    summary = {"source_sha": source_sha, "capture_complete": True, "compiler_build": build,
               "dependency_fetch": dependency, "prelude": preludes, "canonical_phases": phases,
               "no_expected_output_updates": True,
               "boundary": "focused canonical result; separate from full canonical gate and external CLI pairs"}
    write_json(output / "summary.json", summary)
    print(json.dumps(summary, indent=2))
    return 1 if any(row["exit"] != 0 for row in preludes + phases) else 0

if __name__ == "__main__":
    sys.exit(main())
