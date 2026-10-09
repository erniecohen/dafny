from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-caller-split-publication-diagnostic';sha=lambda b:hashlib.sha256(b).hexdigest();sources=list(folder.glob('*.cs'));assert len(sources)==2
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
old="""            callerProof.Add(Assert(new ForceCheckOrigin(ObligationOrigin(tok, piece.Tok)), piece.E,
              description, callerProof.Context with { AssertMode = AssertMode.Check }));"""
new="""            var diagnosticCheck = Assert(new ForceCheckOrigin(ObligationOrigin(tok, piece.Tok)), piece.E,
              description, callerProof.Context with { AssertMode = AssertMode.Check });
            NativeCallerSplitPublicationDiagnostic.Capture((codeContext as Declaration)?.Name, lowering.SplitHappened, diagnosticCheck);
            callerProof.Add(diagnosticCheck);"""
changes=[('Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs',old,new),
 ('Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs','          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);','          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeCallerSplitPublicationDiagnostic.Apply(translated);\n          return translated;'),
 ('Source/DafnyCore/DafnyCore.csproj','</Project>','  <ItemGroup><Compile Include="../../.github/review/native-caller-split-publication-diagnostic/*.cs" /></ItemGroup>\n</Project>')]
for name,before,after in changes:
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1;changed=s.replace(before,after).encode();p.write_bytes(changed);record['files'][name]={'before':sha(raw),'after':sha(changed)}
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
