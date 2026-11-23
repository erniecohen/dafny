using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test.Resolver;

[Collection("Obligation translation")]
public class ArgumentApplicationSummaryTests {
  private static async Task<Program> Resolve(string source, bool enabled = true, bool legacy = false, Uri? uri = null) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.DecreasesOrders, enabled);
    options.Set(CommonOptionBag.LegacyNoModularTermination, legacy);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, uri ?? new Uri("untitled:argument-summary.dfy"), reporter);
    Assert.Equal(0, reporter.ErrorCount);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    return parsed.Program;
  }

  [Theory]
  [InlineData("r(0)", true)]
  [InlineData("var alias := r; alias(0)", true)]
  [InlineData("(x: int) => r(x)", false)]
  [InlineData("r", false)]
  [InlineData("Box(r)", false)]
  [InlineData("(r, r)", false)]
  [InlineData("Unknown(r)", false)]
  [InlineData("Unknown((x: int) => r(x))", false)]
  public async Task ChecksEveryParameterUse(string body, bool expected) {
    var resultType = body.Contains("=>") && !body.StartsWith("Unknown") || body == "r" ? "(int -> int)" :
      body.StartsWith("Box") ? "Box" : body.StartsWith("(r,") ? "((int -> int), (int -> int))" : "int";
    var program = await Resolve("""
      datatype Box = Box(f: int -> int)
      ghost function Unknown(r: int -> int): int { r(0) }
      """ + $"ghost function {{:termination_closed}} Subject(r: int -> int): {resultType} {{ {body} }}");
    var member = program.DefaultModuleDef!.DefaultClass.Members.Single(m => m.Name == "Subject");
    Assert.Equal(expected, ArgumentApplicationSummary.Compute(program).Contains(member));
  }

  [Fact]
  public async Task LeastFixpointDoesNotJustifyAMutualCycle() {
    var program = await Resolve("""
      ghost function {:termination_closed} A(r: int -> int, n: nat): int
        decreases n
      { if n == 0 then r(0) else B(r, n - 1) }
      ghost function {:termination_closed} B(r: int -> int, n: nat): int
        decreases n
      { if n == 0 then r(0) else A(r, n - 1) }
      """);
    Assert.Empty(ArgumentApplicationSummary.Compute(program));
  }

  [Fact]
  public async Task PropagatesInEitherDeclarationOrder() {
    var program = await Resolve("""
      ghost function {:termination_closed} Forward(r: int -> int): int { Leaf(r) }
      ghost function {:termination_closed} Leaf(r: int -> int): int { r(0) }
      """);
    Assert.Equal(2, ArgumentApplicationSummary.Compute(program).Count);
  }

  [Fact]
  public async Task ChecksDecreasesAndSelfCalls() {
    var program = await Resolve("""
      ghost function Unknown(r: int -> int): int { r(0) }
      ghost function {:termination_closed} BadContract(r: int -> int): int
        decreases Unknown(r)
      { r(0) }
      ghost function {:termination_closed} Self(r: int -> int, n: nat): int
        decreases n
      { if n == 0 then r(0) else Self(r, n - 1) }
      ghost function {:termination_closed} Changed(r: int -> int, n: nat): int
        decreases n
      { if n == 0 then r(0) else Changed((x: int) => r(x), n - 1) }
      """);
    Assert.Equal(["Self"], ArgumentApplicationSummary.Compute(program).Select(m => m.Name).ToArray());
  }

  [Fact]
  public async Task RejectsHiddenArrowSources() {
    var program = await Resolve("""
      datatype Box = Box(f: int -> int)
      ghost function {:termination_closed} Stored(box: Box): int { box.f(0) }
      ghost function {:termination_closed} Returned(r: int -> int): int -> int { r }
      ghost function {:termination_closed} AppliedReturn(r: int -> int): int { Returned(r)(0) }
      ghost function Unknown(n: int): int { n }
      ghost function {:termination_closed} Fresh(): int -> int { (x: int) => x }
      ghost function {:termination_closed} AppliedFresh(): int { Fresh()(0) }
      ghost function {:termination_closed} Consumer(r: int -> int): int { r(0) }
      ghost function {:termination_closed} UnclosedValue(): int { Consumer(Unknown) }
      """);
    Assert.Equal(["Consumer"], ArgumentApplicationSummary.Compute(program).Select(m => m.Name).ToArray());
  }

  [Theory]
  [InlineData("assert Apply(r) == Apply(r);")]
  [InlineData("Use(r);")]
  public async Task ForwardsActualArgumentEffects(string body) {
    var program = await Resolve("""
      ghost function {:termination_closed} Apply(r: nat -> nat): nat { r(0) }
      lemma {:termination_closed} Use(r: nat -> nat) { assert r(0) == r(0); }
      """ + "method {:termination_closed} Entry(r: nat -> nat) { " + body + " }");
    var entry = program.DefaultModuleDef!.DefaultClass.Members.Single(m => m.Name == "Entry");
    Assert.True(new ModularTerminationAnalysis(program).ReachesOpenDispatch(entry, ((ICallable)entry).EnclosingModule));
  }

  [Fact]
  public async Task BuiltinRelationResultMustBeConsumed() {
    // Use the library identity to exercise its existing returned-relation intrinsic.
    var program = await Resolve("""
      module Std.Termination.Orders {
        ghost function MultisetRel(r: int -> int): int -> int { r }
      }
      module Clients {
        import opened Std.Termination.Orders
        ghost function {:termination_closed} Applied(r: int -> int): int { MultisetRel(r)(0) }
        ghost function {:termination_closed} Escaped(r: int -> int): int -> int { MultisetRel(r) }
      }
      """, uri: DafnyMain.StandardLibrariesOrdersDooUri);
    Assert.Equal(["Applied"], ArgumentApplicationSummary.Compute(program).Select(m => m.Name).ToArray());
  }

  [Fact]
  public async Task ParameterApplicationCannotReturnAnUnknownFunction() {
    var program = await Resolve("""
      ghost function {:termination_closed} Escaped(factory: nat -> (nat -> nat)): nat -> nat { factory(0) }
      """);
    Assert.Empty(ArgumentApplicationSummary.Compute(program));
  }

  [Fact]
  public async Task OpaqueExportStillForwardsArgumentEffects() {
    var program = await Resolve("""
      module Provider {
        export API provides Apply
        ghost function {:termination_closed} Apply(r: nat -> nat): nat { r(0) }
      }
      module Client {
        import P = Provider`API
        method {:termination_closed} Entry(r: nat -> nat) { assert P.Apply(r) == P.Apply(r); }
      }
      """);
    var entry = program.Modules().SelectMany(m => m.TopLevelDecls).OfType<TopLevelDeclWithMembers>()
      .SelectMany(c => c.Members).Single(m => m.Name == "Entry");
    Assert.True(new ModularTerminationAnalysis(program).ReachesOpenDispatch(entry, ((ICallable)entry).EnclosingModule));
  }

  [Fact]
  public async Task RefinementCannotWeakenAnUnconditionalClosedPromise() {
    var program = await Resolve("""
      abstract module Signature {
        ghost function {:termination_closed} Apply(r: nat -> nat): nat
        method {:termination_closed} Entry(r: nat -> nat) { assert Apply(r) == Apply(r); }
      }
      module Implementation refines Signature {
        ghost function {:termination_closed} Apply(r: nat -> nat): nat { r(0) }
      }
      """);
    var refined = program.Modules().SelectMany(m => m.TopLevelDecls).OfType<TopLevelDeclWithMembers>()
      .SelectMany(c => c.Members).Single(m => m.Name == "Apply" && m.RefinementBase != null);
    Assert.DoesNotContain(refined, ArgumentApplicationSummary.Compute(program));
  }

  [Fact]
  public async Task RefinementPreservesAnExistingCheckedSummary() {
    var program = await Resolve("""
      abstract module Signature {
        ghost function {:termination_closed} Apply(r: nat -> nat): nat { r(0) }
      }
      module Implementation refines Signature { }
      """);
    var refined = program.Modules().SelectMany(m => m.TopLevelDecls).OfType<TopLevelDeclWithMembers>()
      .SelectMany(c => c.Members).Single(m => m.Name == "Apply" && m.RefinementBase != null);
    Assert.Contains(refined, ArgumentApplicationSummary.Compute(program));
  }

  [Theory]
  [InlineData(false, false, false)]
  [InlineData(true, true, false)]
  [InlineData(true, false, true)]
  public async Task SummaryRequiresOrdersAndEffectiveModularRules(bool orders, bool legacy, bool expected) {
    var program = await Resolve("ghost function {:termination_closed} Leaf(r: int -> int): int { r(0) }", orders, legacy);
    var leaf = program.DefaultModuleDef!.DefaultClass.Members.Single(m => m.Name == "Leaf");
    Assert.Equal(expected, new ModularTerminationAnalysis(program).AppliesOnlyItsArguments(leaf));
  }
}
