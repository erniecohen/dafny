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

  private static Dictionary<string, string?> Keys(Microsoft.Dafny.Program program) =>
    program.RawModules().ToDictionary(m => m.Name, m => m.ResolutionKey);

  private static async Task<Microsoft.Dafny.Program> Resolve(string[] sources, int threads, bool library = false) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.UsingNewCli = true;
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.MemberInvariants, true);
    options.Set(CommonOptionBag.RecursiveModules, true);
    options.Set(CommonOptionBag.ResolutionThreads, threads);
    if (library) { options.Backend = new LibraryBackend(options); }
    var reporter = new BatchErrorReporter(options);
    var texts = sources.Select((source, index) => (new Uri($"file:///group-{index}.dfy"), source))
      .ToImmutableDictionary(p => p.Item1, p => p.source);
    var fs = new InMemoryFileSystem(texts);
    var files = texts.Keys.OrderBy(u => u.ToString()).Select(u =>
      DafnyFile.HandleDafnyFile(fs, reporter, options, u, Token.NoToken, false)!).ToArray();
    var parsed = await new ProgramParser(NullLogger<ProgramParser>.Instance, fs)
      .ParseFiles("groups", files, reporter, default);
    await new ProgramResolver(parsed.Program).Resolve(default);
    return parsed.Program;
  }
}
