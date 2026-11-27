// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Dafny.Caching;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

public partial class PersistentCacheLifecycleTest {
  private const string SelectionSource = "lemma Selected(x: int) ensures x + 1 > x {}\nlemma Unselected() { assert false; }";

  private static IdeCanVerifyState AtLine(IdeState state, TextDocumentItem document, int line) =>
    state.CanVerifyStates[document.Uri.ToUri()].Single(pair => pair.Key.Start.Line == line).Value;

  private static bool Finished(IdeCanVerifyState symbol) =>
    symbol.PreparationProgress == VerificationPreparationState.Done && symbol.VerificationTasks.Count != 0 &&
    symbol.VerificationTasks.Values.All(task => task.RawStatus is VerificationWorkCompleted);

  private async Task<IdeState> SchedulingState(TextDocumentItem document, Func<IdeState, bool> predicate) {
    await client.WaitForNotificationCompletionAsync(document.Uri, CancellationToken);
    var manager = (await Projects.GetProjectManager(document.Uri))!;
    await manager.GetStateAfterResolutionAsync();
    return await manager.States.Where(state =>
      state.VersionedFiles.TryGetValue(document.Uri.ToUri(), out var version) && version == document.Version &&
      state.Status == CompilationStatus.ResolutionSucceeded && predicate(state))
      .FirstAsync().ToTask(CancellationToken).WaitAsync(TimeSpan.FromSeconds(30));
  }

  private static void Unscheduled(IdeCanVerifyState symbol) {
    Assert.Equal(VerificationPreparationState.NotStarted, symbol.PreparationProgress);
    Assert.Empty(symbol.VerificationTasks);
  }

  private static string[] SelectionFacts(IdeState state, TextDocumentItem document) => Facts(state with {
    CanVerifyStates = state.CanVerifyStates.SetItem(document.Uri.ToUri(),
      state.CanVerifyStates[document.Uri.ToUri()].Where(pair => pair.Key.Start.Line == 0).ToImmutableDictionary())
  });

  [Theory]
  [InlineData(1, VerifyOnMode.Never)]
  [InlineData(2, VerifyOnMode.Never)]
  [InlineData(1, VerifyOnMode.Save)]
  [InlineData(2, VerifyOnMode.Save)]
  public async Task ManualSelectionAndSaveRespectWarmEvidence(int workers, VerifyOnMode mode) {
    var directory = GetFreshTempPath();
    var file = Path.Combine(directory, "input.dfy");
    var cache = Path.Combine(directory, "cache");
    string[]? selectedOracle = null;
    string[]? allOracle = null;
    // Fresh is independent of the store; cold, restarted warm and audit share it.
    foreach (var phase in new[] { "fresh", "cold", "warm", "audit" }) {
      await Start(phase == "fresh" ? "" : cache, workers, audit: phase == "audit", verifyOn: mode);
      var document = await CreateOpenAndWaitForResolve(SelectionSource, file);
      var idle = await SchedulingState(document, state => state.CanVerifyStates.ContainsKey(document.Uri.ToUri()));
      Assert.Equal(2, idle.CanVerifyStates[document.Uri.ToUri()].Count);
      Assert.All(idle.CanVerifyStates[document.Uri.ToUri()].Values, Unscheduled);
      Assert.False(await client.RunSymbolVerification(document, new Position(20, 0), CancellationToken));

      Assert.True(await client.RunSymbolVerification(document, new Position(0, 8), CancellationToken));
      var selected = await SchedulingState(document, state => Finished(AtLine(state, document, 0)));
      Unscheduled(AtLine(selected, document, 1));
      Assert.Empty(selected.Counterexamples);
      Assert.All(AtLine(selected, document, 0).VerificationTasks.Values,
        task => Assert.Equal(PublishedVerificationStatus.Correct, task.Status));
      var selectedFacts = SelectionFacts(selected, document);
      Assert.NotEmpty(selectedFacts);
      selectedOracle ??= selectedFacts;
      Assert.Equal(selectedOracle, selectedFacts);

      await client.SaveDocumentAndWaitAsync(document, CancellationToken);
      if (mode == VerifyOnMode.Never) {
        // Saving must not turn a manual request into whole-document verification.
        var afterSave = await SchedulingState(document, state => Finished(AtLine(state, document, 0)));
        Unscheduled(AtLine(afterSave, document, 1));
        Assert.True(await client.RunSymbolVerification(document, new Position(1, 8), CancellationToken));
      }
      var all = await SchedulingState(document, state =>
        state.CanVerifyStates[document.Uri.ToUri()].Values.All(Finished));
      Assert.Contains(AtLine(all, document, 1).VerificationTasks.Values,
        task => task.Status == PublishedVerificationStatus.Error);
      Assert.NotEmpty(all.Counterexamples);
      Assert.All(all.Counterexamples, counterexample => Assert.NotNull(counterexample.Model));
      allOracle ??= Facts(all);
      Assert.Equal(allOracle, Facts(all));
      if (phase == "warm") {
        Assert.True(Session(all).Hits > 0, Session(all).LastBypassReason);
        Assert.Equal(0, Session(all).SolverStarts);
      } else if (phase == "cold") {
        Assert.True(Session(all).SolverStarts > 0);
      } else if (phase == "audit") {
        Assert.True(Session(all).SolverStarts > 0);
        Assert.Equal(0, Session(all).Hits);
        Assert.Equal(0, Session(all).Publications);
        var persistent = PersistentModuleCache.For(((Program)all.ResolvedProgram!).Options)!;
        Assert.True(persistent.AuditChecks > 0);
        Assert.Equal(0, persistent.AuditMismatches);
      }
      Projects.Dispose();
      await client.Shutdown();
    }
  }

