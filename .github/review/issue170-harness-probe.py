"""Bounded diagnostic: retain actual reference-harness and standing-suite results."""
from pathlib import Path
import argparse,hashlib,importlib.util,json,os,signal,subprocess,sys,urllib.request,zipfile
p=argparse.ArgumentParser();p.add_argument('assembly',type=Path);p.add_argument('solver',type=Path);p.add_argument('output',type=Path);a=p.parse_args()
assembly=a.assembly.resolve();solver=a.solver.resolve();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
archive=out/'reference.zip';urllib.request.urlretrieve('https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03/z3-4.16.0-x64-ubuntu-24.04-bin.zip',archive)
assert hashlib.sha256(archive.read_bytes()).hexdigest()=='87de9c9e86c877abfccb00cf24b946c4a1bda7b9cba6f363ea58311d2573c320'
with zipfile.ZipFile(archive) as z:z.extractall(out/'reference')
reference=out/'reference/z3-4.16.0';reference.chmod(0o755)
assert '4.16.0' in subprocess.check_output([str(reference),'--version'],text=True)
os.environ['DOTNET_PROCESSOR_COUNT']='1'
source='Source/IntegrationTests/TestFiles/LitTests/LitTest/dafny1/SchorrWaite-stages.dfy'
rows=[]
for refresh in [False,True]:
 for axioms in [False,True]:
  for mode in ['off','on']:
   label=f'reference-r{int(refresh)}-a{int(axioms)}-{mode}';folder=out/label;folder.mkdir()
   cmd=['dotnet',str(assembly),'verify',source,'--solver-path',str(reference),'--cores=1','--resource-limit:50e6','--verification-time-limit:90','--allow-axioms','--show-snippets:false','--use-basename-for-filename','--type-system-refresh:'+str(refresh).lower(),'--general-traits='+('datatype' if refresh else 'legacy'),'--general-newtypes:'+str(refresh).lower(),'--allow-deprecation','--additional-axioms:'+str(axioms).lower(),'--consistent-obligation-checks:'+str(mode=='on').lower(),'--print:'+str(folder/'print.dfy'),'--rprint:'+str(folder/'rprint.dfy'),'--bprint:'+str(folder/'program.bpl'),'--log-format','json;LogFileName='+str(folder/'results.json'),'--log-format','csv;LogFileName='+str(folder/'resources.csv')]
   (folder/'command.json').write_text(json.dumps(cmd,indent=2)+'\n')
   with (folder/'stdout.txt').open('w') as stream:
    child=subprocess.Popen(cmd,stdout=stream,stderr=subprocess.STDOUT,start_new_session=True)
    try:code=child.wait(timeout=900)
    except subprocess.TimeoutExpired:
     os.killpg(child.pid,signal.SIGTERM)
     try:child.wait(timeout=5)
     except subprocess.TimeoutExpired:os.killpg(child.pid,signal.SIGKILL);child.wait()
     code=124
   (folder/'exit.txt').write_text(str(code)+'\n');rows.append({'case':label,'exit':code});print(label,'actual exit',code,flush=True)
(out/'results.json').write_text(json.dumps(rows,indent=2)+'\n')
spec=importlib.util.spec_from_file_location('standing_lit','.github/review/lit-verdicts.py');lit=importlib.util.module_from_spec(spec);spec.loader.exec_module(lit)
L=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest');plan=out/'complete-plan.tsv';lit.plan(str(L),str(plan))
selected=out/'plan.tsv';selected.write_text(''.join(line+'\n' for line in plan.read_text().splitlines() if line.split('\t')[0]=='dafny1/SchorrWaite-stages.dfy'))
assert len(selected.read_text().splitlines())==1
launcher=assembly.parent/'dafny-for-reference-probe';launcher.write_text('#!/bin/sh\nexec dotnet "'+str(assembly)+'" "$@"\n');launcher.chmod(0o755)
for axioms in [False,True]:
 folder=out/('standing-a'+str(int(axioms)));folder.mkdir()
 lit.run(str(selected),str(L),str(launcher),str(solver),str(folder/'verdicts.tsv'),jobs=1,additional_axioms=axioms,measurements=str(folder/'measurements'))
print('Diagnostic complete. Actual proof failures are retained; wrapper exits zero.',flush=True)
