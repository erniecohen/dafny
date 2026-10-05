#!/usr/bin/env python3
"""Pure source planning for matched issue-113 default-off comparisons."""
import argparse
import contextlib
import glob
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import shlex
import tempfile

HERE = Path(__file__).resolve().parent
LIT = Path("Source/IntegrationTests/TestFiles/LitTests/LitTest")
STD = Path("Source/DafnyStandardLibraries")


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def canonical(line, kind):
    file = HERE / "canonical" / line / (kind + "-verdicts.py")
    spec = importlib.util.spec_from_file_location(line + "_" + kind, file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def suite_rows(line, checkout, semantic=False):
    module = canonical(line, "lit")
    if semantic:
        # The additional cohort corrects only the planner's Unicode drop.
        module.DROP = module.re.compile(
            r"--(target|output|compile-verbose|build|no-verify|spill-translation)(=|$)")
    with tempfile.TemporaryDirectory() as directory:
        plan = Path(directory) / "plan.tsv"
        with contextlib.redirect_stdout(io.StringIO()):
            module.plan(str(checkout / LIT), str(plan))
        rows = []
        for index, line_text in enumerate(plan.read_text().splitlines()):
            path, _, opts = line_text.partition("\t")
            file = checkout / LIT / path
            rows.append({"index": index, "id": path, "args": [path],
                         "own_options": shlex.split(opts), "options_text": opts,
                         "cwd": LIT.as_posix(), "source_sha256": digest(file),
                         "first_run": file.read_text(errors="replace").splitlines()[0]})
        return rows


def fixed_options(line, own):
    # Byte-for-byte option selection algorithm used by the original runner.
    fixed_source = canonical(line, "lit").FIXED
    names = {t.split("=")[0].split(":")[0] for t in own if t.startswith("--")}
    fixed, i = [], 0
    while i < len(fixed_source):
        name = fixed_source[i]
        takes = i + 1 < len(fixed_source) and not fixed_source[i + 1].startswith("--")
        if name not in names:
            fixed += fixed_source[i:i + 2] if takes else [name]
        i += 2 if takes else 1
    return fixed


def rows(line, checkout):
    canonical_rows = suite_rows(line, checkout)
    semantic_rows = suite_rows(line, checkout, True)
    assert [r["id"] for r in canonical_rows] == [r["id"] for r in semantic_rows]
    expected = (HERE / "canonical" / line / "expected-verdicts.tsv").read_text().splitlines()
    expected_ids = {row.split("\t")[0] for row in expected if row.strip()}
    assert expected_ids == {r["id"] for r in canonical_rows}, "Original expected/plan denominator mismatch"
    extra = []
    for original, semantic in zip(canonical_rows, semantic_rows):
        if original["own_options"] != semantic["own_options"]:
            semantic["index"] = len(extra)
            semantic["canonical_index"] = original["index"]
            semantic["canonical_options"] = original["own_options"]
            extra.append(semantic)
    ts = checkout / STD / "src/Std/TargetSpecific"
    std = [{"index": 0, "id": "Std", "cwd": STD.as_posix(),
            "args": ["src/Std/dfyconfig.toml"], "own_options": []}]
    for target in canonical(line, "std").TARGETS:
        files = sorted("./" + Path(file).relative_to(ts).as_posix()
                       for file in glob.glob(str(ts / "**" / ("*-" + target + "*.dfy")), recursive=True))
        std.append({"index": len(std), "id": "TargetSpecific-" + target,
                    "cwd": (STD / "src/Std/TargetSpecific").as_posix(),
                    "args": ["dfyconfig.toml"] + files, "own_options": []})
    return {"canonical": canonical_rows, "semantic-unicode": extra, "std": std}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("line", choices=("dev", "shipped"))
    parser.add_argument("checkout", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    output = json.dumps(rows(args.line, args.checkout.resolve()), indent=2) + "\n"
    if args.check:
        if args.output.read_text() != output:
            raise SystemExit("Plan differs from its recorded original-source plan")
    else:
        args.output.write_text(output)
    print(json.dumps({k: len(v) for k, v in json.loads(output).items()}))


if __name__ == "__main__":
    main()
