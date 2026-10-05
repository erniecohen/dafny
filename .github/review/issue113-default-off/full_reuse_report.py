#!/usr/bin/env python3
"""Pure mandatory38-job closure report; matched failures are never proof-green."""
import collections
import json
from pathlib import Path
import sys
from plan import HERE, digest
from matrix import workload
import repair82
from ci_reuse_pair import denominators, package_guard


def load(path):
    return json.loads(path.read_text())


def main(directory, output):
    manifest = package_guard()
    matrix = workload(False, False)['matrix']
    old = load(HERE / 'canonical/shipped/plan.json')
    literal = repair82.plan()
    arms = load(HERE / 'reused-compilers.json')['arms']
    jobs, incomplete = [], []
    counts = collections.Counter()
    all_classifications = {side: {} for side in ['baseline', 'final']}
    for row in matrix:
        root = directory / ('full-default-off-' + row['tag'])
        result = dict(matrix_row=row, source_guards_complete=False, raw_scope_complete=False,
                      raw_original_checks_complete=None, failures=[])
        try:
            setup = load(root / 'prepare-phase.json')
            final = load(root / 'finalize-phase.json')
            observe = load(root / 'observe-phase.json')
            if setup.get('complete') is not True or final.get('complete') is not True or observe.get('observer_returned') is not True or observe.get('adapter_exit') != 0:
                raise ValueError('Required setup/observer/post-execution phase is absent or incomplete')
            if observe['matrix_row'] != row or setup['candidate_overlay_used'] is not False or setup['new_build_performed'] is not False:
                raise ValueError('Actual matrix/arm source or no-build boundary differs')
            if setup['package'] != final['package'] or setup['package'] != manifest:
                raise ValueError('Before/after published source package differs')
            if setup['inputs'] != final['inputs']:
                raise ValueError('Before/after original inputs differ')
            result['source_guards_complete'] = True
            physical = []
            if row['cohort'] == 'repair82-literal':
                mine = [r for r in literal if r['index'] % 2 == int(row['shard'].split('/')[0])]
                inv = load(root / 'observations/invocation-plan.json')
                summary = load(root / 'observations/repair82-report.json')
                if inv['rows'] != mine or summary['sides'] != ['baseline', 'final'] or summary['source_ids'] != [r['id'] for r in mine]:
                    raise ValueError('Actual literal82 source/command closure differs')
                expected = sum(run['kind'] == 'verify' for r in mine for run in r['runs'])
                if summary['requested_invocations'] != 2*expected or summary['paired_verification_runs'] != expected:
                    raise ValueError('Actual literal82 invocation denominator differs')
                for source in mine:
                    scoped = load(root / 'observations/sources' / str(source['index']) / 'comparison.json')
                    if scoped['source'] != source or set(scoped['arms']) != {'baseline', 'final'}:
                        raise ValueError('Actual literal82 source mapping differs')
                    ids = [r['source_run_id'] for r in source['runs'] if r['kind'] == 'verify']
                    for side in ['baseline', 'final']:
                        if [r['source_run_id'] for r in scoped['arms'][side]['runs']] != ids:
                            raise ValueError('Actual literal82 RUN mapping differs')
                        for run in scoped['arms'][side]['runs']:
                            physical.append((side, run, root / 'observations/sources' / str(source['index']) / side / run['source_run_id']))
                result['raw_original_checks_complete'] = summary['complete']
                result['raw_scope_complete'] = summary['observations_complete']
                result['paired_cases'] = expected
                result['verdict_movements'] = summary['verdict_movements']
                result['resource_or_batch_movements'] = summary['resource_or_batch_movements']
                result['translated_bpl_movements'] = summary['translated_bpl_movements']
            else:
                mine = [r for r in old[row['cohort']] if r['index'] % int(row['shard'].split('/')[1]) == int(row['shard'].split('/')[0])]
                if row['part']:
                    mine = [r for r in mine if r['id'] == row['part']]
                request = load(root / 'observations/request.json')
                summary = load(root / 'observations/summary.json')
                if request['plan'] != mine or request['requested'] != len(mine) or summary['requested'] != len(mine) or summary['completed_pairs'] != len(mine):
                    raise ValueError('Actual canonical/Std command denominator differs')
                if (request['line'], request['cohort'], request['mode'], request['axioms'], request['shard'], request['part']) != ('shipped', row['cohort'], row['mode'], row['axioms'], row['shard'], row['part'] or None):
                    raise ValueError('Actual canonical/Std axis differs')
                if len(summary['results']) != len(mine) or {r['id'] for r in summary['results']} != {r['id'] for r in mine}:
                    raise ValueError('Actual paired case IDs differ')
                for source in mine:
                    case = root / 'observations/cases' / str(source['index']).zfill(4)
                    if load(case / 'plan.json') != source:
                        raise ValueError('Actual source plan bytes differ')
                    for side in ['baseline', 'final']:
                        physical.append((side, load(case / side / 'result.json'), case / side))
                result['raw_scope_complete'] = summary['incomplete'] == 0
                result['paired_cases'] = len(mine)
                result['verdict_movements'] = summary['verdict_movements']
                result['resource_or_batch_movements'] = summary['resource_or_batch_movements']
                result['translated_bpl_movements'] = summary['translated_bpl_movements']
            for side, value, folder in physical:
                command = load(folder / 'command.json')
                if command['compiler_identity'] != arms[side]['identity_record']:
                    raise ValueError('Actual physical command compiler identity differs')
                argv = command['argv']
                if any('extended-newtype-bases' in arg for arg in argv) or argv[0] != setup['arms'][side]['executable']:
                    raise ValueError('Actual physical compiler or feature default differs')
                for name in ['stdout', 'stderr']:
                    if digest(folder / (name + '.txt')) != value[name + '_sha256']:
                        raise ValueError('Raw command log differs from its receipt')
                for name, field in [('results.csv', 'resources_csv_sha256'), ('results.json', 'resources_json_sha256')]:
                    expected_hash = value.get(field)
                    if expected_hash is not None and digest(folder / name) != expected_hash:
                        raise ValueError('Raw resource log differs from its receipt')
                for bpl in value['bpl']['files']:
                    if digest(folder / bpl['file']) != bpl['raw_sha256']:
                        raise ValueError('Raw BPL differs from its receipt')
                    normalized = (folder / bpl['file']).with_suffix('.normalized.bpl')
                    if bpl['normalized_sha256'] is not None and digest(normalized) != bpl['normalized_sha256']:
                        raise ValueError('Normalized BPL differs from its receipt')
                solver_value = None
                for i, token in enumerate(argv):
                    if token == '--solver-path' and i+1 < len(argv): solver_value = argv[i+1]
                    elif token.startswith('--solver-path=') or token.startswith('--solver-path:'): solver_value = token[14:]
                solver_label = next((name for name, value in setup['solvers'].items() if value['path'] == solver_value), 'unmatched-literal-solver-option')
                group = '/'.join([row['cohort'], row['mode'], 'requestedAX-' + row['axioms'], solver_label])
                bucket = all_classifications[side].setdefault(group, {'exits': collections.Counter(), 'declarations': collections.Counter(),
                    'batches': collections.Counter(), 'warning_lines': 0, 'resource_units': 0})
                bucket['exits'][str(value['exit'])] += 1
                bucket['warning_lines'] += len(value['warning_lines'])
                for decl in value['declarations']['rows']:
                    bucket['declarations'][decl['outcome']] += 1
                    bucket['resource_units'] += decl['resourceCount']
                    for batch in decl['vcResults']:
                        bucket['batches'][batch['outcome']] += 1
                if value['timed_out'] or value['exit'] < 0 or value['exit'] == 125:
                    result['raw_scope_complete'] = False
            result['raw_invocations'] = len(physical)
            if result['raw_invocations'] != 2*result['paired_cases']:
                raise ValueError('Physical two-arm coverage is incomplete')
            counts['pairs'] += result['paired_cases']
            counts['physical_invocations'] += result['raw_invocations']
        except Exception as error:
            result['failures'].append(type(error).__name__ + ': ' + str(error))
            result['raw_scope_complete'] = False
        if not result['source_guards_complete'] or not result['raw_scope_complete'] or result['failures']:
            incomplete.append(row['tag'])
        jobs.append(result)
    denominator = denominators()
    report = dict(diagnostic_only=True, proof_acceptance_claimed=False, candidate_source_adoption=None,
                  resource_parity_accepted=False, requested_denominators=denominator,
                  completed_paired_cases=counts['pairs'], observed_physical_invocations=counts['physical_invocations'],
                  jobs=jobs, missing_or_incomplete=incomplete, raw_observation_closure_complete=not incomplete and counts['pairs'] == denominator['total_pairs'],
                  physical_classifications=all_classifications, source_spec_sha256=digest(HERE / 'spec.json'),
                  public_archive_record_sha256=digest(HERE / 'reused-compilers.json'), source_package=manifest,
                  boundary='Matched errors/warnings/OOR remain actual outcomes; completeness is observation closure, never proof-green. RU comparisons stay within the same solver. Byte-equal translated BPL is not solver-query equality. Raw original check failures are preserved without updating goldens.')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({k: report[k] for k in ['raw_observation_closure_complete', 'completed_paired_cases', 'observed_physical_invocations', 'missing_or_incomplete']}))


if __name__ == '__main__':
    main(Path(sys.argv[1]), Path(sys.argv[2]))
