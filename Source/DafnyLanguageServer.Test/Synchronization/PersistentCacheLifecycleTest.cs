// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Dafny.Caching;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

public partial class PersistentCacheLifecycleTest : ClientBasedLanguageServerTest {
  public PersistentCacheLifecycleTest(ITestOutputHelper output) : base(output) { }
  public override Task InitializeAsync() => Task.CompletedTask;
  private const string Negative = "module Cached { lemma Negative() { assert false; } }";
  private const string Source = "module Cached { lemma Positive(x: int) ensures x + 1 > x {} }";

  private Task Start(string cache, int workers, bool audit = false,
    VerifyOnMode verifyOn = VerifyOnMode.Change, uint legacyCache = 0, bool reuseSolvers = false, bool ideReports = false,
    bool resolutionCache = true, int errorLimit = 5) => SetUp(options => {
    options.Set(ProjectManager.Verification, verifyOn);
    options.Set(LanguageServer.VerifySnapshots, legacyCache);
    options.Set(ProjectManager.ReuseSolvers, reuseSolvers);
    options.Set(CommonOptionBag.VerificationCache, cache);
    options.Set(CommonOptionBag.PersistentTranslation, cache.Length != 0);
    options.Set(CommonOptionBag.PersistentCounterexamples, cache.Length != 0);
    options.Set(CommonOptionBag.PersistentTranslationAudit, audit);
    options.Set(CommonOptionBag.PersistentIdeReports, ideReports);
    options.Set(CommonOptionBag.ResolutionCache, resolutionCache && cache.Length != 0);
    options.Set(CommonOptionBag.ResolutionCacheCheck, resolutionCache && cache.Length != 0);
    options.Set(CommonOptionBag.TranslationWorkers, workers);
    options.Set(BoogieOptionBag.Cores, 2U);
    options.Set(BoogieOptionBag.VerificationErrorLimit, errorLimit);
    options.ApplyBinding(BoogieOptionBag.VerificationErrorLimit);
    var solver = Environment.GetEnvironmentVariable("DAFNY_TEST_Z3") ??
      Path.Combine(AppContext.BaseDirectory, "z3", "bin", "z3-5.1.0");
    options.Set(BoogieOptionBag.SolverPath, new FileInfo(solver));
    options.Set(BoogieOptionBag.VerificationTimeLimit, 0U);
    options.Set(BoogieOptionBag.SolverResourceLimit, 1000000U);
  });

  private async Task<IdeState> Complete(TextDocumentItem document) {
    await client.WaitForNotificationCompletionAsync(document.Uri, CancellationToken);
    var manager = (await Projects.GetProjectManager(document.Uri))!;
    await manager.GetStateAfterResolutionAsync();
    await manager.VerifyEverythingAsync(document.Uri.ToUri());
    return await manager.States.Where(state => state.Version == document.Version &&
      state.Status == CompilationStatus.ResolutionSucceeded &&
      state.CanVerifyStates.TryGetValue(document.Uri.ToUri(), out var symbols) && symbols.Count != 0 &&
      symbols.Values.All(symbol => symbol.PreparationProgress == VerificationPreparationState.Done &&
        symbol.VerificationTasks.Count != 0 && symbol.VerificationTasks.Values.All(task =>
          task.RawStatus is VerificationWorkCompleted)))
      .FirstAsync().ToTask(CancellationToken).WaitAsync(TimeSpan.FromSeconds(60));
  }

  private static string[] Facts(IdeState state) => state.CanVerifyStates.Values.SelectMany(file => file.Values)
    .SelectMany(symbol => symbol.VerificationTasks.Values).Select(task => {
      var native = BoogieVerificationReporting.GetTask(task.Task);
      Assert.True(VerificationTaskFacts.HasSplit(native), "IDE reports require actual native splits");
      Assert.NotNull(native.Split.Implementation);
      var result = BoogieVerificationReporting.GetResult(((VerificationWorkCompleted)task.RawStatus).Result);
      Assert.All(result.Asserts, assertion => Assert.NotNull(assertion.Description));
      return $"{native.ScopeId}|{native.Split.SplitIndex}|{result.Outcome}|{result.ResourceCount}|{result.Asserts.Count}";
    }).OrderBy(fact => fact, StringComparer.Ordinal).ToArray();

  private static TaskCacheSession Session(IdeState state) => TaskCacheSession.For(((Program)state.ResolvedProgram!).Options)!;

