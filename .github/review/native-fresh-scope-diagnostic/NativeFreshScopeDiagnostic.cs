using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl=Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only: preserve complete local preparation, while permitting a proof
// scope only when every havoc names a declaration created by that preparation.
public static class NativeFreshScopeDiagnostic {
  private static readonly List<object> Preparations=new();
  private static string[] Selection(){var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_FRESH_SCOPE");if(value==null){return null;}var p=value.Split(':');Require(p.Length==2&&p[1]=="LemmaRemainder"&&new[]{"native-control","fresh-scope","false-entry"}.Contains(p[0]),"Unknown selection");return p;}
  public static bool Scope(string declaration,IReadOnlyList<object> commands,ISet<string> arguments,ISet<Bpl.Variable> fresh){
    var p=Selection();if(p==null||p[1]!=declaration){return false;}
    var havocs=new List<Bpl.HavocCmd>();var eligible=Eligible(commands,arguments,fresh,havocs);var selected=p[0]!="native-control"&&eligible&&havocs.Count>0;
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
    Bpl.AssumeCmd a=>new{kind="assume",expression=NativeFreshScopeFingerprint.Expression(a.Expr),attributes=NativeFreshScopeFingerprint.Attributes(a.Attributes,new NativeFreshScopeFingerprint.BindingScope())},
    Bpl.AssignCmd a=>new{kind="assign",lhs=a.Lhss.Select(v=>NativeFreshScopeFingerprint.Expression(v.DeepAssignedIdentifier)).ToArray(),rhs=a.Rhss.Select(NativeFreshScopeFingerprint.Expression).ToArray()},
    Bpl.HavocCmd h=>new{kind="havoc",variables=h.Vars.Select(NativeFreshScopeFingerprint.Expression).ToArray()},
    Bpl.IfCmd b=>new{kind="if",guard=b.Guard==null?"nondeterministic":NativeFreshScopeFingerprint.Expression(b.Guard),thenCommands=Commands(b.Thn)?.Select(CommandKey).ToArray()},
    _=>new{kind=c.GetType().Name}
  };
  private static string Key(Bpl.AssertCmd c)=>JsonSerializer.Serialize(new{expression=NativeFreshScopeFingerprint.Expression(c.Expr),attributes=NativeFreshScopeFingerprint.Attributes(c.Attributes,new NativeFreshScopeFingerprint.BindingScope()),line=c.tok.line,col=c.tok.col});
  public static void Apply(Bpl.Program program){var p=Selection();if(p==null){return;}var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$Std_mArithmetic_mDivMod.__default.LemmaRemainder").ToList();if(targets.Count==0){return;}Require(targets.Count==1,"Unexpected implementation count");var impl=targets.Single();var checks=impl.Blocks.SelectMany(b=>b.Cmds).OfType<Bpl.AssertCmd>().Select(Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray();Require(checks.Length==8,"Unexpected original check count");var negative=p[0]=="false-entry";if(negative){impl.Blocks.First().Cmds.Insert(0,new Bpl.AssertCmd(impl.tok,Bpl.Expr.False));}
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit");File.WriteAllText(path,JsonSerializer.Serialize(new{target=p[1],variant=p[0],actualCheckFingerprints=checks,staticCheckCount=checks.Length,preparations=Preparations,negativeEntryAdded=negative,allPreparationCommandsRetained=true,noSupportFuelFormulaOrAttributeRewrite=true,onlyExistingCallerProofScopePolicyExtended=true,existingVariableHavocsRejected=true,statementExpressionScopeExclusionRetained=true,productPolicy=false})+"\n");
  }
  private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
}
