"""Diagnostic only: preserve actual build/proof outcomes and exit successfully."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import traceback

out = Path("issue75-probe") / os.environ["ISSUE97_COMPILER_SOURCE"][:8]
out.mkdir(parents=True, exist_ok=True)
records = []


def run(name, command, timeout=180, cwd=None):
    folder = out / name
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "command.json").write_text(json.dumps(command, indent=2) + "\n")
    try:
        result = subprocess.run(command, capture_output=True, text=True, timeout=timeout, cwd=cwd)
        code, output = result.returncode, result.stdout + result.stderr
    except subprocess.TimeoutExpired as error:
        code, output = "TIMEOUT", str(error)
    (folder / "output.txt").write_text(output)
    row = {"name": name, "exit": code}
    records.append(row)
    print(json.dumps(row), flush=True)
    return code


cases = {
    "original": "predicate p(x:int)\nconst f := (x:int) requires p(x) => x\nlemma test(x:int) ensures f.requires(x) ==> p(x) {}\n",
    "forward": "predicate p(x:int)\nconst f := (x:int) requires p(x) => x\nlemma test(x:int) ensures p(x) ==> f.requires(x) {}\n",
    "concrete-reverse": "const f := (x:int) requires x > 0 => x\nlemma test(x:int) ensures f.requires(x) ==> x > 0 {}\n",
    "declared-false": "const f := (x:int) requires false => x\nlemma test(x:int) ensures !f.requires(x) {}\n",
    "inhabited-vacuity": "const f := (x:int) requires x > 0 => x\nlemma test() { assert f.requires(1); assert false; }\n",
    "false-range-call": "const f := (x:int) requires x > 0 => x\nlemma test() { assert f(0) == 0; }\n",
    "local-reverse": "lemma test(x:int) { var f := (y:int) requires y > 0 => y; assert f.requires(x) ==> x > 0; }\n",
    "named-control": "function f(x:int): int requires x > 0 { x }\nlemma test(x:int) ensures f.requires(x) ==> x > 0 {}\n",
}
for name in ["positive", "negative"]:
    cases["coverage-" + name] = Path("Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-97-" + name + ".dfy").read_text()

try:
    compiler = Path("compiler").resolve()
    source = subprocess.check_output(["git", "-C", str(compiler), "rev-parse", "HEAD"], text=True).strip()
    product = Path("out/issue97-compiler-" + source[:8]).resolve()
    if run("boogie", ["sh", "Scripts/fetch-boogie-packages.sh"], 300, cwd=compiler) != 0:
        raise RuntimeError("Boogie package acquisition failed")
    code = run("build", ["dotnet", "publish", str(compiler / "Source/Dafny/Dafny.csproj"),
        "-c", "Release", "-o", str(product), "-p:GeneratePackageOnBuild=false",
        "-p:SourceRevisionId=" + source[:8]], 1800)
    if code != 0:
        raise RuntimeError("Compiler build failed; no proofs attempted")
    solver = str(Path(os.environ["Z3"]).resolve())
    dafny = ["dotnet", str(product / "Dafny.dll")]
    identity = {"source": source, "probe_source": os.environ.get("GITHUB_SHA"),
        "dafny": subprocess.check_output(dafny + ["--version"], text=True).strip(),
        "solver": subprocess.check_output([solver, "--version"], text=True).strip(),
        "core_sha256": hashlib.sha256((product / "DafnyCore.dll").read_bytes()).hexdigest(),
        "solver_sha256": hashlib.sha256(Path(solver).read_bytes()).hexdigest()}
    (out / "identity.json").write_text(json.dumps(identity, indent=2) + "\n")
    for name, text in cases.items():
        fixture = out / (name + ".dfy")
        fixture.write_text(text)
        for refresh in ["false", "true"]:
            for axioms in ["false", "true"]:
                label = name + "-refresh-" + refresh + "-axioms-" + axioms
                folder = out / label
                folder.mkdir(exist_ok=True)
                command = dafny + ["verify", str(fixture.resolve()), "--solver-path=" + solver,
                    "--resource-limit=16000000", "--verification-time-limit=30", "--cores=1",
                    "--type-system-refresh=" + refresh, "--additional-axioms=" + axioms,
                    "--boogie=/normalizeDeclarationOrder:0", "--allow-warnings=false", "--allow-axioms",
                    "--bprint=" + str((folder / "program.bpl").resolve()),
                    "--log-format=csv;LogFileName=" + str((folder / "resources.csv").resolve())]
                if refresh == "false":
                    command += ["--general-newtypes=false"]
                run(label, command)
except Exception:
    (out / "exception.txt").write_text(traceback.format_exc())
finally:
    summary = {"diagnostic_only": True, "phases": records}
    (out / "results.json").write_text(json.dumps(summary, indent=2) + "\n")
    text = "## Issue 97 diagnostic only\n\nWrapper success is not proof acceptance.\n\n" + "".join(
        "- " + row["name"] + ": actual exit " + str(row["exit"]) + "\n" for row in records)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        Path(os.environ["GITHUB_STEP_SUMMARY"]).write_text(text)
