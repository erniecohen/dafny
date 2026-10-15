"""Scratch evidence only: immutable baseline/candidate builds, A1/B1/B2/A2.

No verdict baseline is updated. Raw output, invocations and per-VC logs survive
both differences and expected verifier failures. A missing/incomplete pass is
reported as such, never as resource preservation.
"""
import hashlib
import os
import platform
import json
import re
import shutil
from pathlib import Path
import subprocess
import sys

import default_suite_comparison as suite


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


# Product revisions are independent of the harness checkout. Test/harness repairs
# must never silently replace the candidate being measured.
PRODUCT_A = '4e98f3f592310854ca1448d517b82dcbcc0f51f4'
PRODUCT_B = 'b85e4b6f3af7eedd39bf355394a3f50c1b348646'
LIT = 'Source/IntegrationTests/TestFiles/LitTests/LitTest'
PASSES = ['A1', 'B1', 'B2', 'A2']
PAIRS = [('A1', 'A2'), ('B1', 'B2'), ('A1', 'B1'), ('A1', 'B2'), ('A2', 'B1'), ('A2', 'B2')]


def tracked_files(source, *paths):
    return {name: sha(source / name) for name in git(source, 'ls-files', *paths).splitlines()}


def product_files(source):
    # REVIEW.md's product boundary; integration tests and harness files are not
    # compiled into the shipped product.
    excluded = {'REVIEW.md', 'ROADMAP.md', 'AGENTS.md', 'CLAUDE.md'}
    return {name: digest for name, digest in tracked_files(source).items()
            if name not in excluded and not name.startswith(('Source/IntegrationTests/', 'docs/', '.github/'))}


def std_commands(directory, candidate, dafny, solver):
    lib = candidate / 'Source/DafnyStandardLibraries'
    target = lib / 'src/Std/TargetSpecific'
    commands = {}
    for part in sorted(PARTS):
        invocation = json.loads((directory / 'measurements' / (part + '.invocation.json')).read_text())
        cwd = lib if part == 'Std' else target
        args = ['src/Std/dfyconfig.toml'] if part == 'Std' else ['dfyconfig.toml'] + sorted(
            './' + path.relative_to(target).as_posix() for path in target.rglob('*-' + part.removeprefix('TargetSpecific-') + '*.dfy'))
        command = invocation['command']
        log = command[-1]
        require(isinstance(log, str) and log.startswith('json;LogFileName=') and log != 'json;LogFileName=',
                part + ': missing observation filename')
        expected = [str(dafny), 'verify'] + args + ['--solver-path', str(solver), '--cores', '4',
                    '--verification-time-limit', '0', '--log-format', log]
        require(invocation['cwd'] == str(cwd) and command == expected,
                part + ': actual command/cwd differs from original native proof options')
        canonical = command.copy()
        canonical[0], canonical[canonical.index('--solver-path') + 1], canonical[-1] = '<dafny>', '<solver>', 'json;LogFileName=<observation>'
        commands[part] = {'cwd': str(cwd), 'command': canonical}
    return commands


