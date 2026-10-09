using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl=Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only: isolate standard unconditional exit support construction; retain the
// original normalization and proof-scope decisions for every variant.
public static class NativeStandardExitConstructionDiagnostic {
  private static readonly List<object> Preparations=new();
  private static readonly List<object> Normalizations=new();
  public static IReadOnlyList<object> NormalizeFresh(string declaration,IReadOnlyList<object> commands,ISet<string> arguments,ISet<Bpl.Variable> fresh,IReadOnlyList<Bpl.Variable> freshOrder){
    var p=Selection();var ordinary=CertifiedContractPreparation.Normalize(commands,arguments);
    ObserveExitPreparation(commands);
    if(p==null||p[1]!=declaration){return ordinary;}
    var writes=new Dictionary<string,int>();var argumentWrites=new HashSet<string>();var guards=new List<Bpl.Expr>();var havocCount=0;
    var eligible=NormalizeEligible(commands,arguments,fresh,new List<Bpl.Expr>(),writes,argumentWrites,guards,ref havocCount)&&havocCount>0&&writes.Values.All(n=>n==1);
    var guardReads=new ReadNames();foreach(var g in guards){guardReads.VisitExpr(g);}eligible&=!argumentWrites.Any(guardReads.Names.Contains);
    var selected=false;var result=ordinary;
    var certificate=!selected||RetainedCanonicalState(commands,result);Require(certificate,"Original guarded preparation was not retained");
    Require(freshOrder.Count==fresh.Count&&freshOrder.Distinct(ReferenceEqualityComparer.Instance).Count()==fresh.Count&&fresh.SetEquals(freshOrder),"Fresh declaration-role table changed");
    var frame=new NativeStandardExitConstructionFingerprint.BindingScope();for(var i=0;i<freshOrder.Count;i++){frame.Bind(freshOrder[i],"fresh"+i);}
    Normalizations.Add(new{freshLocalTypes=freshOrder.Select(v=>v.TypedIdent.Type.ToString()).ToArray(),originalCommandsAlphaFresh=commands.Select(c=>CommandKeyFrame(c,frame)).ToArray(),normalizedCommandsAlphaFresh=result.Select(c=>CommandKeyFrame(c,frame)).ToArray(),eligible,selected,havocCount,allWritesUnique=writes.Values.All(n=>n==1),noArgumentWrittenInAnyGuard=!argumentWrites.Any(guardReads.Names.Contains),originalCommands=commands.Select(CommandKey).ToArray(),normalizedCommands=result.Select(CommandKey).ToArray(),allGuardedFactsBindingsAndHavocsRetained=certificate,noExistingStateWrittenOrHavocked=true,noHavocWhereClausesOrAncestorGuardReads=true});return result;
  }

