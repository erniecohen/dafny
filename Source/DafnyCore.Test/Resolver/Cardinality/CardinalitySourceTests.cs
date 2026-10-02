// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test.Resolver.Cardinality;

[CollectionDefinition("Cardinality resolution", DisableParallelization = true)]
public class CardinalityResolutionCollection { }

[Collection("Cardinality resolution")]
public class CardinalitySourceTests {
  private const string Cycle = "r_cardinality_expansive_cycle";
  private const string Unretained = "r_cardinality_unretained_parameter";
  private const string ParentContract = "r_cardinality_parent_contract";
  private const string ParameterContract = "r_cardinality_parameter_contract";

  // No verifier runs here. These tests identify resolution diagnostics rather than
  // counting a later assertion failure as successful cardinality validation.
  public static IEnumerable<object[]> ForbiddenRepresentations() {
    var representations = new[] {
      "p: V -> bool", "p: V --> bool", "p: V ~> bool",
      "ghost p: iset<V>", "ghost p: imap<V, int>",
      "p: (V -> bool) -> bool", "p: seq<V -> bool>",
      "p: map<int, V -> bool>", "ghost p: V -> bool",
      "p: (int, V -> bool)", "p: seq<seq<V -> bool>>"
    };
    foreach (var representation in representations) {
      foreach (var refresh in new[] { false, true }) {
        yield return new object[] { representation, refresh };
      }
    }
  }

  [Theory]
  [MemberData(nameof(ForbiddenRepresentations))]
  public async Task RejectsExpansiveTraitCycles(string representation, bool refresh) {
    await AssertDiagnostic($"trait V {{}} datatype D extends V = D({representation})", Cycle, refresh);
  }

  public static IEnumerable<object[]> HiddenExpansions() {
    yield return new object[] { "type F = V -> bool datatype D extends V = D(p: F)" };
    yield return new object[] { "type Id<T> = T type F = Id<V> -> bool datatype D extends V = D(p: F)" };
    yield return new object[] { "type F<!T> = T -> bool datatype D extends V = D(p: F<V>)" };
    yield return new object[] { "datatype B = B(p: V -> bool) datatype D extends V = D(b: B)" };
    yield return new object[] { "trait Middle extends V {} datatype D extends Middle = D(p: V -> bool)" };
    yield return new object[] { "datatype D extends V = Ground | ghost D(p: V -> bool)" };
    yield return new object[] { "codatatype D extends V = D(p: V -> bool)" };
    yield return new object[] { "type F = p: V -> bool | false witness * datatype D extends V = D(p: F)" };
    yield return new object[] { "trait W {} datatype D extends V = D(p: W -> bool) datatype E extends W = E(d: D)" };
  }

  [Theory]
  [MemberData(nameof(HiddenExpansions))]
  public async Task WrappersDoNotEraseExpansiveDependencies(string declarations) {
    await AssertBothDiagnostics("trait V {} " + declarations, Cycle);
  }

  [Theory]
  [InlineData("datatype D<T> extends V = D(x: T)")]
  [InlineData("datatype D<T(!new)> extends V = D(x: T)")]
  [InlineData("datatype D<T(==)> extends V = D(x: T)")]
  [InlineData("trait Bound {} datatype D<T extends Bound> extends V = D(x: T)")]
  [InlineData("datatype D<T> extends V = D(ghost x: T)")]
  [InlineData("datatype D<T> extends V = D")]
  [InlineData("trait Middle<T> extends V {}")]
  [InlineData("class C<T> extends V {}")]
  public async Task RejectsErasedFamiliesWithoutUseSites(string declaration) {
    await AssertBothDiagnostics("trait V {} " + declaration, Unretained);
  }

  [Fact]
  public async Task NestedIndexIsConservativelyNotDirectRetention() {
    await AssertBothDiagnostics("trait V<T> {} datatype D<X> extends V<seq<X>> = D(x: X)", Unretained);
  }

  [Fact]
  public async Task DistinctInstancesShareTheConservativeNominalHead() {
    await AssertBothDiagnostics("trait V<T> {} datatype D extends V<bool> = D(p: V<int> -> bool)", Cycle);
  }

