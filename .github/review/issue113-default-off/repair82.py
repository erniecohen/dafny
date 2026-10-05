#!/usr/bin/env python3
"""Literal #82 existing-language paired observations; plan/check are pure.

The run subcommand is CI-only engine execution. It never changes RUN flags,
source bodies, expected output, budgets, seeds, or the feature default.
"""
import argparse
import collections
import concurrent.futures
import hashlib
import json
from pathlib import Path
import re
import shlex
from plan import HERE, digest
from prepare import components
from run import append, dump, execute, verdict, declarations, bpl_receipt, compare, bundle_libraries


def verify_inputs():
    spec = json.loads((HERE / 'spec.json').read_text())['repair82']
    root = HERE / spec['input_root']
    actual = {file.relative_to(root).as_posix(): digest(file)
              for file in sorted(root.rglob('*')) if file.is_file()}
    if actual != spec['files_sha256'] or any(file.is_symlink() for file in root.rglob('*')):
        raise ValueError('Separate82 input/oracle closure changed')
    return root, spec


def plan():
    root, spec = verify_inputs()
    rows = []
    for path in spec['literal_sources']:
        source = root / path
        runs = []
        for line, text in enumerate(source.read_text().splitlines(), 1):
            if not text.startswith('// RUN: '):
                continue
            literal = text[len('// RUN: '):]
            tokens = shlex.split(literal)
            expected = 0
            if tokens[:1] == ['%exits-with']:
                expected, tokens = int(tokens[1]), tokens[2:]
            macro = tokens.pop(0)
            if macro == '%baredafny':
                if tokens[0] != 'verify' or any('extended-newtype-bases' in x for x in tokens):
                    raise ValueError('82 proof source unexpectedly changes language admission')
                if len(tokens) < 3 or tokens[-2] not in ('>', '>>'):
                    raise ValueError('82 literal redirect is not the supported exact form')
                redirect, output = tokens[-2:]
                argv = tokens[:-2]
                runs.append({'line': line, 'literal_run': literal, 'kind': 'verify',
                             'expected_exit': expected, 'argv': argv,
                             'redirect': redirect, 'output': output})
            elif macro == '%diff':
                if len(tokens) != 2:
                    raise ValueError('Unexpected literal diff command')
                runs.append({'line': line, 'literal_run': literal, 'kind': 'diff',
                             'expected': tokens[0], 'actual': tokens[1]})
            elif macro == '%OutputCheck':
                if len(tokens) != 3 or tokens[0] != '--file-to-check':
                    raise ValueError('Unexpected literal output-check command')
                runs.append({'line': line, 'literal_run': literal, 'kind': 'OutputCheck',
                             'actual': tokens[1], 'check': tokens[2]})
            else:
                raise ValueError('Unreviewed82 literal macro: ' + macro)
            runs[-1]['source_run_id'] = hashlib.sha256((path+'\0'+str(line)+'\0'+literal).encode()).hexdigest()[:20]
        rows.append({'index': len(rows), 'id': path, 'source_sha256': digest(source), 'runs': runs})
    return rows


def check_output(text, check_file):
    """Exact current corpus subset of OutputCheckCommand's sequential semantics.

    Only CHECK and CHECK-NOT regexes occur in these frozen .check files. A NOT
    applies through the next matched line, then resets; consecutive NOT replaces
    the previous NOT, matching the upstream enumerator. No broad regex rewrite.
    """
    directives = []
    for line, text_line in enumerate(check_file.read_text().splitlines(), 1):
        match = re.search(r'\b(CHECK(?:-NOT)?):\s*(.*)$', text_line)
        if match:
            directives.append((match.group(1), re.compile(match.group(2)), line))
        elif re.search(r'\bCHECK[^ ]*:', text_line):
            raise ValueError('OutputCheck corpus exceeds the reviewed regex subset')
    if not directives:
        raise ValueError('Empty check file')
    lines, cursor, forbidden = text.splitlines(), 0, None
    for kind, regex, line in directives:
        if kind == 'CHECK-NOT':
            forbidden = (regex, line)
            continue
        found = False
        while cursor < len(lines):
            current = lines[cursor]
            cursor += 1
            if forbidden and forbidden[0].search(current):
                return {'passed': False, 'directive_line': forbidden[1], 'actual_line': cursor, 'reason': 'Forbidden pattern matched'}
            if regex.search(current):
                found = True
                break
        if not found:
            return {'passed': False, 'directive_line': line, 'reason': 'Required pattern absent'}
        forbidden = None
    if forbidden:
        for index, current in enumerate(lines[cursor:], cursor + 1):
            if forbidden[0].search(current):
                return {'passed': False, 'directive_line': forbidden[1], 'actual_line': index, 'reason': 'Forbidden suffix pattern matched'}
    return {'passed': True, 'directives': len(directives)}


