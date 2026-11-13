#!/usr/bin/env python3
"""Measure persistent task reuse with fresh CLI processes and verify the observations."""
import argparse
import collections
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import shutil
import statistics
import subprocess
import threading
import time
import traceback

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dafny', required=True, type=Path, help='Built Dafny.dll')
parser.add_argument('--solver', required=True, type=Path, help='Pinned native Z3 executable')
parser.add_argument('--library', required=True, type=Path, help='Source/DafnyStandardLibraries')
parser.add_argument('--output', required=True, type=Path, help='New private artifact directory')
parser.add_argument('--dotnet', default='dotnet')
parser.add_argument('--runs', type=int, default=5)
parser.add_argument('--jobs', type=int, default=1)
args = parser.parse_args()
if not 1 <= args.jobs <= 8 or args.runs < 5:
    parser.error('use at least five runs and between one and eight concurrent processes')
if not hasattr(os, 'wait4'):
    parser.error('CPU accounting currently requires POSIX wait4')
args.dafny = args.dafny.resolve()
args.solver = args.solver.resolve()
args.library = args.library.resolve()
args.output = args.output.resolve()
args.output.mkdir(parents=True, exist_ok=False)
environment = os.environ.copy()
barrier = threading.Barrier(args.runs)
slots = threading.Semaphore(args.jobs)
manifest = {'arguments': {k: str(v) if isinstance(v, Path) else v for k, v in vars(args).items()},
            'assemblySha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                               for p in sorted(args.dafny.parent.glob('*.dll'))},
            'sourceSha256': {str(p.relative_to(args.library)): hashlib.sha256(p.read_bytes()).hexdigest()
                             for p in sorted(args.library.rglob('*')) if p.suffix in ('.dfy', '.toml')}}
(args.output / 'manifest.json').write_text(json.dumps(manifest, indent=2))


def untimed(value):
    if isinstance(value, dict):
        result = {k: untimed(v) for k, v in value.items()
                  if k not in ('runTime', 'startTime', 'endTime', 'timingReplayed')}
        if 'vcResults' in result:
            # The logger retains asynchronous split completion order. Compare every
            # complete row and its multiplicity by identity, without dropping fields.
            result['vcResults'] = sorted(result['vcResults'], key=lambda row:
                (row['vcNum'], str(row.get('randomSeed', 0)), json.dumps(row, sort_keys=True)))
        return result
    if isinstance(value, list):
        return [untimed(v) for v in value]
    return value


def sample(number):
    destination = args.output / str(number)
    source = destination / 'library'
    shutil.copytree(args.library, source, ignore=shutil.ignore_patterns('bin', 'obj', 'build'))
    observations = {}
    rows = []
    try:
        for mode in ('cold', 'warm', 'edit'):
            barrier.wait(timeout=21600)
            if mode == 'edit':
                function = source / 'src/Std/Functions.dfy'
                original = function.read_text()
                before = 'forall x,y :: f(x,y) == f(y,x)'
                assert original.count(before) == 1
                function.write_text(original.replace(before, 'forall x,y :: f(y,x) == f(x,y)'))
            current_profile = destination / 'current.jsonl'
            current_log = destination / 'current.json'
            command = [args.dotnet, str(args.dafny), 'verify', 'src/Std/dfyconfig.toml',
                       '--cores', '1', '--solver-path', str(args.solver),
                       '--verification-time-limit', '0', '--boogie', '/deterministicLiteralHashes:1',
                       '--boogie', '/restartProver', '--verification-cache', str(destination / 'cache'),
                       '--pipeline-profile', str(current_profile),
                       '--log-format', 'json;LogFileName=' + str(current_log)]
            (destination / (mode + '.command.json')).write_text(json.dumps(command))
            with slots:
                start = time.monotonic()
                with (destination / (mode + '.stdout')).open('wb') as out, (destination / (mode + '.stderr')).open('wb') as err:
                    process = subprocess.Popen(command, cwd=source, env=environment, stdout=out, stderr=err)
                    _, status, usage = os.wait4(process.pid, 0)
                    process.returncode = os.waitstatus_to_exitcode(status)
                wall = time.monotonic() - start
            (destination / (mode + '.process.json')).write_text(json.dumps({
                'wall': wall, 'cpu': usage.ru_utime + usage.ru_stime,
                'exit': process.returncode}, indent=2))
            for suffix, path in (('jsonl', current_profile), ('json', current_log)):
                shutil.copyfile(path, destination / (mode + '.' + suffix))
            events = [json.loads(line) for line in current_profile.read_text().splitlines()]
            stats, = (event for event in events if event.get('ev') == 'result-cache')
            tasks = [event for event in events if event.get('ev') == 'result-task']
            assert tasks and all(task['executionKey'] is not None for task in tasks)
            assert stats['fallbacks'] == 0 and stats['bypassReason'] is None, stats
            observation = {'report': untimed(json.loads(current_log.read_text())),
                           'output': (process.returncode, (destination / (mode + '.stdout')).read_bytes(),
                                      (destination / (mode + '.stderr')).read_bytes()),
                           'keys': collections.Counter(task['executionKey'] for task in tasks)}
            if mode == 'warm':
                assert observation == observations['cold'], 'cold/warm report, output, exit or key mismatch'
                assert stats['solverStarts'] == stats['solverVersionProbes'] == 0, stats
                assert stats['hits'] == len(tasks), stats
            if mode == 'edit':
                changed = set(observation['keys']) - set(observations['cold']['keys'])
                actual = {task['executionKey'] for task in tasks if not task['replayed']}
                assert changed == actual, 'recomputed keys differ from changed complete task inputs'
                assert stats['solverStarts'] == sum(not task['replayed'] for task in tasks), stats
            observations[mode] = observation
            row = {'sample': number, 'mode': mode, 'wall': wall,
                   'cpu': usage.ru_utime + usage.ru_stime, 'exit': process.returncode,
                   'RU': sum(task['resourceCount'] for task in tasks), 'tasks': len(tasks), **stats}
            rows.append(row)
            (destination / (mode + '.measurement.json')).write_text(json.dumps(row, indent=2))
            print(json.dumps(row), flush=True)
    except BaseException:
        (destination / 'failure.json').write_text(json.dumps({
            'mode': mode, 'traceback': traceback.format_exc()}, indent=2))
        barrier.abort()
        raise
    return rows


with concurrent.futures.ThreadPoolExecutor(args.runs) as pool:
    rows = [row for group in pool.map(sample, range(args.runs)) for row in group]
summary = {}
for mode in ('cold', 'warm', 'edit'):
    observations = [row for row in rows if row['mode'] == mode]
    summary[mode] = {metric: {'median': statistics.median(values), 'minimum': min(values), 'maximum': max(values)}
                     for metric in ('wall', 'cpu', 'solverStarts', 'solverVersionProbes', 'RU')
                     for values in [[row[metric] for row in observations]]}
(args.output / 'measurements.json').write_text(json.dumps(rows, indent=2))
(args.output / 'summary.json').write_text(json.dumps(summary, indent=2))
print(json.dumps(summary, indent=2))
