"""Preserve all actual outcomes from the complete registered normal harness."""
import hashlib,json,subprocess
from pathlib import Path
out=Path('out/registered-evidence');out.mkdir(parents=True,exist_ok=True)
rows=[]
def phase(name,command):
 folder=out/name;folder.mkdir(exist_ok=True)
 (folder/'command.json').write_text(json.dumps(command,indent=2))
 with (folder/'output.txt').open('w') as f:
  try:code=subprocess.run(command,stdout=f,stderr=subprocess.STDOUT,timeout=4800).returncode
  except subprocess.TimeoutExpired:code=124
 (folder/'exit.txt').write_text(str(code)+'\n');rows.append({'name':name,'exit':code});print(name,'actual exit',code,flush=True)
phase('packages',['sh','Scripts/fetch-boogie-packages.sh'])
phase('build',['dotnet','build','Source/IntegrationTests','-c','Release'])
entries=[s.strip() for s in Path('.github/review/regressions').read_text().splitlines() if s.strip() and not s.startswith('#')]
assert len(entries)>=191
phase('registered',['dotnet','test','Source/IntegrationTests','-c','Release','--no-build','--logger','console;verbosity=normal','--logger','trx;LogFileName=registered.trx','--results-directory',str((out/'trx').resolve()),'--filter','|'.join('DisplayName~'+s for s in entries)])
phase('axiom-translation',['dotnet','test','Source/IntegrationTests','-c','Release','--no-build','--filter','FullyQualifiedName~AdditionalAxiomsTranslationTests','--logger','console;verbosity=normal'])
receipt={'source':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'registered_entries':entries,'registered_manifest_sha256':hashlib.sha256(Path('.github/review/regressions').read_bytes()).hexdigest(),'diagnostic':True,'phases':rows,'solvers':{p.name:{'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'version':subprocess.check_output([str(p.resolve()),'--version'],text=True).strip()} for p in Path('Binaries/z3/bin').glob('z3-*')}}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
