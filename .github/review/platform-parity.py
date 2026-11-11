#!/usr/bin/env python3
"""Compare complete fixed-source Linux/macOS probe artifacts; never update baselines.

platform-parity.py ARTIFACT_DIRECTORY RUN_JSON OUTPUT_DIRECTORY
RUN_JSON is `gh run view --json headSha,url,jobs`. Download artifacts without merging
artifact directories. Missing jobs, rows, proof batches or test results fail closed.
"""
import argparse
from collections import defaultdict
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def tsv(path):
    rows = [line.split('\t') for line in path.read_text().splitlines() if line.strip()]
    result = {row[0]: row for row in rows}
    if len(result) != len(rows):
        raise ValueError('Duplicate rows: ' + str(path))
    return result


def compare_rows(left, right):
    return [{'name': key, 'linux': left.get(key), 'macos': right.get(key)}
            for key in sorted(left.keys() | right.keys()) if left.get(key) != right.get(key)]


def suite(root, platform, mode):
    verdicts, resources, missing, batches = {}, {}, {}, {}
    plan = None
    for shard in range(4):
        folder = root / f'parity-{platform}-verdicts-{mode}-{shard}'
        current_plan = tsv(folder / 'plan.tsv')
        if plan is not None and current_plan != plan:
            raise ValueError('Shard plans differ')
        plan = current_plan
        rows = tsv(folder / f'verdicts-{mode}-{shard}.tsv')
        if verdicts.keys() & rows.keys():
            raise ValueError('Overlapping suite shards')
        verdicts.update({key: row[:4] for key, row in rows.items()})
        data = json.loads((folder / f'resources-{mode}-{shard}.json').read_text())
        if set(data['programs']) != set(rows) or batches.keys() & data['batches'].keys():
            raise ValueError('Resource coverage differs from verdict coverage')
        batches.update(data['batches'])
        missing.update(data['unavailable'])
        totals = defaultdict(int)
        for key, value in data['batches'].items():
            totals[key.split('\t')[0]] += value[1]
        for key in rows:
            resources[key] = None if key in data['unavailable'] else totals[key]
    if set(plan) != set(verdicts):
        raise ValueError('Suite does not cover its complete plan')
    return verdicts, resources, {'plan': plan, 'batches': batches, 'unavailable': missing}


def library(root, platform, mode, version):
    rows = tsv(root / f'parity-{platform}-std-verdicts-{mode}-z3-{version}' / 'std-verdicts.tsv')
    if len([key for key in rows if key.startswith('run ')]) != 7:
        raise ValueError('Missing standard-library parts')
    if any(row[1] == 'TIMEOUT' for row in rows.values()):
        raise ValueError('Incomplete standard-library run')
    return ({key: row[:4] for key, row in rows.items()},
            {key: int(row[4]) for key, row in rows.items() if not key.startswith('run ')})


def test_results(root, platform, name, expected_files):
    paths = list((root / f'parity-{platform}-{name}').glob('*.trx'))
    if len(paths) != expected_files:
        raise ValueError(f'Expected {expected_files} test result files for {platform} {name}, got {len(paths)}')
    result = {}
    for path in paths:
        rows = ET.parse(path).findall('.//{*}UnitTestResult')
        if not rows:
            raise ValueError('Empty test result file: ' + str(path))
        for row in rows:
            key = path.name + '\t' + row.attrib['testName']
            if key in result:
                raise ValueError('Duplicate test result: ' + key)
            result[key] = row.attrib['outcome']
    return result


def resource_report(left, right):
    changes = compare_rows(left, right)
    numeric = [row for row in changes if isinstance(row['linux'], int) and isinstance(row['macos'], int)]
    for row in numeric:
        row['delta'] = row['macos'] - row['linux']
    return {'entries': len(left), 'differing': len(changes),
            'max_absolute_difference': max((abs(row['delta']) for row in numeric), default=0),
            'unavailable_linux': sum(v is None for v in left.values()),
            'unavailable_macos': sum(v is None for v in right.values()), 'differences': changes}


