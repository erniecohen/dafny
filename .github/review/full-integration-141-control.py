"""Verify the restored accepted issue141 negative controls in both axiom modes."""
import hashlib,json,os,re,subprocess
from pathlib import Path
out=Path('out/issue141');out.mkdir(parents=True,exist_ok=True)
assembly=Path('out/toolchain/dafny/Dafny.dll').resolve();solver=Path(os.environ['Z3']).resolve();rows=[]
for case,line in [('original',11),('original-control',19)]:
 source=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-141-'+case+'.dfy').resolve()
 for axioms in [False,True]:
  folder=out/(case+'-'+str(axioms));folder.mkdir(exist_ok=True)
  command=['dotnet',str(assembly),'verify',str(source),'--type-system-refresh=true','--additional-axioms:'+str(axioms).lower(),'--cores=1','--resource-limit=16000000','--verification-time-limit=60','--show-snippets=false','--use-basename-for-filename','--solver-path',str(solver),'--log-format','json;LogFileName='+str((folder/'results.json').resolve())]
  (folder/'command.json').write_text(json.dumps(command,indent=2));p=subprocess.run(command,capture_output=True,text=True,timeout=300);output=p.stdout+p.stderr;(folder/'output.txt').write_text(output)
  decls=json.loads((folder/'results.json').read_text()).get('verificationResults',[]);vcs=[v for d in decls for v in d['vcResults']]
  expected=(source.with_name(source.name+'.expect')).read_text().strip();actual=re.sub(r'(?m)^Results for .*\n?','',output).strip()
  # The native JSON log has no source-level theorem acceptance for either false control.
  passed=p.returncode==4 and expected in actual and len(re.findall(r'(?m)^.*: Error:',output))==1 and bool(vcs) and sum(v['outcome']=='Invalid' for v in vcs)==1 and all(v['outcome'] in ['Valid','Invalid'] for v in vcs)
  rows.append({'case':case,'additional_axioms':axioms,'expected_error_line':line,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'actual_exit':p.returncode,'declarations':len(decls),'vc_batches':len(vcs),'passed':passed});print(case,axioms,'actual exit',p.returncode,'strict pass',passed,flush=True)
receipt={'source':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),'core_sha256':hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'solver_version':subprocess.check_output([str(solver),'--version'],text=True).strip(),'profiles':rows,'complete':len(rows)==4 and all(r['passed'] for r in rows)}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
# Diagnostic wrapper records the actual expected-failure comparisons, never relabels them proof success.
