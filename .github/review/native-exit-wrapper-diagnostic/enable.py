from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-exit-wrapper-diagnostic';sha=lambda b:hashlib.sha256(b).hexdigest();sources=list(folder.glob('*.cs'));assert len(sources)==2
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
p=root/'Source/DafnyCore/Verifier/BoogieGenerator.Obligations.cs';raw=p.read_bytes();s=raw.decode('utf-8-sig');a=s.index('  private void CheckExitPostconditions(');b=s.index('  private Bpl.Expr HigherOrderRequirement',a);part=s[a:b];old='    foreach (var ensures in ConjunctsOf(clauses)) {';assert part.count(old)==1;part=part.replace(old,'    var diagnosticStart = builder.Commands.Count;\n'+old);assert part.endswith('    }\n  }\n\n');part=part[:-5]+'    NativeExitWrapperDiagnostic.Capture((codeContext as Declaration)?.Name, builder.Commands.Skip(diagnosticStart));\n  }\n\n';changed=(s[:a]+part+s[b:]).encode();p.write_bytes(changed);record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)}
for name,before,after in [
 ('Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs','          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);','          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeExitWrapperDiagnostic.Apply(translated);\n          return translated;'),
 ('Source/DafnyCore/DafnyCore.csproj','</Project>','  <ItemGroup><Compile Include="../../.github/review/native-exit-wrapper-diagnostic/*.cs" /></ItemGroup>\n</Project>')]:
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1;changed=s.replace(before,after).encode();p.write_bytes(changed);record['files'][name]={'before':sha(raw),'after':sha(changed)}
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
