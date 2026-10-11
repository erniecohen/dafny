"""Capture library proof failures and repairs without treating diagnostics as a gate."""
import hashlib,json,os,subprocess,tarfile
from pathlib import Path
out=Path('out/library-evidence');out.mkdir(parents=True,exist_ok=True)
assembly=Path('out/toolchain/dafny/Dafny.dll').resolve();solver=Path(os.environ['Z3']).resolve()
kind=os.environ['COMPILER_SOURCE'];rows=[]
archive=out/'before.tar'
with archive.open('wb') as f: subprocess.run(['git','archive','eac45d90832014b377a68e49444057d230123e58','Source/DafnyStandardLibraries/src/Std'],stdout=f,check=True)
with tarfile.open(archive) as f:f.extractall(out/'before',filter='data')
before=(out/'before/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml').resolve()
current=Path('Source/DafnyStandardLibraries/src/Std/dfyconfig.toml').resolve()
symbols=['Std.Arithmetic.DivMod.LemmaMultiplyDivideLe','Std.Arithmetic.DivMod.LemmaMultiplyDivideLt','Std.Arithmetic.Power.LemmaPowIncreases','Std.Base64.EncodeBVLengthCongruentToZeroMod4','Std.Base64.EncodeBVIsBase64','Std.Producers.ConcatenatedProducer.Invoke']
for profile,project in [('before',before)]+([('repair',current)] if kind=='integrated' else []):
 for symbol in symbols if profile=='before' else symbols[:2]+['Std.Arithmetic.Mul.LemmaMulIsCommutative','Std.Arithmetic.DivMod.LemmaModMultiplesBasic']:
  name=profile+'-'+symbol;folder=out/name;folder.mkdir(exist_ok=True)
  command=['dotnet',str(assembly),'verify',str(project),'--filter-symbol:'+symbol,'--solver-path',str(solver),'--cores','1','--verification-time-limit','300','--log-format','json;LogFileName='+str((folder/'results.json').resolve())]
  if kind!='dev-baseline':command+=['--additional-axioms:false','--consistent-obligation-checks:false']
  (folder/'command.json').write_text(json.dumps(command,indent=2))
  with (folder/'output.txt').open('w') as f:
   try:code=subprocess.run(command,stdout=f,stderr=subprocess.STDOUT,timeout=900).returncode
   except subprocess.TimeoutExpired:code=124
  (folder/'exit.txt').write_text(str(code)+'\n');rows.append({'name':name,'exit':code});print(name,'actual exit',code,flush=True)
receipt={'compiler_kind':kind,'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),'verification_sources':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'before_source':'eac45d90832014b377a68e49444057d230123e58','version':subprocess.check_output(['dotnet',str(assembly),'--version'],text=True).strip(),'solver_version':subprocess.check_output([str(solver),'--version'],text=True).strip(),'core_sha256':hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'diagnostic':True,'phases':rows}
receipt['source_hashes']={str(p.relative_to(current.parent)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(current.parent.rglob('*.dfy'))}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
