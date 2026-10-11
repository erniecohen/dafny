"""Capture complete native library profiles after verified archive regeneration."""
import hashlib,importlib.util,json,os,subprocess
from pathlib import Path
out=Path('out/library-recheck');out.mkdir(parents=True,exist_ok=True)
assembly=Path('out/toolchain/dafny/Dafny.dll').resolve();launcher=assembly.with_name('Dafny');solver=Path(os.environ['Z3']).resolve()
spec=importlib.util.spec_from_file_location('std',Path('.github/review/std-verdicts.py'));std=importlib.util.module_from_spec(spec);spec.loader.exec_module(std)
mode=os.environ['AXIOMS']=='on';actual=out/'verdicts.tsv'
# stdout preserves each complete original phase and its actual proof summaries.
std.run('Source/DafnyStandardLibraries',str(launcher),str(solver),str(actual),cores=4,additional_axioms=mode)
expected=out/'expected-verdicts.tsv'
with expected.open('w') as f:
 import contextlib
 with contextlib.redirect_stdout(f):std.expected(str(actual))
rows=[l.split('\t') for l in actual.read_text().splitlines()]
source=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
assert not subprocess.check_output(['git','diff',Path('out/toolchain/source.txt').read_text().strip(),source,'--','Source/DafnyCore','Source/DafnyDriver','Source/Dafny','Source/DafnyLanguageServer'],text=True)
receipt={'source':source,'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),'version':subprocess.check_output([str(launcher),'--version'],text=True).strip(),'solver_version':subprocess.check_output([str(solver),'--version'],text=True).strip(),'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'core_sha256':hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),'additional_axioms':mode,'consistent_obligation_checks':'default OFF','diagnostic':True,'rows':len(rows),'declarations':len([r for r in rows if not r[0].startswith('run ')]),'phases':[r[:4] for r in rows if r[0].startswith('run ')],'archives':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in Path('Source/DafnyStandardLibraries/binaries').glob('*.doo')},'source_hashes':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in Path('Source/DafnyStandardLibraries/src').rglob('*.dfy')}}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
