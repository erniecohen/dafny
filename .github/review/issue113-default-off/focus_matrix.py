#!/usr/bin/env python3
"""Pure focused workload; no engine calls."""
import json,os,sys
from pathlib import Path
rows=[dict(line='shipped',cohort='repair82-literal',mode='literal',axioms='literal',solver='literal',shard=str(i)+'/2',part='',jobs=2,tag='shipped-repair82-literal-'+str(i)) for i in range(2)]
for solver in ['5.1.0','reference']:
 for axioms in ['off','on']:
  rows.append(dict(line='shipped',cohort='parser-focus',mode='project',axioms=axioms,solver=solver,shard='0/1',part='',jobs=1,tag='shipped-parser-focus-'+solver+'-ax-'+axioms))
candidate='--candidate' in sys.argv and sys.argv[sys.argv.index('--candidate')+1]=='true'
result=dict(include_candidate=candidate,schema_version=1,matrix=rows,parser_pairs=4,repair82_sources=5,repair82_pairs=93,requested_invocations=291 if candidate else 194,diagnostic_only=True,boundary='Derived parser selection from unchanged complete original Std source and projects. Separate82 literals keep all flags/solver macros. No full canonical gate replacement.')
Path(sys.argv[1]).write_text(json.dumps(result,indent=2)+'\n')
if '--github-output' in sys.argv:
 with open(os.environ['GITHUB_OUTPUT'],'a') as f:f.write('matrix='+json.dumps(dict(include=rows),separators=(',',':'))+'\n')
print(json.dumps(dict(paired_jobs=len(rows),requested_invocations=291 if candidate else 194)))