  [Fact]
  public async Task PermissiveChildRequiresPermissiveRetainingSlot() {
    await AssertBothDiagnostics("trait V<T> {} datatype D<!X> extends V<X> = D(p: X -> bool)", ParentContract);
  }

  [Theory]
  [InlineData("class G<X> { ghost var p: X -> bool }")]
  [InlineData("class G<X> { const p: X -> bool constructor(p: X -> bool) { this.p := p; } }")]
  [InlineData("trait G<X> extends object { ghost var p: X -> bool }")]
  [InlineData("iterator G<X>(p: X -> bool) {}")]
  public async Task ReferenceStorageMustHonorFormalParameterContract(string source) {
    await AssertBothDiagnostics(source, ParameterContract);
  }

  [Fact]
  public async Task ReferenceParentMustRespectExposedStrictContract() {
    await AssertBothDiagnostics(
      "trait Parent<X> extends object {} class Child<!X> extends Parent<X> { ghost var p: X -> bool }", ParentContract);
  }

  [Fact]
  public async Task CrossModuleCycleUsesAllResolvedModules() {
    await AssertBothDiagnostics(CrossModuleSource(false), Cycle);
  }

  [Fact]
  public async Task ProvidedViewCannotHideRepresentation() {
    await AssertBothDiagnostics(CrossModuleSource(true), Cycle);
  }

  private static string CrossModuleSource(bool provided) => $$"""
    module Api { trait {:termination false} V {} }
    module Storage {
      import A = Api
      {{(provided ? "export provides B" : "")}}
      type F = A.V -> bool
      datatype B = B(f: F)
    }
    module Client {
      import A = Api
      import S = Storage
      datatype D extends A.V = D(b: S.B)
    }
    """;