def pair(args, row, receipts, versions):
    root, _ = verify_inputs()
    folder = args.output / 'sources' / str(row['index'])
    folder.mkdir(parents=True)
    outcomes = {}
    sides=list(getattr(args,'sides',['baseline','final']));offset=row['index']%len(sides);order=sides[offset:]+sides[:offset]
    for side in order:
        side_dir = folder / side
        side_dir.mkdir()
        virtual, observed, checks = {}, [], []
        source = root / row['id']
        def subst(value):
            return value.replace('%review-z3', str(args.review_solver)).replace('%z3', str(args.default_solver)).replace('%S', str(source.parent)).replace('%s', str(source)).replace('%t', '<literal-output>')
        for run in row['runs']:
            key = subst(run.get('actual', run.get('output', '')))
            if run['kind'] == 'diff':
                # ReadAllText/AssertWithDiff consumes text, not .doo/binary bytes.
                expected_file = Path(subst(run['expected']))
                actual = virtual.get(key)
                checks.append({'source_run_id':run['source_run_id'], 'literal_run':run['literal_run'],
                               'kind':'diff', 'expected_sha256':digest(expected_file),
                               'actual_text_sha256':hashlib.sha256((actual or '').encode()).hexdigest(),
                               'passed':actual is not None and expected_file.read_text() == actual})
                continue
            if run['kind'] == 'OutputCheck':
                check_file = Path(subst(run['check']))
                actual = virtual.get(key)
                result = check_output(actual, check_file) if actual is not None else {'passed':False,'reason':'Missing literal output'}
                checks.append({'source_run_id':run['source_run_id'], 'literal_run':run['literal_run'],
                               'kind':'OutputCheck','check_sha256':digest(check_file), **result})
                continue
            directory = side_dir / run['source_run_id']
            directory.mkdir()
            cmd = [str(getattr(args,side))] + [subst(token) for token in run['argv']]
            # Observation options only. Original literal AX/refresh/caps remain exact.
            cmd += ['--log-format','csv;LogFileName='+str(directory/'results.csv'), '--log-format','json;LogFileName='+str(directory/'results.json'), '--bprint',str(directory/'program.bpl')]
            dump(directory/'command.json', {'argv':cmd,'cwd':str(source.parent),'literal_run':run['literal_run'],
                                           'literal_source_line':run['line'],'source_run_id':run['source_run_id'],
                                           'source_sha256':row['source_sha256'],'compiler_identity':receipts[side],
                                           'timeout_seconds':900,'order':order})
            append(args.output/'events.jsonl', {'event':'started','source_run_id':run['source_run_id'],'side':side})
            try:
                execution = execute(cmd, source.parent, 900)
            except OSError as error:
                execution = {'exit':125,'timed_out':False,'seconds':0,'stdout':'','stderr':str(error)}
            (directory/'stdout.txt').write_text(execution['stdout'])
            (directory/'stderr.txt').write_text(execution['stderr'])
            text = execution['stdout']
            logger_lines = ['Results File: '+str(directory/name) for name in ['results.json','results.csv']]
            # Drop only this identified observer path from literal virtual stdout.
            text = ''.join(line for line in text.splitlines(keepends=True) if line.rstrip('\r\n') not in logger_lines)
            virtual[key] = text if run['redirect'] == '>' else virtual.get(key,'')+text
            result = {k:v for k,v in execution.items() if k not in ('stdout','stderr')}
            result.update({'source_run_id':run['source_run_id'],'literal_run':run['literal_run'],
                           'expected_exit':run['expected_exit'],'verdict':verdict(row,execution,'canonical'),
                           'stdout_sha256':digest(directory/'stdout.txt'),'stderr_sha256':digest(directory/'stderr.txt'),
                           'warning_lines':[line for line in (execution['stdout']+execution['stderr']).splitlines() if re.search(r'\bWarning\b',line,re.I)],
                           'resources_csv_sha256':digest(directory/'results.csv') if (directory/'results.csv').is_file() else None,
                           'resources_json_sha256':digest(directory/'results.json') if (directory/'results.json').is_file() else None,
                           'declarations':declarations(directory/'results.json'),
                           'bpl':bpl_receipt(directory,[(str(root),'<repair82-inputs>'),(str(args.output),'<observations>')],versions)})
            result['expected_exit_matched'] = result['exit'] == run['expected_exit']
            incomplete_text = re.search(r'out.of.resource|out of memory|verification.*timed out|internal error|unhandled exception|solver exception', execution['stdout']+execution['stderr'],re.I)
            result['incomplete'] = result['timed_out'] or result['exit'] < 0 or result['exit'] == 125 or bool(incomplete_text)
            if run['expected_exit'] in (0,4) and (not result['declarations']['available'] or not result['bpl']['available'] or not result['resources_csv_sha256']):
                result['incomplete'] = True
            dump(directory/'result.json',result)
            append(args.output/'events.jsonl', {'event':'completed','source_run_id':run['source_run_id'],'side':side,'exit':result['exit'],'incomplete':result['incomplete']})
            observed.append(result)
        outcomes[side] = {'runs':observed,'output_checks':checks}
    comparisons = []
    for baseline,final in zip(outcomes['baseline']['runs'],outcomes['final']['runs']):
        assert baseline['source_run_id'] == final['source_run_id']
        comparison = compare(baseline,final)
        comparison['incomplete'] |= baseline['incomplete'] or final['incomplete']
        comparison['equal_observations'] = not comparison['differences'] and not comparison['incomplete']
        comparisons.append({'source_run_id':baseline['source_run_id'], **comparison})
    additional={}
    if 'candidate' in outcomes:
        for side in ['baseline','final']:
            additional[side+'/candidate']=[dict(source_run_id=a['source_run_id'],**compare(a,b)) for a,b in zip(outcomes[side]['runs'],outcomes['candidate']['runs'])]
    value = {'candidate_comparisons':additional,'source':row,'order':order,'arms':outcomes,'comparisons':comparisons,
             'complete':not any(x['incomplete'] for x in comparisons) and all(x['expected_exit_matched'] for side in outcomes.values() for x in side['runs']) and all(x['passed'] for side in outcomes.values() for x in side['output_checks'])}
    dump(folder/'comparison.json',value)
    return value


