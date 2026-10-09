from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-caller-preparation-diagnostic'
sources=list(folder.glob('*.cs'));assert len(sources)==2
sha=lambda data:hashlib.sha256(data).hexdigest()
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
targets={
 'Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs':(
  'var lowering = LowerDeclaredProposition(instantiated, builder, locals, callEtran);',
  'var lowering = LowerDeclaredProposition(instantiated, builder, locals, callEtran, omitPreparationForDiagnostic: NativeCallerPreparationDiagnostic.Omit((codeContext as Declaration)?.Name));'),
 'Source/DafnyCore/Verifier/BoogieGenerator.Obligations.cs':[
  ('Bpl.Expr preparationGuard = null, Bpl.AssumeCmd leadingSupport = null)',
   'Bpl.Expr preparationGuard = null, Bpl.AssumeCmd leadingSupport = null, bool omitPreparationForDiagnostic = false)'),
  ('      builder.AppendAlreadyTranslated(preparation, normalized);',
   '      NativeCallerPreparationDiagnostic.Prepared(preparation.Commands, normalized, argumentTemporaries, omitPreparationForDiagnostic);\n      if (!omitPreparationForDiagnostic) { builder.AppendAlreadyTranslated(preparation, normalized); }')],
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':(
  '          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
  '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeCallerPreparationDiagnostic.Apply(translated);\n          return translated;'),
 'Source/DafnyCore/DafnyCore.csproj':(
  '</Project>',
  '  <ItemGroup><Compile Include="../../.github/review/native-caller-preparation-diagnostic/*.cs" /></ItemGroup>\n</Project>')}

for name,edits in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig')
 if isinstance(edits,tuple):edits=[edits]
 for before,after in edits:
  assert s.count(before)==1,(name,before);s=s.replace(before,after)
 changed=s.encode('utf-8');record['files'][name]={'before':sha(raw),'after':sha(changed)};p.write_bytes(changed)
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
