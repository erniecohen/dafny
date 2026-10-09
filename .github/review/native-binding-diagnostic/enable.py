from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-binding-diagnostic'
sources=list(folder.glob('*.cs'));assert len(sources)==2
targets={
 'Source/DafnyCore/Verifier/BoogieGenerator.Obligations.cs':(
  '      var normalized = CertifiedContractPreparation.Normalize(preparation.Commands, argumentTemporaries);',
  '      var normalized = CertifiedContractPreparation.Normalize(preparation.Commands, argumentTemporaries);\n      if (!ReferenceEquals(normalized, preparation.Commands)) { normalized = NativeBindingDiagnostic.Rewrite((codeContext as Declaration)?.Name, normalized, argumentTemporaries); }'),
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':(
  '          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
  '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeBindingDiagnostic.Apply(translated);\n          return translated;'),
 'Source/DafnyCore/DafnyCore.csproj':(
  '</Project>',
  '  <ItemGroup><Compile Include="../../.github/review/native-binding-diagnostic/*.cs" /></ItemGroup>\n</Project>')}
sha=lambda data:hashlib.sha256(data).hexdigest()
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
for name,(before,after) in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1,name
 changed=s.replace(before,after).encode('utf-8');record['files'][name]={'before':sha(raw),'after':sha(changed)};p.write_bytes(changed)
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
