#!/usr/bin/env python3
"""Pure supplemental completeness/provenance report; no engines or oracle edits."""
from collections import Counter
import json
from pathlib import Path
import sys
from plan import HERE, digest
from repair82 import plan
from reuse_compiler import checked_api

SIDES=['baseline','final','candidate']

def main():
    directory,original_report,output=map(Path,sys.argv[1:4])
    spec=json.loads((HERE/'spec.json').read_text())
    reuse=json.loads((HERE/'reused-compilers.json').read_text())
    rows=plan()
    proof_rows=[run for row in rows for run in row['runs'] if run['kind']=='verify']
    assert len(rows)==5 and len(proof_rows)==93 and sum(len(row['runs']) for row in rows)==150
    tags=['shipped-repair82-literal-0','shipped-repair82-literal-1']+['shipped-parser-focus-'+solver+'-ax-'+axioms for solver in ['5.1.0','reference'] for axioms in ['off','on']]
    failures=[];guards=[];runs=[];hashes={}
    def hashed(path):
        if path.is_file():hashes[path.relative_to(directory).as_posix()]=digest(path)
    def zero(path):
        value=path.read_text().strip() if path.is_file() else None
        guards.append(dict(file=path.relative_to(directory).as_posix(),exit_text=value))
        hashed(path)
        if value!='0':failures.append('Missing/failed guard: '+path.relative_to(directory).as_posix())
    actual_dirs={p.name.removeprefix('default-off-') for p in directory.glob('default-off-shipped-*') if p.is_dir()}
    if actual_dirs!=set(tags):failures.append('Actual paired-job artifact identities differ')
    compiler=directory/'default-off-compilers-shipped'
    for name in ['artifact-api','artifact-api-check','fresh-artifact-api-copy']:zero(compiler/(name+'-exit.txt'))
    step_outcomes={}
    contexts=[directory/'default-off-workflow-request/plan-metadata/step-outcomes.json',compiler/'step-outcomes.json']+[directory/('default-off-'+tag)/'run-metadata/step-outcomes.json' for tag in tags]
    for path in contexts:
        hashed(path)
        if not path.is_file():
            failures.append('Raw diagnostic step outcomes missing: '+path.relative_to(directory).as_posix());continue
        value=json.loads(path.read_text());step_outcomes[path.relative_to(directory).as_posix()]=value
        for key,step in value.items():
            if step.get('outcome') in ['failure','cancelled']:
                failures.append('Raw diagnostic step failed: '+path.relative_to(directory).as_posix()+':'+key)
    for side in SIDES:
        item=reuse['arms'][side];arm=compiler/side
        for name in ['build','identity','compiler-version','candidate-version-guard' if side=='candidate' else 'reused-version-guard']:zero(arm/(name+'-exit.txt'))
        for name,sha in [('dafny.tar.gz',item['archive_sha256']),('compiler-components.json',item['identity_record_sha256'])]:
            path=arm/name;hashed(path)
            if not path.is_file() or digest(path)!=sha:failures.append('Reused artifact hash differs: '+side+'/'+name)
        path=arm/'compiler-version.txt';hashed(path)
        if not path.is_file() or path.read_text().strip()!=item['actual_version']:failures.append('Fresh version differs: '+side)
        path=arm/'artifact-api.json';hashed(path)
        try:
            api=checked_api(path)
        except (ValueError, OSError, TypeError) as error:
            failures.append('Reused API provenance missing/invalid: '+side+': '+str(error))
        path=arm/'reuse-source.json';hashed(path)
        value=json.loads(path.read_text()) if path.is_file() else {}
        if value.get('public_run')!=reuse['public_run'] or value.get('artifact_id')!=reuse['artifact_id'] or value.get('new_build_performed') is not False or value.get('source_product')!=item['identity_record']['product_revision'] or value.get('artifact_api_digest')!=reuse['artifact_api_digest'] or value.get('unexpired_at_reuse') is not True or value.get('artifact_api_receipt_sha256')!=hashes.get((arm/'artifact-api.json').relative_to(directory).as_posix()):
            failures.append('Reuse provenance differs: '+side)
    expected_proofs={run['source_run_id']:run for run in proof_rows}
    for tag in tags:
        job=directory/('default-off-'+tag)
        zero(job/'run-metadata/prerequisites-exit.txt');zero(job/'run-metadata/runner-exit.txt')
        for side in SIDES:
            for phase in ['before','after']:
                zero(job/('run-metadata/'+side+'-full-closure-'+phase+'-exit.txt'))
                path=job/('run-metadata/'+side+'-full-closure-'+phase+'.json');hashed(path)
                value=json.loads(path.read_text()) if path.is_file() else {}
                if value.get('all_actual_files_and_modes_match') is not True or value.get('files')!=329 or value.get('side')!=side or value.get('artifact_id')!=reuse['artifact_id']:
                    failures.append('Actual full compiler closure receipt differs: '+tag+'/'+side+'/'+phase)
        for command_file in sorted((job/'measurements').rglob('command.json')):
            hashed(command_file)
            command=json.loads(command_file.read_text());result_file=command_file.with_name('result.json');hashed(result_file)
            result=json.loads(result_file.read_text()) if result_file.is_file() else {}
            side=command_file.parent.name if 'parser-focus' in tag else command_file.parent.parent.name
            if side not in SIDES:failures.append('Unrecognized invocation arm: '+command_file.relative_to(directory).as_posix());continue
            if command.get('compiler_identity')!=reuse['arms'][side]['identity_record']:failures.append('Invocation compiler identity differs')
            argv=command.get('argv',[])
            if any('extended-newtype-bases' in token for token in argv):failures.append('Unexpected feature-enable flag in default-off proof command')
            source_id=command.get('source_run_id')
            if 'repair82-literal' in tag:
                frozen=expected_proofs.get(source_id)
                if frozen is None or command.get('literal_run')!=frozen['literal_run'] or result.get('expected_exit')!=frozen['expected_exit']:
                    failures.append('Literal source/exit oracle identity differs')
            else:
                if '--filter-symbol=Std.Parsers' not in argv:failures.append('Parser proof scope differs')
                ax_on=tag.endswith('-ax-on')
                if ('--additional-axioms' in argv)!=ax_on:failures.append('Actual parser AX flag differs')
            if not result_file.is_file() or result.get('timed_out') or result.get('incomplete') or not isinstance(result.get('exit'),int) or result.get('exit')<0 or result.get('exit')==125:
                failures.append('Missing/aborted/incomplete raw invocation: '+command_file.relative_to(directory).as_posix())
            runs.append(dict(tag=tag,side=side,source_run_id=source_id,command_sha256=digest(command_file),result_sha256=digest(result_file) if result_file.is_file() else None,exit=result.get('exit'),timed_out=result.get('timed_out'),incomplete=result.get('incomplete'),expected_exit=result.get('expected_exit'),expected_exit_matched=result.get('expected_exit_matched')))
    actual=Counter((run['tag'],run['side'],run['source_run_id']) for run in runs)
    expected=Counter()
    for side in SIDES:
        for row in rows:
            for run in row['runs']:
                if run['kind']=='verify':expected[('shipped-repair82-literal-'+str(row['index']%2),side,run['source_run_id'])]+=1
        for tag in tags[2:]:expected[(tag,side,None)]+=1
    if actual!=expected:failures.append('Exact 291 invocation identities/multiplicities differ')
    report=json.loads(original_report.read_text()) if original_report.is_file() else {}
    if report.get('complete_observations') is not True:failures.append('Original frozen aggregate has missing/incomplete observations')
    value=dict(schema_version=1,diagnostic_only=True,engine_invoked_by_report=False,proof_acceptance_claimed=False,
        source_spec_sha256=digest(HERE/'spec.json'),reuse_spec_sha256=digest(HERE/'reused-compilers.json'),
        original_aggregate_sha256=digest(original_report) if original_report.is_file() else None,
        original_aggregate_complete_observations=report.get('complete_observations'),requested_invocations=291,
        actual_invocations=len(runs),sides=SIDES,groups=tags,guard_records=guards,raw_step_outcomes=step_outcomes,invocation_records=runs,
        observed_file_sha256=hashes,missing_or_failed=failures,complete_observations=not failures,
        original_expected_checks_all_pass=report.get('expected_failure_checks_all_pass'),
        parser_positive_proof_complete=[p.get('positive_proof_complete') for p in report.get('parser_profiles',[])],
        boundary='Supplemental artifact/setup/closure completeness only; original raw receipts/oracles unchanged. Actions/report completion is not proof or source acceptance. No missing/unsupported/capacity result is accepted.')
    output.write_text(json.dumps(value,indent=2)+'\n')
    print(json.dumps(dict(complete_observations=value['complete_observations'],actual_invocations=len(runs),missing_or_failed=failures,proof_acceptance_claimed=False)))
if __name__=='__main__':main()
