#!/usr/bin/env python3
"""Diagnostic only: reuse public binaries; never build or update expectations."""
import collections
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import signal
import subprocess
import sys
import tarfile
import time
import xml.etree.ElementTree as ET
import zipfile

HERE=Path(__file__).resolve().parent
SPEC=json.loads((HERE/'spec.json').read_text())
WORK=Path(os.environ['GITHUB_WORKSPACE'])
ROOT=WORK/'frozen'
OUT=WORK/'native-law-safety-observations'
BIN=ROOT/'Source/IntegrationTests/bin/Release/net8.0'
COPIED=BIN/'TestFiles/LitTests/LitTest'
COHORT=json.loads((HERE/'cohort/cohort.json').read_text())
NS={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}

def sha(p):
    h=hashlib.sha256()
    with Path(p).open('rb') as f:
        while chunk:=f.read(1048576):h.update(chunk)
    return h.hexdigest()
def save(p,v):
    p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2)+'\n')
def need(v,s):
    if not v:raise ValueError(s)
def load(p):return json.loads(p.read_text())
def logged(args,cwd,where,timeout=3600):
    where.mkdir(parents=True,exist_ok=True)
    started=time.monotonic();p=subprocess.Popen(list(map(str,args)),cwd=cwd,stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,stderr=subprocess.PIPE,start_new_session=True)
    timed=False
    try:out,err=p.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        timed=True;os.killpg(p.pid,signal.SIGKILL);out,err=p.communicate()
    (where/'stdout.txt').write_bytes(out);(where/'stderr.txt').write_bytes(err)
    r={'argv':list(map(str,args)),'cwd':str(cwd),'exit':p.returncode,'timed_out':timed,
       'seconds':time.monotonic()-started,'stdout_sha256':sha(where/'stdout.txt'),'stderr_sha256':sha(where/'stderr.txt')}
    save(where/'process.json',r);return r

def unpack(path,prefix,expected):
    need(sha(path)==expected['inner_tar_sha256'],'Public compiler archive changed')
    seen=set()
    with tarfile.open(path) as t:
        for member in t:
            if member.isdir():continue
            need(member.isfile() and member.name.startswith(prefix),'Unexpected archive entry')
            rel=member.name[len(prefix):]
            need(rel and not rel.startswith('/') and '..' not in Path(rel).parts and rel not in seen,'Unsafe/duplicate archive path')
            need(rel in expected['members'],'Unbound archive member: '+rel)
            data=t.extractfile(member).read();want=expected['members'][rel]
            need(hashlib.sha256(data).hexdigest()==want['sha256'] and len(data)==want['bytes'],'Archive member changed: '+rel)
            target=BIN/rel;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data);target.chmod(want['mode']&0o777)
            seen.add(rel)
    need(seen==set(expected['members']),'Archive did not supply every pinned member')
    return sorted(seen)

def guard():
    want={p:v['sha256'] for p,v in SPEC['harness']['members'].items()}
    want.update({p:v['sha256'] for p,v in SPEC['candidate']['members'].items()})
    for p,h in want.items():need(sha(BIN/p)==h,'Overlay component changed: '+p)
    for p,h in COHORT['tracked_files'].items():
        need(sha(ROOT/p)==h,'Frozen source/oracle changed: '+p)
        prefix='Source/IntegrationTests/TestFiles/'
        need(p.startswith(prefix),'Unexpected cohort closure boundary')
        need(sha(BIN/'TestFiles'/p[len(prefix):])==h,'Copied source/oracle changed: '+p)
    for row in SPEC['solvers']:
        need(sha(BIN/'z3/bin'/('z3-'+row['version']))==row['executable_sha256'],'Solver bytes changed')
    return {'overlay_component_files':len(want),'candidate_overlay_files':len(SPEC['candidate']['members']),
            'cohort_closure_files':len(COHORT['tracked_files']),'all_hash_guards_passed':True}

def ready():
    need((OUT/'reuse-receipt.json').is_file() and not (OUT/'prepare-failure.json').exists(),'Binary reuse preparation did not complete')
    receipt=load(OUT/'reuse-receipt.json')
    need(receipt['spec_sha256']==sha(HERE/'spec.json') and receipt['cohort_sha256']==sha(HERE/'cohort/cohort.json'),'Preparation input changed')
    return guard()

