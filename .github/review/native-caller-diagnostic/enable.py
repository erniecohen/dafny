from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-caller-diagnostic'
sources=list(folder.glob('*.cs'));assert len(sources)==2
sha=lambda data:hashlib.sha256(data).hexdigest()
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
targets={
 'Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs':(
  '        builder.Add(TrAssumeCmd(tok, callEtran.CanCallAssumptionForVerification(instantiated)));\n        var lowering = LowerDeclaredProposition(instantiated, builder, locals, callEtran);',
  '        var callerSupport = TrAssumeCmd(tok, callEtran.CanCallAssumptionForVerification(instantiated));\n        var orderedCaller = NativeCallerDiagnostic.Order((codeContext as Declaration)?.Name, callerSupport);\n        if (!orderedCaller) { builder.Add(callerSupport); }\n        var lowering = LowerDeclaredProposition(instantiated, builder, locals, callEtran, leadingSupport: orderedCaller ? callerSupport : null);'),
 'Source/DafnyCore/Verifier/BoogieGenerator.Obligations.cs':(
  '      if (leadingSupport != null && !supportAfterPreparation) { builder.Add(leadingSupport); }',
  '      NativeCallerDiagnostic.Prepared(leadingSupport, supportAfterPreparation);\n      if (leadingSupport != null && !supportAfterPreparation) { builder.Add(leadingSupport); }'),
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':(
  '          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
  '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeCallerDiagnostic.Apply(translated);\n          return translated;'),
 'Source/DafnyCore/DafnyCore.csproj':(
  '</Project>',
  '  <ItemGroup><Compile Include="../../.github/review/native-caller-diagnostic/*.cs" /></ItemGroup>\n</Project>')}
for name,(before,after) in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1,name
 changed=s.replace(before,after).encode('utf-8');record['files'][name]={'before':sha(raw),'after':sha(changed)};p.write_bytes(changed)
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
