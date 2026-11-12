"""Imported-body early cutoff, with independent current-input oracles and solver counts."""
import collections
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

exe, solver, source, destination = (Path(s).resolve() for s in sys.argv[1:])
assert not destination.exists(), 'use a fresh artifact directory'
destination.mkdir(parents=True)
original = source.read_text()
input_file = destination / 'input.dfy'
cache = destination / 'cache'
measurements = []


def untimed(value):
    if isinstance(value, dict):
        result = {k: untimed(v) for k, v in value.items() if k not in ('runTime', 'timingReplayed')}
        if 'vcResults' in result:
            result['vcResults'].sort(key=lambda row: (row['vcNum'], str(row.get('randomSeed', 0))))
        return result
    if isinstance(value, list):
        return [untimed(v) for v in value]
    return value


def run(name, text, directory=cache):
    input_file.write_text(text)
    profile, report, bpl = (destination / ('current.' + ext) for ext in ('jsonl', 'json', 'bpl'))
    for p in [profile, report, *destination.glob('current*.bpl')]:
        p.unlink(missing_ok=True)
    args = ['dotnet', str(exe), 'verify', str(input_file), '--cores', '1',
            '--solver-path', str(solver), '--verification-time-limit', '0', '--resource-limit', '16000000',
            '--boogie', '/deterministicLiteralHashes:1', '--use-basename-for-filename', '--show-snippets:false',
            '--verification-cache', str(directory), '--pipeline-profile', str(profile), '--bprint', str(bpl),
            '--log-format', 'json;LogFileName=' + str(report)]
    start = time.monotonic()
    with (destination / (name + '.stdout')).open('wb') as out, (destination / (name + '.stderr')).open('wb') as err:
        process = subprocess.Popen(args, stdout=out, stderr=err)
        _, status, usage = os.wait4(process.pid, 0)
        process.returncode = os.waitstatus_to_exitcode(status)
    wall = time.monotonic() - start
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    stats, = (e for e in events if e.get('ev') == 'result-cache')
    tasks = [e for e in events if e.get('ev') == 'result-task']
    assert len(tasks) == 8 and all(t['executionKey'] for t in tasks), (name, tasks)
    translated = {e['module'] for e in events if e.get('stage') == 'translate'}
    assert {'Api', 'ClientB', 'ClientC', 'ClientD', 'Leaf'} <= translated, translated
    modules = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.glob('current*.bpl')}
    assert all(any(module in p for p in modules) for module in ('ClientB', 'ClientC', 'ClientD')), modules
    by_module = collections.defaultdict(lambda: {'hit': 0, 'rerun': 0})
    for task in tasks:
        module = task['task'].split('.', 1)[0]
        by_module[module]['hit' if task['replayed'] else 'rerun'] += 1
    result = {'report': untimed(json.loads(report.read_text())),
              'output': [process.returncode, (destination / (name + '.stdout')).read_bytes(),
                         (destination / (name + '.stderr')).read_bytes()],
              'keys': collections.Counter(t['executionKey'] for t in tasks), 'tasks': tasks,
              'modules': modules, 'stats': stats}
    for suffix, p in [('jsonl', profile), ('json', report)]:
        shutil.copyfile(p, destination / (name + '.' + suffix))
    row = {'scenario': name, 'wall': wall, 'cpu': usage.ru_utime + usage.ru_stime,
           'RU': sum(t['resourceCount'] for t in tasks), 'tasks': len(tasks), 'exit': process.returncode,
           'perModule': dict(by_module), **stats}
    measurements.append(row)
    (destination / (name + '.measurement.json')).write_text(json.dumps(row, indent=2))
    return result


cold = run('cold', original)
assert cold['output'][0] == 0 and cold['stats']['solverStarts'] == cold['stats']['publications'] == 8
warm = run('warm', original)
for field in ('output', 'report', 'keys', 'modules'):
    assert cold[field] == warm[field], field
assert warm['stats']['solverStarts'] == warm['stats']['solverVersionProbes'] == 0
assert warm['stats']['hits'] == 8

edits = {
    'unused-import': ('int { x + 10 }', 'int { x + 11 }'),
    'widely-imported': ('int { x + 1 }', 'int { x + 2 }'),
    'imported-proof': ('{ assert true; }', '{ assert 0 == 0; }'),
    'leaf': ('int { x + 3 }', 'int { x + 4 }'),
}
for name, (before, after) in edits.items():
    # Limit the proof edit to Api.Proof; other bodies remain byte-identical.
    text = original.replace(before, after, 1)
    assert text != original
    edited = run(name, text)
    oracle = run(name + '-oracle', text, destination / (name + '-oracle-cache'))
    for field in ('output', 'report', 'keys', 'modules'):
        assert edited[field] == oracle[field], (name, field)
    changed = set(oracle['keys']) - set(cold['keys'])
    actual = {t['executionKey'] for t in edited['tasks'] if not t['replayed']}
    assert changed == actual, (name, changed, actual)
    assert edited['stats']['solverStarts'] == sum(not t['replayed'] for t in edited['tasks'])
    if name == 'unused-import':
        assert edited['stats']['solverStarts'] == 0 and edited['stats']['hits'] == 8
        for module in ('ClientB', 'ClientC', 'ClientD'):
            assert all(t['replayed'] for t in edited['tasks'] if t['task'].startswith(module + '.'))
        assert any(cold['modules'][p] != edited['modules'][p] for p in cold['modules'] if 'ClientB' in p)
    if name == 'widely-imported':
        assert edited['stats']['solverStarts'] == 4 and edited['stats']['hits'] == 4, edited['stats']
        assert all(t['replayed'] for t in edited['tasks'] if '.Pruned' in t['task'])
    if name == 'imported-proof':
        for module in ('ClientB', 'ClientC', 'ClientD'):
            names = [p for p in cold['modules'] if module in p]
            assert names and all(cold['modules'][p] == edited['modules'][p] for p in names)
            assert all(t['replayed'] for t in edited['tasks'] if t['task'].startswith(module + '.'))
    if name == 'leaf':
        assert edited['stats']['solverStarts'] == 1 and edited['stats']['hits'] == 7, edited['stats']

(destination / 'measurements.json').write_text(json.dumps(measurements, indent=2))
print('verification cache: imported-body early cutoff; unchanged imported Boogie; exact changed-task oracles')
