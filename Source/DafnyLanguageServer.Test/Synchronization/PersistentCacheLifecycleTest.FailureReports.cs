// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dafny.Caching;
using Microsoft.Dafny.LanguageServer.Handlers.Custom;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

public partial class PersistentCacheLifecycleTest {
  private const string FailureReportSource = """
    module Cached {
      method Good(x: int) ensures x + 1 > x {}
      method Bad(x: int) returns (y: int)
        ensures y > x
      { y := x; }
      method Need(x: int) requires x > 0 {}
      method Call(x: int) { Need(x - 1); }
      lemma {:isolate_assertions} Divide(x: int) { assert 1 / x > 0; }
      lemma Equal(x: int) { assert x == 0; }
      lemma Iff(b: bool, c: bool) { assert b <==> c; }
    }
    """;

  private async Task<string[]> ModelFacts(TextDocumentItem document) {
    var models = await client.SendRequest(new CounterExampleParams { TextDocument = document.Uri }, CancellationToken);
    return models.Select(model => $"{model.Position}|{model.Assumption}").OrderBy(f => f, StringComparer.Ordinal).ToArray();
  }

  private async Task<string[]> ActionFacts(TextDocumentItem document, IdeState state) {
    var facts = new List<string>();
    var positions = state.GetAllDiagnostics().Select(d => d.Diagnostic.Range.Start).Distinct();
    foreach (var position in positions) {
      var actions = await client.RequestCodeAction(new CodeActionParams {
        TextDocument = document.Uri, Range = new Range(position, position)
      }, CancellationToken);
      foreach (var item in actions) {
        if (item.CodeAction is not { } action) { continue; }
        var resolved = await client.ResolveCodeAction(action, CancellationToken);
        facts.Add($"{position}|{action.Title}|{JsonConvert.SerializeObject(resolved.Edit)}");
      }
    }
    return facts.OrderBy(f => f, StringComparer.Ordinal).ToArray();
  }

