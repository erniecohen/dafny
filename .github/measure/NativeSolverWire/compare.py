#!/usr/bin/env python3
"""Compare passive native task observations; report physical carriers separately."""
import argparse
import collections
import json
from pathlib import Path


def records(directory, kind):
    result = collections.defaultdict(list)
    for path in directory.glob("solver-*.jsonl"):
        for line in path.read_text().splitlines():
            row = json.loads(line)
            if row.get("kind") == kind:
                result[row["problem"]].append((row["sha256"], row["length"]))
    return {key: sorted(values) for key, values in sorted(result.items())}


def untimed(value):
    if isinstance(value, list):
        return [untimed(item) for item in value]
    if isinstance(value, dict):
        result = {key: untimed(item) for key, item in value.items()
                  if key not in ("runTime", "startTime", "endTime")}
        if "vcResults" in result:
            result["vcResults"].sort(key=lambda row: (
                row.get("vcNum", 0), row.get("randomSeed", 0), json.dumps(row, sort_keys=True)))
        return result
    return value


def compare(left, right):
    metadata = [json.loads((directory / "meta.json").read_text()) for directory in (left, right)]
    tasks = [records(directory, "task-wire") for directory in (left, right)]
    physical = [records(directory, "wire-final") for directory in (left, right)]
    changed = [key for key in sorted(tasks[0].keys() | tasks[1].keys())
               if tasks[0].get(key) != tasks[1].get(key)]
    checks = {name: (left / name).read_bytes() == (right / name).read_bytes()
              for name in ("stdout", "stderr", "boogie.json")}
    checks.update(exit=metadata[0]["code"] == metadata[1]["code"],
                  taskNativeBytes=not changed,
                  completeTaskCoverage=all(sum(map(len, rows.values())) == meta["taskStarts"]
                                           for rows, meta in zip(tasks, metadata)),
                  completePhysicalCoverage=all(sum(map(len, rows.values())) == meta["solvers"]
                                               for rows, meta in zip(physical, metadata)))
    reports = [directory / "report.json" for directory in (left, right)]
    checks["reportPresence"] = reports[0].exists() == reports[1].exists()
    if all(path.exists() for path in reports):
        try:
            checks["untimedReports"] = untimed(json.loads(reports[0].read_text())) == untimed(json.loads(reports[1].read_text()))
        except json.JSONDecodeError:
            checks["reportBytes"] = reports[0].read_bytes() == reports[1].read_bytes()
    return {"checks": checks, "changedTasks": changed,
            "physicalSolvers": [meta["solvers"] for meta in metadata],
            "physicalStreamsEqual": physical[0] == physical[1]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("left", type=Path)
    parser.add_argument("right", type=Path)
    args = parser.parse_args()
    result = compare(args.left, args.right)
    print(json.dumps(result, indent=2))
    raise SystemExit(0 if all(result["checks"].values()) else 1)
