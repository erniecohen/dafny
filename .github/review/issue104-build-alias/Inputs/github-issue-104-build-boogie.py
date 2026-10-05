"""Check the existing run --build contract with and without nonempty --boogie options."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

assembly = Path(sys.argv[1]).resolve()
root = Path(sys.argv[2]).resolve()
if root.exists():
    shutil.rmtree(root)
root.mkdir(parents=True)
release = os.environ.get("DAFNY_RELEASE")
launcher = [str(Path(release) / "dafny")] if release else ["dotnet", str(assembly)]
records = []
failures = []

for refresh in ("false", "true"):
    for build in (False, True):
        for boogie in (False, True):
            for runtime in (True, False):
                name = ("refresh-" + refresh + "-build-" + str(build).lower() +
                        "-boogie-" + str(boogie).lower() + "-runtime-" + str(runtime).lower())
                work = root / name
                source_dir = work / "source"
                target_dir = work / "generated"
                source_dir.mkdir(parents=True)
                target_dir.mkdir()
                source = source_dir / "input.dfy"
                source.write_text('datatype Pair<T> = Pair(left: T, right: T)\n'
                                  'method Main() { var p: Pair<int> := Pair(20, 22); '
                                  'print p.left + p.right, "\\n"; }\n')
                args = launcher + ["run", str(source), "--no-verify", "--target:cs",
                                   "--type-system-refresh:" + refresh, "--cores:1",
                                   "--show-snippets:false"]
                stem = target_dir / "chosen" if build else source_dir / "input"
                if build:
                    args.append("--build:" + str(stem))
                if boogie:
                    args += ["--boogie", "/normalizeDeclarationOrder:0"]
                if not runtime:
                    runtime_path = assembly.parent / "DafnyRuntime.dll"
                    if release:
                        runtime_path = Path(release) / "DafnyRuntime.dll"
                    args += ["--include-runtime:false", "--input", str(runtime_path)]
                result = subprocess.run(args, cwd=work, capture_output=True, text=True, timeout=120)
                files = []
                for p in sorted(work.rglob("*")):
                    if p.is_file():
                        files.append({"path": str(p.relative_to(work)).replace(os.sep, "/"),
                                      "bytes": p.stat().st_size,
                                      "sha256": hashlib.sha256(p.read_bytes()).hexdigest()})
                errors = []
                if result.returncode != 0:
                    errors.append("exit " + str(result.returncode))
                if "42\n" not in result.stdout.replace("\r\n", "\n"):
                    errors.append("Main did not print 42")
                for suffix in (".cs", ".csproj", ".dll", ".deps.json"):
                    if not Path(str(stem) + suffix).is_file():
                        errors.append("missing requested output " + suffix)
                if build and any((source_dir / ("input" + suffix)).exists()
                                 for suffix in (".cs", ".csproj", ".dll", ".deps.json")):
                    errors.append("generated output beside input instead of requested --build path")
                records.append({"name": name, "argv": args, "cwd": str(work),
                                "exit": result.returncode, "stdout": result.stdout,
                                "stderr": result.stderr, "files": files, "errors": errors})
                if errors:
                    failures.append(name)
                    print("FAIL " + name + ": " + "; ".join(errors))
                else:
                    print("PASS " + name)
(root / "results.json").write_text(json.dumps(records, indent=2) + "\n")
if failures:
    for record in records:
        if record["errors"]:
            print(record["name"] + "\n" + record["stdout"] + record["stderr"], file=sys.stderr)
    sys.exit(1)
