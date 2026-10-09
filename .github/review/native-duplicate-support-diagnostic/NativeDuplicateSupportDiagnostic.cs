using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl=Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only removal of one certified structurally identical local assumption.
public static class NativeDuplicateSupportDiagnostic {
  private sealed record Captured(Bpl.AssumeCmd Original,Bpl.AssumeCmd Retained,IReadOnlyList<object> Preparation,bool Omitted);
  private static readonly List<Captured> Captures=new();
  private static string[] Selection(){var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_DUPLICATE_SUPPORT");if(value==null){return null;}var p=value.Split(':');Require(p.Length==2&&p[1]=="RemoveFactor"&&new[]{"native-control","deduplicate-support","false-entry"}.Contains(p[0]),"Unknown selection");return p;}
  public static bool Omit(string declaration,Bpl.AssumeCmd leading,IReadOnlyList<object> preparation){
    var p=Selection();if(p==null||p[1]!=declaration){return false;}
    Require(leading.Attributes==null,"Leading assumption has metadata");var retained=preparation.LastOrDefault() as Bpl.AssumeCmd;
    Require(retained!=null&&retained.Attributes==null&&leading.Expr.Equals(retained.Expr),"Local can-call assumptions not structurally identical");
    Require(NativeDuplicateSupportFingerprint.Expression(leading.Expr)==NativeDuplicateSupportFingerprint.Expression(retained.Expr),"Strict expression fingerprint differs");
    var omit=p[0]!="native-control";Captures.Add(new Captured(leading,retained,preparation,omit));return omit;
  }
  public static void Apply(Bpl.Program program){
    var p=Selection();if(p==null){return;}var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$_module.__default.RemoveFactor").ToList();if(targets.Count==0){return;}Require(targets.Count==1&&Captures.Count==1,"Unexpected target/capture count");var impl=targets.Single();var commands=impl.Blocks.SelectMany(b=>b.Cmds).ToList();var captured=Captures.Single();
    Require(commands.Count(c=>ReferenceEquals(c,captured.Original))==(captured.Omitted?0:1),"Unexpected duplicate emission");
    foreach(var c in captured.Preparation){Require(commands.Count(x=>ReferenceEquals(x,c))==1,"Original preparation command lost/duplicated");}
    Require(commands.Count(c=>ReferenceEquals(c,captured.Retained))==1&&captured.Original.Expr.Equals(captured.Retained.Expr),"Required support not retained");
    var actual=commands.OfType<Bpl.AssertCmd>().Select(c=>JsonSerializer.Serialize(new{expression=NativeDuplicateSupportFingerprint.Expression(c.Expr),attributes=NativeDuplicateSupportFingerprint.Attributes(c.Attributes,new NativeDuplicateSupportFingerprint.BindingScope()),line=c.tok.line,col=c.tok.col})).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    Require(actual.Length==13,"Original static check count changed");var negative=p[0]=="false-entry"?new Bpl.AssertCmd(impl.tok,Bpl.Expr.False):null;if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);}
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit");
    File.WriteAllText(path,JsonSerializer.Serialize(new{target=p[1],variant=p[0],actualCheckFingerprints=actual,staticCheckCount=actual.Length,duplicateCaptures=Captures.Count,omittedDuplicates=captured.Omitted?1:0,allOriginalPreparationCommandsRetainedOnce=true,duplicateExprStructurallyEqual=true,duplicateStrictFingerprintEqual=true,retainedSupportCount=1,allDistinctSupportFuelChecksPublicationAndTransfersUnchanged=true,negativeEntryAdded=negative!=null,productPolicy=false})+"\n");
  }
  private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
}
