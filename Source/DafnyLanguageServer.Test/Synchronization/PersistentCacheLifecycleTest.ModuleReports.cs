// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Dafny.Caching;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

public partial class PersistentCacheLifecycleTest {
  private const string ReportSource = """
    module Cached {
      lemma {:isolate_assertions} First(x: int)
        ensures x + 1 > x
      { assert x == x; }
      lemma Second(x: int) ensures x + 1 > x { First(x); assert x + 1 > x; }
    }
    """;

  private async Task<IdeState> CompleteReports(TextDocumentItem document, string phase = "") {
    await client.WaitForNotificationCompletionAsync(document.Uri, CancellationToken);
    var manager = (await Projects.GetProjectManager(document.Uri))!;
    var resolved = await manager.GetStateAfterResolutionAsync();
    Assert.Equal(CompilationStatus.ResolutionSucceeded, resolved.Status);
    await manager.VerifyEverythingAsync(document.Uri.ToUri());
    IdeState? latest = null;
    using var subscription = manager.States.Subscribe(state => latest = state);
    try {
    return await manager.States.Where(state => state.Version == document.Version &&
      state.Status == CompilationStatus.ResolutionSucceeded &&
      state.CanVerifyStates.TryGetValue(document.Uri.ToUri(), out var symbols) && symbols.Count != 0 &&
      symbols.Values.All(symbol => symbol.PreparationProgress == VerificationPreparationState.Done &&
        symbol.VerificationTasks.Values.All(task => task.RawStatus is VerificationWorkCompleted)))
      .FirstAsync().ToTask(CancellationToken).WaitAsync(TimeSpan.FromSeconds(25));
    } catch (TimeoutException error) {
      throw new InvalidOperationException($"Incomplete {phase}: {latest?.Status}; " +
        string.Join(";", latest?.CanVerifyStates.Values.SelectMany(f => f.Values).Select(s =>
          $"{s.PreparationProgress}:{string.Join(',', s.VerificationTasks.Values.Select(t => t.RawStatus.GetType().Name))}") ?? []) +
        " diagnostics: " + string.Join(";", latest?.GetAllDiagnostics().Select(d => d.Diagnostic.Message) ?? []), error);
    }
  }

  private static string[] ReportFacts(IdeState state) => state.CanVerifyStates.Values.SelectMany(f => f.Values)
    .SelectMany(s => s.VerificationTasks.Values).Select(task => {
      var native = BoogieVerificationReporting.GetTask(task.Task);
      var result = BoogieVerificationReporting.GetResult(((VerificationWorkCompleted)task.RawStatus).Result);
      return $"{native.ScopeId}|{VerificationTaskFacts.SplitIndex(native)}|{result.Outcome}|{result.ResourceCount}|{result.Asserts.Count}";
    }).OrderBy(f => f, StringComparer.Ordinal).ToArray();

  private static string[] TreeFacts(IdeState state) {
    var facts = new List<string>();
    foreach (var tree in state.VerificationTrees.Values) {
      tree.Visit(node => {
        if (node is DocumentVerificationTree) { return; }
        // Assertion child identifiers include completion order, which can differ even between fresh runs.
        // Compare their source ranges and batch membership instead.
        var identity = node is AssertionVerificationTree a ? $"batch:{a.AssertionBatchNum}:{node.DisplayName.Split(" #")[0]}" : node.Identifier;
        facts.Add($"{node.Kind}|{identity}|{node.Range}|{node.StatusVerification}|{node.StatusCurrent}|{node.ResourceCount}");
        if (node is AssertionVerificationTree assertion) {
          facts.Add($"related:{identity}:{node.Range}|{string.Join(";", assertion.ImmediatelyRelatedRanges)}|{string.Join(";", assertion.DynamicallyRelatedRanges)}");
        }
        if (node is ImplementationVerificationTree implementation) {
          facts.AddRange(implementation.AssertionBatchMetrics.OrderBy(p => p.Key)
            .Select(p => $"batch:{node.Identifier}|{p.Key}|{p.Value.ResourceCount}"));
        }
      });
    }
    return facts.OrderBy(f => f, StringComparer.Ordinal).ToArray();
  }

