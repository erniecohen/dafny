#!/usr/bin/env python3
"""Fail-closed, literal OFF output and resource comparison against the pinned product.

Both binaries operate on the same source and observation paths. Output is never
normalized: retained stdout/stderr, parsed/resolved text, and Boogie bytes must
match. Only nonsemantic verification-log timing fields are excluded from the
separate resource vector. Existing expected verdict files are not rewritten.
"""
import argparse
import concurrent.futures
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '-verdicts.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resources(path):
    if not path.exists():
        return None
    data = json.loads(path.read_text())
    return sorted([[d['name'], v['vcNum'], v['outcome'], v['resourceCount']]
                   for d in data.get('verificationResults', []) for v in d.get('vcResults', [])])


def files(folder):
    return {p.relative_to(folder).as_posix(): sha(p) for p in folder.rglob('*') if p.is_file()}


def suite(args, output):
    lit = load('lit')
    litdir = Path('Source/IntegrationTests/TestFiles/LitTests/LitTest').resolve()
    lit.plan(str(litdir), str(output / 'plan.tsv'))
    rows = [line.split('\t') for line in (output / 'plan.tsv').read_text().splitlines()]
    index, count = map(int, args.shard.split('/'))
    selected = rows[index::count]
    shared = output / 'observation'

    def one(row):
        path, options = row
        results = {}
        for mode, binary in [('baseline', args.baseline), ('candidate', args.candidate)]:
            verdict = lit.verify((str(litdir), binary, args.z3, path, options, False, str(shared)))
            folder = shared / path
            destination = output / mode / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            if destination.exists():
                shutil.rmtree(destination)
            shutil.move(str(folder), destination)
            results[mode] = {'verdict': list(verdict[:4]), 'output_sha256': sha(destination / 'output.txt'),
                             'resources': resources(destination / 'results.json')}
        differences = []
        if results['baseline'] != results['candidate']:
            differences.append('literal output, verdict or resource vector differs')
        if any(result['verdict'][1] == 'TIMEOUT' for result in results.values()):
            differences.append('wall-clock safety cap')
        print(path, 'PASS' if not differences else 'FAIL', flush=True)
        return {'program': path, 'differences': differences, 'results': results}

    with concurrent.futures.ThreadPoolExecutor(args.jobs) as executor:
        results = list(executor.map(one, selected))
    return {'scope': 'complete existing verifier-suite plan', 'population': len(rows),
            'shard': args.shard, 'programs': len(results), 'results': results,
            'passed': bool(results) and all(not row['differences'] for row in results)}


def stdlib(args, output):
    std = load('std')
    library = Path('Source/DafnyStandardLibraries').resolve()
    log = output / 'shared-log.json'
    def fixed_log(*_args, **_kwargs):
        return os.open(log, os.O_CREAT | os.O_RDWR | os.O_TRUNC, 0o600), str(log)
    std.tempfile.mkstemp = fixed_log
    shared_text = output / 'shared-text'
    original_part = std.part
    def observe_part(label, cwd, source_args, dafny, z3, cores, fixed, measurements=None):
        folder = shared_text / label
        folder.mkdir(parents=True)
        fixed = fixed + ['--print', str(folder / 'parsed.dfy'), '--rprint', str(folder / 'resolved.dfy'),
                         '--bprint', str(folder / 'program.bpl')]
        return original_part(label, cwd, source_args, dafny, z3, cores, fixed, measurements)
    std.part = observe_part
    results = {}
    for mode, binary in [('baseline', args.baseline), ('candidate', args.candidate)]:
        folder = output / mode
        folder.mkdir()
        measurements = folder / 'measurements'
        std.run(str(library), binary, args.z3, str(folder / 'verdicts.tsv'), cores=4,
                boogie='/deterministicLiteralHashes:1 /restartProver', measurements=str(measurements))
        rows = std.load(str(folder / 'verdicts.tsv'))
        results[mode] = {
            'verdicts': {key: list(row[1:4]) for key, row in rows.items()},
            'resources': {key: row[4:] for key, row in rows.items() if not key.startswith('run ')},
            'literal_output': {p.name: sha(p) for p in measurements.glob('*') if p.suffix in ['.stdout', '.stderr']},
            'batch_resources': {p.name: resources(p) for p in measurements.glob('*.verification.json')},
            'literal_text': files(shared_text),
        }
        shutil.move(str(shared_text), str(folder / 'text'))
    return {'scope': 'source standard library and all six target-specific parts', 'results': results,
            'passed': bool(results['baseline']['resources']) and
            any(name.endswith('.bpl') for name in results['baseline']['literal_text']) and
            results['baseline'] == results['candidate'] and
            all(value[0] != 'TIMEOUT' for value in results['baseline']['verdicts'].values())}


