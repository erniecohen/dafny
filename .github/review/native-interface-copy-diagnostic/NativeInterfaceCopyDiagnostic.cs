using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;
// Scratch-only reuse of the already translated call specification.
public static class NativeInterfaceCopyDiagnostic {
  private static readonly HashSet<Bpl.Requires> UserRequirements = new(ReferenceEqualityComparer.Instance);
  private static readonly List<(Bpl.Procedure Original, Bpl.Procedure Copy)> Copies = new();
  private static int FreshDependencies;
  private static readonly Dictionary<Bpl.ICarriesAttributes, Func<ProofDependency>> DependencyFactories = new(ReferenceEqualityComparer.Instance);
  public static void DependencyFactory(Bpl.ICarriesAttributes node, Func<ProofDependency> factory) {
    if (Selection()!=null) { Require(DependencyFactories.TryAdd(node,factory),"Repeated source dependency factory"); }
  }
  private static string[] Selection() {
    var value=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_INTERFACE_COPY");
    if (value==null) { return null; }
    var parts=value.Split(':');
    Require(parts.Length==2 && new[] {"native-control","copied-interface","false-copy-entry"}.Contains(parts[0]) && new[] {"Composite","RemoveFactor","FormArmy"}.Contains(parts[1]),"Unknown interface copy selection");
    return parts;
  }
  public static bool Enabled => Selection() is { } parts && parts[0]!="native-control";
  public static void User(Bpl.Requires requirement, bool locallyChecked) {
    if (locallyChecked && Selection()!=null) { UserRequirements.Add(requirement); }
  }
  private static Bpl.QKeyValue WithoutId(Bpl.QKeyValue attribute) {
    if (attribute==null) { return null; }
    var rest=WithoutId(attribute.Next);
    return attribute.Key=="id" ? rest : new Bpl.QKeyValue(attribute.tok,attribute.Key,attribute.Params.ToList(),rest);
  }
  private static Bpl.QKeyValue FreshAttributes(Bpl.ICarriesAttributes original, Bpl.ICarriesAttributes copy, IOrigin tok, ProofDependencyManager manager) {
    copy.Attributes=WithoutId(original.Attributes);
    var identifiers=new List<string>();
    for (var a=original.Attributes;a!=null;a=a.Next) {
      if (a.Key!="id") { continue; }
      Require(a.Params.Count==1 && a.Params[0] is string,"Unexpected dependency attribute"); identifiers.Add((string)a.Params[0]);
    }
    Require(identifiers.Count<=1,"Repeated dependency id");
    foreach (var id in identifiers) {
      Require(manager!=null && manager.ProofDependenciesById.ContainsKey(id),"Untracked original dependency");
      var dependency=manager.ProofDependenciesById[id];
      Require(DependencyFactories.TryGetValue(original,out var factory),"Missing source dependency factory");
      var copiedDependency=factory();
      Require(!ReferenceEquals(copiedDependency,dependency) && copiedDependency.GetType()==dependency.GetType() &&
        copiedDependency.Range.Equals(dependency.Range) && copiedDependency.Description==dependency.Description,"Source dependency payload changed");
      var memberSets=manager.idsByMemberName.Values.Where(v=>v.Deps.Contains(dependency)).Select(v=>v.Deps).ToArray();
      Require(memberSets.Length==1,"Ambiguous original dependency coverage set");
      var priorCount=memberSets[0].Count;
      manager.AddProofDependencyId(copy,tok,copiedDependency); FreshDependencies++;
      var fresh=(string)copy.Attributes.Params.Single();
      Require(fresh!=id && ReferenceEquals(manager.ProofDependenciesById[fresh],copiedDependency) &&
        memberSets[0].Contains(copiedDependency) && memberSets[0].Count==priorCount+1,"Copied dependency entry merged or changed");
    }
    return copy.Attributes;
  }
  public static Bpl.Procedure Copy(Bpl.Procedure original, ProofDependencyManager manager) {
    Require(Enabled,"Unexpected copy construction");
    var requires=new List<Bpl.Requires>();
    foreach (var r in original.Requires) {
      Require(r.tok is IOrigin,"Unsupported requirement origin");
      var copy=new Bpl.Requires(r.tok,r.Free,r.Condition,r.Comment,null) { Description=r.Description };
      FreshAttributes(r,copy,(IOrigin)r.tok,manager);
      if (UserRequirements.Contains(r)) {
        Require(r.Free,"Duplicated checked requirement");
        copy.Attributes=BoogieGenerator.AlwaysAssumeAttribute((IOrigin)r.tok,copy.Attributes);
      }
      requires.Add(copy);
    }
    var ensures=new List<Bpl.Ensures>();
    foreach (var e in original.Ensures) {
      Require(e.tok is IOrigin,"Unsupported ensures origin");
      var copy=new Bpl.Ensures(e.tok,e.Free,e.Condition,e.Comment,null) { Description=e.Description };
      FreshAttributes(e,copy,(IOrigin)e.tok,manager); ensures.Add(copy);
    }
    var result=new Bpl.Procedure(original.tok,"$CheckedRequires$"+original.Name,original.TypeParameters,
      original.InParams,original.OutParams,false,requires,original.Modifies,ensures,original.Attributes);
    Require(result.InParams.Zip(original.InParams).All(p=>ReferenceEquals(p.First,p.Second)) &&
      result.OutParams.Zip(original.OutParams).All(p=>ReferenceEquals(p.First,p.Second)),"Formal binding changed");
    Require(result.Requires.Zip(original.Requires).All(p=>ReferenceEquals(p.First.Condition,p.Second.Condition) && p.First.Free==p.Second.Free && ReferenceEquals(p.First.Description,p.Second.Description)),"Requires formula/description changed");
    Require(result.Ensures.Zip(original.Ensures).All(p=>ReferenceEquals(p.First.Condition,p.Second.Condition) && p.First.Free==p.Second.Free && ReferenceEquals(p.First.Description,p.Second.Description)),"Ensures formula/description changed");
    Copies.Add((original,result));return result;
  }
  public static void Apply(Bpl.Program program) {
    var parts=Selection();if(parts==null){return;}
    var implementations=program.TopLevelDeclarations.OfType<Bpl.Implementation>().Where(i=>i.Name=="Impl$$_module.__default."+parts[1]).ToList();
    if(implementations.Count==0){return;}Require(implementations.Count==1,"Ambiguous target");
    var impl=implementations.Single();var commands=impl.Blocks.SelectMany(b=>b.Cmds).ToList();
    var negative=parts[0]=="false-copy-entry" ? new Bpl.AssertCmd(impl.tok,Bpl.Expr.False) : null;
    if(negative!=null){impl.Blocks.First().Cmds.Insert(0,negative);commands.Insert(0,negative);}
    Require(Enabled ? Copies.Count>0 : Copies.Count==0,"Unexpected copied interfaces");
    var checks=commands.OfType<Bpl.AssertCmd>().Where(c=>!ReferenceEquals(c,negative)).Select(c=>new {
      expression=NativeInterfaceCopyFingerprint.Expression(c.Expr),
      attributes=NativeInterfaceCopyFingerprint.Attributes(c.Attributes,new NativeInterfaceCopyFingerprint.BindingScope()),
      line=c.tok.line,col=c.tok.col
    }).OrderBy(c=>c.line).ThenBy(c=>c.col).ThenBy(c=>c.expression,StringComparer.Ordinal).ThenBy(c=>c.attributes,StringComparer.Ordinal).ToArray();
    var path=Environment.GetEnvironmentVariable("OBLIGATION_DIAGNOSTIC_AUDIT");Require(!string.IsNullOrEmpty(path)&&!File.Exists(path),"Missing/repeated audit destination");
    File.WriteAllText(path,JsonSerializer.Serialize(new {
      target=parts[1],variant=parts[0],copiedInterfaces=Copies.Count,freshDependencyIds=FreshDependencies,
      originalContractExpressionsAndFormalsRetainedByIdentity=true,sourceDependenciesAndDescriptionsRetained=true,distinctDependencyEntriesRetained=true,
      oldAllocationRequirementPublicationUnchanged=true,ordinaryUserRequirementsRemainNonchecking=true,
      actualCallerAndExitLoweringUnchanged=true,negativeEntryAdded=negative!=null,
      actualCheckFingerprints=checks,actualChecks=checks.Length,
      interfaces=Copies.Select(p=>new { original=p.Original.Name,copy=p.Copy.Name,requires=p.Copy.Requires.Count,ensures=p.Copy.Ensures.Count,
        markedUserRequirements=p.Original.Requires.Count(UserRequirements.Contains) }).ToArray()
    })+"\n");
  }
  private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
}