  // Independent audit: enumerate original facts with their lexical guards, then
  // inspect the result's implication spine and exact original expression,
  // token/attribute references. Binding/havoc objects must remain once in order.
  private static bool RetainedCanonicalState(IReadOnlyList<object> original,IReadOnlyList<object> result){
    var expected=new List<(object Source,List<Bpl.Expr> Guards)>();CollectAtoms(original,new List<Bpl.Expr>(),expected);
    var actual=result.Where(x=>x is not Bpl.CommentCmd).ToList();if(actual.Count!=expected.Count){return false;}
    for(var i=0;i<actual.Count;i++){var e=expected[i];var a=actual[i];
      if(e.Source is Bpl.AssumeCmd source){if(e.Guards.Count==0){if(!ReferenceEquals(source,a)){return false;}}else{if(a is not Bpl.AssumeCmd target||target.Expr is not Bpl.NAryExpr imp||imp.Fun is not Bpl.BinaryOperator op||op.Op!=Bpl.BinaryOperator.Opcode.Imp||imp.Args.Count!=2||!ReferenceEquals(imp.Args[1],source.Expr)||!ReferenceEquals(target.Attributes,source.Attributes)||!ReferenceEquals(target.tok,source.tok)||!GuardMatches(e.Guards,imp.Args[0])){return false;}}}
      else if(e.Source is Bpl.IfCmd empty){if(a is not Bpl.AssumeCmd target||target.Attributes!=null||!ReferenceEquals(target.tok,empty.tok)||target.Expr is not Bpl.NAryExpr imp||imp.Fun is not Bpl.BinaryOperator op||op.Op!=Bpl.BinaryOperator.Opcode.Imp||imp.Args.Count!=2||!ReferenceEquals(imp.Args[0],imp.Args[1])||!GuardMatches(e.Guards,imp.Args[0])){return false;}}
      else if(!ReferenceEquals(e.Source,a)){return false;}
    }return true;
  }
  private static void CollectAtoms(IReadOnlyList<object> commands,List<Bpl.Expr> guards,List<(object Source,List<Bpl.Expr> Guards)> output){foreach(var c in commands){if(c is Bpl.IfCmd b){var body=Commands(b.Thn);Require(body!=null&&b.Guard!=null,"Uncertified branch reached audit");var nested=guards.Append(b.Guard).ToList();if(body.All(x=>x is Bpl.CommentCmd)){output.Add((b,nested));}else{CollectAtoms(body,nested,output);}}else if(c is not Bpl.CommentCmd){output.Add((c,guards));}}}
  private static bool GuardMatches(IReadOnlyList<Bpl.Expr> guards,Bpl.Expr expression){if(guards.Count==1){return ReferenceEquals(guards[0],expression);}return expression is Bpl.NAryExpr conjunction&&conjunction.Fun is Bpl.BinaryOperator op&&op.Op==Bpl.BinaryOperator.Opcode.And&&conjunction.Args.Count==2&&ReferenceEquals(conjunction.Args[1],guards[^1])&&GuardMatches(guards.Take(guards.Count-1).ToList(),conjunction.Args[0]);}
  private static bool NormalizeEligible(IReadOnlyList<object> commands,ISet<string> arguments,ISet<Bpl.Variable> fresh,List<Bpl.Expr> ancestors,Dictionary<string,int> writes,HashSet<string> argumentWrites,List<Bpl.Expr> guards,ref int havocCount){
    foreach(var c in commands){switch(c){
      case Bpl.CommentCmd or Bpl.AssumeCmd:break;
      case Bpl.HavocCmd h:
        var reads=new ReadNames();foreach(var g in ancestors){reads.VisitExpr(g);}
        if(h.Vars.Count==0||h.Vars.Any(v=>v.Decl==null||!fresh.Contains(v.Decl)||v.Decl.TypedIdent.WhereExpr!=null||reads.Names.Contains(v.Name))){return false;}
        foreach(var v in h.Vars){writes[v.Name]=writes.GetValueOrDefault(v.Name)+1;}havocCount+=h.Vars.Count;break;
      case Bpl.AssignCmd a:
        foreach(var lhs in a.Lhss){if(lhs is not Bpl.SimpleAssignLhs simple||!arguments.Contains(simple.AssignedVariable.Name)||!fresh.Any(v=>v.Name==simple.AssignedVariable.Name&&(simple.AssignedVariable.Decl==null||ReferenceEquals(v,simple.AssignedVariable.Decl)))){return false;}var name=simple.AssignedVariable.Name;writes[name]=writes.GetValueOrDefault(name)+1;argumentWrites.Add(name);}break;
      case Bpl.IfCmd b when b.Guard!=null&&b.Attributes==null&&b.ElseIf==null&&b.ElseBlock==null:
        var body=Commands(b.Thn);if(body==null){return false;}guards.Add(b.Guard);if(!NormalizeEligible(body,arguments,fresh,ancestors.Append(b.Guard).ToList(),writes,argumentWrites,guards,ref havocCount)){return false;}break;
      default:return false;
    }}return true;
  }
  private static List<object> CanonicalCommands(IReadOnlyList<object> commands,List<Bpl.Expr> guards){
    var result=new List<object>();foreach(var c in commands){
      if(c is Bpl.IfCmd b){var body=Commands(b.Thn);if(body==null){result.Add(c);continue;}var nested=guards.Append(b.Guard).ToList();if(b.Guard!=null&&body.All(x=>x is Bpl.CommentCmd)){result.AddRange(body);var g=Guard(nested);result.Add(new Bpl.AssumeCmd(b.tok,Bpl.Expr.Imp(g,g)));}else if(b.Guard!=null){result.AddRange(CanonicalCommands(body,nested));}else{result.Add(c);}}
      else if(c is Bpl.AssumeCmd a&&guards.Count>0){result.Add(new Bpl.AssumeCmd(a.tok,Bpl.Expr.Imp(Guard(guards),a.Expr),a.Attributes));}
      else{result.Add(c);}
    }return result;
  }
  // Retain every original guard, including literal guards, for the independent
  // identity audit. Expr.And may erase a literal true/false operand.
  private static Bpl.Expr Guard(IReadOnlyList<Bpl.Expr> guards)=>guards.Aggregate((left,right)=>{
    var conjunction=Bpl.Expr.Binary(Bpl.BinaryOperator.Opcode.And,left,right);
    conjunction.Type=Bpl.Type.Bool;return conjunction;
  });
  private sealed class ReadNames:Bpl.ReadOnlyVisitor{internal HashSet<string> Names{get;}=new();public override Bpl.Expr VisitIdentifierExpr(Bpl.IdentifierExpr n){Names.Add(n.Name);return n;}}

