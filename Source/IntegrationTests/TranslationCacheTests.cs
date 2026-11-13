using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using System.Security.Cryptography;
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

  private record Run(string Stdout, string Stderr, ExitValue Exit, string[] Tasks,
    long SolverStarts, long ResultHits);

  [Fact]
  public async Task TranslationSnapshotsAndPersistentResultsComposeWithoutWarmSolverCalls() {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-combined-cache-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var cache = ModuleTranslationCache.ProcessCache;
    cache.Clear();
    try {
      var source = Path.Combine(directory, "input.dfy");
      await File.WriteAllTextAsync(source, Source);
      var baseline = await Verify(source, false, true, false);
      var results = Path.Combine(directory, "results");
      var cold = await Verify(source, true, true, false, verificationCache: results);
      var translations = cache.Translations;
      var hits = cache.Hits;
      var warm = await Verify(source, true, true, false, verificationCache: results);
      Assert.Equal(ExitValue.VERIFICATION_ERROR, baseline.Exit);
      Assert.NotEmpty(cold.Tasks);
      Assert.True(cold.SolverStarts > 0);
      Assert.Equal(0, cold.ResultHits);
      Assert.Equal(0, warm.SolverStarts);
      Assert.Equal(cold.SolverStarts, warm.ResultHits);
      Assert.Equal(translations, cache.Translations);
      Assert.True(cache.Hits > hits);
      foreach (var run in new[] { cold, warm }) {
        Assert.Equal(baseline.Stdout, run.Stdout);
        Assert.Equal(baseline.Stderr, run.Stderr);
        Assert.Equal(baseline.Exit, run.Exit);
        Assert.Equal(baseline.Tasks, run.Tasks);
      }
    } finally {
      cache.Clear();
      Directory.Delete(directory, true);
    }
  }

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

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ImportedProofEditChangesResolutionKeyButKeepsClientBoogie(bool refresh) {
    const string source = """
      module Api {
        function Used(x: int): int { x + 1 }
        lemma Proof(x: int) ensures Used(x) == x + 1 { assert true; }
      }
      module Client {
        import A = Api
        lemma Reached(x: int) ensures A.Used(x) == x + 1 {}
      }
      """;
    var directory = Path.Combine(Path.GetTempPath(), "dafny-translation-cutoff-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    try {
      var cache = new ModuleTranslationCache();
      var before = await Translate(source, "before");
      var after = await Translate(source.Replace("assert true;", "assert 0 == 0;"), "after");
      Assert.NotEqual(before.Key, after.Key);
      Assert.Equal(before.Text, after.Text);
      Assert.Equal(2, cache.Translations);
      Assert.Equal(0, cache.Hits);
      var hash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(before.Text)));
      Assert.True(cache.TryGetBoogieText(hash, out var retained));
      Assert.Equal(before.Text, retained);

      async Task<(string Key, string Text)> Translate(string text, string label) {
        Microsoft.Dafny.Type.ResetScopes();
        var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
        options.ApplyDefaultOptionsWithoutSettingsDefault();
        options.Set(CommonOptionBag.UnicodeCharacters, true);
        options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
        var profile = Path.Combine(directory, label + ".jsonl");
        options.Set(CommonOptionBag.PipelineProfile, profile);
        var reporter = new BatchErrorReporter(options);
        var parsed = await ProgramParser.Parse(text, new Uri("untitled:translation-cutoff.dfy"), reporter);
        await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
        Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(d => d.Message)));
        PipelineProfile.For(options)!.Finish("translation-cutoff-test");
        var key = File.ReadLines(profile).Select(line => JsonDocument.Parse(line)).Where(document =>
            document.RootElement.TryGetProperty("stage", out var stage) && stage.GetString() == "resolve.module" &&
            document.RootElement.GetProperty("module").GetString() == "_module.Client")
          .Select(document => document.RootElement.GetProperty("key").GetString()!).Single();
        var module = parsed.Program.RawModules().Single(m => m.Name == "Client");
        var graph = cache.Translate(parsed.Program, module, new(options), default, out var reused);
        Assert.False(reused);
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        using (var writer = new Microsoft.Boogie.TokenTextWriter("translation-cache", output, false, options.PrettyPrint, options)) {
          graph.Emit(writer);
        }
        return (key, output.ToString());
      }
    } finally {
      Directory.Delete(directory, true);
    }
  }

  private static async Task<Run> Verify(string source, bool cached, bool refresh, bool coverage,
    bool extractCounterexample = false, bool modularTermination = false, string? verificationCache = null) {
    var stdout = new StringWriter();
    var stderr = new StringWriter();
    var options = new DafnyOptions(TextReader.Null, stdout, stderr);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.UnicodeCharacters, true);
    options.Set(CommonOptionBag.TranslationCache, cached);
    if (verificationCache != null) {
      options.Set(CommonOptionBag.VerificationCache, verificationCache);
    }
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
    var resultCache = Microsoft.Dafny.Caching.TaskCacheSession.TryFor(options);
    return new Run(stdout.ToString(), stderr.ToString(), exit, tasks.ToArray(),
      resultCache?.SolverStarts ?? 0, resultCache?.Hits ?? 0);
  }
}
