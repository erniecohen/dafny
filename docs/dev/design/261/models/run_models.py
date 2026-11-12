#!/usr/bin/env python3
"""Run all issue-261 standard-library models; no Dafny checkout is required."""
from pathlib import Path
import re
import subprocess
import sys


def main():
    root = Path(__file__).resolve().parent
    total = 0
    for name in ("model_task_keys.py", "model_module_dag.py", "model_naming.py", "model_storage.py"):
        run = subprocess.run([sys.executable, str(root / name)], capture_output=True, text=True)
        output = run.stdout + run.stderr
        if run.returncode:
            print(output, end="")
            return run.returncode
        count = re.search(r"Ran (\d+) tests? in", output)
        if count is None or re.search(r"\nOK\s*(?:\n|$)", output) is None:
            print(f"Unrecognized test report for {name}:\n{output}")
            return 1
        passed = int(count[1])
        total += passed
        print(f"{name}: {passed} tests passed")
    print(f"TOTAL: {total} tests passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
