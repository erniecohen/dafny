"""Compare final 5.1 library proof bridges against their complete failing capture."""
import hashlib,json,os,subprocess,tarfile
from pathlib import Path
out=Path('out/library-tail');out.mkdir(parents=True,exist_ok=True)
assembly=Path('out/toolchain/dafny/Dafny.dll').resolve();solver=Path(os.environ['Z3']).resolve()
mode=os.environ['AXIOMS']=='on';rows=[];beforeSource='468c8dfd23bb843698288edbac4b6407d31703fb'
with (out/'before.tar').open('wb') as f:subprocess.run(['git','archive',beforeSource,'Source/DafnyStandardLibraries/src/Std'],stdout=f,check=True)
with tarfile.open(out/'before.tar') as f:f.extractall(out/'before',filter='data')
projects={'before':(out/'before/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml').resolve(),'repair':Path('Source/DafnyStandardLibraries/src/Std/dfyconfig.toml').resolve()}
symbols=['Std.Arithmetic.Power2.Lemma2To64','Std.Base64.DecodeValidUnpaddedPartialFrom1PaddedSeq','Std.Base64.DecodeValidEncode1Padding']
helpers=['Std.Arithmetic.Power2.LemmaPow2','Std.Arithmetic.Power.LemmaPowAuto','Std.Base64.UnpaddedBase64Prefix','Std.Base64.AboutDecodeValid','Std.Base64.DecodeValidPartialsFrom1PaddedSeq','Std.Base64.DecodeValid1PaddingLengthMod3','Std.Base64.DecodeEncodeUnpadded','Std.Base64.DecodeEncode1Padding']
for profile,project in projects.items():
 for symbol in symbols+(helpers if profile=='repair' else []):
  folder=out/(profile+'-'+symbol);folder.mkdir(exist_ok=True)
  command=['dotnet',str(assembly),'verify',str(project),'--filter-symbol:'+symbol,'--solver-path',str(solver),'--cores','1','--verification-time-limit','300','--additional-axioms:'+str(mode).lower(),'--consistent-obligation-checks:false','--log-format','json;LogFileName='+str((folder/'results.json').resolve())]
  (folder/'command.json').write_text(json.dumps(command,indent=2))
  with (folder/'output.txt').open('w') as f:
   try:code=subprocess.run(command,stdout=f,stderr=subprocess.STDOUT,timeout=900).returncode
   except subprocess.TimeoutExpired:code=124
  (folder/'exit.txt').write_text(str(code)+'\n');decls=json.loads((folder/'results.json').read_text()).get('verificationResults',[]) if (folder/'results.json').exists() else []
  rows.append({'profile':profile,'symbol':symbol,'actual_exit':code,'declarations':len(decls),'vc_batches':sum(len(d.get('vcResults',[])) for d in decls),'nonempty':bool(decls) and all(d.get('vcResults') for d in decls),'outcomes':[d['outcome'] for d in decls]});print(profile,symbol,'actual exit',code,flush=True)
receipt={'source':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),'before_source':beforeSource,'version':subprocess.check_output(['dotnet',str(assembly),'--version'],text=True).strip(),'solver_version':subprocess.check_output([str(solver),'--version'],text=True).strip(),'core_sha256':hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'additional_axioms':mode,'profiles':rows,'diagnostic':True,'source_hashes':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in Path('Source/DafnyStandardLibraries/src').rglob('*.dfy')}}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
