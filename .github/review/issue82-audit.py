#!/usr/bin/env python3
"""Check issue #82's packaged encoding, pipeline structure, and boundary controls."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


def sole_false_control(stdout, stderr, stem, line, verified):
    # Earlier proof errors and exhausted budgets cannot stand in for a live
    # assertion of false, even when the verifier's exit status is identical.
    output = stdout + stderr
    errors = re.findall(r'^.*Error:.*$', output, re.M)
    expected = re.escape(stem + '.dfy') + rf'\({line},\d+\): Error: assertion might not hold$'
    summary = rf'^Dafny program verifier finished with {verified} verified, 1 error$'
    return (len(errors) == 1 and re.search(expected, errors[0]) is not None and
            re.search(summary, stdout, re.M) is not None and
            re.search(r'out of resource|timed out|internal error|WARNING.*pattern', output, re.I) is None)


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

    def capture(name, command, expected=0, false_control=None):
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
                row['errors'] = re.findall(r'^.*Error:.*$', r.stdout + r.stderr, re.M)
                row['passed'] &= sole_false_control(r.stdout, r.stderr, *false_control)
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
            for previous in dest.glob('query-*.smt2'):
                previous.unlink()
            cmd += ['--solver-log', str(dest / 'query-@PROC@.smt2')]
        return cmd

    stem = 'git-issue-82-encoding'
    captures_passed = True
    for phase in ['raw', 'lifted', 'smt']:
        captures_passed &= capture(stem + '-' + phase, command(stem, phase))
    controls = [
        ('git-issue-82-mixed-negative', 7, 0),
        ('git-issue-82-maps-conditional-capture', 8, 1),
        ('git-issue-82-maps-local-capture', 12, 0),
        ('git-issue-82-maps-imap-capture', 8, 1),
    ]
    for control, line, verified in controls:
        captures_passed &= capture(control + '-smt', command(control, 'smt'), expected=4,
                                   false_control=(control, line, verified))
    queries = []
    for source in [stem] + [control for control, _, _ in controls]:
        source_queries = sorted((out / source).glob('query-*.smt2'))
        # A failed or absent solver capture must not silently reduce the audit.
        if not source_queries:
            captures_passed = False
            failed = True
        queries.extend(source_queries)
    report['solver_queries'] = [str(path.relative_to(out)) for path in queries]
    if captures_passed:
        structural = [sys.executable, str(scripts / 'issue82-encoding.py'),
                      '--boogie', str(out / stem / 'raw.bpl'),
                      '--lifted', str(out / stem / 'lifted.bpl')]
        for query in queries:
            structural += ['--smt', str(query)]
        structural += ['--output', str(out / 'structural.json')]
        capture('structural', structural)
    else:
        report['commands'].append({'name': 'structural', 'passed': False,
                                  'reason': 'a required solver capture failed'})
        failed = True
    report['passed'] = not failed
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2), flush=True)
    return int(failed)


if __name__ == '__main__':
    raise SystemExit(main())
