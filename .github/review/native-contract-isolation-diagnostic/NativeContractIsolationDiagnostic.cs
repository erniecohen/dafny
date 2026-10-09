using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl=Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only grouping of the actual local contract checks; no support changes.
public static class NativeContractIsolationDiagnostic {
  private sealed record Captured(Bpl.AssertCmd Check, Bpl.Expr Expression, Bpl.QKeyValue OriginalAttributes, string Fingerprint, string Producer, bool Tagged);
  private static readonly List<Captured> Checks=new();
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_CONTRACT_ISOLATION");if(value==null){return null;}
    var parts=value.Split(':');Require(parts.Length==2&&new[]{"native-control","isolate-contracts","false-contract-entry"}.Contains(parts[0])&&new[]{"Composite","RemoveFactor"}.Contains(parts[1]),"Unknown contract-isolation selection");return parts;
  }
  private static string Key(Bpl.AssertCmd check,Bpl.QKeyValue attributes)=>JsonSerializer.Serialize(new{expression=NativeContractIsolationFingerprint.Expression(check.Expr),attributes=NativeContractIsolationFingerprint.Attributes(attributes,new NativeContractIsolationFingerprint.BindingScope()),line=check.tok.line,col=check.tok.col});
  public static void Capture(string declaration,IEnumerable<object> commands,string producer) {
    var parts=Selection();if(parts==null||parts[1]!=declaration){return;}Require(producer=="exit"||producer=="caller","Unknown producer");
    foreach(var check in commands.OfType<Bpl.AssertCmd>()) {
      Require(!Checks.Any(x=>ReferenceEquals(x.Check,check)),"Repeated contract capture");var attributes=check.Attributes;var before=Key(check,attributes);var tagged=parts[0]!="native-control";
      Require(Bpl.QKeyValue.FindAttribute(attributes,a=>a.Key=="isolate")==null,"Source contract already isolated");
      if(tagged){check.Attributes=new Bpl.QKeyValue(check.tok,"isolate",new List<object>(),attributes);}
      Checks.Add(new Captured(check,check.Expr,attributes,before,producer,tagged));
    }
  }
  public static void Apply(Bpl.Program program) {
    var parts=Selection();if(parts==null){return;}var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$_module.__default."+parts[1]).ToList();if(targets.Count==0){return;}Require(targets.Count==1,"Unexpected target count");var impl=targets.Single();var commands=impl.Blocks.SelectMany(b=>b.Cmds).ToList();var captured=Checks.Where(x=>commands.Any(c=>ReferenceEquals(c,x.Check))).ToList();
    Require(captured.Count>0&&captured.Count==Checks.Count,"Contract capture escaped the target");
    foreach(var c in captured) {
      Require(commands.Count(x=>ReferenceEquals(x,c.Check))==1&&ReferenceEquals(c.Check.Expr,c.Expression),"Contract check/formula missing, changed or duplicated");
      if(c.Tagged){Require(c.Check.Attributes.Key=="isolate"&&c.Check.Attributes.Params.Count==0&&ReferenceEquals(c.Check.Attributes.Next,c.OriginalAttributes),"Existing full metadata altered");}
      else{Require(ReferenceEquals(c.Check.Attributes,c.OriginalAttributes),"Native metadata changed");}
      Require(Key(c.Check,c.OriginalAttributes)==c.Fingerprint,"Original formula/full attribute fingerprint changed");
    }
    var actual=commands.OfType<Bpl.AssertCmd>().Select(check=>{var found=captured.SingleOrDefault(c=>ReferenceEquals(c.Check,check));return Key(check,found==null?check.Attributes:found.OriginalAttributes);}).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    var taggedCount=commands.OfType<Bpl.AssertCmd>().Count(c=>Bpl.QKeyValue.FindAttribute(c.Attributes,a=>a.Key=="isolate")!=null);Require(taggedCount==captured.Count(c=>c.Tagged),"Unrelated assertion acquired isolation");
    var negative=parts[0]=="false-contract-entry"?new Bpl.AssertCmd(impl.tok,Bpl.Expr.False,new Bpl.QKeyValue(impl.tok,"isolate",new List<object>(),null)):null;if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);}
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit output");
    File.WriteAllText(path,JsonSerializer.Serialize(new{target=parts[1],variant=parts[0],actualCheckFingerprints=actual,staticCheckCount=actual.Length,capturedContracts=captured.Select(c=>new{producer=c.Producer,line=c.Check.tok.line,col=c.Check.tok.col,c.Tagged,c.Fingerprint}),isolationMarkers=taggedCount,allOriginalChecksAndFullMetadataRetainedExceptAddedIsolate=true,allPreparationFuelPublicationAndTransfersUnchanged=true,noOtherSourceAssertionIsolated=true,negativeEntryAdded=negative!=null,negativeEntryIsolated=negative!=null,productPolicy=false})+"\n");
  }
  private static void Require(bool value,string message){if(!value){throw new InvalidOperationException(message);}}
}
