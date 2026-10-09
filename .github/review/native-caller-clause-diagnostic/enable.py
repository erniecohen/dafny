from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-caller-clause-diagnostic';sha=lambda b:hashlib.sha256(b).hexdigest()
sources=list(folder.glob('*.cs'));assert len(sources)==2
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
targets={
 'Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs':[(
  '      foreach (var requirement in ConjunctsOf(callee.Req)) {',
  '      foreach (var requirement in NativeCallerClauseDiagnostic.Choose(callee.Req, ConjunctsOf(callee.Req))) {')],
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':[(
  '          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
  '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeCallerClauseDiagnostic.Apply(translated);\n          return translated;')],
 'Source/DafnyCore/DafnyCore.csproj':[(
  '</Project>',
  '  <ItemGroup><Compile Include="../../.github/review/native-caller-clause-diagnostic/*.cs" /></ItemGroup>\n</Project>')]}
for name,edits in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig')
 for before,after in edits:assert s.count(before)==1,(name,before);s=s.replace(before,after)
 changed=s.encode('utf-8');p.write_bytes(changed);record['files'][name]={'before':sha(raw),'after':sha(changed)}
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
