using Microsoft.Boogie;
using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class ResolutionPipelineTests {
  private const string Source = """
    module Base {
      datatype D = X | Y(v: int)
      function Match(d: D): int { match d case X => 0 case Y(_) => 1 }
      function F(x: int): int { x + 1 }
      function Value(): int -> int { F }
    }
    module M {
      import B = Base
      function F(x: int): int { var t := (x, true, 3); B.Value()(t.0) }
      lemma Good(x: int) ensures F(x) == x + 1 { }
    }
    module W {
      import B = Base
      function F(x: bv13): bv13 { x << 2 }
    }
    """;

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task WorkerCountsPreserveKeysDiagnosticsAndBoogie(bool refresh) {
    var (baseline, reporter) = await Resolve(Source, 1, refresh);
    Assert.Empty(Errors(reporter));
    var expected = Translate(baseline);
    var keys = Keys(baseline);
    foreach (var count in new[] { 2, 8, 8 }) {
      var (program, actualReporter) = await Resolve(Source, count, refresh);
      Assert.Empty(Errors(actualReporter));
      Assert.Equal(keys, Keys(program));
      Assert.Equal(expected, Translate(program));
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task UnrelatedBuiltinGrowthPreservesModule(bool refresh) {
    var (before, beforeReporter) = await Resolve(Source, 1, refresh);
    Assert.Empty(Errors(beforeReporter));
    var expected = Translate(before).Single(p => p.Key == "M").Value;
    var (after, reporter) = await Resolve(Source + "\nmodule Unrelated { function F(x: bv77): (int, bool, int, int, int) { (0, true, 0, 0, 0) } }", 8, refresh);
    Assert.Empty(Errors(reporter));
    Assert.Equal(Keys(before)["_module.M"], Keys(after)["_module.M"]);
    Assert.Equal(expected, Translate(after).Single(p => p.Key == "M").Value);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ErrorsAreReplayedInCanonicalOrder(bool refresh) {
    var source = "module A { function F(): int { false } } module B { function G(): bool { 1 } } module C { import X = A function G(): int { X.F() } }";
    var (baseline, baselineReporter) = await Resolve(source, 0, refresh);
    var expected = Errors(baselineReporter);
    Assert.NotEmpty(expected);
    foreach (var count in new[] { 1, 2, 8 }) {
      var (program, reporter) = await Resolve(source, count, refresh);
      Assert.Equal(expected, Errors(reporter));
      Assert.Null(program.CardinalityValidationReceipt);
    }
  }

  [Fact]
  public async Task SiblingRefinementsOwnTheirExportSignatures() {
    var (program, reporter) = await Resolve("""
      module Base { export provides F function F(): int { 1 } }
      module Left refines Base { }
      module Right refines Base { }
      """, 8, true);
    Assert.Empty(Errors(reporter));
    foreach (var definition in program.ModuleSigs.Keys.Where(m => m.Name is "Base" or "Left" or "Right")) {
      foreach (var export in definition.TopLevelDecls.OfType<ModuleExportDecl>()) {
        var member = Assert.IsType<Microsoft.Dafny.Function>(Assert.Single(export.Exports).Decl);
        Assert.Same(definition, member.EnclosingClass.EnclosingModuleDefinition);
      }
    }
  }

  [Fact]
  public async Task ReadyDependentDoesNotWaitForUnrelatedModule() {
    using var ready = new ManualResetEventSlim();
    var dependentStarted = false;
    var slowReleasedByDependent = false;
    var (program, reporter) = await Resolve("module Slow {} module Fast {} module Ready { import F = Fast }", 2, true,
      declaration => {
        if (declaration.Name == "Slow") {
          slowReleasedByDependent = ready.Wait(TimeSpan.FromSeconds(10));
        } else if (declaration.Name == "Ready") {
          dependentStarted = true;
          ready.Set();
        }
      });
    Assert.Empty(Errors(reporter));
    Assert.True(dependentStarted);
    Assert.True(slowReleasedByDependent);
    Assert.NotNull(program.CardinalityValidationReceipt);
  }

  [Fact]
  public async Task RefinementIsAKeyDependency() {
    const string module = "module M refines Base { function F(): int { N } }";
    var (before, reporter) = await Resolve("module Base { const N: int := 1 } " + module, 2, true);
    Assert.Empty(Errors(reporter));
    var (after, _) = await Resolve("module Base { const N: bool := true } " + module, 2, true);
    Assert.NotEqual(Keys(before)["_module.M"], Keys(after)["_module.M"]);
  }

  [Fact]
  public void KeyEncodingIsOrderIndependentAndUnambiguous() {
    var options = new[] { KeyValuePair.Create("a", "bc"), KeyValuePair.Create("d", "e") };
    var prerequisites = new[] {
      new ModuleResolutionKey.Prerequisite("import", "A", "1"),
      new ModuleResolutionKey.Prerequisite("refinement", "B", "2")
    };
    string Key(string name, string source, string version, IEnumerable<KeyValuePair<string, string>> opts,
      IEnumerable<ModuleResolutionKey.Prerequisite> deps) => ModuleResolutionKey.Compute(name, source, version, opts, deps);
    var expected = Key("M", "body", "version", options, prerequisites);
    Assert.Equal(expected, Key("M", "body", "version", options.Reverse(), prerequisites.Reverse()));
    Assert.NotEqual(expected, Key("Other.M", "body", "version", options, prerequisites));
    Assert.NotEqual(expected, Key("M", "body2", "version", options, prerequisites));
    Assert.NotEqual(expected, Key("M", "body", "version2", options, prerequisites));
    Assert.NotEqual(expected, Key("M", "body", "version", new[] { KeyValuePair.Create("ab", "c"), options[1] }, prerequisites));
    Assert.NotEqual(expected, Key("M", "body", "version", options,
      new[] { prerequisites[0] with { Role = "export" }, prerequisites[1] }));
    Assert.NotEqual(expected, Key("M", "body", "version", options,
      new[] { prerequisites[0] with { Key = "changed" }, prerequisites[1] }));
  }

  private static Dictionary<string, string> Keys(Program program) => program.ResolutionKeys
    .OrderBy(p => p.Key.FullName, StringComparer.Ordinal).ToDictionary(p => p.Key.FullName, p => p.Value);

  private static string[] Errors(BatchErrorReporter reporter) => reporter.AllMessagesByLevel[ErrorLevel.Error]
    .Select(d => $"{d.Message}").ToArray();

  private static Dictionary<string, string> Translate(Program program) {
    var result = new Dictionary<string, string>();
    foreach (var module in BoogieGenerator.Translate(program, program.Reporter)) {
      var text = new StringWriter();
      using (var writer = new TokenTextWriter(text, program.Options)) {
        module.Item2.Emit(writer);
      }
      result.Add(module.Item1, text.ToString());
    }
    return result;
  }

  private static async Task<(Program, BatchErrorReporter)> Resolve(string source, int threads, bool refresh,
    Action<ModuleDecl>? starting = null) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.ResolutionThreads, threads);
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:resolution.dfy"), reporter);
    Assert.Empty(Errors(reporter));
    await new ProgramResolver(parsed.Program) { ResolutionWorkerStarting = starting }.Resolve(CancellationToken.None);
    return (parsed.Program, reporter);
  }
}
