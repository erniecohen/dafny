#!/usr/bin/env python3
"""Pure checked composition of one proposed source patch; never builds or proves."""
import json,subprocess,sys
from pathlib import Path
from plan import HERE,digest
from prepare import git
root,output=map(Path,sys.argv[1:3]);spec=json.loads((HERE/'spec.json').read_text())['candidate'];patch=HERE/spec['patch_file']
if len(sys.argv)>3:
    if sys.argv[3]!='--check-existing':raise SystemExit('Unknown source guard mode')
    record=json.loads(output.read_text())
    if record['spec']!=spec:raise SystemExit('Source specification changed')
    changed=[path for path,value in record['source_files_sha256'].items() if digest(root/path)!=value]
    if changed:raise SystemExit('Tracked Source bytes changed during build: '+str(changed))
    guard=output.with_name('post-build-source-guard.json')
    guard.write_text(json.dumps(dict(source_files=len(record['source_files_sha256']),all_tracked_Source_bytes_unchanged=True),indent=2)+'\n')
    raise SystemExit(0)
if git(root,'rev-parse','HEAD')!=spec['base_product'] or git(root,'status','--porcelain'):raise SystemExit('Candidate needs exact clean base')
if digest(patch)!=spec['patch_sha256']:raise SystemExit('Candidate source patch hash differs')
paths=[line.split('\t',2)[2] for line in subprocess.check_output(['git','-C',str(root),'apply','--numstat',str(patch.resolve())],text=True).splitlines()]
if paths!=spec['changed_paths']:raise SystemExit('Candidate source scope differs')
subprocess.run(['git','-C',str(root),'apply','--index',str(patch.resolve())],check=True)
tree=git(root,'write-tree')
if tree!=spec['composed_tree'] or git(root,'rev-parse',tree+':Source')!=spec['source_tree'] or git(root,'rev-parse',tree+':Source/DafnyCore')!=spec['core_tree']:raise SystemExit('Candidate resulting full/Core tree differs')
source_files=subprocess.check_output(['git','-C',str(root),'ls-files','-z','--','Source']).decode().split('\0')
source_hashes={path:digest(root/path) for path in source_files if path}
output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(dict(spec=spec,actual_tree=tree,changes=paths,source_files_sha256=source_hashes),indent=2)+'\n')
print(json.dumps(dict(tree=tree,core_tree=spec['core_tree'],source_revision_id=spec['source_revision_id'])))
