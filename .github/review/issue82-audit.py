#!/usr/bin/env python3
"""Check issue #82's packaged encoding, pipeline structure, and boundary controls."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--dafny', required=True)
    p.add_argument('--z3', required=True)
    p.add_argument('--output', required=True, type=Path)
    a = p.parse_args()
    out = a.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    binary = str(Path(a.dafny).resolve())
    solver = str(Path(a.z3).resolve())
    scripts = Path(__file__).resolve().parent
    inputs = Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs')
    report = {'scope': 'packaged finite-support translation and isolated conditional boundary', 'commands': []}
    failed = False

    def capture(name, command, expected=0, false_control=False):
        nonlocal failed
        dest = out / name
        dest.mkdir(parents=True, exist_ok=True)
        (dest / 'command.json').write_text(json.dumps(command, indent=2) + '\n')
        row = {'name': name, 'expected_exit': expected}
        try:
            r = subprocess.run(command, capture_output=True, text=True, timeout=180)
            (dest / 'stdout.txt').write_text(r.stdout)
            (dest / 'stderr.txt').write_text(r.stderr)
            row.update(exit=r.returncode, passed=r.returncode == expected)
            if false_control:
                # A source error, earlier failed proof, or exhausted budget cannot
                # stand in for the live assertion-of-false control.
                errors = re.findall(r'^.*Error:.*$', r.stdout + r.stderr, re.M)
                row['errors'] = errors
                row['passed'] &= (len(errors) == 1 and
                    re.search(r'git-issue-82-mixed-negative.dfy\(7,\d+\): Error: assertion might not hold', errors[0]) is not None and
                    'verifier finished with 0 verified, 1 error' in r.stdout)
        except subprocess.TimeoutExpired:
            row.update(exit='safety timeout', passed=False)
        report['commands'].append(row)
        failed |= not row['passed']
        return row['passed']

    for label, path in [('dafny', binary), ('z3', solver)]:
        r = subprocess.run([path, '--version'], capture_output=True, text=True, timeout=10)
        report[label] = {'version': (r.stdout + r.stderr).strip(),
                         'sha256': hashlib.sha256(Path(path).read_bytes()).hexdigest()}
        failed |= r.returncode != 0
    if not report['z3']['version'].startswith('Z3 version 5.1.0'):
        raise RuntimeError('The boundary audit requires the pinned Z3 5.1.0')
    capture('parser-controls', [sys.executable, str(scripts / 'issue82-encoding.py'), '--self-test'])
    capture('boundary', [sys.executable, str(scripts / 'issue82-boundary.py'), '--z3', solver, '--output', str(out / 'boundary-results')])

    def command(stem, phase):
        dest = out / stem
        dest.mkdir(exist_ok=True)
        cmd = [binary, 'verify', str(inputs / (stem + '.dfy')), '--solver-path', solver,
               '--cores', '1', '--additional-axioms=false', '--type-system-refresh=false',
               '--resource-limit', '2000000', '--verification-time-limit', '0',
               '--show-snippets=false', '--use-basename-for-filename', '--allow-warnings',
               '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/normalizeNames:0',
               '--bprint', str(dest / (phase + '.bpl'))]
        if phase != 'smt':
            cmd += ['--boogie', '/proc:__NoMatches__']
        if phase == 'lifted':
            cmd += ['--boogie', '/printLambdaLifting']
        if phase == 'smt':
            cmd += ['--solver-log', str(dest / 'query.smt2')]
        return cmd

    stem = 'git-issue-82-encoding'
    for phase in ['raw', 'lifted', 'smt']:
        capture(stem + '-' + phase, command(stem, phase))
    mixed = 'git-issue-82-mixed-negative'
    capture(mixed + '-smt', command(mixed, 'smt'), expected=4, false_control=True)
    capture('structural', [sys.executable, str(scripts / 'issue82-encoding.py'),
                          '--boogie', str(out / stem / 'raw.bpl'),
                          '--lifted', str(out / stem / 'lifted.bpl'),
                          '--smt', str(out / stem / 'query.smt2'),
                          '--smt', str(out / mixed / 'query.smt2'),
                          '--output', str(out / 'structural.json')])
    report['passed'] = not failed
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2), flush=True)
    return int(failed)


if __name__ == '__main__':
    raise SystemExit(main())
