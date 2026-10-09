from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-exit-allocation-diagnostic';sha=lambda b:hashlib.sha256(b).hexdigest()
sources=list(folder.glob('*.cs'));assert len(sources)==2
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
p=root/'Source/DafnyCore/Verifier/BoogieGenerator.Obligations.cs';raw=p.read_bytes();s=raw.decode('utf-8-sig')
before='      builder.AppendAlreadyTranslated(preparation, normalized);';assert s.count(before)==1;s=s.replace(before,'      builder.AppendAlreadyTranslated(preparation, NativeExitAllocationDiagnostic.Filter(normalized, argumentTemporaries, checking.HeapExpr));')
start=s.index('  private void CheckExitPostconditions(');end=s.index('  private Bpl.Expr HigherOrderRequirement',start);f=s[start:end]
before='    foreach (var ensures in ConjunctsOf(clauses)) {';assert f.count(before)==1;f=f.replace(before,'    var exitDiagnostic = NativeExitAllocationDiagnostic.Begin((codeContext as Declaration)?.Name, builder);\n'+before)
before='        leadingSupport: leadingSupport);';assert f.count(before)==1;f=f.replace(before,before+'\n      NativeExitAllocationDiagnostic.Prepared(exitDiagnostic, lowering.PurePreparation);')
assert f.endswith('    }\n  }\n\n');f=f[:-5]+'    NativeExitAllocationDiagnostic.Finish(exitDiagnostic);\n  }\n\n'
changed=(s[:start]+f+s[end:]).encode();p.write_bytes(changed);record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)}
for name,before,after in [
 ('Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs','          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);','          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeExitAllocationDiagnostic.Apply(translated);\n          return translated;'),
 ('Source/DafnyCore/DafnyCore.csproj','</Project>','  <ItemGroup><Compile Include="../../.github/review/native-exit-allocation-diagnostic/*.cs" /></ItemGroup>\n</Project>')]:
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1;changed=s.replace(before,after).encode();p.write_bytes(changed);record['files'][name]={'before':sha(raw),'after':sha(changed)}
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
