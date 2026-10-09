using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only causal omission of certified pure exit preparation. Not a product policy.
public static class NativeExitPreparationDiagnostic {
  public sealed class Scope {
    public BoogieStmtListBuilder Builder;
    public int Start, Preparations;
    public bool Pure;
    public List<object> OriginalPreparation = new();
    public List<Bpl.AssumeCmd> Allocation = new();
    public List<object> ExitCommands;
  }
  private static Scope Active;
  private static readonly List<Scope> Scopes = new();
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_EXIT_PREPARATION");
    if(value==null){return null;}
    var parts=value.Split(':');
    Require(parts.Length==2 && new[]{"native-control","without-exit-preparation","false-omitted-entry"}.Contains(parts[0]) && parts[1]=="RemoveFactor","Unknown exit-preparation selection");return parts;
  }
  public static Scope Begin(string declaration,BoogieStmtListBuilder builder) {
    var parts=Selection();if(parts==null || parts[1]!=declaration){return null;}
    Require(Active==null,"Nested exit diagnostic");
    Active=new Scope{Builder=builder,Start=builder.Commands.Count};Scopes.Add(Active);return Active;
  }
  public static IEnumerable<object> Filter(IReadOnlyList<object> normalized,HashSet<string> arguments,Bpl.Expr heap) {
    if(Active==null){return normalized;}
    Active.Preparations++;
    Require(Active.Preparations==1,"Unexpected exit preparation count");
    Active.OriginalPreparation=normalized.ToList();
    foreach(var command in normalized) {
      Require(command is Bpl.AssumeCmd or Bpl.AssignCmd or Bpl.CommentCmd,"Unsupported preparation command");
      if(command is Bpl.AssignCmd assignment) {
        foreach(var lhs in assignment.Lhss){Require(lhs is Bpl.SimpleAssignLhs simple && arguments.Contains(simple.AssignedVariable.Name),"Non-private preparation write");}
      }
      if(command is Bpl.AssumeCmd assumption && assumption.Expr is Bpl.NAryExpr application && application.Fun is Bpl.FunctionCall function && function.ToString()=="$IsAlloc") {
        Require(application.Args.Count==3 && application.Args[0] is Bpl.IdentifierExpr id && arguments.Contains(id.Name),"Non-private allocatedness argument");
        Require(NativeExitPreparationFingerprint.Expression(application.Args[2])==NativeExitPreparationFingerprint.Expression(heap),"Different allocation heap");
        Require(application.Args[1].ToString()=="TSet(TInt)","Unexpected allocatedness type");
        Require(!Active.Allocation.Contains(assumption),"Repeated allocation fact");Active.Allocation.Add(assumption);
      }
    }
    Require(Active.Allocation.Count==2,"Expected exactly two exit allocation facts");
    var omit=Selection()[0]!="native-control";
    var retained=normalized.Where(c=>!omit || c is Bpl.CommentCmd).ToList();
    Require(retained.Count==(omit?normalized.Count(c=>c is Bpl.CommentCmd):normalized.Count),"Changed omission boundary");return retained;
  }
  public static void Prepared(Scope scope,bool pure){if(scope!=null){Require(ReferenceEquals(scope,Active)&&pure,"Uncertified exit preparation");scope.Pure=pure;}}
  public static void Finish(Scope scope){if(scope!=null){Require(ReferenceEquals(scope,Active),"Wrong active exit");scope.ExitCommands=scope.Builder.Commands.Skip(scope.Start).ToList();Active=null;}}
  public static void Apply(Bpl.Program program) {
    var parts=Selection();if(parts==null){return;}
    var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$_module.__default.RemoveFactor").ToList();if(targets.Count==0){return;}
    Require(targets.Count==1 && Scopes.Count==1 && Active==null,"Unexpected target/exit count");
    var impl=targets.Single();var scope=Scopes.Single();var commands=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    var omit=parts[0]!="native-control";
    int Count(object c)=>commands.Count(x=>ReferenceEquals(c,x));
    Require(scope.OriginalPreparation.All(c=>Count(c)==(omit && c is not Bpl.CommentCmd?0:1)),"Preparation missing or duplicated beyond selected omission");
    Require(scope.ExitCommands.All(c=>Count(c)==1),"Other exit commands changed");
    Require(scope.Pure && scope.Preparations==1,"Incomplete preparation audit");
    if(omit) {
      var omittedVariables=scope.OriginalPreparation.OfType<Bpl.AssignCmd>().SelectMany(a=>a.Lhss).Cast<Bpl.SimpleAssignLhs>().Select(a=>a.AssignedVariable.Name).ToHashSet();
      var visitor=new IdentifierUses();
      foreach(var command in commands){visitor.Visit(command);}
      Require(!omittedVariables.Overlaps(visitor.Names),"Omitted binding escapes preparation");
      Require(impl.LocVars.Where(v=>omittedVariables.Contains(v.Name)).All(v=>v.TypedIdent.WhereExpr==null),"Omitted binding has where clause");
    }
    var negative=parts[0]=="false-omitted-entry"?new Bpl.AssertCmd(impl.tok,Bpl.Expr.False):null;
    var transfers=impl.Blocks.Select(b=>b.TransferCmd).ToList();
    if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);}
    var after=impl.Blocks.SelectMany(b=>b.Cmds).Where(c=>!ReferenceEquals(c,negative)).ToList();
    Require(commands.SequenceEqual(after,ReferenceEqualityComparer.Instance),"Post-translation commands changed");
    Require(transfers.SequenceEqual(impl.Blocks.Select(b=>b.TransferCmd),ReferenceEqualityComparer.Instance),"Transfers changed");
    var checks=commands.OfType<Bpl.AssertCmd>().Select(c=>new{expression=NativeExitPreparationFingerprint.Expression(c.Expr),attributes=NativeExitPreparationFingerprint.Attributes(c.Attributes,new NativeExitPreparationFingerprint.BindingScope()),line=c.tok.line,col=c.tok.col}).OrderBy(c=>c.line).ThenBy(c=>c.col).ThenBy(c=>c.expression,StringComparer.Ordinal).ThenBy(c=>c.attributes,StringComparer.Ordinal).ToArray();
    Require(scope.ExitCommands.OfType<Bpl.AssertCmd>().Count()==1,"Changed mandatory exit check count");
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit output");
    File.WriteAllText(path,JsonSerializer.Serialize(new{target=parts[1],variant=parts[0],exitScopes=1,exitPreparations=scope.Preparations,omittedAllocationFacts=omit?2:0,omittedPreparationCommands=omit?scope.OriginalPreparation.Count(c=>c is not Bpl.CommentCmd):0,allocationFingerprints=scope.Allocation.Select(a=>NativeExitPreparationFingerprint.Expression(a.Expr)).ToArray(),certifiedPureExitPreparation=scope.Pure,completeOriginalPreparationAndFuelTraversalRetained=true,allUnchangedExitCommandObjectsRetainedOnce=true,leadingSupportAndNormalPublicationRetained=true,mandatoryExitChecks=1,allExitChecksSummariesAndContractsRetained=true,allBranchTransfersRetained=true,actualCheckFingerprints=checks,negativeEntryAdded=negative!=null,productPolicy=false})+"\n");
  }
  private sealed class IdentifierUses : Bpl.ReadOnlyVisitor {
    public readonly HashSet<string> Names=new();
    public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr node){Names.Add(node.Name);return node;}
  }
  private static void Require(bool value,string message){if(!value){throw new InvalidOperationException(message);}}
}
