"""Run paired dev diagnostics; every actual verdict and resource observation is data."""
import argparse, hashlib, importlib.util, json, os, subprocess
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('kind',choices=['suite','library','summarize'])
p.add_argument('--mode',choices=['baseline','off','on'])
p.add_argument('--shard',default='0/1')
p.add_argument('--root',default='out/comparison')
a=p.parse_args();root=Path(a.root).resolve();root.mkdir(parents=True,exist_ok=True)
def module(name):
 spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(name+'.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
if a.kind!='summarize':
 executable=Path('out/toolchain/dafny/Dafny').resolve();solver=Path(os.environ['Z3']).resolve()
 if a.kind=='suite':
  runner=module('lit-verdicts')
  if a.mode=='on':runner.FIXED+=['--consistent-obligation-checks']
  sources=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest').resolve()
  runner.plan(str(sources),str(root/'plan.tsv'))
  runner.run(str(root/'plan.tsv'),str(sources),str(executable),str(solver),str(root/'verdicts.tsv'),shard=a.shard,jobs=4,measurements=str(root/'resources'))
 else:
  runner=module('std-verdicts')
  if a.mode=='on':runner.FIXED+=['--consistent-obligation-checks']
  runner.run('Source/DafnyStandardLibraries',str(executable),str(solver),str(root/'verdicts.tsv'),cores=4)
 versions={name:subprocess.check_output(command,text=True).strip() for name,command in [('dafny',[str(executable),'--version']),('solver',[str(solver),'--version'])]}
 assert '5.1.0' in versions['solver']
 (root/'receipt.json').write_text(json.dumps({'kind':a.kind,'mode':a.mode,'shard':a.shard,'verification_sources':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),'core_sha256':hashlib.sha256(Path('out/toolchain/dafny/DafnyCore.dll').read_bytes()).hexdigest(),'versions':versions,'binary_sha256':hashlib.sha256(executable.read_bytes()).hexdigest(),'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'diagnostic':True},indent=2)+'\n')
else:
 report={'diagnostic':True,'groups':{}}
 for kind in ['suite','library']:
  data={};resources={};coverage={};receipts={}
  for mode in ['baseline','off','on']:
   rows={};batches={};missing=[];proof_jsons=0
   for folder in sorted(root.glob(kind+'-'+mode+'-*')):
    receipts.setdefault(mode,[]).append(json.loads((folder/'receipt.json').read_text()))
    for line in (folder/'verdicts.tsv').read_text().splitlines():
     row=line.split('\t');assert row[0] not in rows,row[0];rows[row[0]]=row
    if kind=='suite':
     for row in (folder/'verdicts.tsv').read_text().splitlines():
      source=row.split('\t')[0];path=folder/'resources'/source/'results.json'
      if not path.exists():missing.append(source);continue
      try:content=json.loads(path.read_text())
      except json.JSONDecodeError:
       missing.append({'source':source,'reason':'empty or invalid verifier JSON','bytes':path.stat().st_size});continue
      proof_jsons+=1
      for declaration in content.get('verificationResults',[]):
       for vc in declaration['vcResults']:
        key=f"{source}|{declaration['name']}|{vc['vcNum']}|{vc.get('randomSeed','0')}"
        assert key not in batches,key;batches[key]=[vc['outcome'],vc['resourceCount']]
    else:
     for key,row in rows.items():
      if not key.startswith('run '):batches[key]=[row[1],int(row[4])]
   assert rows,(kind,mode)
   data[mode]={key:row[1:4] for key,row in rows.items()};resources[mode]=batches
   coverage[mode]={'rows':len(rows),'batches':len(batches),'proof_jsons':proof_jsons,'unavailable':missing}
  def diff(left,right):return [{'key':key,'baseline':left.get(key),'candidate':right.get(key)} for key in sorted(set(left)|set(right)) if left.get(key)!=right.get(key)]
  report['groups'][kind]={'coverage':coverage,'receipts':receipts,'off_verdict_differences':diff(data['baseline'],data['off']),'on_verdict_differences':diff(data['baseline'],data['on']),'off_resource_differences':diff(resources['baseline'],resources['off']),'on_resource_differences':diff(resources['baseline'],resources['on']),'resource_totals':{mode:sum(item[1] for item in batches.values()) for mode,batches in resources.items()}}
 (root/'comparison.json').write_text(json.dumps(report,indent=2)+'\n')
 summary='Paired development diagnostics; wrapper success is not acceptance.\n\n'+''.join(f"- {kind}: OFF verdict changes {len(entry['off_verdict_differences'])}, ON verdict changes {len(entry['on_verdict_differences'])}; complete rows and RU differences in artifact.\n" for kind,entry in report['groups'].items())
 print(summary)
 if os.environ.get('GITHUB_STEP_SUMMARY'):Path(os.environ['GITHUB_STEP_SUMMARY']).write_text(summary)
