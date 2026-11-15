#!/usr/bin/env python3
"""Capture the new fixture's OFF/ON verdict rows before changing a baseline."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys

root=Path(__file__).resolve().parents[2]
dafny,solver,out=(Path(x).resolve() for x in sys.argv[1:])
out.mkdir(parents=True,exist_ok=True)
lit=root/'Source/IntegrationTests/TestFiles/LitTests/LitTest'
runner=root/'.github/review/lit-verdicts.py'
subprocess.run([sys.executable,str(runner),'plan',str(lit),str(out/'all.tsv')],check=True)
fixture='git-issues/git-issue-193-reference-laws.dfy'
rows=[r for r in (out/'all.tsv').read_text().splitlines() if r.split('\t')[0]==fixture]
assert len(rows)==1,rows
(out/'plan.tsv').write_text(rows[0]+'\n')
records=[]
for mode in ['off','on']:
    actual=out/f'{mode}.tsv'
    subprocess.run([sys.executable,str(runner),'run',str(out/'plan.tsv'),str(lit),str(dafny),str(solver),str(actual),
                    '--additional-axioms',str(mode=='on').lower(),'--measurements',str(out/f'{mode}-resources')],check=True)
    expected=subprocess.check_output([sys.executable,str(runner),'expected',str(actual)],text=True)
    suffix='-additional-axioms' if mode=='on' else ''
    name=f'expected-verdicts{suffix}.tsv'
    (out/name).write_text(expected)
    original=(root/'.github/review'/name).read_text()
    before='\n'.join(r for r in original.splitlines() if r.split('\t')[0]==fixture)
    (out/f'{mode}-before.tsv').write_text(before+'\n' if before else '')
    p=subprocess.run([sys.executable,str(runner),'compare',str(out/f'{mode}-before.tsv'),str(actual)],capture_output=True,text=True)
    (out/f'{mode}-comparison.txt').write_text(p.stdout+p.stderr)
    records.append({'mode':mode,'comparison_exit_code':p.returncode,'new_row':expected.strip(),'comparison':p.stdout+p.stderr})
(out/'verdict-probe.json').write_text(json.dumps(records,indent=2)+'\n')
print(json.dumps(records,indent=2))
print('Probe only: expected baseline differences are recorded, not accepted.')
