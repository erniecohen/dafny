using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class TranslationGlobalInputsTests {
  private const string Source = """
    module A {
      trait T {
        lemma Step() decreases {:global} 0
        function Get(): int decreases {:global} 0
      }
      lemma Invoke(t: T) decreases {:global} 1 { t.Step(); }
      predicate P(n: nat) { n == 0 || P(n - 1) }
      lemma {:induction n} Induction(n: nat, t: T)
        ensures P(n)
        decreases {:global} n + 1
        decreases n
      { if n > 0 { t.Step(); Induction(n - 1, t); } }
      function High(t: T): int decreases {:global} 1 { t.Get() }
      method Certify() { var h := bounded.certify(0, High); }
      function F(f: int -> int): int { 0 }
    }
    module M {
      import A
      lemma Good(t: A.T) decreases {:global} 2 { A.Invoke(t); }
      lemma TooLow(t: A.T) decreases {:global} 0 { A.Invoke(t); }
      lemma Bad() { assert false; }
    }
    """;

  private static Task<Program> Resolve(string source = Source, string? unrelated = null, bool enabled = true) =>
    ModuleTranslationCacheTests.Resolve(source, unrelated, refresh: true, modularTermination: true, globalInputs: enabled);

  [Fact]
  public async Task FrozenColdAndWarmObligationsAndRawSmtEqualNative() {
    var cache = new ModuleTranslationCache();
    foreach (var name in new[] { "A", "M" }) {
      var native = await Resolve(enabled: false);
      var module = native.RawModules().Single(m => m.Name == name);
      var graph = new BoogieGenerator(native.Reporter, new(), new(native.Options)).DoTranslation(native, module);
      var text = ModuleTranslationCache.Emit(graph, native.Options);
      var commands = await HistoryFreeTranslationTests.CaptureCommands(native, graph);
      foreach (var warm in new[] { false, true }) {
        var program = await Resolve();
        var next = program.RawModules().Single(m => m.Name == name);
        var actual = cache.Translate(program, next, new(program.Options), default, out var reused);
        Assert.True(cache.Bypasses == 0, cache.LastBypassReason);
        Assert.Equal(warm, reused);
        Assert.Equal(text, ModuleTranslationCache.Emit(actual, program.Options));
        Assert.Equal(commands, await HistoryFreeTranslationTests.CaptureCommands(program, actual));
        Assert.True(TranslationGlobalInputs.For(program, next)!.Complete);
        if (name == "A") {
          var step = next.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(t => t.Members).Single(m => m.Name == "Step");
          Assert.True(TranslationGlobalInputs.For(program, next)!.Read(step, next).Dispatch);
        }
      }
    }
    Assert.Equal(2, cache.Hits);
    Assert.Equal(2, cache.Translations);
  }

  [Fact]
  public async Task NegativeAnswersAreKeyedButUnrelatedFactsAreNot() {
    var before = await Resolve();
    var a = before.RawModules().Single(m => m.Name == "A");
    var view = TranslationGlobalInputs.For(before, a)!;
    var member = a.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(t => t.Members).Single(m => m.Name == "F");
    Assert.False(view.Read(member, a).ReachesDispatch);
    var after = await Resolve(Source.Replace("{ 0 }", "{ f(0) }"));
    var changed = after.RawModules().Single(m => m.Name == "A");
    var changedMember = changed.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(t => t.Members).Single(m => m.Name == "F");
    var changedView = TranslationGlobalInputs.For(after, changed)!;
    Assert.True(changedView.Read(changedMember, changed).ReachesDispatch);
    Assert.NotEqual(view.Digest, changedView.Digest);
    Assert.NotEqual(view.FinalIdentity, changedView.FinalIdentity);
    var unrelated = await Resolve(unrelated: "module Other { function H(f: int -> int): int { f(0) } }");
    var same = unrelated.RawModules().Single(m => m.Name == "A");
    Assert.Equal(a.ResolutionIdentity, same.ResolutionIdentity);
    Assert.Equal(view.Digest, TranslationGlobalInputs.For(unrelated, same)!.Digest);
    Assert.Equal(view.FinalIdentity, TranslationGlobalInputs.For(unrelated, same)!.FinalIdentity);
  }

  [Theory]
  [InlineData("reaching")]
  [InlineData("arguments")]
  [InlineData("dispatch")]
  public async Task ChangedGlobalAnswerWithTheSameLocalIdentityMisses(string answer) {
    var first = await Resolve();
    var module = first.RawModules().Single(m => m.Name == "A");
    var cache = new ModuleTranslationCache();
    cache.Translate(first, module, new(first.Options), default);
    var second = await Resolve();
    var next = second.RawModules().Single(m => m.Name == "A");
    Assert.Equal(module.ResolutionIdentity, next.ResolutionIdentity);
    // Isolate the key boundary from local AST changes: a changed current analysis answer must
    // invalidate even when all local resolution identities are unchanged.
    var member = next.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(t => t.Members)
      .Single(m => m.Name == (answer == "dispatch" ? "Step" : "F"));
    var analysis = ModularTerminationAnalysis.Of(second);
    if (answer == "dispatch") {
      member.TerminationContract = member.TerminationContract! with { IsDispatchSlot = false };
    } else {
      var field = typeof(ModularTerminationAnalysis).GetField(answer == "arguments" ? "argumentSummaries" : "reaching", System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic)!;
      if (answer == "arguments") { ((HashSet<MemberDecl>)field.GetValue(analysis)!).Add(member); }
      else { ((HashSet<Declaration>)field.GetValue(analysis)!).Add(member); }
    }
    Assert.NotEqual(TranslationGlobalInputs.For(first, module)!.Digest, TranslationGlobalInputs.For(second, next)!.Digest);
    cache.Translate(second, next, new(second.Options), default, out var reused);
    Assert.False(reused);
    Assert.Equal(2, cache.Translations);
    Assert.Equal(0, cache.Hits);
  }

  [Fact]
  public async Task CheckedArgumentSummaryIsPreservedAndReused() {
    const string source = "module A { ghost function {:termination_closed} Leaf(f: int -> int): int { f(0) } }";
    var native = await ModuleTranslationCacheTests.Resolve(source, refresh: true, modularTermination: true, decreasesOrders: true);
    var nativeModule = native.RawModules().Single(m => m.Name == "A");
    var expected = ModuleTranslationCache.Emit(new BoogieGenerator(native.Reporter, new(), new(native.Options))
      .DoTranslation(native, nativeModule), native.Options);
    var program = await ModuleTranslationCacheTests.Resolve(source, refresh: true, modularTermination: true, globalInputs: true, decreasesOrders: true);
    var module = program.RawModules().Single(m => m.Name == "A");
    var leaf = module.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(t => t.Members).Single(m => m.Name == "Leaf");
    Assert.True(TranslationGlobalInputs.For(program, module)!.Read(leaf, module).ArgumentsOnly);
    var cache = new ModuleTranslationCache();
    foreach (var warm in new[] { false, true }) {
      var graph = cache.Translate(program, module, new(program.Options), default, out var reused);
      Assert.Equal(warm, reused);
      Assert.Equal(expected, ModuleTranslationCache.Emit(graph, program.Options));
    }
  }

  [Fact]
  public async Task LegacyApiCallerCannotCreateAGlobalSnapshot() {
    var program = await ModuleTranslationCacheTests.Resolve(Source, modularTermination: true, globalInputs: true);
    var module = program.RawModules().Single(m => m.Name == "A");
    Assert.Null(TranslationGlobalInputs.For(program, module));
    var cache = new ModuleTranslationCache();
    var graph = cache.Translate(program, module, new(program.Options), default);
    Assert.NotEmpty(graph.Implementations);
    Assert.Equal(1, cache.Bypasses);
    Assert.Equal(0, cache.EntryCount);
  }

  [Fact]
  public async Task ConflictingNominalViewsAreIneligible() {
    var program = await Resolve();
    var module = program.RawModules().Single(m => m.Name == "A");
    var members = module.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(t => t.Members).ToArray();
    var first = members.Single(m => m.Name == "F");
    var other = members.Single(m => m.Name == "High");
    var analysis = ModularTerminationAnalysis.Of(program);
    Assert.False(analysis.ReachesOpenDispatch(first, module));
    Assert.True(analysis.ReachesOpenDispatch(other, module));
    // A synthetic ambiguous view exercises admission; it is not submitted for verification.
    other.NameNode = first.NameNode;
    Assert.False(TranslationGlobalInputs.For(program, module)!.Complete);
    Assert.NotNull(ModuleTranslationCache.BypassReason(program, module, new(program.Options)));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task UnclassifiedPostpassesRetainNativeTranslation(bool testRunner) {
    var program = await Resolve();
    if (testRunner) { program.Options.Set(RunAllTestsMainMethod.IncludeTestRunner, true); }
    else { program.Options.AdditionalPluginArguments.Add("unclassified"); }
    var module = program.RawModules().Single(m => m.Name == "A");
    Assert.Null(TranslationGlobalInputs.For(program, module));
    var cache = new ModuleTranslationCache();
    cache.Translate(program, module, new(program.Options), default);
    cache.Translate(program, module, new(program.Options), default);
    Assert.Equal(2, cache.Bypasses);
    Assert.Equal(0, cache.Hits);
  }

  [Fact]
  public async Task UnlistedReadUsesNativeAnswerAndDisablesPublicationAndReuse() {
    var program = await Resolve(unrelated: "module Other { function H(f: int -> int): int { f(0) } }");
    var module = program.RawModules().Single(m => m.Name == "A");
    var other = program.RawModules().Single(m => m.Name == "Other");
    var member = other.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(t => t.Members).Single(m => m.Name == "H");
    var view = TranslationGlobalInputs.For(program, module)!;
    var cache = new ModuleTranslationCache();
    cache.TranslateModule(program, module, new(program.Options), default, out _);
    Assert.Equal(1, cache.EntryCount);
    Assert.Equal(view.Current(member, other), view.Read(member, other));
    Assert.False(view.Complete);
    var fresh = cache.TranslateModule(program, module, new(program.Options), default, out var reused);
    Assert.False(reused);
    Assert.Null(fresh.Text);
    Assert.Equal(0, cache.Hits);
    Assert.Equal(2, cache.Translations);
    Assert.Equal(1, cache.Bypasses);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task UnlistedTranslationReadCannotPublishPersistentSnapshot(bool inProcess) {
    var directory = Path.Combine(Path.GetTempPath(), "global-inputs-" + Guid.NewGuid());
    try {
      var program = await Resolve();
      program.Options.Set(CommonOptionBag.TranslationCache, inProcess);
      program.Options.Set(CommonOptionBag.PersistentTranslation, true);
      program.Options.Set(CommonOptionBag.VerificationCache, directory);
      program.Options.Set(CommonOptionBag.VerificationLogFormat, Array.Empty<string>());
      ModuleTranslationCache.ProcessCache.Clear();
      var module = program.RawModules().Single(m => m.Name == "A");
      var view = TranslationGlobalInputs.For(program, module)!;
      // Simulate a future translator read omitted by the projection builder, after key construction.
      var field = typeof(TranslationGlobalInputs).GetField("answers", System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic)!;
      ((System.Collections.IDictionary)field.GetValue(view)!).Clear();
      Assert.True(view.Complete);
      using var pipeline = new VerificationPreparationPipeline();
      var prepared = await pipeline.PrepareAsync(new(false, program, null), module, false,
        () => new EmptyBackend(), default);
      Assert.True(prepared.Preparation.IsReady);
      Assert.False(view.Complete);
      var persistent = Microsoft.Dafny.Caching.PersistentModuleCache.TryFor(program.Options)!;
      Assert.NotNull(persistent);
      Assert.Equal(0, persistent.TranslationPublications);
      Assert.Contains(persistent.UnrecordedReasons.Keys, reason => reason.Contains("unlisted") || reason.Contains("not snapshotted"));
    } finally {
      ModuleTranslationCache.ProcessCache.Clear();
      if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
    }
  }

  private sealed class EmptyBackend : IVerificationBackend<Microsoft.Boogie.Program> {
    public Task<VerificationPreparation> PrepareAsync(Microsoft.Boogie.Program input, CancellationToken cancellationToken) =>
      Task.FromResult(VerificationPreparation.Ready([]));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task AuditRegeneratesNativeAnswersEvenWithAWarmProcessSnapshot(bool persistentKey) {
    var directory = Path.Combine(Path.GetTempPath(), "global-audit-" + Guid.NewGuid());
    var cache = ModuleTranslationCache.ProcessCache;
    cache.Clear();
    try {
      var program = await Resolve();
      program.Options.Set(CommonOptionBag.PersistentTranslation, true);
      program.Options.Set(CommonOptionBag.VerificationCache, directory);
      program.Options.Set(CommonOptionBag.VerificationLogFormat, Array.Empty<string>());
      Microsoft.Dafny.Caching.TaskCacheSession.For(program.Options);
      var module = program.RawModules().Single(m => m.Name == "M");
      if (!persistentKey) { module.ResolutionIdentity = null; } // Still eligible for the local process cache.
      program.Options.Set(CommonOptionBag.TranslationGlobalInputs, false);
      var expected = ModuleTranslationCache.Emit(new BoogieGenerator(program.Reporter, new(), new(program.Options))
        .DoTranslation(program, module), program.Options);
      program.Options.Set(CommonOptionBag.TranslationGlobalInputs, true);
      var view = TranslationGlobalInputs.For(program, module)!;
      var callee = program.RawModules().Single(m => m.Name == "A").TopLevelDecls.OfType<TopLevelDeclWithMembers>()
        .SelectMany(t => t.Members).Single(m => m.Name == "Invoke");
      var field = typeof(TranslationGlobalInputs).GetField("answers", System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic)!;
      var answers = (Dictionary<(MemberDecl, ModuleDefinition?), TranslationGlobalInputs.Facts>)field.GetValue(view)!;
      // Deliberately corrupt a frozen answer without changing its key. Audit must not share this fault.
      answers[(callee, module)] = view.Read(callee, module) with { ArgumentsOnly = true };
      var bad = cache.Translate(program, module, new(program.Options), default);
      Assert.NotEqual(expected, ModuleTranslationCache.Emit(bad, program.Options));
      cache.Translate(program, module, new(program.Options), default, out var reused);
      Assert.True(reused); // The process snapshot is genuinely eligible before audit.
      var hits = cache.Hits;
      program.Options.Set(CommonOptionBag.PersistentTranslationAudit, true);
      using var pipeline = new VerificationPreparationPipeline();
      string? captured = null;
      var prepared = await pipeline.PrepareAsync(new(false, program, null), module, false,
        () => new EmptyBackend(), default, graph => captured = ModuleTranslationCache.Emit(graph, program.Options));
      Assert.True(prepared.Preparation.IsReady);
      Assert.Equal(expected, captured);
      Assert.Equal(hits, cache.Hits);
      var persistent = Microsoft.Dafny.Caching.PersistentModuleCache.TryFor(program.Options)!;
      Assert.True(persistent.Audit);
      // An empty persistent store may record the independently regenerated translation.
      Assert.Equal(persistentKey ? 1 : 0, persistent.TranslationPublications);
    } finally {
      cache.Clear();
      if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
    }
  }
}
