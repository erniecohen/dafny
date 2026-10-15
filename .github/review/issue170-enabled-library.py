"""Record the complete ordinary library with obligation checking enabled."""
from pathlib import Path
import argparse,ast,concurrent.futures,hashlib,json,os,re,shlex,signal,subprocess,types,urllib.request,zipfile
p=argparse.ArgumentParser();p.add_argument('assembly',type=Path);p.add_argument('solver',type=Path);p.add_argument('output',type=Path);a=p.parse_args()
assembly=a.assembly.resolve();solver=a.solver.resolve();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
version=re.search(r'DefaultZ3Version = "([^"]+)"',Path('Source/DafnyCore/DafnyOptions.cs').read_text()).group(1)
digests={'4.12.1':'e07d4193d8e1851114fe4d507332a43576639f4aeaa33c7df15c36426d3cbf3b','4.16.0':'87de9c9e86c877abfccb00cf24b946c4a1bda7b9cba6f363ea58311d2573c320'}
archive=out/'reference.zip';urllib.request.urlretrieve('https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03/z3-'+version+'-x64-ubuntu-24.04-bin.zip',archive)
assert hashlib.sha256(archive.read_bytes()).hexdigest()==digests[version]
with zipfile.ZipFile(archive) as z:z.extractall(out/'reference')
reference=out/'reference'/('z3-'+version);reference.chmod(0o755)
os.environ['DOTNET_PROCESSOR_COUNT']='1'
launcher=out/'dafny-enabled';launcher.write_text('#!/bin/sh\nexec dotnet '+shlex.quote(str(assembly))+' "$@" --consistent-obligation-checks:true\n');launcher.chmod(0o755)
P=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/obligation-proof-repairs/registered.py');tree=ast.parse(P.read_text());catalog={'L':'Source/IntegrationTests/TestFiles/LitTests/LitTest/','S':'Source/DafnyStandardLibraries/src/Std/'}
for node in tree.body:
 if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id in ['targets','helpers'] for t in node.targets):exec(compile(ast.Module(body=[node],type_ignores=[]),str(P),'exec'),catalog)
base64=Path(catalog['S']+'Base64.dfy').read_text()
for h in ['UnpaddedPrefixWithPaddingIsBase64','UnpaddedStringIsBase64','DecodeValid1PaddingLengthMod3','DecodeValid2PaddingLengthMod3']:
 if 'lemma '+h+'(' in base64:catalog['helpers'].append((h,catalog['S']+'Base64.dfy','Std.Base64.'+h,[]))
wanted=[item[2] for item in catalog['targets']+catalog['helpers'] if item[1].startswith(catalog['S'])]
def capture(pair):
 ver,chosen,axioms=pair;label='z'+ver+'-a'+str(int(axioms));folder=out/label;folder.mkdir()
 cmd=['python3','.github/review/std-verdicts.py','run',str(Path('Source/DafnyStandardLibraries').resolve()),str(launcher),str(chosen),str(folder/'verdicts.tsv'),'--cores','2','--additional-axioms',str(axioms).lower()]
 (folder/'command.json').write_text(json.dumps(cmd,indent=2)+'\n')
 with (folder/'stdout.txt').open('w') as stream:
  child=subprocess.Popen(cmd,stdout=stream,stderr=subprocess.STDOUT,start_new_session=True)
  try:code=child.wait(timeout=1200)
  except subprocess.TimeoutExpired:
   os.killpg(child.pid,signal.SIGTERM)
   try:child.wait(timeout=5)
   except subprocess.TimeoutExpired:os.killpg(child.pid,signal.SIGKILL);child.wait()
   code=124
 (folder/'exit.txt').write_text(str(code)+'\n')
 table=[line.split('\t') for line in (folder/'verdicts.tsv').read_text().splitlines()] if (folder/'verdicts.tsv').exists() else []
 runs=[row[:4] for row in table if row[0].startswith('run ')];found={symbol:[row for row in table if row[0].startswith('Std '+symbol+' (')] for symbol in wanted}
 selected=all(found[symbol] and all(row[1]=='Correct' for row in found[symbol]) for symbol in wanted)
 complete=code==0 and len(runs)==7 and all(row[1] in ['0','4'] and 'verified' in row[2] for row in runs)
 result={'profile':label,'capture_exit':code,'complete':complete,'selected_correct':bool(selected),'selected_symbols':len(wanted),'run_outcomes':runs,'other_noncorrect':{row[0]:row[1] for row in table if row[0].startswith('Std ') and row[1]!='Correct'}}
 (folder/'receipt.json').write_text(json.dumps(result,indent=2)+'\n');print(label,'complete',complete,'selected proofs correct',bool(selected),flush=True);return result
profiles=[(ver,chosen,axioms) for ver,chosen in [('5.1.0',solver),(version,reference)] for axioms in [False,True]]
with concurrent.futures.ThreadPoolExecutor(2) as pool:results=list(pool.map(capture,profiles))
receipt={'source':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler_core_sha256':hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),'all_complete':all(row['complete'] for row in results),'all_selected_correct':all(row['selected_correct'] for row in results),'profiles':results}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Enabled-library diagnostic complete; actual outcomes retained, wrapper exits zero.',flush=True)
