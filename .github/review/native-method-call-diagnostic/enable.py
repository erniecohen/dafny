from pathlib import Path
import hashlib,json,re
root=Path.cwd();folder=root/'.github/review/native-method-call-diagnostic'
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
  s=s.replace(site,site.replace(flag,'CALL_FAMILY_GATE'));call_count+=1
 def wrap(m):
  global other_count
  other_count+=1;return 'NativeMethodCallDiagnostic.Enabled("other", '+m.group()+')'
 s=expression.sub(wrap,s)
 s=s.replace('CALL_FAMILY_GATE','NativeMethodCallDiagnostic.Enabled("call", '+flag+')')
 if s!=raw.decode('utf-8-sig'):
  changed=s.encode('utf-8');record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)};p.write_bytes(changed)
assert call_count==2 and other_count>30,(call_count,other_count)
record['call_gates']=call_count;record['other_gates']=other_count
targets={
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':(
  '          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
  '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeMethodCallDiagnostic.Apply(translated);\n          return translated;'),
 'Source/DafnyCore/DafnyCore.csproj':(
  '</Project>',
  '  <ItemGroup><Compile Include="../../.github/review/native-method-call-diagnostic/*.cs" /></ItemGroup>\n</Project>')}
for name,(before,after) in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1,name
 changed=s.replace(before,after).encode('utf-8');record['files'][name]={'before':sha(raw),'after':sha(changed)};p.write_bytes(changed)
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
