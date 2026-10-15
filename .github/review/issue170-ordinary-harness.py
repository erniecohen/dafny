"""Record the six affected ordinary harness tests and exact verifier outputs."""
from pathlib import Path
import argparse,hashlib,json,os,re,signal,subprocess,urllib.request,zipfile
p=argparse.ArgumentParser();p.add_argument('output',type=Path);a=p.parse_args();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
version=re.search(r'DefaultZ3Version = "([^"]+)"',Path('Source/DafnyCore/DafnyOptions.cs').read_text()).group(1)
digests={'4.12.1':'e07d4193d8e1851114fe4d507332a43576639f4aeaa33c7df15c36426d3cbf3b','4.16.0':'87de9c9e86c877abfccb00cf24b946c4a1bda7b9cba6f363ea58311d2573c320'}
archive=out/'reference.zip';urllib.request.urlretrieve('https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03/z3-'+version+'-x64-ubuntu-24.04-bin.zip',archive)
assert hashlib.sha256(archive.read_bytes()).hexdigest()==digests[version]
where=Path('Binaries/z3/bin');where.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(archive) as z:z.extractall(where)
solver=(where/('z3-'+version)).resolve();solver.chmod(0o755);alias=where/'z3';alias.unlink(missing_ok=True);alias.symlink_to(solver.name)
env=dict(os.environ,DAFNY_INTEGRATION_TESTS_ONLY_COMPILERS='cs',DOTNET_PROCESSOR_COUNT='1',PATH=str(where.resolve())+os.pathsep+os.environ['PATH'])
files=['dafny1/PriorityQueue.dfy','dafny4/Primes.dfy','dafny4/UnionFind.dfy','dafny1/ExtensibleArray.dfy','dafny2/SnapshotableTrees.dfy','dafny1/SchorrWaite-stages.dfy']
def run(cmd,folder,cap=720):
 folder.mkdir(parents=True,exist_ok=True);(folder/'command.json').write_text(json.dumps(cmd,indent=2)+'\n')
 with (folder/'stdout.txt').open('w') as stream:
  child=subprocess.Popen(cmd,stdout=stream,stderr=subprocess.STDOUT,env=env,start_new_session=True)
  try:code=child.wait(timeout=cap)
  except subprocess.TimeoutExpired:
   os.killpg(child.pid,signal.SIGTERM)
   try:child.wait(timeout=5)
   except subprocess.TimeoutExpired:os.killpg(child.pid,signal.SIGKILL);child.wait()
   code=124
 (folder/'exit.txt').write_text(str(code)+'\n');return code
code=run(['dotnet','test','Source/IntegrationTests','-c','Release','--no-build','--filter','|'.join('DisplayName~'+name for name in files),'--logger','console;verbosity=detailed','--logger','trx;LogFileName=ordinary.trx','--results-directory',str(out/'trx')],out/'ordinary-tests')
rows=[{'case':'ordinary-tests','exit':code}]
L=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest');assembly=Path('Binaries/net10.0/DafnyDriver.dll').resolve()
for name in files:
 if name=='dafny4/UnionFind.dfy':continue
 snapshot='SnapshotableTrees' in name
 for refresh in ([True] if snapshot else [False,True]):
  label=Path(name).stem+'-r'+str(int(refresh));folder=out/label;folder.mkdir()
  cmd=['dotnet',str(assembly),'run' if snapshot else 'verify',str(L/name),'--solver-path',str(solver),'--cores=2','--resource-limit:50e6','--verification-time-limit:300','--show-snippets:false','--use-basename-for-filename','--type-system-refresh:'+str(refresh).lower(),'--general-traits='+('datatype' if refresh else 'legacy'),'--general-newtypes:'+str(refresh).lower()]
  if snapshot:cmd+=['--standard-libraries:false','--solver-option','O:smt.qi.eager_threshold=80']
  else:cmd+=['--allow-axioms','--print:'+str(folder/'print.dfy'),'--rprint:'+str(folder/'rprint.dfy'),'--bprint:'+str(folder/'program.bpl')]
  if 'SchorrWaite' in name:cmd+=['--allow-deprecation']
  code=run(cmd,folder);rows.append({'case':label,'exit':code});print(label,'actual exit',code,flush=True)
(out/'receipt.json').write_text(json.dumps({'diagnostic':True,'source':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'solver_version':version,'cases':rows},indent=2)+'\n')
print('Ordinary-harness diagnostic complete; actual exits and outputs retained.',flush=True)
