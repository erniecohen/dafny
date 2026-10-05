#!/usr/bin/env python3
"""Pure exact workload selection; no compiler, solver or build is invoked."""
import argparse
import json
import os
from pathlib import Path
from plan import HERE


def workload(refresh=False, semantic_unicode=False):
    matrix, groups = [], []
    for line in ("dev", "shipped"):
        axes = ("absent",) if line == "dev" else ("off", "on")
        for mode in (("default", "refresh") if refresh else ("default",)):
            for axioms in axes:
                groups.append({"line": line, "cohort": "canonical", "mode": mode, "axioms": axioms, "solver": "5.1.0"})
                for shard in range(4):
                    matrix.append({"line": line, "cohort": "canonical", "mode": mode, "axioms": axioms,
                                   "solver": "5.1.0", "shard": str(shard) + "/4", "part": "", "jobs": 4,
                                   "tag": line + "-canonical-" + mode + "-ax-" + axioms + "-" + str(shard)})
            if semantic_unicode:
                axioms = axes[0]
                groups.append({"line": line, "cohort": "semantic-unicode", "mode": mode, "axioms": axioms, "solver": "5.1.0"})
                matrix.append({"line": line, "cohort": "semantic-unicode", "mode": mode, "axioms": axioms,
                               "solver": "5.1.0", "shard": "0/1", "part": "", "jobs": 4,
                               "tag": line + "-semantic-unicode-" + mode})
        for solver in ("5.1.0", "reference"):
            for axioms in axes:
                groups.append({"line": line, "cohort": "std", "mode": "project", "axioms": axioms, "solver": solver})
                for part in ("Std", "TargetSpecific-notarget", "TargetSpecific-cs", "TargetSpecific-java", "TargetSpecific-js", "TargetSpecific-go", "TargetSpecific-py"):
                    matrix.append({"line": line, "cohort": "std", "mode": "project", "axioms": axioms,
                                   "solver": solver, "shard": "0/1", "part": part, "jobs": 1,
                                   "tag": line + "-std-" + solver + "-ax-" + axioms + "-" + part})
    return {"schema_version": 1, "matrix": matrix, "groups": groups,
            "global_refresh_requested": refresh, "semantic_unicode_requested": semantic_unicode,
            "boundary": "Canonical default plans exactly retain original wrappers. Extra refresh/Unicode cohorts are explicitly labelled; no original denominator is replaced."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--refresh", choices=("true", "false"), default="false")
    parser.add_argument("--semantic-unicode", choices=("true", "false"), default="false")
    parser.add_argument("--github-output", action="store_true")
    args = parser.parse_args()
    result = workload(args.refresh == "true", args.semantic_unicode == "true")
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    if args.github_output:
        with open(os.environ["GITHUB_OUTPUT"], "a") as stream:
            stream.write("matrix=" + json.dumps({"include": result["matrix"]}, separators=(",", ":")) + "\n")
    print(json.dumps({"paired_jobs": len(result["matrix"]), "denominator_groups": len(result["groups"])}))


if __name__ == "__main__":
    main()
