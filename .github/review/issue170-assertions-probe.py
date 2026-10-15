"""Bounded diagnostic of retained arithmetic assertions and ordinary library verdicts."""
from pathlib import Path
import argparse,concurrent.futures,hashlib,json,os,re,signal,subprocess,urllib.request,zipfile
p=argparse.ArgumentParser();p.add_argument('assembly',type=Path);p.add_argument('solver',type=Path);p.add_argument('output',type=Path);a=p.parse_args()
assembly=a.assembly.resolve();solver=a.solver.resolve();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
version=re.search(r'DefaultZ3Version = "([^"]+)"',Path('Source/DafnyCore/DafnyOptions.cs').read_text()).group(1)
digests={'4.12.1':'e07d4193d8e1851114fe4d507332a43576639f4aeaa33c7df15c36426d3cbf3b','4.16.0':'87de9c9e86c877abfccb00cf24b946c4a1bda7b9cba6f363ea58311d2573c320'}
archive=out/'reference.zip';urllib.request.urlretrieve('https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03/z3-'+version+'-x64-ubuntu-24.04-bin.zip',archive)
assert hashlib.sha256(archive.read_bytes()).hexdigest()==digests[version]
with zipfile.ZipFile(archive) as z:z.extractall(out/'reference')
reference=out/'reference'/('z3-'+version);reference.chmod(0o755)
os.environ['DOTNET_PROCESSOR_COUNT']='1'
source='Source/DafnyStandardLibraries/src/Std/Arithmetic/DivMod.dfy';project='Source/DafnyStandardLibraries/src/Std/dfyconfig.toml';symbol='Std.Arithmetic.DivMod.LemmaModNegNeg'
def run(cmd,folder,cap=900):
 folder.mkdir(parents=True,exist_ok=True);(folder/'command.json').write_text(json.dumps(cmd,indent=2)+'\n')
 with (folder/'stdout.txt').open('w') as stream:
  child=subprocess.Popen(cmd,stdout=stream,stderr=subprocess.STDOUT,start_new_session=True)
  try:code=child.wait(timeout=cap)
  except subprocess.TimeoutExpired:
   os.killpg(child.pid,signal.SIGTERM)
   try:child.wait(timeout=5)
   except subprocess.TimeoutExpired:os.killpg(child.pid,signal.SIGKILL);child.wait()
   code=124
 (folder/'exit.txt').write_text(str(code)+'\n');return code
rows=[]
for solver_version,chosen in [('5.1.0',solver),(version,reference)]:
 for axioms in [False,True]:
  for full in [False,True]:
   for mode in (['off','on'] if full else ['off','on','default']):
    label=('complete' if full else 'mod')+'-z'+solver_version.replace('.','')+f'-a{int(axioms)}-'+mode;folder=out/label;folder.mkdir()
    cmd=['dotnet',str(assembly),'verify',project,'--solver-path',str(chosen),'--cores=1','--verification-time-limit=0','--additional-axioms:'+str(axioms).lower(),'--log-format','json;LogFileName='+str(folder/'results.json'),'--log-format','csv;LogFileName='+str(folder/'resources.csv')]
    if mode!='default':cmd+=['--consistent-obligation-checks:'+str(mode=='on').lower()]
    cmd+=['--filter-position',source] if full else ['--filter-symbol',symbol+'.']
    code=run(cmd,folder)
    try:data=json.loads((folder/'results.json').read_text())['verificationResults']
    except (OSError,ValueError,KeyError):data=[]
    selected=[r for r in data if r['name'].startswith(symbol+' (')];wf=[r for r in data if '(well-formedness)' in r['name']]
    correct=code in (0,4) and bool(selected) and all(r['outcome']=='Correct' for r in selected+wf)
    batches=[(r['name'],v['vcNum'],v['outcome'],v['resourceCount']) for r in data for v in r['vcResults']]
    row={'case':label,'exit':code,'selected_correct':correct,'batches':len(batches),'total_ru':sum(v[3] for v in batches),'max_ru':max((v[3] for v in batches),default=0),'batch_results':batches};rows.append(row)
    print(label,'actual exit',code,'selected correct',correct,'batches',len(batches),'totalRU',row['total_ru'],'maximumRU',row['max_ru'],flush=True)
by_name={r['case']:r for r in rows};equal=all(r['batch_results']==by_name[r['case'].removesuffix('default')+'off']['batch_results'] for r in rows if r['case'].endswith('default'))
(out/'results.json').write_text(json.dumps(rows,indent=2)+'\n');(out/'receipt.json').write_text(json.dumps({'all_selected_correct':all(r['selected_correct'] for r in rows),'default_off_equal':equal,'invocations':len(rows)},indent=2)+'\n')
if not all(r['selected_correct'] for r in rows) or not equal:raise SystemExit(0)
launcher=assembly.parent/'dafny-for-assertions-probe';launcher.write_text('#!/bin/sh\nexec dotnet "'+str(assembly)+'" "$@"\n');launcher.chmod(0o755)
def capture(pair):
 ver,chosen,axioms=pair;folder=out/('library-z'+ver+'-a'+str(int(axioms)))
 cmd=['python3','.github/review/std-verdicts.py','run',str(Path('Source/DafnyStandardLibraries').resolve()),str(launcher),str(chosen),str(folder/'verdicts.tsv'),'--cores','2','--additional-axioms',str(axioms).lower()]
 code=run(cmd,folder,cap=2400);print('ordinary library',ver,axioms,'capture exit',code,flush=True)
profiles=[(ver,chosen,axioms) for ver,chosen in [('5.1.0',solver),(version,reference)] for axioms in [False,True]]
with concurrent.futures.ThreadPoolExecutor(2) as pool:list(pool.map(capture,profiles))
print('Diagnostic complete; actual proof exits are retained, wrapper exits zero.',flush=True)
