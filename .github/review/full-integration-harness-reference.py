"""Attribute exact registered-harness language profiles and resource baselines."""
import hashlib,json,os,subprocess
from pathlib import Path
kind=os.environ['COMPILER_SOURCE'];out=Path('out/harness-reference');out.mkdir(parents=True,exist_ok=True)
assembly=Path('out/toolchain/dafny/Dafny.dll').resolve();root=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest').resolve();rows=[]
profiles=[]
for solver in ['5.1.0','4.12.1']:
 for refresh in [False,True]:
  for case,expected in [('original',0),('positive',0),('negative',4)]:
   profiles.append((f'33-{case}-{refresh}-{solver}',f'git-issues/Inputs/github-issue-33-{case}.dfy',solver,refresh,False,'legacy',['--resource-limit:200000','--verification-time-limit:0','--additional-axioms:true'],expected))
  profiles.append((f'33-newtypes-{solver}', 'git-issues/Inputs/github-issue-33-newtypes.dfy',solver,True,True,'datatype',['--resource-limit:200000','--verification-time-limit:0','--additional-axioms:true'],0))
for refresh in [False,True]:
 for case,expected in [('original',0),('positive',0),('negative',4)]:
  profiles.append((f'1462-{case}-{refresh}','git-issues/Inputs/git-issue-1462-'+case+'.dfy','5.1.0',refresh,refresh,'legacy',['--verification-time-limit:60'],expected))
for program,extra,expected in [('dafny0/CoinductiveProofs.dfy',['--allow-deprecation','--performance-stats=1'],4),('dafny0/SubsetTypes.dfy',['--performance-stats=100','--relax-definite-assignment'],4),('dafny1/SchorrWaite.dfy',['--performance-stats=1'],0),('dafny1/SchorrWaite-stages.dfy',['--allow-deprecation','--verification-time-limit:90','--cores:1'],0),('dafny1/ExtensibleArrayAuto.dfy',[],0)]:
 for refresh in ([False] if 'SubsetTypes' in program else [False,True]):
  profiles.append((program.replace('/','-')+'-'+str(refresh),program,'4.16.0',refresh,refresh,'datatype' if refresh else 'legacy',extra,expected))
for name,program,solverVersion,refresh,newtypes,traits,extra,expected in profiles:
 folder=out/name;folder.mkdir(exist_ok=True);solver=Path('out/solvers/z3-'+solverVersion).resolve()
 command=['dotnet',str(assembly),'verify',str(root/program),'--cores:2','--resource-limit:50e6','--verification-time-limit:300','--allow-axioms','--standard-libraries:false','--show-snippets:false','--use-basename-for-filename','--type-system-refresh:'+str(refresh).lower(),'--general-newtypes:'+str(newtypes).lower(),'--general-traits='+traits,'--solver-path',str(solver),'--log-format','json;LogFileName='+str((folder/'results.json').resolve())]+extra
 if kind=='dev-baseline':command=[c for c in command if not c.startswith('--additional-axioms:')]
 (folder/'command.json').write_text(json.dumps(command,indent=2))
 with (folder/'output.txt').open('w') as f:
  try:code=subprocess.run(command,stdout=f,stderr=subprocess.STDOUT,timeout=1200).returncode
  except subprocess.TimeoutExpired:code=124
 (folder/'exit.txt').write_text(str(code)+'\n');decls=json.loads((folder/'results.json').read_text()).get('verificationResults',[]) if (folder/'results.json').exists() else []
 rows.append({'name':name,'program':program,'source_sha256':hashlib.sha256((root/program).read_bytes()).hexdigest(),'exit':code,'intended_harness_exit':expected,'declarations':len(decls),'vc_batches':sum(len(d.get('vcResults',[])) for d in decls),'nonempty':bool(decls) and all(d.get('vcResults') for d in decls)});print(name,'actual exit',code,flush=True)
receipt={'source':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler_kind':kind,'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),'core_sha256':hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),'solvers':{p.name:{'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'version':subprocess.check_output([str(p.resolve()),'--version'],text=True).strip()} for p in Path('out/solvers').glob('z3-*')},'diagnostic':True,'note':'Same combined source, normal testing argument ceilings; reference outcomes never substitute for complete registered acceptance.','profiles':rows}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
