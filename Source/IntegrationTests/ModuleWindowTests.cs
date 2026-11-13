using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DafnyDriver.Commands;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// --verification-module-window K (issue 241): with K greater than 1, modules overlap, and
/// results, diagnostics and the exit code are those of K = 1, in the same order.
/// </summary>
[Collection("Verification batch")]
public class ModuleWindowTests {

  // Eight modules.  M1 and M5 have a failing lemma; M3 has no symbol to verify; M2 and M6
  // have slower proofs (nonlinear arithmetic), so that later modules finish before them.
  private static string Program() {
    var text = new StringBuilder();
    for (var i = 0; i < 8; i++) {
      text.AppendLine($"module M{i} {{");
      if (i == 3) {
        text.AppendLine("  datatype D = A | B");
      } else {
        for (var j = 0; j < 4; j++) {
          text.AppendLine($"  function F{j}(n: nat): nat {{ if n == 0 then {j} else F{j}(n - 1) + 1 }}");
          text.AppendLine($"  lemma P{j}(n: nat) ensures F{j}(n) == n + {j} {{ }}");
        }
        if (i is 2 or 6) {
          text.AppendLine("  lemma Slow(a: int, b: int, c: int) requires 0 < a && 0 < b && 0 < c " +
                          "ensures a * b * c * (a + b) > 0 { assert a * b > 0; assert a * b * c > 0; }");
        }
        if (i is 1 or 5) {
          text.AppendLine($"  lemma Bad(x: int) ensures x > {i} {{ }}");
        }
      }
      text.AppendLine("}");
    }
    return text.ToString();
  }

  private record Run(string Output, List<string> Results, ExitValue Exit, int MaxInFlight, int MaxUnreported,
    Exception? Thrown);