def prepare():
    OUT.mkdir(exist_ok=True)
    for name in ['DAFNY_INTEGRATION_TESTS_ONLY_COMPILERS','DAFNY_INTEGRATION_TESTS_UPDATE_EXPECT_FILE',
      'DAFNY_INTEGRATION_TESTS_IN_PROCESS','DAFNY_INTEGRATION_TESTS_MODE','DAFNY_RELEASE','DAFNY_EXTRA_TEST_ARGUMENTS',
      'XUNIT_SHARD','XUNIT_SHARD_COUNT']:
        need(not os.environ.get(name),'Unexpected harness override: '+name)
    head=logged(['git','rev-parse','HEAD'],ROOT,OUT/'source-checkout',60)
    need(head['exit']==0 and (OUT/'source-checkout/stdout.txt').read_text().strip()==SPEC['source_checkout'],'Wrong frozen source checkout')
    need(sha(HERE/'cohort/cohort.json')==SPEC['cohort_sha256'],'Cohort binding changed')
    for key,file in [('harness','old-artifact.json'),('candidate','candidate-artifact.json')]:
        metadata=load(OUT/'setup'/file);wanted=SPEC[key]
        need(metadata['id']==wanted['artifact_id'] and metadata['name']==wanted['artifact'] and
             metadata['digest']==wanted['artifact_digest'] and not metadata['expired'] and
             metadata['workflow_run']['id']==wanted['run'],'Public artifact API identity changed')
    for section in ['IntegrationTests_compiled_source_sha256','XUnitExtensions_TestDafny_source_sha256']:
        for p,h in SPEC['harness'][section].items():need(sha(ROOT/p)==h,'Old harness source changed: '+p)
    BIN.mkdir(parents=True,exist_ok=True)
    old=unpack(WORK/'artifacts/harness'/SPEC['harness']['inner_tar'],'./',SPEC['harness'])
    meta=WORK/'artifacts/candidate/candidate/compiler-components.json'
    need(sha(meta)==SPEC['candidate']['metadata_sha256'],'Candidate identity metadata changed')
    need(load(meta)==SPEC['candidate']['identity'],'Candidate identity mismatched')
    new=unpack(WORK/'artifacts/candidate'/SPEC['candidate']['inner_tar'],'dafny/',SPEC['candidate'])
    shutil.copytree(ROOT/'Source/IntegrationTests/TestFiles',BIN/'TestFiles',dirs_exist_ok=True)
    source=ROOT/'node_modules/bignumber.js'
    need(source.is_dir(),'Missing pinned JavaScript dependency')
    shutil.copytree(source,COPIED/'node_modules/bignumber.js',dirs_exist_ok=True)
    # Install the exact existing default solver and the explicit review solver.
    # No source or literal flag is changed to select a different default.
    solvers={}
    for row in SPEC['solvers']:
        archive=OUT/'setup'/('z3-'+row['version']+'.zip')
        need(sha(archive)==row['archive_sha256'],'Solver download checksum mismatch')
        with zipfile.ZipFile(archive) as z:data=z.read(row['member'])
        need(hashlib.sha256(data).hexdigest()==row['executable_sha256'],'Wrong solver executable')
        for directory in [BIN/'z3/bin',ROOT/'Binaries/z3/bin',ROOT.parent/'unzippedRelease/dafny/z3/bin']:
            directory.mkdir(parents=True,exist_ok=True);target=directory/('z3-'+row['version']);target.write_bytes(data);target.chmod(0o755)
        r=logged([BIN/'z3/bin'/('z3-'+row['version']),'--version'],BIN,OUT/'setup'/('version-z3-'+row['version']),60)
        need(r['exit']==0 and ('Z3 version '+row['version']) in (OUT/'setup'/('version-z3-'+row['version'])/'stdout.txt').read_text(),'Solver version mismatch')
        solvers[row['version']]=row['executable_sha256']
    version=logged(['dotnet',BIN/'Dafny.dll','--version'],BIN,OUT/'candidate-version',60)
    text=(OUT/'candidate-version/stdout.txt').read_text().strip()
    need(version['exit']==0 and text==SPEC['expected_candidate_version'],'Overlaid compiler version mismatch')
    g=guard()
    save(OUT/'reuse-receipt.json',{'spec_sha256':sha(HERE/'spec.json'),'cohort_sha256':sha(HERE/'cohort/cohort.json'),
      'old_harness_public_run':SPEC['harness']['run'],'candidate_public_run':SPEC['candidate']['run'],
      'old_harness_archive_sha256':SPEC['harness']['inner_tar_sha256'],'candidate_archive_sha256':SPEC['candidate']['inner_tar_sha256'],
      'old_members':len(old),'overlaid_members':len(new),'actual_candidate_version':text,'guards':g,
      'compiled_harness_source_boundary':SPEC['harness']['source_boundary'],'solvers':solvers,
      'backend_tools':{name:shutil.which(name) for name in ['dotnet','node','python3','java','javac','go','goimports']},
      'not_same_source_build':True,'API_compatibility_pending_actual_execution':True})

