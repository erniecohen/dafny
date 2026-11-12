using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DafnyDriver.Commands;
using Microsoft.Dafny;
using Xunit;

namespace IntegrationTests;

[Collection("Verification batch")]
public class TranslationCacheTests {
  private const string Source = """
    module A {
      function F(x: int): int { x + 1 }
      function Value(): int -> int { F }
    }
    module B {
      import X = A
      lemma Good(x: int) ensures X.Value()(x) == x + 1 {}
      lemma Bad(x: int) { assert x + 1 == x; }
    }
    """;

  private record Run(string Stdout, string Stderr, ExitValue Exit, string[] Tasks);

  [Theory]
  [InlineData(false, false)]
  [InlineData(true, false)]
  [InlineData(false, true)]
  [InlineData(true, true)]
  public async Task ColdWarmAndUncachedReportsAndResourceCountsAreIdentical(bool refresh, bool coverage) {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-translation-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    try {
      var source = Path.Combine(directory, "input.dfy");
      await File.WriteAllTextAsync(source, Source);
      var cache = ModuleTranslationCache.ProcessCache;
      cache.Clear();
      var baseline = await Verify(source, false, refresh, coverage);
      var before = cache.Hits;
      var cold = await Verify(source, true, refresh, coverage);
      var warm = await Verify(source, true, refresh, coverage);
      Assert.True(cold.Tasks.Length > 0, cold.Stdout + cold.Stderr + cache.LastBypassReason);
      Assert.True(warm.Tasks.Length > 0, warm.Stdout + warm.Stderr + cache.LastBypassReason);
      Assert.True(cache.Hits > before);
      Assert.Equal(ExitValue.VERIFICATION_ERROR, baseline.Exit);
      Assert.Equal(baseline.Stdout, cold.Stdout);
      Assert.Equal(baseline.Stderr, cold.Stderr);
      Assert.Equal(baseline.Exit, cold.Exit);
      Assert.Equal(baseline.Tasks, cold.Tasks);
      Assert.Equal(cold.Stdout, warm.Stdout);
      Assert.Equal(cold.Stderr, warm.Stderr);
      Assert.Equal(cold.Exit, warm.Exit);
      Assert.Equal(cold.Tasks, warm.Tasks);
      Assert.Contains(warm.Tasks, t => t.Contains("Invalid/"));
      Assert.Contains(warm.Tasks, t => t.Contains("Valid/"));
    } finally {
      ModuleTranslationCache.ProcessCache.Clear();
      Directory.Delete(directory, true);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ModularTerminationBypassKeepsCurrentModuleCoverageMaps(bool refresh) {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-translation-modular-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var cache = ModuleTranslationCache.ProcessCache;
    cache.Clear();
    var hits = cache.Hits;
    var bypasses = cache.Bypasses;
    try {
      var source = Path.Combine(directory, "input.dfy");
      await File.WriteAllTextAsync(source, Source);
      var baseline = await Verify(source, false, refresh, true, modularTermination: true);
      Assert.Equal(ExitValue.VERIFICATION_ERROR, baseline.Exit);
      Assert.NotEmpty(baseline.Tasks);
      foreach (var repeated in new[] { 0, 1 }) {
        var current = await Verify(source, true, refresh, true, modularTermination: true);
        Assert.Equal(baseline.Stdout, current.Stdout);
        Assert.Equal(baseline.Stderr, current.Stderr);
        Assert.Equal(baseline.Exit, current.Exit);
        Assert.Equal(baseline.Tasks, current.Tasks);
      }
      Assert.Equal(hits, cache.Hits);
      Assert.True(cache.Bypasses > bypasses);
      Assert.Equal(0, cache.EntryCount);
    } finally {
      cache.Clear();
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public async Task ExtractedCounterexampleTraceIsNonemptyAndIdenticalAcrossRepeatedRequests() {
    var source = Path.Combine(AppContext.BaseDirectory, "TestFiles", "LitTests", "LitTest", "git-issues",
      "git-issue-2026.dfy");
    var cache = ModuleTranslationCache.ProcessCache;
    cache.Clear();
    var hitsBefore = cache.Hits;
    var bypassesBefore = cache.Bypasses;
    try {
      var baseline = await Verify(source, false, false, false, extractCounterexample: true);
      var cold = await Verify(source, true, false, false, extractCounterexample: true);
      var warm = await Verify(source, true, false, false, extractCounterexample: true);
      Assert.Equal(ExitValue.VERIFICATION_ERROR, baseline.Exit);
      Assert.NotEmpty(baseline.Tasks);
      Assert.Contains("initial state:", baseline.Stdout);
      Assert.Contains("counterexampleLoopGuard", baseline.Stdout);
      foreach (var run in new[] { cold, warm }) {
        Assert.Equal(baseline.Stdout, run.Stdout);
        Assert.Equal(baseline.Stderr, run.Stderr);
        Assert.Equal(baseline.Exit, run.Exit);
        Assert.Equal(baseline.Tasks, run.Tasks);
      }
      Assert.Equal(hitsBefore, cache.Hits);
      Assert.Equal(0, cache.EntryCount);
      Assert.True(cache.Bypasses > bypassesBefore);
    } finally {
      cache.Clear();
    }
  }

  private static async Task<Run> Verify(string source, bool cached, bool refresh, bool coverage,
    bool extractCounterexample = false, bool modularTermination = false) {
    var stdout = new StringWriter();
    var stderr = new StringWriter();
    var options = new DafnyOptions(TextReader.Null, stdout, stderr);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.UnicodeCharacters, true);
    options.Set(CommonOptionBag.ResolutionThreads, 1);
    options.Set(CommonOptionBag.TranslationCache, cached);
    if (extractCounterexample) {
      options.Set(CommonOptionBag.ExtractCounterexample, true);
      options.ExtractCounterexample = true;
      options.EnhancedErrorMessages = 1;
    }
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.Set(CommonOptionBag.ModularTermination, modularTermination);
    options.Set(CommonOptionBag.WarnRedundantAssumptions, coverage);
    options.Set(CommonOptionBag.WarnContradictoryAssumptions, coverage);
    options.TrackVerificationCoverage = coverage;
    options.VcsCores = 1;
    options.RestartProverPerVC = true;
    options.DeterministicLiteralHashes = true;
    options.ResourceLimit = 16000000;
    options.TimeLimit = 0;
    if (Environment.GetEnvironmentVariable("Z3") is { } solver) {
      options.ProverOptions.Add("PROVER_PATH=" + solver);
    }
    options.CliRootSourceUris.Add(new Uri(source));
    var cli = CliCompilation.Create(options);
    using var compilation = cli.Compilation;
    cli.Start();
    var resolution = (await cli.Resolution)!;
    Assert.False(resolution.HasErrors);
    var tasks = new List<string>();
    using (var results = new Subject<CanVerifyResult>()) {
      VerifyCommand.ReportVerificationDiagnostics(cli, results);
      var summary = VerifyCommand.ReportVerificationSummary(cli, results);
      var dependencies = VerifyCommand.ReportProofDependencies(cli, resolution, results);
      await foreach (var result in cli.VerifyAllLazily()) {
        tasks.AddRange(result.Results.Select(p => p.Task.ScopeId + ":" + p.Result.VcNum + ":" +
          p.Result.Outcome + "/" + p.Result.ResourceCount));
        results.OnNext(result);
      }
      results.OnCompleted();
      await summary;
      await dependencies;
    }
    var exit = await cli.GetAndReportExitValue();
    return new Run(stdout.ToString(), stderr.ToString(), exit, tasks.ToArray());
  }
}
