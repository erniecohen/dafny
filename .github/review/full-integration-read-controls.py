"""Record exact 5.1 native allocation/read outcomes; failed phases remain diagnostic."""
import hashlib,json,os,re,subprocess
from pathlib import Path
root=Path('out/read-evidence');root.mkdir(parents=True,exist_ok=True)
assembly=Path('out/toolchain/dafny/Dafny.dll').resolve();solver=Path(os.environ['Z3']).resolve()
lit=Path('Source/IntegrationTests/TestFiles/LitTests/LitTest');rows=[]
cases=[('reads','dafny0/Reads.dfy',[],50000000,4),
 ('reads-methods','dafny0/ReadsOnMethods.dfy',['--reads-clauses-on-methods'],50000000,4),
 ('naked','hofs/Naked.dfy',[],50000000,2),
 ('capture-positive','git-issues/Inputs/github-issue-132-positive.dfy',[],5000000,0),
 ('capture-negative','git-issues/Inputs/github-issue-132-negative.dfy',['--allow-axioms'],5000000,4),
 ('pure-callback','git-issues/Inputs/git-issue-137-pure-callback-controls.dfy',[],2000000,4),
 ('finite-support','git-issues/git-issue-82.dfy',['--allow-warnings'],5000000,0),
 ('finite-support-false','git-issues/Inputs/git-issue-82-lambda-alloc-negative.dfy',['--allow-warnings'],5000000,4)]
for name,source,extra,limit,expected in cases:
 for refresh in [False,True]:
  for enabled in [False,True]:
   folder=root/f'{name}-{refresh}-{enabled}';folder.mkdir(exist_ok=True)
   command=['dotnet',str(assembly),'verify',str(lit/source),'--solver-path',str(solver),
    '--cores:1','--resource-limit:'+str(limit),'--verification-time-limit:60',
    '--show-snippets:false','--use-basename-for-filename','--error-limit:0',
    '--type-system-refresh:'+str(refresh).lower(),'--general-newtypes:'+str(refresh).lower(),
    '--general-traits:datatype' if refresh else '--general-traits:legacy',
    '--additional-axioms:false','--consistent-obligation-checks:'+str(enabled).lower(),
    '--log-format','json;LogFileName='+str((folder/'results.json').resolve())]+extra
   (folder/'command.json').write_text(json.dumps(command,indent=2)+'\n')
   result=subprocess.run(command,capture_output=True,text=True,timeout=300)
   (folder/'output.txt').write_text(result.stdout+result.stderr)
   data=json.loads((folder/'results.json').read_text()) if (folder/'results.json').exists() else None
   declarations=(data or {}).get('verificationResults',[])
   vcs=[vc for declaration in declarations for vc in declaration.get('vcResults',[])]
   row={'name':name,'refresh':refresh,'enabled':enabled,'exit':result.returncode,
    'declaration_count':len(declarations),'vc_count':len(vcs),
    'has_proof_evidence':bool(declarations and vcs),
    'expected_exit':expected,'resource_limit':limit,'source_sha256':hashlib.sha256((lit/source).read_bytes()).hexdigest(),
    'has_json_schema':bool(data),'error_locations':re.findall(r'([^\s]+\(\d+,\d+\)): Error:',result.stdout+result.stderr),
    'summaries':re.findall(r'Dafny program verifier finished with ([^\n]+)',result.stdout+result.stderr)}
   rows.append(row);print(name,refresh,enabled,'actual',result.returncode,'expected',expected,flush=True)
receipt={'diagnostic':True,'scope':'Exact native 5.1 outcomes on original unmodified upstream and supported fixtures; not complete Lit acceptance.',
 'compiler_source':Path('out/toolchain/source.txt').read_text().strip(),
 'verification_sources':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),
 'compiler_version':subprocess.check_output(['dotnet',str(assembly),'--version'],text=True).strip(),
 'compiler_core_sha256':hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),
 'solver_version':subprocess.check_output([str(solver),'--version'],text=True).strip(),
 'solver_sha256':hashlib.sha256(solver.read_bytes()).hexdigest(),'rows':rows}
assert '5.1.0' in receipt['solver_version']
(root/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