def discover():
    ready();r=logged(['dotnet','vstest',BIN/'IntegrationTests.dll','--ListTests'],BIN,OUT/'discovery',300)
    need(r['exit']==0 and not r['timed_out'],'Harness discovery failed')
    text=(OUT/'discovery/stdout.txt').read_text();marker='The following Tests are available:'
    need(text.count(marker)==1,'Missing/ambiguous unfiltered discovery')
    names=[s.strip() for s in text.split(marker,1)[1].splitlines() if s.strip()]
    need(names and not any(n.startswith(('No test','Error:','Warning:')) for n in names),'Invalid unfiltered discovery')
    roots=[r['source'].split('TestFiles/LitTests/LitTest/',1)[1] for r in COHORT['rows']]
    selected=[n for n in names if n in roots]
    need(collections.Counter(selected)==collections.Counter(roots),'Not all exact97 root display names discovered')
    save(OUT/'discovery/inventory.json',{'all_names':names,'all_count':len(names),'selected':selected,
      'selected_count':len(selected),'source_root_names':roots,'previous_unfiltered_count_context':SPEC['old_unfiltered_Integration_discovery_count'],
      'complete_exact_source_selection':True})

def run():
    before=ready();inventory=load(OUT/'discovery/inventory.json')
    roots=inventory['source_root_names'];filter_value='|'.join('DisplayName='+p for p in roots)
    where=OUT/'literal-harness';where.mkdir(exist_ok=True)
    r=logged(['dotnet','vstest',BIN/'IntegrationTests.dll','--TestCaseFilter:'+filter_value,
      '--Logger:console;verbosity=normal','--Logger:trx;LogFileName=safety.trx','--ResultsDirectory:'+str(where)],BIN,where/'run',10800)
    receipt={'process':r,'passed':False,'incomplete':False,'scope':'Unchanged canonical literal97 roots /341RUN lines; no globalAX reinterpretation'}
    trx=where/'safety.trx'
    if trx.exists():
        tree=ET.parse(trx);rows=[dict(x.attrib) for x in tree.findall('.//t:UnitTestResult',NS)]
        nodes=tree.findall('.//t:UnitTest',NS)
        definitions={x.attrib['id']:dict(x.find('t:TestMethod',NS).attrib) for x in nodes}
        counters=tree.find('.//t:ResultSummary/t:Counters',NS)
        counts={k:int(v) for k,v in counters.attrib.items()} if counters is not None else {}
        exact=collections.Counter(x['testName'] for x in rows)==collections.Counter(roots)
        methods=all(definitions[x['testId']]['className']=='IntegrationTests.LitTests' and definitions[x['testId']]['name']=='LitTest' for x in rows)
        receipt.update({'TRX_sha256':sha(trx),'rows':rows,'definitions':definitions,'counters':counts,
          'discovery_source_row_multiset_exact':exact,'exact_source_method':methods})
        receipt['passed']=r['exit']==0 and not r['timed_out'] and exact and methods and len(rows)==97 and counts.get('total')==counts.get('executed')==counts.get('passed')==97 and all(x['outcome']=='Passed' for x in rows)
    else:receipt.update({'incomplete':True,'failure':'TRX missing'})
    receipt['guards_before']=before;receipt['guards_after']=guard()
    receipt['existing_generated_BPL_or_print_files']={str(p.relative_to(BIN)):sha(p) for p in sorted((BIN/'TestFiles').rglob('*')) if p.is_file() and p.suffix in ['.bpl','.print']}
    save(where/'receipt.json',receipt)

