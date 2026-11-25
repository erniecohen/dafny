using System.Collections.Concurrent;
using DafnyCore.Test.Resolver;
using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using Xunit;
using Bpl = Microsoft.Boogie;

namespace DafnyCore.Test.Verification;

[Collection("Cardinality resolution")]
public class WitnessTranslationTests {
  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task WitnessChecksPreserveSharedConstraintsUnderStress(bool explicitWitness, bool cached) {
    var source = $$"""
      module S {
        type T = v: int | true {{(explicitWitness ? "witness 1" : "")}}
      }
      module U {
        import S
        lemma L(t: S.T) ensures t == t {}
      }
      module V {
        import S
        lemma L(t: S.T) ensures t == t {}
      }
      module W {
        import S
        lemma L(t: S.T) ensures t == t {}
      }
      """;
    var expected = await Translate(1, 0);
    foreach (var (workers, stress) in new[] { (4, -1), (4, 7), (2, 0) }) {
      var actual = await Translate(workers, stress);
      Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
      foreach (var name in expected.Keys) {
        Assert.Equal(expected[name], actual[name]);
      }
    }

    async Task<Dictionary<string, (string Text, string Fingerprint)>> Translate(int workers, int stress) {
      ModuleTranslationCache.ProcessCache.Clear();
      var program = await ModuleTranslationCacheTests.Resolve(source, refresh: true, cached: cached);
      program.Options.Set(CommonOptionBag.TranslationWorkers, workers);
      program.Options.Set(CommonOptionBag.TranslationWorkersStress, stress);
      Assert.False(program.Options.Get(CommonOptionBag.PersistentTranslation));
      Assert.Equal(workers, VerificationPreparationPipeline.TranslationWorkers(program, out _));
      var modules = BoogieGenerator.VerifiableModules(program).ToArray();
      Assert.Equal(4, modules.Length);
      var declaration = modules.Single(m => m.Name == "S").TopLevelDecls.OfType<SubsetTypeDecl>().Single();
      var constraint = declaration.Constraint;
      var origin = constraint.Origin;
      // Capture locations before translation: computing them must not conceal an AST mutation.
      var locations = SourceLocations.For(program);
      var captured = new ConcurrentDictionary<string, (string Text, string Fingerprint)>();
      using var pipeline = new VerificationPreparationPipeline();
      await Task.WhenAll(modules.Select(module => pipeline.PrepareAsync(new(false, program, null), module,
        false, () => new Backend(), default, graph => {
          var fingerprint = BoogieGraphFingerprint.TryCompute(graph, program.Options.EmitDebugInformation,
            locations, out var unsupported);
          Assert.True(fingerprint != null, unsupported);
          Assert.True(captured.TryAdd(module.Name, (ModuleTranslationCache.Emit(graph, program.Options), fingerprint!)));
        })));
      Assert.Equal(4, captured.Count);
      Assert.Same(constraint, declaration.Constraint);
      if (workers > 1) { Assert.Same(origin, constraint.Origin); }
      return captured.ToDictionary(p => p.Key, p => p.Value);
    }
  }

  private sealed class Backend : IVerificationBackend<Bpl.Program> {
    public Task<VerificationPreparation> PrepareAsync(Bpl.Program input, CancellationToken cancellationToken) =>
      Task.FromResult(VerificationPreparation.Ready([]));
  }
}
