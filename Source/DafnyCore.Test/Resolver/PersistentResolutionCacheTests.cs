using Microsoft.Dafny;
using Microsoft.Dafny.Caching;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class PersistentResolutionCacheTests {
  [Fact]
  public async Task HandlesDeclarationsOutsideTheInitialList() {
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse("module Registered {} module Outside {}",
      new Uri("untitled:cache-names.dfy"), reporter);
    Assert.False(reporter.HasErrors);
    var program = parsed.Program;
    var modules = program.DefaultModuleDef.TopLevelDecls.OfType<LiteralModuleDecl>().ToArray();
    var registered = modules.Single(m => m.Name == "Registered");
    var outside = modules.Single(m => m.Name == "Outside");
    var builtins = program.SystemModuleManager;
    var store = new ArtifactStore(Path.GetTempPath(), "name-control", ArtifactLimits.Default);
    var cache = new PersistentResolutionCache(store, program, builtins, new ResolutionBuiltinCatalog(builtins),
      new Dictionary<TopLevelDeclWithMembers, Dictionary<string, MemberDecl>>(), [registered], [[]]);

    // Missing prerequisite records must miss, including declarations not indexed by the constructor.
    var closure = new PersistentResolutionCache.ModuleContext(registered, "local", "prefix", [], [outside]);
    Assert.Null(cache.Key(closure));
    var direct = new PersistentResolutionCache.ModuleContext(registered, "local", "prefix", [("import", outside)], []);
    Assert.NotNull(cache.Key(direct));
    cache.Uncacheable(outside);
    Assert.Null(cache.Key(closure));
    Assert.NotNull(cache.Key(new PersistentResolutionCache.ModuleContext(outside, "local", "prefix", [], [])));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(2)]
  public async Task RepeatedQuantifiedResolutionChecksRealWarmHits(int threads) {
    var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(directory);
    const string source = """
      module Cached {
        lemma Quantified(s: seq<int>) {
          assert forall i :: 0 <= i < |s| ==> s[i] == 0;
        }
        function Nested(s: seq<int>): bool {
          forall i :: 0 <= i < |s| ==> exists j :: 0 <= j < |s| && s[i] == s[j]
        }
      }
      """;
    try {
      string? baseline = null;
      foreach (var phase in new[] { "fresh", "cold", "warm", "warm-again" }) {
        Microsoft.Dafny.Type.ResetScopes();
        var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
        options.ApplyDefaultOptionsWithoutSettingsDefault();
        options.Set(CommonOptionBag.TypeSystemRefresh, true);
        options.Set(CommonOptionBag.ResolutionThreads, threads);
        options.Set(CommonOptionBag.VerificationCache, phase == "fresh" ? "" : Path.Combine(directory, "cache"));
        options.Set(CommonOptionBag.ResolutionCache, phase != "fresh");
        options.Set(CommonOptionBag.ResolutionCacheCheck, phase != "fresh");
        var profile = Path.Combine(directory, phase + ".jsonl");
        options.Set(CommonOptionBag.PipelineProfile, profile);
        var reporter = new BatchErrorReporter(options);
        var parsed = await ProgramParser.Parse(source, new Uri(Path.Combine(directory, "input.dfy")), reporter);
        await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
        Assert.Empty(reporter.AllMessagesByLevel[ErrorLevel.Error]);
        var text = new StringWriter();
        using (var writer = new Microsoft.Boogie.TokenTextWriter(text, options)) {
          foreach (var (_, boogie) in BoogieGenerator.Translate(parsed.Program, reporter,
                     new BoogieGenerator.TranslatorFlags(options))) { boogie.Emit(writer); }
        }
        baseline ??= text.ToString();
        Assert.Equal(baseline, text.ToString());
        var snapshots = File.ReadAllLines(profile).Select(line => {
          using var doc = System.Text.Json.JsonDocument.Parse(line);
          return doc.RootElement.Clone();
        }).Where(entry => entry.TryGetProperty("stage", out var stage) && stage.GetString() == "resolve.snapshot")
          .Select(entry => entry.GetProperty("state").GetString()!).ToArray();
        if (phase.StartsWith("warm", StringComparison.Ordinal)) {
          Assert.NotEmpty(snapshots);
          Assert.All(snapshots, state => Assert.StartsWith("hit (checked ", state));
        }
      }
    } finally {
      Microsoft.Dafny.Type.ResetScopes();
      try { Directory.Delete(directory, true); } catch (IOException) { /* Windows retains profile writers. */ }
    }
  }
}
