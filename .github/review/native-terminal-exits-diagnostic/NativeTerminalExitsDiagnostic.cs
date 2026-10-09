using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only relocation of a certified exit fragment to its original terminal paths.
public static class NativeTerminalExitsDiagnostic {
  public sealed class Scope {
    public BoogieStmtListBuilder Builder;
    public int Start, Preparations;
    public bool Pure;
    public ProofDependencyManager Dependencies;
    public List<Bpl.Cmd> Commands;
  }
  private static Scope Active;
  private static readonly List<object> FreshDependencies = new();
  private static readonly List<Scope> Scopes = new();
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_TERMINAL_EXITS");
    if(value==null){return null;}
    var parts=value.Split(':');
    Require(parts.Length==2 && new[]{"native-control","terminal-exits","false-terminal-entry"}.Contains(parts[0]) && parts[1]=="RemoveFactor","Unknown terminal-exit selection");return parts;
  }
  public static Scope Begin(string declaration,BoogieStmtListBuilder builder,ProofDependencyManager dependencies) {
    var parts=Selection();if(parts==null || parts[1]!=declaration){return null;}
    Require(Active==null,"Nested exit diagnostic");
    Active=new Scope{Builder=builder,Start=builder.Commands.Count,Dependencies=dependencies};Scopes.Add(Active);return Active;
  }
  public static void Prepared(Scope scope,bool pure){if(scope!=null){Require(ReferenceEquals(scope,Active)&&pure,"Uncertified exit preparation");scope.Pure=pure;scope.Preparations++;}}
  public static void Finish(Scope scope){if(scope!=null){Require(ReferenceEquals(scope,Active),"Wrong active exit");var commands=scope.Builder.Commands.Skip(scope.Start).ToList();Require(commands.All(c=>c is Bpl.Cmd),"Structured exit fragment");scope.Commands=commands.Cast<Bpl.Cmd>().ToList();Active=null;}}
  private static Bpl.QKeyValue WithoutId(Bpl.QKeyValue attributes) {
    if(attributes==null){return null;}var rest=WithoutId(attributes.Next);
    return attributes.Key=="id"?rest:new Bpl.QKeyValue(attributes.tok,attributes.Key,attributes.Params.ToList(),rest);
  }
  private static object Fingerprint(Bpl.AssertCmd command)=>new{expression=NativeTerminalExitsFingerprint.Expression(command.Expr),attributes=NativeTerminalExitsFingerprint.Attributes(WithoutId(command.Attributes),new NativeTerminalExitsFingerprint.BindingScope()),line=command.tok.line,col=command.tok.col};
  private static void FreshDependency(Bpl.Cmd original,Bpl.Cmd copy,ProofDependencyManager manager) {
    if(original is not Bpl.ICarriesAttributes source || copy is not Bpl.ICarriesAttributes target){return;}
    var ids=new List<string>();for(var a=source.Attributes;a!=null;a=a.Next){if(a.Key=="id"){Require(a.Params.Count==1&&a.Params[0] is string,"Malformed source dependency id");ids.Add((string)a.Params[0]);}}
    Require(ids.Count<=1,"Multiple source dependency ids");if(ids.Count==0){return;}
    Require(manager!=null&&original.tok is IOrigin&&manager.ProofDependenciesById.ContainsKey(ids[0]),"Untracked exit dependency");
    var dependency=manager.ProofDependenciesById[ids[0]];
    ProofDependency fresh=dependency switch {
      ProofObligationDependency proof=>new ProofObligationDependency(original.tok,proof.ProofObligation),
      AssumptionDependency assumption when assumption.Description=="checked method postcondition"=>new AssumptionDependency(assumption.WarnWhenUnused,assumption.Description,assumption.Expr),
      _=>throw new InvalidOperationException("Unsupported exit dependency type")
    };
    Require(!ReferenceEquals(fresh,dependency)&&fresh.GetType()==dependency.GetType()&&fresh.Range.Equals(dependency.Range)&&fresh.Description==dependency.Description,"Exit dependency payload changed");
    if(fresh is ProofObligationDependency fp && dependency is ProofObligationDependency op){Require(ReferenceEquals(fp.ProofObligation,op.ProofObligation),"Changed proof description");}
    if(fresh is AssumptionDependency fa && dependency is AssumptionDependency oa){Require(ReferenceEquals(fa.Expr,oa.Expr)&&fa.WarnWhenUnused==oa.WarnWhenUnused,"Changed summary source or policy");}
    var member=manager.idsByMemberName.Single(kv=>kv.Value.Deps.Contains(dependency));var count=member.Value.Deps.Count;target.Attributes=WithoutId(source.Attributes);
    var saved=manager.NativeDiagnosticDefinitionContext;
    try{manager.SetCurrentDefinition(member.Key,member.Value.Decl);manager.AddProofDependencyId(target,(IOrigin)original.tok,fresh);}
    finally{manager.SetCurrentDefinition(saved.Name,saved.Declaration);}
    var id=(string)target.Attributes.Params.Single();
    Require(id!=ids[0]&&ReferenceEquals(manager.ProofDependenciesById[id],fresh)&&member.Value.Deps.Count==count+1&&member.Value.Deps.Contains(fresh),"Fresh exit dependency merged or changed member");
    Require(manager.NativeDiagnosticDefinitionContext.Equals(saved),"Dependency context not restored");
    FreshDependencies.Add(new{original=ids[0],copy=id,dependencyType=fresh.GetType().Name,description=fresh.Description,line=fresh.Range.StartToken.line,col=fresh.Range.StartToken.col,distinctCoverageEntry=true,originalSourcePayloadRetained=true});
  }
  private static string Key(Bpl.Cmd command) {
    // Duplicator must preserve the full expression, source origin and attributes of every exit command.
    return command switch {
      Bpl.AssertCmd a=>"assert:"+JsonSerializer.Serialize(Fingerprint(a)),
      Bpl.AssumeCmd a=>"assume:"+NativeTerminalExitsFingerprint.Expression(a.Expr)+":"+NativeTerminalExitsFingerprint.Attributes(WithoutId(a.Attributes),new NativeTerminalExitsFingerprint.BindingScope())+":"+a.tok.line+":"+a.tok.col,
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
        var duplicator=new Bpl.Duplicator();var copy=copies.Count==0?fragment:fragment.Select(c=>c is Bpl.CommentCmd ? (Bpl.Cmd)c.Clone() : (Bpl.Cmd)duplicator.Visit(c)).ToList();
        if(copies.Count!=0){foreach(var pair in fragment.Zip(copy)){FreshDependency(pair.First,pair.Second,scope.Dependencies);}}
        Require(fragment.Select(Key).SequenceEqual(copy.Select(Key)),"Changed relocated exit command metadata or formula");
        predecessor.Cmds.AddRange(copy);predecessor.TransferCmd=(Bpl.TransferCmd)new Bpl.Duplicator().Visit(sink.TransferCmd);copies.Add(copy);
      }
      impl.Blocks.Remove(sink);impl.StructuredStmts=null;
    }
    var after=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    Require(before.Where(c=>!fragment.Contains(c)).All(c=>after.Count(x=>ReferenceEquals(x,c))==1),"Other body command missing or duplicated");
    Require(impl.Blocks.Where(b=>!relocated||!predecessors.Contains(b)).All(b=>ReferenceEquals(b.TransferCmd,oldTransfers[b])),"Other branch transfer changed");
    Require(relocated ? copies.All(copy=>copy.All(c=>after.Count(x=>ReferenceEquals(x,c))==1)) : before.SequenceEqual(after,ReferenceEqualityComparer.Instance),"Exit fragment emission mismatch");
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
    Require(keys.Distinct().OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(originalKeys.Distinct().OrderBy(x=>x,StringComparer.Ordinal)),"Actual check formula, origin or non-id attributes changed");
    Require(checks.Length==originalKeys.Length+(relocated?1:0),"Only the terminal check may occur at both exits");
    var negative=parts[0]=="false-terminal-entry"?new Bpl.AssertCmd(impl.tok,Bpl.Expr.False):null;if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);}
    Require(FreshDependencies.Count==(relocated?2:0),"Unexpected fresh dependency count");
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit output");
    File.WriteAllText(path,JsonSerializer.Serialize(new{target=parts[1],variant=parts[0],exitPreparations=scope.Preparations,certifiedPureExitPreparation=scope.Pure,allOriginalPreparationAndFuelTraversalRetained=true,allExitFragmentExpressionsOriginsAndNonIdAttributesRetainedAtEachPath=true,distinctDependencyPayloadsAndCoverageEntriesRetained=true,freshDependencyIds=FreshDependencies,normalPublicationAndProcedureContractsRetained=true,allOtherBodyCommandObjectsRetainedOnce=true,allOtherBranchTransfersRetained=true,terminalPredecessors=predecessors.Count,actualExitPathCheckCounts=pathCounts,mandatoryChecksPerExit=1,relocated,actualCheckFingerprints=checks,uniqueActualCheckFingerprints=keys.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray(),negativeEntryAdded=negative!=null,productPolicy=false})+"\n");
  }
  private static void Require(bool value,string message){if(!value){throw new InvalidOperationException(message);}}
}
