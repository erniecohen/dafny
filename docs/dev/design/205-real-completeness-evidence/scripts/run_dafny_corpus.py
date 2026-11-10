#!/usr/bin/env python3
"""Collect genuine Dafny real-heavy VCs, then replay dormant completeness candidates."""
import argparse,csv,hashlib,json,os,re,subprocess
from pathlib import Path
from kernel_common import COMMON,AXIOM
from run_kernel import PATTERNS,NESTED
FILES=['dafny0/RealTypes.dfy','dafny0/RealCompare.dfy','dafny0/TypeConversions.dfy','dafny0/TypeMembers.dfy','dafny4/Regression3.dfy']
def profile(s):
 d={}
 for q,n in re.findall(r'\[quantifier_instances\]\s*(.*?)\s*:\s*(\d+)\s*:',s):d[q.strip()]=d.get(q.strip(),0)+int(n)
 return d

def main():
 p=argparse.ArgumentParser()
 for f in ('dafny','dotnet','source','z3','output'):p.add_argument('--'+f,required=True)
 a=p.parse_args();out=Path(a.output);out.mkdir(parents=True,exist_ok=True)
 env=os.environ.copy();env['DOTNET_ROOT']=str(Path(a.dotnet).parent)
 rows=[]
 for name in FILES:
  f=Path(a.source)/'Source/IntegrationTests/TestFiles/LitTests/LitTest'/name;stem=f.stem
  log=out/(stem+'.smt2');csvlog=out/(stem+'.csv')
  cmd=[a.dotnet,a.dafny,'verify',str(f),'--solver-path',a.z3,'--cores','1','--verification-time-limit','20','--resource-limit','16000000','--solver-log',str(log),'--boogie','/normalizeDeclarationOrder:0','--boogie','/restartProver','--log-format',f'csv;LogFileName={csvlog}','--show-snippets:false','--use-basename-for-filename']
  r=subprocess.run(cmd,env=env,text=True,capture_output=True,timeout=180)
  (out/(stem+'.dafny.out')).write_text(r.stdout+r.stderr)
  if not log.exists():
   print('NOLOG',name,r.stdout+r.stderr,flush=True);continue
  if csvlog.exists():
   results=list(csv.DictReader(csvlog.open()));total=sum(int(x['TestResult.ResourceCount']) for x in results)
  else:results=[];total=None
  rows.append({'file':name,'variant':'dafny','exit':r.returncode,'ru':total,'proofs':len(results),'outcomes':[x['TestResult.Outcome'] for x in results],'profiles':profile(r.stdout+r.stderr)})
  original=log.read_text()
  for v in ['none','broad','sup-flat','nested-sup','request','local']:
   # Local emission adds nothing because this corpus contains no new intrinsic call.
   if v in ('none','local'):injected=original
   else:
    declarations=COMMON[COMMON.index('(declare-sort'):]
    ax=NESTED if v=='nested-sup' else AXIOM%PATTERNS[v]
    injected=original.replace('; done setting options','; done setting options\n'+declarations+ax)
   target=out/(stem+'-'+v+'.smt2');target.write_text(injected)
   # One standalone solver per logged reset block matches restartProver process isolation.
   parts=injected.split('(set-option :print-success false)')[1:]
   stats=[];stdout='';stderr=''
   for part in parts:
    part='(set-option :print-success false)\n(set-option :smt.qi.profile true)\n'+part
    # Drop resets after this proof, then ask final statistics before reset.
    part=re.split(r'\(reset\)',part)[0]+'\n(get-info :all-statistics)\n'
    rr=subprocess.run([a.z3,'-in','-smt2'],input=part,text=True,capture_output=True,timeout=30)
    vals=re.findall(r'\(:rlimit (\d+)\)',rr.stdout)
    q=re.search(r':quant-instantiations\s+(\d+)',rr.stdout)
    stats.append({'ru':int(vals[-1]) if vals else None,'instances':int(q[1]) if q else 0,'outcomes':re.findall(r'^(sat|unsat|unknown)$',rr.stdout,re.M),'exit':rr.returncode})
    stdout+=rr.stdout;stderr+=rr.stderr
   (out/(stem+'-'+v+'.out')).write_text(stdout+stderr)
   row={'file':name,'variant':v,'ru':sum(x['ru'] or 0 for x in stats),'instances':sum(x['instances'] for x in stats),'proofs':len(stats),'outcomes':[x['outcomes'] for x in stats],'profiles':profile(stderr),'stats':stats,'smt_sha256':hashlib.sha256(injected.encode()).hexdigest()}
   rows.append(row);print(name,v,row['ru'],row['instances'],row['proofs'],flush=True)
 (out/'results.json').write_text(json.dumps(rows,indent=2))
 (out/'manifest.json').write_text(json.dumps({'dafny_version':subprocess.check_output([a.dotnet,a.dafny,'--version'],env=env,text=True).strip(),'solver_version':subprocess.check_output([a.z3,'--version'],text=True).strip(),'source_commit':subprocess.check_output(['git','-C',a.source,'rev-parse','HEAD'],text=True).strip(),'scope':'Genuine existing Dafny test VCs; added candidates are dormant abstract-kernel declarations, not a product implementation. Local candidate emits zero bytes on this corpus.'},indent=2))
if __name__=='__main__':main()
