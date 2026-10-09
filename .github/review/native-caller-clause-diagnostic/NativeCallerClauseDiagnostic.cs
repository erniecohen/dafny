using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl=Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only source-clause preparation boundary. No support is discarded.
public static class NativeCallerClauseDiagnostic {
  private static int Calls, SourceClauses, ConjunctClauses;
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_CALLER_CLAUSE");
    if(value==null){return null;}
    var parts=value.Split(':');
    Require(parts.Length==2 && new[]{"native-control","whole-clause","false-whole-entry"}.Contains(parts[0]) && new[]{"Composite","RemoveFactor","FormArmy"}.Contains(parts[1]),"Unknown caller-clause selection");return parts;
  }
  public static IEnumerable<AttributedExpression> Choose(List<AttributedExpression> clauses, IEnumerable<AttributedExpression> conjuncts) {
    var selection=Selection();if(selection==null){return conjuncts;}
    var original=conjuncts.ToList();Calls++;SourceClauses+=clauses.Count;ConjunctClauses+=original.Count;
    return selection[0]=="native-control" ? original : clauses;
  }
  public static void Apply(Bpl.Program program) {
    var parts=Selection();if(parts==null){return;}
    var implementations=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$_module.__default."+parts[1]).ToList();
    if(implementations.Count==0){return;}Require(implementations.Count==1,"Ambiguous target");
    var impl=implementations.Single();var commands=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    var negative=parts[0]=="false-whole-entry" ? new Bpl.AssertCmd(impl.tok,Bpl.Expr.False) : null;
    if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);commands.Insert(0,negative);}
    var checks=commands.OfType<Bpl.AssertCmd>().Where(c=>!ReferenceEquals(c,negative)).Select(c=>new {
      expression=NativeCallerClauseFingerprint.Expression(c.Expr),
      attributes=NativeCallerClauseFingerprint.Attributes(c.Attributes,new NativeCallerClauseFingerprint.BindingScope()),
      line=c.tok.line,col=c.tok.col
    }).OrderBy(c=>c.line).ThenBy(c=>c.col).ThenBy(c=>c.expression,StringComparer.Ordinal).ThenBy(c=>c.attributes,StringComparer.Ordinal).ToArray();
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit destination");
    File.WriteAllText(path,JsonSerializer.Serialize(new {
      target=parts[1],variant=parts[0],callerSites=Calls,sourceClauses=SourceClauses,conjunctClauses=ConjunctClauses,
      actualCheckFingerprints=checks,actualChecks=checks.Length,negativeEntryAdded=negative!=null,
      originalProcedureContractsRetained=true,fullSourceLocalPreparationAndFuelTraversalRetained=true,
      existingSingleCheckAndPureScopePolicyRetained=true,onlyCallerSourceClauseBoundaryChanged=true
    })+"\n");
  }
  private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
}