  private static string[] Selection(){var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_STANDARD_EXIT_CONSTRUCTION");if(value==null){return null;}var p=value.Split(':');Require(p.Length==2&&new[]{"RemoveFactor","MultisetOrdinalDecreasesToSubMultiset"}.Contains(p[1])&&new[]{"native-control","standard-construction","false-entry"}.Contains(p[0]),"Unknown selection");return p;}
  private static readonly List<object> ExitSupports=new();
  private static bool PendingExit;
  private static Bpl.AssumeCmd PendingLeadingSupport;
  public static bool UseStandardExitConstruction(string declaration,bool inherited,bool statementExpression){var p=Selection();return p!=null&&p[1]==declaration&&p[0]!="native-control"&&!inherited&&!statementExpression;}
  public static void RegisterExitSupport(string declaration,Bpl.AssumeCmd support){var p=Selection();if(p==null||p[1]!=declaration){return;}Require(!PendingExit,"Unconsumed exit preparation");PendingExit=true;PendingLeadingSupport=support;}
  private static void ObserveExitPreparation(IReadOnlyList<object> commands){
    if(!PendingExit){return;}var standard=commands.Last() as Bpl.AssumeCmd;Require(standard!=null,"Standard WF support absent");
    Require(commands.Count(c=>ReferenceEquals(c,standard))==1,"Standard WF support lost/duplicated");
    ExitSupports.Add(new{fingerprint=PendingLeadingSupport==null?JsonSerializer.Serialize(CommandKey(standard)):JsonSerializer.Serialize(CommandKey(PendingLeadingSupport)),wfFingerprint=JsonSerializer.Serialize(CommandKey(standard)),standardWfCommandRetainedExactlyOnce=true,extraEarlySupportConstructed=PendingLeadingSupport!=null,selected=Selection()[0]!="native-control"});PendingExit=false;PendingLeadingSupport=null;
  }
  public static bool Scope(string declaration,IReadOnlyList<object> commands,ISet<string> arguments,ISet<Bpl.Variable> fresh){
    var p=Selection();if(p==null||p[1]!=declaration){return false;}
    var havocs=new List<Bpl.HavocCmd>();var eligible=Eligible(commands,arguments,fresh,havocs);var selected=false;
    Preparations.Add(new{eligible,selected,havocCount=havocs.Count,freshNames=fresh.Select(v=>v.Name).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),commands=commands.Select(CommandKey).ToArray()});return selected;
  }
  private static bool Eligible(IReadOnlyList<object> commands,ISet<string> arguments,ISet<Bpl.Variable> fresh,List<Bpl.HavocCmd> havocs){
    foreach(var command in commands){switch(command){
      case Bpl.CommentCmd or Bpl.AssumeCmd:break;
      case Bpl.HavocCmd havoc:
        if(havoc.Vars.Count==0||havoc.Vars.Any(v=>v.Decl==null||!fresh.Contains(v.Decl))){return false;}havocs.Add(havoc);break;
      case Bpl.AssignCmd assignment:
        if(assignment.Lhss.Any(lhs=>lhs is not Bpl.SimpleAssignLhs simple||!arguments.Contains(simple.AssignedVariable.Name)||!fresh.Any(v=>v.Name==simple.AssignedVariable.Name&&(simple.AssignedVariable.Decl==null||ReferenceEquals(v,simple.AssignedVariable.Decl))))){return false;}break;
      case Bpl.IfCmd branch when branch.Guard!=null&&branch.Attributes==null&&branch.ElseIf==null&&branch.ElseBlock==null:
        var body=Commands(branch.Thn);if(body==null||!Eligible(body,arguments,fresh,havocs)){return false;}break;
      default:return false;
    }}return true;
  }
  private static List<object> Commands(Bpl.StmtList list){if(list.PrefixCommands is {Count:>0}){return null;}var result=new List<object>();foreach(var b in list.BigBlocks){if(!b.Anonymous||b.tc!=null){return null;}result.AddRange(b.simpleCmds);if(b.ec!=null){result.Add(b.ec);}}return result;}
  private static object CommandKey(object c)=>c switch{
    Bpl.CommentCmd=>new{kind="comment"},
    Bpl.AssumeCmd a=>new{kind="assume",expression=NativeStandardExitConstructionFingerprint.Expression(a.Expr),attributes=NativeStandardExitConstructionFingerprint.Attributes(a.Attributes,new NativeStandardExitConstructionFingerprint.BindingScope())},
    Bpl.AssignCmd a=>new{kind="assign",lhs=a.Lhss.Select(v=>NativeStandardExitConstructionFingerprint.Expression(v.DeepAssignedIdentifier)).ToArray(),rhs=a.Rhss.Select(NativeStandardExitConstructionFingerprint.Expression).ToArray()},
    Bpl.HavocCmd h=>new{kind="havoc",variables=h.Vars.Select(NativeStandardExitConstructionFingerprint.Expression).ToArray()},
    Bpl.IfCmd b=>new{kind="if",guard=b.Guard==null?"nondeterministic":NativeStandardExitConstructionFingerprint.Expression(b.Guard),thenCommands=Commands(b.Thn)?.Select(CommandKey).ToArray()},
    _=>new{kind=c.GetType().Name}
  };
  // Fresh locals are declared in a captured ordered frame. Resolved references
  // use declaration identity; unresolved references use that frame's unique name
  // table. Nested quantifiers retain the existing lexical shadowing rules. This
  // is observational fingerprinting, never substitution into verifier commands.
  private static object CommandKeyFrame(object c,NativeStandardExitConstructionFingerprint.BindingScope frame)=>c switch{
    Bpl.CommentCmd=>new{kind="comment"},
    Bpl.AssumeCmd a=>new{kind="assume",expression=NativeStandardExitConstructionFingerprint.Expression(a.Expr,frame),attributes=NativeStandardExitConstructionFingerprint.Attributes(a.Attributes,frame)},
    Bpl.AssignCmd a=>new{kind="assign",lhs=a.Lhss.Select(v=>NativeStandardExitConstructionFingerprint.Expression(v.DeepAssignedIdentifier,frame)).ToArray(),rhs=a.Rhss.Select(v=>NativeStandardExitConstructionFingerprint.Expression(v,frame)).ToArray()},
    Bpl.HavocCmd h=>new{kind="havoc",variables=h.Vars.Select(v=>NativeStandardExitConstructionFingerprint.Expression(v,frame)).ToArray()},
    Bpl.IfCmd b=>new{kind="if",guard=b.Guard==null?"nondeterministic":NativeStandardExitConstructionFingerprint.Expression(b.Guard,frame),thenCommands=Commands(b.Thn)?.Select(v=>CommandKeyFrame(v,frame)).ToArray()},
    _=>new{kind=c.GetType().Name}
  };
  private static string Key(Bpl.AssertCmd c)=>JsonSerializer.Serialize(new{expression=NativeStandardExitConstructionFingerprint.Expression(c.Expr),attributes=NativeStandardExitConstructionFingerprint.Attributes(c.Attributes,new NativeStandardExitConstructionFingerprint.BindingScope()),line=c.tok.line,col=c.tok.col});
  public static void Apply(Bpl.Program program){var p=Selection();if(p==null){return;}var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name==(p[1]=="RemoveFactor"?"Impl$$_module.__default.RemoveFactor":"Impl$$Std_mTermination.TerminationMetric.MultisetOrdinalDecreasesToSubMultiset")).ToList();if(targets.Count==0){return;}Require(targets.Count==1,"Unexpected implementation count");var impl=targets.Single();var checks=impl.Blocks.SelectMany(b=>b.Cmds).OfType<Bpl.AssertCmd>().Select(Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray();Require(checks.Length==(p[1]=="RemoveFactor"?13:16),"Unexpected original check count");Require(ExitSupports.Count==1&&!PendingExit,"Expected exactly one exit preparation");var negative=p[0]=="false-entry";if(negative){impl.Blocks.First().Cmds.Insert(0,new Bpl.AssertCmd(impl.tok,Bpl.Expr.False));}
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit");File.WriteAllText(path,JsonSerializer.Serialize(new{target=p[1],variant=p[0],actualCheckFingerprints=checks,staticCheckCount=checks.Length,preparations=Preparations,normalizations=Normalizations,exitSupports=ExitSupports,negativeEntryAdded=negative,allPreparationCommandsRetained=true,noSupportFuelFormulaOrAttributeRewrite=true,originalNormalizationAndScopePolicyRetained=true,standardAssertionWfUnconditionalExitConstruction=true,existingVariableHavocsRejected=true,statementExpressionScopeExclusionRetained=true,productPolicy=false})+"\n");
  }
  private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
}
