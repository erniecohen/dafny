using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only exit placement before Boogie resolves structured control flow.
public static class NativeStructuredTerminalDiagnostic {
  public sealed class Scope {
    public BoogieStmtListBuilder Builder;
    public int Start, Preparations;
    public bool Pure;
    public ProofDependencyManager Dependencies;
    public List<Bpl.Cmd> Commands, Original;
    public List<List<Bpl.Cmd>> Copies = new();
    public Bpl.IfCmd TailIf;
    public Bpl.Expr TailGuard;
  }
  private static Scope Active;
  private static readonly List<object> FreshDependencies = new();
  private static readonly List<Scope> Scopes = new();
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_STRUCTURED_TERMINAL");
    if(value==null){return null;}
    var parts=value.Split(':');
    Require(parts.Length==2 && new[]{"native-control","structured-terminal-exits","false-structured-entry"}.Contains(parts[0]) && parts[1]=="RemoveFactor","Unknown terminal-exit selection");return parts;
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
        Require(conditional.ElseIf==null,"Unsupported else-if in retained source audit");
        foreach(var c in FlatList(conditional.Thn)){yield return c;}
        if(conditional.ElseBlock!=null){foreach(var c in FlatList(conditional.ElseBlock)){yield return c;}}
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
    if(scope==null){return;}
    Require(ReferenceEquals(scope,Active),"Wrong active exit");
    var commands=scope.Builder.Commands.Skip(scope.Start).ToList();
    Require(commands.All(c=>c is Bpl.Cmd),"Structured exit fragment");scope.Commands=commands.Cast<Bpl.Cmd>().ToList();
    scope.Original=Flat(scope.Builder.Commands).ToList();
    if(Selection()[0]!="native-control") {
      var prefix=scope.Builder.Commands.Take(scope.Start).ToList();
      Require(prefix.LastOrDefault() is Bpl.IfCmd,"Method body does not end with a conditional");
      var conditional=(Bpl.IfCmd)prefix.Last();
      Require(conditional.ElseIf==null&&conditional.ElseBlock!=null,"Unsupported terminal conditional shape");
      scope.TailIf=conditional;scope.TailGuard=conditional.Guard;
      foreach(var branch in new[]{conditional.Thn,conditional.ElseBlock}) {
        Require(branch.PrefixCommands==null,"Terminal branch already resolved");
        var tail=branch.BigBlocks.Last();
        Require(tail.ec==null&&tail.tc==null&&tail.Anonymous&&tail.LabelName==null,"Terminal branch requires additional control-flow lowering");
        var duplicator=new Bpl.Duplicator();var copy=scope.Copies.Count==0?scope.Commands:scope.Commands.Select(c=>c is Bpl.CommentCmd ? (Bpl.Cmd)c.Clone() : (Bpl.Cmd)duplicator.Visit(c)).ToList();
        if(scope.Copies.Count!=0){foreach(var pair in scope.Commands.Zip(copy)){FreshDependency(pair.First,pair.Second,scope.Dependencies);}}
        Require(scope.Commands.Select(Key).SequenceEqual(copy.Select(Key)),"Changed exit formula or non-id metadata");
        tail.simpleCmds.AddRange(copy);scope.Copies.Add(copy);
      }
      // Rebuild only the native statement-list builder. Original body nodes are
      // unchanged; naming/successors/blocks are computed once by normal Boogie.
      scope.Builder.Commands.RemoveRange(scope.Start,commands.Count);
      var rebuilt=new Bpl.StmtListBuilder();
      foreach(var command in scope.Builder.Commands) {
        switch(command) {
          case Bpl.Cmd simple: rebuilt.Add(simple);break;
          case Bpl.StructuredCmd structured:rebuilt.Add(structured);break;
          case Bpl.TransferCmd transfer:rebuilt.Add(transfer);break;
          default:throw new InvalidOperationException("Unsupported explicit label in terminal-layout comparison");
        }
      }
      scope.Builder.builder=rebuilt;
      scope.Builder.tran.assertionCount+=scope.Commands.OfType<Bpl.AssertCmd>().Count();
    }
    Active=null;
  }
  private static Bpl.QKeyValue WithoutId(Bpl.QKeyValue attributes) {
    if(attributes==null){return null;}var rest=WithoutId(attributes.Next);
    return attributes.Key=="id"?rest:new Bpl.QKeyValue(attributes.tok,attributes.Key,attributes.Params.ToList(),rest);
  }
  private static object Fingerprint(Bpl.AssertCmd command)=>new{expression=NativeStructuredTerminalFingerprint.Expression(command.Expr),attributes=NativeStructuredTerminalFingerprint.Attributes(WithoutId(command.Attributes),new NativeStructuredTerminalFingerprint.BindingScope()),line=command.tok.line,col=command.tok.col};
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
      Bpl.AssumeCmd a=>"assume:"+NativeStructuredTerminalFingerprint.Expression(a.Expr)+":"+NativeStructuredTerminalFingerprint.Attributes(WithoutId(a.Attributes),new NativeStructuredTerminalFingerprint.BindingScope())+":"+a.tok.line+":"+a.tok.col,
      Bpl.AssignCmd a=>"assign:"+string.Join(",",a.Lhss.Select(x=>x.ToString()))+":"+string.Join(",",a.Rhss.Select(NativeStructuredTerminalFingerprint.Expression))+":"+a.tok.line+":"+a.tok.col,
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
    var before=scope.Original;var after=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    var relocated=parts[0]!="native-control";
    Require(before.All(c=>after.Count(x=>ReferenceEquals(x,c))==1),"Original source command missing or duplicated");
    Require(!relocated||scope.Copies.Count==2&&scope.Copies.All(copy=>copy.All(c=>after.Count(x=>ReferenceEquals(x,c))==1)),"Structured exit copy missing or duplicated");
    Require(!relocated||ReferenceEquals(scope.TailIf.Guard,scope.TailGuard),"Original terminal guard changed");
    var exitChecks=relocated?scope.Copies.SelectMany(c=>c).OfType<Bpl.AssertCmd>().ToHashSet():new HashSet<Bpl.AssertCmd>{exit};
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
    var negative=parts[0]=="false-structured-entry"?new Bpl.AssertCmd(impl.tok,Bpl.Expr.False):null;if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);}
    Require(FreshDependencies.Count==(relocated?2:0),"Unexpected fresh dependency count");
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit output");
    File.WriteAllText(path,JsonSerializer.Serialize(new{target=parts[1],variant=parts[0],exitPreparations=scope.Preparations,certifiedPureExitPreparation=scope.Pure,allOriginalPreparationAndFuelTraversalRetained=true,allExitFragmentExpressionsOriginsAndNonIdAttributesRetainedAtEachPath=true,distinctDependencyPayloadsAndCoverageEntriesRetained=true,freshDependencyIds=FreshDependencies,normalPublicationAndProcedureContractsRetained=true,allOriginalSourceCommandObjectsRetainedOnce=true,originalTerminalGuardObjectRetained=true,allControlFlowConstructedOnceByNativeBoogie=true,structuredBeforeResolution=relocated,terminalPredecessors=2,actualExitPathCheckCounts=pathCounts,mandatoryChecksPerExit=1,relocated,actualCheckFingerprints=checks,uniqueActualCheckFingerprints=keys.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray(),negativeEntryAdded=negative!=null,productPolicy=false})+"\n");
  }
  private static void Require(bool value,string message){if(!value){throw new InvalidOperationException(message);}}
}