  [Theory]
  [InlineData(1, false)]
  [InlineData(2, false)]
  [InlineData(1, true)]
  [InlineData(2, true)]
  public async Task AuditRunsFreshResponsesAcrossGenerations(int workers, bool reuseSolvers) {
    var directory = GetFreshTempPath();
    var file = Path.Combine(directory, "input.dfy");
    var cache = Path.Combine(directory, "cache");
    var position = new Position(0, Source.IndexOf("Positive", StringComparison.Ordinal));
    await Start(cache, workers, verifyOn: VerifyOnMode.Never, legacyCache: 1, reuseSolvers: reuseSolvers);
    var document = await CreateOpenAndWaitForResolve(Source, file);
    Assert.True(await client.RunSymbolVerification(document, position, CancellationToken));
    var cold = await SchedulingState(document, state => state.CanVerifyStates[document.Uri.ToUri()].Values.All(Finished));
    var expected = Facts(cold);
    Assert.True(Session(cold).Publications > 0);
    Projects.Dispose();
    await client.Shutdown();

    await Start(cache, workers, audit: true, verifyOn: VerifyOnMode.Never, legacyCache: 1, reuseSolvers: reuseSolvers);
    document = await CreateOpenAndWaitForResolve(Source, file);
    var sessions = new List<TaskCacheSession>();
    var starts = 0L;
    for (var generation = 0; generation != 2; generation++) {
      if (generation != 0) {
        ApplyChange(ref document, new Range(0, Source.Length, 0, Source.Length), " // current generation");
      }
      Assert.True(await client.RunSymbolVerification(document, position, CancellationToken));
      var audit = await SchedulingState(document, state => state.CanVerifyStates[document.Uri.ToUri()].Values.All(Finished));
      Assert.Equal(expected, Facts(audit));
      if (!sessions.Contains(Session(audit))) { sessions.Add(Session(audit)); }
      // Reusing the engine can retain a prior compilation's recording factory.
      var currentStarts = sessions.Sum(session => session.SolverStarts);
      Assert.True(currentStarts > starts, "each audited generation must run a solver");
      starts = currentStarts;
      Assert.All(sessions, session => {
        Assert.Equal(0, session.Hits);
        Assert.Equal(0, session.Publications);
      });
      var persistent = PersistentModuleCache.For(((Program)audit.ResolvedProgram!).Options)!;
      Assert.Equal(0, persistent.AuditMismatches);
    }
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task ExplicitCancelAndCloseReopenKeepCurrentEvidence(int workers) {
    var directory = GetFreshTempPath();
    var file = Path.Combine(directory, "input.dfy");
    var cache = Path.Combine(directory, "cache");
    var position = new Position(0, Source.IndexOf("Positive", StringComparison.Ordinal));
    await Start("", workers, verifyOn: VerifyOnMode.Never);
    var document = await CreateOpenAndWaitForResolve(Source, file);
    Assert.True(await client.RunSymbolVerification(document, position, CancellationToken));
    var fresh = await SchedulingState(document, state => state.CanVerifyStates[document.Uri.ToUri()].Values.All(Finished));
    var expected = Facts(fresh);
    Projects.Dispose();
    await client.Shutdown();

    await Start(cache, workers, verifyOn: VerifyOnMode.Never);
    var pendingSource = Source.Replace("lemma Positive", "lemma {:neverVerify} Positive");
    document = await CreateOpenAndWaitForResolve(pendingSource, file);
    position = new Position(0, pendingSource.IndexOf("Positive", StringComparison.Ordinal));
    Assert.True(await client.RunSymbolVerification(document, position, CancellationToken));
    var pending = await SchedulingState(document, state => state.CanVerifyStates[document.Uri.ToUri()].Values
      .Any(symbol => symbol.VerificationTasks.Values.Any(task => !task.Task.IsIdle)));
    var work = pending.CanVerifyStates[document.Uri.ToUri()].Values.SelectMany(symbol => symbol.VerificationTasks.Values).First().Task;
    // The controlled verifier's observable terminates only when this exact task is cancelled.
    // Observe that termination so an initial stale state cannot make the cancel check vacuous.
    var cancellation = work.TryRun()!.Materialize().FirstAsync().ToTask(CancellationToken);
    await client.CancelSymbolVerification(document, position, CancellationToken);
    var terminal = await cancellation.WaitAsync(TimeSpan.FromSeconds(10));
    Assert.Equal(NotificationKind.OnError, terminal.Kind);
    Assert.IsAssignableFrom<OperationCanceledException>(terminal.Exception);
    await client.CancelSymbolVerification(document, position, CancellationToken);
    Assert.Equal(0, Session(pending).Publications);
    var resultsDirectory = Path.Combine(cache, "v1", "results");
    if (Directory.Exists(resultsDirectory)) {
      Assert.Empty(Directory.EnumerateFiles(resultsDirectory, "data", SearchOption.AllDirectories));
    }

    ApplyChange(ref document, new Range(0, 0, 0, pendingSource.Length), Source);
    position = new Position(0, Source.IndexOf("Positive", StringComparison.Ordinal));
    Assert.True(await client.RunSymbolVerification(document, position, CancellationToken));
    var completed = await SchedulingState(document, state => state.CanVerifyStates[document.Uri.ToUri()].Values.All(Finished));
    Assert.Equal(expected, Facts(completed));
    Assert.Empty(completed.Counterexamples);
    Assert.All(completed.CanVerifyStates[document.Uri.ToUri()].Values.SelectMany(symbol => symbol.VerificationTasks.Values),
      task => Assert.Empty(task.Diagnostics));
    Assert.True(Session(completed).Publications > 0, Session(completed).LastBypassReason);
    var oldManager = (await Projects.GetProjectManager(document.Uri))!;
    client.CloseDocument(document);
    await client.WaitForNotificationCompletionAsync(document.Uri, CancellationToken);
    document = await CreateOpenAndWaitForResolve(Source, file);
    Assert.NotSame(oldManager, await Projects.GetProjectManager(document.Uri));
    Assert.True(await client.RunSymbolVerification(document, position, CancellationToken));
    var reopened = await SchedulingState(document, state => state.CanVerifyStates[document.Uri.ToUri()].Values.All(Finished));
    Assert.NotSame(completed.ResolvedProgram, reopened.ResolvedProgram);
    Assert.Equal(expected, Facts(reopened));
    Assert.Empty(reopened.Counterexamples);
    Assert.True(Session(reopened).Hits > 0, Session(reopened).LastBypassReason);
    Assert.Equal(0, Session(reopened).SolverStarts);
    Assert.All(reopened.CanVerifyStates[document.Uri.ToUri()].Values.SelectMany(symbol => symbol.VerificationTasks.Values), task => {
      Assert.Empty(task.Diagnostics);
      var native = BoogieVerificationReporting.GetTask(task.Task);
      Assert.Equal(document.Uri.ToUri(), Assert.IsAssignableFrom<IOrigin>(native.Split.Implementation.tok).Uri);
    });
    Projects.Dispose();
    await client.Shutdown();
    // A server restart removes that live cache, so require genuine persistent hits too.
    await Start(cache, workers, verifyOn: VerifyOnMode.Never);
    document = await CreateOpenAndWaitForResolve(Source, file);
    Assert.True(await client.RunSymbolVerification(document, position, CancellationToken));
    var restarted = await SchedulingState(document, state => state.CanVerifyStates[document.Uri.ToUri()].Values.All(Finished));
    Assert.Equal(expected, Facts(restarted));
    Assert.Empty(restarted.Counterexamples);
    Assert.True(Session(restarted).Hits > 0, Session(restarted).LastBypassReason);
    Assert.Equal(0, Session(restarted).SolverStarts);
  }
}
