#!/usr/bin/env python3
"""Keep existing verdict inputs and compare opt-in outcomes and resource units."""
import argparse, importlib.util, json
from pathlib import Path

def load(name):
 spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(name+'.py'))
 module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

def differences(left,right):
 return [{'key':key,'baseline':left.get(key),'candidate':right.get(key),
  'delta':right[key][1]-left[key][1] if key in left and key in right and isinstance(left[key][1],int) and isinstance(right[key][1],int) else None}
  for key in sorted(set(left)|set(right)) if left.get(key)!=right.get(key)]

def suite(args):
 runner=load('lit-verdicts')
 if args.enabled:runner.FIXED+=['--consistent-obligation-checks']
 runner.run(args.plan,args.sources,args.dafny,args.solver,args.output,
  shard=args.shard,jobs=4,additional_axioms=args.axioms,measurements=args.measurements)

def library(args):
 runner=load('std-verdicts')
 if args.enabled:runner.FIXED+=['--consistent-obligation-checks']
 runner.run(args.sources,args.dafny,args.solver,args.output,cores=4,additional_axioms=args.axioms)

def compare(args):
 root=Path(args.directory)
 report={'suite':{},'library':{}}
 for kind in ['suite','library']:
  files=sorted(root.glob(kind+'-*/report-input.json'))
  for file in files:
   spec=json.loads(file.read_text());directory=file.parent
   runner=load('lit-verdicts' if kind=='suite' else 'std-verdicts')
   tables={name:runner.load(directory/(name+'.tsv')) for name in ['baseline','off','on']}
   verdict=lambda table:{key:row[1:4] for key,row in table.items()}
   counts=lambda table:{key:[row[1],int(row[4])] for key,row in table.items() if not key.startswith('run ')}
   if kind=='suite':
    inventories={name:json.loads((directory/(name+'-resources.json')).read_text()) for name in tables}
    counts=lambda table:None
    batches={name:inv['batches'] for name,inv in inventories.items()}
    coverage={name:{'programs':len(inv['programs']),'batches':len(inv['batches']),'unavailable':inv['unavailable']} for name,inv in inventories.items()}
   else:
    batches={name:counts(table) for name,table in tables.items()}
    coverage={name:{'declarations':len(batch)} for name,batch in batches.items()}
   entry={'inputs':spec,'coverage':coverage,
    'off_verdict_differences':differences(verdict(tables['baseline']),verdict(tables['off'])),
    'on_verdict_differences':differences(verdict(tables['baseline']),verdict(tables['on'])),
    'off_resource_differences':differences(batches['baseline'],batches['off']),
    'on_resource_differences':differences(batches['baseline'],batches['on']),
    'totals':{name:sum(batch[1] for batch in data.values()) for name,data in batches.items()}}
   entry['performance_review']=[row for row in entry['on_resource_differences'] if row['baseline'] and row['candidate'] and
    row['baseline'][0]==row['candidate'][0] and row['candidate'][0] in ['Valid','Correct'] and
    row['candidate'][1]>2*row['baseline'][1] and row['delta']>=100000]
   report[kind][directory.name]=entry
 Path(args.output).write_text(json.dumps(report,indent=2)+'\n')
 for kind,entries in report.items():
  for name,entry in entries.items():
   print(name,entry['coverage'],'off verdict/RU differences',len(entry['off_verdict_differences']),len(entry['off_resource_differences']),
    'on verdict/RU differences',len(entry['on_verdict_differences']),len(entry['on_resource_differences']),
    'performance review',len(entry['performance_review']))

parser=argparse.ArgumentParser(description=__doc__);commands=parser.add_subparsers(dest='command',required=True)
for name in ['suite','library']:
 p=commands.add_parser(name)
 if name=='suite':p.add_argument('plan');p.add_argument('--shard',default='0/1');p.add_argument('--measurements')
 for item in ['sources','dafny','solver','output']:p.add_argument(item)
 p.add_argument('--enabled',action='store_true');p.add_argument('--axioms',action='store_true')
p=commands.add_parser('compare');p.add_argument('directory');p.add_argument('output')
a=parser.parse_args();{'suite':suite,'library':library,'compare':compare}[a.command](a)
