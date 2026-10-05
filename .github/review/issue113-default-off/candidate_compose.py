#!/usr/bin/env python3
"""Pure checked composition of one proposed source patch; never builds or proves."""
import json,subprocess,sys
from pathlib import Path
from plan import HERE,digest
from prepare import git
root,output=map(Path,sys.argv[1:3]);spec=json.loads((HERE/'spec.json').read_text())['candidate'];patch=HERE/spec['patch_file']
if git(root,'rev-parse','HEAD')!=spec['base_product'] or git(root,'status','--porcelain'):raise SystemExit('Candidate needs exact clean base')
if digest(patch)!=spec['patch_sha256']:raise SystemExit('Candidate source patch hash differs')
paths=[line.split('\t',2)[2] for line in subprocess.check_output(['git','-C',str(root),'apply','--numstat',str(patch.resolve())],text=True).splitlines()]
if paths!=spec['changed_paths']:raise SystemExit('Candidate source scope differs')
subprocess.run(['git','-C',str(root),'apply','--index',str(patch.resolve())],check=True)
tree=git(root,'write-tree')
if tree!=spec['composed_tree'] or git(root,'rev-parse',tree+':Source/DafnyCore')!=spec['core_tree']:raise SystemExit('Candidate resulting full/Core tree differs')
output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(dict(spec=spec,actual_tree=tree,changes=paths),indent=2)+'\n')
print(json.dumps(dict(tree=tree,core_tree=spec['core_tree'],source_revision_id=spec['source_revision_id'])))
