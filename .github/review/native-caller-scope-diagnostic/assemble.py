"""Assemble one diagnostic component onto the exact preceding public build."""
from pathlib import Path,PurePosixPath
import hashlib,json,shutil,tarfile,subprocess

root=Path.cwd(); current=root/'current-base-artifact';current.mkdir()
subprocess.run(['gh','run','download','37955030278','-R','erniecohen/dafny','-n','obligation-preservation-build','-D',str(current)],check=True)
archive=current/'obligation-preservation-build.tar.gz'
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
assert sources['out/source.txt'].strip()=='63fb750a939396e1e2dc133b6bf71b4a83eb031a'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(base/'DafnyCore.dll')=='a4637dc6479077323e9b0228331edf923026209c0e0464a4ad5aa4d71e3dc144'
components=['DafnyCore.dll','DafnyLanguageServer.dll'];assembled=out/'assembled-osx-arm64';assert not assembled.exists()
shutil.copytree(base,assembled)
for component in components:shutil.copyfile(out/'rebuilt-osx-arm64'/component,assembled/component)
for p in assembled.rglob('*'):
 if p.is_file() and p.name not in components:assert sha(p)==sha(base/p.relative_to(assembled)),p
(out/'assembly-provenance.json').write_text(json.dumps(dict(base_build=sources['out/source.txt'].strip(),base_version=sources['out/version.txt'],base_product='df4c7066f8828608400784a220faab8b3b62dd82',replacements=components,base_components_sha256={c:sha(base/c) for c in components},diagnostic_components_sha256={c:sha(assembled/c) for c in components},all_other_binaries_byte_exact=True),indent=2)+'\n')