def compare(root, run):
    report = {'source': run['headSha'], 'run': run['url'], 'complete': False,
              'suite': {}, 'library': {}, 'tests': {}, 'job_seconds': {}, 'job_failures': []}
    for job in run['jobs']:
        if not job['name'].startswith('Parity '):
            continue
        failed = [step['name'] for step in job['steps'] if step['conclusion'] in ['failure', 'cancelled']]
        if job['conclusion'] != 'success' or failed:
            report['job_failures'].append({'job': job['name'], 'conclusion': job['conclusion'], 'steps': failed})
        if job.get('startedAt') and job.get('completedAt'):
            from datetime import datetime
            report['job_seconds'][job['name']] = (datetime.fromisoformat(job['completedAt'].replace('Z', '+00:00')) -
                                                      datetime.fromisoformat(job['startedAt'].replace('Z', '+00:00'))).total_seconds()
    for mode in ['off', 'on']:
        a, ar, ac = suite(root, 'linux', mode)
        b, br, bc = suite(root, 'macos', mode)
        if ac['plan'] != bc['plan']:
            raise ValueError('Platform plans differ')
        report['suite'][mode] = {'verdict_differences': compare_rows(a, b), 'resources': resource_report(ar, br),
                                'batch_differences': compare_rows(ac['batches'], bc['batches']),
                                'unavailable_differences': compare_rows(ac['unavailable'], bc['unavailable'])}
        for version in ['5.1.0', '4.12.1']:
            a, ar = library(root, 'linux', mode, version)
            b, br = library(root, 'macos', mode, version)
            report['library'][mode + '-z3-' + version] = {
                'verdict_differences': compare_rows(a, b), 'resources': resource_report(ar, br)}
    for name, count in [('regressions', 10), ('resolver-tests', 3)]:
        try:
            a = test_results(root, 'linux', name, count)
            b = test_results(root, 'macos', name, count)
            report['tests'][name] = {'linux': len(a), 'macos': len(b), 'differences': compare_rows(a, b),
                                     'nonpassing': {k: v for k, v in b.items() if v != 'Passed'}}
        except (OSError, ValueError) as error:
            report['tests'][name] = {'incomplete': str(error)}
    # Unit-test/harness infrastructure failures do not change a complete proof comparison.
    # They independently prevent moving those control jobs.
    proof_failures = [item for item in report['job_failures'] if any(
        label in item['job'] for label in ['Build (Release)', 'Verifier suite', 'Standard library'])]
    report['complete'] = not proof_failures
    sections = list(report['suite'].values()) + list(report['library'].values())
    if not report['complete']:
        report['decision'] = 'incomplete: keep gates on Linux'
    elif any(item['verdict_differences'] for item in sections):
        report['decision'] = 'verdicts differ: keep gates on Linux'
    elif any(item['resources']['differing'] for item in sections) or any(
            item['batch_differences'] or item['unavailable_differences'] for item in report['suite'].values()):
        report['decision'] = 'resources differ: STOP before switching gates; owner decision required'
    else:
        report['decision'] = 'verdict and resource parity: eligible to route gates to macOS'
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('artifacts', type=Path)
    parser.add_argument('run', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    try:
        report = compare(args.artifacts, json.loads(args.run.read_text()))
    except (OSError, ValueError, KeyError) as error:
        report = {'complete': False, 'decision': 'incomplete: keep gates on Linux', 'error': str(error)}
    (args.output / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    lines = ['# Platform parity', '', report['decision'], '']
    if report.get('error'):
        lines += [report['error']]
    for kind in ['suite', 'library']:
        for label, item in report.get(kind, {}).items():
            r = item['resources']
            lines += [f"{kind} {label}: {len(item['verdict_differences'])} verdict differences; "
                      f"{r['differing']} resource differences among {r['entries']} entries; "
                      f"maximum absolute difference {r['max_absolute_difference']} RU."]
    lines += ['', '| Job | Wall seconds |', '|---|---:|']
    for name, seconds in sorted(report.get('job_seconds', {}).items()):
        lines.append(f'| {name} | {seconds:.0f} |')
    text = '\n'.join(lines) + '\n'
    (args.output / 'summary.md').write_text(text)
    print(text)
    return 0 if report.get('complete') else 1


if __name__ == '__main__':
    raise SystemExit(main())
