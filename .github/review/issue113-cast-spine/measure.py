#!/usr/bin/env python3
"""Two-arm focused depth commands; original runner and caps unchanged."""
import json,os,subprocess
from pathlib import Path
ROOT=Path.cwd();HERE=Path(__file__).resolve().parent
stage=os.environ['STAGE'];need=stage in ['translate','verify']
if not need:raise ValueError('Only focused stages allowed')
steps=[]
for arm in ['before','after']:
 compiler_root=ROOT/('before-compiler' if arm=='before' else 'compiler')
 version=(compiler_root/'compiler-version.txt').read_text().strip()
 compiler_source_sha=json.loads((compiler_root/'compiler-source-binding.json').read_text())['scratch_source_sha']
 destination=ROOT/'measurements'/arm/stage;destination.mkdir(parents=True)
 command=['python3',str(ROOT/'.github/review/issue113-benchmarks/run.py'),str(ROOT/'.github/review/issue113-benchmarks/fixtures/manifest.json'),str(destination),'--dafny',str(ROOT/'out'/arm/'dafny/Dafny'),'--expected-dafny',version,'--solver',os.environ['SOLVER'],'--solver-version','5.1.0','--solver-seed','0','--source-sha',compiler_source_sha,'--executor','github-ubuntu-24.04','--execution-environment','public-ci','--select','depth-1/N,depth-10/N,depth-100/N,depth-1000/N,depth-1000/B','--stage',stage,'--samples','3']
 result=subprocess.run(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 (ROOT/'measurements'/f'{arm}-{stage}-runner.txt').write_bytes(result.stdout)
 steps.append({'arm':arm,'stage':stage,'command':command,'exit':result.returncode,'measurement_checkout_sha':os.environ['GITHUB_SHA'],'compiler_source_sha':compiler_source_sha})
 (ROOT/'measurements/stages.json').write_text(json.dumps(steps,indent=2)+'\n')
 print(arm,stage,result.returncode,result.stdout[-200:].decode(errors='replace'))
