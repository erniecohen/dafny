using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl=Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only original postcondition wrapper around the same local assertion.
public static class NativeExitWrapperDiagnostic {
  private static readonly List<Bpl.AssertCmd> Checks=new();
  private static string[] Selection(){var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_EXIT_WRAPPER");if(value==null){return null;}var p=value.Split(':');Require(p.Length==2&&p[1]=="RemoveFactor"&&new[]{"native-control","ensures-wrapper","false-entry"}.Contains(p[0]),"Unknown selection");return p;}
  public static void Capture(string declaration,IEnumerable<object> commands){var p=Selection();if(p==null||p[1]!=declaration){return;}Checks.AddRange(commands.OfType<Bpl.AssertCmd>());}
  private static string Key(Bpl.AssertCmd c)=>JsonSerializer.Serialize(new{expression=NativeExitWrapperFingerprint.Expression(c.Expr),attributes=NativeExitWrapperFingerprint.Attributes(c.Attributes,new NativeExitWrapperFingerprint.BindingScope()),line=c.tok.line,col=c.tok.col});
  public static void Apply(Bpl.Program program){
    var p=Selection();if(p==null){return;}var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$_module.__default.RemoveFactor").ToList();if(targets.Count==0){return;}Require(targets.Count==1&&Checks.Count==1,"Unexpected target/check count");var impl=targets.Single();var proc=program.TopLevelDeclarations.OfType<Bpl.Procedure>().Single(q=>q.Name==impl.Name);Require(proc.Ensures.All(e=>e.Free),"Procedure contains a second checked postcondition");
    var commands=impl.Blocks.SelectMany(b=>b.Cmds).ToList();var original=Checks.Single();Require(original.GetType()==typeof(Bpl.AssertCmd)&&commands.Count(c=>ReferenceEquals(c,original))==1,"Original local check missing/duplicated/specialized");Require(original.Checksum==null&&original.SugaredCmdChecksum==null,"Unexpected pre-pass checksum");
    var actualBefore=commands.OfType<Bpl.AssertCmd>().Select(Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray();Require(actualBefore.Length==13,"Unexpected original check count");var replacement=original;
    if(p[0]!="native-control"){
      var ensures=new Bpl.Ensures(original.tok,false,original.Expr,null,original.Attributes){Description=original.Description,ErrorDataEnhanced=original.ErrorDataEnhanced};
      replacement=new Bpl.AssertEnsuresCmd(ensures){Attributes=original.Attributes,Description=original.Description,ErrorDataEnhanced=original.ErrorDataEnhanced,OrigExpr=original.OrigExpr,IncarnationMap=original.IncarnationMap,IrrelevantForChecksumComputation=original.IrrelevantForChecksumComputation};
      var block=impl.Blocks.Single(b=>b.Cmds.Any(c=>ReferenceEquals(c,original)));var index=block.Cmds.FindIndex(c=>ReferenceEquals(c,original));block.Cmds[index]=replacement;
      Require(ReferenceEquals(replacement.Expr,original.Expr)&&ReferenceEquals(replacement.Attributes,original.Attributes)&&ReferenceEquals(replacement.Description,original.Description)&&ReferenceEquals(replacement.ErrorDataEnhanced,original.ErrorDataEnhanced),"Expression/full metadata not retained");
      Require(ReferenceEquals(ensures.Condition,original.Expr)&&!ensures.Free&&!proc.Ensures.Contains(ensures),"Wrapper introduces another procedure proof");
    }
    var after=impl.Blocks.SelectMany(b=>b.Cmds).ToList();Require(commands.Count==after.Count,"Command count changed");for(var i=0;i<commands.Count;i++){Require(ReferenceEquals(after[i],ReferenceEquals(commands[i],original)?replacement:commands[i]),"Other command/order changed");}
    var actualAfter=after.OfType<Bpl.AssertCmd>().Select(Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray();Require(actualBefore.SequenceEqual(actualAfter),"Actual check/full attributes differ");var negative=p[0]=="false-entry"?new Bpl.AssertCmd(impl.tok,Bpl.Expr.False):null;if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);}
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit");File.WriteAllText(path,JsonSerializer.Serialize(new{target=p[1],variant=p[0],actualCheckFingerprints=actualAfter,staticCheckCount=actualAfter.Length,exitCheckCount=Checks.Count,wrappedChecks=p[0]=="native-control"?0:1,allOriginalCommandsRetainedExceptCheckWrapper=true,expressionAndFullMetadataRetainedByReference=true,allPreparationFuelPublicationAndTransfersUnchanged=true,noncheckingProcedureContractRetained=true,negativeEntryAdded=negative!=null,productPolicy=false})+"\n");
  }
  private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
}
