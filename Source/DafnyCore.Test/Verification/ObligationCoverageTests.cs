using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DafnyCore.Test.Verification;

public class ObligationCoverageTests {
  private static readonly HashSet<string> Sinks = new() {
    "Assert", "AssertAndForget", "AssertMethodContract", "CheckContractFuelLayers", "CheckContractBodyEqualities", "PublishContractProofCut", "AssertNS", "AssertCmd", "AssertSink", "Requires", "Ensures",
    "FreeRequires", "FreeEnsures", "RequiresWithDependencies", "EnsuresWithDependencies",
    "CheckSubrange", "GetSubrangeCheck", "CheckSubsetType", "CheckResultToBeInType",
    "CheckResultToBeInType_Aux", "TrSplitExpr", "TrSplitExprForMethodSpec", "LowerProposition",
    "CheckPropositionUnderGuard", "CheckVisibleTypeObligations", "AllocationObligation",
    "CanCallAssumption", "CanCallAssumptionForVerification", "MakeAssertCmd", "CheckWellformed",
    "CheckWellformedWithResult", "TrStmt_CheckWellformed", "CheckFrameSubset", "CheckCallTermination"
  };

  // Each source family has an explicit semantic audit in obligation-lowering.md.
  // New files with producers have no default classification and fail this gate.
  private static readonly Dictionary<string,string> Families = new() {
    ["BoogieGenerator.DefiniteAssignment.cs"]="definite-assignment",
    ["BoogieGenerator.Iterators.cs"]="iterator-contracts",
    ["ProofObligationDescription.cs"]="diagnostics-only",
    ["BoogieGenerator.Fields.cs"]="constant-initializers",
    ["BoogieGenerator.Functions.Wellformedness.cs"]="function-contracts",
    ["TranslateBinaryExpr.cs"]="value-translation",
    ["BoogieGenerator.Types.cs"]="type-witness-conversion",
    ["BoogieGenerator.ExpressionTranslator.cs"]="value-permission-translation",
    ["BoogieGenerator.Methods.cs"]="method-override-contracts",
    ["BoogieGenerator.DataTypes.cs"]="datatype-constructors",
    ["BoogieGenerator.ExpressionWellformed.cs"]="ordered-expression-wf",
    ["BoogieGenerator.Decreases.cs"]="termination",
    ["BoogieGenerator.TypeObligations.cs"]="guarded-type-introduction",
    ["BoogieGenerator.LetExpr.cs"]="let-permissions",
    ["BoogieGenerator.SplitExpr.cs"]="canonical-proposition-core",
    ["BoogieGenerator.Reveal.cs"]="visibility",
    ["BoogieGenerator.cs"]="membership-frame-spec-bridges",
    ["BoogieGenerator.Obligations.cs"]="canonical-obligation-adapters",
    ["BoogieGenerator.Extremes.cs"]="extreme-prefix-contracts",
    ["BoogieGenerator.Functions.cs"]="definition-consequence-axioms",
    ["BoogieGenerator.BoogieFactory.cs"]="assertion-contract-sinks",
    ["OpaqueBlockVerifier.cs"]="opaque-block-contracts",
    ["BoogieGenerator.TrLoop.cs"]="loop-contracts",
    ["BoogieGenerator.TrCall.cs"]="method-calls",
    ["BoogieGenerator.TrPredicateStatement.cs"]="explicit-predicates",
    ["MatchVerifier.cs"]="match-completeness",
    ["IfStatementVerifier.cs"]="if-guards",
    ["BoogieGenerator.TrStatement.cs"]="statement-wf-calculations",
    ["BoogieGenerator.TrForallStmt.cs"]="forall-proof-export",
    ["BoogieGenerator.TrAssignment.cs"]="assignment-initialization"
  };

  private record Site(string File, string Member, string Producer, string Family, int Count, string Hash, string FileHash);

  [Fact]
  public void EveryProducerMatchesTheReviewedInventory() {
    var directory=new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory!=null && !Directory.Exists(Path.Combine(directory.FullName,"Source/DafnyCore/Verifier"))) {
      directory=directory.Parent;
    }
    Assert.NotNull(directory);
    var root=directory!.FullName;
    var sites=new List<(string File,string Member,string Producer,string Family,string Tokens)>();
    foreach (var file in Directory.EnumerateFiles(Path.Combine(root,"Source/DafnyCore/Verifier"),"*.cs",SearchOption.AllDirectories)) {
      var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.Preview));
      Assert.DoesNotContain(tree.GetDiagnostics(),d=>d.Severity==DiagnosticSeverity.Error);
      var syntax=tree.GetRoot();
      foreach (var node in syntax.DescendantNodes()) {
        string? producer=null;
        if (node is InvocationExpressionSyntax call) {
          var name=call.Expression switch {
            IdentifierNameSyntax n=>n.Identifier.Text,
            MemberAccessExpressionSyntax m=>m.Name.Identifier.Text,
            _=>""
          };
          if (call.Expression is MemberAccessExpressionSyntax access && access.Expression.ToString()=="Contract") continue;
          if (Sinks.Contains(name)) producer=name;
        } else if (node is ObjectCreationExpressionSyntax creation) {
          var name=creation.Type.ToString().Split('.').Last();
          if (name is "AssertCmd" or "Requires" or "Ensures") producer="new "+name;
        } else if (node is MethodDeclarationSyntax method &&
            method.ReturnType.ToString().Split('.').Last() is "AssertCmd" or "Requires" or "Ensures") {
          producer="factory "+method.Identifier.Text;
        }
        if (producer==null) continue;
        var fileName=Path.GetFileName(file);
        Assert.True(Families.ContainsKey(fileName),$"Unclassified producer file {fileName}");
        var owner=node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var member=owner==null ? "<initializer>" : owner.Identifier.Text+"/"+owner.ParameterList.Parameters.Count;
        var tokens=string.Join(" ",node.DescendantTokens().Select(t=>t.Text));
        sites.Add((Path.GetRelativePath(root,file).Replace('\\','/'),member,producer,Families[fileName],tokens));
      }
    }
    var inventory=sites.GroupBy(s=>(s.File,s.Member,s.Producer,s.Family))
      .Select(g=>new Site(g.Key.File,g.Key.Member,g.Key.Producer,g.Key.Family,g.Count(),
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",g.Select(s=>s.Tokens).Order())))),
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(root,g.Key.File)).Replace("\r\n","\n"))))))
      .OrderBy(s=>s.File).ThenBy(s=>s.Member).ThenBy(s=>s.Producer).ToList();
    var json=JsonSerializer.Serialize(inventory,new JsonSerializerOptions{WriteIndented=true});
    var capture=Environment.GetEnvironmentVariable("OBLIGATION_INVENTORY_CAPTURE");
    if (!string.IsNullOrEmpty(capture)) {
      Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(capture))!);
      File.WriteAllText(capture,json+"\n");
      return; // Explicit diagnostic capture; CI's normal gate never sets this.
    }
    var registry=Path.Combine(root,"docs/dev/obligation-producers.json");
    Assert.Equal(File.ReadAllText(registry).TrimEnd(),json);
  }
}
