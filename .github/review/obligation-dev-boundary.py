"""Explain original reproducer behavior without altering product or acceptance inputs."""
import hashlib,json,os,re,subprocess
from pathlib import Path
root=Path('out/obligation-dev-probe/boundary');root.mkdir(parents=True,exist_ok=True)
fixtures=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/assertion-invariance')
solver=Path(os.environ['Z3']).resolve();observations=[]
def run(name,command):
 folder=root/name;folder.mkdir(exist_ok=True)
 (folder/'command.json').write_text(json.dumps(command))
 r=subprocess.run(command,text=True,capture_output=True,timeout=180)
 (folder/'output.txt').write_text(r.stdout+r.stderr)
 row={'name':name,'exit':r.returncode,'summaries':[x for x in (r.stdout+r.stderr).splitlines() if 'verifier finished' in x]}
 observations.append(row);return folder,row
for mode in ['baseline','candidate']:
 native=Path('out/'+mode+'/dafny/Dafny').resolve()
 identity={'source':Path('out/'+mode+'/source.txt').read_text().strip(),'version':subprocess.check_output([str(native),'--version'],text=True).strip(),'core_sha256':hashlib.sha256((native.parent/'DafnyCore.dll').read_bytes()).hexdigest()}
 (root/(mode+'-identity.json')).write_text(json.dumps(identity,indent=2)+'\n')
 for refresh in [False,True]:
  for case in ['issue100-original','issue100-explicit','issue100-final-true','twostate-labeled-recursive','twostate-nested-replay','negative-twostate-labeled-replay','negative-twostate-nested-replay']:
   for enabled in ([False,True] if mode=='candidate' else [False]):
    name=f'{mode}-{case}-refresh-{refresh}-enabled-{enabled}'
    folder=root/name;folder.mkdir(exist_ok=True)
    command=[str(native),'verify',str(fixtures/(case+'.dfy')),'--solver-path',str(solver),'--cores','1','--resource-limit','16000000','--verification-time-limit','30',f'--type-system-refresh:{str(refresh).lower()}',f'--general-newtypes:{str(refresh).lower()}','--boogie','/normalizeDeclarationOrder:0','--bprint',str(folder/'program.bpl'),'--log-format','json;LogFileName='+str((folder/'results.json').resolve())]
    if mode=='candidate':command.append('--consistent-obligation-checks:'+str(enabled).lower())
    run(name,command)
original=(root/'candidate-issue100-original-refresh-False-enabled-True/program.bpl').read_text()
final=(root/'candidate-issue100-final-true-refresh-False-enabled-True/program.bpl').read_text()
marker='    // Begin Comprehension WF check'
# Only the final wfi exit preparation, after the exported forall proof.
position=original.rfind(marker,0,original.index('procedure {:verboseName "WfO (well-formedness)"}'))
assert position>=0
variants={'original':original,'original-plus-tautology':original[:position]+'    assert Lit(true);\n'+original[position:],'final-true':final}
removed,count=re.subn(r'    assert \{:id "id63"\} Lit\(true\);\n','',final);assert count==1
variants['final-minus-tautology']=removed
boogie=str(Path('out/native-boogie/boogie').resolve())
(root/'boogie-identity.json').write_text(json.dumps({'pinned_package':'Boogie 3.5.5','execution_engine_sha256':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in Path('out/native-boogie').rglob('Boogie.ExecutionEngine.dll')}},indent=2)+'\n')
for name,content in variants.items():
 path=root/(name+'.bpl');path.write_text(content)
 for seed in [0,1,7]:
  command=[boogie,str(path),'/proc:Impl$$_module.__default.wfi','/vcsCores:1','/typeEncoding:m','/normalizeDeclarationOrder:0','/rlimit:16000000','/timeLimit:60',f'/randomSeed:{seed}','/proverOpt:PROVER_PATH='+str(solver),'/trace']
  run(f'boogie-{name}-seed-{seed}',command)
(root/'summary.json').write_text(json.dumps({'diagnostic':True,'solver':subprocess.check_output([str(solver),'--version'],text=True).strip(),'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'observations':observations},indent=2)+'\n')
print('Recorded native baseline and controlled Boogie observations; strict original acceptance is unchanged.')
