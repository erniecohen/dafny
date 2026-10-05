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

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--label", required=True)
    args = parser.parse_args()
    repo, package, output = args.repo.resolve(), args.package.resolve(), args.out.resolve()
    output.mkdir(parents=True, exist_ok=True)
    actual_sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
    if actual_sha != args.source_sha:
        raise ValueError("Compiler checkout does not match the matrix source SHA")
    write_json(output / "identity.json", {
        "label": args.label, "source_sha": actual_sha,
        "source_tree": subprocess.check_output(["git", "rev-parse", "HEAD^{tree}"], cwd=repo, text=True).strip(),
        "entrypoint": "external Dafny CLI from the IntegrationTests output directory",
        "scope": "compile/run --no-verify plus final-source prelude and focused strict canonical runtime tests; not a canonical-full-gate result",
        "package_inventory": inventory(package),
    })
    defaults = compiler_defaults(repo)
    has_feature = "ExtendedNewtypeBases" in (repo / "Source/DafnyCore/Options/CommonOptionBag.cs").read_text() \
        if (repo / "Source/DafnyCore/Options/CommonOptionBag.cs").is_file() else \
        any("ExtendedNewtypeBases" in path.read_text() for path in
            (repo / "Source/DafnyCore").glob("**/CommonOptionBag.cs"))
    write_json(output / "canonical-defaults.json", {"flags": defaults, "feature_option_present": has_feature})
    execute(["dotnet", "--info"], repo, output / "dotnet-info")
    dependency_script = repo / "Scripts/fetch-boogie-packages.sh"
    if dependency_script.is_file():
        dependency = execute(["bash", str(dependency_script)], repo, output / "fetch-boogie-packages")
        if dependency["exit"] != 0:
            write_json(output / "summary.json", {"dependency_fetch": dependency,
                       "capture_complete": False, "reason": "pinned Boogie dependency fetch did not pass"})
            return 1
    build_result = execute(
        ["dotnet", "build", "Source/IntegrationTests", "-c", "Release",
         "-p:SourceRevisionId=" + actual_sha], repo, output / "compiler-build")
    if build_result["exit"] != 0:
        write_json(output / "summary.json", {"compiler_build": build_result,
                    "capture_complete": False, "reason": "compiler build did not pass"})
        return 1
    binary_dir = repo / "Source/IntegrationTests/bin/Release/net8.0"
    cli, runtime = binary_dir / "Dafny.dll", binary_dir / "DafnyRuntime.dll"
    if not cli.is_file() or not runtime.is_file():
        raise ValueError("IntegrationTests output lacks the required CLI or external runtime DLL")
    write_json(output / "compiler-dll-inventory.json", [file for file in inventory(binary_dir, dlls_only=True) if file["path"].lower().endswith(".dll")])
    for name in ["Dafny.dll", "DafnyCore.dll", "DafnyDriver.dll", "TestDafny.dll", "DafnyRuntime.dll"]:
        if (binary_dir / name).is_file():
            target = output / "compiler-components" / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(binary_dir / name, target)
    cases = json.loads((package / "cases.json").read_text())
    records, skipped, preludes, harnesses = [], [], [], []
    phases = ["before-prelude", "after-prelude"] if has_feature else ["baseline"]
    for phase in phases:
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
        snapshot = output / "compiler-state" / phase
        write_json(snapshot / "dll-inventory.json", inventory(binary_dir, dlls_only=True))
        execute(["dotnet", str(cli), "--version"], binary_dir, snapshot / "dafny-version")
        for name in ["Dafny.dll", "DafnyCore.dll", "DafnyDriver.dll", "TestDafny.dll", "DafnyRuntime.dll"]:
            if (binary_dir / name).is_file():
                target = snapshot / "components" / name
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(binary_dir / name, target)
        for case in cases:
            if case["requires_extended"] and not has_feature:
                skipped.append({"case": case["id"], "status": "not applicable",
                                "reason": "unchanged compiler has no extended-base feature option"})
                continue
            feature_settings = [True] if case["requires_extended"] else ([False, True] if has_feature else [False])
            fixture = package / "fixtures" / case["source"]
            expected = (package / "fixtures" / (case["source"] + ".expect")).read_bytes()
            for feature in feature_settings:
                for erasure in [True, False]:
                    for include_runtime in [True, False]:
                        identity = phase + "/" + case["id"] + "/feature-" + str(feature).lower() + "/erasure-" + \
                            str(erasure).lower() + ("/ordinary" if include_runtime else "/external-runtime")
                        directory = output / "cases" / identity
                        build_dir = directory / "generated"
                        # The canonical harness uses an empty existing directory for ordinary C#;
                        # it deletes that directory before external-runtime runs. Match that ordering.
                        if include_runtime:
                            build_dir.mkdir(parents=True, exist_ok=True)
                        flags = defaults + [
                            "--type-system-refresh=true", "--general-newtypes=true",
                            "--optimize-erasable-datatype-wrapper:" + str(erasure).lower(),
                            "--cores=1", "--resource-limit=16000000", "--show-snippets=false",
                            "--allow-warnings", "--boogie", "/normalizeDeclarationOrder:0",
                            "--relax-definite-assignment"]
                        if has_feature:
                            flags.append("--extended-newtype-bases=" + str(feature).lower())
                        command = ["dotnet", str(cli), "run", "--no-verify", "--emit-uncompilable-code",
                                   "--target:cs", "--build:" + str(build_dir / "program"), str(fixture)] + flags
                        if not include_runtime:
                            command += ["--include-runtime:false", "--input", str(runtime)]
                        result = execute(command, binary_dir, directory / "command")
                        stdout = (directory / "command/stdout.txt").read_text(errors="replace")
                        stderr = (directory / "command/stderr.txt").read_text(errors="replace")
                        observed = normalized_output(stdout).encode()
                        (directory / "program-output.txt").write_bytes(observed)
                        (directory / "expected-output.txt").write_bytes(expected)
                        files = inventory(build_dir) if build_dir.is_dir() else []
                        write_json(directory / "generated-inventory.json", files)
                        record = {
                            "identity": identity, "case": case["id"], "source_sha256": sha256(fixture),
                            "feature": feature, "erasure": erasure, "include_runtime": include_runtime,
                            "compiler_exit": result["exit"], "timed_out": result["timed_out"],
                            "stdout_matches_expected": observed == expected, "stderr_empty": not stderr,
                            "expected_program_sha256": hashlib.sha256(expected).hexdigest(),
                            "observed_program_sha256": hashlib.sha256(observed).hexdigest(),
                            "runtime_input_sha256": sha256(runtime) if not include_runtime else None,
                            "generated_dlls": [file for file in files if file["path"].lower().endswith(".dll")],
                            "generated_projects": [file for file in files if file["path"].endswith(".csproj")],
                            "generated_dependencies": [file for file in files if file["path"].endswith(".deps.json")],
                        }
                        # Host tracing is an additional replay only after a failed external-runtime
                        # run. Preserve the original command/output and file inventory first.
                        if not include_runtime and result["exit"] not in [0, None]:
                            dependencies = list(build_dir.glob("*.deps.json"))
                            generated_dll = build_dir / (dependencies[0].name[:-len(".deps.json")] + ".dll") if len(dependencies) == 1 else build_dir / "no-unique-application.dll"
                            if generated_dll.is_file():
                                trace_dir = directory / "host-trace-replay"
                                trace_dir.mkdir(parents=True, exist_ok=True)
                                replay = execute(["dotnet", str(generated_dll)], binary_dir, trace_dir,
                                    {"COREHOST_TRACE": "1", "COREHOST_TRACE_VERBOSITY": "4",
                                     "COREHOST_TRACEFILE": str(trace_dir / "host-trace.txt")})
                                record["host_trace_replay"] = replay
                        write_json(directory / "record.json", record)
                        records.append(record)
        if has_feature:
            # This separately preserves the actual strict in-process canonical test
            # behavior, including each source RUN line and its unchanged goldens.
            harness_filter = (
                "DisplayName~git-issue-113-extended-newtypes-generic-companion-runtime.dfy|"
                "DisplayName~git-issue-113-extended-newtypes-datatype-generic-witness-runtime.dfy")
            result = execute(["dotnet", "test", "Source/IntegrationTests", "-c", "Release",
                              "--no-build", "--logger", "console;verbosity=normal",
                              "--filter", harness_filter], repo, output / "canonical-harness" / phase,
                             {"DAFNY_INTEGRATION_TESTS_ONLY_COMPILERS": "cs",
                              "DAFNY_INTEGRATION_TESTS_UPDATE_EXPECT_FILE": "false"})
            harnesses.append({"phase": phase, **result})
    failures = [record["identity"] for record in records
                if record["compiler_exit"] != 0 or not record["stdout_matches_expected"] or
                not record["stderr_empty"] or record["timed_out"]]
    write_json(output / "summary.json", {
        "source_sha": actual_sha, "label": args.label, "compiler_build": build_result,
        "capture_complete": True, "actual_commands": len(records), "records": records,
        "not_applicable": skipped, "failures": failures, "prelude": preludes, "canonical_harness": harnesses,
        "boundary": "This is an external-CLI diagnostic, not a replacement for the in-process canonical harness.",
    })
    print(json.dumps({"label": args.label, "commands": len(records),
                      "failures": failures, "not_applicable": skipped}, indent=2))
    return 1 if failures or any(step["exit"] != 0 for step in preludes + harnesses) else 0

if __name__ == "__main__":
    sys.exit(main())
