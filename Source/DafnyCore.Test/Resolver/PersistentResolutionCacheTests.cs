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
}
