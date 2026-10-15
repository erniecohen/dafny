"""Scratch-only issue 97 diagnostics through the existing diagnostic entry."""
from pathlib import Path
import os
import runpy
import subprocess
import traceback

out = Path("issue75-probe")
out.mkdir(exist_ok=True)
if os.environ.get("GITHUB_REF_NAME", "").rsplit("/", 1)[-1].startswith(("suite-", "std-")):
    runpy.run_path(".github/review/issue97-compatibility.py", run_name="__main__")
    raise SystemExit(0)

try:
    os.environ["Z3"] = str(Path("Binaries/z3/bin/z3-5.1.0").resolve())
    for source in ["4e98f3f592310854ca1448d517b82dcbcc0f51f4", "HEAD"]:
        source = subprocess.check_output(["git", "rev-parse", source], text=True).strip() if source == "HEAD" else source
        subprocess.run(["git", "fetch", "--no-tags", "--depth=1", "origin", source], check=True)
        subprocess.run(["git", "worktree", "add", "--detach", "compiler", source], check=True)
        os.environ["ISSUE97_COMPILER_SOURCE"] = source
        runpy.run_path(".github/review/issue97-probe.py", run_name="__main__")
        subprocess.run(["git", "worktree", "remove", "--force", "compiler"], check=True)
except Exception:
    (out / "driver-exception.txt").write_text(traceback.format_exc())

# Exercise the permanent driver with the repository's own harness at this exact head.
try:
    subprocess.run(["sh", "Scripts/fetch-boogie-packages.sh"], check=True)
    environment = dict(os.environ, DAFNY_INTEGRATION_TESTS_ONLY_COMPILERS="cs")
    result = subprocess.run(["dotnet", "test", "Source/IntegrationTests", "-c", "Release",
        "--filter", "DisplayName~git-issue-6110.dfy", "--logger", "trx;LogFileName=issue97.trx",
        "--results-directory", str((out / "registered").resolve())], env=environment,
        text=True, capture_output=True, timeout=1800)
    (out / "registered-output.txt").write_text(result.stdout + result.stderr)
    (out / "registered-exit.json").write_text(str(result.returncode) + "\n")
except Exception:
    (out / "registered-exception.txt").write_text(traceback.format_exc())
