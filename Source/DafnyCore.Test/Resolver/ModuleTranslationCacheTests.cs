using Microsoft.Dafny;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Bpl = Microsoft.Boogie;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class ModuleTranslationCacheTests {
  private const string Source = """
    module Base {
      function F(x: int): int { x + 1 }
      function Value(): int -> int { F }
    }
    module M {
      import B = Base
      lemma T(x: int) ensures B.Value()(x) == x + 1 {}
    }
    """;

  [Fact]
  public async Task GlobalTranslationFactsWithoutAFinalizedKeyAlwaysRecompute() {
    var program = await Resolve(Source, modularTermination: true);
    var module = program.RawModules().Single(m => m.Name == "M");
    Assert.NotNull(module.ResolutionKey);
    var cache = new ModuleTranslationCache();
    cache.Translate(program, module, new(program.Options), default);
    cache.Translate(program, module, new(program.Options), default);
    Assert.Equal(0, cache.Hits);
    Assert.Equal(0, cache.EntryCount);
    Assert.Equal(2, cache.Translations);
    Assert.Equal(2, cache.Bypasses);
  }

  [Fact]
  public async Task ProofMapsBelongToOneModuleAndWarmConsumersAreIndependent() {
    var program = await Resolve(Source, "module Other { lemma Unrelated() { assert true; } }");
    var cache = new ModuleTranslationCache();
    var module = program.RawModules().Single(m => m.Name == "M");
    var other = program.RawModules().Single(m => m.Name == "Other");
    var first = cache.TranslateModule(program, module, new(program.Options), default, out var reused);
    Assert.False(reused);
    Assert.NotEmpty(first.ProofDependencies.ProofDependenciesById);
    var keys = first.ProofDependencies.idsByMemberName.Keys.Order().ToArray();
    var unrelated = cache.TranslateModule(program, other, new(program.Options), default, out _);
    Assert.NotSame(first.ProofDependencies, unrelated.ProofDependencies);
    Assert.Equal(keys, first.ProofDependencies.idsByMemberName.Keys.Order());
    Assert.DoesNotContain(keys, k => k.Contains("Unrelated"));
    var warm = cache.TranslateModule(program, module, new(program.Options), default, out reused);
    Assert.True(reused);
    Assert.NotSame(first.ProofDependencies, warm.ProofDependencies);
    Assert.Equal(keys, warm.ProofDependencies.idsByMemberName.Keys.Order());
    warm.ProofDependencies.ProofDependenciesById.Clear();
    Assert.NotEmpty(first.ProofDependencies.ProofDependenciesById);
    Assert.Empty(program.ProofDependencyManager.ProofDependenciesById);
  }

  [Fact]
  public async Task WarmGraphsAreIndependentAndTextIsExact() {
    var program = await Resolve(Source);
    var cache = new ModuleTranslationCache();
    var module = program.RawModules().Single(m => m.Name == "M");
    var flags = new BoogieGenerator.TranslatorFlags(program.Options);
    DafnyType.ResetScopes();
    var first = cache.Translate(program, module, flags, default);
    Assert.True(cache.Bypasses == 0, cache.LastBypassReason);
    var text = ModuleTranslationCache.Emit(first, program.Options);
    Assert.Equal(1, cache.Translations);
    first.AddTopLevelDeclaration(new Bpl.Constant(Bpl.Token.NoToken,
      new Bpl.TypedIdent(Bpl.Token.NoToken, "ConsumerMutation", Bpl.Type.Int), false));
    var second = cache.Translate(program, module, flags, default);
    Assert.Equal(1, cache.Hits);
    Assert.NotSame(first, second);
    Assert.Empty(first.TopLevelDeclarations.Select(d => d.UniqueId).Intersect(second.TopLevelDeclarations.Select(d => d.UniqueId)));
    Assert.Equal(text, ModuleTranslationCache.Emit(second, program.Options));
  }

  [Fact]
  public async Task SeparateCompilationsAndAnUnrelatedFileReuseOnlyUnchangedModules() {
    var cache = new ModuleTranslationCache();
    var before = await Resolve(Source);
    var module = before.RawModules().Single(m => m.Name == "M");
    var descriptorBefore = ModuleTranslationCache.DescriptorParts(before, module, new(before.Options));
    var first = cache.Translate(before, module, new BoogieGenerator.TranslatorFlags(before.Options), default);
    var text = ModuleTranslationCache.Emit(first, before.Options);
    var after = await Resolve(Source);
    var next = after.RawModules().Single(m => m.Name == "M");
    Assert.Equal(module.ResolutionKey, next.ResolutionKey);
    var descriptorAfter = ModuleTranslationCache.DescriptorParts(after, next, new(after.Options));
    var differences = descriptorBefore.Zip(descriptorAfter).Select((pair, i) => (pair, i))
      .Where(x => x.pair.First != x.pair.Second).Select(x => $"{descriptorBefore[x.i - 1]}: {x.pair.First} != {x.pair.Second}");
    Assert.True(descriptorBefore.SequenceEqual(descriptorAfter), string.Join("\n", differences));
    var second = cache.Translate(after, next, new BoogieGenerator.TranslatorFlags(after.Options), default);
    Assert.Equal(1, cache.Hits);
    Assert.Equal(text, ModuleTranslationCache.Emit(second, after.Options));
    Assert.Empty(after.ProofDependencyManager.ProofDependenciesById);
    var unrelated = await Resolve(Source, "module Unrelated { function H(x: bv77): bv77 { x + 1 } }");
    cache.Translate(unrelated, unrelated.RawModules().Single(m => m.Name == "M"), new(unrelated.Options), default);
    Assert.Equal(2, cache.Hits);
    var edited = await Resolve(Source, "module Unrelated { function H(x: bv77): bv77 { x + 2 } }");
    cache.Translate(edited, edited.RawModules().Single(m => m.Name == "M"), new(edited.Options), default);
    Assert.Equal(3, cache.Hits);
  }

  [Fact]
  public async Task GeneratedAbstractSignaturesWithOneOwnerKeyHaveDistinctTranslations() {
    var program = await Resolve("""
      abstract module Input { type T }
      abstract module Core { import A: Input type U = A.T }
      abstract module Builder { import P: Core }
      """);
    var modules = BoogieGenerator.VerifiableModules(program).ToArray();
    var siblings = modules.GroupBy(m => m.ResolutionKey)
      .Where(g => g.Count() > 1).SelectMany(g => g).ToArray();
    Assert.NotEmpty(siblings);
    var cache = new ModuleTranslationCache();
    var texts = new Dictionary<string, string>();
    foreach (var module in siblings) {
      var flags = new BoogieGenerator.TranslatorFlags(program.Options);
      var expected = new BoogieGenerator(program.Reporter, program.ProofDependencyManager, flags).DoTranslation(program, module);
      var actual = cache.Translate(program, module, flags, default);
      texts[module.FullName] = ModuleTranslationCache.Emit(expected, program.Options);
      Assert.Equal(texts[module.FullName], ModuleTranslationCache.Emit(actual, program.Options));
    }
    Assert.Equal(0, cache.Hits);
    Assert.Equal(siblings.Length, cache.Translations);
    Assert.True(texts.Values.Distinct().Count() > 1);
    foreach (var module in siblings) {
      var actual = cache.Translate(program, module, new(program.Options), default);
      Assert.Equal(texts[module.FullName], ModuleTranslationCache.Emit(actual, program.Options));
    }
    Assert.Equal(siblings.Length, cache.Hits);
  }

  [Fact]
  public async Task SourceLocationAndPreludeChangesCannotReuseARecord() {
    var cache = new ModuleTranslationCache();
    var before = await Resolve(Source);
    cache.Translate(before, before.RawModules().Single(m => m.Name == "M"), new(before.Options), default);
    var moved = await Resolve("\n\n" + Source);
    cache.Translate(moved, moved.RawModules().Single(m => m.Name == "M"), new(moved.Options), default);
    Assert.Equal(0, cache.Hits);
    Assert.Equal(2, cache.Translations);
    var prelude = File.ReadAllText(BoogieGenerator.PreludePath(before.Options));
    cache.Translate(before, before.RawModules().Single(m => m.Name == "M"),
      new(before.Options) { PreludeText = "// changed prelude\n" + prelude }, default);
    Assert.Equal(0, cache.Hits);
    Assert.Equal(3, cache.Translations);
  }

  [Fact]
  public void LiteralAliasesArePreservedAndUnknownMetadataIsRejected() {
    var graph = new Bpl.Program();
    var axiom = new Bpl.Axiom(Bpl.Token.NoToken, Bpl.Expr.True);
    graph.AddTopLevelDeclaration(axiom);
    Assert.True(BoogieSnapshotCloner.TryClone(graph, out var copy, out _));
    Assert.Same(Bpl.Expr.True, Assert.Single(copy!.TopLevelDeclarations.OfType<Bpl.Axiom>()).Expr);
    axiom.SetMetadata(0, new object());
    Assert.False(BoogieSnapshotCloner.TryClone(graph, out _, out _));
  }

  [Fact]
  public void TypeCopiesPreserveRuntimeTypeAndImmutableExpressionHashes() {
    var proxy = new Bpl.TypeProxy(Bpl.Token.NoToken, "p");
    Assert.True(proxy.Unify(Bpl.Type.Int));
    var variable = new Bpl.Constant(Bpl.Token.NoToken, new Bpl.TypedIdent(Bpl.Token.NoToken, "x", proxy), false);
    var identifier = new Bpl.IdentifierExpr(Bpl.Token.NoToken, variable, immutable: true);
    var graph = new Bpl.Program();
    graph.AddTopLevelDeclaration(variable);
    graph.AddTopLevelDeclaration(new Bpl.Axiom(Bpl.Token.NoToken, identifier));
    graph.AddTopLevelDeclaration(new Bpl.Constant(Bpl.Token.NoToken,
      new Bpl.TypedIdent(Bpl.Token.NoToken, "float", new Bpl.FloatType(24, 8)), false));
    Assert.True(BoogieSnapshotCloner.TryClone(graph, out var copy, out var reason), reason);
    var clonedVariable = copy!.Constants.Single(v => v.Name == "x");
    Assert.NotSame(proxy, Assert.IsType<Bpl.TypeProxy>(clonedVariable.TypedIdent.Type));
    Assert.Same(Bpl.Type.Int, ((Bpl.TypeProxy)clonedVariable.TypedIdent.Type).ProxyFor);
    var clonedIdentifier = Assert.IsType<Bpl.IdentifierExpr>(copy.Axioms.Single().Expr);
    Assert.Same(clonedVariable, clonedIdentifier.Decl);
    Assert.Equal(clonedIdentifier.ComputeHashCode(), clonedIdentifier.GetHashCode());
    Assert.NotSame(graph.Constants.Single(v => v.Name == "float").TypedIdent.Type,
      copy.Constants.Single(v => v.Name == "float").TypedIdent.Type);
  }

  [Fact]
  public async Task DebugPrintingOfProcessSpecificNodeIdentitiesBypassesReuse() {
    var program = await Resolve(Source);
    program.Options.PrintWithUniqueASTIds = true;
    var module = program.RawModules().Single(m => m.Name == "M");
    var cache = new ModuleTranslationCache();
    cache.Translate(program, module, new(program.Options), default);
    cache.Translate(program, module, new(program.Options), default);
    Assert.Equal(2, cache.Translations);
    Assert.Equal(2, cache.Bypasses);
    Assert.Equal(0, cache.Hits);
    Assert.Equal(0, cache.EntryCount);
  }

  [Fact]
  public async Task NullAndEmptyChecksumPrefixesAreDifferentInputs() {
    var program = await Resolve(Source);
    var module = program.RawModules().Single(m => m.Name == "M");
    var cache = new ModuleTranslationCache();
    cache.Translate(program, module, new(program.Options) { InsertChecksums = true }, default);
    cache.Translate(program, module, new(program.Options) { InsertChecksums = true, UniqueIdPrefix = "" }, default);
    Assert.Equal(0, cache.Hits);
    Assert.Equal(2, cache.Translations);
  }

  [Fact]
  public async Task EvictionCancellationAndChangedImportsRecompute() {
    var cache = new ModuleTranslationCache(maximumEntries: 1);
    var first = await Resolve(Source);
    var module = first.RawModules().Single(m => m.Name == "M");
    cache.Translate(first, module, new(first.Options), default);
    var changed = await Resolve(Source.Replace("x + 1", "x + 2"));
    cache.Translate(changed, changed.RawModules().Single(m => m.Name == "M"), new(changed.Options), default);
    cache.Translate(first, module, new(first.Options), default);
    Assert.Equal(3, cache.Translations);
    Assert.Equal(0, cache.Hits);
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();
    Assert.Throws<OperationCanceledException>(() => cache.Translate(first, module, new(first.Options), cancellation.Token));
    Assert.Equal(0, cache.Hits);
    var next = await Resolve(Source);
    cache.Translate(next, next.RawModules().Single(m => m.Name == "M"), new(next.Options), default);
    Assert.Equal(1, cache.Hits);
    Assert.Empty(next.ProofDependencyManager.ProofDependenciesById);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ModuleAloneInFullProgramAndWithUnrelatedModuleEmitsIdenticalText(bool refresh) {
    var alone = await Resolve(Source, refresh: refresh);
    var module = alone.RawModules().Single(m => m.Name == "M");
    var uncached = new BoogieGenerator(alone.Reporter, alone.ProofDependencyManager, new(alone.Options)).DoTranslation(alone, module);
    var expected = ModuleTranslationCache.Emit(uncached, alone.Options);
    var full = await Resolve(Source, "module Unrelated { function H(x: bv77): (int, bool, int) { (0, true, 0) } }", refresh);
    var fresh = new ModuleTranslationCache();
    foreach (var other in full.RawModules().Where(m => m.Name != "M")) {
      fresh.Translate(full, other, new(full.Options), default);
    }
    var actual = fresh.Translate(full, full.RawModules().Single(m => m.Name == "M"), new(full.Options), default);
    Assert.Equal(expected, ModuleTranslationCache.Emit(actual, full.Options));
  }

  [Fact]
  public async Task CurrentAdmissionIsRequiredEvenForAWarmEntry() {
    var program = await Resolve(Source);
    var cache = new ModuleTranslationCache();
    var module = program.RawModules().Single(m => m.Name == "M");
    cache.Translate(program, module, new(program.Options), default);
    var other = await Resolve(Source);
    Assert.Throws<InvalidOperationException>(() => cache.Translate(other, module, new(other.Options), default));
    program.CardinalityValidationReceipt = null;
    Assert.Throws<InvalidOperationException>(() => cache.Translate(program, module, new(program.Options), default));
  }

  internal static async Task<Microsoft.Dafny.Program> Resolve(string source, string? unrelated = null, bool refresh = false, bool modularTermination = false) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.ModularTermination, modularTermination);
    options.Set(CommonOptionBag.ResolutionThreads, 1);
    options.Set(CommonOptionBag.TranslationCache, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    var reporter = new BatchErrorReporter(options);
    var uri = new Uri("file:///translation-cache.dfy");
    var sources = ImmutableDictionary<Uri, string>.Empty.Add(uri, source);
    if (unrelated != null) { sources = sources.Add(new Uri("file:///unrelated.dfy"), unrelated); }
    var fs = new InMemoryFileSystem(sources);
    var parser = new ProgramParser(NullLogger<ProgramParser>.Instance, fs);
    var files = sources.Keys.OrderBy(u => u.ToString()).Select(u =>
      DafnyFile.HandleDafnyFile(fs, reporter, options, u, Token.NoToken, false)!).ToArray();
    var parsed = await parser.ParseFiles(uri.ToString(), files, reporter, default);
    await new ProgramResolver(parsed.Program).Resolve(default);
    Assert.False(reporter.HasErrors);
    return parsed.Program;
  }
}
