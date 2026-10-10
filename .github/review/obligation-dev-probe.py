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
code=phase('build',['dotnet','publish','Source/Dafny/Dafny.csproj','-c','Release','-o','out/dafny','-p:SourceRevisionId='+subprocess.check_output(['git','rev-parse','--short=8','HEAD'],text=True).strip()])
if code==0:
 env=dict(os.environ,OBLIGATION_INVENTORY_CAPTURE=str((root/'obligation-producers.json').resolve()))
 phase('structural',['dotnet','test','Source/DafnyCore.Test/DafnyCore.Test.csproj','-c','Release','--filter','FullyQualifiedName~Obligation','--logger','trx;LogFileName=tests.trx','--results-directory',str((root/'structural').resolve())],env)
if code==0:
 import hashlib, urllib.request, zipfile
 archive=root/'z3.zip'
 urllib.request.urlretrieve('https://github.com/Z3Prover/z3/releases/download/z3-5.1.0/z3-5.1.0-x64-glibc-2.39.zip',archive)
 assert hashlib.sha256(archive.read_bytes()).hexdigest()=='f47be8d27d3230e823bf1eeede2fe0abaca55bb78d0b59974370e6689a92284a'
 with zipfile.ZipFile(archive) as z:z.extractall(root)
 solver=(root/'z3-5.1.0-x64-glibc-2.39/bin/z3').resolve();solver.chmod(0o755)
 import shutil
 target=Path('Binaries/z3/bin/z3-5.1.0');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(solver,target)
 env=dict(os.environ,Z3=str(solver),DAFNY_INTEGRATION_TESTS_ONLY_COMPILERS='cs')
 phase('inventory-gate',['dotnet','test','Source/DafnyCore.Test/DafnyCore.Test.csproj','-c','Release','--no-build','--filter','FullyQualifiedName~ObligationCoverageTests','--logger','trx;LogFileName=tests.trx','--results-directory',str((root/'inventory-gate').resolve())],env)
 phase('core-all',['dotnet','test','Source/DafnyCore.Test/DafnyCore.Test.csproj','-c','Release','--no-build','--logger','trx;LogFileName=tests.trx','--results-directory',str((root/'core-all').resolve())],env)
 phase('registered',['dotnet','test','Source/IntegrationTests','-c','Release','--filter','DisplayName~git-issue-100.dfy','--logger','trx;LogFileName=tests.trx','--results-directory',str((root/'registered').resolve())],env)
 phase('editor',['dotnet','test','Source/DafnyLanguageServer.Test','-c','Release','--filter','FullyQualifiedName~ConsistentObligationChecksTest','--logger','trx;LogFileName=tests.trx','--results-directory',str((root/'editor').resolve())],env)
 (root/'identities.json').write_text(json.dumps({'source':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'dafny':subprocess.check_output(['dotnet','out/dafny/Dafny.dll','--version'],text=True).strip(),'solver':subprocess.check_output([str(solver),'--version'],text=True).strip(),'core_sha256':hashlib.sha256(Path('out/dafny/DafnyCore.dll').read_bytes()).hexdigest(),'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest()},indent=2)+'\n')
(root/'results.json').write_text(json.dumps({'diagnostic':True,'phases':results},indent=2)+'\n')
summary='Diagnostic port probe; wrapper success does not mean acceptance.\n\n'+''.join(f"- {r['name']}: actual exit {r['exit']}\n" for r in results)
print(summary)
if os.environ.get('GITHUB_STEP_SUMMARY'):Path(os.environ['GITHUB_STEP_SUMMARY']).write_text(summary)