def option_override(args,name,value):
    result=[];index=0
    while index<len(args):
        a=args[index]
        if a==name:index+=2;continue
        if a.startswith(name+'=') or a.startswith(name+':'):index+=1;continue
        result.append(a);index+=1
    return result+[name+'='+value]

def log_cost(csv_path,json_path,expected_exit):
    if not csv_path.exists() or not json_path.exists():
        need(expected_exit==2,'Verifier did not produce complete CSV/JSON resource logs')
        return {'status':'resolver-only; no proof-resource claim','total_ru':None,'csv_rows':0}
    # CSVTestLogger leaves generic names containing commas unquoted. Parse the
    # fixed final four columns from the right, then bind every raw row to JSON.
    header=['TestResult.DisplayName','TestResult.Outcome','TestResult.Duration',
            'TestResult.ResourceCount','RandomSeed']
    lines=csv_path.read_text().splitlines()
    need(lines and lines[0]==','.join(header),'Unexpected CSV resource schema')
    fields=[line.rsplit(',',4) for line in lines[1:] if line]
    need(all(len(row)==5 for row in fields),'Truncated CSV resource row')
    rows=[dict(zip(header,row)) for row in fields]
    observed=[(row[header[0]],int(row[header[3]]),row[header[1]],row[header[4]]) for row in rows]
    values=json.loads(json_path.read_text())['verificationResults']
    statuses=[];json_total=0;expected=[];seed_presence=[]
    for value in values:
        resource=value['resourceCount']
        need(type(resource) is int and resource>=0,'Invalid JSON declaration resource count')
        json_total+=resource;statuses.append(value['outcome'])
        batches=value['vcResults']
        need(all(type(vc['resourceCount']) is int and vc['resourceCount']>=0 for vc in batches),
             'Invalid JSON batch resource count')
        need(value['outcome'] in ['Correct','Errors'] and all(vc['outcome'] in ['Valid','Invalid'] for vc in batches),
             'Incomplete solver outcome in JSON declaration or batch')
        merged='Errors' if any(vc['outcome']=='Invalid' for vc in batches) else 'Correct'
        need(value['outcome']==merged,'JSON declaration outcome disagrees with its complete batch outcomes')
        need(sum(int(vc['resourceCount']) for vc in batches)==int(value['resourceCount']),
             'JSON declaration and batch resource totals disagree')
        for vc in batches:
            statuses.append(vc['outcome'])
            name=value['name']+(' (assertion batch '+str(vc['vcNum'])+')' if len(batches)>1 else '')
            # The int-typed TestProperty exposes default 0 when it is unset;
            # JSON omits that property. This is a logger representation check,
            # never evidence of an actual solver random-seed parameter.
            reported_seed=str(vc['randomSeed']) if 'randomSeed' in vc else '0'
            seed_presence.append({'name':name,'vcNum':vc['vcNum'],
                                  'JSON_seed_present':'randomSeed' in vc,
                                  'JSON_reported_seed':vc.get('randomSeed'),
                                  'expected_CSV_logger_field':reported_seed})
            expected.append((name,int(vc['resourceCount']),'Passed' if vc['outcome']=='Valid' else 'Failed',reported_seed))
    need(all(x in ['Correct','Errors','Valid','Invalid'] for x in statuses),
         'Incomplete solver outcome: '+str(statuses))
    need(all(row[1]>=0 for row in observed),'Negative CSV resource count')
    if expected_exit==0:
        need(all(x in ['Correct','Valid'] for x in statuses),'Positive invocation has a failed JSON proof scope')
    need(collections.Counter(observed)==collections.Counter(expected),
         'CSV and JSON per-batch names, outcomes, resources or reported seeds disagree')
    need(sum(row[1] for row in observed)==json_total,'CSV and JSON resource totals disagree')
    if expected_exit==2:
        need(not values and not rows,'Resolver rejection unexpectedly produced proof scopes')
        return {'status':'resolver only; no proof-resource claim','total_ru':None,
                'csv_sha256':sha(csv_path),'json_sha256':sha(json_path)}
    return {'status':'complete','total_ru':json_total,'csv_rows':len(rows),'json_scopes':len(values),'outcomes':statuses,
            'csv_sha256':sha(csv_path),'json_sha256':sha(json_path),
            'seed_field_presence':seed_presence,
            'seed_boundary':'CSV typed-property default0 and omitted JSON fields are retained as logger representations; actual solver seeds are not inferred'}

