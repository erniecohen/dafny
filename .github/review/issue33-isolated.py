#!/usr/bin/env python3
"""Generate the isolated-assertion counterpart without changing the stress source."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
p=argparse.ArgumentParser();p.add_argument('--dafny',required=True);p.add_argument('--z3',required=True);p.add_argument('--input',required=True);p.add_argument('--output',required=True)
a=p.parse_args();out=Path(a.output);out.mkdir(parents=True,exist_ok=True)
source=out/'input.dfy';shutil.copyfile(a.input,source)
cmd=[a.dafny,'verify',str(source),'--additional-axioms','--solver-path',a.z3,'--cores','1','--resource-limit','20000000','--verification-time-limit','0','--isolate-assertions','--log-format','csv;LogFileName='+str(out/'results.csv'),'--solver-log',str(out/'solver.smt2'),'--solver-option','O:smt.qi.profile=true','--boogie','/emitDebugInformation:1']
(out/'command.json').write_text(json.dumps(cmd,indent=2))
try:
    result=subprocess.run(cmd,capture_output=True,text=True,timeout=2400)
    (out/'stdout.txt').write_text(result.stdout);(out/'stderr.txt').write_text(result.stderr)
    (out/'exit-code.txt').write_text(str(result.returncode))
except subprocess.TimeoutExpired as e:
    (out/'exit-code.txt').write_text('wall-time safety cap')
    for name,data in [('stdout.txt',e.stdout),('stderr.txt',e.stderr)]:
        (out/name).write_bytes(data or b'')
print((out/'exit-code.txt').read_text(),flush=True)
