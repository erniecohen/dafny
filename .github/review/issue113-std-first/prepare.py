#!/usr/bin/env python3
"""Validate source inputs or compose an audited repaired baseline; no build/proof."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from plan import HERE, digest, rows


def git(checkout, *args):
    return subprocess.check_output(["git", "-C", str(checkout), *args]).decode().strip()


def inputs(line, checkout):
    spec = json.loads((HERE / "spec.json").read_text())["lines"][line]
    if git(checkout, "rev-parse", "HEAD") != spec["original"]:
        raise ValueError("Input checkout is not the exact original public source")
    receipt_file = HERE / "canonical" / line / "inputs.json"
    if digest(receipt_file) != spec["inputs_sha256"]:
        raise ValueError("Recorded input receipt changed")
    receipt = json.loads(receipt_file.read_text())
    for name, expected in receipt.items():
        path = checkout / name
        data = path.readlink().as_posix().encode() if expected["mode"] == "120000" else path.read_bytes()
        if hashlib.sha256(data).hexdigest() != expected["sha256"]:
            raise ValueError("Original input bytes differ: " + name)
    expected_plan = json.loads((HERE / "canonical" / line / "plan.json").read_text())
    if rows(line, checkout) != expected_plan:
        raise ValueError("Original plan/source bytes differ")
    return {"original_source": spec["original"], "original_tree": spec["original_tree"],
            "input_receipt_sha256": digest(receipt_file), "files": len(receipt),
            "plans": {kind: len(value) for kind, value in expected_plan.items()}}


def compose(line, checkout):
    spec = json.loads((HERE / "spec.json").read_text())["lines"][line]
    if git(checkout, "rev-parse", "HEAD") != spec["original"] or git(checkout, "status", "--porcelain"):
        raise ValueError("Composition needs a clean exact original checkout")
    patch = HERE / "baseline" / (line + "-core.patch")
    if digest(patch) != spec["patch_sha256"]:
        raise ValueError("Audited source patch checksum changed")
    subprocess.run(["git", "-C", str(checkout), "apply", "--index", str(patch)], check=True)
    changed = git(checkout, "diff", "--cached", "--name-only").splitlines()
    if not changed or any(not name.startswith("Source/DafnyCore/") for name in changed):
        raise ValueError("Baseline patch exceeds the independently audited Core scope")
    tree = git(checkout, "write-tree")
    if tree != spec["repaired_tree"]:
        raise ValueError("Composed tree does not match the audited repaired baseline")
    core = git(checkout, "rev-parse", tree + ":Source/DafnyCore")
    if core != spec["repaired_core_tree"]:
        raise ValueError("Composed Core tree differs")
    return {"original_source": spec["original"], "repaired_product": spec["repaired_product"],
            "composed_tree": tree, "core_tree": core, "patch_sha256": digest(patch),
            "changes": changed, "boundary": "source composition only; acceptance unmeasured"}


def components(executable):
    executable = executable.resolve()
    root = executable.parent
    required = ("DafnyCore.dll", "DafnyDriver.dll", "Dafny.dll", "Dafny.deps.json", "Dafny.runtimeconfig.json")
    if any(not (root / name).is_file() for name in required):
        raise ValueError("Published managed compiler bundle is incomplete")
    result = {file.relative_to(root).as_posix(): digest(file)
              for file in sorted(root.rglob("*")) if file.is_file() and file.suffix in (".dll", ".json")}
    result[executable.name] = digest(executable)
    return dict(sorted(result.items()))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("inputs", "compose", "identity"))
    parser.add_argument("line", choices=("dev", "shipped"))
    parser.add_argument("path", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--side", choices=("original", "baseline", "final"))
    parser.add_argument("--archive", type=Path)
    args = parser.parse_args()
    if args.command == "identity":
        spec = json.loads((HERE / "spec.json").read_text())["lines"][args.line]
        if not args.side or not args.archive:
            parser.error("identity needs --side and --archive")
        result = {"line": args.line, "side": args.side,
                  "product_revision": spec[{"original": "original", "baseline": "repaired_product", "final": "final_product"}[args.side]],
                  "archive_sha256": digest(args.archive), "components_sha256": components(args.path),
                  "bundled_libraries_sha256": {file.relative_to(args.path.resolve().parent).as_posix(): digest(file)
                                               for file in sorted(args.path.resolve().parent.rglob("*.doo"))},
                  "source_model": "original public tree + audited patch" if args.side == "baseline" else "exact public product commit"}
    else:
        result = (inputs if args.command == "inputs" else compose)(args.line, args.path.resolve())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result if args.command != "identity" else {"components": len(result["components_sha256"])}))


if __name__ == "__main__":
    main()
