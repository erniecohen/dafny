#!/usr/bin/env python3
"""Record library source verification B/O/E and repeat controls for its known order variation."""
import argparse
import importlib.util
import json
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--baseline',required=True);p.add_argument('--candidate',required=True)
p.add_argument('--z3',required=True);p.add_argument('--output',required=True)
a=p.parse_args();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
spec=importlib.util.spec_from_file_location('std','.github/review/std-verdicts.py')
std=importlib.util.module_from_spec(spec);spec.loader.exec_module(std)
files={}
original_part = std.part
for mode,binary,flag in [('B',a.baseline,[]),('O',a.candidate,[]),('B2',a.baseline,[]),('O2',a.candidate,[]),('E',a.candidate,['--additional-axioms'])]:
    def measured_part(label, cwd, args, dafny, z3, cores, fixed):
        fixed = fixed + ['--log-format', 'csv;LogFileName=' + str(out / (mode + '-' + label + '.csv'))]
        return original_part(label, cwd, args, dafny, z3, cores, fixed)
    std.part = measured_part
    std.FIXED=['--verification-time-limit','0']+flag
    path=out/(mode+'.tsv')
    std.run(str(Path('Source/DafnyStandardLibraries').resolve()),binary,a.z3,str(path),cores=4)
    files[mode]=std.load(str(path))
    print(mode,'rows',len(files[mode]),flush=True)
# Separate outcomes from proof cost. Run rows include nondeterministic times.
# Full translation identity is measured separately by issue33-translation.py.
report={}
for x,y in [('B','O'),('B','B2'),('O','O2'),('O','E')]:
    different=[];resources=[];regressions=[]
    for key in sorted(set(files[x])|set(files[y])):
        left=files[x].get(key);right=files[y].get(key)
        if left is None or right is None or std.verdict(left)!=std.verdict(right):different.append([key,left,right])
        if left and right and not key.startswith('run '):
            if left[4]!=right[4]:resources.append([key,left[4],right[4]])
            if left[1]=='Correct' and right[1]!='Correct':regressions.append([key,left[1],right[1]])
    report[x+'/'+y]={'verdict_changes':different,'resource_changes':resources,'regressions':regressions}
(out/'report.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:{field:len(value) for field,value in entry.items()} for k,entry in report.items() if '/' in k},indent=2))
