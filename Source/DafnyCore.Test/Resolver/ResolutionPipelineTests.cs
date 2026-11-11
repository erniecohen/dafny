using Microsoft.Boogie;
using Microsoft.Dafny;
using System.Reflection;
using System.Text.RegularExpressions;
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
      method Work(x: int) returns (y: int) { var z := x; y := z; assert y == x; }
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
    foreach (var count in new[] { 2, 4, 8, 8 }) {
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
    var (alone, aloneReporter) = await Resolve(Source[..Source.IndexOf("module W", StringComparison.Ordinal)], 4, refresh);
    Assert.Empty(Errors(aloneReporter));
    Assert.Equal(Keys(before)["_module.M"], Keys(alone)["_module.M"]);
    Assert.Equal(expected, Translate(alone).Single(p => p.Key == "M").Value);
    var (changed, changedReporter) = await Resolve(Source.Replace("bv13", "bv29"), 2, refresh);
    Assert.Empty(Errors(changedReporter));
    Assert.Equal(Keys(before)["_module.M"], Keys(changed)["_module.M"]);
    Assert.Equal(expected, Translate(changed).Single(p => p.Key == "M").Value);
    var (shifted, shiftedReporter) = await Resolve("module Before { function F(): bv33 { 0 } }\n" + Source, 4, refresh);
    Assert.Empty(Errors(shiftedReporter));
    Assert.Equal(Keys(before)["_module.M"], Keys(shifted)["_module.M"]);
    Assert.Equal(expected, Translate(shifted).Single(p => p.Key == "M").Value);
    var stateName = Regex.Match(expected, "captureState \"([^\"]+)\"").Groups[1].Value;
    Assert.NotEmpty(stateName);
    string ModelText() => $"*** MODEL\n*** STATE {stateName}\n*** END_STATE\n*** END_MODEL\n";
    var beforeState = DafnyModel.ExtractModel(before.Options, ModelText()).States.Last();
    var shiftedState = DafnyModel.ExtractModel(shifted.Options, ModelText()).States.Last();
    Assert.True(beforeState.StateContainsPosition());
    Assert.Equal(beforeState.GetLineId() + 1, shiftedState.GetLineId());
    Assert.Equal(beforeState.GetCharId(), shiftedState.GetCharId());
    var (renamed, renamedReporter) = await Resolve(Source, 8, refresh, uri: new Uri("untitled:renamed.dfy"));
    Assert.Empty(Errors(renamedReporter));
    Assert.Equal(Keys(before)["_module.M"], Keys(renamed)["_module.M"]);
    Assert.Equal(expected, Translate(renamed).Single(p => p.Key == "M").Value);
    Assert.Equal(Translate(before, true).Single(p => p.Key == "M").Value,
      Translate(renamed, true).Single(p => p.Key == "M").Value);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ErrorsAreReplayedInCanonicalOrder(bool refresh) {
    var source = "module A { function F(): int { false } } module B { function G(): bool { 1 } } module C { import X = A function G(): int { X.F() } }";
    var (baseline, baselineReporter) = await Resolve(source, 0, refresh);
    var expected = Errors(baselineReporter);
    Assert.NotEmpty(expected);
    foreach (var count in new[] { 1, 2, 4, 8 }) {
      var (program, reporter) = await Resolve(source, count, refresh);
      Assert.Equal(expected, Errors(reporter));
      Assert.Null(program.CardinalityValidationReceipt);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task TranslationIncludesLoweredArrayAndConstraintBuiltins(bool refresh) {
    var (program, reporter) = await Resolve("""
      module Signatures {
        predicate P(f: (int, int, int, int) -> int) { true }
        predicate Q(f: (int, int, int) --> int) { true }
      }
      module M {
        predicate Reads(f: (int, int) -> int) reads {}
        newtype Octet = x: int | 0 <= x && x < 256
        newtype Converted = x: int | 0 <= x && x <= (255 as bv8) as int
        method Arrays() {
          var a := new int[2, 3]((i, j) => i + j);
          assert a[0, 0] == 0;
        }
      }
      """, 8, refresh);
    Assert.Empty(Errors(reporter));
    Translate(program);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task TranslationDoesNotLeaveMutableAstState(bool refresh) {
    var (program, reporter) = await Resolve("""
      module Base {
        datatype D = X | Y
        ghost function Pick(n: int): int { var x: int :| x == n; x }
        ghost function Captured(I: int, i: int): int -> int { x => I + i + x }
        function Partial(d: D): int { match d case X => 0 }
        method Work() { assert L: true; reveal L; }
      }
      module M { import B = Base ghost function F(n: int): int { B.Pick(n) } }
      """, 4, refresh);
    Assert.Empty(Errors(reporter));
    var module = program.Modules().Single(m => m.Name == "Base");
    var nodes = new HashSet<INode>();
    void Visit(INode node) {
      if (nodes.Add(node)) {
        foreach (var child in node.Children) { Visit(child); }
        if (node is NestedMatchExpr expression) { Visit(expression.Flattened); }
        if (node is NestedMatchStmt statement) { Visit(statement.Flattened); }
      }
    }
    Visit(module);
    var lets = nodes.OfType<Microsoft.Dafny.LetExpr>().Where(e => !e.Exact).ToArray();
    var labels = nodes.OfType<AssertStmt>().Select(s => s.Label).Where(l => l != null).ToArray();
    Assert.NotEmpty(lets);
    Assert.NotEmpty(labels);
    var matches = nodes.OfType<IMatch>().ToDictionary(m => m, m => m.MissingCases.ToArray());
    Assert.NotEmpty(matches);
    // The caller's async context may already contain a fuel stack. Translation
    // must not borrow or initialize it, even when it translates nested scopes.
    var inherited = BoogieGenerator.FuelSetting.SavedContexts.Value;
    var translator = new BoogieGenerator(reporter, new ProofDependencyManager());
    var first = translator.DoTranslation(program, module);
    Assert.Equal(0, first.Resolve(program.Options));
    Assert.Equal(0, first.Typecheck(program.Options));
    foreach (var let in lets) { Assert.Null(let.GetTranslationDesugaring(translator)); }
    foreach (var label in labels) { Assert.Null(label!.E); }
    foreach (var match in matches) { Assert.Equal(match.Value, match.Key.MissingCases); }
    Assert.Same(inherited, BoogieGenerator.FuelSetting.SavedContexts.Value);
    var expected = Translate(program);
    Assert.Equal(expected, Translate(program));
    var culture = System.Globalization.CultureInfo.CurrentCulture;
    try {
      System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
      Assert.Equal(expected, Translate(program));
    } finally {
      System.Globalization.CultureInfo.CurrentCulture = culture;
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
  public void KeyVersionBindsTheResolverBuild() {
    var assembly = typeof(ProgramResolver).Assembly;
    Assert.Contains(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
      ModuleResolutionKey.DafnyVersion);
    Assert.Contains(typeof(ProgramResolver).Module.ModuleVersionId.ToString("D"), ModuleResolutionKey.DafnyVersion);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task VerificationMembershipKeysResolutionDiagnostics(bool refresh) {
    const string source = "module M { trait {:termination false} T {} }";
    var uri = new Uri("untitled:resolution.dfy");
    void Configure(Program program) {
      if (!program.Compilation.RootSourceUris.Contains(uri)) { program.Compilation.RootSourceUris.Add(uri); }
      program.Compilation.UrisToVerify = null;
    }
    var (root, rootReporter) = await Resolve(source, 2, refresh, configure: Configure, warnUncheckedTermination: true);
    var (library, libraryReporter) = await Resolve(source, 4, refresh, configure: program => {
      Configure(program);
      program.Compilation.AlreadyVerifiedRoots.Add(uri);
    }, warnUncheckedTermination: true);
    Assert.Empty(Errors(rootReporter));
    Assert.Empty(Errors(libraryReporter));
    Assert.Contains(rootReporter.AllMessages, d => d.Message.Contains("termination false"));
    Assert.DoesNotContain(libraryReporter.AllMessages, d => d.Message.Contains("termination false"));
    Assert.NotEqual(Keys(root)["_module.M"], Keys(library)["_module.M"]);
  }

  [Fact]
  public void EffectiveLegacyOptionsParticipateInTheKey() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    string Key() => ModuleResolutionKey.Compute("M", "source", "version",
      ModuleResolutionKey.Options(options), []);
    var original = Key();
    options.WarnShadowing = true;
    Assert.NotEqual(original, Key());
    options.WarnShadowing = false;
    options.ForbidNondeterminism = true;
    Assert.NotEqual(original, Key());
    options.ForbidNondeterminism = false;
    Assert.Equal(original, Key());
    options.Set(CommonOptionBag.ResolutionThreads, 8);
    Assert.Equal(original, Key());
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

  private static Dictionary<string, string> Translate(Program program, bool checksums = false) {
    var result = new Dictionary<string, string>();
    var flags = new BoogieGenerator.TranslatorFlags(program.Options) { InsertChecksums = checksums };
    foreach (var module in BoogieGenerator.Translate(program, program.Reporter, flags)) {
      Assert.Equal(0, module.Item2.Resolve(program.Options));
      Assert.Equal(0, module.Item2.Typecheck(program.Options));
      var text = new StringWriter();
      using (var writer = new TokenTextWriter(text, program.Options)) {
        module.Item2.Emit(writer);
      }
      result.Add(module.Item1, text.ToString());
    }
    return result;
  }

  private static async Task<(Program, BatchErrorReporter)> Resolve(string source, int threads, bool refresh,
    Action<ModuleDecl>? starting = null, Uri? uri = null, Action<Program>? configure = null,
    bool warnUncheckedTermination = false) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.ResolutionThreads, threads);
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.Set(CommonOptionBag.WarnUncheckedTermination, warnUncheckedTermination);
    options.ModelViewFile = "-";
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, uri ?? new Uri("untitled:resolution.dfy"), reporter);
    Assert.Empty(Errors(reporter));
    configure?.Invoke(parsed.Program);
    await new ProgramResolver(parsed.Program) { ResolutionWorkerStarting = starting }.Resolve(CancellationToken.None);
    return (parsed.Program, reporter);
  }
}
