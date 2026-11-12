#!/usr/bin/env python3
"""Paired default-preservation and N=1,2,4,8 resolution audit.

Use the unchanged native suite/library runners. A small executable adapter adds
only resolution-threads and raw Boogie capture; every invocation, verification
log, per-VC outcome/RU, and per-module byte digest is retained. Std uses the same
literal hashing and prover restart options as the ordinary default gate.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import re
from pathlib import Path
import shlex
import signal
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
MODES = ('baseline', 'off', '1', '2', '4', '8')


def load_runner(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '-verdicts.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + '\n')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def wrapper():
    """Called by a native runner as its executable, once per Dafny process."""
    args = sys.argv[2:]
    cwd = Path.cwd().relative_to(ROOT).as_posix()
    # Native runners place input arguments before --solver-path. Observation
    # paths, random temporary names, and worker counts never enter this identity.
    inputs = args[:args.index('--solver-path')]
    identity = json.dumps([cwd, inputs], separators=(',', ':'))
    key = hashlib.sha256(identity.encode()).hexdigest()
    folder = Path(os.environ['RESOLUTION_AUDIT_OUT']) / 'boogie' / key
    folder.mkdir(parents=True, exist_ok=False)
    executable = os.environ['RESOLUTION_AUDIT_DAFNY']
    threads = os.environ['RESOLUTION_AUDIT_THREADS']
    added = ['--bprint', str(folder / 'program.bpl')]
    if threads != '0':
        added += ['--resolution-threads', threads]
    command = [executable, args[0], *added, *args[1:]]
    process = subprocess.Popen(command, start_new_session=True)
    timed_out = False
    try:
        code = process.wait(timeout=int(os.environ['RESOLUTION_AUDIT_TIMEOUT']))
    except subprocess.TimeoutExpired:
        timed_out = True
        os.killpg(process.pid, signal.SIGKILL)
        process.wait()
        code = 124
    files = {p.name: digest(p) for p in sorted(folder.glob('*.bpl'))}
    dump(folder / 'manifest.json', dict(identity=identity, command=command,
        exit=code, timeout=timed_out, modules=files))
    # Keep raw input for the cost investigation; the other byte vectors are
    # compared without path or identifier normalization, then discard their text.
    keep = 'MinWindowMax.dfy' in identity or 'src/Std/dfyconfig.toml' in identity
    if not keep:
        for p in folder.glob('*.bpl'):
            p.unlink()
    if code < 0:
        signal.signal(-code, signal.SIG_DFL)
        os.kill(os.getpid(), -code)
    return code


def boogie_vector(folder):
    result = {}
    for path in sorted((folder / 'boogie').glob('*/manifest.json')):
        record = json.loads(path.read_text())
        if record['timeout']:
            raise ValueError('Safety timeout: ' + record['identity'])
        if record['identity'] in result:
            raise ValueError('Duplicate invocation identity')
        result[record['identity']] = record['modules']
    return result


def proof_vector(folder, part):
    result = {}
    patterns = ['**/results.json'] if part != 'stdlib' else ['*.verification.json']
    for pattern in patterns:
        for path in sorted((folder / 'measurements').glob(pattern)):
            if not path.read_text().strip():
                continue  # Native verdicts retain intentional pre-verification diagnostics.
            records = json.loads(path.read_text())['verificationResults']
            source = path.parent.relative_to(folder / 'measurements').as_posix() if part != 'stdlib' else path.name
            for declaration in records:
                for proof in declaration['vcResults']:
                    key = '\t'.join([source, declaration['name'], str(proof['vcNum'])])
                    if key in result:
                        raise ValueError('Duplicate proof: ' + key)
                    cost = proof['resourceCount']
                    if type(cost) is not int or cost < 0:
                        raise ValueError('Invalid resource count: ' + key)
                    result[key] = [proof['outcome'], cost]
    return result


def capture(args):
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    native = load_runner('std' if args.part == 'stdlib' else 'lit')
    adapter = out / 'dafny-adapter'
    adapter.write_text('#!/bin/sh\nexec python3 ' + shlex.quote(str(Path(__file__).resolve())) + ' wrapper "$@"\n')
    adapter.chmod(0o755)
    if args.part != 'stdlib':
        plan = out / 'plan.tsv'
        if args.plan:
            plan.write_bytes(args.plan.read_bytes())
        else:
            native.plan(str(ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest'), str(plan))
    for mode in args.modes.split(','):
        folder = out / mode
        folder.mkdir(exist_ok=False)
        os.environ.update(RESOLUTION_AUDIT_OUT=str(folder),
            RESOLUTION_AUDIT_DAFNY=str((args.baseline if mode == 'baseline' else args.dafny).resolve()),
            RESOLUTION_AUDIT_THREADS='0' if mode in ('off', 'baseline') else mode,
            RESOLUTION_AUDIT_TIMEOUT='5350' if args.part == 'stdlib' else '890')
        verdicts = folder / 'verdicts.tsv'
        if args.part == 'stdlib':
            native.run(str(ROOT / 'Source/DafnyStandardLibraries'), str(adapter), str(args.z3.resolve()), str(verdicts),
                boogie='/deterministicLiteralHashes:1 /restartProver', measurements=str(folder / 'measurements'))
        else:
            native.run(str(plan), str(ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest'),
                str(adapter), str(args.z3.resolve()), str(verdicts), shard=str(args.part) + '/4', jobs=4,
                measurements=str(folder / 'measurements'))
        rows = native.load(verdicts)
        if args.part != 'stdlib':
            for key, row in rows.items():
                positive = any(int(n) > 0 for n in re.findall(r'(\d+) verified', row[2]))
                log = folder / 'measurements' / key / 'results.json'
                if positive and (not log.is_file() or not log.read_text().strip() or
                    not json.loads(log.read_text()).get('verificationResults')):
                    raise ValueError('Missing proof observations for positive program: ' + key)
        vector = dict(verdicts={key: row[1:4] for key, row in rows.items()},
            proofs=proof_vector(folder, args.part), boogie=boogie_vector(folder))
        runs = len(rows) if args.part != 'stdlib' else sum(key.startswith('run ') for key in rows)
        if len(vector['boogie']) != runs:
            raise ValueError('Incomplete process observation population for ' + mode)
        if not vector['boogie'] or not vector['proofs']:
            raise ValueError('Empty invocation or proof population for ' + mode)
        vector['total_ru'] = sum(p[1] for p in vector['proofs'].values())
        vector['out_of_resource_vcs'] = sum(p[0] == 'OutOfResource' for p in vector['proofs'].values())
        dump(folder / 'vector.json', vector)
        print(mode, 'VCs', len(vector['proofs']), 'RU', vector['total_ru'], flush=True)


def differences(left, right):
    return {key: dict(before=left.get(key), after=right.get(key))
        for key in sorted(set(left) | set(right)) if left.get(key) != right.get(key)}


def compare(args):
    root = args.output
    vectors = {mode: json.loads((root / mode / 'vector.json').read_text()) for mode in MODES}
    errors = []
    report = {'part': args.part, 'comparisons': {}, 'totals': {}}
    for mode, vector in vectors.items():
        report['totals'][mode] = {k: vector[k] for k in ('total_ru', 'out_of_resource_vcs')}
    for left, right in [('baseline', 'off'), ('1', '2'), ('1', '4'), ('1', '8')]:
        delta = {field: differences(vectors[left][field], vectors[right][field])
            for field in ('verdicts', 'proofs', 'boogie')}
        report['comparisons'][left + ':' + right] = delta
        if any(delta.values()):
            errors.append(left + ':' + right + ' differs')
    for field in ('verdicts', 'proofs'):
        if set(vectors['off'][field]) != set(vectors['1'][field]):
            errors.append('Default-to-on ' + field + ' population differs')
    report['default_to_on'] = {field: differences(vectors['off'][field], vectors['1'][field])
        for field in ('verdicts', 'proofs')}
    # This is the opt-in vector to review after the first failing run. Never
    # overwrite the default baseline and never derive an expected vector in-place.
    actual = vectors['1']['verdicts']
    (root / 'expected-opt-in.tsv').write_text(''.join(k + '\t' + '\t'.join(v) + '\n' for k, v in sorted(actual.items())))
    if args.expected.is_file():
        expected = {r[0]: r[1:4] for line in args.expected.read_text().splitlines() if (r := line.split('\t'))}
        expected = {k: v for k, v in expected.items() if k in actual}
        delta = differences(expected, actual)
        report['expected_to_on'] = delta
        if delta:
            errors.append('Opt-in baseline differs')
    else:
        errors.append('Opt-in baseline not yet reviewed')
    report['errors'] = errors
    dump(root / 'report.json', report)
    print(json.dumps({k: v for k, v in report.items() if k not in ('comparisons', 'default_to_on')}, indent=2))
    print('Default-to-on verdict movements:', json.dumps(report['default_to_on']['verdicts'], indent=2))
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as f:
            f.write('\n### Module-resolution audit: ' + args.part + '\n\n```\n' + json.dumps(report['totals'], indent=2) + '\n```\n')
            f.write('\n' + ('; '.join(errors) or 'All default-preservation and worker-count checks passed.') + '\n')
    return int(bool(errors))


def validate_seed_zero(vector, source, records):
    expected = {key: value for key, value in vector['proofs'].items() if key.split('\t')[0] == source}
    actual = {}
    for record in records:
        for proof in record['vcResults']:
            key = '\t'.join([source, record['name'], str(proof['vcNum'])])
            if key in actual:
                raise ValueError('Duplicate seed-zero proof: ' + key)
            actual[key] = [proof['outcome'], proof['resourceCount']]
    if not expected or expected != actual:
        raise ValueError('Seed zero does not reproduce the full capture: ' + source)


def variance(args):
    """Three-seed OFF/ON observations of every scope whose outcome moved."""
    root = args.output.resolve()
    report = json.loads((root / 'report.json').read_text())
    changed = report['default_to_on']['proofs']
    scopes = sorted({tuple(key.split('\t')[:2]) for key, value in changed.items()
        if value['before'] is not None and value['after'] is not None and value['before'][0] != value['after'][0]})
    observations = []
    # Filtering to one symbol skips translation of other modules. That changes
    # legacy name history, so it is not a controlled seed experiment. Repeat the
    # complete original invocation and retain affected scopes in the summary.
    cases = sorted({(source, '') for source, _ in scopes})
    for source, scope in cases:
        if args.part == 'stdlib':
            label = source.removesuffix('.verification.json')
            invocation = json.loads((root / 'off/measurements' / (label + '.invocation.json')).read_text())
            if label != 'Std':
                raise ValueError('Target-specific variance requires preserving its binary options')
            cwd = ROOT / 'Source/DafnyStandardLibraries'
            original = invocation['command'][1:]
        else:
            original = json.loads((root / 'off/measurements' / source / 'command.json').read_text())[1:]
            cwd = ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest'
        name = hashlib.sha256((source + '\t' + scope).encode()).hexdigest()[:16]
        for seed in (0, 1, 2):
            for mode in ('off', '1'):
                folder = root / 'seed-variance' / name / str(seed) / mode
                folder.mkdir(parents=True, exist_ok=False)
                log = folder / 'proofs.json'
                argv = original.copy()
                argv[argv.index('--log-format') + 1] = 'json;LogFileName=' + str(log)
                extra = ['--bprint', str(folder / 'program.bpl'), '--boogie', '/randomSeed:' + str(seed)]
                if mode == '1':
                    extra += ['--resolution-threads', '1']
                command = [str(args.dafny.resolve()), argv[0], *extra, *argv[1:]]
                process = subprocess.Popen(command, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                    text=True, start_new_session=True)
                try:
                    output, _ = process.communicate(timeout=5350 if args.part == 'stdlib' else 1200)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.communicate()
                    raise ValueError('Seed-variance safety timeout')
                (folder / 'output.txt').write_text(output)
                dump(folder / 'invocation.json', dict(command=command, exit=process.returncode, cwd=str(cwd)))
                if process.returncode not in (0, 4) or not log.is_file():
                    raise ValueError('Incomplete seed-variance observation: ' + str(folder))
                records = json.loads(log.read_text())['verificationResults']
                affected = {name for input_source, name in scopes if input_source == source}
                if not records or not affected.issubset({r['name'] for r in records}):
                    raise ValueError('Missing affected scopes: ' + source)
                if seed == 0:
                    vector = json.loads((root / mode / 'vector.json').read_text())
                    validate_seed_zero(vector, source, records)
                entries = {r['name']: dict(outcome=r['outcome'], ru=r['resourceCount'],
                    vcs={str(v['vcNum']): [v['outcome'], v['resourceCount']] for v in r['vcResults']})
                    for r in records if args.part != 'stdlib' or r['name'] in affected}
                observations.append(dict(source=source, scope=scope, seed=seed, mode=mode, results=entries))
                dump(root / 'seed-variance.json', observations)
                print('Seed variance', source, scope, seed, mode, entries, flush=True)
    dump(root / 'seed-variance.json', observations)
    return 0


def main():
    if len(sys.argv) > 1 and sys.argv[1] == 'wrapper':
        return wrapper()
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='action', required=True)
    run = commands.add_parser('capture')
    run.add_argument('--part', required=True, choices=['0', '1', '2', '3', 'stdlib'])
    run.add_argument('--dafny', type=Path, required=True)
    run.add_argument('--baseline', type=Path, required=True)
    run.add_argument('--z3', type=Path, required=True)
    run.add_argument('--output', type=Path, required=True)
    run.add_argument('--plan', type=Path)
    run.add_argument('--modes', default=','.join(MODES))
    check = commands.add_parser('compare')
    check.add_argument('--part', required=True)
    check.add_argument('--output', type=Path, required=True)
    check.add_argument('--expected', type=Path, required=True)
    seeds = commands.add_parser('variance')
    seeds.add_argument('--part', required=True)
    seeds.add_argument('--dafny', type=Path, required=True)
    seeds.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.action == 'capture':
        capture(args)
        return 0
    return variance(args) if args.action == 'variance' else compare(args)


if __name__ == '__main__':
    sys.exit(main())
