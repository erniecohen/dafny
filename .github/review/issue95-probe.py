import json, os, pathlib, subprocess
out=pathlib.Path('issue95-results'); out.mkdir(exist_ok=True)
rows=[]
for case in ['original','positive','negative']:
 for mode in ['false','true']:
  for option in ['false','true']:
   name=f'{case}-{mode}-{option}'
   folder=out/name; folder.mkdir()
   source='Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-95-'+case+'.dfy'
   cmd=['out/dafny/Dafny','verify',source,'--function-syntax:3','--allow-axioms', '--type-system-refresh:'+mode,'--additional-axioms:'+option,'--solver-path',os.environ['Z3'],'--show-snippets:false','--use-basename-for-filename','--resource-limit:16000000','--verification-time-limit:60','--cores:1','--boogie:/print:'+str(folder/'program.bpl')]
   try:
    p=subprocess.run(cmd,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=180)
    code,output=p.returncode,p.stdout
   except subprocess.TimeoutExpired as e:
    code,output='TIMEOUT',str(e.stdout)
   (folder/'stdout.txt').write_text(output)
   rows.append(dict(case=case,refresh=mode,option=option,exit=code,output=output))
   print(name,code,output)
(out/'results.json').write_text(json.dumps(rows,indent=2))
with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as f:
 for row in rows:
  f.write(str({k:v for k,v in row.items() if k!='output'})+'\n\n')
# Probe deliberately records expected failures without failing the workflow.