def semantic():
    """Derived observation: literal macro semantics + explicit AX/351 observer axes."""
    ready()
    defaults=['verify','--type-system-refresh','--general-traits=datatype','--general-newtypes',
      '--use-basename-for-filename','--show-snippets:false','--standard-libraries:false',
      '--cores:2','--verification-time-limit:300','--resource-limit:50e6']
    mapping=[]
    for row in COHORT['rows']:
        source=COPIED/row['source'].split('TestFiles/LitTests/LitTest/',1)[1]
        for run_row in row['literal_RUNs']:
            tokens=shlex.split(run_row['literal']);expected=0
            if tokens[0]=='%exits-with':expected=int(tokens[1]);tokens=tokens[2:]
            if tokens[0] not in ['%verify','%baredafny']:continue
            tokens=tokens[:next((i for i,t in enumerate(tokens) if t in ['>','>>']),len(tokens))]
            command=defaults+tokens[1:] if tokens[0]=='%verify' else tokens[1:]
            need(command[0] in ['verify','resolve'],'Unknown direct semantic command')
            ident=hashlib.sha256((row['source']+'\0'+str(run_row['line'])+'\0'+run_row['literal']).encode()).hexdigest()[:20]
            modes=[False,True] if command[0]=='verify' else [None]
            for axis in modes:
                where=OUT/'semantic-projections'/(ident+('-AX'+str(axis).lower() if axis is not None else '-resolver'))
                where.mkdir(parents=True,exist_ok=True)
                args=[a.replace('%S',str(source.parent)).replace('%s',str(source)).replace('%t',str(where/'literal-temp')).replace('%repositoryRoot',str(ROOT))
                      .replace('%review-z3',str(BIN/'z3/bin/z3-5.1.0')).replace('%z3',str(BIN/'z3/bin/z3-4.12.1')) for a in command]
                if axis is not None:
                    args=option_override(args,'--additional-axioms',str(axis).lower())
                    args=option_override(args,'--solver-path',str(BIN/'z3/bin/z3-5.1.0'))
                    args=option_override(args,'--bprint',str(where/'program.bpl'))
                    args+=['--log-format','csv;LogFileName='+str(where/'resources.csv'),'--log-format','json;LogFileName='+str(where/'results.json')]
                before=guard();r=logged(['dotnet',BIN/'Dafny.dll',*args],BIN,where,1200)
                text=(where/'stdout.txt').read_text()+(where/'stderr.txt').read_text()
                resources=None
                if axis is not None:
                    try:resources=log_cost(where/'resources.csv',where/'results.json',expected)
                    except Exception as error:resources={'complete':False,'error':type(error).__name__+': '+str(error)}
                receipt={'source_run_id':ident,'source':row['source'],'source_sha256':row['source_sha256'],'body_sha256':row['body_sha256'],
                  'literal_RUN':run_row,'actual_AX':axis,'solver_axis':'5.1.0' if axis is not None else None,'expected_literal_exit':expected,
                  'process':r,'raw_resource_receipt':resources,'raw_BPL_sha256':{p.name:sha(p) for p in where.glob('program*.bpl')},
                  'summary_lines':[s for s in text.splitlines() if 'verifier finished' in s],
                  'diagnostic_lines':[s for s in text.splitlines() if 'Error:' in s or 'Related location:' in s],
                  'warning_lines':[s for s in text.splitlines() if re.search(r'\bwarning\b',s,re.I)],
                  'guards_before':before,'guards_after':guard(),'original_oracles':row['oracles'],
                  'exit_matches_original':r['exit']==expected and not r['timed_out'],
                  'classification':'Derived explicit effectiveAX/351 replay; canonical literal execution is independent',
                  'reviewed_acceptance':False}
                save(where/'receipt.json',receipt);mapping.append({'id':ident,'AX':axis,'receipt_sha256':sha(where/'receipt.json')})
    need(len(mapping)==320,'Expected318 verify projections and2 resolver observations')
    save(OUT/'semantic-projections/mapping.json',{'runs':mapping,'verify_projections':318,'resolver_observations':2,'all_source_semantic_RUNs_mapped':True})

