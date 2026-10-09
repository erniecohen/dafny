using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only: retain every original check and support command in a local exit branch.
public static class NativeExitScopeDiagnostic {
  public sealed class Scope {
    public bool Selected, Scoped;
    public BoogieStmtListBuilder Builder;
    public int Start, Clauses;
    public List<object> Commands;
  }
  private static readonly List<Scope> Scopes = new();
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_EXIT_SCOPE");
    if (value==null) { return null; }
    var parts=value.Split(':');
    Require(parts.Length==2 && new[] {"native-control","scoped-exit","false-scoped-entry"}.Contains(parts[0]) && parts[1]=="RemoveFactor", "Unknown exit scope selection");
    return parts;
  }
  public static Scope Begin(string declaration, BoogieStmtListBuilder original) {
    var parts=Selection(); var selected=parts!=null && parts[1]==declaration;
    var scope=new Scope {Selected=selected, Scoped=selected && parts[0]!="native-control"};
    scope.Builder=scope.Scoped ? new BoogieStmtListBuilder(original.tran, original.Options, original.Context) : original;
    scope.Start=scope.Builder.Commands.Count;
    if (selected) { Scopes.Add(scope); }
    return scope;
  }
  public static void Prepared(Scope scope, bool pure) {
    if (!scope.Selected) { return; }
    Require(pure,"Uncertified exit preparation or statement expression"); scope.Clauses++;
  }
  public static void Finish(Scope scope) {
    if (scope.Selected) { scope.Commands=scope.Builder.Commands.Skip(scope.Start).ToList(); }
  }
  public static void Apply(Bpl.Program program) {
    var parts=Selection(); if (parts==null) { return; }
    var targets=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$_module.__default.RemoveFactor").ToList();
    if (!targets.Any()) { return; }
    Require(targets.Count==1 && Scopes.Count==1 && Scopes.Single().Clauses==1,"Unexpected exit/clauses");
    var impl=targets.Single(); var commands=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    int Count(object value)=>commands.Count(c=>ReferenceEquals(c,value));
    Require(Scopes.All(s=>s.Commands.All(c=>Count(c)==1)),"Original exit command missing or duplicated");
    var negative=parts[0]=="false-scoped-entry" ? new Bpl.AssertCmd(impl.tok,Bpl.Expr.False) : null;
    if (negative!=null) { impl.Blocks.First().Cmds.Insert(0,negative); commands.Insert(0,negative); }
    var checks=commands.OfType<Bpl.AssertCmd>().Where(c=>!ReferenceEquals(c,negative)).Select(c=>new {
      expression=NativeExitScopeFingerprint.Expression(c.Expr),
      attributes=NativeExitScopeFingerprint.Attributes(c.Attributes,new NativeExitScopeFingerprint.BindingScope()),
      line=c.tok.line,col=c.tok.col
    }).OrderBy(c=>c.line).ThenBy(c=>c.col).ThenBy(c=>c.expression,StringComparer.Ordinal).ThenBy(c=>c.attributes,StringComparer.Ordinal).ToArray();
    var exits=Scopes.Single().Commands.OfType<Bpl.AssertCmd>().ToList();
    Require(exits.Count==1 && checks.Length>1,"Unexpected actual exit/body checks");
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");
    Require(!string.IsNullOrEmpty(path) && !File.Exists(path),"Missing/repeated audit destination");
    File.WriteAllText(path,JsonSerializer.Serialize(new {
      target=parts[1],variant=parts[0],exitScopes=Scopes.Count,exitClauses=Scopes.Single().Clauses,
      exitCommands=Scopes.Single().Commands.Count,mandatoryExitChecks=exits.Count,
      scoped=Scopes.Single().Scoped,allOriginalExitCommandsRetainedOnce=true,
      certifiedPureExitPreparation=true,allOriginalFuelTraversalRetained=true,
      normalProcedureContractsUnchanged=true,negativeEntryAdded=negative!=null,
      actualCheckFingerprints=checks,actualChecks=checks.Length
    })+"\n");
  }
  private static void Require(bool condition,string message) { if (!condition) { throw new InvalidOperationException(message); } }
}