def repeated_scope(scope, output, invocations, load_vector, check_repetition=None):
    vectors, failures = {}, {}
    for name in PASSES:
        directory = output / scope / name
        directory.mkdir(parents=True)
        command = invocations(name, directory)
        invocation = {'command': command, 'working_directory': str(Path.cwd())}
        try:
            try:
                with (directory / 'runner.stdout').open('w') as stdout, (directory / 'runner.stderr').open('w') as stderr:
                    process = subprocess.run(command, stdout=stdout, stderr=stderr)
                invocation['exit'] = process.returncode
            except Exception as error:
                invocation.update(exit='launch-error', error=str(error))
                raise
            finally:
                (directory / 'runner-invocation.json').write_text(json.dumps(invocation, indent=2) + '\n')
            require(process.returncode == 0, scope + ': runner failed; see retained diagnostics')
            result = load_vector(directory, name)
            if check_repetition is not None:
                require(name == 'A1' or 'A1' in vectors, scope + ': authoritative A1 pass is incomplete')
                if name != 'A1':
                    check_repetition(vectors['A1'], result)
            vectors[name] = result
            (directory / 'vectors.json').write_text(json.dumps(result, indent=2) + '\n')
            validation = {'status': 'complete-inventory', 'scope': scope, 'pass': name}
        except Exception as error:
            validation = {'status': 'incomplete', 'scope': scope, 'pass': name, 'error': str(error)}
            failures[name] = validation
        (directory / 'validation.json').write_text(json.dumps(validation, indent=2) + '\n')
    if failures:
        result = {'status': 'incomplete', 'failures': failures}
        (output / ('incomplete-' + scope + '.json')).write_text(json.dumps(result, indent=2) + '\n')
        return result
    pairs = {}
    # Inspect same-binary repetitions before the cross-build pairs.
    for left, right in PAIRS:
        delta = {kind: differences(vectors[left][kind], vectors[right][kind]) for kind in vectors[left]}
        pairs[left + ':' + right] = {'counts': {kind: len(value) for kind, value in delta.items()}, 'differences': delta}
    result = {'status': 'complete-inventory-comparison',
              'denominators': {name: {kind: len(value) for kind, value in vec.items()} for name, vec in vectors.items()},
              'pairs': pairs}
    (output / ('comparison-' + scope + '.json')).write_text(json.dumps(result, indent=2) + '\n')
    return result


def std_repetition(before, after):
    # The original artifact fixes exact scopes and per-scope counts. Original raw
    # VC logs were not retained, so the complete controlled A1 fixes VC numbers,
    # seeds and assertion identities for all later passes, without intersection.
    for kind in ['runs', 'declarations', 'vcs']:
        require(set(before[kind]) == set(after[kind]), 'source-library ' + kind + ': identity population differs from complete A1')
    require({key: value[2] for key, value in before['vcs'].items()} ==
            {key: value[2] for key, value in after['vcs'].items()}, 'source-library assertion identities differ from complete A1')


