from pathlib import Path
import hashlib,json,re
root=Path.cwd();folder=root/'.github/review/native-family-diagnostic'
sources=list(folder.glob('*.cs'));assert len(sources)==2
sha=lambda data:hashlib.sha256(data).hexdigest()
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
flag='options.Get(CommonOptionBag.ConsistentObligationChecks)'
exit_sites={
 'BoogieGenerator.Methods.cs':[
  'if ('+flag+') {\n        CheckMethodPostconditions',
  'var locallyChecked = '+flag+' &&\n                kind == MethodTranslationKind.Implementation'],
 'BoogieGenerator.Iterators.cs':[
  'var nonchecking = '+flag+' &&\n                kind == MethodTranslationKind.Implementation',
  'if ('+flag+') {\n        TrStmtList'],
 'Statements/BoogieGenerator.TrStatement.cs':[
  'if ('+flag+') {\n            if (codeContext is MethodOrConstructor returningMethod)']
}
verifier=root/'Source/DafnyCore/Verifier'
expression=re.compile(r'(?<![A-Za-z0-9_])(?:(?:this|generator)\.)?options\.Get\(CommonOptionBag\.ConsistentObligationChecks\)')
exit_count=0;other_count=0
for p in sorted(verifier.rglob('*.cs')):
 raw=p.read_bytes();s=raw.decode('utf-8-sig');relative=str(p.relative_to(verifier))
 for site in exit_sites.get(relative,[]):
  assert s.count(site)==1,(relative,site)
  s=s.replace(site,site.replace(flag,'EXIT_FAMILY_GATE'));exit_count+=1
 def wrap(m):
  global other_count
  other_count+=1;return 'NativeFamilyDiagnostic.Enabled("other", '+m.group()+')'
 s=expression.sub(wrap,s)
 s=s.replace('EXIT_FAMILY_GATE','NativeFamilyDiagnostic.Enabled("exit", '+flag+')')
 if s!=raw.decode('utf-8-sig'):
  changed=s.encode('utf-8');record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)};p.write_bytes(changed)
assert exit_count==5 and other_count>30,(exit_count,other_count)
record['exit_gates']=exit_count;record['other_gates']=other_count
targets={
 'Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs':(
  '          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);',
  '          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeFamilyDiagnostic.Apply(translated);\n          return translated;'),
 'Source/DafnyCore/DafnyCore.csproj':(
  '</Project>',
  '  <ItemGroup><Compile Include="../../.github/review/native-family-diagnostic/*.cs" /></ItemGroup>\n</Project>')}
for name,(before,after) in targets.items():
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1,name
 changed=s.replace(before,after).encode('utf-8');record['files'][name]={'before':sha(raw),'after':sha(changed)};p.write_bytes(changed)
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
