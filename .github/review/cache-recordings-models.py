#!/usr/bin/env python3
"""Exercise native and replayed model reports after completed recording release."""
import hashlib
import json
from pathlib import Path
import runpy
import subprocess
import sys

exe, solver, source, destination = sys.argv[1:]
digest = hashlib.sha256(Path(solver).read_bytes()).hexdigest()
original_run = subprocess.run


def run(args, *positional, **keywords):
    if isinstance(args, (list, tuple)) and len(args) > 2 and args[2] == 'verify':
        args = list(args) + ['--release-cache-recordings']
        if '--verification-cache' in args:
            args += ['--verification-cache-solver-digest', digest]
        if '--cores' in args:
            args[args.index('--cores') + 1] = '2'
        keywords['timeout'] = min(keywords.get('timeout', 110), 110)
    return original_run(args, *positional, **keywords)


subprocess.run = run
helper = Path(__file__).with_name('persistent-models.py')
sys.argv = [str(helper), exe, solver, source, destination, '--smoke']
try:
    runpy.run_path(str(helper), run_name='__main__')
except SystemExit as stopped:
    if stopped.code != 0:
        raise
finally:
    subprocess.run = original_run

events = [json.loads(line) for line in (Path(destination) / 'cold.jsonl').read_text().splitlines()]
activation = next(e for e in events if e['ev'] == 'result-cache')
assert activation['releasedRecordings'] > 0 and activation['releasedReporters'] > 0, 'recording release was not activated'
print('Recording release activated; native and replayed counterexample reports match')
