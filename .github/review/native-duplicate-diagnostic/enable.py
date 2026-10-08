from pathlib import Path
import hashlib,json
root=Path.cwd()
source=root/'.github/review/native-duplicate-diagnostic/NativeDuplicateSupportDiagnostic.cs'
assert source.is_file()
targets={
 'Source/DafnyCore/Verifier/BoogieGenerator.Obligations.cs':(
  '      if (supportAfterPreparation) { builder.Add(leadingSupport); }',
  '      if (supportAfterPreparation && !NativeDuplicateSupportDiagnostic.OmitDuplicate((codeContext as Declaration)?.Name, leadingSupport, normalized)) { builder.Add(leadingSupport); }'),
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':(
  '          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
  '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeDuplicateSupportDiagnostic.Apply(translated);\n          return translated;'),
 'Source/DafnyCore/DafnyCore.csproj':(
  '</Project>',
  '  <ItemGroup><Compile Include="../../.github/review/native-duplicate-diagnostic/NativeDuplicateSupportDiagnostic.cs" /></ItemGroup>\n</Project>')
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
