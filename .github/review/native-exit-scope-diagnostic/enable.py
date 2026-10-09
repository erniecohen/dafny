from pathlib import Path
import hashlib,json
root=Path.cwd(); folder=root/'.github/review/native-exit-scope-diagnostic';sha=lambda b:hashlib.sha256(b).hexdigest()
sources=list(folder.glob('*.cs'));assert len(sources)==2
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
p=root/'Source/DafnyCore/Verifier/BoogieGenerator.Obligations.cs';raw=p.read_bytes();s=raw.decode('utf-8-sig')
start=s.index('  private void CheckExitPostconditions(');end=s.index('  private Bpl.Expr HigherOrderRequirement',start);fragment=s[start:end]
before='    foreach (var ensures in ConjunctsOf(clauses)) {'
after='    var outerExitBuilder = builder;\n    var exitScope = NativeExitScopeDiagnostic.Begin((codeContext as Declaration)?.Name, builder);\n    builder = exitScope.Builder;\n'+before
assert fragment.count(before)==1;fragment=fragment.replace(before,after)
before='        leadingSupport: leadingSupport);';assert fragment.count(before)==1;fragment=fragment.replace(before,before+'\n      NativeExitScopeDiagnostic.Prepared(exitScope, lowering.PurePreparation);')
assert fragment.endswith('    }\n  }\n\n')
fragment=fragment[:-5]+'    NativeExitScopeDiagnostic.Finish(exitScope);\n    if (exitScope.Scoped) { PathAsideBlock(returnOrigin, builder, outerExitBuilder); }\n  }\n\n'
changed=(s[:start]+fragment+s[end:]).encode('utf-8');p.write_bytes(changed);record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)}
targets={
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':('          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);','          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeExitScopeDiagnostic.Apply(translated);\n          return translated;'),
 'Source/DafnyCore/DafnyCore.csproj':('</Project>','  <ItemGroup><Compile Include="../../.github/review/native-exit-scope-diagnostic/*.cs" /></ItemGroup>\n</Project>')}
for name,(before,after) in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1,(name,before);changed=s.replace(before,after).encode('utf-8');p.write_bytes(changed);record['files'][name]={'before':sha(raw),'after':sha(changed)}
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
