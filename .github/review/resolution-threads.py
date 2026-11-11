#!/usr/bin/env python3
"""Measure opt-in resolution wall time, excluding parsing and process startup.

Run from the repository root, for example:
  python3 .github/review/resolution-threads.py --dafny 'dotnet Binaries/net10.0/DafnyDriver.dll' --out work/resolution-times

Each sample is a fresh process. The sidecar's outer resolve span includes global
admission, builtin publication, worker startup/join and deterministic output merge.
The three integration inputs have the most module declarations in the public lit
suite at the time of introduction (98, 57, 47); the last two intentionally report
resolution errors. This benchmark runs no solver.
"""
import argparse
import json
from pathlib import Path
import shlex
import statistics
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--runs', type=int, default=5)
    args = parser.parse_args()
    if args.runs < 5:
        parser.error('at least five runs are required')
    args.out.mkdir(parents=True, exist_ok=True)
    wide = args.out / 'wide.dfy'
    wide.write_text('module Base { function F(x: int): int { x + 1 } }\n' + ''.join(
        f'module M{i} {{ import B = Base\n' + ''.join(
            f'function F{j}(x: int): int {{ var t := (x, true, {j}); B.F(t.0) + t.2 }}\n'
            for j in range(64)) + '}\n' for i in range(64)))
    lit = Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues')
    inputs = {
        'stdlib': (['Source/DafnyStandardLibraries/src/Std/dfyconfig.toml'], 0),
        'modules-98': ([str(lit / 'github-issue-187.dfy')], 0),
        'modules-57': ([str(lit / 'git-issue-1180a.dfy')], 2),
        'modules-47': ([str(lit / 'git-issue-1996.dfy'), '--type-system-refresh=false', '--general-newtypes=false'], 2),
        'wide': ([str(wide)], 0),
    }
    summary = []
    for name, (files, expected_exit) in inputs.items():
        for threads in (1, 2, 4, 8):
            samples = []
            for run in range(args.runs):
                stem = args.out / f'{name}-{threads}-{run}'
                profile = stem.with_suffix('.jsonl')
                profile.unlink(missing_ok=True)
                command = shlex.split(args.dafny) + ['resolve', *files, '--allow-warnings',
                    '--resolution-threads', str(threads), '--pipeline-profile', str(profile)]
                with stem.with_suffix('.log').open('w') as output:
                    result = subprocess.run(command, stdout=output, stderr=subprocess.STDOUT, timeout=180)
                if result.returncode != expected_exit:
                    raise RuntimeError(f'{stem}: expected exit {expected_exit}, got {result.returncode}')
                events = [json.loads(line) for line in profile.read_text().splitlines()]
                span, = [e for e in events if e.get('stage') == 'resolve']
                samples.append(span['end'] - span['start'])
            row = dict(input=name, threads=threads, runs=args.runs, median_ms=statistics.median(samples),
                min_ms=min(samples), max_ms=max(samples), samples_ms=samples)
            summary.append(row)
            print(json.dumps(row), flush=True)
            (args.out / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')


if __name__ == '__main__':
    main()
