from pathlib import Path
import hashlib,json,re
root=Path.cwd();folder=root/'.github/review/native-caller-placement-diagnostic'
sources=list(folder.glob('*.cs'));assert len(sources)==2
sha=lambda data:hashlib.sha256(data).hexdigest()
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
flag='options.Get(CommonOptionBag.ConsistentObligationChecks)'
# Couple the local proof and USER-DEFINED procedure requirement only.
call_sites={
 'Statements/BoogieGenerator.TrCall.cs':['if ('+flag+' && !call.IsFree)'],
 'BoogieGenerator.Methods.cs':[
  'var locallyChecked = '+flag+' &&\n                  kind is MethodTranslationKind.Call or MethodTranslationKind.CoCall;'
 ]
}

verifier=root/'Source/DafnyCore/Verifier'
expression=re.compile(r'(?<![A-Za-z0-9_])(?:(?:this|generator)\.)?options\.Get\(CommonOptionBag\.ConsistentObligationChecks\)')
call_count=0;other_count=0
for p in sorted(verifier.rglob('*.cs')):
 raw=p.read_bytes();s=raw.decode('utf-8-sig');relative=str(p.relative_to(verifier))
 for site in call_sites.get(relative,[]):
  assert s.count(site)==1,(relative,site)
  s=s.replace(site,site.replace(flag,'CALL_PREPARE_GATE' if relative.endswith('TrCall.cs') else 'CALL_CHECK_GATE'));call_count+=1
 def wrap(m):
  global other_count
  other_count+=1;return 'NativeCallerPlacementDiagnostic.Enabled("other", '+m.group()+')'
 s=expression.sub(wrap,s)
 s=s.replace('CALL_PREPARE_GATE','NativeCallerPlacementDiagnostic.Enabled("prepare", '+flag+')').replace('CALL_CHECK_GATE','NativeCallerPlacementDiagnostic.Enabled("check", '+flag+')')
 if s!=raw.decode('utf-8-sig'):
  changed=s.encode('utf-8');record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)};p.write_bytes(changed)
assert call_count==2 and other_count>30,(call_count,other_count)
record['call_gates']=call_count;record['other_gates']=other_count
p=verifier/'Statements/BoogieGenerator.TrCall.cs';raw=p.read_bytes();s=raw.decode('utf-8-sig')
before='            builder.Add(Assert(new ForceCheckOrigin(ObligationOrigin(tok, piece.Tok)), piece.E,\n              description, builder.Context with { AssertMode = AssertMode.Check }));'
after='            var preparedCheck = Assert(new ForceCheckOrigin(ObligationOrigin(tok, piece.Tok)), piece.E,\n              description, builder.Context with { AssertMode = AssertMode.Check });\n            if (NativeCallerPlacementDiagnostic.EmitCheck((codeContext as Declaration)?.Name, call, preparedCheck)) { builder.Add(preparedCheck); }'
assert s.count(before)==1;s=s.replace(before,after)
assert s.count('          builder.Add(summary);')==1
s=s.replace('          builder.Add(summary);','          if (NativeCallerPlacementDiagnostic.EmitSummary((codeContext as Declaration)?.Name, summary)) { builder.Add(summary); }')
changed=s.encode('utf-8');record['files'][str(p.relative_to(root))]['after']=sha(changed);p.write_bytes(changed)
targets={
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':(
  '          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
  '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeCallerPlacementDiagnostic.Apply(translated);\n          return translated;'),
 'Source/DafnyCore/DafnyCore.csproj':(
  '</Project>',
  '  <ItemGroup><Compile Include="../../.github/review/native-caller-placement-diagnostic/*.cs" /></ItemGroup>\n</Project>')}
for name,(before,after) in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1,name
 changed=s.replace(before,after).encode('utf-8');record['files'][name]={'before':sha(raw),'after':sha(changed)};p.write_bytes(changed)
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