def text_corpus(args, output):
    input_dir = output / 'shared'
    input_dir.mkdir()
    source = input_dir / 'input.dfy'
    inputs = [
        'method Main() { assert 1 + 2 == 3; }',
        'datatype D = D { function {:operator 1} f(): int { 0 } }',
        'datatype D = D { function {:operator "+", "override"} f(x: D): D { x } }',
        'function operator(builtin: int): int { builtin + 1 }',
        'module {:foo 1 + 2, 1 < 2 < 3} M { function f(x:int):int { -x * (x+1) } }',
        'method m() { var x := 0; x += 1; }',
        'function f(): int { builtin.+(1, 2) }',
        'method m( { assert ; }',
        'datatype D = D { function {:operator} f(): int { 0 } }',
    ]
    results = []
    for index, text in enumerate(inputs):
        source.write_text(text + '\n')
        snapshots = {}
        for mode, binary, flags in [('baseline', args.baseline, []), ('candidate', args.candidate, []),
                                    ('explicit-false', args.candidate, ['--user-defined-operators:false'])]:
            observations = input_dir / 'text'
            observations.mkdir(exist_ok=True)
            command = [binary, 'verify', str(source), '--solver-path', args.z3, '--cores', '1',
                       '--boogie', '/normalizeDeclarationOrder:0', '--verification-time-limit', '0',
                       '--print', str(observations / 'parsed.dfy'), '--rprint', str(observations / 'resolved.dfy'),
                       '--bprint', str(observations / 'program.bpl')] + flags
            process = subprocess.run(command, cwd=input_dir, capture_output=True, timeout=120)
            (observations / 'stdout').write_bytes(process.stdout)
            (observations / 'stderr').write_bytes(process.stderr)
            snapshots[mode] = {'exit': process.returncode, 'bytes': files(observations)}
            shutil.move(str(observations), str(output / (str(index) + '-' + mode)))
        results.append({'case': index, 'source': text, 'snapshots': snapshots,
                        'passed': snapshots['baseline'] == snapshots['candidate'] == snapshots['explicit-false']})
    # Disabled artifact output and invalid-record error paths must also stay literal.
    source.write_text('method Main() { print 1 + 2; }\n')
    invalid_record = input_dir / 'invalid.dtr'
    invalid_record.write_text('invalid translation record\n')
    for scenario in ['automatic-record', 'missing-record', 'invalid-record']:
        snapshots = {}
        for mode, binary in [('baseline', args.baseline), ('candidate', args.candidate)]:
            observations = input_dir / 'artifacts'
            observations.mkdir()
            command = [binary, 'translate', 'py', str(source), '--no-verify', '--output', str(observations / 'test')]
            if scenario != 'automatic-record':
                command += ['--translation-record', str(invalid_record if scenario == 'invalid-record' else input_dir / 'missing.dtr')]
            process = subprocess.run(command, cwd=input_dir, capture_output=True, timeout=120)
            (observations / 'stdout').write_bytes(process.stdout)
            (observations / 'stderr').write_bytes(process.stderr)
            snapshots[mode] = {'exit': process.returncode, 'bytes': files(observations)}
            shutil.move(str(observations), str(output / (scenario + '-' + mode)))
        expected = snapshots['baseline']['exit'] == 0 if scenario == 'automatic-record' else snapshots['baseline']['exit'] != 0
        results.append({'case': scenario, 'snapshots': snapshots,
                        'passed': snapshots['baseline'] == snapshots['candidate'] and expected})
    return {'scope': 'literal parsed/resolved/Boogie output and malformed input diagnostics',
            'corpus': results, 'passed': all(row['passed'] for row in results)}


def main():
    parser = argparse.ArgumentParser()
    for name in ['baseline', 'candidate', 'z3', 'output']:
        parser.add_argument('--' + name, required=True)
    parser.add_argument('--scope', choices=['suite', 'stdlib', 'text'], required=True)
    parser.add_argument('--shard', default='0/1')
    parser.add_argument('--jobs', type=int, default=4)
    args = parser.parse_args()
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    report = {'passed': False}
    try:
        report = {'suite': suite, 'stdlib': stdlib, 'text': text_corpus}[args.scope](args, output)
    except Exception as error:
        report['exception'] = repr(error)
        raise
    finally:
        (output / 'report.json').write_text(json.dumps(report, indent=2))
    print(json.dumps({key: value for key, value in report.items() if key not in ['results', 'corpus', 'library']}, indent=2))
    raise SystemExit(0 if report['passed'] else 1)


if __name__ == '__main__':
    main()
