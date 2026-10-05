#!/usr/bin/env python3
"""CI-only paired observer using two retained compilers; no build or source overlay.

Package/denominator checks are pure. prepare/observe/finalize require a guarded
scratch Actions context; failed phases remain durable diagnostics and return 0.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import sys
import traceback
import urllib.request
import zipfile
from plan import HERE, digest
from prepare import inputs
from matrix import workload
import repair82
import reuse_compiler


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + '\n')


def package_guard():
    expected = json.loads((HERE / 'reuse-package-manifest.json').read_text())
    actual = {p.relative_to(HERE).as_posix(): digest(p) for p in HERE.rglob('*')
              if p.is_file() and '__pycache__' not in p.parts and p.name != 'reuse-package-manifest.json'}
    if actual != expected['files_sha256'] or any(p.is_symlink() for p in HERE.rglob('*')):
        raise ValueError('Published observer/source/oracle byte closure changed')
    if digest(HERE.parents[2] / '.github/workflows/review.yml') != expected['workflow_sha256']:
        raise ValueError('Published scratch workflow bytes changed')
    return dict(files=len(actual), manifest_sha256=digest(HERE / 'reuse-package-manifest.json'))


def denominators():
    old = json.loads((HERE / 'canonical/shipped/plan.json').read_text())
    matrix = workload(False, False)
    literal = repair82.plan()
    canonical = len(old['canonical'])
    standard = len(old['std'])
    calls = sum(run['kind'] == 'verify' for row in literal for run in row['runs'])
    checks = sum(run['kind'] != 'verify' for row in literal for run in row['runs'])
    return dict(paired_jobs=len(matrix['matrix']), canonical_cases_per_AX=canonical,
                canonical_shard_cases=[sum(row['index'] % 4 == i for row in old['canonical']) for i in range(4)],
                canonical_pairs=2*canonical, Std_parts_per_solver_AX=standard,
                Std_pairs=4*standard, repair82_sources=len(literal), repair82_verify_invocations_per_arm=calls,
                repair82_output_checks_per_arm=checks, repair82_pairs=calls,
                total_pairs=2*canonical+4*standard+calls,
                actual_compiler_invocations=2*(2*canonical+4*standard+calls),
                output_mappings_per_arm=sum(len(row['runs']) for row in literal),
                old_input_files=len(json.loads((HERE / 'canonical/shipped/inputs.json').read_text())),
                mandatory_semantic_observation_boundary='Actual declaration/batch denominators, proof errors, warnings and OOR are measured outputs; no proof-green or solver-query/cost equality follows from workflow success or BPL equality.')


def ci_guard(harness):
    if os.environ.get('GITHUB_ACTIONS') != 'true' or os.environ.get('GITHUB_REPOSITORY') != 'erniecohen/dafny' or not os.environ.get('GITHUB_REF', '').startswith('refs/heads/scratch/'):
        raise ValueError('Engine observations require the named public repository scratch Actions context')
    sha = os.environ.get('GITHUB_SHA', '')
    if not re.fullmatch('[0-9a-f]{40}', sha) or subprocess.check_output(['git', '-C', str(harness), 'rev-parse', 'HEAD'], text=True).strip() != sha:
        raise ValueError('Scratch checkout differs from immutable dispatch SHA')
    binding = json.loads((HERE / 'reused-compilers.json').read_text())['arms']['final']['source_binding']
    for path, key in [('Source', 'source_tree'), ('Source/DafnyCore', 'core_tree'), ('Source/DafnyDriver', 'driver_tree')]:
        if subprocess.check_output(['git', '-C', str(harness), 'rev-parse', 'HEAD:' + path], text=True).strip() != binding[key]:
            raise ValueError('Scratch recording tree contains a changed compiler Source; candidate overlay is excluded')
    return dict(repository=os.environ['GITHUB_REPOSITORY'], ref=os.environ['GITHUB_REF'], harness_source=sha,
                Actions_run=os.environ.get('GITHUB_RUN_ID'), runner_platform=platform.platform(), Python=sys.version)


def recorded(argv, where):
    where.mkdir(parents=True, exist_ok=True)
    write(where / 'command.json', {'argv': list(map(str, argv))})
    with (where / 'stdout.txt').open('wb') as out, (where / 'stderr.txt').open('wb') as err:
        value = subprocess.run(list(map(str, argv)), stdout=out, stderr=err)
    write(where / 'exit.json', {'exit': value.returncode, 'stdout_sha256': digest(where / 'stdout.txt'),
                               'stderr_sha256': digest(where / 'stderr.txt')})
    return value.returncode


def install_solver(version, item, root, logs):
    archive = root / (version + '.zip')
    urllib.request.urlretrieve(item['url'], archive)
    if digest(archive) != item['sha256']:
        raise ValueError('Pinned solver download hash differs')
    destination = root / version
    destination.mkdir()
    with zipfile.ZipFile(archive) as handle:
        for member in handle.infolist():
            p = Path(member.filename)
            if p.is_absolute() or '..' in p.parts:
                raise ValueError('Unsafe pinned solver archive member')
        handle.extractall(destination)
    # These hash-pinned archives intentionally have different published layouts.
    if version == '5.1.0':
        matches = list(destination.rglob('bin/z3'))
    elif version == '4.12.1':
        matches = [destination / 'z3-4.12.1'] if (destination / 'z3-4.12.1').is_file() else []
    else:
        raise ValueError('Solver version is outside the two pinned layouts')
    if len(matches) != 1:
        raise ValueError('Pinned solver has no unique binary in its published layout')
    executable = matches[0].resolve()
    executable.chmod(0o755)
    if recorded([executable, '--version'], logs) != 0 or not re.search(r'\b' + re.escape(version) + r'\b', (logs / 'stdout.txt').read_text()):
        raise ValueError('Actual solver version differs')
    return dict(path=str(executable), archive_sha256=digest(archive), binary_sha256=digest(executable),
                version_file=str((logs / 'stdout.txt').resolve()), archive_path=str(archive.resolve()))


def prepare(args):
    result = dict(phase='prepare', complete=False, new_build_performed=False, candidate_overlay_used=False)
    result['Actions_context'] = ci_guard(args.harness)
    result['package'] = package_guard()
    result['inputs'] = inputs('shipped', args.inputs)
    result['artifact_API'] = reuse_compiler.checked_api(args.download / 'artifact-api.json')
    archive_record = json.loads((HERE / 'reused-compilers.json').read_text())
    result['arms'] = {}
    for side in ['baseline', 'final']:
        bundle = args.root / 'bundles' / side / 'dafny'
        output = args.root / 'setup' / side
        if recorded([sys.executable, HERE / 'reuse_compiler.py', 'reuse', side, args.download, bundle, output], args.root / 'setup' / (side + '-extraction')) != 0:
            raise ValueError('Pure public archive/source/closure extraction failed: ' + side)
        version = args.root / 'setup' / (side + '-actual-version')
        if recorded([bundle / 'Dafny', '--version'], version) != 0 or (version / 'stdout.txt').read_text().strip() != archive_record['arms'][side]['actual_version']:
            raise ValueError('Actual archived compiler version differs: ' + side)
        result['arms'][side] = dict(executable=str((bundle / 'Dafny').resolve()), bundle=str(bundle.resolve()),
                                    identity=str((output / 'compiler-components.json').resolve()),
                                    version_file=str((version / 'stdout.txt').resolve()),
                                    archive_sha256=archive_record['arms'][side]['archive_sha256'])
    tools = args.root / 'tools'; tools.mkdir()
    spec = json.loads((HERE / 'spec.json').read_text())
    result['solvers'] = {'5.1.0': install_solver('5.1.0', spec['solver_5_1_0'], tools, args.root / 'setup/solver-351'),
                         'reference': install_solver('4.12.1', spec['reference_solvers']['4.12.1'], tools, args.root / 'setup/solver-reference')}
    result['denominators'] = denominators()
    result['complete'] = True
    write(args.root / 'setup/preparation.json', result)
    return result


def observe(args):
    ci_guard(args.harness); package_guard()
    prepared = json.loads((args.root / 'setup/preparation.json').read_text())
    if not prepared['complete'] or prepared['candidate_overlay_used'] or prepared['new_build_performed']:
        raise ValueError('Exact two retained compiler prerequisites are absent')
    row = next((r for r in workload(False, False)['matrix'] if r['tag'] == args.tag), None)
    if row is None:
        raise ValueError('Job does not belong to the exact frozen38-job matrix')
    common = []
    for side in ['baseline', 'final']:
        arm = prepared['arms'][side]
        common += ['--'+side, arm['executable'], '--'+side+'-identity', arm['identity'], '--'+side+'-version', arm['version_file']]
        reuse_compiler.checked_bundle(side, Path(arm['bundle']))
    if row['cohort'] == 'repair82-literal':
        script = HERE / 'repair82.py'
        flags = common + ['--default-solver', prepared['solvers']['reference']['path'], '--review-solver', prepared['solvers']['5.1.0']['path'],
                          '--default-solver-version', prepared['solvers']['reference']['version_file'], '--review-solver-version', prepared['solvers']['5.1.0']['version_file'],
                          '--shard', row['shard'], '--output', str(args.root / 'observations')]
    else:
        script = HERE / 'run.py'
        solver = prepared['solvers'][row['solver']]
        flags = common + ['--line', 'shipped', '--cohort', row['cohort'], '--mode', row['mode'], '--axioms', row['axioms'],
                          '--inputs', str(args.inputs), '--solver', solver['path'], '--solver-version', solver['version_file'],
                          '--jobs', str(row['jobs']), '--shard', row['shard'], '--output', str(args.root / 'observations')]
        if row['part']:
            flags += ['--part', row['part']]
    status = recorded([sys.executable, script, 'run', *flags], args.root / 'execution')
    return dict(phase='observe', adapter_exit=status, observer_returned=status == 0, matrix_row=row,
                comparison_and_proof_acceptance_claimed=False, candidate_overlay_used=False,
                boundary='An adapter exit0 only indicates its observer returned. Raw summaries/negative controls/resources/BPL and missing data require independent review.')


def finalize(args):
    ci_guard(args.harness)
    before = json.loads((args.root / 'setup/preparation.json').read_text())
    result = dict(phase='finalize', complete=False, package=package_guard(), inputs=inputs('shipped', args.inputs), arms={})
    for side in ['baseline', 'final']:
        reuse_compiler.checked_archive(side, args.download)
        result['arms'][side] = reuse_compiler.checked_bundle(side, Path(before['arms'][side]['bundle']))
    for value in before['solvers'].values():
        if digest(Path(value['path'])) != value['binary_sha256'] or digest(Path(value['archive_path'])) != value['archive_sha256']:
            raise ValueError('Actual solver binary/archive changed during observations')
    result['complete'] = True
    return result


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('phase', choices=['check-package', 'denominators', 'prepare', 'observe', 'finalize'])
    for name in ['harness', 'inputs', 'download', 'root']:
        p.add_argument('--' + name, type=Path)
    p.add_argument('--tag')
    args = p.parse_args()
    if args.phase == 'check-package':
        print(json.dumps(package_guard())); return
    if args.phase == 'denominators':
        print(json.dumps(denominators(), indent=2)); return
    if any(getattr(args, name) is None for name in ['harness', 'inputs', 'download', 'root']):
        p.error('CI phases require harness, inputs, download and fresh root')
    for name in ['harness', 'inputs', 'download', 'root']:
        setattr(args, name, getattr(args, name).resolve())
    args.root.mkdir(parents=True, exist_ok=True)
    try:
        result = {'prepare': prepare, 'observe': observe, 'finalize': finalize}[args.phase](args)
    except Exception as error:
        result = dict(phase=args.phase, complete=False, error=type(error).__name__ + ': ' + str(error),
                      diagnostic_only=True, proof_acceptance_claimed=False, candidate_overlay_used=False,
                      new_build_performed=False, traceback=traceback.format_exc())
    write(args.root / (args.phase + '-phase.json'), result)
    print(json.dumps(result, indent=2))
    # Expected setup/proof/comparison failures are retained, never mailed as a
    # scratch Actions failure or converted into a semantic acceptance claim.
    return 0


if __name__ == '__main__':
    sys.exit(main())
