#!/usr/bin/env python3
"""Identity checks only. Does not invoke a compiler or solver."""
import argparse,hashlib,json,os,stat,subprocess,tarfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def digest(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def need(v,message):
 if not v:raise ValueError(message)
def save(p,v):
 Path(p).parent.mkdir(parents=True,exist_ok=True);Path(p).write_text(json.dumps(v,indent=2,sort_keys=True)+'\n')
def source():
 rows={}
 raw=subprocess.check_output(['git','ls-tree','-r','-z','HEAD','Source'],cwd=ROOT)
 for entry in raw.split(b'\0'):
  if not entry:continue
  attrs,name=entry.split(b'\t',1);mode,kind,blob=attrs.split();rel=name.decode();p=ROOT/rel
  need(kind==b'blob' and mode in [b'100644',b'100755',b'120000'],'Unreviewed Source entry')
  if mode==b'120000':
   need(p.is_symlink(),'Source link changed');data=os.fsencode(os.readlink(p))
  else:
   need(p.is_file() and not p.is_symlink(),'Source regular file changed');data=p.read_bytes()
   need(bool(p.stat().st_mode & 0o111)==(mode==b'100755'),'Source executable mode changed')
  raw_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
  clean_blob=raw_blob if raw_blob==blob.decode() else subprocess.check_output(['git','hash-object','--path',rel,'--stdin'],cwd=ROOT,input=data).decode().strip()
  need(clean_blob==blob.decode(),'Tracked Source bytes changed: '+rel)
  rows[rel]={'git_mode':mode.decode(),'git_blob':blob.decode(),'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'checkout_clean_filter_applied':raw_blob!=clean_blob}
 need(len(rows)==6543,'Source denominator changed')
 return {'source_tree':subprocess.check_output(['git','rev-parse','HEAD:Source'],cwd=ROOT,text=True).strip(),'count':len(rows),'tracked_entries':rows,'boundary':'All tracked Source bytes/modes checked under repository Git clean filters, with actual checkout SHA256 retained separately. Intentional symlink target literals are hashed without following links. Generated untracked build files are outside this guard.'}
def before(root,bundle):
 root,bundle=Path(root),Path(bundle)
 receipt=root/'compiler-source-binding.json'
 need(digest(receipt)=='482b0e4508daf78e2ed72aa70a42b7bc1d09bce5a9e816cfafd1e3b974c62b45','Original public source binding changed')
 old=json.loads(receipt.read_text())
 spec=json.loads((ROOT/'.github/review/issue113-benchmark-source-identity.json').read_text())
 need(old['scratch_source_sha']=='ec224fdbc8c5bcba488ba92f1a387f737b555d42' and old['product_commit']=='4e50e86cde6ab2ef6cab0762ffc4df59d646dc15' and old['source_tree']=='9c2bb48a778ee2abd6ef10f8a0d72f7843a16af5','Original public source identities changed')
 need(old['actual_version']=='4.11.0+fcb2042d.review.c9c7622d','Original version changed')
 need(old['benchmark_files_sha256']==spec['benchmark_files_sha256'],'Original benchmark bytes changed')
 current=json.loads((ROOT/'.github/review/issue113-benchmark-product-inputs.json').read_text())['files']
 need(set(current)==set(old['product_files_sha256']) and [n for n in current if current[n]!=old['product_files_sha256'][n]]==['Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs'],'Before/after product inputs differ beyond reviewed one-file candidate')
 need(digest(root/'dafny.tar.gz')==old['archive_sha256']=='68f933cc25877a70e344610994b870b05939404006062237c820e7595be443b0','Original archive changed')
 actual={p.relative_to(bundle).as_posix():digest(p) for p in bundle.rglob('*') if p.is_file()}
 need(not any(p.is_symlink() for p in bundle.rglob('*')) and actual==old['all_published_files_sha256'] and len(actual)==329,'Original full compiler closure changed')
 with tarfile.open(root/'dafny.tar.gz') as tf:
  entries=[e for e in tf if e.isfile()]
  need(len(entries)==len(actual) and {e.name.removeprefix('dafny/') for e in entries}==set(actual),'Original archive file closure changed')
  for e in entries:need((bundle/e.name.removeprefix('dafny/')).stat().st_mode & 0o777==e.mode & 0o777,'Original compiler member mode changed')
 return {'all_original_compiler_bytes_and_modes_match':True,'published_count':len(actual),'source_binding_sha256':digest(receipt),'original_product_commit':old['product_commit'],'original_version':old['actual_version'],'archive_sha256':old['archive_sha256'],'all_published_files_sha256':actual,'product_changed_paths':['Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs']}
def main():
 p=argparse.ArgumentParser();p.add_argument('kind',choices=['source','before']);p.add_argument('--output',required=True);p.add_argument('--compiler-root');p.add_argument('--bundle-root');a=p.parse_args()
 save(a.output,source() if a.kind=='source' else before(a.compiler_root,a.bundle_root))
if __name__=='__main__':main()
