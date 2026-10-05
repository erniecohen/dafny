#!/usr/bin/env python3
"""Retain six pending control observations; no intended-exit acceptance shortcut."""
import hashlib,json,os,sys
from pathlib import Path
ROOT=Path.cwd();HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'.github/review/issue113-benchmarks'))
import run
spec=json.loads((HERE/'controls.json').read_text());observations=[]
for arm in ['before','after']:
 compiler=ROOT/'out'/arm/'dafny/Dafny'
 compiler_root=ROOT/('before-compiler' if arm=='before' else 'compiler')
 expected=(compiler_root/'compiler-version.txt').read_text().strip()
 compiler_source_sha=json.loads((compiler_root/'compiler-source-binding.json').read_text())['scratch_source_sha']
 dest=ROOT/'measurements'/arm/'controls';dest.mkdir(parents=True)
 preflight=run.execute([str(compiler),'--version'],dest,'compiler-version',30)
 if preflight['exit']!=0 or preflight['stdout'].strip()!=expected:raise ValueError('Control compiler actual version differs')
 for case in spec['cases']:
  source=HERE/case['file']
  if run.digest(source)!=case['sha256']:raise ValueError('Control bytes changed')
  directory=dest/source.stem;directory.mkdir()
  command=[str(compiler),'verify',str(source),'--general-newtypes=true','--type-system-refresh=true','--unicode-char=true','--extended-newtype-bases=true','--show-snippets=false','--solver-path',os.environ['SOLVER'],'--cores=1','--resource-limit=16000000','--verification-time-limit=60','--boogie','/normalizeDeclarationOrder:0','--boogie','/proverOpt:O:smt.random_seed=0','--bprint',str(directory/'program.bpl'),'--log-format','csv;LogFileName='+str(directory/'resources.csv'),'--log-format','json;LogFileName='+str(directory/'results.json')]
  result=run.execute(command,directory,'verify',90)
  result.update(compiler_arm=arm,case=case,measurement_checkout_sha=os.environ['GITHUB_SHA'],compiler_source_sha=compiler_source_sha,compiler_version=expected,compiler_components_sha256=run.compiler_component_hashes(compiler),solver_binary_sha256=run.digest(os.environ['SOLVER']),resources=run.csv_cost(directory/'resources.csv'),boogie=run.boogie_counts(directory/'program.bpl'),accepted=None,semantic_reviewed=False)
  (directory/'receipt.json').write_text(json.dumps(result,indent=2)+'\n');observations.append(result)
  (ROOT/'measurements/control-observations.json').write_text(json.dumps({'requested_control_calls':6,'observed_calls':len(observations),'results':observations,'complete_trusted_gate':False,'accepted':None,'boundary':'Actual locations, preceding valid obligations and BPL require review. Matching intended exit does not establish semantic acceptance.'},indent=2)+'\n')
