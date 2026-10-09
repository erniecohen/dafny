from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-interface-copy-diagnostic';sha=lambda b:hashlib.sha256(b).hexdigest()
sources=list(folder.glob('*.cs'));assert len(sources)==2
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
targets={
 'Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs':[
  ('        sink.AddTopLevelDeclaration(AddMethod(m, MethodTranslationKind.Call));\n        if (options.Get(CommonOptionBag.ConsistentObligationChecks)) {\n          sink.AddTopLevelDeclaration(AddMethod(m, MethodTranslationKind.Call, publishCheckedRequires: true));\n        }',
   '        var originalCall = AddMethod(m, MethodTranslationKind.Call);\n        sink.AddTopLevelDeclaration(originalCall);\n        if (options.Get(CommonOptionBag.ConsistentObligationChecks)) {\n          sink.AddTopLevelDeclaration(NativeInterfaceCopyDiagnostic.Enabled\n            ? NativeInterfaceCopyDiagnostic.Copy(originalCall, proofDependencies)\n            : AddMethod(m, MethodTranslationKind.Call, publishCheckedRequires: true));\n        }'),
  ('        sink.AddTopLevelDeclaration(AddMethod(m, MethodTranslationKind.CoCall));\n        if (options.Get(CommonOptionBag.ConsistentObligationChecks)) {\n          sink.AddTopLevelDeclaration(AddMethod(m, MethodTranslationKind.CoCall, publishCheckedRequires: true));\n        }',
   '        var originalCoCall = AddMethod(m, MethodTranslationKind.CoCall);\n        sink.AddTopLevelDeclaration(originalCoCall);\n        if (options.Get(CommonOptionBag.ConsistentObligationChecks)) {\n          sink.AddTopLevelDeclaration(NativeInterfaceCopyDiagnostic.Enabled\n            ? NativeInterfaceCopyDiagnostic.Copy(originalCoCall, proofDependencies)\n            : AddMethod(m, MethodTranslationKind.CoCall, publishCheckedRequires: true));\n        }'),
  ('                if (publishCheckedRequires && locallyChecked) {','                NativeInterfaceCopyDiagnostic.User(requirement, locallyChecked);\n                if (publishCheckedRequires && locallyChecked) {')],
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':[
  ('          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
   '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeInterfaceCopyDiagnostic.Apply(translated);\n          return translated;')],
 'Source/DafnyCore/DafnyCore.csproj':[
  ('</Project>','  <ItemGroup><Compile Include="../../.github/review/native-interface-copy-diagnostic/*.cs" /></ItemGroup>\n</Project>')]}
for name,edits in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig')
 for before,after in edits:assert s.count(before)==1,(name,before);s=s.replace(before,after)
 changed=s.encode('utf-8');p.write_bytes(changed);record['files'][name]={'before':sha(raw),'after':sha(changed)}
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
