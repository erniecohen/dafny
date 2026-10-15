using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;
using DafnyDriver.Commands;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Xunit;

namespace IntegrationTests;

[CollectionDefinition("Verification batch", DisableParallelization = true)]
public class VerificationBatchCollection { }

[Collection("Verification batch")]
public class VerificationBatchTests {
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
      using var events = compilation.Updates.Subscribe(ev => {
        if (ev is FinishedParsing) { parsing++; }
        if (ev is FinishedResolution) { resolving++; }
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
      Assert.Equal(ExitValue.SUCCESS, await cli.GetAndReportExitValue());
    } finally {
      Directory.Delete(directory, true);
    }

    static string Describe(DafnyCore.Verifier.ProofDependency dependency) =>
      dependency.RangeString() + ":" + dependency.Description;
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
      Assert.All(await Request(cli, "input.dfy"), result => Assert.Equal(SolverOutcome.Valid, result.Result.Outcome));
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

    public async Task<IReadOnlyList<IVerificationTask>> GetVerificationTasksAsync(ExecutionEngine engine,
      ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
      Preparing.SetResult();
      await Task.Delay(Timeout.Infinite, cancellationToken);
      return Array.Empty<IVerificationTask>();
    }
  }

  private static DafnyOptions Options(bool refresh, params string[] files) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
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
