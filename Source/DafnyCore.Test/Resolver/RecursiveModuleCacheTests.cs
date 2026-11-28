using System.Collections.Immutable;
using Microsoft.Dafny;
using Microsoft.Dafny.Compilers;
using Microsoft.Extensions.Logging.Abstractions;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class RecursiveModuleCacheTests {
  [Theory]
  [InlineData(0, false)]
  [InlineData(3, false)]
  [InlineData(0, true)]
  [InlineData(3, true)]
  public async Task EveryMemberKeyCoversPrivateBodiesAndLowerDependencies(int threads, bool three) {
    var sources = new[] {
      "module A { import B function F(): int { B.F() } }",
      "module B { import " + (three ? "C" : "A") + " import Lower function F(): int { Lower.F() } lemma Private() { assert true; } }",
      three ? "module C { import A }" : "module C {}",
      "module Lower { function F(): int { 0 } }",
      "module Outside { function F(): int { 1 } }"
    };
    var before = await Resolve(sources, threads);
    Assert.False(before.Reporter.HasErrors);
    var keys = Keys(before);
    foreach (var edit in new[] { 1, 3 }) {
      var changed = sources.ToArray();
      changed[edit] = changed[edit].Replace("assert true;", "assert 0 == 0;").Replace("{ 0 }", "{ 2 }");
      var after = await Resolve(changed, threads);
      Assert.False(after.Reporter.HasErrors);
      var afterKeys = Keys(after);
      foreach (var name in three ? new[] { "A", "B", "C" } : new[] { "A", "B" }) {
        Assert.NotEqual(keys[name], afterKeys[name]);
      }
      Assert.Equal(keys["Outside"], afterKeys["Outside"]);
    }
    var unrelated = sources.ToArray();
    unrelated[4] = unrelated[4].Replace("{ 1 }", "{ 2 }");
    var outsideKeys = Keys(await Resolve(unrelated, threads));
    foreach (var name in new[] { "A", "B", "C", "Lower" }) { Assert.Equal(keys[name], outsideKeys[name]); }
  }

  [Theory]
  [InlineData(0)]
  [InlineData(3)]
  public async Task DependenceSummariesCannotSurviveASiblingEdit(int threads) {
    var sources = new[] {
      "module A { import B datatype T = T(n: int) { invariant B.Good() } }",
      "module B { import A ghost predicate Good() { true } }"
    };
    Assert.False((await Resolve(sources, threads)).Reporter.HasErrors);
    sources[1] = sources[1].Replace("{ true }", "{ forall t: A.T :: t.n >= 0 }");
    var changed = await Resolve(sources, threads);
    Assert.Contains(((BatchErrorReporter)changed.Reporter).AllMessagesByLevel[ErrorLevel.Error],
      d => d.Message.Contains("depends on 'T'"));
    sources[1] = sources[1].Replace("forall t: A.T :: t.n >= 0", "forall n: int :: n == n");
    Assert.False((await Resolve(sources, threads)).Reporter.HasErrors);

    // This edit leaves the invariant and the reached function's body unchanged. A previously
    // independent quantified datatype becomes T-dependent solely through its sibling declaration.
    sources[1] = "module B { import A datatype U = U(n: int) ghost predicate Good() { forall u: U :: true } }";
    Assert.False((await Resolve(sources, threads)).Reporter.HasErrors);
    sources[1] = sources[1].Replace("U(n: int)", "U(t: A.T)");
    var dependentType = await Resolve(sources, threads);
    Assert.Contains(((BatchErrorReporter)dependentType.Reporter).AllMessagesByLevel[ErrorLevel.Error],
      d => d.Message.Contains("depends on 'T'"));
  }

  [Fact]
  public async Task LibraryProducerCannotRetainOnlyOneMember() {
    var program = await Resolve(["module A { import B }", "module B { import A }"], 0, library: true);
    Assert.False(program.Reporter.HasErrors);
    var a = program.RawModules().Single(m => m.Name == "A");
    program.Compilation.UrisToCompile = new HashSet<Uri> { a.Origin.Uri };
    var output = new ConcreteSyntaxTree();
    program.Options.Backend.Compile(program, "group.dfy", output);
    Assert.Contains(((BatchErrorReporter)program.Reporter).AllMessagesByLevel[ErrorLevel.Error],
      d => d.Message.Contains("retain every member"));
    Assert.DoesNotContain("manifest.toml", output.ToString());
  }

  [Theory]
  [InlineData(0, false)]
  [InlineData(3, false)]
  [InlineData(0, true)]
  [InlineData(3, true)]
  public async Task PersistentHitsRetainTheWholeRecursiveGroup(int threads, bool three) {
    var sources = new[] {
      "module A { import B datatype T = T(n: int) { invariant B.Good(n) witness T(0) } " +
      "function F(n: nat): nat { if n == 0 then 0 else B.F(n - 1) } }",
      "module B { import " + (three ? "C" : "A") + " predicate Good(n: int) { n >= 0 } " +
      "function F(n: nat): nat { if n == 0 then 0 else " + (three ? "C" : "A") + ".F(n - 1) } " +
      "predicate Quant(s: seq<int>) { forall i :: 0 <= i < |s| ==> s[i] >= 0 } " +
      "export reveals * export Good }",
      three ? "module C { import A function F(n: nat): nat { if n == 0 then 0 else A.F(n - 1) } }" : "module C {}",
      "module Client { import A lemma Facts(t: A.T) { assert t.n >= 0; assert A.F(0) == 0; } }"
    };
    var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(directory);
    try {
      string? baseline = null;
      string[]? diagnostics = null;
      foreach (var phase in new[] { "fresh", "cold", "warm", "audit", "audit-again" }) {
        var profile = Path.Combine(directory, phase + ".jsonl");
        var program = await Resolve(sources, threads, cache: phase == "fresh" ? null : directory,
          profile: profile, check: phase.StartsWith("audit", StringComparison.Ordinal), separateOptions: true);
        Assert.False(program.Reporter.HasErrors, phase + ": " + string.Join("; ",
          ((BatchErrorReporter)program.Reporter).AllMessagesByLevel[ErrorLevel.Error].Select(d => d.Message)));
        var warnings = ((BatchErrorReporter)program.Reporter).AllMessagesByLevel[ErrorLevel.Warning]
          .Select(d => d.Message).Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(warnings);
        diagnostics ??= warnings;
        Assert.Equal(diagnostics, warnings);
        var modules = program.RawModules().Where(m => m.Name is "A" or "B" || three && m.Name == "C").ToArray();
        var group = modules[0].RecursiveGroup;
        Assert.NotNull(group);
        Assert.All(modules, module => Assert.Same(group, module.RecursiveGroup));
        Assert.Empty(group.Resolvers);
        Assert.Empty(group.Shared);
        Assert.All(group.Members, member => Assert.Contains(member.ModuleDef, modules));
        var text = new StringWriter();
        using (var writer = new Microsoft.Boogie.TokenTextWriter(text, program.Options)) {
          foreach (var (_, graph) in BoogieGenerator.Translate(program, program.Reporter,
                     new BoogieGenerator.TranslatorFlags(program.Options))) { graph.Emit(writer); }
        }
        baseline ??= text.ToString();
        Assert.Equal(baseline, text.ToString());
        if (phase.StartsWith("warm", StringComparison.Ordinal) || phase.StartsWith("audit", StringComparison.Ordinal)) {
          var snapshots = File.ReadAllLines(profile).Select(line => {
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            return doc.RootElement.Clone();
          }).Where(e => e.TryGetProperty("stage", out var stage) && stage.GetString() == "resolve.snapshot").ToArray();
          Assert.NotEmpty(snapshots);
          Assert.All(snapshots, e => Assert.StartsWith(phase == "warm" ? "hit" : "hit (checked ",
            e.GetProperty("state").GetString()));
          Assert.Equal(phase == "warm" ? 0 : 1, snapshots.Where(e => e.GetProperty("kind").GetString() == "module" &&
              e.GetProperty("module").GetString()!.EndsWith(".A", StringComparison.Ordinal))
            .Sum(e => e.GetProperty("resolverCalls").GetInt32()));
        }
      }
    } finally {
      Microsoft.Dafny.Type.ResetScopes();
      Directory.Delete(directory, true);
    }
  }

  private static Dictionary<string, string?> Keys(Microsoft.Dafny.Program program) =>
    program.RawModules().ToDictionary(m => m.Name, m => m.ResolutionKey);

  private static async Task<Microsoft.Dafny.Program> Resolve(string[] sources, int threads, bool library = false,
    string? cache = null, string? profile = null, bool check = false, bool separateOptions = false) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.UsingNewCli = true;
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.MemberInvariants, true);
    options.Set(CommonOptionBag.RecursiveModules, true);
    options.Set(CommonOptionBag.ResolutionThreads, threads);
    if (cache != null) { options.Set(CommonOptionBag.VerificationCache, cache); }
    options.Set(CommonOptionBag.ResolutionCache, cache != null);
    if (profile != null) { options.Set(CommonOptionBag.PipelineProfile, profile); }
    options.Set(CommonOptionBag.ResolutionCacheCheck, check);
    if (library) { options.Backend = new LibraryBackend(options); }
    var reporter = new BatchErrorReporter(options);
    var texts = sources.Select((source, index) => (new Uri($"file:///group-{index}.dfy"), source))
      .ToImmutableDictionary(p => p.Item1, p => p.source);
    var fs = new InMemoryFileSystem(texts);
    var files = texts.Keys.OrderBy(u => u.ToString()).Select(u =>
      DafnyFile.HandleDafnyFile(fs, reporter, separateOptions ? new DafnyOptions(options) : options, u, Token.NoToken, false)!).ToArray();
    var parsed = await new ProgramParser(NullLogger<ProgramParser>.Instance, fs)
      .ParseFiles("groups", files, reporter, default);
    await new ProgramResolver(parsed.Program).Resolve(default);
    return parsed.Program;
  }
}
