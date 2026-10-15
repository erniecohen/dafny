"""Scratch evidence only: immutable baseline/candidate builds, A1/B1/B2/A2.

No verdict baseline is updated. Raw output, invocations and per-VC logs survive
both differences and expected verifier failures. A missing/incomplete pass is
reported as such, never as resource preservation.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(source, *args):
    return subprocess.check_output(["git", "-C", str(source), *args], text=True).strip()


def manifest(directory):
    return {str(path.relative_to(directory)): sha(path)
            for path in sorted(directory.rglob("*")) if path.is_file()}


def vector(directory):
    declarations, vcs, runs = {}, {}, {}
    for path in sorted((directory / "measurements").glob("*.invocation.json")):
        part = path.name.removesuffix(".invocation.json")
        invocation = json.loads(path.read_text())
        runs[part] = invocation["exit"]
        if invocation["exit"] == "TIMEOUT" or not invocation["verification_log_present"]:
            raise ValueError("Incomplete measurement: " + str(path))
        log = path.with_name(part + ".verification.json")
        for declaration in json.loads(log.read_text())["verificationResults"]:
            key = part + "\t" + declaration["name"]
            if key in declarations:
                raise ValueError("Duplicate declaration: " + key)
            proofs = declaration["vcResults"]
            declarations[key] = [declaration["outcome"], declaration["resourceCount"], len(proofs)]
            for proof in proofs:
                vc = key + "\t" + str(proof["vcNum"]) + "\t" + str(proof.get("randomSeed", 0))
                if vc in vcs:
                    raise ValueError("Duplicate VC: " + vc)
                vcs[vc] = [proof["outcome"], proof["resourceCount"], proof["assertions"]]
    if len(runs) != 7:
        raise ValueError("Expected all seven source-library parts")
    # Preserve the run verdict's summary/error positions as well as each exit.
    run_verdicts = {row.split("\t")[0]: row.split("\t")[1:4]
                    for row in (directory / "std-verdicts.tsv").read_text().splitlines()
                    if row.startswith("run ")}
    return {"runs": run_verdicts, "declarations": declarations, "vcs": vcs}


def differences(before, after):
    return {key: {"before": before.get(key), "after": after.get(key)}
            for key in sorted(set(before) | set(after)) if before.get(key) != after.get(key)}


def main():
    baseline, candidate, output, solver51, solver_harness = map(Path, sys.argv[1:])
    sources = {"A": baseline, "B": candidate}
    builds = {"A": output / "build-baseline", "B": output / "build-candidate"}
    identities = {}
    for label, source in sources.items():
        assert not git(source, "status", "--porcelain", "--untracked-files=no", "--", "Source/DafnyStandardLibraries")
        packages = json.loads((source / "Source/Dafny/obj/project.assets.json").read_text())["libraries"]
        identities[label] = {
            "source": git(source, "rev-parse", "HEAD"),
            "tree": git(source, "rev-parse", "HEAD^{tree}"),
            "library_tree": git(source, "rev-parse", "HEAD:Source/DafnyStandardLibraries"),
            "library_files": {name: sha(source / name) for name in git(source, "ls-files", "Source/DafnyStandardLibraries").splitlines()},
            "packages": {key: value.get("sha512") for key, value in packages.items() if value["type"] == "package"},
            "build_files": manifest(builds[label]),
            "reported_version": subprocess.check_output([str(builds[label] / "Dafny"), "--version"], text=True).strip(),
        }
    (output / "identities.json").write_text(json.dumps(identities, indent=2) + "\n")
    assert identities["A"]["source"] == "4e98f3f592310854ca1448d517b82dcbcc0f51f4"
    for field in ["library_tree", "library_files", "packages", "reported_version"]:
        assert identities["A"][field] == identities["B"][field], field
    protected = {name: digest for name, digest in identities["A"]["build_files"].items()
                 if (name.startswith("Boogie.") and name.endswith(".dll"))
                 or name.endswith((".doo", ".deps.json", ".runtimeconfig.json", "DafnyPrelude.bpl"))}
    assert protected
    assert all(identities["B"]["build_files"].get(name) == digest for name, digest in protected.items()), "Proof dependencies differ"
    results = {}
    for solver_name, solver in [("5.1.0", solver51), ("4.16.0", solver_harness)]:
        solver_version = subprocess.check_output([str(solver), "--version"], text=True).strip()
        assert "Z3 version " + solver_name in solver_version
        vectors = {}
        for name in ["A1", "B1", "B2", "A2"]:
            directory = output / ("z3-" + solver_name) / name
            directory.mkdir(parents=True)
            with (directory / "runner.stdout").open("w") as stdout, (directory / "runner.stderr").open("w") as stderr:
                invocation = [sys.executable, str(candidate / ".github/review/std-verdicts.py"), "run",
                              str(candidate / "Source/DafnyStandardLibraries"), str(builds[name[0]] / "Dafny"),
                              str(solver), str(directory / "std-verdicts.tsv"), "--cores", "4",
                              "--measurements", str(directory / "measurements")]
                process = subprocess.run(invocation, stdout=stdout, stderr=stderr)
            (directory / "runner-invocation.json").write_text(json.dumps({"command": invocation, "exit": process.returncode}, indent=2) + "\n")
            assert process.returncode == 0, "Source-library runner failed; see retained diagnostics"
            vectors[name] = vector(directory)
            (directory / "vectors.json").write_text(json.dumps(vectors[name], indent=2) + "\n")
        pairs = {}
        # Inspect same-binary repetitions before the cross-build pairs.
        for left, right in [("A1", "A2"), ("B1", "B2"), ("A1", "B1"), ("A1", "B2"), ("A2", "B1"), ("A2", "B2")]:
            delta = {kind: differences(vectors[left][kind], vectors[right][kind]) for kind in vectors[left]}
            pairs[left + ":" + right] = {"counts": {kind: len(value) for kind, value in delta.items()}, "differences": delta}
        results[solver_name] = {"solver_sha256": sha(solver), "solver_version": solver_version,
                               "denominators": {name: {kind: len(value) for kind, value in vec.items()} for name, vec in vectors.items()},
                               "pairs": pairs}
        (output / ("comparison-z3-" + solver_name + ".json")).write_text(json.dumps(results[solver_name], indent=2) + "\n")
    summary = {solver: {"denominators": value["denominators"], "pairs": {pair: record["counts"] for pair, record in value["pairs"].items()}}
               for solver, value in results.items()}
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    # A probe records differences; it does not waive acceptance or update baselines.
    return 0


if __name__ == "__main__":
    sys.exit(main())
