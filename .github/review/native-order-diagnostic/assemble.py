"""Assemble one diagnostic component onto the exact preceding public build."""
from pathlib import Path,PurePosixPath
import hashlib,json,shutil,tarfile

root=Path.cwd();archive=root/'base-artifact/obligation-preservation-build.tar.gz'
out=root/'out';base=out/'accepted-base-osx-arm64';assert not base.exists();base.mkdir()
prefix='out/dafny-osx-arm64/';seen=set();sources={}
with tarfile.open(archive) as t:
 for member in t:
  path=PurePosixPath(member.name)
  assert not path.is_absolute() and '..' not in path.parts and member.name not in seen
  seen.add(member.name);assert member.isdir() or member.isfile(),member.name
  if member.isfile() and member.name in ['out/source.txt','out/version.txt']:
   sources[member.name]=t.extractfile(member).read().decode()
  if not member.isfile() or not member.name.startswith(prefix):continue
  target=base/member.name[len(prefix):];target.parent.mkdir(parents=True,exist_ok=True)
  target.write_bytes(t.extractfile(member).read());target.chmod(member.mode & 0o777)
assert sources['out/source.txt'].strip()=='a9971975f98359731aca90bac868f2d7562c9ba6'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(base/'DafnyCore.dll')=='52fd4aeb3eff9d8989a9e261925d7840ca1563872c5ba0139e83fa33211ec462'
component='DafnyLanguageServer.dll';assembled=out/'assembled-osx-arm64';assert not assembled.exists()
shutil.copytree(base,assembled);shutil.copyfile(out/'rebuilt-osx-arm64'/component,assembled/component)
for p in assembled.rglob('*'):
 if p.is_file() and p.name!=component:assert sha(p)==sha(base/p.relative_to(assembled)),p
(out/'assembly-provenance.json').write_text(json.dumps(dict(base_build=sources['out/source.txt'].strip(),base_version=sources['out/version.txt'],base_product='a709df226cab0d1073bc1fb52feeddde8145d795',replacement=component,base_component_sha256=sha(base/component),diagnostic_component_sha256=sha(assembled/component),core_sha256=sha(assembled/'DafnyCore.dll'),all_other_binaries_byte_exact=True),indent=2)+'\n')
