#!/usr/bin/env python3
"""Focused parser tasks with unchanged complete old Std source/project inputs.

Only run mode invokes engines; aggregate is pure and never claims acceptance.
"""
import argparse
import json
from pathlib import Path
import sys
from plan import HERE,digest
from prepare import components,inputs
from run import bundle_libraries,dump,pair
from repair82 import plan as repair82_plan


def aggregate(directory,output):
    spec=json.loads((HERE/'spec.json').read_text())
    parser=[json.loads(file.read_text()) for file in directory.glob('**/parser-focus-report.json')]
    repair=[json.loads(file.read_text()) for file in directory.glob('**/repair82-report.json')]
    requests=list(directory.glob('**/workflow-request.json'))
    requested=json.loads(requests[0].read_text()) if len(requests)==1 else {}
    expected_arms=3 if requested.get('include_candidate') else 2
    expected_sides=['baseline','final']+(['candidate'] if expected_arms==3 else [])
    failures=[]
    if len(requests)!=1:failures.append('Exactly one requested workload receipt required')
    if requested.get('requested_invocations')!=97*expected_arms:failures.append('Requested 93 literal plus four parser proof denominator differs')
    if len(parser)!=4 or len({(p['solver_axis'],p['axioms']) for p in parser})!=4:failures.append('Parser four solver/AX pairs incomplete')
    if {(p['solver_axis'],p['axioms']) for p in parser}!={('5.1.0','off'),('5.1.0','on'),('4.12.1','off'),('4.12.1','on')}:failures.append('Parser solver/AX identity differs')
    if len(repair)!=2 or len({p['shard'] for p in repair})!=2 or sum(p['literal_sources'] for p in repair)!=5:failures.append('Five82wrapper/two shard coverage incomplete')
    if any(not p.get('observations_complete') for p in parser+repair):failures.append('Raw observation or source guard incomplete')
    if any(p.get('sides')!=expected_sides or p.get('source_spec_sha256')!=digest(HERE/'spec.json') for p in parser+repair):failures.append('Requested arms or source spec differs')
    if sum(p.get('paired_verification_runs',0) for p in repair)!=93 or sum(p.get('literal_mappings',0) for p in repair)!=150:failures.append('93proof/150literal82 coverage differs')
    actual_source_ids=[source for p in repair for source in p.get('source_ids',[])]
    if sorted(actual_source_ids)!=sorted(row['id'] for row in repair82_plan()):failures.append('82literal wrapper identity differs')
    # Positive proof failures are retained separately from completed raw observations.
    if any(p.get('requested_invocations')!=p['paired_verification_runs']*expected_arms for p in repair):failures.append('82actualarm invocation denominator differs')
    value=dict(diagnostic_only=True,proof_acceptance_claimed=False,source_spec_sha256=digest(HERE/'spec.json'),
        requested=requested,parser_profiles=parser,repair82_shards=repair,missing_or_incomplete=failures,
        complete_observations=not failures,expected_failure_checks_all_pass=all(p.get('complete') for p in repair),
        boundary='Focused parser selection from complete original Std source; all82literals retained. No full canonical substitute, no source/cost/semantic acceptance from orchestration.')
    dump(output,value)
    print(json.dumps(dict(complete_observations=value['complete_observations'],proof_acceptance_claimed=False,failures=failures)))


