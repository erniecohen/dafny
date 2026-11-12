#!/usr/bin/env python3
"""Record the corrected/legacy comparison, including individual proof outcomes and RU."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]


def runner(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '-verdicts.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def inventory(out):
    declarations, batches, missing = {}, {}, []
    for command in sorted((out / 'measurements').rglob('command.json')):
        name = command.parent.relative_to(out / 'measurements').as_posix()
        log = command.with_name('results.json')
        if not log.exists() or not log.read_text().strip():
            missing.append(name)
            continue
        for decl in json.loads(log.read_text())['verificationResults']:
            key = name + ': ' + decl['name']
            if key in declarations:
                raise ValueError('duplicate declaration ' + key)
            declarations[key] = decl['outcome']
            for vc in decl['vcResults']:
                batches[key + ': ' + str(vc['vcNum'])] = [vc['outcome'], vc['resourceCount']]
    (out / 'proofs.json').write_text(json.dumps({
        'declarations': declarations, 'batches': batches, 'no_verification_log': missing,
        'resource_units': sum(b[1] for b in batches.values())}, indent=2))


def record(args):
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=True)
    module = runner('lit' if args.kind == 'suite' else 'std')
    if args.mode == 'legacy':
        module.FIXED = module.FIXED + ['--legacy-decreases-order']
    dafny, z3 = str(Path(args.dafny).resolve()), str(Path(args.z3).resolve())
    (out / 'source.json').write_text(json.dumps({
        'source': os.environ.get('GITHUB_SHA', ''), 'mode': args.mode,
        'axioms': args.axioms, 'solver_sha256': hashlib.sha256(Path(z3).read_bytes()).hexdigest()}, indent=2))
    if args.kind == 'suite':
        litdir = ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest'
        module.plan(litdir, out / 'plan.tsv')
        module.run(out / 'plan.tsv', str(litdir), dafny, z3, out / 'verdicts.tsv',
                   shard=args.shard, jobs=4, additional_axioms=args.axioms == 'on',
                   measurements=out / 'measurements')
        inventory(out)
    else:
        module.run(str(ROOT / 'Source/DafnyStandardLibraries'), dafny, z3, out / 'verdicts.tsv',
                   cores=4, additional_axioms=args.axioms == 'on',
                   boogie='/deterministicLiteralHashes:1 /restartProver', measurements=str(out / 'measurements'))


def compare(args):
    inp, out = Path(args.input), Path(args.output)
    out.mkdir(parents=True, exist_ok=True)
    report, text = {}, ['# Decreases-order comparison', '', 'All differences are default minus legacy; measurements are Z3 resource units.', '']
    for group in ['suite-off', 'suite-on', 'std-off-5.1.0', 'std-on-5.1.0', 'std-off-4.12.1', 'std-on-4.12.1']:
        data = {}
        for mode in ['legacy', 'default']:
            dirs = sorted(path for path in inp.glob(group + '-' + mode + '*') if path.is_dir())
            if not dirs:
                raise ValueError('missing measurements: ' + group + '-' + mode)
            rows, declarations, units = {}, {}, 0
            for directory in dirs:
                for line in (directory / 'verdicts.tsv').read_text().splitlines():
                    row = line.split('\t')
                    if row[0] in rows:
                        raise ValueError('duplicate verdict ' + row[0])
                    rows[row[0]] = row[1:4]
                    if group.startswith('std') and not row[0].startswith('run '):
                        declarations[row[0]] = row[1]
                        units += int(row[4])
                if group.startswith('suite'):
                    proofs = json.loads((directory / 'proofs.json').read_text())
                    overlap = declarations.keys() & proofs['declarations'].keys()
                    if overlap:
                        raise ValueError('duplicate declarations ' + str(overlap))
                    declarations.update(proofs['declarations'])
                    units += proofs['resource_units']
            data[mode] = dict(rows=rows, declarations=declarations, resource_units=units)
            suffix = '-additional-axioms' if '-on' in group else ''
            baseline = ('expected-verdicts' if group.startswith('suite') else 'expected-std-verdicts-z3-' + group.split('-')[-1]) + suffix + '.tsv'
            if mode == 'default':
                (out / baseline).write_text(''.join('\t'.join([name] + row) + '\n' for name, row in sorted(rows.items())))
        a, b = data['legacy'], data['default']
        differences = {name: {'legacy': a['rows'].get(name), 'default': b['rows'].get(name)}
                       for name in sorted(a['rows'].keys() | b['rows'].keys()) if a['rows'].get(name) != b['rows'].get(name)}
        changes = {name: {'legacy': a['declarations'].get(name), 'default': b['declarations'].get(name)}
                   for name in sorted(a['declarations'].keys() | b['declarations'].keys()) if a['declarations'].get(name) != b['declarations'].get(name)}
        report[group] = {'row_changes': differences, 'declaration_changes': changes,
                         'legacy_ru': a['resource_units'], 'default_ru': b['resource_units'],
                         'delta_ru': b['resource_units'] - a['resource_units'],
                         'legacy_declarations': len(a['declarations']), 'default_declarations': len(b['declarations'])}
        text += ['## ' + group, '', f"{len(differences)} verdict rows and {len(changes)} declaration outcomes change.",
                 f"RU: {a['resource_units']:,} → {b['resource_units']:,} ({b['resource_units'] - a['resource_units']:+,}).", '']
        for name, change in differences.items():
            text += [f"- `{name}`: `{change['legacy']}` → `{change['default']}`"]
        text += ['', 'Declaration outcomes:', '']
        for name, change in changes.items():
            text += [f"- `{name}`: {change['legacy']} → {change['default']}"]
        if not changes:
            text += ['None.']
        text += ['']
    (out / 'report.json').write_text(json.dumps(report, indent=2))
    (out / 'summary.md').write_text('\n'.join(text))
    print('\n'.join(text))


def identity(args):
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=True)
    fixtures = ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs'
    report = {}
    for name in ['real', 'false', 'mixed', 'm2', 'positive', 'negative', 'callback']:
        results = []
        for mode, dafny, extra in [('baseline', args.baseline, []), ('legacy', args.dafny, ['--legacy-decreases-order'])]:
            bpl, smt = out / 'program.bpl', out / 'program.smt2'
            for path in out.glob('program*'):
                path.unlink()
            cmd = [str(Path(dafny).resolve()), 'verify', str(fixtures / f'git-issue-264-{name}.dfy'),
                   '--solver-path', str(Path(args.z3).resolve()), '--cores:1', '--show-snippets:false',
                   '--use-basename-for-filename', '--bprint', str(bpl), '--solver-log', str(smt)]
            if name in ['m2', 'callback']:
                cmd += ['--modular-termination']
            p = subprocess.run(cmd + extra, capture_output=True, timeout=120)
            paths = sorted(out.glob('program*'))
            content = {path.name: path.read_bytes() for path in paths}
            results.append((p.returncode, p.stdout, p.stderr, content))
            (out / f'{name}-{mode}.stdout').write_bytes(p.stdout)
            (out / f'{name}-{mode}.stderr').write_bytes(p.stderr)
            for path in paths:
                shutil.copyfile(path, out / f'{name}-{mode}-{path.name}')
        report[name] = dict(equal=results[0] == results[1], baseline_exit=results[0][0], legacy_exit=results[1][0])
    (out / 'identity.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(report, indent=2))
    return 0 if all(case['equal'] for case in report.values()) else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    p = sub.add_parser('record')
    p.add_argument('kind', choices=['suite', 'std'])
    p.add_argument('--mode', choices=['default', 'legacy'], required=True)
    p.add_argument('--axioms', choices=['off', 'on'], default='off')
    p.add_argument('--shard', default='0/1')
    p.add_argument('--dafny', required=True)
    p.add_argument('--z3', required=True)
    p.add_argument('--output', required=True)
    p = sub.add_parser('compare')
    p.add_argument('--input', required=True)
    p.add_argument('--output', required=True)
    p = sub.add_parser('identity')
    p.add_argument('--baseline', required=True)
    p.add_argument('--dafny', required=True)
    p.add_argument('--z3', required=True)
    p.add_argument('--output', required=True)
    args = parser.parse_args()
    sys.exit({'record': record, 'compare': compare, 'identity': identity}[args.command](args) or 0)