  private async Task<string[]> Hovers(TextDocumentItem document, IdeState state) {
    var assertions = new List<AssertionVerificationTree>();
    state.VerificationTrees[document.Uri.ToUri()].Visit(node => {
      if (node is AssertionVerificationTree assertion) { assertions.Add(assertion); }
    });
    Assert.NotEmpty(assertions);
    var hovers = new List<string>();
    foreach (var position in assertions.SelectMany(a => a.ImmediatelyRelatedRanges.Select(r => r.Start).Prepend(a.Position)).Distinct()) {
      var hover = await client.RequestHover(new HoverParams { TextDocument = document.Uri, Position = position }, CancellationToken);
      Assert.NotNull(hover?.Contents.MarkupContent);
      hovers.Add($"{position}|{hover.Contents.MarkupContent.Value}");
    }
    return hovers.OrderBy(h => h, StringComparer.Ordinal).ToArray();
  }

  private async Task<string[]> ReseededFacts(TextDocumentItem document, IdeState state) {
    var manager = (await Projects.GetProjectManager(document.Uri))!;
    var symbol = state.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values)
      .First().Task.Source.CanVerify!;
    Assert.True(await manager.Compilation.VerifyCanVerify(symbol, _ => true, 17));
    var reseeded = await manager.States.Where(current => current.CanVerifyStates.Values.SelectMany(f => f.Values)
      .SelectMany(s => s.VerificationTasks.Values).Where(t => t.Task.Source.CanVerify == symbol).All(t =>
        t.RawStatus is VerificationWorkCompleted &&
        VerificationTaskFacts.HasSplit(BoogieVerificationReporting.GetTask(t.Task)) &&
        BoogieVerificationReporting.GetTask(t.Task).Split.RandomSeed == 17))
      .FirstAsync().ToTask(CancellationToken).WaitAsync(TimeSpan.FromSeconds(30));
    return ReportFacts(reseeded with {
      CanVerifyStates = reseeded.CanVerifyStates.ToImmutableDictionary(f => f.Key,
        f => f.Value.Where(s => s.Value.VerificationTasks.Values.Any(t => t.Task.Source.CanVerify == symbol)).ToImmutableDictionary())
    });
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task WholeModuleReportsPreserveGuttersHoversAndResources(int workers) {
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    var file = Path.Combine(directory, "input.dfy");
    // The open overlay, rather than this different disk content, is authoritative.
    await File.WriteAllTextAsync(file, Negative);
    var cache = Path.Combine(directory, "cache");
    string[]? expected = null, trees = null, hovers = null, seeded = null;
    foreach (var phase in new[] { "fresh", "cold", "warm", "audit" }) {
      await Start(phase == "fresh" ? "" : cache, workers, audit: phase == "audit", ideReports: phase != "fresh");
      var document = await CreateOpenAndWaitForResolve(ReportSource, file);
      var state = await CompleteReports(document, phase);
      expected ??= ReportFacts(state);
      trees ??= TreeFacts(state);
      hovers ??= await Hovers(document, state);
      Assert.Equal(expected, ReportFacts(state));
      Assert.Equal(trees, TreeFacts(state));
      Assert.Equal(hovers, await Hovers(document, state));
      Assert.Empty(state.Counterexamples);
      Assert.DoesNotContain(state.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values)
        .SelectMany(t => t.Diagnostics), d => d.Severity == DiagnosticSeverity.Error);
      if (phase == "fresh") { seeded = await ReseededFacts(document, state); }
      if (phase == "warm") {
        var persistent = PersistentModuleCache.For(((Program)state.ResolvedProgram!).Options)!;
        Assert.True(persistent.KeyHits > 0, persistent.LastBypassReason);
        Assert.Equal(0, Session(state).SolverStarts);
        Assert.All(state.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values), task => {
          var native = BoogieVerificationReporting.GetTask(task.Task);
          Assert.False(VerificationTaskFacts.HasSplit(native));
          Assert.NotNull(VerificationTaskFacts.IdeReport(native));
        });
        Assert.Equal(seeded, await ReseededFacts(document, state));
        // A changed overlay must not acquire a successful report for its old source.
        ApplyChange(ref document, new Range(0, 0, 5, 1), Negative);
        var failed = await CompleteReports(document);
        Assert.NotEmpty(failed.Counterexamples);
        Assert.Contains(failed.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values)
          .SelectMany(t => t.Diagnostics), d => d.Severity == DiagnosticSeverity.Error);
        Assert.All(failed.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values), task =>
          Assert.True(VerificationTaskFacts.HasSplit(BoogieVerificationReporting.GetTask(task.Task))));
        var failedOracle = ReportFacts(failed);
        Projects.Dispose();
        await client.Shutdown();
        await Start(cache, workers, ideReports: true);
        document = await CreateOpenAndWaitForResolve(Negative, file);
        var failedWarm = await CompleteReports(document, "failed warm");
        Assert.Equal(failedOracle, ReportFacts(failedWarm));
        Assert.NotEmpty(failedWarm.Counterexamples);
        Assert.All(failedWarm.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values), task =>
          Assert.True(VerificationTaskFacts.HasSplit(BoogieVerificationReporting.GetTask(task.Task))));

      }
      if (phase == "audit") {
        var persistent = PersistentModuleCache.For(((Program)state.ResolvedProgram!).Options)!;
        Assert.True(persistent.AuditChecks > 0);
        Assert.Equal(0, persistent.AuditMismatches);
        Assert.Equal(0, persistent.KeyHits + persistent.GraphHits);
        Assert.True(Session(state).SolverStarts > 0);
      }
      Projects.Dispose();
      await client.Shutdown();
    }
  }
  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task WholeModuleReportsRespectManualSelection(int workers) {
    var directory = GetFreshTempPath();
    var file = Path.Combine(directory, "input.dfy");
    var cache = Path.Combine(directory, "cache");
    await Start(cache, workers, verifyOn: VerifyOnMode.Never, ideReports: true);
    var document = await CreateOpenAndWaitForResolve(ReportSource, file);
    var cold = await CompleteReports(document);
    var expected = ReportFacts(cold);
    Assert.True(PersistentModuleCache.For(((Program)cold.ResolvedProgram!).Options)!.ManifestPublications > 0);
    Projects.Dispose();
    await client.Shutdown();

    await Start(cache, workers, verifyOn: VerifyOnMode.Never, ideReports: true);
    document = await CreateOpenAndWaitForResolve(ReportSource, file);
    var idle = await SchedulingState(document, state => state.CanVerifyStates.ContainsKey(document.Uri.ToUri()));
    Assert.All(idle.CanVerifyStates[document.Uri.ToUri()].Values, Unscheduled);
    Assert.True(await client.RunSymbolVerification(document, new Position(1, 31), CancellationToken));
    var selected = await SchedulingState(document, state => Finished(AtLine(state, document, 1)));
    Unscheduled(AtLine(selected, document, 4));
    Assert.True(PersistentModuleCache.For(((Program)selected.ResolvedProgram!).Options)!.KeyHits > 0);
    Assert.Equal(0, Session(selected).SolverStarts);
    await client.SaveDocumentAndWaitAsync(document, CancellationToken);
    var saved = await SchedulingState(document, state => Finished(AtLine(state, document, 1)));
    Unscheduled(AtLine(saved, document, 4));
    Assert.Equal(expected, ReportFacts(await CompleteReports(document)));
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task WholeModuleReportsAtGraphCutoffKeepCurrentEvidence(int workers) {
    var directory = GetFreshTempPath();
    var file = Path.Combine(directory, "input.dfy");
    var cache = Path.Combine(directory, "cache");
    await Start(cache, workers, ideReports: true);
    var document = await CreateOpenAndWaitForResolve(ReportSource, file);
    var cold = await CompleteReports(document);
    var expected = ReportFacts(cold);
    var trees = TreeFacts(cold);
    var hovers = await Hovers(document, cold);
    Assert.True(PersistentModuleCache.For(((Program)cold.ResolvedProgram!).Options)!.ManifestPublications > 0);
    Projects.Dispose();
    await client.Shutdown();
    var translations = Path.Combine(cache, "v1", "artifacts", "translations");
    var records = Directory.GetFiles(translations, "*", SearchOption.AllDirectories);
    Assert.NotEmpty(records);
    foreach (var record in records) { File.Delete(record); }
    await Start(cache, workers, ideReports: true);
    document = await CreateOpenAndWaitForResolve(ReportSource, file);
    var warm = await CompleteReports(document, "graph cutoff");
    Assert.Equal(expected, ReportFacts(warm));
    Assert.Equal(trees, TreeFacts(warm));
    Assert.Equal(hovers, await Hovers(document, warm));
    var persistent = PersistentModuleCache.For(((Program)warm.ResolvedProgram!).Options)!;
    Assert.Equal(0, persistent.KeyHits);
    Assert.True(persistent.GraphHits > 0, persistent.LastBypassReason);
    Assert.Equal(0, Session(warm).SolverStarts);
  }

}
