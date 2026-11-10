#!/usr/bin/env python3
import argparse,concurrent.futures,csv,hashlib,json,os,re,subprocess
from pathlib import Path

def main():
 p=argparse.ArgumentParser()
 for k in ['dotnet','dafny','source_dir','z3_4','z3_5','output']:p.add_argument('--'+k,required=True)
 a=p.parse_args();out=Path(a.output);out.mkdir(exist_ok=True,parents=True);env=os.environ.copy();env['DOTNET_ROOT']=str(Path(a.dotnet).parent)
 def run(spec):
  version,z3,refresh,kind=spec;name=f'{kind}-z3-{version}-refresh-{int(refresh)}';dest=out/name;dest.mkdir(exist_ok=True)
  src=Path(a.source_dir)/('real-completeness.dfy' if kind=='positive' else 'real-completeness-negative.dfy')
  cmd=[a.dotnet,a.dafny,'verify',str(src),'--solver-path',z3,'--cores','1','--resource-limit','2000000','--verification-time-limit','30','--boogie','/normalizeDeclarationOrder:0','--boogie','/restartProver','--boogie','/emitDebugInformation:1','--solver-log',str(dest/'input.smt2'),'--log-format',f'csv;LogFileName={dest/"results.csv"}','--show-snippets:false','--use-basename-for-filename',f'--type-system-refresh:{str(refresh).lower()}','--additional-axioms:true']
  r=subprocess.run(cmd,env=env,capture_output=True,text=True,timeout=600);(dest/'output.txt').write_text(r.stdout+r.stderr)
  rows=list(csv.DictReader((dest/'results.csv').open())) if (dest/'results.csv').exists() else []
  result=dict(version=version,refresh=refresh,kind=kind,exit=r.returncode,total_ru=sum(int(x['TestResult.ResourceCount']) for x in rows),source_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),cases=[dict(name=x['TestResult.DisplayName'],outcome=x['TestResult.Outcome'],ru=int(x['TestResult.ResourceCount'])) for x in rows],summary=re.findall(r'^Dafny program verifier finished.*$',r.stdout,re.M),errors=re.findall(r'^.*Error:.*$',r.stdout,re.M))
  (dest/'summary.json').write_text(json.dumps(result,indent=2));print(name,result['total_ru'],result['exit'],result['summary'],flush=True);return result
 specs=[(v,z,r,k) for v,z in [('4.12.1',a.z3_4),('5.1.0',a.z3_5)] for r in [False,True] for k in ['positive','negative']]
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as ex:results=list(ex.map(run,specs))
 (out/'results.json').write_text(json.dumps(results,indent=2))
 (out/'manifest.json').write_text(json.dumps({'dafny_version':subprocess.check_output([a.dotnet,a.dafny,'--version'],env=env,text=True).strip(),'scope':'Full arbitrary coefficient-sequence IVT and LUB proof with exactly one experimental trusted complete placeholder; exact native real/ISet/Box encoding. No product implementation. Negatives are three live assertfalse controls.'},indent=2))
if __name__=='__main__':main()
