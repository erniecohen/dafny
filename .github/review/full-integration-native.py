"""Record selected native outcomes for three immutable compiler receipts."""
import hashlib, json, os, subprocess
from pathlib import Path
root=Path('out/native-evidence');root.mkdir(parents=True,exist_ok=True)
solver=Path(os.environ['Z3']).resolve();assembly=Path('out/toolchain/dafny/Dafny.dll').resolve()
kind=os.environ['COMPILER_SOURCE'];rows=[]
def phase(name,command):
 folder=root/name;folder.mkdir(exist_ok=True)
 (folder/'command.json').write_text(json.dumps(command,indent=2))
 with (folder/'output.txt').open('w') as output:
  result=subprocess.run(command,stdout=output,stderr=subprocess.STDOUT,timeout=900)
 (folder/'exit.txt').write_text(str(result.returncode)+'\n')
 rows.append({'name':name,'exit':result.returncode});print(name,'actual exit',result.returncode,flush=True)
for refresh in [False,True]:
 for name in (['issue100-original','issue100-explicit','issue100-final-true'] if kind!='dev-baseline' else []):
  folder=root/f'{name}-{refresh}';folder.mkdir(exist_ok=True)
  source=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/assertion-invariance')/(name+'.dfy')
  command=['dotnet',str(assembly),'verify',str(source),'--solver-path',str(solver),'--cores','1',
   '--resource-limit','16000000','--verification-time-limit','30',f'--type-system-refresh:{str(refresh).lower()}',
   f'--general-newtypes:{str(refresh).lower()}','--additional-axioms:false','--consistent-obligation-checks:true',
   '--boogie','/normalizeDeclarationOrder:0','--log-format','json;LogFileName='+str((folder/'results.json').resolve())]
  phase(folder.name,command)
 for enabled in ([False,True] if kind!='dev-baseline' else [False]):
  folder=root/f'good-post-{refresh}-{enabled}';folder.mkdir(exist_ok=True)
  command=['dotnet',str(assembly),'verify','Source/IntegrationTests/TestFiles/LitTests/LitTest/dafny0/FunctionSpecifications.dfy',
   '--filter-symbol:GoodPost','--solver-path',str(solver),'--cores','1','--resource-limit','16000000',
   '--verification-time-limit','30',f'--type-system-refresh:{str(refresh).lower()}',
   f'--general-newtypes:{str(refresh).lower()}','--additional-axioms:false','--boogie','/normalizeDeclarationOrder:0',
   '--log-format','json;LogFileName='+str((folder/'results.json').resolve())]
  if kind!='dev-baseline':command.append(f'--consistent-obligation-checks:{str(enabled).lower()}')
  else:command.remove('--additional-axioms:false')
  phase(folder.name,command)
for axioms in ([False,True] if kind!='dev-baseline' else [False]):
 for enabled in ([False,True] if kind!='dev-baseline' else [False]):
  folder=root/f'divmod-{axioms}-{enabled}';folder.mkdir(exist_ok=True)
  command=['dotnet',str(assembly),'verify','Source/DafnyStandardLibraries/src/Std/dfyconfig.toml',
   '--filter-symbol:Std.Arithmetic.DivMod.LemmaMultiplyDivideLe','--solver-path',str(solver),'--cores','1',
   '--verification-time-limit','0','--log-format','json;LogFileName='+str((folder/'results.json').resolve())]
  if kind!='dev-baseline':command += [f'--additional-axioms:{str(axioms).lower()}',f'--consistent-obligation-checks:{str(enabled).lower()}']
  phase(folder.name,command)
if kind=='integrated':
 phase('floating-controls',['python3','Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/floating-obligations/registered.py',str(assembly),str(solver),str((root/'floating-controls').resolve())])
receipt={'compiler_kind':kind,'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),
 'verification_sources':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),
 'version':subprocess.check_output(['dotnet',str(assembly),'--version'],text=True).strip(),
 'solver_version':subprocess.check_output([str(solver),'--version'],text=True).strip(),
 'core_sha256':hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),
 'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'diagnostic':True,'phases':rows}
(root/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
