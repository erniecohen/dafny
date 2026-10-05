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
    recipe = spec.get("composition_patches", [{
        "file": "baseline/" + line + "-core.patch",
        "sha256": spec["patch_sha256"], "scope": "Core"
    }])
    if not recipe or recipe[0] != {
        "file": "baseline/" + line + "-core.patch",
        "sha256": spec["patch_sha256"], "scope": "Core"
    }:
        raise ValueError("Composition must start with the unchanged audited Core patch")
    if len(recipe) > 2 or (len(recipe) == 2 and recipe[1]["scope"] != "driver-run-build-104"):
        raise ValueError("Composition exceeds the separately audited prerequisite scopes")
    patch_receipts = []
    for entry in recipe:
        patch = (HERE / entry["file"]).resolve()
        if not patch.is_relative_to((HERE / "baseline").resolve()) or digest(patch) != entry["sha256"]:
            raise ValueError("Audited source patch location/checksum changed")
        # Inspect every patch's exact paths before staging any of its contents.
        stats = subprocess.check_output(["git", "-C", str(checkout), "apply", "--numstat", str(patch)]).decode()
        paths = [row.split("\t", 2)[2] for row in stats.splitlines()]
        permitted = (all(name.startswith("Source/DafnyCore/") for name in paths)
                     if entry["scope"] == "Core" else
                     paths == ["Source/DafnyDriver/Commands/RunCommand.cs"])
        if not paths or not permitted:
            raise ValueError("Baseline patch exceeds its independently audited scope")
        subprocess.run(["git", "-C", str(checkout), "apply", "--index", str(patch)], check=True)
        patch_receipts.append({**entry, "changes": paths})
    changed = git(checkout, "diff", "--cached", "--name-only").splitlines()
    tree = git(checkout, "write-tree")
    if tree != spec["repaired_tree"]:
        raise ValueError("Composed tree does not match the audited repaired baseline")
    core = git(checkout, "rev-parse", tree + ":Source/DafnyCore")
    if core != spec["repaired_core_tree"]:
        raise ValueError("Composed Core tree differs")
    driver = git(checkout, "rev-parse", tree + ":Source/DafnyDriver")
    if len(recipe) > 1 and driver != spec.get("repaired_driver_tree"):
        raise ValueError("Composed Driver tree differs from the independently audited #104 port")
    return {"original_source": spec["original"], "repaired_product": spec["repaired_product"],
            "composed_tree": tree, "core_tree": core, "driver_tree": driver,
            "patch_sha256": spec["patch_sha256"], "composition_patches": patch_receipts,
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
    parser.add_argument("--side", choices=("baseline", "final"))
    parser.add_argument("--archive", type=Path)
    args = parser.parse_args()
    if args.command == "identity":
        spec = json.loads((HERE / "spec.json").read_text())["lines"][args.line]
        if not args.side or not args.archive:
            parser.error("identity needs --side and --archive")
        result = {"line": args.line, "side": args.side,
                  "product_revision": spec["repaired_product" if args.side == "baseline" else "final_product"],
                  "archive_sha256": digest(args.archive), "components_sha256": components(args.path),
                  "bundled_libraries_sha256": {file.relative_to(args.path.resolve().parent).as_posix(): digest(file)
                                               for file in sorted(args.path.resolve().parent.rglob("*.doo"))},
                  "source_model": "original public tree + audited ordered source recipe" if args.side == "baseline" else "exact public product commit",
                  "composition_patches": spec.get("composition_patches", [{"file": "baseline/" + args.line + "-core.patch", "sha256": spec["patch_sha256"], "scope": "Core"}]) if args.side == "baseline" else []}
    else:
        result = (inputs if args.command == "inputs" else compose)(args.line, args.path.resolve())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result if args.command != "identity" else {"components": len(result["components_sha256"])}))


if __name__ == "__main__":
    main()
