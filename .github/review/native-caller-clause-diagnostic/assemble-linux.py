from pathlib import Path,PurePosixPath
import hashlib,json,os,shutil,subprocess,tarfile
root=Path.cwd();out=root/'out';revision=next(line.split('=',1)[1] for line in (out/'version.txt').read_text().splitlines() if line.startswith('revision='))
cmd=['dotnet','publish','Source/Dafny/Dafny.csproj','-c','Release','-r','linux-x64','--self-contained','-o','out/rebuilt-linux-x64','-p:GeneratePackageOnBuild=false','-p:SourceRevisionId='+revision]
with (out/'diagnostic-linux.log').open('w') as log:
 result=subprocess.run(cmd,env=dict(os.environ,RUNTIME_IDENTIFIER='linux-x64'),stdout=log,stderr=subprocess.STDOUT)
(out/'diagnostic-linux.exit').write_text(str(result.returncode)+'\n');assert result.returncode==0
base=out/'accepted-base-linux-x64';assert not base.exists();base.mkdir();archive=root/'current-base-artifact/obligation-preservation-build.tar.gz';prefix='out/dafny-linux-x64/';seen=set();sources={}
with tarfile.open(archive) as stream:
 for member in stream:
  path=PurePosixPath(member.name);assert not path.is_absolute() and '..' not in path.parts and member.name not in seen
  seen.add(member.name);assert member.isdir() or member.isfile()
  if member.isfile() and member.name in ['out/source.txt','out/version.txt']:sources[member.name]=stream.extractfile(member).read().decode()
  if not member.isfile() or not member.name.startswith(prefix):continue
  target=base/member.name[len(prefix):];target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(stream.extractfile(member).read());target.chmod(member.mode & 0o777)
assert sources['out/source.txt'].strip()=='63fb750a939396e1e2dc133b6bf71b4a83eb031a'
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest();assert sha(base/'DafnyCore.dll')=='f7ec20a28ad0574b4df2fff232386541fad64082484cff7e7ea6136a421ce7cc'
components=['DafnyCore.dll','DafnyLanguageServer.dll'];assembled=out/'assembled-linux-x64';assert not assembled.exists();shutil.copytree(base,assembled)
for component in components:shutil.copyfile(out/'rebuilt-linux-x64'/component,assembled/component)
for path in assembled.rglob('*'):
 if path.is_file() and path.name not in components:assert sha(path)==sha(base/path.relative_to(assembled)),path
(out/'assembly-provenance-linux.json').write_text(json.dumps(dict(base_build=sources['out/source.txt'].strip(),base_version=sources['out/version.txt'],base_product='df4c7066f8828608400784a220faab8b3b62dd82',replacements=components,base_components_sha256={c:sha(base/c) for c in components},diagnostic_components_sha256={c:sha(assembled/c) for c in components},all_other_binaries_byte_exact=True),indent=2)+'\n')
(out/'assembly-linux.exit').write_text('0\n')
