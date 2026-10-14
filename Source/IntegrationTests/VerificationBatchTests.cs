using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    await File.WriteAllTextAsync(first, "lemma Good(x: int) { assert x + 1 > x; }\n");
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
      var overlap = await Request(cli, "first.dfy:1");
      var negative = await Request(cli, "second.dfy");
      var afterNegative = await Request(cli, "first.dfy");
      foreach (var result in new[] { original, duplicate, overlap, afterNegative }) {
        Assert.NotEmpty(result);
        Assert.All(result, part => Assert.Equal(SolverOutcome.Valid, part.Result.Outcome));
        Assert.All(result, part => Assert.True(part.Result.ResourceCount > 0));
      }
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

  private static DafnyOptions Options(bool refresh, params string[] files) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.Set(CommonOptionBag.GeneralTraits, CommonOptionBag.GeneralTraitsOptions.Datatype);
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
