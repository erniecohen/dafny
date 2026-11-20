using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;
using DafnyDriver.Commands;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests;

[CollectionDefinition("Verification batch", DisableParallelization = true)]
public class VerificationBatchCollection { }

[Collection("Verification batch")]
public class VerificationBatchTests {
  [Theory]
  [InlineData(VerificationOutcome.Verified, ExitValue.SUCCESS, "1 verified, 0 errors", false)]
  [InlineData(VerificationOutcome.Failed, ExitValue.VERIFICATION_ERROR, "0 verified, 1 error", false)]
  [InlineData(VerificationOutcome.Inconclusive, ExitValue.VERIFICATION_ERROR, "1 inconclusive", false)]
  [InlineData(VerificationOutcome.Timeout, ExitValue.VERIFICATION_ERROR, "1 time out", false)]
  [InlineData(VerificationOutcome.ResourceExhaustion, ExitValue.VERIFICATION_ERROR, "1 out of resource", false)]
  [InlineData(VerificationOutcome.OutOfMemory, ExitValue.VERIFICATION_ERROR, "1 out of memory", false)]
  [InlineData(VerificationOutcome.Cancelled, ExitValue.VERIFICATION_ERROR, "1 solver exceptions", false)]
  [InlineData(VerificationOutcome.Unsupported, ExitValue.VERIFICATION_ERROR, "1 solver exceptions", false)]
  [InlineData(VerificationOutcome.ToolError, ExitValue.VERIFICATION_ERROR, "1 solver exceptions", false)]
  [InlineData(VerificationOutcome.Bounded, ExitValue.VERIFICATION_ERROR, "1 solver exceptions", false)]
  [InlineData(VerificationOutcome.Bounded, ExitValue.SUCCESS, "1 verified, 0 errors", true)]
  public async Task NeutralCliSummaryAndDiagnosticsNeedNoNativeEvidence(
    VerificationOutcome outcome, ExitValue exit, string summaryText, bool legacyPolicy) {
    var directory = Path.Combine(Path.GetTempPath(), "neutral-reporting-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "input.dfy");
    await File.WriteAllTextAsync(source, "lemma Good() { assert true; }\n");
    try {
      var output = new StringWriter();
      var options = OptionsWithWriter(output, true, source);
      options.Set(VerifyCommand.PerformanceStatisticsOption, 1);
      var cli = CliCompilation.Create(options, new NeutralResultVerifier(outcome, payload: legacyPolicy ? new LegacyDiagnosticPolicy() : null));
      using var compilation = cli.Compilation;
      var runs = new List<VerificationRun>();
      using var events = compilation.Updates.Subscribe(update => {
        if (update is VerificationRunStarted started) { runs.Add(started.Run); }
      });
      cli.Start();
      using var results = new Subject<CanVerifyResult>();
      VerifyCommand.ReportVerificationDiagnostics(cli, results);
      var summary = VerifyCommand.ReportVerificationSummary(cli, results);
      await foreach (var result in cli.VerifyAllLazily()) {
        var part = Assert.Single(result.WorkResults);
        Assert.Equal(legacyPolicy, part.Result.Payload != null);
        Assert.Null(part.Result.Metrics);
        Assert.Equal(outcome, part.Result.Outcome);
        Assert.Throws<InvalidOperationException>(() => result.Results);
        results.OnNext(result);
      }
      results.OnCompleted();
      await summary;
      Assert.Equal(exit, await cli.GetAndReportExitValue());
      Assert.Contains(summaryText, output.ToString());
      Assert.Contains("Proof resource counts are unavailable", output.ToString());
      Assert.DoesNotContain("Total resources used is 0", output.ToString());
      var run = Assert.Single(runs);
      await run.AuditCompletion.WaitAsync(TimeSpan.FromSeconds(5));
      Assert.True(run.IsClosed);
      Assert.Equal(outcome == VerificationOutcome.Verified, run.IsVerified);
    } finally {
      Directory.Delete(directory, true);
    }
  }

  [Theory]
  [InlineData(false, VerificationOutcome.Failed)]
  [InlineData(true, VerificationOutcome.Failed)]
  [InlineData(false, VerificationOutcome.Bounded)]
  [InlineData(true, VerificationOutcome.Bounded)]
  public async Task NeutralCliDistinguishesMissingAndAvailableDetails(bool available, VerificationOutcome outcome) {
    var directory = Path.Combine(Path.GetTempPath(), "neutral-details-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "input.dfy");
    await File.WriteAllTextAsync(source, "lemma Good() { assert true; }\n");
    try {
      var output = new StringWriter();
      var options = OptionsWithWriter(output, true, source);
      options.Set(VerifyCommand.PerformanceStatisticsOption, 1);
      var cli = CliCompilation.Create(options, new NeutralResultVerifier(outcome,
        available ? new VerificationMetrics(654) : null, available ? new AssertionCounts() : null));
      using var compilation = cli.Compilation;
      cli.Start();
      using var results = new Subject<CanVerifyResult>();
      VerifyCommand.ReportVerificationDiagnostics(cli, results);
      var summary = VerifyCommand.ReportVerificationSummary(cli, results);
      await foreach (var result in cli.VerifyAllLazily(filterPosition: ":1")) { results.OnNext(result); }
      results.OnCompleted();
      await summary;
      Assert.Equal(ExitValue.VERIFICATION_ERROR, await cli.GetAndReportExitValue());
      if (available) {
        if (outcome == VerificationOutcome.Failed) {
          Assert.Contains("2 assertions verified, 1 error", output.ToString());
        } else {
          Assert.Contains("0 assertions verified, 0 errors, 1 solver exceptions", output.ToString());
        }
        Assert.Contains("Total resources used is 654", output.ToString());
        Assert.Contains("Max resources used by VC is 654", output.ToString());
        Assert.DoesNotContain("unavailable", output.ToString());
      } else {
        Assert.Contains("assertion counts unavailable", output.ToString());
        Assert.Contains("Proof resource counts are unavailable", output.ToString());
        Assert.DoesNotContain("0 assertions verified", output.ToString());
      }
    } finally {
      Directory.Delete(directory, true);
    }
  }

  private sealed class AssertionCounts : IVerificationPayload, IVerificationAssertionStatistics {
    public int AssertionCount => 3;
    public int FailedAssertionCount => 1;
  }

  private sealed class LegacyDiagnosticPolicy : IVerificationPayload, IVerificationDiagnosticReporting {
    public bool UsesLegacyDiagnosticPolicy => true;
    public void ReportDiagnostics(IVerificationWork work, DafnyOptions options, string name, ErrorReporter reporter) { }
  }

  private sealed class NeutralResultVerifier(VerificationOutcome outcome, VerificationMetrics? metrics = null,
    IVerificationPayload? payload = null) : IProgramVerifier {
    public Task<ModuleVerificationPreparation> GetVerificationTasksAsync(VerificationPreparationRequest request,
      ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
      var owner = resolution.CanVerifies!.Values.SelectMany(tree => tree.Values).Single(c => c.ContainingModule == moduleDefinition);
      var work = new NeutralWork(new(new(owner.FullDafnyName, 0), owner.Origin, owner.Origin) { CanVerify = owner }, new(outcome, Metrics: metrics, Payload: payload));
      return Task.FromResult(new ModuleVerificationPreparation(VerificationPreparation.Ready([work]), new()));
    }

    private sealed class NeutralWork(VerificationSource source, VerificationWorkResult result) : IVerificationWork {
      public VerificationSource Source => source;
      public VerificationWorkStatus CacheStatus => new VerificationWorkStale();
      public bool IsIdle => true;
      public IVerificationWork FromSeed(int seed) => this;
      public IObservable<VerificationWorkStatus>? TryRun() => Observable.Return<VerificationWorkStatus>(
        new VerificationWorkCompleted(result));
      public void Cancel() { }
    }
  }

  [Fact]
  public async Task LateErrorsBelongToTheirOriginalRequest() {
    var directory = Path.Combine(Path.GetTempPath(), "verification-request-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "input.dfy");
    await File.WriteAllTextAsync(source, "lemma Good() { assert true; }\n");
    try {
      var verifier = new RetainedResultVerifier();
      var cli = CliCompilation.Create(Options(true, source), verifier);
      using var compilation = cli.Compilation;
      var runs = new List<VerificationRun>();
      using var events = compilation.Updates.Subscribe(update => {
        if (update is VerificationRunStarted started) { runs.Add(started.Run); }
      });
      cli.Start();
      Assert.NotEmpty(await Request(cli, "input.dfy"));
      var first = Assert.Single(runs);
      Assert.True(first.IsComplete);
      Assert.False(first.IsVerified);
      Assert.False(first.IsClosed);
      Assert.False(first.AuditCompletion.IsCompleted);
      Assert.NotEmpty(await Request(cli, "input.dfy"));
      Assert.Equal(2, runs.Count);
      Assert.Same(verifier.LateError, Assert.Single(runs[0].Failures).Value.Exception);
      Assert.False(runs[0].IsVerified);
      Assert.False(runs[1].IsVerified);
      Assert.False(runs[1].IsClosed);
      Assert.True(runs[0].AuditCompletion.IsCompletedSuccessfully);
    } finally {
      Directory.Delete(directory, true);
    }
  }

  // Retain native publication but leave its stream open so an older request can fail
  // while a new request for the same source identities is starting.
  private sealed class RetainedResultVerifier : IProgramVerifier {
    private readonly DafnyProgramVerifier inner = new(NullLogger<DafnyProgramVerifier>.Instance);
    private Subject<VerificationWorkStatus>? previous;
    public Exception LateError { get; } = new InvalidOperationException("late worker error");
    public async Task<ModuleVerificationPreparation> GetVerificationTasksAsync(VerificationPreparationRequest request,
      ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
      var result = await inner.GetVerificationTasksAsync(request, resolution, moduleDefinition, cancellationToken);
      return result with { Preparation = VerificationPreparation.Ready(result.Preparation.GetWorkOrThrow().Select(w => new RetainedWork(w, this))) };
    }

    private sealed class RetainedWork(IVerificationWork inner, RetainedResultVerifier owner) : IVerificationWork {
      public VerificationSource Source => inner.Source;
      public IVerificationPayload? Payload => inner.Payload;
      public IReadOnlyList<Microsoft.Dafny.Function> HiddenFunctions => inner.HiddenFunctions;
      public VerificationWorkStatus CacheStatus => inner.CacheStatus;
      public bool IsIdle => inner.IsIdle;
      public IVerificationWork FromSeed(int seed) => new RetainedWork(inner.FromSeed(seed), owner);
      public IObservable<VerificationWorkStatus>? TryRun() => TryRunForScheduling();
      public IObservable<VerificationWorkStatus>? TryRunForScheduling() {
        owner.previous?.OnError(owner.LateError);
        var subject = new Subject<VerificationWorkStatus>();
        owner.previous = subject;
        return Observable.Create<VerificationWorkStatus>(observer => {
          var subscription = subject.Subscribe(observer);
          inner.TryRunForScheduling()!.Subscribe(subject.OnNext, subject.OnError);
          return subscription;
        });
      }
      public void Cancel() => inner.Cancel();
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OneResolutionFreshRequestsAndNextInvocationReloadsSources(bool refresh) {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-verification-batch-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var first = Path.Combine(directory, "first.dfy");
    var second = Path.Combine(directory, "second.dfy");
    await File.WriteAllTextAsync(first, "lemma Good(x: int) {\n  assert x + 1 > x;\n  assert x + 2 > x;\n}\n");
    await File.WriteAllTextAsync(second, "lemma Bad() { assert false; }\n");
    try {
      var options = Options(refresh, first, second);
      var cli = CliCompilation.Create(options);
      using var compilation = cli.Compilation;
      var parsing = 0;
      var resolving = 0;
      var runs = new List<VerificationRun>();
      using var events = compilation.Updates.Subscribe(ev => {
        if (ev is FinishedParsing) { parsing++; }
        if (ev is FinishedResolution) { resolving++; }
        if (ev is VerificationRunStarted started) { runs.Add(started.Run); }
      });
      cli.Start();
      var resolution = await cli.Resolution;
      Assert.NotNull(resolution);
      Assert.False(resolution!.HasErrors);
      // A batch has one resolved input version, even when the disk changes later.
      await File.WriteAllTextAsync(first, "lemma Good() { assert false; }\n");
      var original = await Request(cli, "first.dfy");
      var duplicate = await Request(cli, "first.dfy");
      var overlap = await Request(cli, "first.dfy:2");
      var negative = await Request(cli, "second.dfy");
      var afterNegative = await Request(cli, "first.dfy");
      Assert.Equal(5, runs.Count);
      foreach (var run in runs) {
        await run.AuditCompletion.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(run.IsClosed);
        Assert.All(run.Closures.Values, kind => Assert.Equal(VerificationCompletionKind.StreamClosed, kind));
      }
      Assert.All(runs, run => {
        Assert.True(run.IsComplete);
        Assert.Equal(run.ExpectedCount, run.Results.Count);
        Assert.Empty(run.Failures);
        Assert.NotNull(run.Manifest);
        Assert.All(run.Results.Keys, id => Assert.Contains(id, run.Manifest!.Expected.Keys));
      });
      Assert.True(runs[0].IsVerified);
      Assert.False(runs[3].IsVerified);
      Assert.Contains(runs[3].Results.Values, r => r.Outcome == VerificationOutcome.Failed);
      Assert.NotSame(runs[0], runs[1]);
      Assert.Equal(runs[0].Results.Keys, runs[1].Results.Keys);
      Assert.True(runs[2].ExpectedCount < runs[0].ExpectedCount);
      var state = new CliCanVerifyState { Run = runs[0] };
      foreach (var task in runs[0].Work) {
        state.RecordCompletion(task, new(runs[0].Results[task.Source.Id]));
      }
      Assert.Equal(state.TaskCount, state.CompletedCount);
      var repeated = runs[0].Work[0];
      Assert.Throws<InvalidOperationException>(() => state.RecordCompletion(repeated, new(runs[0].Results[repeated.Source.Id])));
      Assert.Throws<InvalidOperationException>(() => new CliCanVerifyState { Run = runs[1] }
        .RecordCompletion(repeated, new(runs[0].Results[repeated.Source.Id])));
      foreach (var result in new[] { original, duplicate, overlap, afterNegative }) {
        Assert.NotEmpty(result);
        Assert.All(result, part => Assert.Equal(SolverOutcome.Valid, part.Result.Outcome));
        Assert.All(result, part => Assert.True(part.Result.ResourceCount > 0));
      }
      Assert.True(original.Count >= 2);
      Assert.True(overlap.Count < original.Count);
      Assert.Equal(original.Select(part => part.Result.ResourceCount), duplicate.Select(part => part.Result.ResourceCount));
      Assert.Equal(original.Select(part => part.Result.ResourceCount), afterNegative.Select(part => part.Result.ResourceCount));
      Assert.Contains(negative, part => part.Result.Outcome == SolverOutcome.Invalid);
      Assert.NotSame(original.First().Task, duplicate.First().Task);
      Assert.NotSame(original.First().Task, overlap.First().Task);
      Assert.NotSame(original.First().Task, afterNegative.First().Task);
      Assert.Equal(1, parsing);
      Assert.Equal(1, resolving);
      Assert.Same(resolution, await cli.Resolution);

      var next = CliCompilation.Create(Options(refresh, first, second));
      using var nextCompilation = next.Compilation;
      next.Start();
      Assert.Contains(await Request(next, "first.dfy"), part => part.Result.Outcome == SolverOutcome.Invalid);
      Assert.NotSame(resolution, await next.Resolution);
    } finally {
      Directory.Delete(directory, true);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task DependenciesRemainLocalToEachRequestAndEarlierResultsStayUsable(bool refresh) {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-batch-dependencies-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "input.dfy");
    await File.WriteAllTextAsync(source, "lemma Needed(x: int) requires x > 0 { assert x >= 1; }\n");
    try {
      var options = Options(refresh, source);
      options.Set(CommonOptionBag.WarnContradictoryAssumptions, true);
      options.Set(CommonOptionBag.WarnRedundantAssumptions, true);
      options.TrackVerificationCoverage = true;
      var cli = CliCompilation.Create(options);
      using var compilation = cli.Compilation;
      var diagnostics = new ConcurrentQueue<DafnyDiagnostic>();
      using var events = compilation.Updates.Subscribe(ev => {
        if (ev is NewDiagnostic diagnostic) { diagnostics.Enqueue(diagnostic.Diagnostic); }
      });
      cli.Start();
      var resolution = (await cli.Resolution)!;
      var requests = new List<CanVerifyResult>();
      foreach (var filter in new[] { "input.dfy", "input.dfy:1", "input.dfy" }) {
        await foreach (var result in cli.VerifyAllLazily(filterPosition: filter, repeatVerification: true)) {
          requests.Add(result);
        }
      }
      Assert.Equal(3, requests.Count);
      var first = requests[0];
      var scope = first.Results.First().Task.ScopeId;
      var originalDependencies = first.ProofDependencies!.GetPotentialDependenciesForDefinition(scope).ToList();
      Assert.NotEmpty(originalDependencies);
      foreach (var request in requests) {
        var manager = request.ProofDependencies!;
        Assert.NotEmpty(request.Results);
        Assert.All(request.Results, part => Assert.Equal(SolverOutcome.Valid, part.Result.Outcome));
        var dependencies = manager.GetPotentialDependenciesForDefinition(scope).ToList();
        Assert.Equal(originalDependencies.Select(Describe).Order(), dependencies.Select(Describe).Order());
        // Deliberately inspect the old results after all later translations.
        var used = request.Results.SelectMany(part => part.Result.CoveredElements)
          .Select(manager.GetFullIdDependency).ToHashSet();
        Assert.NotEmpty(used);
        Assert.All(used, dependency => Assert.Contains(dependency, dependencies));
        ProofDependencyWarnings.ReportSuspiciousDependencies(options, request.Results,
          resolution.ResolvedProgram.Reporter, manager);
      }
      Assert.NotSame(first.ProofDependencies, requests[1].ProofDependencies);
      Assert.NotSame(first.ProofDependencies, requests[2].ProofDependencies);
      Assert.NotSame(first.Results[0].Task, requests[1].Results[0].Task);
      Assert.NotSame(first.Results[0].Task, requests[2].Results[0].Task);
      var exit = await cli.GetAndReportExitValue();
      Assert.True(diagnostics.All(diagnostic => diagnostic.Level is not (ErrorLevel.Warning or ErrorLevel.Error)),
        string.Join("\n", diagnostics.Select(diagnostic => diagnostic.ErrorId + ": " + diagnostic.Message)));
      Assert.Equal(ExitValue.SUCCESS, exit);
    } finally {
      Directory.Delete(directory, true);
    }

    static string Describe(DafnyCore.Verifier.ProofDependency dependency) =>
      dependency.RangeString() + ":" + dependency.Description;
  }

  [Fact]
  public async Task DeprecatedUnicodeOptionStillFailsStrictWarningHandling() {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-batch-warning-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "input.dfy");
    await File.WriteAllTextAsync(source, "lemma Good(x: int) { assert x + 1 > x; }\n");
    try {
      var options = Options(false, source);
      options.Set(CommonOptionBag.UnicodeCharacters, false);
      Assert.False(options.Get(CommonOptionBag.AllowWarnings));
      var cli = CliCompilation.Create(options);
      using var compilation = cli.Compilation;
      var diagnostics = new ConcurrentQueue<DafnyDiagnostic>();
      using var events = compilation.Updates.Subscribe(ev => {
        if (ev is NewDiagnostic diagnostic) { diagnostics.Enqueue(diagnostic.Diagnostic); }
      });
      cli.Start();
      var results = await Request(cli, "input.dfy");
      Assert.NotEmpty(results);
      Assert.All(results, result => Assert.Equal(SolverOutcome.Valid, result.Result.Outcome));
      Assert.All(results, result => Assert.True(result.Result.ResourceCount > 0));
      var exit = await cli.GetAndReportExitValue();
      Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Level == ErrorLevel.Error);
      var warning = Assert.Single(diagnostics.Where(diagnostic => diagnostic.Level == ErrorLevel.Warning));
      Assert.Equal("unicodeCharDeprecated", warning.ErrorId);
      Assert.Equal(ExitValue.DAFNY_ERROR, exit);
    } finally {
      Directory.Delete(directory, true);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task PreludeIsCapturedBeforeFirstRequestAndRetainedAfterEdits(bool refresh) {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-batch-prelude-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "input.dfy");
    var prelude = Path.Combine(directory, "prelude.bpl");
    await File.WriteAllTextAsync(source, "lemma Good(x: int) { assert x + 1 > x; }\n");
    var text = await File.ReadAllTextAsync(Path.Combine(
      Path.GetDirectoryName(typeof(BoogieGenerator).Assembly.Location)!, "DafnyPrelude.bpl"));
    await File.WriteAllTextAsync(prelude, text);
    try {
      var options = Options(refresh, source);
      options.DafnyPrelude = prelude;
      var cli = CliCompilation.Create(options);
      using var compilation = cli.Compilation;
      cli.Start();
      var first = await Request(cli, "input.dfy");
      await File.WriteAllTextAsync(prelude, "This is no longer a valid prelude.");
      var second = await Request(cli, "input.dfy");
      Assert.NotEmpty(first);
      Assert.NotEmpty(second);
      Assert.All(second, result => Assert.Equal(SolverOutcome.Valid, result.Result.Outcome));
      Assert.Equal(first.Select(part => part.Result.ResourceCount), second.Select(part => part.Result.ResourceCount));
      Assert.NotSame(first[0].Task, second[0].Task);
      Assert.Equal(text, (await cli.Resolution)!.ResolvedProgram.PreludeText);
      File.Delete(prelude);
      var afterDeletion = await Request(cli, "input.dfy");
      Assert.NotEmpty(afterDeletion);
      Assert.Equal(first.Count, afterDeletion.Count);
      Assert.All(afterDeletion, result => Assert.Equal(SolverOutcome.Valid, result.Result.Outcome));
      Assert.Equal(first.Select(part => part.Result.ResourceCount), afterDeletion.Select(part => part.Result.ResourceCount));
      for (var index = 0; index < afterDeletion.Count; index++) {
        Assert.NotSame(first[index].Task, afterDeletion[index].Task);
        Assert.NotSame(second[index].Task, afterDeletion[index].Task);
      }
      var next = CliCompilation.Create(options);
      using var nextCompilation = next.Compilation;
      next.Start();
      await Assert.ThrowsAsync<FileNotFoundException>(() => Request(next, "input.dfy"));
    } finally {
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public async Task CancellationDuringPreparationCompletesWithoutIdentifiedTasks() {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-batch-cancel-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "input.dfy");
    await File.WriteAllTextAsync(source, "lemma Good() { assert true; }\n");
    try {
      var verifier = new CancelledPreparationVerifier();
      var cli = CliCompilation.Create(Options(false, source), verifier);
      using var compilation = cli.Compilation;
      var identified = 0;
      using var events = compilation.Updates.Subscribe(ev => {
        if (ev is CanVerifyPartsIdentified) { identified++; }
      });
      cli.Start();
      var pending = Request(cli, "input.dfy");
      await verifier.Preparing.Task.WaitAsync(TimeSpan.FromSeconds(30));
      compilation.CancelPendingUpdates();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => pending.WaitAsync(TimeSpan.FromSeconds(30)));
      Assert.Equal(0, identified);
    } finally {
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public async Task CancellationBetweenRequestsDoesNotYieldAnotherSuccess() {
    var directory = Path.Combine(Path.GetTempPath(), "dafny-batch-cancel-between-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "input.dfy");
    await File.WriteAllTextAsync(source, "lemma Good(x: int) { assert x + 1 > x; }\n");
    try {
      var cli = CliCompilation.Create(Options(false, source));
      using var compilation = cli.Compilation;
      cli.Start();
      var first = await Request(cli, "input.dfy");
      Assert.NotEmpty(first);
      Assert.All(first, result => Assert.Equal(SolverOutcome.Valid, result.Result.Outcome));
      compilation.CancelPendingUpdates();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => Request(cli, "input.dfy").WaitAsync(TimeSpan.FromSeconds(30)));
    } finally {
      Directory.Delete(directory, true);
    }
  }

  private class CancelledPreparationVerifier : IProgramVerifier {
    public TaskCompletionSource Preparing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<ModuleVerificationPreparation> GetVerificationTasksAsync(VerificationPreparationRequest request,
      ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
      Preparing.SetResult();
      await Task.Delay(Timeout.Infinite, cancellationToken);
      return new ModuleVerificationPreparation(VerificationPreparation.Ready([]), resolution.ResolvedProgram.ProofDependencyManager);
    }
  }

  private static DafnyOptions Options(bool refresh, params string[] files) => OptionsWithWriter(TextWriter.Null, refresh, files);

  private static DafnyOptions OptionsWithWriter(TextWriter output, bool refresh, params string[] files) {
    var options = new DafnyOptions(TextReader.Null, output, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    // API construction does not populate the CLI option defaults.
    options.Set(CommonOptionBag.UnicodeCharacters, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.Set(CommonOptionBag.GeneralTraits, CommonOptionBag.GeneralTraitsOptions.Datatype);
    options.Set(BoogieOptionBag.IsolateAssertions, true);
    options.ApplyBinding(BoogieOptionBag.IsolateAssertions);
    options.VcsCores = 1;
    options.ResourceLimit = 16000000;
    options.TimeLimit = 30;
    if (Environment.GetEnvironmentVariable("Z3") is { } solver) {
      options.ProverOptions.Add("PROVER_PATH=" + solver);
    }
    foreach (var file in files) {
      options.CliRootSourceUris.Add(new Uri(file));
    }
    return options;
  }

  private static async Task<List<VerificationTaskResult>> Request(CliCompilation cli, string filter) {
    var results = new List<VerificationTaskResult>();
    await foreach (var result in cli.VerifyAllLazily(filterPosition: filter, repeatVerification: true)) {
      results.AddRange(result.Results);
    }
    return results;
  }
}