  private static string[] FailureDiagnostics(IdeState state) => state.GetAllDiagnostics().Select(d =>
    JsonConvert.SerializeObject(d.Diagnostic)).OrderBy(d => d, StringComparer.Ordinal).ToArray();

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task WholeModuleFailureReportsPreserveModelsDiagnosticsAndActions(int workers) {
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    var file = Path.Combine(directory, "input.dfy");
    await File.WriteAllTextAsync(file, "lemma Disk() { assert false; }");
    var cache = Path.Combine(directory, "cache");
    string[]? facts = null, trees = null, hovers = null, models = null, diagnostics = null, actions = null;
    foreach (var phase in new[] { "fresh", "cold", "warm", "audit", "graph", "native" }) {
      if (phase == "graph") {
        var records = Directory.GetFiles(Path.Combine(cache, "v1", "artifacts", "translations"), "*", SearchOption.AllDirectories);
        Assert.NotEmpty(records);
        foreach (var record in records) { File.Delete(record); }
      }
      await Start(phase == "fresh" ? "" : cache, workers, audit: phase == "audit", ideReports: phase != "fresh" && phase != "native", errorLimit: 1);
      var document = await CreateOpenAndWaitForResolve(FailureReportSource, file);
      var state = await CompleteReports(document, phase);
      if (phase is "warm" or "graph") {
        // The handler is declared parallel. Exercise overlapping calls even when a transport
        // happens to dispatch immediately completed requests on one thread.
        var handler = ActivatorUtilities.CreateInstance<DafnyCounterExampleHandler>(Server.Services);
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(async () => {
          var reply = await handler.Handle(new CounterExampleParams { TextDocument = document.Uri }, CancellationToken);
          var actual = reply.Select(model => $"{model.Position}|{model.Assumption}")
            .OrderBy(f => f, StringComparer.Ordinal).ToArray();
          Assert.Equal(models!, actual);
        }, CancellationToken)));
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => ModelFacts(document)));
        Assert.All(concurrent, actual => Assert.Equal(models!, actual));
      }
      facts ??= ReportFacts(state);
      trees ??= TreeFacts(state);
      hovers ??= await Hovers(document, state);
      models ??= await ModelFacts(document);
      actions ??= await ActionFacts(document, state);
      diagnostics ??= FailureDiagnostics(state);
      Assert.NotEmpty(models);
      Assert.Contains(actions, action => action.Contains("Insert explicit failing assertion"));
      Assert.Contains(actions, action => action.Contains("Insert a calc statement"));
      Assert.Contains(actions, action => action.Contains("Assert postcondition at return location where it fails"));
      Assert.Equal(facts, ReportFacts(state));
      Assert.Equal(trees, TreeFacts(state));
      var actualHovers = await Hovers(document, state);
      Assert.True(hovers.SequenceEqual(actualHovers), phase + " expected hovers:\n" + string.Join("\n", hovers) + "\nactual:\n" + string.Join("\n", actualHovers));
      Assert.Equal(models, await ModelFacts(document));
      Assert.Equal(models, await ModelFacts(document)); // Repeated model requests must be stable.
      Assert.Equal(actions, await ActionFacts(document, state));
      Assert.Equal(diagnostics, FailureDiagnostics(state));
      var tasks = state.CanVerifyStates.Values.SelectMany(f => f.Values).SelectMany(s => s.VerificationTasks.Values).ToList();
      Assert.Contains(tasks, task => task.Status == PublishedVerificationStatus.Correct);
      Assert.Contains(tasks, task => task.Status == PublishedVerificationStatus.Error);
      if (phase is "warm" or "graph") {
        var persistent = PersistentModuleCache.For(((Program)state.ResolvedProgram!).Options)!;
        Assert.True(phase == "warm" ? persistent.KeyHits > 0 : persistent.GraphHits > 0, persistent.LastBypassReason);
        Assert.Equal(0, Session(state).SolverStarts);
        Assert.All(tasks, task => Assert.False(VerificationTaskFacts.HasSplit(BoogieVerificationReporting.GetTask(task.Task))));
        Assert.Contains(tasks, task => task.HitErrorLimit);
      }
      if (phase == "audit") {
        var persistent = PersistentModuleCache.For(((Program)state.ResolvedProgram!).Options)!;
        Assert.True(persistent.AuditChecks > 0);
        Assert.Equal(0, persistent.AuditMismatches);
        Assert.True(Session(state).SolverStarts > 0);
      }
      if (phase == "native") {
        Assert.All(tasks, task => Assert.True(VerificationTaskFacts.HasSplit(BoogieVerificationReporting.GetTask(task.Task))));
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
  public async Task WholeModuleQuantifiedAndExhaustedReportsRemainNativeEquivalent(int workers, bool exhausted) {
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    var file = Path.Combine(directory, "input.dfy");
    var cache = Path.Combine(directory, "cache");
    var source = exhausted ? """
      module Cached {
        function Fib(n: nat): nat { if n <= 1 then n else Fib(n - 1) + Fib(n - 2) }
        lemma {:resource_limit 1} Hard() { assert Fib(10) == 1; }
      }
      """ :
      "module Cached { lemma Quantified(s: seq<int>) { assert forall i :: 0 <= i < |s| ==> s[i] == 0; } }";
    string[]? facts = null, trees = null, hovers = null, actions = null, diagnostics = null;
    foreach (var phase in new[] { "fresh", "cold", "warm", "audit" }) {
      var profile = Path.Combine(directory, phase + ".jsonl");
      // Resolution, translation and result/report reuse must compose across server restarts.
      await Start(phase == "fresh" ? "" : cache, workers, audit: phase == "audit", ideReports: phase != "fresh",
        profile: profile);
      var document = await CreateOpenAndWaitForResolve(source, file);
      var state = await CompleteReports(document, phase);
      facts ??= ReportFacts(state);
      trees ??= TreeFacts(state);
      hovers ??= await Hovers(document, state);
      actions ??= await ActionFacts(document, state);
      diagnostics ??= FailureDiagnostics(state);
      Assert.Equal(facts, ReportFacts(state));
      Assert.Equal(trees, TreeFacts(state));
      Assert.Equal(hovers, await Hovers(document, state));
      Assert.Equal(actions, await ActionFacts(document, state));
      Assert.Equal(diagnostics, FailureDiagnostics(state));
      Assert.Contains(facts, fact => fact.Contains(exhausted ? "OutOfResource" : "Invalid"));
      if (!exhausted) { Assert.Contains(actions, action => action.Contains("Insert a forall statement")); }
      if (phase == "warm") {
        AssertCheckedResolutionHits(profile);
        var persistent = PersistentModuleCache.For(((Program)state.ResolvedProgram!).Options)!;
        Assert.True(persistent.KeyHits > 0, persistent.LastBypassReason);
        Assert.Equal(0, Session(state).SolverStarts);
      }
      if (phase == "audit") {
        AssertCheckedResolutionHits(profile);
        var persistent = PersistentModuleCache.For(((Program)state.ResolvedProgram!).Options)!;
        Assert.True(persistent.AuditChecks > 0);
        Assert.Equal(0, persistent.AuditMismatches);
        Assert.True(Session(state).SolverStarts > 0);
      }
      Projects.Dispose();
      await client.Shutdown();
    }
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task WholeModuleModelsExcludeStaleUnselectedFiles(int workers) {
    var directory = GetFreshTempPath();
    Directory.CreateDirectory(directory);
    var firstFile = Path.Combine(directory, "first.dfy");
    var secondFile = Path.Combine(directory, "second.dfy");
    const string firstSource = "lemma First(mainValue: int) { assert mainValue == 0; }";
    const string secondSource = "lemma Second(otherValue: int) { assert otherValue == 1; }";
    await File.WriteAllTextAsync(firstFile, firstSource);
    await File.WriteAllTextAsync(secondFile, secondSource);
    await File.WriteAllTextAsync(Path.Combine(directory, DafnyProject.FileName), "includes = [\"*.dfy\"]\n");
    var cache = Path.Combine(directory, "cache");
    string[]? expected = null;
    foreach (var phase in new[] { "fresh", "cold", "warm" }) {
      await Start(phase == "fresh" ? "" : cache, workers, verifyOn: VerifyOnMode.Never, ideReports: phase != "fresh");
      var first = await CreateOpenAndWaitForResolve(firstSource, firstFile);
      var second = await CreateOpenAndWaitForResolve(secondSource, secondFile);
      Assert.Same(await Projects.GetProjectManager(first.Uri), await Projects.GetProjectManager(second.Uri));
      if (phase == "fresh") {
        expected = await ModelFacts(first);
        Assert.NotEmpty(expected);
      } else {
        await ModelFacts(first);
        await ModelFacts(second);
        var complete = await SchedulingState(first, state => Finished(AtLine(state, first, 0)) && Finished(AtLine(state, second, 0)));
        if (phase == "warm") {
          Assert.True(PersistentModuleCache.For(((Program)complete.ResolvedProgram!).Options)!.KeyHits > 0);
          Assert.Equal(0, Session(complete).SolverStarts);
          Assert.All(AtLine(complete, second, 0).VerificationTasks.Values, task => {
            var native = BoogieVerificationReporting.GetTask(task.Task);
            Assert.False(VerificationTaskFacts.HasSplit(native));
            Assert.NotEmpty(VerificationTaskFacts.IdeReport(native)!.Models!);
          });
          ApplyChange(ref first, new Range(0, firstSource.Length, 0, firstSource.Length), " ");
          Assert.Equal(expected, await ModelFacts(first));
          var changed = await SchedulingState(first, state => Finished(AtLine(state, first, 0)));
          // Only the requested file was reverified. Old completed reports remain for the other
          // file, but its stale models must not join the current generation's model response.
          Assert.All(AtLine(changed, second, 0).VerificationTasks.Values, task => {
            Assert.Equal(PublishedVerificationStatus.Stale, task.Status);
            Assert.IsType<VerificationWorkCompleted>(task.RawStatus);
            Assert.NotEmpty(VerificationTaskFacts.IdeReport(BoogieVerificationReporting.GetTask(task.Task))!.Models!);
          });
        }
      }
      Projects.Dispose();
      await client.Shutdown();
    }
  }

}
