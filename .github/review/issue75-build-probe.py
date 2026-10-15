"""Scratch-only issue 97 diagnostics through the existing diagnostic entry."""
from pathlib import Path
import os
import runpy
import subprocess
import traceback

out = Path("issue75-probe")
out.mkdir(exist_ok=True)
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
