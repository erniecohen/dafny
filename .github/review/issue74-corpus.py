#!/usr/bin/env python3
"""Paired merged-baseline/candidate off/on measurements using the verifier suite's established options.

This runner deliberately records probe failures in report.json and exits zero.
The review workflow remains the required verdict/regression gate. Inspect report.json
before treating this performance experiment as accepted.
"""
import argparse
import concurrent.futures
import hashlib
import importlib.util
import json
from pathlib import Path
import shlex
import subprocess
import time

p=argparse.ArgumentParser()
p.add_argument('--baseline',required=True)
p.add_argument('--candidate',required=True)
p.add_argument('--z3',required=True)
p.add_argument('--output',required=True)
p.add_argument('--shard',default='0/1')
p.add_argument('--jobs',type=int,default=4)
p.add_argument('--only',default='')
a=p.parse_args()
out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
litdir=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest').resolve()
spec=importlib.util.spec_from_file_location('lit', '.github/review/lit-verdicts.py')
lit=importlib.util.module_from_spec(spec);spec.loader.exec_module(lit)
lit.plan(str(litdir),str(out/'plan.tsv'))
rows=[line.split('\t') for line in (out/'plan.tsv').read_text().splitlines()]
if a.only: rows=[r for r in rows if r[0] in a.only.split(',')]
i,n=map(int,a.shard.split('/'));rows=rows[i::n]

def run_one(task):
    path,opts=task
    own=shlex.split(opts)
    # lit-verdicts' plan drops output placeholders, leaving bare print options.
    # Drop those output-only options before adding the measurement log argument.
    own=[t for t in own if t not in ['--print', '--bprint', '--rprint']]
    if own and own[-1] == '--library': own.append(path)
    names={t.split('=')[0].split(':')[0] for t in own if t.startswith('--')}
    fixed=[];k=0
    while k<len(lit.FIXED):
        name=lit.FIXED[k];takes=k+1<len(lit.FIXED) and not lit.FIXED[k+1].startswith('--')
        if name not in names:fixed+=lit.FIXED[k:k+2] if takes else [name]
        k+=2 if takes else 1
    results={}
    for mode,binary,flag in [('B',a.baseline,[]),('O',a.candidate,[]),('BE',a.baseline,['--additional-axioms']),('E',a.candidate,['--additional-axioms'])]:
        dest=out/mode/path;dest.mkdir(parents=True,exist_ok=True)
        log=dest/'results.json'
        cmd=[binary,'verify',path,'--solver-path',a.z3]+fixed+own+flag+['--log-format','json;LogFileName='+str(log)]
        (dest/'command.json').write_text(json.dumps(cmd))
        try:
            result=subprocess.run(cmd,cwd=litdir,capture_output=True,text=True,timeout=lit.TIMEOUT)
            rc=result.returncode;text=result.stdout+result.stderr
        except subprocess.TimeoutExpired:
            rc='TIMEOUT';text='TIMEOUT'
        (dest/'output.txt').write_text(text)
        decls=json.loads(log.read_text()).get('verificationResults',[]) if log.exists() else []
        batches={f"{d['name']} #{v['vcNum']}":[v['outcome'],v['resourceCount']] for d in decls for v in d['vcResults']}
        # Verifier diagnostics form the same verdict boundary as lit-verdicts.py.
        import re
        verdict=[rc,''.join(re.findall(r'verifier finished with ([^\n]*)',text)), sorted(set(re.findall(r'\((\d+),\d+\): Error',text)),key=int)]
        results[mode]={'verdict':verdict,'batches':batches}
    b,o,be,e=(results[m] for m in ['B','O','BE','E'])
    off=[];regressions=[];outliers=[];improvements=[]
    if b['verdict']!=o['verdict']:off.append('verdict')
    for key in sorted(set(b['batches'])|set(o['batches'])):
        if b['batches'].get(key)!=o['batches'].get(key):off.append(key)
    for key,old in be['batches'].items():
        new=e['batches'].get(key)
        if new is None or (old[0]=='Valid' and new[0]!='Valid'):regressions.append([key,old,new])
        elif old[0]!='Valid' and new[0]=='Valid':improvements.append([key,old,new])
        if new and old[0]==new[0]=='Valid' and new[1]>2*old[1] and new[1]-old[1]>100000:outliers.append([key,old[1],new[1]])
    result={'path':path,'off_differences':off,'enabled_regressions':regressions,'enabled_improvements':improvements,'enabled_outliers':outliers,'results':results}
    print(path,'off',len(off),'regressions',len(regressions),'outliers',len(outliers),flush=True)
    return result

with concurrent.futures.ThreadPoolExecutor(a.jobs) as executor:
    results=list(executor.map(run_one,rows))
summary={'programs':len(results),'off_differences':[r['path'] for r in results if r['off_differences']], 'enabled_regressions':[r['path'] for r in results if r['enabled_regressions']], 'outliers':[r['path'] for r in results if r['enabled_outliers']], 'results':results}
(out/'report.json').write_text(json.dumps(summary,indent=2))
with (out/'batches.tsv').open('w') as f:
    f.write('program\tmode\tbatch\toutcome\tresources\n')
    for r in results:
        for mode,data in r['results'].items():
            for key,val in data['batches'].items():f.write('\t'.join(map(str,[r['path'],mode,key,*val]))+'\n')
print(json.dumps({k:v for k,v in summary.items() if k!='results'},indent=2))