def main():
    baseline, candidate, output, solver51, solver_harness = [Path(arg).resolve() for arg in sys.argv[1:]]
    harness = Path(__file__).resolve().parents[2]
    require(not (output / 'summary.json').exists(), 'refusing to reuse an earlier summary')
    status = output / 'comparison-status.json'
    status.write_text(json.dumps({'status': 'running'}) + '\n')
    sources = {'A': baseline, 'B': candidate}
    builds = {'A': output / 'build-baseline', 'B': output / 'build-candidate'}
    require(not git(harness, 'status', '--porcelain', '--untracked-files=no'), 'modified tracked harness inputs')
    for runner in ['lit-verdicts.py', 'std-verdicts.py']:
        require(sha(harness / '.github/review' / runner) == sha(candidate / '.github/review' / runner),
                'native proof runner differs from frozen product B: ' + runner)
    identities = {}
    for label, source in sources.items():
        require(not git(source, 'status', '--porcelain', '--untracked-files=no'), label + ': modified tracked product inputs')
        packages = json.loads((source / 'Source/Dafny/obj/project.assets.json').read_text())['libraries']
        restore = output / 'restore-outputs' / label
        for assets in (source / 'Source').glob('**/obj/project.assets.json'):
            destination = restore / assets.relative_to(source)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(assets, destination)
        boogie = {line.split()[1]: line.split()[0] for line in (source / 'Scripts/boogie-packages.sha256').read_text().splitlines() if line.strip()}
        require(boogie and all(sha(source / 'Binaries/boogie-packages' / name) == digest for name, digest in boogie.items()),
                label + ': package bytes disagree with checked source manifest')
        identities[label] = {
            'source': git(source, 'rev-parse', 'HEAD'), 'tree': git(source, 'rev-parse', 'HEAD^{tree}'),
            'library_tree': git(source, 'rev-parse', 'HEAD:Source/DafnyStandardLibraries'),
            'lit_tree': git(source, 'rev-parse', 'HEAD:' + LIT),
            'library_files': tracked_files(source, 'Source/DafnyStandardLibraries'),
            'suite_files': tracked_files(source, LIT),
            'packages': {key: value.get('sha512') for key, value in packages.items() if value['type'] == 'package'},
            'boogie_packages': boogie, 'restore_outputs': manifest(restore),
            'restore_inputs': {key: value for key, value in tracked_files(source).items()
                               if key.endswith(('.csproj', '.props', '.targets', '.config', 'global.json', 'packages.lock.json'))},
            'build_files': manifest(builds[label]),
            'reported_version': subprocess.check_output([str(builds[label] / 'Dafny'), '--version'], text=True).strip(),
        }
    identities['harness'] = {'source': git(harness, 'rev-parse', 'HEAD'), 'tree': git(harness, 'rev-parse', 'HEAD^{tree}'),
                             'files': tracked_files(harness, '.github/review', '.github/workflows/review.yml'),
                             'production_equals_product_B': product_files(harness) == product_files(candidate)}
    identities['environment'] = {'system': platform.platform(), 'machine': platform.machine(),
        'runner': {key: os.environ.get(key) for key in ['ImageOS', 'ImageVersion', 'RUNNER_OS', 'RUNNER_ARCH', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT']},
        'os_release': Path('/etc/os-release').read_text()}
    (output / 'identities.json').write_text(json.dumps(identities, indent=2) + '\n')
    require(identities['A']['source'] == PRODUCT_A and identities['B']['source'] == PRODUCT_B, 'product revision differs from frozen A/B')
    require(identities['harness']['production_equals_product_B'], 'harness production differs from measured product B')
    for field in ['library_tree', 'library_files', 'packages', 'boogie_packages', 'reported_version']:
        require(identities['A'][field] == identities['B'][field], 'A/B ' + field + ' differs')
    require(all(identities['B']['suite_files'].get(name) == value for name, value in identities['A']['suite_files'].items()),
            'original suite input files differ')
    protected = {name: digest for name, digest in identities['A']['build_files'].items()
                 if (name.startswith('Boogie.') and name.endswith('.dll'))
                 or name.endswith(('.doo', '.deps.json', '.runtimeconfig.json', 'DafnyPrelude.bpl'))}
    require(protected and all(identities['B']['build_files'].get(name) == digest for name, digest in protected.items()), 'proof dependencies differ')
    inventory = json.loads(Path(__file__).with_name('default-comparison-inventory.json').read_text())
    suite_inventory = json.loads(Path(__file__).with_name('default-comparison-suite-inventory.json').read_text())
    require(inventory['baseline_source'] == identities['A']['source'] and inventory['library_tree'] == identities['A']['library_tree'],
            'authoritative library inventory source differs')
    require(suite_inventory['baseline_source'] == PRODUCT_A and suite_inventory['candidate_source'] == PRODUCT_B and
            suite_inventory['baseline_lit_tree'] == identities['A']['lit_tree'] and suite_inventory['candidate_lit_tree'] == identities['B']['lit_tree'],
            'authoritative suite inventory source differs')
    solver_identities = {}
    for version, solver in [('5.1.0', solver51), ('4.16.0', solver_harness)]:
        actual = subprocess.check_output([str(solver), '--version'], text=True).strip()
        require(actual.startswith('Z3 version ' + version + ' '), 'unexpected solver version')
        solver_identities[version] = {'sha256': sha(solver), 'version': actual}
    (output / 'solvers.json').write_text(json.dumps(solver_identities, indent=2) + '\n')
    plan = output / 'suite-plan.tsv'
    subprocess.run([sys.executable, str(harness / '.github/review/lit-verdicts.py'), 'plan', str(candidate / LIT), str(plan)], check=True)
    require(sha(plan) == suite_inventory['plan_sha256'], 'generated suite plan differs from original baseline')
    def suite_invocation(name, directory):
        (directory / 'plan.tsv').write_bytes(plan.read_bytes())
        return [sys.executable, str(harness / '.github/review/lit-verdicts.py'), 'run', str(directory / 'plan.tsv'),
                str(candidate / LIT), str(builds[name[0]] / 'Dafny'), str(solver51), str(directory / 'verdicts.tsv'),
                '--shard', '0/1', '--jobs', '4', '--measurements', str(directory / 'measurements')]
    results = {'suite-z3-5.1.0': repeated_scope('suite-z3-5.1.0', output, suite_invocation,
        lambda directory, name: suite.vector(directory, suite_inventory, builds[name[0]] / 'Dafny', solver51, merge_outcomes))}
    for version, solver in [('5.1.0', solver51), ('4.16.0', solver_harness)]:
        def std_invocation(name, directory):
            return [sys.executable, str(harness / '.github/review/std-verdicts.py'), 'run', str(candidate / 'Source/DafnyStandardLibraries'),
                    str(builds[name[0]] / 'Dafny'), str(solver), str(directory / 'std-verdicts.tsv'), '--cores', '4',
                    '--measurements', str(directory / 'measurements')]
        def std_vector(directory, name):
            result = vector(directory, inventory)
            result['commands'] = std_commands(directory, candidate, builds[name[0]] / 'Dafny', solver)
            return result
        results['stdlib-z3-' + version] = repeated_scope('stdlib-z3-' + version, output, std_invocation, std_vector, std_repetition)
    complete = all(result['status'] == 'complete-inventory-comparison' for result in results.values())
    final = {'status': 'complete-inventory-comparison' if complete else 'incomplete',
             'product_sources': {'A': PRODUCT_A, 'B': PRODUCT_B}, 'harness_source': identities['harness']['source'],
             'scopes': {scope: {key: value for key, value in result.items() if key != 'pairs'} |
                       ({'pairs': {pair: record['counts'] for pair, record in result['pairs'].items()}} if 'pairs' in result else {})
                       for scope, result in results.items()}}
    (output / ('summary.json' if complete else 'incomplete.json')).write_text(json.dumps(final, indent=2) + '\n')
    status.write_text(json.dumps({'status': final['status']}) + '\n')
    return 0 if complete else 1


def run_probe():
    destination = Path(sys.argv[3])
    # An attempted reuse must leave the previous packet untouched, including any
    # old complete summary that could otherwise be mistaken for this invocation.
    require(not any((destination / name).exists() for name in
                    ['comparison-status.json', 'summary.json', 'incomplete.json', 'raw-manifest.json']),
            'refusing to reuse an earlier comparison packet')
    try:
        return main()
    except Exception as error:
        if destination.is_dir():
            failure = {'status': 'incomplete', 'error': str(error)}
            (destination / 'comparison-status.json').write_text(json.dumps(failure, indent=2) + '\n')
            (destination / 'incomplete.json').write_text(json.dumps(failure, indent=2) + '\n')
        raise


def finalize_manifest(destination):
    # The parent wrapper calls this only after the probe exits, its redirected
    # streams close, and its real exit status is recorded. A traceback printed
    # while unwinding the probe must be included in the final stderr hash.
    require(all((destination / name).is_file() for name in ['probe.stdout', 'probe.stderr', 'probe-exit.txt']),
            'probe streams and exit record must exist before manifest finalization')
    require(not (destination / 'raw-manifest.json').exists(), 'refusing to overwrite an earlier manifest')
    with (destination / 'raw-manifest.json').open('x') as output:
        # The newly opened manifest is excluded from its own inventory.
        output.write(json.dumps({key: value for key, value in manifest(destination).items()
                                 if key != 'raw-manifest.json'}, indent=2) + '\n')


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--finalize-manifest':
        finalize_manifest(Path(sys.argv[2]))
    else:
        sys.exit(run_probe())
