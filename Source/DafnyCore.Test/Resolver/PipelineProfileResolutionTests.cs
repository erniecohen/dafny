using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Dafny;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class PipelineProfileResolutionTests {
  private const string Source = """
    module Api {
      export reveals F
      function F(x: int): int { x + 1 }
    }
    module Client {
      import A = Api
      function G(x: int): int { A.F(x) }
      module Inner { function H(): int { 1 } }
    }
    """;

  [Theory]
  [InlineData(0, false)]
  [InlineData(0, true)]
  [InlineData(2, false)]
  [InlineData(2, true)]
  public async Task WorkerEventsPreserveIdsPhasesAndResolution(int threads, bool refresh) {
    using var run = new ProfileRun();
    var off = await Parse(Source, threads, refresh);
    await new ProgramResolver(off).Resolve(CancellationToken.None);
    var on = await Parse(Source, threads, refresh, run.Path);
    var observedThreads = new ConcurrentBag<int>();
    await new ProgramResolver(on) {
      ResolutionWorkerStarting = _ => observedThreads.Add(Environment.CurrentManagedThreadId)
    }.Resolve(CancellationToken.None);
    Assert.False(off.Reporter.HasErrors);
    Assert.False(on.Reporter.HasErrors);
    Assert.Equal(off.ResolutionKeys.Values.Order(), on.ResolutionKeys.Values.Order());
    Assert.Equal(PrintBoogie(off), PrintBoogie(on));
    var events = run.Events();
    AssertGraphIds(events);
    var modules = Modules(events);
    Assert.All(modules, e => {
      Assert.False(e.GetProperty("cached").GetBoolean());
      Assert.Equal("completed", e.GetProperty("status").GetString());
      Assert.Contains(e.GetProperty("thread").GetInt32(), observedThreads);
      Assert.True(e.GetProperty("start").GetDouble() <= e.GetProperty("end").GetDouble());
      Assert.True(e.GetProperty("threadAllocBytes").GetInt64() >= 0);
      var phases = Phases(e);
      Assert.True(phases.Count(p => p == "preResolve") <= 1);
      if (e.GetProperty("kind").GetString() == "module") {
        Assert.Contains("signature", phases);
        Assert.Contains("body", phases);
      }
    });
    Assert.Contains(modules, e => Phases(e).Contains("exportSignature") && Phases(e).Contains("exportBody"));
    Assert.False(PipelineProfile.RecordingModulePhases);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(2)]
  public async Task CacheHitsHaveExplicitEventsAndNoReplayedPhases(int threads) {
    var cache = new OutputCache();
    using var first = new ProfileRun();
    using var second = new ProfileRun();
    var before = await Parse(Source, threads, true, first.Path);
    await new ReusingResolver(before, cache).Resolve(CancellationToken.None);
    var after = await Parse(Source, threads, true, second.Path);
    await new ReusingResolver(after, cache).Resolve(CancellationToken.None);
    Assert.False(before.Reporter.HasErrors);
    Assert.False(after.Reporter.HasErrors);
    AssertGraphIds(second.Events());
    Assert.All(Modules(first.Events()), e => Assert.False(e.GetProperty("cached").GetBoolean()));
    Assert.All(Modules(second.Events()), e => {
      Assert.True(e.GetProperty("cached").GetBoolean());
      Assert.Equal("completed", e.GetProperty("status").GetString());
      Assert.Empty(Phases(e));
    });
    Assert.False(PipelineProfile.RecordingModulePhases);
  }

  [Theory]
  [InlineData(0, false)]
  [InlineData(2, false)]
  [InlineData(0, true)]
  [InlineData(2, true)]
  public async Task CancellationAndFailureClearPhases(int threads, bool cancel) {
    using var run = new ProfileRun();
    var program = await Parse("module M { function F(): int { 1 } }", threads, false, run.Path);
    var resolver = new ProgramResolver(program) {
      ResolutionWorkerStarting = _ => {
        PipelineProfile.ModulePhase(program.Options, "interrupted");
        if (cancel) { throw new OperationCanceledException(); }
        throw new InvalidOperationException("injected worker failure");
      }
    };
    if (cancel) {
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.Resolve(CancellationToken.None));
    } else {
      await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.Resolve(CancellationToken.None));
    }
    var module = Assert.Single(Modules(run.Events()));
    Assert.Equal(cancel ? "cancelled" : "failed", module.GetProperty("status").GetString());
    Assert.Equal(JsonValueKind.Null, module.GetProperty("cached").ValueKind);
    Assert.Equal(new[] { "interrupted" }, Phases(module));
    Assert.False(PipelineProfile.RecordingModulePhases);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(2)]
  public async Task ResolutionDiagnosticsHaveModuleEvents(int threads) {
    using var run = new ProfileRun();
    var program = await Parse("module M { function F(): int { true } }", threads, false, run.Path);
    await new ProgramResolver(program).Resolve(CancellationToken.None);
    Assert.True(program.Reporter.HasErrors);
    var events = run.Events();
    AssertGraphIds(events);
    Assert.Contains(Modules(events), e => e.GetProperty("errors").GetInt32() > 0);
    Assert.All(Modules(events), e => Assert.Equal("completed", e.GetProperty("status").GetString()));
    Assert.False(PipelineProfile.RecordingModulePhases);
  }

  [Fact]
  public async Task CompletionOrderIsIndependentOfCanonicalGraphOrder() {
    using var run = new ProfileRun();
    var program = await Parse("module A { function F(): int { 1 } } module B { function G(): int { 2 } }",
      2, false, run.Path);
    await new ProgramResolver(program) {
      ResolutionWorkerStarting = declaration => {
        var graph = run.Events().Single(e => e.GetProperty("ev").GetString() == "moduleGraph");
        if (declaration.Name == graph.GetProperty("nodes")[0].GetProperty("module").GetString()) {
          Assert.True(SpinWait.SpinUntil(() => Modules(run.Events()).Any(e => e.GetProperty("id").GetInt32() == 1),
            TimeSpan.FromSeconds(10)), "The independent worker must complete while graph node 0 is held.");
        }
      }
    }.Resolve(CancellationToken.None);
    Assert.False(program.Reporter.HasErrors);
    var events = run.Events();
    AssertGraphIds(events);
    var ids = Modules(events).Select(e => e.GetProperty("id").GetInt32()).ToArray();
    Assert.True(Array.IndexOf(ids, 1) < Array.IndexOf(ids, 0));
  }

  [Fact]
  public void MeasurementOptionsDoNotChangeResolutionKeys() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    var before = ModuleResolutionKey.Options(options).OrderBy(p => p.Key).ToArray();
    options.Set(CommonOptionBag.PipelineProfile, "profile.jsonl");
    options.Set(CommonOptionBag.PipelineProfileHeap, true);
    options.Set(CommonOptionBag.PipelineProfileGcEvents, true);
    options.Set(CommonOptionBag.PipelineProfileHold, "hold");
    Assert.Equal(before, ModuleResolutionKey.Options(options).OrderBy(p => p.Key));
  }

  private static async Task<Program> Parse(string source, int threads, bool refresh, string? profile = null) {
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.ResolutionThreads, threads);
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    if (profile != null) { options.Set(CommonOptionBag.PipelineProfile, profile); }
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:profile.dfy"), new BatchErrorReporter(options));
    Assert.False(parsed.Program.Reporter.HasErrors);
    return parsed.Program;
  }

  private static string PrintBoogie(Program program) {
    var modules = new SortedDictionary<string, string>();
    foreach (var (name, boogie) in BoogieGenerator.Translate(program, program.Reporter,
               new BoogieGenerator.TranslatorFlags(program.Options))) {
      var text = new StringWriter();
      using var writer = new Microsoft.Boogie.TokenTextWriter(text, program.Options);
      boogie.Emit(writer);
      modules[name] = text.ToString();
    }
    return string.Join("\n", modules.Values);
  }

  private static JsonElement[] Modules(JsonElement[] events) => events.Where(e =>
    e.GetProperty("ev").GetString() == "fe" && e.GetProperty("stage").GetString() == "resolve.module").ToArray();

  private static string[] Phases(JsonElement module) => module.GetProperty("phases").EnumerateArray()
    .Select(p => p.GetProperty("phase").GetString()!).ToArray();

  private static void AssertGraphIds(JsonElement[] events) {
    var nodes = events.Single(e => e.GetProperty("ev").GetString() == "moduleGraph").GetProperty("nodes");
    var expected = nodes.EnumerateArray().Select(e => (e.GetProperty("id").GetInt32(), e.GetProperty("module").GetString()));
    var actual = Modules(events).Select(e => (e.GetProperty("id").GetInt32(), e.GetProperty("module").GetString()));
    Assert.Equal(expected.Order(), actual.Order());
  }

  // A complete-output cache exercises the same virtual hit/miss boundary used by the editor.
  private sealed class OutputCache {
    public SystemModuleManager? Builtins;
    public ConcurrentDictionary<string, ModuleResolutionResult> Outputs { get; } = new();
  }

  private sealed class ReusingResolver(Program program, OutputCache cache) : ProgramResolver(program) {
    protected override SystemModuleManager PrepareResolutionSystemModule(string optionsKey) {
      cache.Builtins = (cache.Builtins ?? SystemModuleManager).CreateResolutionView(SystemModuleManager);
      return cache.Builtins;
    }

    protected override ModuleResolutionResult ResolveModuleOutput(ModuleDecl declaration, string key,
      IReadOnlyList<ModuleResolutionResult> prerequisites, Func<ModuleResolutionResult> resolve, out bool cached) {
      if (cache.Outputs.TryGetValue(key, out var result)) {
        cached = true;
        return result;
      }
      cached = false;
      return cache.Outputs[key] = resolve();
    }
  }

  private sealed class ProfileRun : IDisposable {
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
    public string Path => System.IO.Path.Combine(directory, "profile.jsonl");

    public JsonElement[] Events() {
      using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      using var reader = new StreamReader(stream);
      // A worker may be writing the last line. Only complete JSONL records are observations.
      return reader.ReadToEnd().Split('\n').SkipLast(1).Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => {
          using var doc = JsonDocument.Parse(line);
          return doc.RootElement.Clone();
        }).ToArray();
    }

    public void Dispose() {
      Microsoft.Dafny.Type.ResetScopes();
      try { Directory.Delete(directory, true); } catch (IOException) { /* Windows keeps the writer open. */ }
    }
  }
}
