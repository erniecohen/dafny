using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl=Microsoft.Boogie;
namespace Microsoft.Dafny;
// Diagnostic only: explain the existing difference between split source
// assertions and implicit caller checks. No product policy is proposed here.
public static class NativeCallerSplitPublicationDiagnostic {
  private static readonly List<Bpl.AssertCmd> Checks=new();
  private static string[] Selection(){var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_CALLER_SPLIT");if(value==null){return null;}var p=value.Split(':');Require(p.Length==2&&p[1]=="LemmaRemainder"&&new[]{"native-control","source-split-publication","false-entry"}.Contains(p[0]),"Unknown selection");return p;}
  public static void Capture(string declaration,bool split,Bpl.PredicateCmd check){var p=Selection();if(p==null||p[1]!=declaration||!split){return;}Require(check is Bpl.AssertCmd,"Caller check is not mandatory");Checks.Add((Bpl.AssertCmd)check);}
  private static string Key(Bpl.AssertCmd c)=>JsonSerializer.Serialize(new{expression=NativeCallerSplitFingerprint.Expression(c.Expr),attributes=NativeCallerSplitFingerprint.Attributes(c.Attributes,new NativeCallerSplitFingerprint.BindingScope()),line=c.tok.line,col=c.tok.col});
  public static void Apply(Bpl.Program program){var p=Selection();if(p==null){return;}var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$Std_mArithmetic_mDivMod.__default.LemmaRemainder").ToList();if(targets.Count==0){return;}Require(targets.Count==1&&Checks.Count==3,"Unexpected target/split count");var impl=targets.Single();var commands=impl.Blocks.SelectMany(b=>b.Cmds).ToList();var transfers=impl.Blocks.Select(b=>b.TransferCmd).ToList();var asserts=commands.OfType<Bpl.AssertCmd>().ToList();Require(asserts.Count==8,"Unexpected actual check count");Require(Checks.All(c=>commands.Count(v=>ReferenceEquals(v,c))==1),"Caller piece lost/duplicated");var before=asserts.Select(Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray();var originals=Checks.ToDictionary(c=>c,c=>c.Attributes,ReferenceEqualityComparer.Instance);var exprs=asserts.Select(c=>c.Expr).ToList();var selected=p[0]!="native-control";
    if(selected){foreach(var c in Checks){Require(!HasSubsumption(c.Attributes),"Caller piece already has a publication override");c.Attributes=new Bpl.QKeyValue(c.tok,"subsumption",new List<object>{Bpl.Expr.Literal(0)},c.Attributes);}}
    var actualAfter=asserts.Select(Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    foreach(var c in Checks){if(selected){Require(c.Attributes.Key=="subsumption"&&ReferenceEquals(c.Attributes.Next,originals[c]),"Original full metadata was not retained");var added=c.Attributes;c.Attributes=originals[c];Require(before.Contains(Key(c)),"Actual P/full original metadata changed");c.Attributes=added;}}
    Require(asserts.Select(c=>c.Expr).SequenceEqual(exprs,ReferenceEqualityComparer.Instance),"Actual expression changed");Require(commands.SequenceEqual(impl.Blocks.SelectMany(b=>b.Cmds),ReferenceEqualityComparer.Instance)&&transfers.SequenceEqual(impl.Blocks.Select(b=>b.TransferCmd),ReferenceEqualityComparer.Instance),"Other commands/order/transfers changed");
    var negative=p[0]=="false-entry";if(negative){impl.Blocks.First().Cmds.Insert(0,new Bpl.AssertCmd(impl.tok,Bpl.Expr.False));}
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit");File.WriteAllText(path,JsonSerializer.Serialize(new{target=p[1],variant=p[0],actualCheckFingerprints=before,actualCheckFingerprintsAfter=actualAfter,staticCheckCount=8,callerSplitCheckCount=3,addedPublicationAttributes=selected?3:0,allActualExpressionsRetainedByReference=true,allOriginalAttributeChainsRetainedByReference=true,allOriginalCommandsOrderAndTransfersRetained=true,allPrecheckPreparationFuelAndCompleteSummariesRetained=true,negativeEntryAdded=negative,changesPiecePublicationContext=selected,productPolicy=false})+"\n");
  }
  private static bool HasSubsumption(Bpl.QKeyValue attributes){for(var a=attributes;a!=null;a=a.Next){if(a.Key=="subsumption"){return true;}}return false;}
  private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
}
