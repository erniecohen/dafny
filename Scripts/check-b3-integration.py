#!/usr/bin/env python3
"""Run the real Dafny-to-worker corpus with explicit failure expectations."""
import argparse
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('dafny', type=Path)
parser.add_argument('worker', type=Path)
parser.add_argument('solver', type=Path)
parser.add_argument('--output', type=Path, default=Path('build/b3-integration'))
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
corpus = root / 'Source/IntegrationTests/TestFiles/B3'
args.output.mkdir(parents=True, exist_ok=True)
prefix = ['dotnet', str(args.dafny.resolve())] if args.dafny.suffix == '.dll' else [str(args.dafny.resolve())]
version = subprocess.run([str(args.solver.resolve()), '-version'], capture_output=True, text=True, timeout=10)
if version.returncode != 0 or version.stdout.strip() != 'Z3 version 5.1.0 - 64 bit':
    raise SystemExit('The B3 corpus requires Z3 5.1.0')
common = ['--verification-backend', 'b3', '--b3-worker', str(args.worker.resolve()),
          '--solver-path', str(args.solver.resolve()), '--cores', '1', '--resource-limit', '200000',
          '--verification-time-limit', '20', '--show-snippets:false', '--use-basename-for-filename',
          '--progress', 'Batch']
results = []

def run(name, source, expected_exit, diagnostic=None, extra=()):
    options = common.copy()
    for option in ('--verification-time-limit', '--b3-worker'):
        if option in extra:
            index = options.index(option)
            del options[index:index + 2]
    command = prefix + ['verify', str(source)] + options + list(extra)
    completed = subprocess.run(command, capture_output=True, text=True, timeout=120)
    text = completed.stdout + completed.stderr
    (args.output / (name + '.txt')).write_text(text)
    result = {'name': name, 'exitCode': completed.returncode, 'expectedExitCode': expected_exit,
              'diagnostic': diagnostic, 'passed': completed.returncode == expected_exit and
              (diagnostic is None or diagnostic in text)}
    if expected_exit == 0:
        # Successful preparation alone is insufficient: at least one complete unit must run.
        result['passed'] = result['passed'] and 'verified successfully' in text and 'resource count: unavailable' in text
    results.append(result)
    print(name, 'PASS' if result['passed'] else 'FAIL', completed.returncode, flush=True)

for case in json.loads((corpus / 'cases.json').read_text())['cases']:
    run(case['name'], corpus / case['file'], case['exitCode'], case['diagnostic'])
run('time-limit', corpus / 'true.dfy', 4, 'requires --verification-time-limit', ['--verification-time-limit', '0'])
run('isolation', corpus / 'true.dfy', 4, 'unsupported by B3', ['--isolate-assertions'])
run('filter-position', corpus / 'false.dfy', 1, 'does not currently support --filter-position', ['--filter-position', 'false.dfy:1'])
run('missing-worker', corpus / 'true.dfy', 4, 'worker package is unavailable or invalid', ['--b3-worker', str(args.output / 'absent.dll')])
run('symbol-filter', corpus / 'bad-precondition.dfy', 0, None, ['--filter-symbol', 'P'])
parse_source = args.output / 'parse-error.dfy'
parse_source.write_text('method Broken( {\n')
run('parse-error', parse_source, 2, 'parse errors detected')
(args.output / 'summary.json').write_text(json.dumps({'schemaVersion': 1, 'results': results}, indent=2) + '\n')
if not all(result['passed'] for result in results):
    raise SystemExit(1)
