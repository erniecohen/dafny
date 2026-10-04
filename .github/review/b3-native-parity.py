#!/usr/bin/env python3
"""Scratch-only paired default Boogie compatibility report; mismatches are observations."""
import json
from pathlib import Path
import sys


def load_inventory(paths):
    programs, batches, unavailable = set(), {}, {}
    for path in paths:
        data = json.loads(path.read_text())
        if programs.intersection(data['programs']) or batches.keys() & data['batches'].keys():
            raise ValueError('overlapping resource shards')
        programs.update(data['programs'])
        batches.update(data['batches'])
        unavailable.update(data.get('unavailable', {}))
    return {'programs': sorted(programs), 'batches': batches, 'unavailable': unavailable}


def load_rows(paths):
    rows = {}
    for path in paths:
        for line in path.read_text().splitlines():
            columns = line.split('\t')
            if columns[0] in rows:
                raise ValueError('duplicate verdict: ' + columns[0])
            rows[columns[0]] = columns
    return rows


def differences(before, after):
    return {key: {'baseline': before.get(key), 'candidate': after.get(key)}
            for key in sorted(before.keys() | after.keys()) if before.get(key) != after.get(key)}


def compare(root):
    baseline_paths = sorted(root.glob('native-baseline-*/resources-baseline-*.json'))
    candidate_paths = sorted(root.glob('verdicts-off-*/resources-off-*.json'))
    if len(baseline_paths) != 4 or len(candidate_paths) != 4:
        raise ValueError('expected four baseline and four candidate default resource shards')
    baseline = load_inventory(baseline_paths)
    candidate = load_inventory(candidate_paths)
    baseline_verdicts = load_rows(sorted(root.glob('native-baseline-*/baseline-verdicts-*.tsv')))
    candidate_verdicts = load_rows(sorted(root.glob('verdicts-off-*/verdicts-off-*.tsv')))
    if set(baseline_verdicts) != set(baseline['programs']) or set(candidate_verdicts) != set(candidate['programs']):
        raise ValueError('verdict and resource coverage differ')
    suite_verdicts = differences({key: row[:4] for key, row in baseline_verdicts.items()},
                                {key: row[:4] for key, row in candidate_verdicts.items()})
    baseline_std_path = root / 'std-verdicts-off-z3-5.1.0' / 'baseline-std-verdicts.tsv'
    candidate_std_path = root / 'std-verdicts-off-z3-5.1.0' / 'std-verdicts.tsv'
    std_baseline = load_rows([baseline_std_path])
    std_candidate = load_rows([candidate_std_path])
    std_verdict = lambda rows: {key: row[:4] if key.startswith('run ') else row[:2] for key, row in rows.items()}
    std_resource = lambda rows: {key: row[4:6] for key, row in rows.items() if not key.startswith('run ')}
    return {'complete': True,
            'suite': {'baseline_programs': len(baseline_verdicts), 'candidate_programs': len(candidate_verdicts),
                      'baseline_batches': len(baseline['batches']), 'candidate_batches': len(candidate['batches']),
                      'verdict_changes': suite_verdicts,
                      'resource_changes': differences(baseline['batches'], candidate['batches']),
                      'unavailable_changes': differences(baseline['unavailable'], candidate['unavailable']),
                      'baseline_unavailable': baseline['unavailable'], 'candidate_unavailable': candidate['unavailable']},
            'library': {'baseline_declarations': len(std_resource(std_baseline)),
                        'candidate_declarations': len(std_resource(std_candidate)),
                        'verdict_changes': differences(std_verdict(std_baseline), std_verdict(std_candidate)),
                        'resource_changes': differences(std_resource(std_baseline), std_resource(std_candidate))}}


def main(root, output):
    output.mkdir(parents=True, exist_ok=True)
    try:
        report = compare(root)
        report['default_parity'] = all(not report[scope][kind]
            for scope, kinds in [('suite', ['verdict_changes', 'resource_changes', 'unavailable_changes']),
                                 ('library', ['verdict_changes', 'resource_changes'])] for kind in kinds)
    except Exception as error:
        report = {'complete': False, 'default_parity': False, 'error': str(error)}
    report['baseline_source'] = 'b07c038737d6713b6d1a5848d7568bdc972de7dd'
    report['candidate_product'] = '36ebc48e9fd8b9133995d6b1b12e183a2a6855b2'
    report['solver'] = '5.1.0'
    report['excluded'] = ['Legacy-solver upstream regression harness other than the neutral-boundary regression',
                          'Legacy-solver cardinality harness checks',
                          'Legacy-solver standard library companion jobs',
                          'Legacy-solver literal-identity targeted check',
                          'Legacy-solver editor additional-axiom option checks',
                          'Existing ON/OFF axiom-resource report replaced by paired default native report',
                          'Legacy-solver packaged-library check replaced by a pinned-solver check']
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    lines = ['# Scratch default Boogie compatibility', '',
             'This probe records observations without failing the workflow. Inspect the artifact before accepting parity.',
             'All proof execution uses Z3 5.1.0. This is not the unmodified full review workflow.', '',
             'Complete: ' + str(report['complete']), 'Default verdict and resource parity: ' + str(report['default_parity'])]
    if report['complete']:
        for scope in ['suite', 'library']:
            lines += ['', scope + ':']
            lines += [key + ': ' + str(len(value) if isinstance(value, dict) else value)
                      for key, value in report[scope].items()]
        lines += ['', 'Library declaration order is not fully deterministic; investigate any resource differences before attribution.']
    else:
        lines += ['', 'Incomplete: ' + report['error']]
    lines += ['', 'Excluded portions:'] + ['- ' + item for item in report['excluded']]
    (output / 'summary.md').write_text('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main(Path(sys.argv[1]), Path(sys.argv[2]))
