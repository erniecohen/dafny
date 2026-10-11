"""Record actual integration metadata and structural gate outcomes with Z3 5.1.0."""
from pathlib import Path
import subprocess,os,json,hashlib
root=Path('out/repair-evidence');root.mkdir(parents=True,exist_ok=True)
solver=Path(os.environ['Z3']).resolve();binary=Path('out/toolchain/dafny/Dafny').resolve()
rows=[]
def phase(name,command,env=None):
 folder=root/name;folder.mkdir(exist_ok=True)
 (folder/'command.json').write_text(json.dumps(command,indent=2)+'\n')
 with (folder/'output.txt').open('w') as output:result=subprocess.run(command,stdout=output,stderr=subprocess.STDOUT,env=env,timeout=1800)
 rows.append({'name':name,'exit':result.returncode});print(name,'actual exit',result.returncode,flush=True)
phase('packages',['sh','Scripts/fetch-boogie-packages.sh'])
capture=os.environ.copy();capture['OBLIGATION_INVENTORY_CAPTURE']=str((root/'captured-producers.json').resolve())
phase('capture-inventory',['dotnet','test','Source/DafnyCore.Test','-c','Release','--filter','FullyQualifiedName~EveryProducerMatchesTheReviewedInventory'],capture)
phase('core-all',['dotnet','test','Source/DafnyCore.Test','-c','Release','--no-build','--logger','trx;LogFileName=tests.trx','--results-directory',str((root/'core-all').resolve())])
phase('finite-support',['python3','.github/review/issue82-audit.py','--dafny',str(binary),'--z3',str(solver),'--output',str((root/'finite-support').resolve())])
phase('resolver',['python3','.github/review/resolver-matrix.py','--manifest','.github/review/resolver-cases.json','--lit-dir','Source/IntegrationTests/TestFiles/LitTests/LitTest','--dafny',str(binary),'--expected','.github/review/expected-resolver-results.json','--output',str((root/'resolver').resolve())])
receipt={'diagnostic':True,'verification_sources':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),
 'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),
 'compiler_version':subprocess.check_output([str(binary),'--version'],text=True).strip(),
 'solver_version':subprocess.check_output([str(solver),'--version'],text=True).strip(),
 'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'phases':rows}
assert '5.1.0' in receipt['solver_version']
(root/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
