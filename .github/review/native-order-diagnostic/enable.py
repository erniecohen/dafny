"""Enable scratch-only native AST instrumentation after the pristine build."""
import hashlib,json
from pathlib import Path

root=Path.cwd()
source=root/'.github/review/native-order-diagnostic/NativeSupportOrderDiagnostic.cs'
assert source.is_file()
targets={
 'Source/DafnyDriver/Legacy/SynchronousCliCompilation.cs':(
  '      foreach (var prog in BoogieGenerator.Translate(dafnyProgram, dafnyProgram.Reporter)) {',
  '      foreach (var prog in BoogieGenerator.Translate(dafnyProgram, dafnyProgram.Reporter)) {\n        NativeSupportOrderDiagnostic.Apply(prog.Item2);'),
 'Source/DafnyDriver/DafnyDriver.csproj':(
  '</Project>',
  '  <ItemGroup><Compile Include="../../.github/review/native-order-diagnostic/NativeSupportOrderDiagnostic.cs" /></ItemGroup>\n</Project>')
}
sha=lambda data:hashlib.sha256(data).hexdigest()
record={'instrumentation':sha(source.read_bytes()),'files':{}}
for name,(before,after) in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig')
 assert s.count(before)==1,name
 changed=s.replace(before,after).encode('utf-8')
 record['files'][name]={'before':sha(raw),'after':sha(changed)}
 p.write_bytes(changed)
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
