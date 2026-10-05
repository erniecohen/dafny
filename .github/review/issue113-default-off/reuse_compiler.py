#!/usr/bin/env python3
"""Pure checks of two exact public archives; --version is a separate CI step."""
import hashlib,json,shutil,sys,tarfile
from pathlib import Path
from plan import HERE,digest
from prepare import components
side,download,bundle,output=sys.argv[1:5];download,bundle,output=map(Path,[download,bundle,output])
record=json.loads((HERE/'reused-compilers.json').read_text());expected=record['arms'][side];spec=json.loads((HERE/'spec.json').read_text())['lines']['shipped'];source=download/side
if side not in ['baseline','final']:raise SystemExit('Only exact reference archives may be reused')
archive=download/expected['member']
if digest(archive)!=expected['archive_sha256']:raise SystemExit('Public retained compiler archive differs')
identity=source/'compiler-components.json'
if digest(identity)!=expected['identity_record_sha256'] or json.loads(identity.read_text())!=expected['identity_record']:raise SystemExit('Original public component identity receipt differs')
product=spec['repaired_product' if side=='baseline' else 'final_product']
if expected['identity_record']['product_revision']!=product:raise SystemExit('Reused source product differs')
original_source=source/expected['original_source_record']
if digest(original_source)!=expected['original_source_record_sha256']:raise SystemExit('Original source receipt differs')
if side=='baseline':
 original=json.loads(original_source.read_text())
 if original['composed_tree']!=spec['repaired_tree'] or original['core_tree']!=spec['repaired_core_tree'] or original['driver_tree']!=spec['repaired_driver_tree'] or original['patch_sha256']!=spec['patch_sha256']:raise SystemExit('Reused baseline original full/Core/Driver composition differs')
if digest(source/'build-exit.txt')!=expected['original_build_exit_sha256'] or (source/'build-exit.txt').read_text().strip()!='0':raise SystemExit('Original public build receipt differs')
if bundle.exists():raise SystemExit('Extracted compiler destination must be fresh')
bundle.parent.mkdir(parents=True,exist_ok=True)
with tarfile.open(archive,'r:gz') as tf:
 for member in tf.getmembers():
  path=Path(member.name)
  if path.is_absolute() or '..' in path.parts or path.parts[:1]!=('dafny',) or not (member.isfile() or member.isdir()):raise SystemExit('Unreviewed archive member')
 tf.extractall(bundle.parent)
actual={p.relative_to(bundle).as_posix():digest(p) for p in bundle.rglob('*') if p.is_file()}
if actual!=expected['files_sha256'] or any(p.is_symlink() for p in bundle.rglob('*')):raise SystemExit('Full329file extracted compiler closure differs')
if components(bundle/'Dafny')!=expected['identity_record']['components_sha256']:raise SystemExit('Actual managed/apphost compiler components differ')
libraries={p.relative_to(bundle).as_posix():digest(p) for p in bundle.rglob('*.doo')}
if libraries!=expected['identity_record']['bundled_libraries_sha256']:raise SystemExit('Actual bundled libraries differ')
output.mkdir(parents=True,exist_ok=True)
shutil.copyfile(archive,output/'dafny.tar.gz');shutil.copyfile(identity,output/'compiler-components.json');shutil.copyfile(original_source,output/expected['original_source_record']);shutil.copyfile(source/'build-exit.txt',output/'build-exit.txt')
(output/'reuse-source.json').write_text(json.dumps(dict(public_run=record['public_run'],artifact_name=record['artifact_name'],artifact_id=record['artifact_id'],side=side,original_build_receipt_copied=True,new_build_performed=False,source_product=product,archive_sha256=digest(archive),files=len(actual),components=len(expected['identity_record']['components_sha256']),libraries=len(libraries),boundary='Exact retained public compiler reuse; original build exit is copied provenance, not a new build'),indent=2)+'\n')
print(json.dumps(dict(reused=True,new_build=False,files=len(actual),components=len(expected['identity_record']['components_sha256']))))
