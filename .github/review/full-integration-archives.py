"""Generate archives only after complete source verification with the authorized solver."""
from pathlib import Path
import os, subprocess, json, hashlib, zipfile, tomllib
root=Path.cwd(); library=root/'Source/DafnyStandardLibraries';out=root/'out/archive-evidence';out.mkdir(parents=True,exist_ok=True)
assembly=(root/'out/toolchain/dafny/Dafny.dll').resolve();solver=Path(os.environ['Z3']).resolve()
source=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
compiler_source=(root/'out/toolchain/source.txt').read_text().strip()
version=subprocess.check_output(['dotnet',str(assembly),'--version'],text=True).strip()
solver_version=subprocess.check_output([str(solver),'--version'],text=True).strip()
assert os.environ['ARCHIVE_SOLVER_VERSION'] in solver_version
receipt={'source':source,'compiler_source':compiler_source,'compiler_version':version,'solver_version':solver_version,
 'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),
 'assemblies':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in assembly.parent.glob('*.dll') if p.name.startswith(('Dafny','Boogie'))},
 'library_inputs':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in library.joinpath('src').rglob('*') if p.is_file()},
 'phases':[],'archives':{}}
# The compiler's product source must match the checked-out implementation.
assert not subprocess.check_output(['git','diff',compiler_source,source,'--','Source/DafnyCore','Source/DafnyDriver','Source/Dafny','Source/DafnyLanguageServer'],text=True)
common=['--solver-path',str(solver),'--verification-time-limit','0','--cores','4',
 '--additional-axioms:false','--consistent-obligation-checks:false','--hidden-no-verify=false']
def build(label,cwd,inputs,target,extra=()):
 command=['dotnet',str(assembly),'build','-t:lib',*inputs,'--output:'+str(target),*common,*extra,
 '--log-format','json;LogFileName='+str(out/(label+'.json'))]
 with (out/(label+'.txt')).open('w') as output:result=subprocess.run(command,cwd=cwd,stdout=output,stderr=subprocess.STDOUT,timeout=10800)
 receipt['phases'].append({'name':label,'command':command,'exit':result.returncode})
 (out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
 assert result.returncode==0,(label,result.returncode)
 log=json.loads((out/(label+'.json')).read_text());decls=log['verificationResults'];assert decls
 assert all(d['outcome']=='Correct' and d['vcResults'] and all(v['outcome']=='Valid' for v in d['vcResults']) for d in decls)
 with zipfile.ZipFile(target) as z:
  manifest=z.read('manifest.toml').decode();config=tomllib.loads(manifest)
  assert config['options']['hidden-no-verify']==False and config['options']['no-verify']==False
  assert config['solver_version']==os.environ['ARCHIVE_SOLVER_VERSION']
  assert config['dafny_version']=='4.11.1.0'
  receipt['archives'][target.name]={'sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'program_sha256':hashlib.sha256(z.read('program')).hexdigest(),'manifest':config,'declarations':len(decls),'vc_batches':sum(len(d['vcResults']) for d in decls),'resource_units':sum(d['resourceCount'] for d in decls)}
 (out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
def generate():
 build('Std',library,['src/Std/dfyconfig.toml'],library/'binaries/DafnyStandardLibraries.doo')
 ts=library/'src/Std/TargetSpecific'
 for target in ['notarget','cs','java','js','go','py']:
  files=sorted('./'+str(p.relative_to(ts)) for p in ts.rglob('*-'+target+'*.dfy'))
  build('TargetSpecific-'+target,ts,['dfyconfig.toml',*files],library/('binaries/DafnyStandardLibraries-'+target+'.doo'))
 arithmetic=library/'src/Std/Arithmetic'
 files=sorted(str(p.relative_to(library)) for p in arithmetic.rglob('*.dfy'))
 build('Arithmetic',library,files,library/'binaries/DafnyStandardLibraries-arithmetic.doo',extra=['--resource-limit','5000000','--enforce-determinism','--reads-clauses-on-methods','--track-print-effects','--type-system-refresh','--general-newtypes','--general-traits:datatype'])
try:
 generate()
 assert len(receipt['archives'])==8
except Exception as error:
 receipt['complete']=False
 receipt['failure']=str(error)
 print('Verified archive generation incomplete:',error,flush=True)
else:
 receipt['complete']=True
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
