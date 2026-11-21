using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test.Resolver;

/// <summary>
/// The per-module resolution identity (issue 261): the semantic key plus the generated-name prefix and
/// the identities of the prerequisites. It does not depend on where the module's source is.
/// </summary>
[Collection("Cardinality resolution")]
public class ModuleResolutionIdentityTests {
  private const string Base = "module A { function F(x: int): int { x + 1 } }";
  private const string User = "module B { import A function G(): int { A.F(1) } }";

  [Theory]
  [InlineData(1)]
  [InlineData(4)]
  public async Task InsertingLinesAboveAModuleKeepsItsIdentity(int threads) {
    var before = await Resolve(User + "\n" + Base, threads);
    var after = await Resolve("\n\n// a comment\n" + User + "\n\n  " + Base, threads);
    Assert.Equal(before.Keys, after.Keys);
    Assert.Equal(before.Identities, after.Identities);
  }

  /// <summary>Two checkouts of one tree: the same text under another path.</summary>
  [Fact]
  public async Task AnotherPathKeepsTheIdentity() {
    var first = await Resolve(User + "\n" + Base, 2, "file:///checkout-one/src/identity.dfy");
    var second = await Resolve(User + "\n" + Base, 2, "file:///elsewhere/checkout-two/src/identity.dfy");
    Assert.Equal(first.Keys, second.Keys);
    Assert.Equal(first.Identities, second.Identities);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(4)]
  public async Task ChangedTextChangesTheIdentityOfTheModuleAndItsDependents(int threads) {
    var before = await Resolve(User + "\n" + Base, threads);
    var after = await Resolve(User + "\n" + Base.Replace("x + 1", "x + 2"), threads);
    Assert.NotEqual(before.Identities["_module.A"], after.Identities["_module.A"]);
    // B's text is unchanged, and its key and identity both follow A's.
    Assert.NotEqual(before.Identities["_module.B"], after.Identities["_module.B"]);
    Assert.NotEqual(before.Keys["_module.B"], after.Keys["_module.B"]);
  }

  [Fact]
  public async Task UnchangedProgramsHaveEqualIdentities() {
    var first = await Resolve(User + "\n" + Base, 1);
    var second = await Resolve(User + "\n" + Base, 4);
    Assert.Equal(first.Identities, second.Identities);
    Assert.Equal(first.Keys.Keys.Order(), first.Identities.Keys.Order());
  }

  [Fact]
  public async Task AnUnrelatedLaterModuleLeavesTheIdentity() {
    var before = await Resolve(User + "\n" + Base, 2);
    var after = await Resolve(User + "\n" + Base + "\nmodule Unrelated { }", 2);
    Assert.Equal(before.Identities["_module.A"], after.Identities["_module.A"]);
    Assert.Equal(before.Identities["_module.B"], after.Identities["_module.B"]);
  }

  /// <summary>
  /// The two graph names share the first six base-32 characters of their name digests, so adding
  /// the second lengthens the first one's generated-name prefix without changing its key
  /// (the 2026-11-18 coordinator decision on 241).
  /// </summary>
  [Fact]
  public async Task AColludingSiblingChangesTheNamingPrefixAndTheIdentity() {
    const string owner = "module SnapshotGraphModule6913 { datatype D = C(x: int) function F(d: D): D { d.(x := 1) } }";
    var before = await Resolve(owner, 2);
    var after = await Resolve(owner + "\nmodule SnapshotGraphModule30048 { }", 2);
    const string name = "_module.SnapshotGraphModule6913";
    Assert.Equal(before.Keys[name], after.Keys[name]);
    Assert.NotEqual(before.Identities[name], after.Identities[name]);
  }

  [Fact]
  public async Task DefinitionsCarryTheirDeclarationsIdentity() {
    var (program, _) = await ResolveProgram(User + "\n" + Base, 4);
    var literals = program.ResolutionIdentities.Where(p => p.Key is LiteralModuleDecl).ToArray();
    Assert.Equal(3, literals.Length);
    foreach (var (declaration, identity) in literals) {
      Assert.Equal(identity, ((LiteralModuleDecl)declaration).ModuleDef.ResolutionIdentity);
    }
    Assert.Equal(program.ResolutionIdentities[program.DefaultModule], program.DefaultModuleDef.ResolutionIdentity);
  }

  [Fact]
  public void TheEncodingIsOrderIndependentAndEveryComponentCounts() {
    var prerequisites = new[] {
      new ModuleResolutionIdentity.Prerequisite("import", "A", "1"),
      new ModuleResolutionIdentity.Prerequisite("nested", "B", "2")
    };
    var context = ModuleResolutionIdentity.Context("Mabc_", prerequisites);
    Assert.Equal(context, ModuleResolutionIdentity.Context("Mabc_", prerequisites.Reverse()));
    Assert.NotEqual(context, ModuleResolutionIdentity.Context("Mabd_", prerequisites));
    Assert.NotEqual(context, ModuleResolutionIdentity.Context("Mabc_", prerequisites.Take(1)));
    Assert.NotEqual(context, ModuleResolutionIdentity.Context("Mabc_",
      new[] { prerequisites[0] with { Role = "refinement" }, prerequisites[1] }));
    Assert.NotEqual(context, ModuleResolutionIdentity.Context("Mabc_",
      new[] { prerequisites[0] with { Identity = "3" }, prerequisites[1] }));
    // Length delimiting: moving a character between two fields is a different context.
    Assert.NotEqual(ModuleResolutionIdentity.Context("Ma", [new("bc", "", "")]), ModuleResolutionIdentity.Context("Mab", [new("c", "", "")]));
    var identity = ModuleResolutionIdentity.Compute("key", context);
    Assert.Equal(64, identity.Length);
    Assert.NotEqual(identity, ModuleResolutionIdentity.Compute("key2", context));
    Assert.NotEqual(identity, ModuleResolutionIdentity.Compute("key", context + "0"));
  }

  private record Result(Dictionary<string, string> Keys, Dictionary<string, string> Identities);

  private static async Task<Result> Resolve(string source, int threads, string uri = "untitled:identity.dfy") {
    var (program, identities) = await ResolveProgram(source, threads, uri);
    var keys = program.ResolutionKeys.ToDictionary(p => p.Key.FullName, p => p.Value);
    return new(keys, identities);
  }

  private static async Task<(Program, Dictionary<string, string>)> ResolveProgram(string source, int threads,
    string uri = "untitled:identity.dfy") {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.ResolutionThreads, threads);
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri(uri), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Empty(reporter.AllMessagesByLevel[ErrorLevel.Error]);
    return (parsed.Program, parsed.Program.ResolutionIdentities.ToDictionary(p => p.Key.FullName, p => p.Value));
  }
}