def supplemental():
    # Fresh observation only, no guessed golden or universal proof-success claim.
    ready()
    for name,row in SPEC['supplemental'].items():
        source=HERE/'supplemental'/name;need(sha(source)==row['sha256'],'Supplemental source changed')
        for refresh in [False,True]:
            for axioms in [False,True]:
                ident=name+'-refresh'+str(refresh).lower()+'-AX'+str(axioms).lower()
                where=OUT/'supplemental'/ident;where.mkdir(parents=True,exist_ok=True)
                before=guard()
                args=['dotnet',BIN/'Dafny.dll','verify',source,'--solver-path',BIN/'z3/bin/z3-5.1.0',
                  '--cores=1','--resource-limit=16000000','--verification-time-limit=60','--show-snippets=false',
                  '--allow-warnings','--use-basename-for-filename','--error-limit=0','--boogie','/normalizeDeclarationOrder:0',
                  '--type-system-refresh='+str(refresh).lower(),'--general-newtypes='+str(refresh).lower(),
                  '--additional-axioms='+str(axioms).lower(),'--bprint='+str(where/'program.bpl'),
                  '--log-format','json;LogFileName='+str(where/'results.json'),'--log-format','csv;LogFileName='+str(where/'resources.csv')]
                r=logged(args,BIN,where,300)
                text=(where/'stdout.txt').read_text()+(where/'stderr.txt').read_text()
                save(where/'receipt.json',{'source':name,'source_sha256':row['sha256'],'actual_refresh':refresh,'actual_AX':axioms,
                  'process':r,'proposed_exit':4,'exact_expected_counts':'pending review of actual source boundary',
                  'summary_lines':[line for line in text.splitlines() if 'verifier finished' in line],
                  'diagnostic_lines':[line for line in text.splitlines() if 'Error:' in line or 'Related location:' in line],
                  'warning_lines':[line for line in text.splitlines() if re.search(r'\bwarning\b',line,re.I)],
                  'raw_BPL_sha256':{p.name:sha(p) for p in where.glob('program*.bpl')},
                  'guards_before':before,'guards_after':guard(),'reviewed_acceptance':False})

def report():
    r={'reviewed_acceptance':False,'complete_trusted_gate':False,
       'public_run':'https://github.com/erniecohen/dafny/actions/runs/'+os.environ['GITHUB_RUN_ID'],
       'spec_sha256':sha(HERE/'spec.json'),'cohort_sha256':sha(HERE/'cohort/cohort.json'),
       'phase_receipts':{str(p.relative_to(OUT)):sha(p) for p in OUT.rglob('*.json')},
       'canonical_literal_boundary':'Exact original commands/oracles; no additionalAX mode inferred from a suite label',
       'execution_phases':['prepare','discover','run','report'],
       'derived_semantic_execution':'Not executed by this workflow',
       'supplemental_boundary':'Not executed by this workflow; separate observations remain pending',
       'source_adoption':'None; candidate remains separate from authoritative product'}
    if (OUT/'literal-harness/receipt.json').exists():r['literal_harness_passed']=load(OUT/'literal-harness/receipt.json')['passed']
    else:r['literal_harness_passed']=False
    save(OUT/'report.json',r);print(json.dumps(r,indent=2))

if __name__=='__main__':
    phase=sys.argv[1]
    try:globals()[phase]()
    except Exception as error:
        save(OUT/(phase+'-failure.json'),{'phase':phase,'error':type(error).__name__+': '+str(error),'complete':False})
        print(type(error).__name__+': '+str(error),file=sys.stderr)
    # An expected-negative diagnostic records real failure; it never mails a
    # false failing Actions wrapper or upgrades source acceptance from exit0.
