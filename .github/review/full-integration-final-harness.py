"""Replay explained metadata failures and all maps/reads RUN/check pairs."""
import hashlib,json,os,re,shlex,subprocess,sys,xml.etree.ElementTree as ET
from pathlib import Path
out=Path('out/final-harness');out.mkdir(parents=True,exist_ok=True)
source=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
def digest(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(name,argv,expected=0):
 d=out/name;d.mkdir(parents=True,exist_ok=True)
 (d/'command.json').write_text(json.dumps(argv,indent=2))
 with (d/'stdout.txt').open('w') as so,(d/'stderr.txt').open('w') as se:
  try:code=subprocess.run(argv,stdout=so,stderr=se,timeout=900).returncode
  except subprocess.TimeoutExpired:code=124
 row={'name':name,'actual':code,'expected':expected,'passed':code==expected}
 (d/'exit.txt').write_text(str(code)+'\n');print(name,row,flush=True);return row
if sys.argv[1]=='resources':
 before=out/'before';before.mkdir(exist_ok=True)
 for name in ['additional-axioms.py','std-verdicts.py','lit-verdicts.py']:
  (before/name).write_bytes(subprocess.check_output(['git','show','e3c0e064e:.github/review/'+name]))
 prior=run('resource-before',['python3',str(before/'additional-axioms.py'),'resources','measurements',str(out/'before-report')],1)
 prior['passed'] &= 'std-verdicts-off-z3-4.12.1/std-verdicts.tsv' in (out/'resource-before/stderr.txt').read_text()
 v=re.search(r'DefaultZ3Version = "([^"]+)"',Path('Source/DafnyCore/DafnyOptions.cs').read_text()).group(1)
 fixed=run('resource-after',['python3','.github/review/additional-axioms.py','resources','measurements',str(out/'resource-report'),'--harness-z3',v])
 report=json.loads((out/'resource-report/resources.json').read_text())
 fixed['passed'] &= set(report['library'])=={'5.1.0','4.16.0'} and all(len(rows)==2432 for rows in report['library'].values()) and all(c['programs']==1183 and c['batches']>0 for c in report['suite_coverage'].values())
 receipt={'source':source,'reference_run':38156642539,'before':prior,'after':fixed,'suite_coverage':report['suite_coverage'],'library_rows':{k:len(v) for k,v in report['library'].items()},'solver_archives':report['solver_archive_sha256'],'input_sha256':{str(p):digest(p) for p in Path('measurements').rglob('*') if p.is_file()},'passed':prior['passed'] and fixed['passed']}
else:
 assembly=Path('out/toolchain/dafny/Dafny.dll').resolve();exe=assembly.with_name('Dafny');z3=Path(os.environ['Z3']).resolve()
 assert not subprocess.check_output(['git','diff','e3c0e064e',source,'--','Source/DafnyCore','Source/DafnyDriver','Source/Dafny','Source/DafnyLanguageServer'],text=True)
 def check(text,spec):
  cursor=0;pending=[]
  for line in spec.splitlines():
   if line.startswith('// CHECK-NOT: '):pending.append(line.split(': ',1)[1])
   elif line.startswith('// CHECK: '):
    match=re.search(line.split(': ',1)[1],text[cursor:],re.MULTILINE)
    if match is None:return False
    begin=cursor+match.start();end=cursor+match.end()
    if any(re.search(p,text[cursor:begin],re.MULTILINE) for p in pending):return False
    cursor=end;pending=[]
   elif line.strip():raise ValueError('Unsupported check directive '+line)
  return not any(re.search(p,text[cursor:],re.MULTILINE) for p in pending)
 rows=[];fixture_rows=[]
 for name in ['git-issue-82-maps.dfy','git-issue-82-reads.dfy']:
  fixture=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues')/name
  folder=(out/fixture.stem).resolve();folder.mkdir(exist_ok=True)
  commands=[(i,line[8:]) for i,line in enumerate(fixture.read_text().splitlines(),1) if line.startswith('// RUN: ')]
  assert len(commands)%2==0
  for j in range(0,len(commands),2):
   n,raw=commands[j];cn,cr=commands[j+1]
   def expand(s):return s.replace('%S',str(fixture.parent.resolve())).replace('%review-z3',str(z3)).replace('%t',str(folder/'temporary'))
   args=shlex.split(expand(raw));assert args[:1]==['%exits-with'] and args[2:4]==['%baredafny','verify']
   expect=int(args[1]);cut=args.index('>');stdout=Path(args[cut+1]);args=args[3:cut]
   log=folder/('run-'+str(n)+'.json')
   argv=[str(exe),*args,'--log-format','json;LogFileName='+str(log)]
   result=run(fixture.stem+'/run-'+str(n),argv,expect)
   text=(out/result['name']/'stdout.txt').read_text();stdout.write_text(text)
   ca=shlex.split(expand(cr));assert ca[:2]==['%OutputCheck','--file-to-check'] and Path(ca[2])==stdout
   spec=Path(ca[3]);rel=spec.relative_to(Path.cwd())
   old=subprocess.check_output(['git','show','e3c0e064e:'+str(rel)],text=True)
   current=check(text,spec.read_text());old_pass=check(text,old)
   data=json.loads(log.read_text());decl=data['verificationResults'];vcs=[v for d in decl for v in d['vcResults']]
   result.update({'fixture':str(fixture),'run_line':n,'check_line':cn,'run':raw,'check':cr,'check_source':str(rel),'check_sha256':digest(spec),'before_check_passed':old_pass,'after_check_passed':current,'declarations':len(decl),'vc_batches':len(vcs),'vc_outcomes':{k:sum(v['outcome']==k for v in vcs) for k in sorted({v['outcome'] for v in vcs})},'diagnostic_errors':[l for l in text.splitlines() if 'Error:' in l]})
   result['passed'] &= current and len(decl)>0 and len(vcs)>0 and all(v['outcome'] in ['Valid','Invalid'] for v in vcs) and not (out/result['name']/'stderr.txt').read_text()
   if expect==0:result['passed'] &= all(v['outcome']=='Valid' for v in vcs)
   else:result['passed'] &= any(v['outcome']=='Invalid' for v in vcs)
   if old!=spec.read_text():result['passed'] &= not old_pass
   rows.append(result)
  fixture_rows.append({'fixture':str(fixture),'sha256':digest(fixture),'run_lines':len(commands),'invocations':len(commands)//2})
 packages=run('harness-packages',['sh','Scripts/fetch-boogie-packages.sh'])
 build=run('harness-build',['dotnet','build','Source/IntegrationTests','-c','Release'])
 harness=run('complete-harness-sequences',['dotnet','test','Source/IntegrationTests','-c','Release','--no-build','--filter','DisplayName~git-issues/git-issue-82-maps.dfy|DisplayName~git-issues/git-issue-82-reads.dfy','--logger','console;verbosity=normal','--logger','trx;LogFileName=controls.trx','--results-directory',str((out/'trx').resolve())])
 tree=ET.parse(out/'trx/controls.trx');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};tests=tree.findall('.//t:UnitTestResult',ns)
 harness['tests']=[{'name':t.attrib['testName'],'outcome':t.attrib['outcome']} for t in tests];harness['passed'] &= len(tests)==2 and all(t.attrib['outcome']=='Passed' for t in tests)
 receipt={'source':source,'compiler_source':'e3c0e064e1deed523cfc6fb1a888556b315be7a0','version':subprocess.check_output([str(exe),'--version'],text=True).strip(),'core_sha256':digest(assembly.with_name('DafnyCore.dll')),'solver_sha256':digest(z3),'solver_version':subprocess.check_output([str(z3),'--version'],text=True).strip(),'fixtures':fixture_rows,'invocations':rows,'harness_phases':[packages,build,harness],'passed':all(x['passed'] for x in rows+[packages,build,harness])}
(out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
print('Expected-failure-aware diagnostic; actual acceptance:',receipt['passed'],flush=True)
# All actual outcomes are retained. Expected before failures do not fail a probe workflow.
