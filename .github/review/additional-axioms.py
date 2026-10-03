#!/usr/bin/env python3
"""Enforce separate axiom verdicts and report OFF/ON resources from existing runners."""
import argparse
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import sys


def runner(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '-verdicts.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def inventory(directory, output):
    batches = {}
    programs = []
    unavailable = {}
    for command in sorted(Path(directory).rglob('command.json')):
        program = command.parent.relative_to(directory).as_posix()
        programs.append(program)
        log = command.with_name('results.json')
        if not log.exists() or not log.read_text().strip():
            # A pre-verification diagnostic may leave an empty logger file (for
            # example an invalid second logger). Keep the missing observation
            # visible; the independent verdict gate still checks that program.
            unavailable[program] = 'empty log' if log.exists() else 'no log'
            continue
        for declaration in json.loads(log.read_text()).get('verificationResults', []):
            for batch in declaration['vcResults']:
                key = program + '\t' + declaration['name'] + '\t' + str(batch['vcNum'])
                if key in batches:
                    raise ValueError('duplicate proof batch: ' + key)
                batches[key] = [batch['outcome'], batch['resourceCount']]
    Path(output).write_text(json.dumps({'programs': programs, 'batches': batches, 'unavailable': unavailable}, indent=2))


def gate(kind, expected, actual, mode, output):
    out = Path(output)
    out.mkdir(parents=True, exist_ok=True)
    probe = mode == 'on' and os.environ.get('PROBE') == 'true'
    if probe and not os.environ.get('GITHUB_REF', '').startswith('refs/heads/scratch/'):
        raise ValueError('Baseline probes require a scratch branch')
    module = runner('lit' if kind == 'suite' else 'std')
    keys = [line.split('\t')[0] for line in Path(actual).read_text().splitlines() if line.strip()]
    if len(keys) != len(set(keys)):
        raise ValueError('Duplicate verdict rows')
    capture = io.StringIO()
    with contextlib.redirect_stdout(capture):
        if Path(expected).is_file():
            differences = module.compare(expected, actual)
        else:
            print('Missing expected verdict file:', expected)
            off_baseline = Path(str(expected).replace('-additional-axioms.tsv', '.tsv'))
            if probe and off_baseline.is_file():
                print('Initial comparison with existing OFF baseline:')
                module.compare(off_baseline, actual)
            differences = 1
    with contextlib.redirect_stdout(io.StringIO()) as baseline:
        module.expected([actual] if kind == 'suite' else actual)
    (out / Path(expected).name).write_text(baseline.getvalue())
    report = {'kind': kind, 'mode': mode, 'probe': probe, 'differences': differences,
              'source': os.environ.get('GITHUB_SHA', ''), 'comparison': capture.getvalue()}
    (out / 'report.json').write_text(json.dumps(report, indent=2))
    (out / 'comparison.txt').write_text(capture.getvalue())
    message = ('Baseline probe; no acceptance claim.\n' if probe else '') + capture.getvalue()
    print(message)
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
            summary.write('\n### ' + kind + ', axioms ' + mode + '\n\n```\n' + message + '```\n')
    return 0 if probe or not differences else 1


def pair(off, on):
    return [{'batch': key, 'off': off.get(key), 'on': on.get(key),
             'delta': on[key][1] - off[key][1] if key in off and key in on else None}
            for key in sorted(set(off) | set(on))]


def resources(directory, output):
    root, out = Path(directory), Path(output)
    out.mkdir(parents=True, exist_ok=True)
    suite = {}
    counts = {}
    for mode in ['off', 'on']:
        batches, programs, unavailable = {}, set(), {}
        files = sorted(root.glob('verdicts-' + mode + '-*/resources-*.json'))
        if len(files) != 4:
            raise ValueError('Expected four resource shards for ' + mode)
        for file in files:
            data = json.loads(file.read_text())
            if programs.intersection(data['programs']) or batches.keys() & data['batches'].keys():
                raise ValueError('Overlapping resource shards')
            programs.update(data['programs'])
            batches.update(data['batches'])
            unavailable.update(data.get('unavailable', {}))
        suite[mode] = batches
        counts[mode] = {'programs': len(programs), 'batches': len(batches), 'unavailable': unavailable}
    report = {'source': os.environ.get('GITHUB_SHA', ''),
              'solver_archive_sha256': {
                  '5.1.0': os.environ.get('Z3_LINUX_X64_SHA256', ''),
                  '4.12.1': os.environ.get('HARNESS_Z3_4_12_1_SHA256', '')},
              'suite_coverage': counts,
              'suite': pair(suite['off'], suite['on']), 'library': {}}
    std = runner('std')
    for version in ['4.12.1', '5.1.0']:
        modes = {}
        for mode in ['off', 'on']:
            rows = std.load(root / ('std-verdicts-' + mode + '-z3-' + version) / 'std-verdicts.tsv')
            modes[mode] = {key: [row[1], int(row[4])] for key, row in rows.items()
                           if not key.startswith('run ')}
        report['library'][version] = pair(modes['off'], modes['on'])
    (out / 'resources.json').write_text(json.dumps(report, indent=2))
    lines = ['# Additional-axiom resources', '',
             'Same source and solver, option ON versus OFF; resource units, not elapsed time.',
             'Suite declaration order and resource caps are unchanged. Parser/resolver diagnostics have no proof batches.',
             'Library counts use its existing declaration order and can vary between unchanged-build runs (#30).',
             'These paired library observations do not establish exact controlled cost differences.',
             'Cost changes are reported without a failure threshold. Verdict changes are checked by separate gates.',
             'Accepted MinimumWindowMax costs remain tracked in #78; no limits or proof sources are changed.', '',
             '| Mode | Suite programs | Proof batches | Missing/empty logs |', '|---|---:|---:|---:|']
    for mode, count in counts.items():
        lines.append(f"| {mode} | {count['programs']} | {count['batches']} | {len(count['unavailable'])} |")
    sections = {'suite (Z3 5.1.0)': report['suite'], **{'library Z3 ' + v: rows for v, rows in report['library'].items()}}
    for label, rows in sections.items():
        changed = [r for r in rows if r['off'] != r['on']]
        lines += ['', '## ' + label, '', f'{len(rows)} entries; {len(changed)} differ in outcome or resources.', '',
                  'Largest increases among matching valid outcomes (all observations are in the artifact):', '',
                  '| Batch/declaration | OFF RU | ON RU | Increase |', '|---|---:|---:|---:|']
        increases = [r for r in rows if r['off'] and r['on'] and r['off'][0] == r['on'][0]
                     and r['on'][0] in ['Valid', 'Correct'] and r['delta'] > 0]
        for row in sorted(increases, key=lambda r: -r['delta'])[:20]:
            key = row['batch'].replace('\t', ' / ').replace('|', '\\|')
            lines.append(f"| `{key}` | {row['off'][1]} | {row['on'][1]} | {row['delta']} |")
    text = '\n'.join(lines) + '\n'
    (out / 'summary.md').write_text(text)
    print(text)
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
            summary.write(text)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    inv = commands.add_parser('inventory')
    inv.add_argument('directory'); inv.add_argument('output')
    res = commands.add_parser('resources')
    res.add_argument('directory'); res.add_argument('output')
    check = commands.add_parser('gate')
    check.add_argument('kind', choices=['suite', 'std'])
    check.add_argument('expected'); check.add_argument('actual')
    check.add_argument('--mode', choices=['off', 'on'], required=True)
    check.add_argument('--output', required=True)
    args = vars(parser.parse_args())
    command = args.pop('command')
    return {'inventory': inventory, 'resources': resources, 'gate': gate}[command](**args)


if __name__ == '__main__':
    sys.exit(main())
