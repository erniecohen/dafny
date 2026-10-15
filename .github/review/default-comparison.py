"""Scratch evidence only: immutable baseline/candidate builds, A1/B1/B2/A2.

No verdict baseline is updated. Raw output, invocations and per-VC logs survive
both differences and expected verifier failures. A missing/incomplete pass is
reported as such, never as resource preservation.
"""
import hashlib
import json
import re
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


PARTS = {'Std'} | {'TargetSpecific-' + target for target in ['notarget', 'cs', 'java', 'js', 'go', 'py']}
VC_OUTCOMES = {'Valid': 'Correct', 'Bounded': 'Correct', 'Invalid': 'Errors',
               'Undetermined': 'Inconclusive', 'OutOfMemory': 'OutOfMemory',
               'TimeOut': 'TimedOut', 'OutOfResource': 'OutOfResource'}
ERRLINE = re.compile(r'^(\S[^\n]*?\(\d+,\d+\)): (?:Error|Verification out of resource|Verification of .* timed out)', re.M)


def require(condition, message):
    if not condition:
        raise ValueError('Incomplete measurement: ' + message)


def merge_outcomes(current, new):
    # Match JsonVerificationLogger.MergeOutcomes, including emitted VC order.
    if new == 'Valid':
        return current
    if new == 'Invalid':
        return 'Errors'
    if new in ['OutOfMemory', 'TimeOut', 'OutOfResource']:
        return current if current in ['Errors', 'Inconclusive'] else VC_OUTCOMES[new]
    if new == 'Undetermined':
        return current if current == 'Errors' else 'Inconclusive'
    raise ValueError('Unrecognized JSON producer outcome: ' + str(new))


def population(declarations):
    return {'declarations': len(declarations), 'vcs': sum(declarations.values()),
            'scope_vc_counts_sha256': hashlib.sha256(json.dumps(
                sorted(declarations.items()), separators=(',', ':')).encode()).hexdigest()}


