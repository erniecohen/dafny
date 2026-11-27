using System.Runtime.CompilerServices;
using System.Reflection;
using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using Xunit;
using Bpl = Microsoft.Boogie;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class TranslationCopyTests {
  [Fact]
  public void OptionIsRegisteredAndOffByDefault() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    Assert.False(options.Get(CommonOptionBag.OptimizeTranslationCopy));
    OptionRegistry.CheckOptionsAreKnown([CommonOptionBag.OptimizeTranslationCopy]);
  }

  private class Parent(string text) {
    private readonly string inherited = text;
    public string Inherited => inherited;
  }

  private sealed class Fields(int number, string text, int? optional, DayOfWeek kind) : Parent(text) {
    public readonly int Number = number;
    public readonly int? Optional = optional;
    public readonly DayOfWeek Kind = kind;
  }

  private readonly struct ValueFields(int number, string text) {
    public readonly int Number = number;
    public readonly string Text = text;
  }

  [Fact]
  public void GeneratedAccessorsCopyPrivateInheritedReadonlyAndBoxedValueFields() {
    var copier = BoogieSnapshotCloner.CreateFieldCopier(typeof(Fields));
    if (RuntimeFeature.IsDynamicCodeSupported) { Assert.Equal("CopyBoogieFields", copier.Method.Name); }
    var cloner = new BoogieSnapshotCloner(null, false);
    var target = new Fields(0, "before", 0, DayOfWeek.Monday);
    copier(cloner, new Fields(42, "after", null, DayOfWeek.Friday), target);
    Assert.Equal(42, target.Number);
    Assert.Equal("after", target.Inherited);
    Assert.Null(target.Optional);
    Assert.Equal(DayOfWeek.Friday, target.Kind);
    copier(cloner, new Fields(17, "again", 23, DayOfWeek.Tuesday), target);
    Assert.Equal(23, target.Optional);
    object boxed = new ValueFields(1, "before");
    BoogieSnapshotCloner.CreateFieldCopier(typeof(ValueFields))(cloner, new ValueFields(2, "after"), boxed);
    Assert.Equal(2, ((ValueFields)boxed).Number);
    Assert.Equal("after", ((ValueFields)boxed).Text);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OptionSelectsTheCopierWithoutChangingBoogieProofMapsOrRawSmt(bool refresh) {
    var program = await Resolve(refresh);
    var module = program.RawModules().Single(m => m.Name == "Client");
    var expectedManager = new ProofDependencyManager();
    var before = BoogieSnapshotCloner.OptimizedClones;
    var expected = new BoogieGenerator(program.Reporter, expectedManager, new(program.Options)).DoTranslation(program, module);
    Assert.Equal(before, BoogieSnapshotCloner.OptimizedClones);
    var text = ModuleTranslationCache.Emit(expected, program.Options);
    program.Options.Set(CommonOptionBag.OptimizeTranslationCopy, true);
    var actualManager = new ProofDependencyManager();
    var actual = new BoogieGenerator(program.Reporter, actualManager, new(program.Options)).DoTranslation(program, module);
    Assert.Equal(before + (RuntimeFeature.IsDynamicCodeSupported ? 1 : 0), BoogieSnapshotCloner.OptimizedClones);
    Assert.NotEmpty(actual.TopLevelDeclarations);
    Assert.NotEmpty(actualManager.ProofDependenciesById);
    Assert.Equal(expectedManager.ProofDependenciesById.Keys.Order(), actualManager.ProofDependenciesById.Keys.Order());
    Assert.Equal(text, ModuleTranslationCache.Emit(actual, program.Options));
    Assert.Empty(expected.TopLevelDeclarations.Select(d => d.UniqueId).Intersect(actual.TopLevelDeclarations.Select(d => d.UniqueId)));
    Assert.Equal(await HistoryFreeTranslationTests.CaptureCommands(program, expected),
      await HistoryFreeTranslationTests.CaptureCommands(program, actual));
  }

  [Fact]
  public async Task ColdWarmAndBypassedTranslationCachesUsePrivateOptimizedCopies() {
    var program = await Resolve(true);
    program.Options.Set(CommonOptionBag.OptimizeTranslationCopy, true);
    var module = program.RawModules().Single(m => m.Name == "Client");
    var cache = new ModuleTranslationCache();
    var flags = new BoogieGenerator.TranslatorFlags(program.Options);
    var before = BoogieSnapshotCloner.OptimizedClones;
    var cold = cache.TranslateModule(program, module, flags, default, out var reused);
    Assert.False(reused);
    var text = ModuleTranslationCache.Emit(cold.Program, program.Options);
    cold.Program.AddTopLevelDeclaration(new Bpl.Axiom(Bpl.Token.NoToken, Bpl.Expr.False));
    var warm = cache.TranslateModule(program, module, flags, default, out reused);
    Assert.True(reused);
    Assert.Equal(text, ModuleTranslationCache.Emit(warm.Program, program.Options));
    Assert.NotSame(cold.Program, warm.Program);
    program.Options.PrintWithUniqueASTIds = true;
    cache.TranslateModule(program, module, flags, default, out reused);
    Assert.False(reused);
    Assert.True(cache.Bypasses > 0);
    Assert.Equal(before + (RuntimeFeature.IsDynamicCodeSupported ? 3 : 0), BoogieSnapshotCloner.OptimizedClones);
  }

  [Fact]
  public async Task PersistentTranslationUsesTheSelectedCopier() {
    var program = await Resolve(true);
    var module = program.RawModules().Single(m => m.Name == "Client");
    var translate = typeof(VerificationPreparationPipeline).GetMethod("TranslatePersistent",
      BindingFlags.Static | BindingFlags.NonPublic)!;
    (Bpl.Program Graph, PersistentModuleCache.ModuleIdentity Identity, ProofDependencyManager Dependencies) Run() =>
      ((Bpl.Program, PersistentModuleCache.ModuleIdentity, ProofDependencyManager))translate.Invoke(null,
        [program, module, new BoogieGenerator.TranslatorFlags(program.Options),
          new PersistentModuleCache.ModuleIdentity("test", null, null, null), null, null, null, default(CancellationToken)])!;
    var before = BoogieSnapshotCloner.OptimizedClones;
    var expected = Run();
    Assert.Equal(before, BoogieSnapshotCloner.OptimizedClones);
    program.Options.Set(CommonOptionBag.OptimizeTranslationCopy, true);
    var actual = Run();
    Assert.Equal(before + (RuntimeFeature.IsDynamicCodeSupported ? 1 : 0), BoogieSnapshotCloner.OptimizedClones);
    Assert.NotNull(actual.Identity.Text);
    Assert.NotNull(actual.Identity.Fingerprint);
    Assert.Equal(expected.Identity, actual.Identity);
    Assert.Equal(ModuleTranslationCache.Emit(expected.Graph, program.Options), ModuleTranslationCache.Emit(actual.Graph, program.Options));
    Assert.Equal(expected.Dependencies.ProofDependenciesById.Keys.Order(), actual.Dependencies.ProofDependenciesById.Keys.Order());
  }

  [Fact]
  public async Task TokenFieldsStillRebindToTheCurrentResolvedProgram() {
    var oldProgram = await Resolve(true);
    var currentProgram = await Resolve(true);
    ICanVerify Owner(Microsoft.Dafny.Program p) => p.RawModules().Single(m => m.Name == "Client")
      .TopLevelDecls.OfType<DefaultClassDecl>().Single().Members.OfType<Method>().Single(m => m.Name == "L");
    var oldOwner = Owner(oldProgram);
    var currentOwner = Owner(currentProgram);
    Assert.NotSame(oldOwner, currentOwner);
    var graph = new Bpl.Program();
    graph.AddTopLevelDeclaration(new Bpl.Axiom(new CanVerifyOrigin(oldOwner), Bpl.Expr.True));
    var bindings = new Dictionary<BoogieSnapshotCloner.SymbolBindingKey, ICanVerify> {
      [BoogieSnapshotCloner.SymbolKey(currentOwner)] = currentOwner
    };
    Assert.True(BoogieSnapshotCloner.TryClone(graph, out var copy, out var reason, bindings, true), reason);
    Assert.Same(currentOwner, Assert.IsType<CanVerifyOrigin>(copy!.Axioms.Single().tok).CanVerify);
    bindings.Clear();
    Assert.False(BoogieSnapshotCloner.TryClone(graph, out _, out _, bindings, true));
  }

  private static async Task<Microsoft.Dafny.Program> Resolve(bool refresh) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(HistoryFreeTranslationTests.Units,
      new Uri("untitled:translation-copy.dfy"), reporter);
    await new ProgramResolver(parsed.Program).Resolve(default);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(m => m.Message)));
    return parsed.Program;
  }
}
