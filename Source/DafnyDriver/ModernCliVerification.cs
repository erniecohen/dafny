#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace DafnyDriver.Commands;

/// <summary>Complete B3 module preparation and streams for one modern compiler continuation.</summary>
internal static class ModernCliVerification {
  internal static async Task<CliVerificationReceipt> VerifyAsync(CliCompilation compilation,
    ResolutionResult resolution, CancellationToken cancellationToken) {
    if (resolution.HasErrors || resolution.CanVerifies == null ||
        !ReferenceEquals(resolution, await compilation.Resolution)) {
      return CliVerificationLedger.Rejected(resolution.ResolvedProgram, compilation.Options,
        "Resolution did not admit a complete current verification scope");
    }
    if (!compilation.Options.Get(CommonOptionBag.UnicodeCharacters) &&
        compilation.Options.Backend is not Microsoft.Dafny.Compilers.CppBackend) {
      compilation.Compilation.Reporter.Deprecated(MessageSource.Verifier, "unicodeCharDeprecated", Token.Cli,
        "the option unicode-char has been deprecated.");
    }
    var scope = resolution.CanVerifies.Values.SelectMany(tree => tree.Values).Distinct<ICanVerify>(
      System.Collections.Generic.ReferenceEqualityComparer.Instance).ToArray();
    var modules = BoogieGenerator.VerifiableModules(resolution.ResolvedProgram).
      OrderBy(module => module.Origin.pos).ToArray();
    var ledger = new CliVerificationLedger(resolution.ResolvedProgram, modules, scope);
    compilation.MarkCompilationVerificationAttempted();
    try {
      await VerifyCommand.VerifyAndReportPreparedAsync(compilation, resolution,
        Results(compilation, modules, ledger, cancellationToken), cancellationToken);
    } catch (Exception exception) {
      ledger.Reject(exception is OperationCanceledException ? "Verification was cancelled" : exception.Message);
      compilation.Compilation.Reporter.Error(MessageSource.Verifier, Token.Cli,
        "B3 compilation verification did not complete: " + exception.Message);
    }
    return ledger.Seal(await compilation.GetAndReportExitValue() == ExitValue.SUCCESS, cancellationToken);
  }

  private static async IAsyncEnumerable<CanVerifyResult> Results(CliCompilation compilation,
    IReadOnlyList<ModuleDefinition> modules, CliVerificationLedger ledger,
    [EnumeratorCancellation] CancellationToken cancellationToken) {
    foreach (var module in modules) {
      cancellationToken.ThrowIfCancellationRequested();
      ledger.BeginPreparation(module);
      IReadOnlyList<IVerificationWorkItem> prepared;
      try {
        prepared = await compilation.Compilation.PrepareModuleForCompilationAsync(module, cancellationToken);
        if (!ledger.CompletePreparation(module, prepared)) {
          throw new InvalidOperationException("The prepared module inventory was rejected before execution");
        }
      } catch (Exception exception) {
        ledger.FailedPreparation(module, exception.Message);
        throw;
      }
      try {
        foreach (var ownerTasks in prepared.GroupBy(task => task.Source.CanVerify)) {
          var completed = new List<VerificationWorkItemResult>();
          foreach (var task in ownerTasks) {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await RunUnitAsync(task, cancellationToken);
            ledger.CompleteUnit(module, task, result);
            completed.Add(new VerificationWorkItemResult(task, result));
            if (compilation.Options.Get(CommonOptionBag.ProgressOption) == CommonOptionBag.ProgressLevel.Batch) {
              await compilation.Options.OutputWriter.Status(
                $"{task.Source.ProgressDescription}: {CliCompilation.DescribeOutcome(result.Outcome)}");
            }
          }
          yield return new CanVerifyResult(ownerTasks.Key, completed);
        }
      } finally {
        foreach (var task in prepared) { task.Cancel(); }
        compilation.Compilation.ClearModuleCache(module);
      }
    }
  }

  internal static async Task<VerificationResult> RunUnitAsync(IVerificationWorkItem task,
    CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();
    var updates = task.TryRun() ?? throw new InvalidOperationException("A checking unit supplied no owned result stream");
    var terminal = await updates.RequireCompletion().OfType<VerificationCompleted>().SingleAsync().ToTask(cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();
    return terminal.Result;
  }
}
