"""Compare stale supported ON oracle paths with exact supported/combined source profiles."""
import hashlib,importlib.util,json,os,subprocess,tarfile
from pathlib import Path
kind=os.environ['COMPILER_SOURCE'];out=Path('out/on-reference');out.mkdir(parents=True,exist_ok=True)
assembly=Path('out/toolchain/dafny/Dafny.dll').resolve();launcher=assembly.with_name('Dafny');solver=Path(os.environ['Z3']).resolve()
spec=importlib.util.spec_from_file_location('lit',Path('.github/review/lit-verdicts.py'));lit=importlib.util.module_from_spec(spec);spec.loader.exec_module(lit)
source=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
if kind=='supported-reference':
 source='1256f92b0b494e8f1bf52aaec145d36a8a862f33'
 archive=out/'source.tar'
 with archive.open('wb') as f:subprocess.run(['git','archive',source,'Source'],stdout=f,check=True)
 with tarfile.open(archive) as f:f.extractall(out/'source',filter='data')
 root=(out/'source/Source/IntegrationTests/TestFiles/LitTests/LitTest').resolve()
else:root=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest').resolve()
plan=out/'all-plan.tsv';lit.plan(str(root),str(plan));allrows={line.split('\t')[0]:line for line in plan.read_text().splitlines()}
keys=json.loads(Path('.github/review/full-integration-on-reference-programs.json').read_text())
selected=[allrows[k] for k in keys if k in allrows];(out/'plan.tsv').write_text('\n'.join(selected)+'\n')
failure=None
try:
 lit.run(str(out/'plan.tsv'),str(root),str(launcher),str(solver),str(out/'verdicts.tsv'),jobs=3,additional_axioms=True,measurements=str(out/'measurements'))
 assert len((out/'verdicts.tsv').read_text().splitlines())==len(selected)>0
except Exception as error:
 failure=str(error)
 print('Reference capture incomplete:',error,flush=True)
receipt={'source':source,'candidate':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),'compiler_kind':kind,'requested_programs':keys,'planned_programs':[k for k in keys if k in allrows],'omitted_programs':[k for k in keys if k not in allrows],'profile':'normal planner with additional-axioms=true and consistent-obligation-checks default OFF','solver_version':subprocess.check_output([str(solver),'--version'],text=True).strip(),'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'core_sha256':hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),'source_hashes':{k:hashlib.sha256((root/k).read_bytes()).hexdigest() for k in keys if (root/k).exists()},'diagnostic':True,'complete':failure is None,'capture_failure':failure}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
