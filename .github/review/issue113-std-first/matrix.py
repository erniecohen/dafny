#!/usr/bin/env python3
"""Pure focused workload selection; no compiler, solver or build is invoked."""
import argparse
import json
import os
from pathlib import Path


def workload():
    matrix, groups = [], []
    for solver in ("5.1.0", "reference"):
        groups.append({"line": "dev", "cohort": "std", "mode": "project",
                       "axioms": "absent", "solver": solver})
        for part in ("Std", "TargetSpecific-notarget", "TargetSpecific-cs", "TargetSpecific-java",
                     "TargetSpecific-js", "TargetSpecific-go", "TargetSpecific-py"):
            matrix.append({"line": "dev", "cohort": "std", "mode": "project", "axioms": "absent",
                           "solver": solver, "shard": "0/1", "part": part, "jobs": 1,
                           "tag": "dev-std-" + solver + "-" + part})
    return {"schema_version": 1, "matrix": matrix, "groups": groups,
            "arms": ["original", "baseline", "final"], "compiler_builds": 3,
            "verification_requests": 42, "primary_comparison": "repaired baseline/final",
            "boundary": "Fourteen old canonical development Std jobs, both original solvers; original arm is context. No suite, global refresh or Unicode cohort is requested."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--github-output", action="store_true")
    args = parser.parse_args()
    result = workload()
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    if args.github_output:
        with open(os.environ["GITHUB_OUTPUT"], "a") as stream:
            stream.write("matrix=" + json.dumps({"include": result["matrix"]}, separators=(",", ":")) + "\n")
    print(json.dumps({"matched_jobs": len(result["matrix"]), "denominator_groups": len(result["groups"]),
                      "compiler_builds": 3, "verification_requests": 42}))


if __name__ == "__main__":
    main()