  private async Task<string> SeededFacts(IdeState state, int seed) {
    var original = state.CanVerifyStates.Values.SelectMany(file => file.Values)
      .SelectMany(symbol => symbol.VerificationTasks.Values).First().Task;
    var task = BoogieVerificationReporting.GetTask(original).FromSeed(seed);
    Assert.Equal(seed, task.Split.RandomSeed);
    var completed = await task.TryRun()!.OfType<Completed>().FirstAsync().ToTask(CancellationToken);
    return $"{completed.Result.Outcome}|{completed.Result.ResourceCount}|{completed.Result.Asserts.Count}";
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task RestartAndUnsavedEditRevertPreserveNativeEvidence(int workers) {
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    var cache = Path.Combine(directory, "cache");
    var file = Path.Combine(directory, "input.dfy");
    // Disk content must never replace the open, unsaved overlay in a cache key.
    await File.WriteAllTextAsync(file, "lemma Disk() { assert false; }");
    await Start("", workers);
    var freshDocument = await CreateOpenAndWaitForResolve(Source, file);
    var fresh = await Complete(freshDocument);
    var expected = Facts(fresh);
    var seeded = await SeededFacts(fresh, 17);
    ApplyChange(ref freshDocument, new Range(0, 0, 0, Source.Length), Negative);
    var freshFailed = await Complete(freshDocument);
    var expectedNegative = Facts(freshFailed);
    Projects.Dispose();
    await client.Shutdown();

    await Start(cache, workers);
    var document = await CreateOpenAndWaitForResolve(Source, file);
    var cold = await Complete(document);
    Assert.Equal(expected, Facts(cold));
    Assert.NotEmpty(expected);
    Assert.True(Session(cold).SolverStarts > 0);
    Assert.True(Session(cold).Publications > 0, Session(cold).LastBypassReason);
    Projects.Dispose();
    await client.Shutdown();

    await Start(cache, workers);
    document = await CreateOpenAndWaitForResolve(Source, file);
    var warm = await Complete(document);
    Assert.Equal(expected, Facts(warm));
    Assert.True(Session(warm).Hits > 0, Session(warm).LastBypassReason);
    Assert.Equal(0, Session(warm).SolverStarts);
    Assert.NotSame(cold.ResolvedProgram, warm.ResolvedProgram);
    var persistent = PersistentModuleCache.For(((Program)warm.ResolvedProgram!).Options)!;
    Assert.Equal(0, persistent.KeyHits);
    Assert.Equal(0, persistent.GraphHits);
    Assert.True(PersistentModuleCache.For(((Program)cold.ResolvedProgram!).Options)!.ManifestPublications > 0);
    Assert.Equal(seeded, await SeededFacts(warm, 17));
    var hitsBeforeSeedReplay = Session(warm).Hits;
    Assert.Equal(seeded, await SeededFacts(warm, 17));
    Assert.True(Session(warm).Hits > hitsBeforeSeedReplay);

    ApplyChange(ref document, new Range(0, 0, 0, Source.Length),
      Negative);
    var failed = await Complete(document);
    Assert.Equal(expectedNegative, Facts(failed));
    Assert.Contains(failed.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values).SelectMany(t => t.Diagnostics),
      diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    Assert.NotEmpty(failed.Counterexamples);
    Assert.All(failed.Counterexamples, counterexample => Assert.NotNull(counterexample.Model));

    ApplyChange(ref document, new Range(0, 0, 0, Negative.Length), Source);
    var reverted = await Complete(document);
    Assert.Equal(expected, Facts(reverted));
    Assert.DoesNotContain(reverted.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values).SelectMany(t => t.Diagnostics),
      diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    // Hold a generation at the existing controlled-verifier seam, then cancel it by editing.
    // Completion of the reverted warm generation must not retain the old tasks or diagnostics.
    const string pendingSource = "module Cached { lemma {:neverVerify} Pending(x: int) ensures x + 1 > x {} }";
    ApplyChange(ref document, new Range(0, 0, 0, Source.Length), pendingSource);
    await client.WaitForNotificationCompletionAsync(document.Uri, CancellationToken);
    var pendingManager = (await Projects.GetProjectManager(document.Uri))!;
    await pendingManager.GetStateAfterResolutionAsync();
    await pendingManager.VerifyEverythingAsync(document.Uri.ToUri());
    var pendingVersion = document.Version;
    await pendingManager.States.Where(state => state.Version == pendingVersion &&
      state.CanVerifyStates.Values.SelectMany(file => file.Values)
        .SelectMany(symbol => symbol.VerificationTasks.Values).Any(task => !task.Task.IsIdle))
      .FirstAsync().ToTask(CancellationToken).WaitAsync(TimeSpan.FromSeconds(30));
    ApplyChange(ref document, new Range(0, 0, 0, pendingSource.Length), Source);
    var afterCancellation = await Complete(document);
    Assert.Equal(document.Version, afterCancellation.Version);
    Assert.Equal(expected, Facts(afterCancellation));
    Assert.Empty(afterCancellation.Counterexamples);

    Projects.Dispose();
    await client.Shutdown();
    await Start(cache, workers, audit: true);
    document = await CreateOpenAndWaitForResolve(Source, file);
    var audit = await Complete(document);
    Assert.Equal(expected, Facts(audit));
    var checkedCache = PersistentModuleCache.For(((Program)audit.ResolvedProgram!).Options)!;
    Assert.True(checkedCache.AuditChecks > 0);
    Assert.Equal(0, checkedCache.AuditMismatches);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task NativeTasksAreRequiredAtBothReplayBoundaries(int workers) {
    var directory = GetFreshTempPath();
    await Start(Path.Combine(directory, "cache"), workers);
    var document = await CreateOpenAndWaitForResolve(Source, Path.Combine(directory, "input.dfy"));
    var cold = await Complete(document);
    var cache = PersistentModuleCache.For(((Program)cold.ResolvedProgram!).Options)!;
    Assert.True(cache.ManifestPublications > 0);
    var manager = (await Projects.GetProjectManager(document.Uri))!;
    var resolution = (await manager.Compilation.Resolution)!;
    var module = resolution.ResolvedProgram.RawModules().Single(m => m.Name == "Cached");
    var verifier = new DafnyProgramVerifier(NullLogger<DafnyProgramVerifier>.Instance);
    var reachedPreparation = new InvalidOperationException("native preparation reached");
    // A detached module hit would return without asking the backend for current native tasks.
    // Both the translation-key lookup and the post-translation cutoff can otherwise hit here.
    try {
      var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => verifier.GetVerificationTasksAsync(
        new(false, () => throw reachedPreparation, RequiresNativeTasks: true), resolution, module, CancellationToken));
      Assert.Same(reachedPreparation, thrown);
    } finally {
      verifier.Dispose();
    }
    Assert.Equal(0, cache.KeyHits);
    Assert.Equal(0, cache.GraphHits);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task SeparateProjectRootsKeepCurrentEvidence(int workers) {
    var directory = GetFreshTempPath();
    var goodFile = Path.Combine(directory, "good", "input.dfy");
    var badFile = Path.Combine(directory, "bad", "input.dfy");
    foreach (var file in new[] { goodFile, badFile }) {
      Directory.CreateDirectory(Path.GetDirectoryName(file)!);
      await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(file)!, DafnyProject.FileName),
        "includes = [\"input.dfy\"]\n");
    }
    // Matching declaration/file names in independent projects must not identify a proof.
    var badSource = Source.Replace("x + 1 > x", "x + 1 < x");
    await Start("", workers);
    var good = await CreateOpenAndWaitForResolve(Source, goodFile);
    var bad = await CreateOpenAndWaitForResolve(badSource, badFile);
    var goodOracle = Facts(await Complete(good));
    var badOracle = Facts(await Complete(bad));
    Assert.NotEqual(goodOracle, badOracle);
    Projects.Dispose();
    await client.Shutdown();

    var cache = Path.Combine(directory, "shared-cache");
    for (var restart = 0; restart != 2; restart++) {
      await Start(cache, workers);
      good = await CreateOpenAndWaitForResolve(Source, goodFile);
      bad = await CreateOpenAndWaitForResolve(badSource, badFile);
      var goodState = await Complete(good);
      var badState = await Complete(bad);
      Assert.Equal(goodOracle, Facts(goodState));
      Assert.Equal(badOracle, Facts(badState));
      Assert.NotSame(await Projects.GetProjectManager(good.Uri), await Projects.GetProjectManager(bad.Uri));
      Assert.NotSame(goodState.ResolvedProgram, badState.ResolvedProgram);
      Assert.DoesNotContain(bad.Uri.ToUri(), goodState.CanVerifyStates.Keys);
      Assert.DoesNotContain(good.Uri.ToUri(), badState.CanVerifyStates.Keys);
      Assert.Empty(goodState.Counterexamples);
      Assert.NotEmpty(badState.Counterexamples);
      Assert.All(badState.Counterexamples, counterexample => Assert.NotNull(counterexample.Model));
      foreach (var (state, document) in new[] { (goodState, good), (badState, bad) }) {
        var session = Session(state);
        Assert.True(restart == 0 ? session.SolverStarts > 0 : session.Hits > 0, session.LastBypassReason);
        if (restart != 0) { Assert.Equal(0, session.SolverStarts); }
        foreach (var task in state.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values)) {
          var native = BoogieVerificationReporting.GetTask(task.Task);
          var origin = Assert.IsAssignableFrom<IOrigin>(native.Split.Implementation.tok);
          Assert.Equal(document.Uri.ToUri(), origin.Uri);
        }
      }
      // Editing one project's overlay must not clear the other project's failure or reuse its graph.
      ApplyChange(ref good, new Range(0, 0, 0, Source.Length), badSource);
      var changed = await Complete(good);
      Assert.Equal(badOracle, Facts(changed));
      Assert.NotEmpty(changed.Counterexamples);
      var other = await Complete(bad);
      Assert.Equal(badOracle, Facts(other));
      ApplyChange(ref good, new Range(0, 0, 0, badSource.Length), Source);
      Assert.Equal(goodOracle, Facts(await Complete(good)));
      Projects.Dispose();
      await client.Shutdown();
    }
  }

}
