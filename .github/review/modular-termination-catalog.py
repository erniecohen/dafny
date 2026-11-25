#!/usr/bin/env python3
"""Check embedded checked-Std selection and erasure after producing and embedding its catalog.

Usage: modular-termination-catalog.py DAFNY Z3 OUT [--cores N]
Proofs use the complete certificates. Backend-only runs skip re-verification of the
already checked five-family control; they do not produce certificates.
"""
import argparse
import json
from pathlib import Path
import subprocess

from catalog_erasure import check_ghost_erasure

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('dafny')
parser.add_argument('solver')
parser.add_argument('output')
parser.add_argument('--cores', type=int, default=3)
args = parser.parse_args()
if args.cores < 1:
    parser.error('--cores must be positive')
binary, solver, directory = [str(Path(p).resolve()) for p in [args.dafny, args.solver, args.output]]
root = Path(__file__).resolve().parents[2]
out = Path(directory)
out.mkdir(parents=True, exist_ok=True)
launcher = ['dotnet', binary] if binary.endswith('.dll') else [binary]
options = ['--standard-libraries:true', '--solver-path', solver,
    '--type-system-refresh', '--general-traits=datatype', '--general-newtypes',
    '--reads-clauses-on-methods', '--enforce-determinism', '--track-print-effects',
    '--cores', str(args.cores), '--resource-limit', '16000000', '--use-basename-for-filename']
ghost = '--modular-termination-trust:standard-library'
runtime = '--modular-termination-runtime-trust:standard-library'
rows = []


def run(name, arguments, expected=0, contains=()):
    result = subprocess.run(launcher + arguments, cwd=out, capture_output=True, text=True)
    text = result.stdout + result.stderr
    (out / (name + '.log')).write_text(text)
    accepted = result.returncode == expected and all(fragment in text for fragment in contains)
    rows.append({'case': name, 'exit': result.returncode, 'accepted': accepted})
    print(name + ': ' + ('PASS' if accepted else 'FAIL'), flush=True)
    assert accepted, text
    assert not any(s in text.lower() for s in ['out of resource', 'timed out', 'unhandled exception']), text
    return text


try:
    client = out / 'client.dfy'
    client.write_text('module Client { import opened Std.Wrappers method Use() { assert Some(1).value == 1; } }')
    text = run('legacy-catalog-default', ['verify'] + options + [str(client)])
    assert 'these results assume' not in text
    client.write_text('module Client { import Std.ActionsExterns method Use() { '
        'var producer, proof := ActionsExterns.MakeSetReader({7}); '
        'assert proof.Valid(); assert proof.Producer() == producer; '
        'assert producer.allowance <= proof.allowance; proof.ProducesSet([]); // CONTROL\n} }')
    source = client.read_text()
    run('catalog-no-runtime-opt-in', ['verify'] + options + [ghost, str(client)], 1, [runtime])
    run('catalog-no-ghost-opt-in', ['verify'] + options + [runtime, str(client)], 1, [ghost])
    run('catalog-client', ['verify'] + options + [ghost, runtime, str(client)],
        contains=['Std.ActionsExterns.MakeSetReader', 'including termination'])
    client.write_text(source.replace('// CONTROL', 'assert false;'))
    text = run('catalog-false', ['verify'] + options + [ghost, runtime, str(client)], 4,
        ['Error: assertion might not hold', '1 error'])
    assert text.count('Error: assertion might not hold') == 1
    families = root / 'Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-211-checked-std-five-families.dfy'
    for target in ['cs', 'java', 'js', 'go', 'py']:
        destination = out / target
        destination.mkdir(exist_ok=True)
        run(target + '-erasure', ['translate', target] + options + [ghost, runtime,
            '--no-verify', '--output:' + str(destination / 'families'), str(families)])
        files = [p for p in destination.rglob('*') if p.is_file()]
        assert files, target + ': no generated files'
        for path in files:
            check_ghost_erasure(path, target)
    run('cs-execution', ['run'] + options + [ghost, runtime, '--no-verify',
        '--output:' + str(out / 'execute'), str(families)], contains=['five families exercised'])
finally:
    (out / 'results.json').write_text(json.dumps(rows, indent=2) + '\n')
    (out / 'summary.md').write_text('# Embedded catalog and backend controls\n\n' +
        '\n'.join('- ' + row['case'] + ': ' + ('PASS' if row['accepted'] else 'FAIL') for row in rows) + '\n')
