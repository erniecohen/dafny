from pathlib import Path
import subprocess,json,hashlib,re
root=Path.cwd();out=root/'results';records=[]
case='dafny0/BoundedPolymorphismCompilation.dfy'
for arm in ['original','repaired','final']:
 p=out/('build-'+arm+'-exit.txt')
 if not p.exists() or p.read_text().strip()!='0':
  records.append({'arm':arm,'skipped':'compiler build did not succeed'});continue
 cwd=root/('before/' if arm!='final' else '')/'Source/IntegrationTests/TestFiles/LitTests/LitTest'
 inputs=(cwd/case).read_bytes();dest=out/arm;dest.mkdir()
 binary=root/'out'/arm/'Dafny';solver=root/'z3-5.1.0-x64-glibc-2.39/bin/z3'
 args=[str(binary),'verify',case,'--type-system-refresh=true','--general-traits=datatype','--solver-path',str(solver),'--resource-limit','16000000','--verification-time-limit','60','--cores','1','--boogie','/normalizeDeclarationOrder:0','--allow-warnings','--bprint',str(dest/'program.bpl'),'--log-format','json;LogFileName='+str(dest/'resources.json')]
 version=subprocess.run([str(binary),'--version'],capture_output=True,text=True)
 try:
  result=subprocess.run(args,cwd=cwd,capture_output=True,text=True,timeout=180)
  exit_code=result.returncode;stdout=result.stdout;stderr=result.stderr
 except subprocess.TimeoutExpired as e:
  exit_code='TIMEOUT';stdout=(e.stdout or b'').decode() if isinstance(e.stdout,bytes) else e.stdout or '';stderr=(e.stderr or b'').decode() if isinstance(e.stderr,bytes) else e.stderr or ''
 (dest/'stdout.txt').write_text(stdout);(dest/'stderr.txt').write_text(stderr)
 records.append({'arm':arm,'input_sha256':hashlib.sha256(inputs).hexdigest(),'compiler_version':version.stdout.strip(),'command':args,'exit':exit_code,'summary':re.findall(r'verifier finished with[^\n]+',stdout+stderr)})
 print(arm,exit_code,stdout[-1800:],stderr[-5000:],flush=True)
(out/'records.json').write_text(json.dumps(records,indent=2)+'\n')
