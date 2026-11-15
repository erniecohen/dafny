#!/usr/bin/env python3
"""Compare complete library Boogie input. This does not claim library verification."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
p=argparse.ArgumentParser();p.add_argument('--baseline',required=True);p.add_argument('--candidate',required=True);p.add_argument('--output',required=True)
a=p.parse_args();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
project=Path('Source/DafnyStandardLibraries/src/Std/dfyconfig.toml').resolve()
report={}
for mode,binary,flags in [('B',a.baseline,[]),('O',a.candidate,[]),('F',a.candidate,['--additional-axioms=false'])]:
    dest=out/mode;dest.mkdir(exist_ok=True)
    cmd=[binary,'build','-t:lib','--no-verify',str(project),'--output',str(dest/'Std'),'--bprint',str(dest/'Std.bpl')]+flags
    result=subprocess.run(cmd,capture_output=True,text=True,timeout=1200)
    (dest/'output.txt').write_text(result.stdout+result.stderr)
    (dest/'command.json').write_text(json.dumps(cmd))
    modules={path.name:hashlib.sha256(path.read_bytes()).hexdigest() for path in dest.glob('*.bpl')}
    report[mode]={'exit':result.returncode,'modules':modules}
report['identical']=all(report[m]['exit']==0 for m in ['B','O','F']) and report['B']['modules'] == report['O']['modules'] == report['F']['modules'] and bool(report['B']['modules'])
(out/'report.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
