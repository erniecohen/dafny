from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-frozen-argument-diagnostic';sha=lambda b:hashlib.sha256(b).hexdigest();sources=list(folder.glob('*.cs'));assert len(sources)==2
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
p=root/'Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs';raw=p.read_bytes();s=raw.decode('utf-8-sig')
changes=[('    var directSubstMap = new Dictionary<IVariable, Expression>();','    var directSubstMap = new Dictionary<IVariable, Expression>();\n    var diagnosticFrozenArguments = NativeFrozenArgumentDiagnostic.Begin((codeContext as Declaration)?.Name, callee.Name);'),
('      builder.Add(cmd);\n      ins.Add(AdaptBoxing','      builder.Add(cmd);\n      NativeFrozenArgumentDiagnostic.Bind(diagnosticFrozenArguments, cmd, builder.Commands);\n      ins.Add(AdaptBoxing'),
('      foreach (var requirement in ConjunctsOf(callee.Req)) {','      NativeFrozenArgumentDiagnostic.Ready(diagnosticFrozenArguments, builder.Commands);\n      foreach (var requirement in ConjunctsOf(callee.Req)) {'),
('            callerProof.Add(Assert(new ForceCheckOrigin(ObligationOrigin(tok, piece.Tok)), piece.E,\n              description, callerProof.Context with { AssertMode = AssertMode.Check }));','            var diagnosticCheck = Assert(new ForceCheckOrigin(ObligationOrigin(tok, piece.Tok)), piece.E,\n              description, callerProof.Context with { AssertMode = AssertMode.Check });\n            NativeFrozenArgumentDiagnostic.Capture(diagnosticFrozenArguments, diagnosticCheck, callerProof.Commands);\n            callerProof.Add(diagnosticCheck);')]
for before,after in changes:assert s.count(before)==1;s=s.replace(before,after)
changed=s.encode();p.write_bytes(changed);record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)}
for name,before,after in [
 ('Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs','          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);','          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeFrozenArgumentDiagnostic.Apply(translated);\n          return translated;'),
 ('Source/DafnyCore/DafnyCore.csproj','</Project>','  <ItemGroup><Compile Include="../../.github/review/native-frozen-argument-diagnostic/*.cs" /></ItemGroup>\n</Project>')]:
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1;changed=s.replace(before,after).encode();p.write_bytes(changed);record['files'][name]={'before':sha(raw),'after':sha(changed)}
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
