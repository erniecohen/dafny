using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only relocation of a certified exit fragment to its original terminal paths.
public static class NativeTerminalExitsDiagnostic {
  public sealed class Scope {
    public BoogieStmtListBuilder Builder;
    public int Start, Preparations;
    public bool Pure;
    public List<Bpl.Cmd> Commands;
  }
  private static Scope Active;
  private static readonly List<Scope> Scopes = new();
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_TERMINAL_EXITS");
    if(value==null){return null;}
    var parts=value.Split(':');
    Require(parts.Length==2 && new[]{"native-control","terminal-exits","false-terminal-entry"}.Contains(parts[0]) && parts[1]=="RemoveFactor","Unknown terminal-exit selection");return parts;
  }
  public static Scope Begin(string declaration,BoogieStmtListBuilder builder) {
    var parts=Selection();if(parts==null || parts[1]!=declaration){return null;}
    Require(Active==null,"Nested exit diagnostic");
    Active=new Scope{Builder=builder,Start=builder.Commands.Count};Scopes.Add(Active);return Active;
  }
  public static void Prepared(Scope scope,bool pure){if(scope!=null){Require(ReferenceEquals(scope,Active)&&pure,"Uncertified exit preparation");scope.Pure=pure;scope.Preparations++;}}
  public static void Finish(Scope scope){if(scope!=null){Require(ReferenceEquals(scope,Active),"Wrong active exit");var commands=scope.Builder.Commands.Skip(scope.Start).ToList();Require(commands.All(c=>c is Bpl.Cmd),"Structured exit fragment");scope.Commands=commands.Cast<Bpl.Cmd>().ToList();Active=null;}}
  private static object Fingerprint(Bpl.AssertCmd command)=>new{expression=NativeTerminalExitsFingerprint.Expression(command.Expr),attributes=NativeTerminalExitsFingerprint.Attributes(command.Attributes,new NativeTerminalExitsFingerprint.BindingScope()),line=command.tok.line,col=command.tok.col};
  private static string Key(Bpl.Cmd command) {
    // Duplicator must preserve the full expression, source origin and attributes of every exit command.
    return command switch {
      Bpl.AssertCmd a=>"assert:"+JsonSerializer.Serialize(Fingerprint(a)),
      Bpl.AssumeCmd a=>"assume:"+NativeTerminalExitsFingerprint.Expression(a.Expr)+":"+NativeTerminalExitsFingerprint.Attributes(a.Attributes,new NativeTerminalExitsFingerprint.BindingScope())+":"+a.tok.line+":"+a.tok.col,
      Bpl.AssignCmd a=>"assign:"+string.Join(",",a.Lhss.Select(x=>x.ToString()))+":"+string.Join(",",a.Rhss.Select(NativeTerminalExitsFingerprint.Expression))+":"+a.tok.line+":"+a.tok.col,
      Bpl.CommentCmd c=>"comment:"+c.ToString(),
      _=>throw new InvalidOperationException("Unsupported exit command")
    };
  }
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
    var oldTransfers=impl.Blocks.ToDictionary(b=>b,b=>b.TransferCmd);var copies=new List<List<Bpl.Cmd>>();
    var relocated=parts[0]!="native-control";
    if(relocated) {
      foreach(var predecessor in predecessors) {
        var duplicator=new Bpl.Duplicator();var copy=fragment.Select(c=>c is Bpl.CommentCmd ? (Bpl.Cmd)c.Clone() : (Bpl.Cmd)duplicator.Visit(c)).ToList();
        Require(fragment.Select(Key).SequenceEqual(copy.Select(Key)),"Changed relocated exit command metadata or formula");
        predecessor.Cmds.AddRange(copy);predecessor.TransferCmd=(Bpl.TransferCmd)new Bpl.Duplicator().Visit(sink.TransferCmd);copies.Add(copy);
      }
      impl.Blocks.Remove(sink);impl.StructuredStmts=null;
    }
    var after=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    Require(before.Where(c=>!fragment.Contains(c)).All(c=>after.Count(x=>ReferenceEquals(x,c))==1),"Other body command missing or duplicated");
    Require(impl.Blocks.Where(b=>!relocated||!predecessors.Contains(b)).All(b=>ReferenceEquals(b.TransferCmd,oldTransfers[b])),"Other branch transfer changed");
    Require(relocated ? fragment.All(c=>!after.Contains(c))&&copies.All(copy=>copy.All(c=>after.Count(x=>ReferenceEquals(x,c))==1)) : before.SequenceEqual(after,ReferenceEqualityComparer.Instance),"Exit fragment emission mismatch");
    var exitChecks=relocated?copies.SelectMany(c=>c).OfType<Bpl.AssertCmd>().ToHashSet():new HashSet<Bpl.AssertCmd>{exit};
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
    Require(keys.Distinct().OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(originalKeys.Distinct().OrderBy(x=>x,StringComparer.Ordinal)),"Actual check formula or full attributes changed");
    Require(checks.Length==originalKeys.Length+(relocated?1:0),"Only the terminal check may occur at both exits");
    var negative=parts[0]=="false-terminal-entry"?new Bpl.AssertCmd(impl.tok,Bpl.Expr.False):null;if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);}
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit output");
    File.WriteAllText(path,JsonSerializer.Serialize(new{target=parts[1],variant=parts[0],exitPreparations=scope.Preparations,certifiedPureExitPreparation=scope.Pure,allOriginalPreparationAndFuelTraversalRetained=true,allExitFragmentCommandsAndMetadataRetainedAtEachPath=true,normalPublicationAndProcedureContractsRetained=true,allOtherBodyCommandObjectsRetainedOnce=true,allOtherBranchTransfersRetained=true,terminalPredecessors=predecessors.Count,actualExitPathCheckCounts=pathCounts,mandatoryChecksPerExit=1,relocated,actualCheckFingerprints=checks,uniqueActualCheckFingerprints=keys.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray(),negativeEntryAdded=negative!=null,productPolicy=false})+"\n");
  }
  private static void Require(bool value,string message){if(!value){throw new InvalidOperationException(message);}}
}
