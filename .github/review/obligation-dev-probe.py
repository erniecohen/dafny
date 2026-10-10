"""Diagnostic capture: actual failures are recorded and do not fail the wrapper."""
from pathlib import Path
import json, os, subprocess
root=Path('out/obligation-dev-probe');root.mkdir(parents=True,exist_ok=True)
results=[]
def phase(name,cmd,env=None):
 folder=root/name;folder.mkdir(exist_ok=True)
 (folder/'command.json').write_text(json.dumps(cmd))
 with (folder/'output.txt').open('w') as output:
  run=subprocess.run(cmd,stdout=output,stderr=subprocess.STDOUT,env=env)
 results.append({'name':name,'exit':run.returncode})
 (folder/'exit.txt').write_text(str(run.returncode)+'\n')
 return run.returncode
code=phase('build',['dotnet','publish','Source/Dafny/Dafny.csproj','-c','Release','-o','out/dafny'])
if code==0:
 env=dict(os.environ,OBLIGATION_INVENTORY_CAPTURE=str((root/'obligation-producers.json').resolve()))
 phase('structural',['dotnet','test','Source/DafnyCore.Test/DafnyCore.Test.csproj','-c','Release','--filter','FullyQualifiedName~Obligation','--logger','trx;LogFileName=tests.trx','--results-directory',str((root/'structural').resolve())],env)
(root/'results.json').write_text(json.dumps({'diagnostic':True,'phases':results},indent=2)+'\n')
summary='Diagnostic port probe; wrapper success does not mean acceptance.\n\n'+''.join(f"- {r['name']}: actual exit {r['exit']}\n" for r in results)
print(summary)
if os.environ.get('GITHUB_STEP_SUMMARY'):Path(os.environ['GITHUB_STEP_SUMMARY']).write_text(summary)
