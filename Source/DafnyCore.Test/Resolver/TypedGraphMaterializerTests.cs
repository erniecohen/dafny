using System.Collections.Immutable;
using Microsoft.Dafny;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class TypedGraphMaterializerTests {
  private const string Body = """
    module Body {
      datatype D = D(value: int)
      function Id(d: D): D { d }
      function Use(d: D): D { Id(d) }
      function Count(n: nat): nat decreases n {
        if n == 0 then 0 else Count(n - 1)
      }
      function Read(a: array2<int>): int
        requires a.Length0 > 0 && a.Length1 > 0
        reads a
      { a[0, 0] }
    }
    """;

  private sealed class ObservingResolver(Program program,
    Action<ModuleResolutionResult, IReadOnlyList<ModuleResolutionResult>> observe) : ProgramResolver(program) {
    protected override ModuleResolutionResult ResolveModuleOutput(ModuleDecl declaration, string key,
      IReadOnlyList<ModuleResolutionResult> prerequisites, Func<ModuleResolutionResult> resolve, out bool cached) {
      var result = base.ResolveModuleOutput(declaration, key, prerequisites, resolve, out cached);
      if (result.ResolvedDeclaration is LiteralModuleDecl { ModuleDef.IsDefaultModule: false }) { observe(result, prerequisites); }
      return result;
    }
  }

  private static async Task Resolve(string source,
    Action<ModuleResolutionResult, IReadOnlyList<ModuleResolutionResult>> observe) {
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.WarnUncheckedTermination, false);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:typed-graph.dfy"), reporter);
    await new ObservingResolver(parsed.Program, observe).Resolve(CancellationToken.None);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(m => m.Message)));
  }

  private static object[] Roots(ModuleResolutionResult result) =>
    [result.ResolvedDeclaration, result.Signatures, result.ClassMembers, result.TranslationInputs];

  private static async Task<(LiteralModuleDecl Expected, LiteralModuleDecl Copy, GraphBindings Bindings)> RoundTrip(string source = Body,
    Action<LiteralModuleDecl, LiteralModuleDecl, GraphBindings>? inspect = null) {
    var snapshots = new Dictionary<string, TypedGraphSnapshot>();
    var producers = new Dictionary<string, HashSet<object>>();
    await Resolve(source, (result, prerequisites) => {
      var bindings = GraphBindings.For(result, prerequisites);
      snapshots.Add(result.ResolvedDeclaration.FullDafnyName, TypedGraphSnapshot.Capture(Roots(result), bindings));
      producers.Add(result.ResolvedDeclaration.FullDafnyName, GraphData.OwnedObjects(Roots(result), bindings));
    });
    (LiteralModuleDecl, LiteralModuleDecl, GraphBindings) last = default;
    var compared = 0;
    await Resolve(source, (result, prerequisites) => {
      var bindings = GraphBindings.For(result, prerequisites);
      var copy = (object[])snapshots[result.ResolvedDeclaration.FullDafnyName].Materialize(bindings);
      Assert.Empty(GraphData.OwnedObjects(copy, bindings).Intersect(producers[result.ResolvedDeclaration.FullDafnyName], ReferenceEqualityComparer.Instance));
      Assert.Null(new TypedGraphEquality(bindings).Difference(Roots(result), copy));
      last = ((LiteralModuleDecl)result.ResolvedDeclaration, (LiteralModuleDecl)copy[0], bindings);
      inspect?.Invoke(last.Item1, last.Item2, bindings);
      compared++;
    });
    Assert.Equal(snapshots.Count, compared);
    Assert.True(compared > 0);
    return last;
  }

  [Fact]
  public async Task ExistingResolvedClonerDoesNotDetachTheWitness() {
    var observed = false;
    await Resolve(Body, (result, _) => {
      var original = ((LiteralModuleDecl)result.ResolvedDeclaration).ModuleDef;
      var clone = new Cloner(cloneLiteralModuleDefinition: true, cloneResolvedFields: true)
        .CloneModuleDefinition(original, original.EnclosingModule);
      var originalId = Functions(original).Single(f => f.Name == "Id");
      var cloneId = Functions(clone).Single(f => f.Name == "Id");
      var call = (FunctionCallExpr)Functions(clone).Single(f => f.Name == "Use").Body!.Resolved;
      Assert.Same(originalId, call.Function);
      Assert.NotSame(cloneId, call.Function);
      var originalCall = (FunctionCallExpr)Functions(original).Single(f => f.Name == "Use").Body!.Resolved;
      Assert.Same(originalCall.Type, call.Type);
      Assert.Same(originalCall.PreType, call.PreType);
      Assert.Same(originalCall.TypeApplication_JustFunction, call.TypeApplication_JustFunction);
      Assert.True(originalId.IsRevealedInScope(original.VisibilityScope));
      Assert.False(cloneId.IsRevealedInScope(clone.VisibilityScope));
      Assert.True(Functions(original).Single(f => f.Name == "Count").IsRecursive);
      Assert.False(Functions(clone).Single(f => f.Name == "Count").IsRecursive);
      Assert.NotEmpty(original.CallGraph.GetVertices());
      Assert.Empty(clone.CallGraph.GetVertices());
      Assert.NotEmpty(original.AccessibleMembers);
      Assert.Empty(clone.AccessibleMembers);
      var originalArray = (UserDefinedType)Functions(original).Single(f => f.Name == "Read").Ins.Single().Type.NormalizeExpand();
      var cloneArray = (UserDefinedType)Functions(clone).Single(f => f.Name == "Read").Ins.Single().Type.NormalizeExpand();
      Assert.Same(originalArray.ResolvedClass, cloneArray.ResolvedClass);
      Assert.False(clone.SuccessfullyResolved);
      observed = true;
    });
    Assert.True(observed);
  }

  private static IEnumerable<Function> Functions(ModuleDefinition module) =>
    module.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(c => c.Members).OfType<Function>();

  [Fact]
  public async Task DetachedBodyPreservesTypedTargetsVisibilityAndRecursion() {
    var (expected, copy, bindings) = await RoundTrip();
    Assert.NotSame(expected, copy);
    var id = Functions(copy.ModuleDef).Single(f => f.Name == "Id");
    var call = (FunctionCallExpr)Functions(copy.ModuleDef).Single(f => f.Name == "Use").Body!.Resolved;
    Assert.Same(id, call.Function);
    Assert.NotSame(Functions(expected.ModuleDef).Single(f => f.Name == "Id"), id);
    Assert.True(id.IsRevealedInScope(copy.ModuleDef.VisibilityScope));
    Assert.True(Functions(copy.ModuleDef).Single(f => f.Name == "Count").IsRecursive);
    Assert.True(copy.ModuleDef.SuccessfullyResolved);
    Assert.NotEmpty(copy.ModuleDef.CallGraph.GetVertices());
    var array = Functions(copy.ModuleDef).Single(f => f.Name == "Read").Ins.Single().Type.NormalizeExpand();
    Assert.Contains(((UserDefinedType)array).ResolvedClass, bindings.Selectors.Keys);
  }

  [Fact]
  public async Task DatatypePretypesKeepCurrentCanonicalBuiltinIdentity() {
    await RoundTrip("module M { datatype D = C(x: bv8) function Id(d: D): D { d } }",
      (expected, copy, _) => {
        static TopLevelDecl Builtin(LiteralModuleDecl module) =>
          ((DPreType)module.ModuleDef.SourceDecls.OfType<DatatypeDecl>().Single()
            .Ctors.Single().Formals.Single().PreType!.Normalize()).Decl;
        Assert.Same(Builtin(expected), Builtin(copy));
      });
  }

  [Theory]
  [InlineData("module Base { const N: int := 1 } module Client { import B = Base lemma Good() ensures B.N == 1 {} }")]
  [InlineData("module M { datatype D = C(x: int) function F(d: D): D { d.(x := 1) } }")]
  [InlineData("module M { function F(n: nat): nat decreases n { if n == 0 then 0 else G(n-1) } function G(n: nat): nat decreases n { if n == 0 then 0 else F(n-1) } }")]
  [InlineData("module M { function Id<T>(x: T): T { x } function F(): int { Id(1) } }")]
  [InlineData("module M { datatype D = C(x: bv8) function Id(d: D): D { d } }")]
  public async Task SnapshotFixturesMatchIndependentNormalResolution(string source) {
    await RoundTrip(source);
  }

  [Theory]
  [InlineData("git-issues/github-issue-241-parallel-resolution.dfy.valid.dfy")]
  [InlineData("git-issues/github-issue-241-translation-cache.dfy")]
  [InlineData("git-issues/git-issue-241-history-free-translation.dfy")]
  [InlineData("git-issues/github-issue-241-module-window.dfy")]
  [InlineData("git-issues/github-issue-241-frontend.dfy")]
  [InlineData("git-issues/Inputs/github-issue-241-frontend-exports.dfy")]
  [InlineData("git-issues/Inputs/git-issue-241-cache-cutoff.dfy")]
  [InlineData("git-issues/git-issue-241-typed-graph.dfy")]
  [InlineData("dafny0/AllLiteralsAxiom.dfy")]
  [InlineData("dafny0/ByMethod.dfy")]
  [InlineData("dafny0/OpaqueFunctions.dfy")]
  [InlineData("dafny0/Newtypes.dfy")]
  public async Task ExistingLitProgramsMatchIndependentNormalResolution(string relative) {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (!Directory.Exists(Path.Combine(directory.FullName, "Source/IntegrationTests"))) { directory = directory.Parent!; }
    var source = await File.ReadAllTextAsync(Path.Combine(directory.FullName,
      "Source/IntegrationTests/TestFiles/LitTests/LitTest", relative));
    await RoundTrip(source);
  }

  [Theory]
  [InlineData("target")]
  [InlineData("type")]
  [InlineData("visibility")]
  [InlineData("call-graph")]
  [InlineData("recursive")]
  [InlineData("builtin")]
  [InlineData("accessible-members")]
  public async Task EqualityRejectsChangedSemanticEdges(string change) {
    await RoundTrip(inspect: (expected, copy, bindings) => {
      Assert.Null(new TypedGraphEquality(bindings).Difference(expected, copy));
      var functions = Functions(copy.ModuleDef).ToArray();
      var use = functions.Single(f => f.Name == "Use");
      switch (change) {
        case "target": ((FunctionCallExpr)use.Body!.Resolved).Function = Functions(expected.ModuleDef).Single(f => f.Name == "Id"); break;
        case "type": use.Body!.Type = Microsoft.Dafny.Type.Bool; break;
        case "visibility": GraphData.Tokens(copy.ModuleDef.VisibilityScope).Clear(); break;
        case "call-graph": copy.ModuleDef.CallGraph = new(); break;
        case "recursive": functions.Single(f => f.Name == "Count").IsRecursive = false; break;
        case "accessible-members": copy.ModuleDef.AccessibleMembers.Clear(); break;
        case "builtin":
          var array = GraphData.OwnedObjects(copy, bindings).OfType<UserDefinedType>()
            .First(t => t.ResolvedClass is NonNullTypeDecl { Class: ArrayClassDecl } or ArrayClassDecl);
          array.ResolvedClass = (TopLevelDecl)typeof(object).GetMethod("MemberwiseClone",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(array.ResolvedClass, null)!;
          break;
      }
      Assert.NotNull(new TypedGraphEquality(bindings).Difference(expected, copy));
    });
  }

  [Fact]
  public void MaterializerRejectsUnknownKindsFieldsAndDanglingEdges() {
    var bindings = new GraphBindings();
    var snapshot = TypedGraphSnapshot.Capture(new Name("test"), bindings);
    var entry = snapshot.Entries[0];
    Assert.Throws<InvalidOperationException>(() => (snapshot with {
      Entries = snapshot.Entries.SetItem(0, entry with { Kind = "unknown" })
    }).Materialize(bindings));
    Assert.Throws<InvalidOperationException>(() => (snapshot with {
      Entries = snapshot.Entries.SetItem(0, entry with { Names = ["unknown"] })
    }).Materialize(bindings));
    Assert.Throws<InvalidOperationException>(() => (snapshot with { Root = new TypedGraphSnapshot.Reference(999) }).Materialize(bindings));
    Assert.Throws<NotSupportedException>(() => TypedGraphSnapshot.Capture(new object(), bindings));
    Assert.Throws<InvalidOperationException>(() => (snapshot with { Root = new TypedGraphSnapshot.External("missing") }).Materialize(bindings));
    Assert.Throws<InvalidOperationException>(() => (snapshot with { Root = new TypedGraphSnapshot.Scalar(new object()) }).Materialize(bindings));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void EqualityRejectsSplittingAndMergingIdentities(bool merge) {
    var name = new Name("same");
    object[] shared = [name, name];
    object[] distinct = [new Name("same"), new Name("same")];
    Assert.NotNull(new TypedGraphEquality(new()).Difference(merge ? distinct : shared, merge ? shared : distinct));
  }

  [Fact]
  public void CollectionComparersAndMalformedRecordsAreChecked() {
    var bindings = new GraphBindings();
    var identity = new HashSet<object>(ReferenceEqualityComparer.Instance);
    Assert.NotNull(new TypedGraphEquality(bindings).Difference(new HashSet<object>(), identity));
    var ordinal = new Dictionary<string, int>(StringComparer.Ordinal) { ["x"] = 1 };
    var snapshot = TypedGraphSnapshot.Capture(ordinal, bindings);
    Assert.Null(new TypedGraphEquality(bindings).Difference(ordinal, snapshot.Materialize(bindings)));
    var entry = snapshot.Entries[0];
    Assert.Throws<InvalidOperationException>(() => (snapshot with {
      Entries = snapshot.Entries.SetItem(0, entry with { Names = ["unknown"] })
    }).Materialize(bindings));
    Assert.Throws<InvalidOperationException>(() => (snapshot with {
      Entries = snapshot.Entries.SetItem(0, entry with { Values = entry.Values.RemoveAt(0) })
    }).Materialize(bindings));
    Assert.Throws<NotSupportedException>(() => TypedGraphSnapshot.Capture(new int[1, 1], bindings));
    Assert.Throws<NotSupportedException>(() => TypedGraphSnapshot.Capture(Array.CreateInstance(typeof(int), [1], [1]), bindings));
    Assert.Throws<NotSupportedException>(() => TypedGraphSnapshot.Capture((Action)(() => { }), bindings));
    Assert.Throws<NotSupportedException>(() => TypedGraphSnapshot.Capture(new HashSet<string>(StringComparer.OrdinalIgnoreCase), bindings));
    Assert.Throws<InvalidOperationException>(() => GraphData.NewCollection(typeof(Name), "default"));
    var scope = TypedGraphSnapshot.Capture(new VisibilityScope("test"), bindings);
    Assert.Throws<InvalidOperationException>(() => (scope with {
      Entries = scope.Entries.SetItem(0, scope.Entries[0] with { Values = [new TypedGraphSnapshot.External("missing")] })
    }).Materialize(bindings));
  }

  [Fact]
  public void HashKeysAreInitializedBeforeInsertionAndProxyTargetsAreChecked() {
    var bindings = new GraphBindings();
    var token = new Token { pos = 123, val = "key" };
    var dictionary = new Dictionary<Token, string> { [token] = "value" };
    var copy = (Dictionary<Token, string>)TypedGraphSnapshot.Capture(dictionary, bindings).Materialize(bindings);
    Assert.Equal("value", copy[copy.Keys.Single()]);
    var first = new InferredTypeProxy { T = Microsoft.Dafny.Type.Int };
    var second = new InferredTypeProxy { T = Microsoft.Dafny.Type.Int };
    Assert.Null(new TypedGraphEquality(bindings).Difference(first, second));
    second.T = Microsoft.Dafny.Type.Bool;
    Assert.NotNull(new TypedGraphEquality(bindings).Difference(first, second));
  }

  [Fact]
  public void EnumerableGraphNodesStillCompareTheirBackingEdges() {
    var bindings = new GraphBindings();
    var name = new Name("item");
    var expected = new ConcatReadOnlyLists<Name>(new List<Name> { name }, new List<Name>());
    var different = new ConcatReadOnlyLists<Name>(new List<Name>(), new List<Name> { name });
    Assert.Equal(expected.ToArray(), different.ToArray());
    Assert.NotNull(new TypedGraphEquality(bindings).Difference(expected, different));
    var copy = TypedGraphSnapshot.Capture(expected, bindings).Materialize(bindings);
    Assert.Null(new TypedGraphEquality(bindings).Difference(expected, copy));
  }

  [Fact]
  public void ScopeNamesDoNotIdentifyTokensAndSharingSurvivesAugmentation() {
    var first = new VisibilityScope("same");
    var second = new VisibilityScope("same");
    var joined = new VisibilityScope();
    joined.Augment(first);
    object[] roots = [first, second, joined, joined];
    var bindings = new GraphBindings();
    var copy = (object[])TypedGraphSnapshot.Capture(roots, bindings).Materialize(bindings);
    Assert.Null(new TypedGraphEquality(bindings).Difference(roots, copy));
    Assert.False(((VisibilityScope)copy[0]).VisibleInScope((VisibilityScope)copy[1]));
    Assert.Same(copy[2], copy[3]);
    Assert.True(((VisibilityScope)copy[2]).VisibleInScope((VisibilityScope)copy[0]));
    ((VisibilityScope)copy[2]).Augment((VisibilityScope)copy[1]);
    Assert.NotNull(new TypedGraphEquality(bindings).Difference(roots, copy));
    Assert.False(joined.VisibleInScope(second));
  }
}