  [Fact]
  public async Task OpenedProvidedAndRevealedViewsUseOneCarrier() {
    const string source = """
      module Api { trait {:termination false} V {} }
      module Storage {
        import A = Api
        export Opaque provides B, A
        export Transparent provides A reveals B, F
        type F = A.V -> bool
        datatype B = B(f: F)
      }
      module Client {
        import A = Api
        import opened O = Storage`Opaque
        import opened R = Storage`Transparent
        datatype D extends A.V = D(opaque: O.B, transparent: R.B)
      }
      """;
    foreach (var refresh in new[] { false, true }) {
      var (_, reporter) = await ResolveAsync(source, refresh);
      Assert.Equal(Cycle, Assert.Single(reporter.AllMessagesByLevel[ErrorLevel.Error]).ErrorId);
      var (program, safeReporter) = await ResolveAsync(source.Replace("A.V -> bool", "int"), refresh);
      Assert.True(safeReporter.ErrorCount == 0, Diagnostics(safeReporter));
      Assert.True(program.CardinalityValidationReceipt?.Succeeded);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SelectedReplacementParticipatesInTheClientCarrierGraph(bool refresh) {
    const string source = """
      module Api { trait {:termination false} V {} }
      replaceable module Storage { import A = Api type F }
      module ConcreteStorage replaces Storage { type F = A.V -> bool }
      module Client {
        import A = Api
        import S = Storage
        datatype D extends A.V = Ground | D(f: S.F)
      }
      """;
    await AssertDiagnostic(source, Cycle, refresh);
    var (program, reporter) = await ResolveAsync(source.Replace("A.V -> bool", "int"), refresh);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SelectedAbstractFacadeParticipatesInTheClientGraph(bool refresh) {
    const string source = """
      abstract module A { trait {:termination false} V {} type F }
      module B refines A { type F = V -> bool }
      replaceable module P { import M : A export provides M }
      module C replaces P { import M = B }
      abstract module Client {
        import Pkg = P
        datatype D extends Pkg.M.V = Ground | D(f: Pkg.M.F)
      }
      """;
    await AssertDiagnostic(source, Cycle, refresh);
    var (program, reporter) = await ResolveAsync(source.Replace("V -> bool", "int"), refresh);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task IndependentSelectedFacadesKeepTheirOwnCarrierGraphs(bool refresh) {
    const string source = """
      abstract module A { trait {:termination false} V {} type F }
      module B1 refines A { type F = int }
      module B2 refines A { type F = V -> bool }
      replaceable module P1 { import M : A export provides M }
      replaceable module P2 { import M : A export provides M }
      module C1 replaces P1 { import M = B1 }
      module C2 replaces P2 { import M = B2 }
      abstract module Client {
        import Pkg1 = P1
        import Pkg2 = P2
        datatype D1 extends Pkg1.M.V = Ground1 | D1(f: Pkg1.M.F)
        datatype D2 extends Pkg2.M.V = Ground2 | D2(f: Pkg2.M.F)
      }
      """;
    var (program, reporter) = await ResolveAsync(source, refresh);
    var error = Assert.Single(reporter.AllMessagesByLevel[ErrorLevel.Error]);
    Assert.Equal(Cycle, error.ErrorId);
    Assert.Contains("Client.D2", error.Message);
    Assert.DoesNotContain("Client.D1", error.Message);
    Assert.Null(program.CardinalityValidationReceipt);
    var (safe, safeReporter) = await ResolveAsync(source.Replace("V -> bool", "bool"), refresh);
    Assert.True(safeReporter.ErrorCount == 0, Diagnostics(safeReporter));
    Assert.True(safe.CardinalityValidationReceipt?.Succeeded);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SelectedStrengtheningCannotEraseTheImportedInterfaceContract(bool refresh) {
    var (program, reporter) = await ResolveAsync("""
      abstract module A { type D<!X> }
      replaceable module B refines A {}
      module C replaces B { type D<X> = X }
      """, refresh);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
    var original = program.RawModules().Single(module => module.Name == "B").TopLevelDecls.Single(declaration => declaration.Name == "D");
    var selected = program.RawModules().Single(module => module.Name == "C").TopLevelDecls.Single(declaration => declaration.Name == "D");
    Assert.False(original.TypeArgs.Single().StrictVariance);
    Assert.True(selected.TypeArgs.Single().StrictVariance);

    await AssertDiagnostic("""
      abstract module A { trait V<X> {} type D<!X> }
      replaceable module B refines A {}
      module C replaces B { datatype D<X> extends V<X> = D(x: X) }
      """, ParentContract, refresh);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ExistingSelectedInheritanceVarianceRejectionIsNotNewCoverage(bool refresh) {
    var (_, reporter) = await ResolveAsync("""
      abstract module A { trait V<!X> {} type D<!X> extends V<X> }
      replaceable module B refines A {}
      module C replaces B { datatype D<X> = D(x: X) }
      """, refresh);
    Assert.Contains(reporter.AllMessagesByLevel[ErrorLevel.Error],
      diagnostic => diagnostic.Message.Contains("variance specification", StringComparison.Ordinal));
    Assert.DoesNotContain(reporter.AllMessagesByLevel[ErrorLevel.Error],
      diagnostic => diagnostic.ErrorId?.StartsWith("r_cardinality_") == true);
  }

  [Fact]
  public async Task AbstractTypeRefinementKeepsDatatypeRepresentationAndParents() {
    await AssertBothDiagnostics(
      "abstract module A { trait V {} type D extends V } module B refines A { datatype D = Ground | D(p: V -> bool) }", Cycle);
    foreach (var refresh in new[] { false, true }) {
      var (program, reporter) = await ResolveAsync(
        "abstract module A { trait V {} type D extends V } module B refines A { datatype D = D(n: int) }", refresh);
      Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
      Assert.True(program.CardinalityValidationReceipt?.Succeeded);
    }
  }

  [Fact]
  public async Task AbstractTypeRefinementKeepsNewtypeRepresentationAndParents() {
    const string source = """
      abstract module A { trait V {} type D extends V }
      module B refines A {
        datatype Payload = Payload(p: V -> bool)
        newtype D = p: seq<Payload> | true witness *
      }
      """;
    await AssertDiagnostic(source, Cycle, full: true);
    var (program, reporter) = await ResolveAsync(source.Replace("p: V -> bool", "n: int"), full: true);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  public static IEnumerable<object[]> LegalDefinitions() {
    var sources = new[] {
      "trait V {} datatype D extends V = D(n: int)",
      "trait V {} datatype D extends V = Ground | D(v: V)",
      "trait V {} datatype D extends V = Ground | D(v: seq<V>)",
      "trait V {} datatype D extends V = Ground | D(ghost v: set<V>)",
      "trait V {} datatype D extends V = Ground | D(ghost v: multiset<V>)",
      "trait V {} datatype D extends V = Ground | D(ghost v: map<V, int>)",
      "trait V {} datatype D extends V = Ground | D(ghost v: map<int, V>)",
      "trait V {} datatype Leaf extends V = Leaf datatype D extends V = D(p: int -> V)",
      "trait V {} datatype Leaf extends V = Leaf datatype D extends V = D(p: imap<int, V>)",
      "trait V<T> {} datatype D<T> extends V<T> = D(x: T)",
      "trait V<A, B> {} datatype D<X, Y> extends V<Y, X> = D(x: X, y: Y)",
      "trait V<A, B> {} datatype D<X> extends V<X, int> = D(x: X)",
      "trait V<A, B> {} datatype D<X> extends V<X, seq<X>> = D(x: X)",
      "type Id<T> = T trait V<T> {} datatype D<X> extends V<Id<X>> = D(x: X)",
      "trait V<!T> {} datatype D<!T> extends V<T> = D(p: T -> bool)",
      "trait V<A, !B> {} datatype D<!X> extends V<X, X> = D(p: X -> bool)",
      "trait V {} datatype D extends V = D(n: int) datatype Observer = Observer(f: V -> bool)",
      "trait V {} datatype D extends V = D(n: int) function Apply<T>(f: T -> bool, v: T): bool { f(v) }",
      "class C<T> { var x: T constructor(x: T) { this.x := x; } }",
      "class C<T> { method Apply(f: T -> bool, x: T) returns (b: bool) { b := f(x); } }",
      "class C<!T> { ghost var f: T -> bool }",
      "trait H<!T> extends object { ghost var p: T -> bool } class G<!X> extends H<X> {}",
      "class C { ghost var f: C -> bool }",
      "trait Root<T> {} trait Left<T> extends Root<T> {} trait Right<T> extends Root<T> {} datatype D<T> extends Left<T>, Right<T> = D(x: T)",
      "module A { import Other = B trait V {} datatype D extends V = D(f: Other.V -> bool) } module B { trait V {} datatype D extends V = D(n: int) }"
    };
    foreach (var source in sources) {
      foreach (var refresh in new[] { false, true }) {
        yield return new object[] { source, refresh };
      }
    }
  }

  [Theory]
  [MemberData(nameof(LegalDefinitions))]
  public async Task AcceptsPreservingOrAcyclicDefinitions(string source, bool refresh) {
    var (program, reporter) = await ResolveAsync(source, refresh);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Fact]
  public async Task NewtypeSequenceBaseKeepsNestedRepresentationDependencies() {
    await AssertDiagnostic(
      "trait V {} datatype Payload = Payload(p: V -> bool) newtype VP extends V = p: seq<Payload> | true witness *",
      Cycle, full: true);
    var (program, reporter) = await ResolveAsync(
      "trait V {} datatype Payload = Payload(n: int) newtype VP extends V = p: seq<Payload> | true witness *",
      full: true);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ExistingRecursiveCardinalityChecksRemainActive(bool refresh) {
    // This program is rejected by the pre-existing CheckVariance rule. It must
    // not be counted as a new cardinality diagnostic or double-diagnosed.
    var (_, reporter) = await ResolveAsync("datatype D = D(p: D -> bool)", refresh);
    Assert.NotEmpty(reporter.AllMessagesByLevel[ErrorLevel.Error]);
    Assert.DoesNotContain(reporter.AllMessagesByLevel[ErrorLevel.Error],
      diagnostic => diagnostic.ErrorId?.StartsWith("r_cardinality_") == true);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ExistingInheritedVarianceRejectionIsNotNewCheckerCoverage(bool refresh) {
    var (_, reporter) = await ResolveAsync(
      "trait H<!T> extends object { ghost var p: T -> bool } class G<X> extends H<X> {}", refresh);
    Assert.Contains(reporter.AllMessagesByLevel[ErrorLevel.Error],
      diagnostic => diagnostic.Message.Contains("variance specification", StringComparison.Ordinal));
    Assert.DoesNotContain(reporter.AllMessagesByLevel[ErrorLevel.Error],
      diagnostic => diagnostic.ErrorId?.StartsWith("r_cardinality_") == true);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task GenericReferenceContractsRunWithBothTypeInferencePaths(bool refresh) {
    await AssertDiagnostic("class G<X> { ghost var p: X -> bool }", ParameterContract, refresh);
  }

  [Theory]
  [InlineData("X", "!X")]
  [InlineData("+X", "*X")]
  public async Task RefinementCannotRelaxExportedCardinalityContract(string original, string refined) {
    var (_, reporter) = await ResolveAsync($"abstract module A {{ type T<{original}> }} module B refines A {{ type T<{refined}> = X }}");
    Assert.Contains(reporter.AllMessagesByLevel[ErrorLevel.Error],
      diagnostic => diagnostic.Message.Contains("cardinality", StringComparison.OrdinalIgnoreCase));
  }

  [Theory]
  [InlineData("!X", "X")]
  [InlineData("*X", "+X")]
  public async Task RefinementCanStrengthenCardinalityContract(string original, string refined) {
    var (program, reporter) = await ResolveAsync(
      $"abstract module A {{ type T<{original}> }} module B refines A {{ type T<{refined}> = X }}");
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    Assert.True(program.CardinalityValidationReceipt?.Succeeded);
  }

  [Fact]
  public async Task SeededHidingTransformsPreserveForbiddenAndLegalCases() {
    var random = new Random(6547);
    var wrappers = new[] { "seq<{0}>", "set<{0}>", "map<int, {0}>", "({0}, int)" };
    for (var index = 0; index < 12; index++) {
      var negative = "V -> bool";
      var positive = "V";
      for (var depth = 0; depth < 1 + index % 4; depth++) {
        var wrapper = wrappers[random.Next(wrappers.Length)];
        negative = string.Format(wrapper, negative);
        positive = string.Format(wrapper, positive);
      }
      foreach (var refresh in new[] { false, true }) {
        await AssertDiagnostic($"trait V {{}} datatype Payload = Payload(ghost p: {negative}) datatype D extends V = Ground | D(p: Payload)", Cycle, refresh);
        var (program, reporter) = await ResolveAsync(
          $"trait V {{}} datatype Payload = Payload(ghost p: {positive}) datatype D extends V = Ground | D(p: Payload)", refresh);
        Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
        Assert.True(program.CardinalityValidationReceipt?.Succeeded);
      }
    }
  }

  internal static async Task<(Program Program, BatchErrorReporter Reporter)> ResolveAsync(
    string source, bool refresh = true, bool full = false) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.Set(CommonOptionBag.GeneralTraits, full ? CommonOptionBag.GeneralTraitsOptions.Full : CommonOptionBag.GeneralTraitsOptions.Datatype);
    options.Set(CommonOptionBag.GeneralNewtypes, refresh);
    var reporter = new BatchErrorReporter(options);
    var result = await ProgramParser.Parse(source, new Uri("untitled:cardinality.dfy"), reporter);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    await new ProgramResolver(result.Program).Resolve(CancellationToken.None);
    return (result.Program, reporter);
  }

  private static async Task AssertBothDiagnostics(string source, string errorId) {
    foreach (var refresh in new[] { false, true }) {
      await AssertDiagnostic(source, errorId, refresh);
    }
  }

  private static async Task AssertDiagnostic(string source, string errorId, bool refresh = true, bool full = false) {
    var (program, reporter) = await ResolveAsync(source, refresh, full);
    var errors = reporter.AllMessagesByLevel[ErrorLevel.Error];
    Assert.Contains(errors, diagnostic => diagnostic.ErrorId == errorId);
    Assert.DoesNotContain(errors, diagnostic => diagnostic.ErrorId != errorId);
    Assert.False(program.CardinalityValidationReceipt?.Succeeded ?? false);
  }

  private static string Diagnostics(BatchErrorReporter reporter) =>
    string.Join(Environment.NewLine, reporter.AllMessages.Select(diagnostic => diagnostic.Message));
}