def run(args):
    spec=json.loads((HERE/'spec.json').read_text())
    args.line='shipped';args.cohort='parser-focus';args.mode='project';args.inputs=args.inputs.resolve();args.output=args.output.resolve();args.sides=['baseline','final']+(['candidate'] if args.candidate else [])
    if args.output.exists():raise ValueError('Observation directory must be fresh')
    old_inputs=inputs('shipped',args.inputs)
    receipts,versions={},[]
    for side in args.sides:
        setattr(args,side,getattr(args,side).resolve())
        receipt=json.loads(getattr(args,side+'_identity').read_text())
        expected=spec['candidate']['product_revision'] if side=='candidate' else spec['lines']['shipped']['repaired_product' if side=='baseline' else 'final_product']
        if receipt['line']!='shipped' or receipt['side']!=side or receipt['product_revision']!=expected or components(getattr(args,side))!=receipt['components_sha256'] or bundle_libraries(getattr(args,side))!=receipt['bundled_libraries_sha256']:
            raise ValueError('Actual compiler or library identity differs')
        if side=='candidate' and receipt.get('candidate_source')!=spec['candidate']:raise ValueError('Candidate overlay source guard differs')
        receipts[side]=receipt;versions+=getattr(args,side+'_version').read_text().strip().splitlines()
    args.solver=args.solver.resolve();solver_before=digest(args.solver);args.timeout=5400
    # Full source/project input unchanged. The only proof selection is the CLI
    # substring filter, as CliCompilation.cs applies it to native task names.
    std=json.loads((HERE/'canonical/shipped/plan.json').read_text())['std'][0]
    row={**std,'id':'Parsers','own_options':['--filter-symbol=Std.Parsers']}
    args.output.mkdir(parents=True)
    dump(args.output/'focused-plan.json',dict(original_input_receipt=old_inputs,row=row,sides=args.sides,
        whole_project_source='Original complete Std project/source closure retained',
        additional_proof_selection='--filter-symbol=Std.Parsers; no trailing-dot suffix mode',
        compiler_pair=receipts,solver_sha256=solver_before,solver_version=args.solver_version.read_text()))
    result=pair(args,row,receipts,versions)
    inputs('shipped',args.inputs)
    if digest(args.solver)!=solver_before:raise ValueError('Solver changed during invocation')
    for side in receipts:
        if components(getattr(args,side))!=receipts[side]['components_sha256'] or bundle_libraries(getattr(args,side))!=receipts[side]['bundled_libraries_sha256']:raise ValueError('Compiler/library changed during invocation')
    observations={}
    for side in args.sides:
        arm_dir=args.output/'cases/0000'/side
        arm=json.loads((arm_dir/'result.json').read_text())
        names=[row['name'] for row in arm['declarations']['rows']]
        if not names or any('Std.Parsers' not in name for name in names):raise ValueError('Focused task source scope missing or different')
        observations[side]=dict(exit=arm['exit'],timed_out=arm['timed_out'],declarations=len(names),
            outcomes={kind:sum(row['outcome']==kind for row in arm['declarations']['rows']) for kind in sorted({row['outcome'] for row in arm['declarations']['rows']})},
            total_resource_count=sum(row['resourceCount'] for row in arm['declarations']['rows']),
            resources_csv_sha256=arm['resources_csv_sha256'],resources_json_sha256=arm['resources_json_sha256'],bpl=arm['bpl'],warnings=arm['warning_lines'])
    version=args.solver_version.read_text()
    value=dict(source_spec_sha256=digest(HERE/'spec.json'),solver_axis='5.1.0' if '5.1.0' in version else '4.12.1',axioms=args.axioms,
        sides=args.sides,observations=observations,comparison=result,observations_complete=not result['incomplete'] and all(arm['resources_csv_sha256'] and arm['resources_json_sha256'] for arm in observations.values()),
        positive_proof_complete=all(arm['exit']==0 and not arm['warnings'] for arm in observations.values()),
        proof_acceptance_claimed=False,scope='Full original Std source/projects loaded, parser task filter only. All contracts/bodies retained; not a full Std verdict oracle.')
    dump(args.output/'parser-focus-report.json',value)
    print(json.dumps({k:v for k,v in value.items() if k not in ['observations','comparison']}))


def main():
    if sys.argv[1:2]==['aggregate']:
        if len(sys.argv)!=4:raise ValueError('aggregate DIRECTORY OUTPUT')
        aggregate(Path(sys.argv[2]),Path(sys.argv[3]));return
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['baseline','final','baseline-identity','final-identity','baseline-version','final-version','inputs','solver','solver-version','output']:parser.add_argument('--'+name,type=Path,required=True)
    for name in ['candidate','candidate-identity','candidate-version']:parser.add_argument('--'+name,type=Path)
    parser.add_argument('--axioms',choices=['off','on'],required=True)
    run(parser.parse_args())
if __name__=='__main__':main()
