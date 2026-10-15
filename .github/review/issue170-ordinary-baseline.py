"""Compare unchanged ordinary controls against the pre-repair source."""
from pathlib import Path
import argparse,hashlib,json,os,re,signal,subprocess,urllib.request,zipfile
p=argparse.ArgumentParser();p.add_argument('assembly',type=Path);p.add_argument('output',type=Path);a=p.parse_args();assembly=a.assembly.resolve();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
version=re.search(r'DefaultZ3Version = "([^"]+)"',Path('Source/DafnyCore/DafnyOptions.cs').read_text()).group(1)
digests={'4.12.1':'e07d4193d8e1851114fe4d507332a43576639f4aeaa33c7df15c36426d3cbf3b','4.16.0':'87de9c9e86c877abfccb00cf24b946c4a1bda7b9cba6f363ea58311d2573c320'}
archive=out/'reference.zip';urllib.request.urlretrieve('https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03/z3-'+version+'-x64-ubuntu-24.04-bin.zip',archive);assert hashlib.sha256(archive.read_bytes()).hexdigest()==digests[version]
with zipfile.ZipFile(archive) as z:z.extractall(out/'reference')
solver=out/'reference'/('z3-'+version);solver.chmod(0o755)
baseline='1256f92b0b494e8f1bf52aaec145d36a8a862f33' if version=='4.12.1' else '4e98f3f592310854ca1448d517b82dcbcc0f51f4'
subprocess.run(['git','fetch','--quiet','--no-tags','--depth=1','origin',baseline],check=True)
subprocess.run(['git','diff','--quiet',baseline,'HEAD','--','Source/DafnyCore','Source/DafnyDriver'],check=True)
env=dict(os.environ,DOTNET_PROCESSOR_COUNT='1');rows=[]
L=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest')
for name in ['dafny4/UnionFind.dfy','dafny2/SnapshotableTrees.dfy']:
 source=L/name;current=source.read_bytes();original=subprocess.check_output(['git','show',baseline+':'+str(source)]);source.write_bytes(original)
 try:
  for refresh in ([True] if 'Snapshotable' in name else [False,True]):
   label=source.stem+'-r'+str(int(refresh));folder=out/label;folder.mkdir()
   cmd=['dotnet',str(assembly),'run' if 'Snapshotable' in name else 'verify',str(source),'--solver-path',str(solver),'--cores=2','--resource-limit:50e6','--verification-time-limit:300','--show-snippets:false','--use-basename-for-filename','--type-system-refresh:'+str(refresh).lower(),'--general-traits='+('datatype' if refresh else 'legacy'),'--general-newtypes:'+str(refresh).lower(),'--log-format','json;LogFileName='+str(folder/'results.json')]
   if 'Snapshotable' in name:cmd+=['--standard-libraries:false','--solver-option','O:smt.qi.eager_threshold=80']
   else:cmd+=['--allow-axioms','--relax-definite-assignment','--print:'+str(folder/'print.dfy'),'--rprint:'+str(folder/'rprint.dfy'),'--bprint:'+str(folder/'program.bpl')]
   (folder/'command.json').write_text(json.dumps(cmd,indent=2)+'\n')
   with (folder/'stdout.txt').open('w') as stream:
    child=subprocess.Popen(cmd,stdout=stream,stderr=subprocess.STDOUT,env=env,start_new_session=True)
    try:code=child.wait(timeout=720)
    except subprocess.TimeoutExpired:
     os.killpg(child.pid,signal.SIGTERM)
     try:child.wait(timeout=5)
     except subprocess.TimeoutExpired:os.killpg(child.pid,signal.SIGKILL);child.wait()
     code=124
   (folder/'exit.txt').write_text(str(code)+'\n');rows.append({'case':label,'source_sha256':hashlib.sha256(original).hexdigest(),'exit':code});print(label,'actual baseline exit',code,flush=True)
 finally:source.write_bytes(current)
(out/'receipt.json').write_text(json.dumps({'diagnostic':True,'compiler_source':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'proof_source':baseline,'cases':rows},indent=2)+'\n')
print('Original-source diagnostic complete; actual failures retained.',flush=True)
