#!/usr/bin/env python3
"""Diagnostic, design-only Dafny runner for issue 193 round 2.

This runner records expected failures and always exits zero after producing a
summary, as required for diagnostic workflows. Read summary.json's unexpected
counts; a green workflow is not feature acceptance. Standard library only.
"""
from __future__ import annotations

import argparse
import csv
import fnmatch
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


SCHEMA = 1
FALSE_ASSERT = re.compile(r"\bassert\s+(?:\{:[^}]*\}\s*)*false\s*;")
ERROR_LOCATION = re.compile(
    r"^(.*?\.dfy)\((\d+),(\d+)\):\s*(?:Error|error):\s*(.*)$")
ABSOLUTE_PATH = re.compile(
    r'''(?<![:\w/])/(?:[^\s"'()\[\]{}<>/]+/)+[^\s"'()\[\]{}<>]*''')


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for part in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(part)
    return h.hexdigest()


def executable(value: str) -> Path:
    found = shutil.which(value)
    return Path(found or value).expanduser().resolve()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dafny", required=True,
                        help="Dafny executable; with --dotnet, the Dafny DLL")
    parser.add_argument("--dotnet", help="Optional dotnet executable for DLL mode")
    parser.add_argument("--z3", required=True)
    parser.add_argument("--sources", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--refresh", action="store_true")
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--resource-limit", type=int, default=200000)
    parser.add_argument("--filter-files", action="append", default=[],
                        help="Repeatable glob for immediate source basenames")
    parser.add_argument("--wall-timeout", type=int, default=120,
                        help="External per-invocation safety limit, in seconds")
    args = parser.parse_args()
    source_dir = args.sources.resolve()
    output_dir = args.output.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    dafny = executable(args.dafny)
    z3 = executable(args.z3)
    dotnet = executable(args.dotnet) if args.dotnet else None
    prefix = [str(dotnet), str(dafny)] if dotnet else [str(dafny)]
    portable_prefix = ["dotnet", "Dafny.dll"] if dotnet else ["dafny"]
    env = os.environ.copy()
    if dotnet:
        env["DOTNET_ROOT"] = str(dotnet.parent)

    replacements = [
        (str(source_dir), "SOURCES"), (str(output_dir), "OUTPUT"),
        (str(dafny), "Dafny.dll" if dotnet else "dafny"), (str(z3), "Z3"),
        (str(Path.cwd()), "WORKDIR"),
    ]
    if dotnet:
        replacements.append((str(dotnet), "dotnet"))
    replacements.sort(key=lambda item: len(item[0]), reverse=True)

    def scrub(value):
        if isinstance(value, dict):
            return {k: scrub(v) for k, v in value.items()}
        if isinstance(value, list):
            return [scrub(v) for v in value]
        if not isinstance(value, str):
            return value
        for before, after in replacements:
            value = value.replace(before, after)
        return ABSOLUTE_PATH.sub(lambda m: "<path>/" + Path(m[0]).name, value)

    def write_json(path: Path, value):
        path.write_text(json.dumps(scrub(value), indent=2, sort_keys=True) + "\n")

    def version(command):
        try:
            result = subprocess.run(command, capture_output=True, text=True,
                                    env=env, timeout=20)
            return {"exit": result.returncode,
                    "stdout": result.stdout.strip(),
                    "stderr": result.stderr.strip()}
        except (OSError, subprocess.TimeoutExpired) as exc:
            return {"exit": None, "error": type(exc).__name__ + ": " + str(exc)}

    tool_files = {}
    for name, path in [("launcher", dafny), ("solver", z3),
                       ("Dafny.dll", dafny.parent / "Dafny.dll"),
                       ("DafnyCore.dll", dafny.parent / "DafnyCore.dll"),
                       ("DafnyDriver.dll", dafny.parent / "DafnyDriver.dll"),
                       ("Boogie.ExecutionEngine.dll", dafny.parent / "Boogie.ExecutionEngine.dll"),
                       ("Boogie.Provers.SMTLib.dll", dafny.parent / "Boogie.Provers.SMTLib.dll")]:
        tool_files[name] = digest(path) if path.is_file() else None
    if dotnet:
        tool_files["dotnet"] = digest(dotnet) if dotnet.is_file() else None
    sources = sorted(source_dir.glob("*.dfy"))
    sources = [path for path in sources if not args.filter_files or any(
        fnmatch.fnmatch(path.name, pat) for pat in args.filter_files)]
    source_lines = {
        path.name: path.read_text(encoding="utf-8").splitlines() for path in sources
    }
    source_hashes = {path.name: digest(path) for path in sources}
    receipt = {
        "schema": SCHEMA, "source_commit": args.source_commit,
        "dafny_version": version(prefix + ["--version"]),
        "solver_version": version([str(z3), "--version"]),
        "tool_sha256": tool_files, "source_sha256": source_hashes,
        "refresh": args.refresh, "resource_limit": args.resource_limit,
        "filter_files": args.filter_files,
        "normalization": "normalizeDeclarationOrder:0; restartProver; cores:1",
        "purpose": "Design-only diagnostic; trusted bridge projections are not product implementation or proofs of their contracts.",
        "control_rule": "Only correctness scopes named control_* expect Errors with Invalid VC(s), and every reported failure must be an assert false source line. Well-formedness and other positive scopes must be Correct. Ablation files are diagnostic-only.",
    }
    write_json(output_dir / "receipt.json", receipt)
    invocation_rows, scope_rows = [], []

    for source in sources:
        for heap_free in ([False, True] if source.name.startswith("lambda-") else [False]):
            tag = source.stem + ("-heapfree-on" if heap_free else "-heapfree-off"
                                 if source.name.startswith("lambda-") else "")
            log_path = output_dir / (tag + ".json")
            text_path = output_dir / (tag + ".txt")
            # A previous interrupted run must never masquerade as this result.
            if log_path.exists():
                log_path.unlink()
            additional = not source.name.startswith("pure-")
            options = [
                "--additional-axioms:" + str(additional).lower(),
                "--type-system-refresh:" + str(args.refresh).lower(),
                "--heap-free-lambda-facts:" + str(heap_free).lower(),
                "--cores:1", "--resource-limit:" + str(args.resource_limit),
                "--verification-time-limit:0", "--error-limit:0",
                "--boogie", "/normalizeDeclarationOrder:0",
                "--boogie", "/restartProver", "--allow-axioms",
                "--show-snippets:false", "--use-basename-for-filename",
            ]
            command = prefix + ["verify", source.name] + options + [
                "--solver-path", str(z3), "--log-format",
                "json;LogFileName=" + str(log_path)]
            portable_command = portable_prefix + ["verify", source.name] + options + [
                "--solver-path", "Z3", "--log-format",
                "json;LogFileName=OUTPUT/" + log_path.name]
            status, exit_code, output = "completed", None, ""
            try:
                result = subprocess.run(command, cwd=source_dir, env=env,
                                        text=True, capture_output=True,
                                        timeout=args.wall_timeout)
                exit_code = result.returncode
                output = result.stdout + result.stderr
                if exit_code not in (0, 4):
                    status = "tool-or-setup-failure"
            except subprocess.TimeoutExpired as exc:
                status = "external-timeout"
                for part in [exc.stdout, exc.stderr]:
                    if part:
                        output += part.decode(errors="replace") if isinstance(part, bytes) else part
                output += "\nExternal invocation safety timeout; no verification conclusion.\n"
            except OSError as exc:
                status = "tool-or-setup-failure"
                output = type(exc).__name__ + ": " + str(exc) + "\n"
            text_path.write_text(scrub(output), encoding="utf-8")
            parsed_log, log_error = None, None
            if log_path.exists():
                try:
                    parsed_log = json.loads(log_path.read_text())
                    write_json(log_path, parsed_log)
                except (ValueError, OSError) as exc:
                    log_error = type(exc).__name__ + ": " + str(exc)
            else:
                log_error = "JSON verification log was not created"
            if log_error and status == "completed":
                status = "log-failure"

            failures = []
            for line in output.splitlines():
                match = ERROR_LOCATION.match(line)
                if not match:
                    continue
                filename, row, col, message = match.groups()
                filename, row, col = Path(filename).name, int(row), int(col)
                lines = source_lines.get(filename)
                if lines is None:
                    candidate = source_dir / filename
                    lines = candidate.read_text().splitlines() if candidate.is_file() else []
                statement = lines[row - 1].strip() if 0 < row <= len(lines) else ""
                failures.append({
                    "file": filename, "line": row, "column": col,
                    "message": message, "statement": statement,
                    "assert_false": bool(FALSE_ASSERT.search(statement)),
                })
            rows_for_invocation = []
            for scope in (parsed_log or {}).get("verificationResults", []):
                name = scope["name"]
                is_correctness = name.endswith(" (correctness)")
                declaration = name.rsplit(" (", 1)[0]
                leaf = re.split(r"[.:/]", declaration)[-1]
                negative = is_correctness and leaf.startswith("control_")
                diagnostic = source.name.startswith("ablation")
                vcs = scope.get("vcResults", [])
                locations = {
                    (Path(assertion.get("filename", "")).name, assertion.get("line"))
                    for vc in vcs for assertion in vc.get("assertions", [])
                }
                own_failures = [f for f in failures if (f["file"], f["line"]) in locations]
                vc_outcomes = [v.get("outcome") for v in vcs]
                if negative:
                    expected = "Errors/Invalid at assert false only"
                    matched = (scope.get("outcome") == "Errors" and "Invalid" in vc_outcomes
                               and all(v in ("Valid", "Invalid") for v in vc_outcomes)
                               and scope.get("resourceCount", args.resource_limit + 1) <= args.resource_limit
                               and bool(own_failures)
                               and all(f["assert_false"] for f in own_failures))
                else:
                    expected = "Correct"
                    matched = (scope.get("outcome") == "Correct"
                               and all(v == "Valid" for v in vc_outcomes))
                row = {
                    "invocation": tag, "file": source.name,
                    "additional_axioms": additional, "heap_free_lambda_facts": heap_free,
                    "refresh": args.refresh, "scope": name,
                    "outcome": scope.get("outcome"), "ru": scope.get("resourceCount"),
                    "max_vc_ru": max((v.get("resourceCount", 0) for v in vcs), default=0),
                    "vc_count": len(vcs),
                    "vc_outcomes": ";".join(str(v) for v in vc_outcomes),
                    "expected": "diagnostic" if diagnostic else expected,
                    "matched": None if diagnostic else matched,
                    "failure_locations": ";".join(
                        f'{f["file"]}:{f["line"]}:{f["column"]}' for f in own_failures),
                }
                rows_for_invocation.append(row)
                scope_rows.append(row)
            diagnostic = source.name.startswith("ablation")
            unmapped_failures = [f for f in failures if not any(
                f'{f["file"]}:{f["line"]}:{f["column"]}' in r["failure_locations"].split(";")
                for r in rows_for_invocation)]
            entry = {
                "name": tag, "file": source.name, "command": portable_command,
                "source_sha256": source_hashes[source.name], "status": status,
                "exit": exit_code, "diagnostic_only": diagnostic,
                "scope_count": len(rows_for_invocation),
                "unexpected_scope_count": sum(r["matched"] is False for r in rows_for_invocation),
                "failures": failures, "unmapped_failures": unmapped_failures,
                "log_error": log_error,
            }
            write_json(output_dir / (tag + ".command.json"), entry)
            invocation_rows.append(entry)
            print(tag, status, "scopes=" + str(len(rows_for_invocation)),
                  "unexpected=" + str(entry["unexpected_scope_count"]), flush=True)

    fields = ["invocation", "file", "additional_axioms", "heap_free_lambda_facts", "refresh",
              "scope", "outcome", "ru", "max_vc_ru", "vc_count", "vc_outcomes", "expected", "matched", "failure_locations"]
    with (output_dir / "summary.csv").open("w", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(scope_rows)
    summary = {
        "schema": SCHEMA, "source_commit": args.source_commit,
        "invocations": invocation_rows, "scope_count": len(scope_rows),
        "unexpected_scope_count": sum(r["matched"] is False for r in scope_rows),
        "tool_or_log_failures": sum(r["status"] != "completed" for r in invocation_rows),
        "unmapped_failure_count": sum(len(r["unmapped_failures"]) for r in invocation_rows
                                      if not r["diagnostic_only"]),
        "no_sources_selected": not sources,
        "diagnostic_scope_count": sum(r["expected"] == "diagnostic" for r in scope_rows),
        "meaning": "Expected controls are Dafny Errors/Boogie Invalid at assert false, with no resource failure and a scope total within the limit. This is operational rejection, not necessarily solver SAT: incomplete quantifiers can produce the same verdict. See the separate finite consistency model. This runner does not approve an axiom or change suite expectations.",
    }
    write_json(output_dir / "summary.json", summary)
    print(json.dumps({k: v for k, v in summary.items() if k != "invocations"}, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main())
