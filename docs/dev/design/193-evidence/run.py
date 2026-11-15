#!/usr/bin/env python3
"""Run the design-only #193 corpus. Expected diagnostic failures do not fail CI."""
import argparse, concurrent.futures, csv, hashlib, json, os, re, subprocess, time
from pathlib import Path

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def main():
    ap=argparse.ArgumentParser()
    for s in ['dafny','z3','corpus','output']: ap.add_argument('--'+s,required=True)
    ap.add_argument('--dotnet');ap.add_argument('--source-commit',required=True)
    ap.add_argument('--resource-limit',type=int,default=200000)
    ap.add_argument('--workers',type=int,default=2)
    ap.add_argument('--mode',action='append')
    ap.add_argument('--refresh',action='store_true')
    ap.add_argument('--solver-log',action='store_true')
    a=ap.parse_args();out=Path(a.output);out.mkdir(parents=True,exist_ok=True)
    corpus=Path(a.corpus);manifest=json.loads((corpus/'manifest.json').read_text())
    if a.mode:manifest=[x for x in manifest if x['mode'] in a.mode]
    prefix=[a.dotnet,a.dafny] if a.dotnet else [a.dafny]
    env=os.environ.copy()
    if a.dotnet:env['DOTNET_ROOT']=str(Path(a.dotnet).resolve().parent)

    def job(info):
        name=Path(info['file']).stem;source=corpus/info['file'];log=out/(name+'.csv');jlog=out/(name+'.json')
        cmd=prefix+['verify',str(source),'--additional-axioms','--cores','1',
          '--verification-time-limit','15','--resource-limit',str(a.resource_limit),
          '--solver-path',a.z3,'--boogie','/normalizeDeclarationOrder:0',
          '--boogie','/restartProver','--log-format',f'csv;LogFileName={log}',
          '--log-format',f'json;LogFileName={jlog}',
          '--show-snippets:false','--use-basename-for-filename','--allow-axioms',
          '--type-system-refresh:'+str(a.refresh).lower()]
        if a.solver_log:cmd+=['--solver-log',str(out/(name+'.smt2'))]
        started=time.monotonic()
        try:r=subprocess.run(cmd,env=env,text=True,capture_output=True,timeout=420);code=r.returncode;output=r.stdout+r.stderr
        except subprocess.TimeoutExpired as e:
            code='wall-timeout';output=(e.stdout or b'').decode() if isinstance(e.stdout,bytes) else e.stdout or ''
        (out/(name+'.txt')).write_text(output)
        # Public receipt records portable arguments rather than runner-local absolute paths.
        (out/(name+'.command.json')).write_text(json.dumps({'command':['dafny','verify',info['file'],'--additional-axioms','--cores','1','--verification-time-limit','15','--resource-limit',str(a.resource_limit),'--solver-path','Z3','--boogie','/normalizeDeclarationOrder:0','--boogie','/restartProver','--log-format','csv','--show-snippets:false','--use-basename-for-filename','--allow-axioms','--type-system-refresh:'+str(a.refresh).lower()],'source_sha256':sha(source)},indent=2)+'\n')
        rows=list(csv.DictReader(log.open())) if log.exists() else []
        exact=json.loads(jlog.read_text()).get('verificationResults',[]) if jlog.exists() else []
        outcomes={x['name']:x['outcome'] for x in exact}
        results=[]
        for row in rows:
            name=row['TestResult.DisplayName'];m=re.match(r'(client|control)_(\w+) \((.*?)\)',name)
            if not m:continue
            op=m[2];outcome=outcomes.get(name,row['TestResult.Outcome'])
            outcome={'Correct':'Passed','Errors':'Failed','TimedOut':'Timeout'}.get(outcome,outcome)
            results.append({'width':info['width'],'mode':info['mode'],'operation':op,'batch':m[3],'outcome':outcome,'ru':int(row['TestResult.ResourceCount'])})
        print(info['file'],code,len(results),flush=True)
        all_batches=[{'name':x['name'],'outcome':x['outcome'],'ru':x['resourceCount'],'vcResults':[{'vcNum':v['vcNum'],'outcome':v['outcome'],'ru':v['resourceCount']} for v in x['vcResults']]} for x in exact]
        return {**info,'exit':code,'source_sha256':sha(source),'results':results,'all_batches':all_batches,'summary':re.findall(r'Dafny program verifier finished.*',output)}

    with concurrent.futures.ThreadPoolExecutor(max_workers=a.workers) as pool:files=list(pool.map(job,manifest))
    rows=[row for f in files for row in f['results']]
    (out/'results.json').write_text(json.dumps(files,indent=2)+'\n')
    with (out/'results.csv').open('w') as f:
        wr=csv.DictWriter(f,fieldnames=['width','mode','operation','batch','outcome','ru']);wr.writeheader();wr.writerows(rows)
    receipt={'source_commit':a.source_commit,'dafny_version':subprocess.check_output(prefix+['--version'],env=env,text=True).strip(),
      'solver_version':subprocess.check_output([a.z3,'--version'],text=True).strip(),'dafny_sha256':sha(a.dafny),'solver_sha256':sha(a.z3),
      'resource_limit':a.resource_limit,'refresh':a.refresh,'additional_axioms':True,
      'scope':'Design-only API projection. Candidate bridge contracts are trusted, not proved implementations. Assume-false baseline is vacuous. Native mode asks the unmodified verifier to establish the same client postconditions with no body hints. Candidate clients use no assume but depend on explicit axiom contracts. Vacuity controls must fail.'}
    (out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    report=['# #193 design probe','',receipt['scope'],'',f"Source: `{a.source_commit}`; solver: `{receipt['solver_version']}`; cap: {a.resource_limit:,} RU.",'', '| operation | width | assume false | native | raw candidate | raw false control | fused | fused false control | native helper | int reference |','| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |']
    index={(r['width'],r['mode'],r['operation']):r for r in rows if r['batch']=='correctness'}
    for op in manifest[0]['operations'] if manifest else []:
        for w in [8,16,32,64]:
            cells=[]
            for mode in ['assume_false','native','candidate','vacuity','fused','fused_vacuity','native_helpers','int_reference']:
                r=index.get((w,mode,op));cells.append('—' if r is None else f"{r['outcome']} {r['ru']:,}")
            report.append('| '+op+' | '+str(w)+' | '+' | '.join(cells)+' |')
    (out/'summary.md').write_text('\n'.join(report)+'\n')
    # An expected diagnostic failure is recorded, never converted into acceptance.

if __name__=='__main__':main()
