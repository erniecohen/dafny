from pathlib import Path
import hashlib,json
root=Path.cwd();folder=root/'.github/review/native-fresh-scope-diagnostic';sha=lambda b:hashlib.sha256(b).hexdigest();sources=list(folder.glob('*.cs'));assert len(sources)==2
record={'instrumentation':{p.name:sha(p.read_bytes()) for p in sources},'files':{}}
p=root/'Source/DafnyCore/Verifier/BoogieGenerator.Obligations.cs';raw=p.read_bytes();s=raw.decode('utf-8-sig');start=s.index('  private PropositionLowering LowerDeclaredProposition(');end=s.index('  private Bpl.Expr AssertionSummary(',start);part=s[start:end]
old='      var argumentTemporaries = new HashSet<string>();';assert part.count(old)==1;part=part.replace(old,'      var precedingLocals = new HashSet<Bpl.Variable>(locals.Values, ReferenceEqualityComparer.Instance);\n'+old)
old='      var normalized = CertifiedContractPreparation.Normalize(preparation.Commands, argumentTemporaries);';assert part.count(old)==1;part=part.replace(old,'      var freshLocals = new HashSet<Bpl.Variable>(locals.Values.Where(v => !precedingLocals.Contains(v)), ReferenceEqualityComparer.Instance);\n      var normalized = NativeFreshScopeDiagnostic.NormalizeFresh((codeContext as Declaration)?.Name, preparation.Commands, argumentTemporaries, freshLocals);')
old='      return LowerProposition(builder.Context, condition, etran, preparedTranslator: checking) with {';assert part.count(old)==1;part=part.replace(old,'      var diagnosticScope = NativeFreshScopeDiagnostic.Scope((codeContext as Declaration)?.Name, normalized, argumentTemporaries, freshLocals);\n'+old)
old="""        PurePreparation = !ReferenceEquals(normalized, preparation.Commands) &&
          !condition.DescendantsAndSelf.Any(expression => expression.Resolved is StmtExpr) &&
          CertifiedContractPreparation.CanScope(normalized, argumentTemporaries)""";assert part.count(old)==1
part=part.replace(old,"""        PurePreparation = !condition.DescendantsAndSelf.Any(expression => expression.Resolved is StmtExpr) &&
          ((!ReferenceEquals(normalized, preparation.Commands) &&
            CertifiedContractPreparation.CanScope(normalized, argumentTemporaries)) || diagnosticScope)""")
changed=(s[:start]+part+s[end:]).encode();p.write_bytes(changed);record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)}
# Construct the same original can-call expression at the same original point.
# Only pass its command to the existing certified support-order machinery in
# selected variants. Native control retains its original command position.
p=root/'Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs';raw=p.read_bytes();s2=raw.decode('utf-8-sig')
before="""        callerProof.Add(TrAssumeCmd(tok, callEtran.CanCallAssumptionForVerification(instantiated)));
        var lowering = LowerDeclaredProposition(instantiated, callerProof, locals, callEtran);"""
after="""        var diagnosticLeadingSupport = TrAssumeCmd(tok, callEtran.CanCallAssumptionForVerification(instantiated));
        var diagnosticOrder = NativeFreshScopeDiagnostic.UseCallerSupportOrder((codeContext as Declaration)?.Name, diagnosticLeadingSupport);
        if (!diagnosticOrder) { callerProof.Add(diagnosticLeadingSupport); }
        var lowering = LowerDeclaredProposition(instantiated, callerProof, locals, callEtran,
          leadingSupport: diagnosticOrder ? diagnosticLeadingSupport : null);
        NativeFreshScopeDiagnostic.ObserveCallerSupport((codeContext as Declaration)?.Name,
          diagnosticLeadingSupport, callerProof.Commands);"""
assert s2.count(before)==1;changed=s2.replace(before,after).encode();p.write_bytes(changed);record['files'][str(p.relative_to(root))]={'before':sha(raw),'after':sha(changed)}
for name,before,after in [
 ('Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs','          return translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);','          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);\n          NativeFreshScopeDiagnostic.Apply(translated);\n          return translated;'),
 ('Source/DafnyCore/DafnyCore.csproj','</Project>','  <ItemGroup><Compile Include="../../.github/review/native-fresh-scope-diagnostic/*.cs" /></ItemGroup>\n</Project>')]:
 p=root/name;raw=p.read_bytes();s=raw.decode('utf-8-sig');assert s.count(before)==1;changed=s.replace(before,after).encode();p.write_bytes(changed);record['files'][name]={'before':sha(raw),'after':sha(changed)}
(root/'out/native-instrumentation.json').write_text(json.dumps(record,indent=2)+'\n')