def vector(directory, inventory):
    require(set(inventory['parts']) == PARTS, 'authoritative inventory must name the exact seven parts')
    paths = {path.name.removesuffix('.invocation.json'): path
             for path in (directory / 'measurements').glob('*.invocation.json')}
    require(set(paths) == PARTS, 'expected the exact seven source-library parts')
    rows = {}
    for line in (directory / 'std-verdicts.tsv').read_text().splitlines():
        row = line.split('\t')
        require(row[0] not in rows, 'duplicate TSV row: ' + row[0])
        require(len(row) == (5 if row[0].startswith('run ') else 6), 'malformed TSV row: ' + row[0])
        rows[row[0]] = row
    require({key for key in rows if key.startswith('run ')} == {'run ' + part for part in PARTS},
            'TSV must name the exact seven run parts')
    declarations, vcs, runs = {}, {}, {}
    for part, path in sorted(paths.items()):
        invocation = json.loads(path.read_text())
        require(invocation['exit'] in ['0', '4'], part + ': non-verification process exit ' + str(invocation['exit']))
        require(invocation['verification_log_present'], part + ': missing proof log')
        log = path.with_name(part + '.verification.json')
        raw = json.loads(log.read_text())['verificationResults']
        counts, valid, outcomes_for_part = {}, 0, []
        failed = False
        for declaration in raw:
            name = declaration['name']
            key = part + ' ' + name
            require(key not in declarations, 'duplicate declaration: ' + key)
            proofs = declaration['vcResults']
            require(proofs, 'no VCs: ' + key)
            counts[name] = len(proofs)
            expected_outcome = 'Correct'
            for proof in proofs:
                vc = key + '\t' + str(proof['vcNum']) + '\t' + str(proof.get('randomSeed', 0))
                require(vc not in vcs, 'duplicate VC: ' + vc)
                require(proof['outcome'] in VC_OUTCOMES, 'unrecognized VC outcome: ' + vc)
                require(type(proof['resourceCount']) is int and proof['resourceCount'] >= 0, 'invalid VC cost: ' + vc)
                outcome = VC_OUTCOMES[proof['outcome']]
                expected_outcome = merge_outcomes(expected_outcome, proof['outcome'])
                outcomes_for_part.append(proof['outcome'])
                valid += outcome == 'Correct'
                failed |= outcome != 'Correct'
                vcs[vc] = [proof['outcome'], proof['resourceCount'], proof['assertions']]
            resources = sum(proof['resourceCount'] for proof in proofs)
            require(declaration['outcome'] == expected_outcome and declaration['resourceCount'] == resources,
                    'declaration disagrees with its VCs: ' + key)
            require(rows.get(key, [])[1:] == [expected_outcome, '', '', str(resources), str(len(proofs))],
                    'declaration TSV disagrees with raw proof log: ' + key)
            declarations[key] = [expected_outcome, resources, len(proofs)]
        require(population(counts) == inventory['parts'][part], part + ': declaration/VC inventory differs from authoritative baseline')
        require(invocation['exit'] == ('4' if failed else '0'), part + ': process exit disagrees with verification outcomes')
        stdout = path.with_name(part + '.stdout').read_text()
        stderr = path.with_name(part + '.stderr').read_text()
        summaries = re.findall(r'verifier finished with ([^\n]*)', stdout + stderr)
        summary = re.fullmatch(r'(\d+) verified, (\d+) errors?(.*)', summaries[0]) if len(summaries) == 1 else None
        require(summary is not None and int(summary[1]) == valid, part + ': absent or inconsistent verification summary')
        invalid = outcomes_for_part.count('Invalid')
        require(int(summary[2]) >= invalid if invalid else int(summary[2]) == 0, part + ': inconsistent error count')
        tail = ''
        for outcome, label, plural in [('Undetermined', 'inconclusive', True), ('TimeOut', 'time out', True),
                                       ('OutOfMemory', 'out of memory', False), ('OutOfResource', 'out of resource', False)]:
            count = outcomes_for_part.count(outcome)
            if count:
                tail += ', ' + str(count) + ' ' + label + ('s' if plural and count != 1 else '')
        require(summary[3] == tail, part + ': inconsistent verification failure summary')
        verdict = [invocation['exit'], summaries[0], ' '.join(sorted(set(ERRLINE.findall(stdout + stderr))))]
        require(rows['run ' + part][1:4] == verdict, part + ': run TSV disagrees with invocation/diagnostics')
        runs['run ' + part] = verdict
    require(set(rows) == set(runs) | set(declarations), 'unexpected or missing TSV inventory rows')
    return {'runs': runs, 'declarations': declarations, 'vcs': vcs}


def validated_vector(directory, inventory, output, solver, name, runner_exit):
    try:
        require(runner_exit == 0, 'source-library runner failed; see retained diagnostics')
        result = vector(directory, inventory)
    except Exception as error:
        failure = {'status': 'incomplete', 'solver': solver, 'pass': name, 'error': str(error)}
        (directory / 'validation.json').write_text(json.dumps(failure, indent=2) + '\n')
        (output / 'comparison-status.json').write_text(json.dumps(failure, indent=2) + '\n')
        raise
    (directory / 'validation.json').write_text(json.dumps({'status': 'complete-inventory'}) + '\n')
    return result


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
    inventory = json.loads(Path(__file__).with_name('default-comparison-inventory.json').read_text())
    require(inventory['baseline_source'] == identities['A']['source'] and
            inventory['library_tree'] == identities['A']['library_tree'], 'authoritative inventory source identity differs')
    status = output / 'comparison-status.json'
    require(not (output / 'summary.json').exists(), 'refusing to reuse an earlier summary')
    status.write_text(json.dumps({'status': 'running'}) + '\n')
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
            vectors[name] = validated_vector(directory, inventory, output, solver_name, name, process.returncode)
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
    (output / "summary.json").write_text(json.dumps({'status': 'complete-inventory-comparison', 'solvers': summary}, indent=2) + "\n")
    status.write_text(json.dumps({'status': 'complete-inventory-comparison'}) + '\n')
    # A probe records differences; it does not waive acceptance or update baselines.
    return 0


if __name__ == "__main__":
    sys.exit(main())
