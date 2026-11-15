#!/usr/bin/env python3
"""Re-run reviewed suite cases with their original options and capture lowering."""
import argparse, concurrent.futures, importlib.util, json, pathlib, shlex, subprocess
p=argparse.ArgumentParser(description=__doc__)
for name in ['dafny','solver','output']:p.add_argument(name,type=pathlib.Path)
a=p.parse_args();exe=a.dafny.resolve();solver=a.solver.resolve();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
spec=importlib.util.spec_from_file_location('lit_verdicts',pathlib.Path(__file__).with_name('lit-verdicts.py'))
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
root=pathlib.Path('Source/IntegrationTests/TestFiles/LitTests/LitTest').resolve()
runner.plan(root,out/'plan.tsv')
options={line.split('\t')[0]:line.rstrip('\n').split('\t')[1] for line in (out/'plan.tsv').read_text().splitlines(keepends=True)}
cases=pathlib.Path(__file__).with_name('obligation-lit-cases.txt').read_text().splitlines()
selected={'dafny4/Bug75.dfy','git-issues/git-issue-895.dfy','dafny0/AllLiteralsAxiom.dfy','dafny0/Compilation.dfy','dafny0/LeastGreatest.dfy'}
def verify(task):
 source,enabled=task;directory=out/('on' if enabled else 'off')/source;directory.mkdir(parents=True,exist_ok=True)
 own=shlex.split(options[source]);names={t.split('=')[0].split(':')[0] for t in own if t.startswith('--')}
 fixed=[];i=0
 while i<len(runner.FIXED):
  key=runner.FIXED[i];takes=i+1<len(runner.FIXED) and not runner.FIXED[i+1].startswith('--')
  if key not in names:fixed+=runner.FIXED[i:i+2] if takes else [key]
  i+=2 if takes else 1
 extra=['--consistent-obligation-checks:'+str(enabled).lower(),'--log-format','json;LogFileName='+str(directory/'results.json')]
 if source in selected:extra+=['--bprint',str(directory/'program.bpl'),'--pprint',str(directory/'passive.bpl'),'--solver-log',str(directory/'solver.smt2')]
 cmd=[str(exe),'verify',source,'--solver-path',str(solver)]+fixed+extra+own
 (directory/'command.json').write_text(json.dumps(cmd))
 try:
  result=subprocess.run(cmd,cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=runner.TIMEOUT)
  output=result.stdout;status=result.returncode
 except subprocess.TimeoutExpired:output='TIMEOUT';status='TIMEOUT'
 (directory/'output.txt').write_text(output)
 row={'source':source,'enabled':enabled,'exit':status,'summary':[line for line in output.splitlines() if 'verifier finished' in line],
      'errors':[line for line in output.splitlines() if 'Error' in line or 'Exception' in line]}
 print(json.dumps(row),flush=True);return row
with concurrent.futures.ThreadPoolExecutor(4) as executor:rows=list(executor.map(verify,[(source,enabled) for source in cases for enabled in [False,True]]))
(out/'summary.json').write_text(json.dumps(rows,indent=2))
