#!/usr/bin/env python3
"""Pure exact-archive/source checks and extraction; never invokes a compiler."""
import hashlib
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil
import sys
import tarfile
from plan import HERE, digest
from prepare import components

def expected(side):
    record=json.loads((HERE/'reused-compilers.json').read_text())
    spec=json.loads((HERE/'spec.json').read_text())
    if side not in ['baseline','final','candidate'] or set(record['arms'])!={'baseline','final','candidate'}:
        raise ValueError('Exactly the three pinned arms are required')
    item=record['arms'][side]
    product=spec['candidate']['product_revision'] if side=='candidate' else spec['lines']['shipped']['repaired_product' if side=='baseline' else 'final_product']
    if item['identity_record']['line']!='shipped' or item['identity_record']['side']!=side or item['identity_record']['product_revision']!=product:
        raise ValueError('Pinned compiler product/line/side differs')
    if side=='candidate' and item['identity_record'].get('candidate_source')!=spec['candidate']:
        raise ValueError('Candidate exact full/Core/Source/patch identity differs')
    if len(item['files_sha256'])!=329 or len(item['identity_record']['components_sha256'])!=208 or len(item['identity_record']['bundled_libraries_sha256'])!=7:
        raise ValueError('Pinned complete compiler denominator differs')
    return record,item,product

def checked_api(path):
    record=json.loads((HERE/'reused-compilers.json').read_text())
    value=json.loads(path.read_text())
    run=value.get('workflow_run',{})
    if value.get('id')!=record['artifact_id'] or value.get('name')!=record['artifact_name'] or run.get('id')!=record['public_run'] or run.get('head_sha')!=record['build_workflow_head'] or value.get('digest')!=record['artifact_api_digest'] or value.get('size_in_bytes')!=record['artifact_size_in_bytes'] or value.get('expired') is not False:
        raise ValueError('Fresh public artifact API identity/run/digest/availability differs')
    if value.get('url')!='https://api.github.com/repos/erniecohen/dafny/actions/artifacts/'+str(record['artifact_id']):
        raise ValueError('Public artifact repository differs')
    expires=value.get('expires_at')
    if not isinstance(expires,str) or datetime.fromisoformat(expires.replace('Z','+00:00'))<=datetime.now(timezone.utc):
        raise ValueError('Retained artifact is expired or has no expiry provenance')
    return dict(public_run=record['public_run'],artifact_id=record['artifact_id'],api_receipt_sha256=digest(path),api_digest=value['digest'],unexpired=True)

def checked_archive(side,download):
    record,item,product=expected(side)
    checked_api(download/'artifact-api.json')
    source=download/side
    archive=download/item['member']
    if digest(archive)!=item['archive_sha256']:
        raise ValueError('Retained compiler archive hash differs')
    identity=source/'compiler-components.json'
    if digest(identity)!=item['identity_record_sha256'] or json.loads(identity.read_text())!=item['identity_record']:
        raise ValueError('Original retained component receipt differs')
    for name,sha in item['provenance_files_sha256'].items():
        if digest(source/name)!=sha:
            raise ValueError('Original retained provenance bytes differ: '+name)
    if any((source/name).read_text().strip()!='0' for name in item['original_zero_exit_files']):
        raise ValueError('Original required build/setup/identity guard was not successful')
    if (source/'compiler-version.txt').read_text().strip()!=item['actual_version']:
        raise ValueError('Original compiler version differs')
    spec=json.loads((HERE/'spec.json').read_text())
    original=source/item['original_source_record']
    if side=='baseline':
        value=json.loads(original.read_text())
        line=spec['lines']['shipped']
        for source_key,spec_key in [('composed_tree','repaired_tree'),('core_tree','repaired_core_tree'),('driver_tree','repaired_driver_tree'),('patch_sha256','patch_sha256')]:
            if value[source_key]!=line[spec_key]:
                raise ValueError('Original baseline composition differs: '+source_key)
    elif side=='candidate':
        value=json.loads(original.read_text())
        if value['spec']!=spec['candidate'] or value['actual_tree']!=spec['candidate']['composed_tree'] or value['changes']!=spec['candidate']['changed_paths'] or len(value['source_files_sha256'])!=6543:
            raise ValueError('Original candidate composition/source-byte closure differs')
        guard=json.loads((source/'post-build-source-guard.json').read_text())
        if guard!={'source_files':6543,'all_tracked_Source_bytes_unchanged':True}:
            raise ValueError('Original candidate post-build Source guard differs')
    files,modes={},{}
    with tarfile.open(archive,'r:gz') as tf:
        for member in tf.getmembers():
            path=Path(member.name)
            if path.is_absolute() or '..' in path.parts or path.parts[:1]!=('dafny',) or not (member.isfile() or member.isdir()):
                raise ValueError('Unreviewed archive member')
            if member.isdir():continue
            name=path.relative_to('dafny').as_posix()
            if name in files:raise ValueError('Duplicate archive file')
            files[name]=hashlib.sha256(tf.extractfile(member).read()).hexdigest()
            modes[name]=member.mode&0o777
    if files!=item['files_sha256'] or modes!=item['files_mode']:
        raise ValueError('Retained full329file bytes/modes differ')
    if any(files.get(name)!=sha for name,sha in item['identity_record']['components_sha256'].items()) or any(files.get(name)!=sha for name,sha in item['identity_record']['bundled_libraries_sha256'].items()):
        raise ValueError('Archive compiler/library member identity differs')
    return record,item,product,archive,source

