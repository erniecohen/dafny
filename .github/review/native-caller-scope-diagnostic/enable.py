from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-caller-scope-diagnostic'
sources=list(folder.glob('*.cs'));assert len(sources)==2
sha=lambda b:hashlib.sha256(b).hexdigest()
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
p=root/'Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs';raw=p.read_bytes();s=raw.decode('utf-8-sig')
start=s.index('    if (options.Get(CommonOptionBag.ConsistentObligationChecks) && !call.IsFree) {')
end=s.index('    builder.Add(call);',start)
fragment=s[start:end]
fragment=fragment.replace('      var callEtran', '      var scope = NativeCallerScopeDiagnostic.Begin((codeContext as Declaration)?.Name, call, builder);\n      var callerBuilder = scope.Builder;\n      var callEtran',1)
fragment=fragment.replace('builder.Add(', 'callerBuilder.Add(').replace('LowerDeclaredProposition(instantiated, builder, locals, callEtran)', 'LowerDeclaredProposition(instantiated, callerBuilder, locals, callEtran, callerForDiagnostic: true)').replace('builder.Context', 'callerBuilder.Context')
before='            callerBuilder.Add(Assert(new ForceCheckOrigin(ObligationOrigin(tok, piece.Tok)), piece.E,\n              description, callerBuilder.Context with { AssertMode = AssertMode.Check }));'
after='            var actualCheck = Assert(new ForceCheckOrigin(ObligationOrigin(tok, piece.Tok)), piece.E,\n              description, callerBuilder.Context with { AssertMode = AssertMode.Check });\n            NativeCallerScopeDiagnostic.Check(scope, actualCheck);\n            callerBuilder.Add(actualCheck);'
assert fragment.count(before)==1;fragment=fragment.replace(before,after)
assert fragment.endswith('    }\n')
fragment=fragment[:-6]+'''      NativeCallerScopeDiagnostic.Finish(scope);
      if (scope.Scoped) {
        if (scope.Negative) { callerBuilder.Add(NativeCallerScopeDiagnostic.Negative(scope)); }
        PathAsideBlock(tok, callerBuilder, builder);
        foreach (var fact in NativeCallerScopeDiagnostic.Publications(scope)) { builder.Add(fact); }
      }
    }
'''
changed=(s[:start]+fragment+s[end:]).encode('utf-8');p.write_bytes(changed);record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)}
targets={
 'Source/DafnyCore/Verifier/BoogieGenerator.Obligations.cs':[
  ('Bpl.Expr preparationGuard = null, Bpl.AssumeCmd leadingSupport = null)', 'Bpl.Expr preparationGuard = null, Bpl.AssumeCmd leadingSupport = null, bool callerForDiagnostic = false)'),
  ('      var normalized = CertifiedContractPreparation.Normalize(preparation.Commands, argumentTemporaries);',
   '      var normalized = CertifiedContractPreparation.Normalize(preparation.Commands, argumentTemporaries);\n      if (callerForDiagnostic) { NativeCallerScopeDiagnostic.Prepared((codeContext as Declaration)?.Name, preparation.Commands, normalized, argumentTemporaries); }')],
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':[
  ('          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
   '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeCallerScopeDiagnostic.Apply(translated);\n          return translated;')],
 'Source/DafnyCore/DafnyCore.csproj':[
  ('</Project>', '  <ItemGroup><Compile Include="../../.github/review/native-caller-scope-diagnostic/*.cs" /></ItemGroup>\n</Project>')]}
for name,edits in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig')
 for before,after in edits:
  assert s.count(before)==1,(name,before);s=s.replace(before,after)
 changed=s.encode('utf-8');p.write_bytes(changed);record['files'][name]={'before':sha(raw),'after':sha(changed)}
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