def run(args):
    args.output = args.output.resolve()
    if args.output.exists():
        raise ValueError('Output must be fresh')
    root,spec82 = verify_inputs()
    shard,shards = map(int,args.shard.split('/'))
    if shards != 2 or not 0 <= shard < shards:
        raise ValueError('Exactly two whole-source shards required')
    rows = [row for row in plan() if row['index']%shards == shard]
    spec = json.loads((HERE/'spec.json').read_text())['lines']['shipped']
    receipts,versions = {},[]
    args.sides=['baseline','final']+(['candidate'] if args.candidate else [])
    for side in args.sides:
        setattr(args,side,getattr(args,side).resolve())
        receipt = json.loads(getattr(args,side+'_identity').read_text())
        if receipt['side'] != side or receipt['line'] != 'shipped' or receipt['product_revision'] != (json.loads((HERE/'spec.json').read_text())['candidate']['product_revision'] if side=='candidate' else spec['repaired_product' if side=='baseline' else 'final_product']) or components(getattr(args,side)) != receipt['components_sha256']:
            raise ValueError('Actual compiler differs from pinned build receipt')
        if bundle_libraries(getattr(args,side)) != receipt.get('bundled_libraries_sha256'):
            raise ValueError('Actual packaged libraries differ from build receipt')
        if side=='candidate' and receipt.get('candidate_source')!=json.loads((HERE/'spec.json').read_text())['candidate']:
            raise ValueError('Candidate overlay source identity differs')
        receipts[side] = receipt
        versions += getattr(args,side+'_version').read_text().strip().splitlines()
    for name,expected in [('review','5.1.0'),('default','4.12.1')]:
        setattr(args,name+'_solver',getattr(args,name+'_solver').resolve())
        if not getattr(args,name+'_solver').is_file() or not re.search(r'\b'+re.escape(expected)+r'\b',getattr(args,name+'_solver_version').read_text()):
            raise ValueError('Wrong literal-macro solver version')
    solver_identities = {name:digest(getattr(args,name+'_solver')) for name in ['review','default']}
    args.output.mkdir(parents=True)
    dump(args.output/'invocation-plan.json', {'separate_existing_language_cohort':True,'rows':rows,
      'input_source':spec82['source_commit'],'input_files':spec82['files_sha256'],
      'compiler_pair':receipts,'solvers':{name:{'sha256':digest(getattr(args,name+'_solver')),'version':getattr(args,name+'_solver_version').read_text()} for name in ['review','default']}})
    with concurrent.futures.ThreadPoolExecutor(2) as pool:
        results = list(pool.map(lambda row:pair(args,row,receipts,versions),rows))
    verify_inputs()
    for side in receipts:
        if components(getattr(args,side)) != receipts[side]['components_sha256']:
            raise ValueError('Compiler changed during observations')
        if bundle_libraries(getattr(args,side)) != receipts[side]['bundled_libraries_sha256']:
            raise ValueError('Packaged libraries changed during observations')
    if any(digest(getattr(args,name+'_solver')) != solver_identities[name] for name in solver_identities):
        raise ValueError('Solver changed during observations')
    summary={'sides':args.sides,'observations_complete':not any(run['incomplete'] for row in results for arm in row['arms'].values() for run in arm['runs']),'candidate_verdict_movements':sum('verdict' in c['differences'] for row in results for c in row['candidate_comparisons'].get('final/candidate',[])),'source_spec_sha256':digest(HERE/'spec.json'),'shard':args.shard,'literal_sources':len(rows),
             'literal_mappings':sum(len(row['runs']) for row in rows),
             'paired_verification_runs':sum(len(side['runs']) for row in results for side in [row['arms']['baseline']]),
             'requested_invocations':sum(len(side['runs']) for row in results for side in row['arms'].values()),
             'complete':all(row['complete'] for row in results),
             'verdict_movements':sum('verdict' in c['differences'] for row in results for c in row['comparisons']),
             'resource_or_batch_movements':sum('declarations_outcomes_resources_batches' in c['differences'] for row in results for c in row['comparisons']),
             'translated_bpl_movements':sum('normalized_bpl' in c['differences'] for row in results for c in row['comparisons']),
             'boundary':'Separate82 literal checks/flags; original4b denominator untouched; matched failures or warnings are not proof-green; translated BPL equality is not solver-query identity.',
             'source_ids':[row['source']['id'] for row in results]}
    dump(args.output/'repair82-report.json',summary)
    print(json.dumps(summary))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    sub=parser.add_subparsers(dest='command',required=True)
    p=sub.add_parser('plan');p.add_argument('output',type=Path)
    p=sub.add_parser('run')
    for name in ['baseline','final','baseline-identity','final-identity','baseline-version','final-version','default-solver','review-solver','default-solver-version','review-solver-version','output']:
        p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--shard',required=True)
    for name in ['candidate','candidate-identity','candidate-version']:p.add_argument('--'+name,type=Path)
    args=parser.parse_args()
    if args.command=='plan':
        args.output.write_text(json.dumps(plan(),indent=2)+'\n')
    else:run(args)
if __name__=='__main__':main()
