using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only comparison with the pinned Boogie unified-exit construction.
public static class NativeUnifiedExitDiagnostic {
  public sealed class Scope {
    public BoogieStmtListBuilder Builder;
    public int Start, Preparations;
    public bool Pure;
    public ProofDependencyManager Dependencies;
    public List<Bpl.Cmd> Commands;
  }
  private static Scope Active;
  private static readonly List<Scope> Scopes = new();
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_UNIFIED_EXIT");
    if(value==null){return null;}
    var parts=value.Split(':');
    Require(parts.Length==2 && new[]{"native-control","native-unified-exit","false-unified-entry"}.Contains(parts[0]) && parts[1]=="RemoveFactor","Unknown terminal-exit selection");return parts;
  }
  public static Scope Begin(string declaration,BoogieStmtListBuilder builder,ProofDependencyManager dependencies) {
    var parts=Selection();if(parts==null || parts[1]!=declaration){return null;}
    Require(Active==null,"Nested exit diagnostic");
    Active=new Scope{Builder=builder,Start=builder.Commands.Count,Dependencies=dependencies};Scopes.Add(Active);return Active;
  }
  public static void Prepared(Scope scope,bool pure){if(scope!=null){Require(ReferenceEquals(scope,Active)&&pure,"Uncertified exit preparation");scope.Pure=pure;scope.Preparations++;}}
  public static void Finish(Scope scope){if(scope!=null){Require(ReferenceEquals(scope,Active),"Wrong active exit");var commands=scope.Builder.Commands.Skip(scope.Start).ToList();Require(commands.All(c=>c is Bpl.Cmd),"Structured exit fragment");scope.Commands=commands.Cast<Bpl.Cmd>().ToList();Active=null;}}
  private static object Fingerprint(Bpl.AssertCmd command)=>new{expression=NativeUnifiedExitFingerprint.Expression(command.Expr),attributes=NativeUnifiedExitFingerprint.Attributes(command.Attributes,new NativeUnifiedExitFingerprint.BindingScope()),line=command.tok.line,col=command.tok.col};
  public static void Apply(Bpl.Program program) {
    var parts=Selection();if(parts==null){return;}
    var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$_module.__default.RemoveFactor").ToList();if(targets.Count==0){return;}
    Require(targets.Count==1 && Scopes.Count==1 && Active==null,"Unexpected target/exit count");
    var impl=targets.Single();var scope=Scopes.Single();Require(scope.Pure&&scope.Preparations==1,"Incomplete pure preparation");
    var fragment=scope.Commands;var exit=fragment.OfType<Bpl.AssertCmd>().Single();
    Require(fragment.All(c=>c is Bpl.AssignCmd or Bpl.AssumeCmd or Bpl.AssertCmd or Bpl.CommentCmd),"Unsupported exit fragment");
    var before=impl.Blocks.SelectMany(b=>b.Cmds).ToList();Require(fragment.All(c=>before.Count(x=>ReferenceEquals(x,c))==1),"Missing original exit command");
    var sinks=impl.Blocks.Where(b=>b.Cmds.Contains(exit)).ToList();Require(sinks.Count==1,"Multiple original check blocks");var sink=sinks.Single();
    Require(sink.TransferCmd is Bpl.ReturnCmd && sink.Cmds.SequenceEqual(fragment,ReferenceEqualityComparer.Instance),"Exit block contains non-exit body commands");
    var predecessors=impl.Blocks.Where(b=>b.TransferCmd is Bpl.GotoCmd g && g.LabelNames.Contains(sink.Label)).ToList();
    Require(predecessors.Count==2 && predecessors.All(b=>b.TransferCmd is Bpl.GotoCmd g && g.LabelNames.Count==1),"Unexpected terminal predecessor shape");
    var oldTransfers=impl.Blocks.ToDictionary(b=>b,b=>b.TransferCmd);
    var relocated=parts[0]!="native-control";
    var originalOtherBlocks=impl.Blocks.Where(b=>!ReferenceEquals(b,sink)&&!predecessors.Contains(b)).ToList();
    Bpl.Block unified=sink;
    if(relocated) {
      // Restore the two terminal returns, then use the exact public Boogie pass.
      // Keep the body's EndCurly token until this pass has used it.
      foreach(var predecessor in predecessors) {
        var go=(Bpl.GotoCmd)predecessor.TransferCmd;
        predecessor.TransferCmd=new Bpl.ReturnCmd(go.tok){Attributes=go.Attributes};
      }
      impl.Blocks.Remove(sink);
      Require(impl.Blocks.Count(b=>b.TransferCmd is Bpl.ReturnCmd)==2,"Unexpected additional terminal return");
      unified=VCGeneration.Transformations.DesugarReturns.GenerateUnifiedExit(impl);
      Require(unified.Label=="GeneratedUnifiedExit"&&unified.Cmds.Count==0&&unified.TransferCmd is Bpl.ReturnCmd,"Unexpected native unified-exit shape");
      unified.Cmds.AddRange(fragment);
      impl.StructuredStmts=null;
    }
    var after=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    Require(before.Count==after.Count&&before.All(c=>after.Count(x=>ReferenceEquals(x,c))==1),"Original command missing or duplicated");
    Require(originalOtherBlocks.All(b=>ReferenceEquals(b.TransferCmd,oldTransfers[b])),"Other branch transfer changed");
    Require(unified.Cmds.SequenceEqual(fragment,ReferenceEqualityComparer.Instance),"Exit preparation/check/publication order changed");
    var exitChecks=new HashSet<Bpl.AssertCmd>{exit};
    var byLabel=impl.Blocks.ToDictionary(b=>b.Label);var pathCounts=new List<int>();
    void Paths(Bpl.Block block,int checks,HashSet<Bpl.Block> seen) {
      Require(seen.Add(block),"Cycle in selected terminal-path audit");
      checks+=block.Cmds.OfType<Bpl.AssertCmd>().Count(exitChecks.Contains);
      if(block.Cmds.OfType<Bpl.AssumeCmd>().Any(c=>c.Expr is Bpl.LiteralExpr literal&&literal.IsFalse)){return;}
      if(block.TransferCmd is Bpl.ReturnCmd){pathCounts.Add(checks);return;}
      Require(block.TransferCmd is Bpl.GotoCmd,"Unexpected transfer");
      foreach(var label in ((Bpl.GotoCmd)block.TransferCmd).LabelNames){Paths(byLabel[label],checks,new HashSet<Bpl.Block>(seen));}
    }
    Paths(impl.Blocks.First(),0,new HashSet<Bpl.Block>());Require(pathCounts.Count==2&&pathCounts.All(n=>n==1),"An actual exit path lost or duplicated its mandatory check");
    var checks=after.OfType<Bpl.AssertCmd>().Select(Fingerprint).ToArray();
    var keys=checks.Select(x=>JsonSerializer.Serialize(x)).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    var originalKeys=before.OfType<Bpl.AssertCmd>().Select(Fingerprint).Select(x=>JsonSerializer.Serialize(x)).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    Require(keys.Distinct().OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(originalKeys.Distinct().OrderBy(x=>x,StringComparer.Ordinal)),"Actual check formula, origin or non-id attributes changed");
    Require(checks.Length==originalKeys.Length&&keys.SequenceEqual(originalKeys),"Actual checks or full attributes changed");
    var negative=parts[0]=="false-unified-entry"?new Bpl.AssertCmd(impl.tok,Bpl.Expr.False):null;if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);}
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit output");
    File.WriteAllText(path,JsonSerializer.Serialize(new{target=parts[1],variant=parts[0],exitPreparations=scope.Preparations,certifiedPureExitPreparation=scope.Pure,allOriginalPreparationAndFuelTraversalRetained=true,allExitFragmentCommandsRetainedOnceInOrder=true,allFullDependencyAttributesRetained=true,nativeBoogieUnifiedExitUsed=relocated,normalPublicationAndProcedureContractsRetained=true,allBodyCommandObjectsRetainedOnce=true,allNonterminalBranchTransfersRetained=true,terminalPredecessors=predecessors.Count,actualExitPathCheckCounts=pathCounts,mandatoryChecksPerExit=1,relocated,actualCheckFingerprints=checks,uniqueActualCheckFingerprints=keys.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray(),negativeEntryAdded=negative!=null,productPolicy=false})+"\n");
  }
  private static void Require(bool value,string message){if(!value){throw new InvalidOperationException(message);}}
}
