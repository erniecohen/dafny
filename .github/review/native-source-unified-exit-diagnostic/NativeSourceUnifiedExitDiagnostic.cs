using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only comparison with the pinned Boogie unified-exit construction.
public static class NativeSourceUnifiedExitDiagnostic {
  public sealed class Scope {
    public BoogieStmtListBuilder Builder;
    public int Start, Preparations;
    public bool Pure;
    public ProofDependencyManager Dependencies;
    public List<Bpl.Cmd> Commands, Original;
  }
  private static Scope Active;
  private static readonly List<Scope> Scopes = new();
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_SOURCE_UNIFIED_EXIT");
    if(value==null){return null;}
    var parts=value.Split(':');
    Require(parts.Length==2 && new[]{"native-control","source-native-unified-exit","false-source-unified-entry"}.Contains(parts[0]) && parts[1]=="RemoveFactor","Unknown terminal-exit selection");return parts;
  }
  public static Scope Begin(string declaration,BoogieStmtListBuilder builder,ProofDependencyManager dependencies) {
    var parts=Selection();if(parts==null || parts[1]!=declaration){return null;}
    Require(Active==null,"Nested exit diagnostic");
    Active=new Scope{Builder=builder,Start=builder.Commands.Count,Dependencies=dependencies};Scopes.Add(Active);return Active;
  }
  public static void Prepared(Scope scope,bool pure){if(scope!=null){Require(ReferenceEquals(scope,Active)&&pure,"Uncertified exit preparation");scope.Pure=pure;scope.Preparations++;}}
  private static IEnumerable<Bpl.Cmd> Flat(IEnumerable<object> commands) {
    foreach(var command in commands) {
      if(command is Bpl.Cmd simple){yield return simple;}
      else if(command is Bpl.IfCmd conditional) {
        foreach(var c in FlatList(conditional.Thn)){yield return c;}
        if(conditional.ElseBlock!=null){foreach(var c in FlatList(conditional.ElseBlock)){yield return c;}}
        if(conditional.ElseIf!=null){foreach(var c in Flat(new object[]{conditional.ElseIf})){yield return c;}}
      } else {Require(command is Bpl.TransferCmd,"Unsupported source statement kind");}
    }
  }
  private static IEnumerable<Bpl.Cmd> FlatList(Bpl.StmtList list) {
    Require(list.PrefixCommands==null,"Structured control flow already resolved");
    foreach(var block in list.BigBlocks) {
      foreach(var c in block.simpleCmds){yield return c;}
      if(block.ec!=null){foreach(var c in Flat(new object[]{block.ec})){yield return c;}}
    }
  }
  public static void Finish(Scope scope) {
    if(scope==null){return;}Require(ReferenceEquals(scope,Active),"Wrong active exit");
    var commands=scope.Builder.Commands.Skip(scope.Start).ToList();Require(commands.All(c=>c is Bpl.Cmd),"Structured exit fragment");scope.Commands=commands.Cast<Bpl.Cmd>().ToList();
    scope.Original=Flat(scope.Builder.Commands).ToList();
    if(Selection()[0]!="native-control") {
      var prefix=scope.Builder.Commands.Take(scope.Start).ToList();
      Require(prefix.LastOrDefault() is Bpl.IfCmd conditional&&conditional.ElseIf==null&&conditional.ElseBlock!=null,"Unsupported terminal conditional");
      // No local fragment participates in anonymous naming or successor construction.
      // It was still prepared at the original point, with the original local state.
      scope.Builder.Commands.RemoveRange(scope.Start,commands.Count);
      var rebuilt=new Bpl.StmtListBuilder();
      foreach(var command in scope.Builder.Commands) {
        switch(command) {
          case Bpl.Cmd simple:rebuilt.Add(simple);break;
          case Bpl.StructuredCmd structured:rebuilt.Add(structured);break;
          case Bpl.TransferCmd transfer:rebuilt.Add(transfer);break;
          default:throw new InvalidOperationException("Unsupported explicit label in source unified-exit comparison");
        }
      }
      scope.Builder.builder=rebuilt;
    }
    Active=null;
  }
  private static object Fingerprint(Bpl.AssertCmd command)=>new{expression=NativeSourceUnifiedExitFingerprint.Expression(command.Expr),attributes=NativeSourceUnifiedExitFingerprint.Attributes(command.Attributes,new NativeSourceUnifiedExitFingerprint.BindingScope()),line=command.tok.line,col=command.tok.col};
  public static void Apply(Bpl.Program program) {
    var parts=Selection();if(parts==null){return;}
    var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$_module.__default.RemoveFactor").ToList();if(targets.Count==0){return;}
    Require(targets.Count==1 && Scopes.Count==1 && Active==null,"Unexpected target/exit count");
    var impl=targets.Single();var scope=Scopes.Single();Require(scope.Pure&&scope.Preparations==1,"Incomplete pure preparation");
    var fragment=scope.Commands;var exit=fragment.OfType<Bpl.AssertCmd>().Single();
    Require(fragment.All(c=>c is Bpl.AssignCmd or Bpl.AssumeCmd or Bpl.AssertCmd or Bpl.CommentCmd),"Unsupported exit fragment");
    var before=scope.Original;var nativeBefore=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    var relocated=parts[0]!="native-control";
    Require(fragment.All(c=>nativeBefore.Count(x=>ReferenceEquals(x,c))==(relocated?0:1)),"Exit fragment emitted too early or missing");
    var terminals=impl.Blocks.Where(b=>b.TransferCmd is Bpl.ReturnCmd).ToList();
    var oldTransfers=impl.Blocks.ToDictionary(b=>b,b=>b.TransferCmd);
    Bpl.Block unified;
    if(relocated) {
      Require(terminals.Count==2,"Source layout did not retain two native terminal returns");
      // Apply exactly the pinned pass to the naturally constructed terminal CFG.
      // No return/block is reconstructed after naming and successor resolution.
      unified=VCGeneration.Transformations.DesugarReturns.GenerateUnifiedExit(impl);
      Require(unified.Label=="GeneratedUnifiedExit"&&unified.Cmds.Count==0&&unified.TransferCmd is Bpl.ReturnCmd,"Unexpected native unified-exit shape");
      unified.Cmds.AddRange(fragment);impl.StructuredStmts=null;
    } else {
      var sinks=impl.Blocks.Where(b=>b.Cmds.Contains(exit)).ToList();Require(sinks.Count==1,"Multiple original check blocks");unified=sinks.Single();
      Require(terminals.Count==1&&ReferenceEquals(terminals.Single(),unified),"Native control terminal changed");
    }
    var after=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    Require(after.Count==nativeBefore.Count+(relocated?fragment.Count:0)&&nativeBefore.All(c=>after.Count(x=>ReferenceEquals(x,c))==1),"Native body command missing or duplicated");
    Require(before.All(c=>after.Count(x=>ReferenceEquals(x,c))==1),"Original source command or fragment missing/duplicated");
    Require(oldTransfers.Where(kv=>!relocated||!terminals.Contains(kv.Key)).All(kv=>ReferenceEquals(kv.Key.TransferCmd,kv.Value)),"Nonterminal transfer changed");
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
    var negative=parts[0]=="false-source-unified-entry"?new Bpl.AssertCmd(impl.tok,Bpl.Expr.False):null;if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);}
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit output");
    File.WriteAllText(path,JsonSerializer.Serialize(new{target=parts[1],variant=parts[0],exitPreparations=scope.Preparations,certifiedPureExitPreparation=scope.Pure,allOriginalPreparationAndFuelTraversalRetained=true,allExitFragmentCommandsRetainedOnceInOrder=true,allFullDependencyAttributesRetained=true,nativeBoogieUnifiedExitUsed=relocated,fragmentWithheldBeforeNativeCFG=relocated,nativeTerminalReturnsBeforeUnification=terminals.Count,normalPublicationAndProcedureContractsRetained=true,allBodyCommandObjectsRetainedOnce=true,allNonterminalBranchTransfersRetained=true,terminalPredecessors=2,actualExitPathCheckCounts=pathCounts,mandatoryChecksPerExit=1,relocated,actualCheckFingerprints=checks,uniqueActualCheckFingerprints=keys.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray(),negativeEntryAdded=negative!=null,productPolicy=false})+"\n");
  }
  private static void Require(bool value,string message){if(!value){throw new InvalidOperationException(message);}}
}
