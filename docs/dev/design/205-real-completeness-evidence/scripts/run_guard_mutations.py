#!/usr/bin/env python3
"""Intentional UNSOUND guard mutations: confirm invalid-domain controls detect them."""
import argparse,csv,json,os,re,subprocess
from pathlib import Path

def main():
 p=argparse.ArgumentParser()
 for k in ['dotnet','dafny','source_dir','z3','output']:p.add_argument('--'+k,required=True)
 a=p.parse_args();out=Path(a.output);out.mkdir(exist_ok=True,parents=True);env=os.environ.copy();env['DOTNET_ROOT']=str(Path(a.dotnet).parent);rows=[]
 source=(Path(a.source_dir)/'real-completeness.dfy').read_text();negative=(Path(a.source_dir)/'real-completeness-negative.dfy').read_text()
 for name,new in [('remove-nonempty','ensures upper(s,b) ==>'),('remove-bounded','ensures w in s ==>')]:
  dest=out/name;dest.mkdir(exist_ok=True);old='ensures w in s && upper(s,b) ==>';assert source.count(old)==2
  (dest/'real-completeness.dfy').write_text(source.replace(old,new));(dest/'real-completeness-negative.dfy').write_text(negative)
  cmd=[a.dotnet,a.dafny,'verify',str(dest/'real-completeness-negative.dfy'),'--solver-path',a.z3,'--cores','1','--resource-limit','2000000','--verification-time-limit','30','--boogie','/normalizeDeclarationOrder:0','--boogie','/restartProver','--additional-axioms:true','--show-snippets:false','--use-basename-for-filename','--log-format',f'csv;LogFileName={dest/"results.csv"}']
  r=subprocess.run(cmd,env=env,capture_output=True,text=True,timeout=180);(dest/'output.txt').write_text(r.stdout+r.stderr)
  cases=list(csv.DictReader((dest/'results.csv').open()));result={'mutation':name,'exit':r.returncode,'summary':re.findall(r'^Dafny program verifier finished.*$',r.stdout,re.M),'cases':[{'name':x['TestResult.DisplayName'],'outcome':x['TestResult.Outcome'],'ru':int(x['TestResult.ResourceCount'])} for x in cases]};rows.append(result);print(result,flush=True)
 (out/'results.json').write_text(json.dumps(rows,indent=2))
if __name__=='__main__':main()