def checked_bundle(side,bundle):
    record,item,product=expected(side)
    if bundle.is_symlink() or any(p.is_symlink() for p in bundle.rglob('*')):
        raise ValueError('Extracted compiler contains a link')
    actual={p.relative_to(bundle).as_posix():digest(p) for p in bundle.rglob('*') if p.is_file()}
    modes={p.relative_to(bundle).as_posix():p.stat().st_mode&0o777 for p in bundle.rglob('*') if p.is_file()}
    if actual!=item['files_sha256'] or modes!=item['files_mode']:
        raise ValueError('Extracted complete compiler byte/mode closure differs')
    if components(bundle/'Dafny')!=item['identity_record']['components_sha256']:
        raise ValueError('Actual managed/apphost components differ')
    if {p.relative_to(bundle).as_posix():digest(p) for p in bundle.rglob('*.doo')}!=item['identity_record']['bundled_libraries_sha256']:
        raise ValueError('Actual bundled libraries differ')
    return dict(public_run=record['public_run'],artifact_id=record['artifact_id'],side=side,source_product=product,files=329,components=208,libraries=7,all_actual_files_and_modes_match=True)

def main():
    mode=sys.argv[1]
    if mode=='check-api' and len(sys.argv)==3:
        print(json.dumps(checked_api(Path(sys.argv[2]))));return
    if mode=='check' and len(sys.argv)==4:
        record,item,product,archive,source=checked_archive(sys.argv[2],Path(sys.argv[3]))
        print(json.dumps(dict(checked=True,side=sys.argv[2],source_product=product,files=len(item['files_sha256']),archive_sha256=digest(archive),engine_invoked=False)));return
    if mode=='check-bundle' and len(sys.argv)==5:
        receipt=checked_bundle(sys.argv[2],Path(sys.argv[3]))
        Path(sys.argv[4]).write_text(json.dumps(receipt,indent=2)+'\n')
        print(json.dumps(receipt));return
    if mode!='reuse' or len(sys.argv)!=6:raise ValueError('reuse SIDE DOWNLOAD BUNDLE OUTPUT | check SIDE DOWNLOAD | check-bundle SIDE BUNDLE OUTPUT')
    side=sys.argv[2];download,bundle,output=map(Path,sys.argv[3:6])
    record,item,product,archive,source=checked_archive(side,download)
    if bundle.exists():raise ValueError('Extracted compiler destination must be fresh')
    bundle.parent.mkdir(parents=True,exist_ok=True)
    with tarfile.open(archive,'r:gz') as tf:tf.extractall(bundle.parent)
    checked_bundle(side,bundle)
    output.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(archive,output/'dafny.tar.gz')
    shutil.copyfile(source/'compiler-components.json',output/'compiler-components.json')
    shutil.copyfile(download/'artifact-api.json',output/'artifact-api.json')
    for name in item['provenance_files_sha256']:
        shutil.copyfile(source/name,output/('original-'+name))
    shutil.copyfile(source/item['original_source_record'],output/item['original_source_record'])
    shutil.copyfile(source/'build-exit.txt',output/'build-exit.txt')
    (output/'reuse-source.json').write_text(json.dumps(dict(public_run=record['public_run'],artifact_name=record['artifact_name'],artifact_id=record['artifact_id'],artifact_api_receipt_sha256=digest(download/'artifact-api.json'),artifact_api_digest=record['artifact_api_digest'],unexpired_at_reuse=True,side=side,original_build_receipt_copied=True,new_build_performed=False,source_product=product,archive_sha256=digest(archive),files=329,components=208,libraries=7,boundary='Exact retained public compiler reuse; copied original build exit is provenance, not a new build'),indent=2)+'\n')
    print(json.dumps(dict(reused=True,new_build=False,files=329,components=208,libraries=7)))
if __name__=='__main__':main()
