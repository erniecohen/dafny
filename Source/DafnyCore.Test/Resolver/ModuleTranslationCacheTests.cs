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
    Assert.NotEmpty(after.ProofDependencyManager.ProofDependenciesById);
    var unrelated = await Resolve(Source, "module Unrelated { function H(x: bv77): bv77 { x + 1 } }");
    cache.Translate(unrelated, unrelated.RawModules().Single(m => m.Name == "M"), new(unrelated.Options), default);
    Assert.Equal(2, cache.Hits);
    var edited = await Resolve(Source, "module Unrelated { function H(x: bv77): bv77 { x + 2 } }");
    cache.Translate(edited, edited.RawModules().Single(m => m.Name == "M"), new(edited.Options), default);
    Assert.Equal(3, cache.Hits);
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
    cancellation.Cancel();
    Assert.Throws<OperationCanceledException>(() => cache.Translate(first, module, new(first.Options), cancellation.Token));
    Assert.Equal(0, cache.Hits);
    var next = await Resolve(Source);
    cache.Translate(next, next.RawModules().Single(m => m.Name == "M"), new(next.Options), default);
    Assert.Equal(1, cache.Hits);
    Assert.NotEmpty(next.ProofDependencyManager.ProofDependenciesById);
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
    program.CardinalityValidationReceipt = null;
    Assert.Throws<InvalidOperationException>(() => cache.Translate(program, module, new(program.Options), default));
  }

  internal static async Task<Microsoft.Dafny.Program> Resolve(string source, string? unrelated = null, bool refresh = false) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
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
