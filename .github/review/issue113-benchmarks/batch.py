from pathlib import Path
import os,subprocess,json
root=Path.cwd(); tools=Path(__file__).resolve().parent
manifest=tools/os.environ['CORPUS']/'manifest.json'
family=os.environ['FAMILY']; dest=root/'measurements'
common=['python3',str(tools/'run.py'),str(manifest), '--dafny',str(root/'out/dafny/Dafny'),
 '--expected-dafny',(root/'compiler/compiler-version.txt').read_text().strip(),
 '--solver',os.environ['SOLVER'],'--solver-version',os.environ['SOLVER_VERSION'],
 '--solver-seed',os.environ['SOLVER_SEED'],'--source-sha',os.environ['GITHUB_SHA'],
 '--executor','github-ubuntu-24.04','--execution-environment','public-ci','--select',(family+'*/*' if family in ['depth','generic-nesting'] else family+'/*')]
steps=[]
def run(stage,args=[]):
 out=dest/stage; out.mkdir(exist_ok=True)
 cmd=common+[str(out),'--stage',stage.split('-')[0]]+args
 p=subprocess.run(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
 (dest/(stage+'-runner.txt')).write_text(p.stdout)
 steps.append({'stage':stage,'command':cmd,'exit':p.returncode})
 print(stage,p.returncode,p.stdout[-150:])
 (dest/'stages.json').write_text(json.dumps(steps,indent=2))
for stage in ['resolve','translate','verify']:run(stage)
selected=[c for c in json.loads(manifest.read_text())['cases'] if c['family']==family]
if any(c['runtime'] for c in selected) and os.environ['SOLVER_VERSION']=='5.1.0' and os.environ['SOLVER_SEED']=='0':
 run('build',['--proof-report',str(dest/'verify/results.json')])
 run('run',['--build-report',str(dest/'build/results.json'),'--samples','3'])
 run('build-profile',['--proof-report',str(dest/'verify/results.json'),'--targets','cs','--profile','cs-allocated'])
 run('run-profile',['--build-report',str(dest/'build-profile/results.json'),'--targets','cs','--profile','cs-allocated','--samples','5'])