  [Theory]
  [InlineData(2)]
  [InlineData(4)]
  [InlineData(16)]
  public async Task ResultsDiagnosticsAndExitCodeAreThoseOfOneModuleAtATime(int window) {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program());
    var baseline = await Verify(source, 1);
    var windowed = await Verify(source, window);
    Assert.Equal(ExitValue.VERIFICATION_ERROR, baseline.Exit);
    Assert.Equal(baseline.Exit, windowed.Exit);
    Assert.Equal(baseline.Results, windowed.Results);
    Assert.Equal(baseline.Output, windowed.Output);
    Assert.Equal(0, baseline.MaxInFlight);
    Assert.InRange(windowed.MaxInFlight, 2, window);
    Assert.InRange(windowed.MaxUnreported, 2, 2 * window);
  }

  [Fact]
  public async Task InstantAndDelayedPreparationKeepsTheOrder() {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program());
    // Even modules are prepared at once; odd ones take a while after translation, so that
    // later modules are prepared and solved while earlier ones wait.
    Task Delay(ModuleDefinition module, CancellationToken token) =>
      module.Name.EndsWith('1') || module.Name.EndsWith('5') ? Task.Delay(400, token) : Task.CompletedTask;
    var baseline = await Verify(source, 1, Delay);
    var windowed = await Verify(source, 4, Delay);
    Assert.Equal(baseline.Results, windowed.Results);
    Assert.Equal(baseline.Output, windowed.Output);
    Assert.Equal(baseline.Exit, windowed.Exit);
    Assert.InRange(windowed.MaxInFlight, 2, 4);
  }

  [Fact]
  public async Task ByteBudgetAdmitsOneModuleAtATime() {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program());
    var baseline = await Verify(source, 1);
    // No managed heap is under 1 MiB, so each module waits until the one before it is reported.
    var budgeted = await Verify(source, 8, memoryMiB: 1);
    Assert.Equal(baseline.Results, budgeted.Results);
    Assert.Equal(baseline.Output, budgeted.Output);
    Assert.Equal(1, budgeted.MaxInFlight);
    Assert.Equal(1, budgeted.MaxUnreported);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task FailureInLaterModuleWhileEarlierOnesRunIsReportedAsWithOneModule(bool proverFailure) {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program());
    // M4's preparation fails while M2, slower, is still in verification.  The earlier modules'
    // results are all reported, then the failure, as when M4 is started after them.
    Task Fail(ModuleDefinition module, CancellationToken token) =>
      module.Name == "M4"
        ? Task.FromException(proverFailure ? new ProverException("the solver failed") : new InvalidOperationException("broken"))
        : Task.CompletedTask;
    var baseline = await Verify(source, 1, Fail);
    var windowed = await Verify(source, 4, Fail);
    Assert.Equal(baseline.Results, windowed.Results);
    // An internal error prints its stack trace, whose frames name the loop that awaited it.
    Assert.Equal(WithoutStackFrames(WithoutInternalExceptionStatus(baseline.Output)),
      WithoutStackFrames(WithoutInternalExceptionStatus(windowed.Output)));
    if (proverFailure) {
      // The status line of the internal-exception event is placed nondeterministically by the
      // default loop itself (K = 1 against K = 1: seen in 2 of 41 runs under a parallel test load),
      // so it is compared apart from the rest of the output.
      Assert.Equal(WithoutInternalExceptionStatus(baseline.Output), WithoutInternalExceptionStatus(windowed.Output));
      Assert.Single(InternalExceptionStatus.Matches(windowed.Output));
    }
    Assert.Equal(baseline.Exit, windowed.Exit);
    Assert.Equal(baseline.Thrown?.GetType(), windowed.Thrown?.GetType());
    Assert.Contains(baseline.Results, r => r.StartsWith("M2."));
    Assert.DoesNotContain(baseline.Results, r => r.StartsWith("M4.") || r.StartsWith("M5."));
    Assert.Contains(proverFailure ? "the solver failed" : "Internal error occurred during verification: broken",
      windowed.Output);
    if (proverFailure) {
      Assert.Null(windowed.Thrown);
    } else {
      Assert.NotNull(windowed.Thrown);
    }
  }

  [Fact]
  public async Task ErrorInEarlyModuleWhileLaterOnesRunIsReportedInOrder() {
    using var directory = new TemporaryDirectory();
    // The first module fails slowly; all later modules verify while it runs.
    var text = "module M0 { lemma Bad(a: int, b: int) requires 0 < a ensures a * b * b * a > 1 { } }\n" +
               string.Concat(Enumerable.Range(1, 6).Select(i => $"module M{i} {{ lemma L(x: int) ensures x + {i} > x {{ }} }}\n"));
    var source = directory.Write("input.dfy", text);
    var baseline = await Verify(source, 1);
    var windowed = await Verify(source, 8);
    Assert.Equal(ExitValue.VERIFICATION_ERROR, windowed.Exit);
    Assert.Equal(baseline.Results, windowed.Results);
    Assert.Equal(baseline.Output, windowed.Output);
    Assert.StartsWith("M0.", windowed.Results[0]);
  }

  [Fact]
  public async Task CancellationDuringPreparationOfALaterModule() {
    using var directory = new TemporaryDirectory();
    var source = directory.Write("input.dfy", Program());
    var preparing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    async Task Block(ModuleDefinition module, CancellationToken token) {
      if (module.Name == "M2") {
        preparing.TrySetResult();
        await Task.Delay(Timeout.Infinite, token);
      }
    }
    var options = Options(source, 4);
    var cli = CliCompilation.Create(options, new HookedVerifier(Block));
    using var compilation = cli.Compilation;
    cli.Start();
    var results = new List<string>();
    var consumed = Task.Run(async () => {
      await foreach (var result in cli.VerifyAllLazily(repeatVerification: true)) {
        results.Add(result.CanVerify.FullDafnyName);
      }
    });
    await preparing.Task.WaitAsync(TimeSpan.FromSeconds(60));
    compilation.CancelPendingUpdates();
    // As with one module at a time (VerificationBatchTests), cancellation ends the request.
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consumed.WaitAsync(TimeSpan.FromSeconds(60)));
    Assert.DoesNotContain(results, r => r.StartsWith("M2.") || r.StartsWith("M4."));
  }

  private static async Task<Run> Verify(string source, int window,
    Func<ModuleDefinition, CancellationToken, Task>? hook = null, uint memoryMiB = 0) {
    var output = new StringWriter();
    var options = Options(source, window, output);
    if (memoryMiB != 0) {
      options.Set(VerifyCommand.VerificationModuleWindowMemory, memoryMiB);
    }
    var cli = CliCompilation.Create(options, hook == null ? null : new HookedVerifier(hook));
    using var compilation = cli.Compilation;
    cli.Start();
    var resolution = await cli.Resolution;
    Assert.NotNull(resolution);
    var results = new List<string>();
    Exception? thrown = null;
    using (var verificationResults = new Subject<CanVerifyResult>()) {
      VerifyCommand.ReportVerificationDiagnostics(cli, verificationResults);
      var summarized = VerifyCommand.ReportVerificationSummary(cli, verificationResults);
      try {
        await foreach (var result in cli.VerifyAllLazily()) {
          results.Add(result.CanVerify.FullDafnyName + ": " + string.Join(",",
            result.Results.Select(r => r.Result.Outcome + "/" + r.Result.ResourceCount)));
          verificationResults.OnNext(result);
        }
        verificationResults.OnCompleted();
      } catch (Exception e) {
        thrown = e;
        verificationResults.OnCompleted(); // The diagnostics observer takes no error.
      }
      await summarized;
    }
    var exit = await cli.GetAndReportExitValue();
    return new Run(output.ToString(), results, exit, cli.MaxModulesInFlight, cli.MaxModulesUnreported, thrown);
  }

  private static readonly System.Text.RegularExpressions.Regex InternalExceptionStatus =
    new(@"^Encountered internal compilation exception: [^\n]*\n", System.Text.RegularExpressions.RegexOptions.Multiline);

  private static string WithoutInternalExceptionStatus(string output) => InternalExceptionStatus.Replace(output, "");

  private static string WithoutStackFrames(string output) =>
    System.Text.RegularExpressions.Regex.Replace(output,
      @"\s*(at [^\n]*|--- End of stack trace from previous location ---)", "");

  private static DafnyOptions Options(string source, int window, TextWriter? output = null) {
    output ??= TextWriter.Null;
    var options = new DafnyOptions(TextReader.Null, output, output);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    // API construction does not populate the CLI option defaults.
    options.Set(CommonOptionBag.UnicodeCharacters, true);
    options.Set(VerifyCommand.VerificationModuleWindow, (uint)window);
    options.VcsCores = 4;
    // Contract (b) of issue 241: per-proof SMT input and resource counts are compared between K = 1
    // and K > 1 only with a fresh solver per proof and deterministic literal hashes.
    options.RestartProverPerVC = true;
    options.DeterministicLiteralHashes = true;
    options.ResourceLimit = 16000000;
    options.TimeLimit = 0;
    if (Environment.GetEnvironmentVariable("Z3") is { } solver) {
      options.ProverOptions.Add("PROVER_PATH=" + solver);
    }
    options.CliRootSourceUris.Add(new Uri(source));
    return options;
  }

  /// <summary>Dafny's own verifier, with a hook that runs after a module is translated and prepared.</summary>
  private class HookedVerifier(Func<ModuleDefinition, CancellationToken, Task> hook) : IProgramVerifier {
    private readonly DafnyProgramVerifier inner = new(NullLogger<DafnyProgramVerifier>.Instance);

    public async Task<ModuleVerificationTasks> GetVerificationTasksAsync(ExecutionEngine engine,
      ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
      var tasks = await inner.GetVerificationTasksAsync(engine, resolution, moduleDefinition, cancellationToken);
      await hook(moduleDefinition, cancellationToken);
      return tasks;
    }
  }

  private sealed class TemporaryDirectory : IDisposable {
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dafny-module-window-" + Guid.NewGuid());

    public TemporaryDirectory() {
      Directory.CreateDirectory(Path);
    }

    public string Write(string name, string text) {
      var file = System.IO.Path.Combine(Path, name);
      File.WriteAllText(file, text);
      return file;
    }

    public void Dispose() {
      Directory.Delete(Path, true);
    }
  }
}
