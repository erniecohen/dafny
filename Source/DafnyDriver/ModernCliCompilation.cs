#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using DafnyDriver.Commands;
using Microsoft.Dafny.Compilers;

namespace Microsoft.Dafny;

/// <summary>Modern build/run/test dispatch; the default and legacy driver remain unchanged.</summary>
public static class ModernCliCompilation {
  private static readonly AsyncLocal<CancellationToken> inputCancellation = new();
  internal static CancellationToken CurrentInputCancellation => inputCancellation.Value;

  public static Task<int> Run(DafnyOptions options, CancellationToken cancellationToken = default) =>
    options.GetOrOptionDefault(B3OptionBag.VerificationBackend) == B3OptionBag.Backend.B3
      ? RunB3Async(options, cancellationToken, ModernCliServices.Default)
      : SynchronousCliCompilation.Run(options);

  internal static async Task<int> RunB3Async(DafnyOptions options, CancellationToken cancellationToken,
    ModernCliServices services) {
    var unsupported = CheckInvocation(options);
    if (unsupported != null) {
      await options.OutputWriter.Status("Error: " + unsupported);
      return (int)ExitValue.PREPROCESSING_ERROR;
    }
    CliCompilation? compilation = null;
    var previousInputCancellation = inputCancellation.Value;
    inputCancellation.Value = cancellationToken;
    try {
      var (inputExit, inputs) = await PreparedCliInputs.PrepareAsync(options, cancellationToken);
      if (inputExit != ExitValue.SUCCESS || inputs == null) {
        return (int)inputExit;
      }
      compilation = services.CreateCompilation(options, inputs);
      // Ordinary skip must precede Start/Resolve; no solver configuration, translation or preparation.
      if (options.Get(BoogieOptionBag.NoVerify)) {
        compilation.Compilation.ShouldProcessSolverOptions = false;
      }
      compilation.Start();
      var resolution = await compilation.Resolution.WaitAsync(cancellationToken);
      var diagnosticExit = await compilation.GetAndReportExitValue();
      if (resolution == null || resolution.HasErrors || resolution.CanVerifies == null || diagnosticExit != ExitValue.SUCCESS) {
        return (int)(diagnosticExit == ExitValue.SUCCESS ? ExitValue.DAFNY_ERROR : diagnosticExit);
      }
      var program = resolution.ResolvedProgram;
      var receipt = options.Get(BoogieOptionBag.NoVerify)
        ? CliVerificationLedger.Disabled(program)
        : await ModernCliVerification.VerifyAsync(compilation, resolution, cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      if (!receipt.Authorizes(program, cancellationToken)) {
        if (await compilation.GetAndReportExitValue() == ExitValue.SUCCESS) {
          compilation.Compilation.Reporter.Error(MessageSource.Verifier, Token.Cli,
            "B3 compilation requires complete selected-backend verification: " + receipt.Reason);
        }
        return (int)ExitValue.VERIFICATION_ERROR;
      }
      if (receipt.Disposition == CliVerificationDisposition.Disabled) {
        await options.OutputWriter.Status("B3 verification disabled (--no-verify); no proof was attempted.");
      } else if (receipt.Disposition == CliVerificationDisposition.CompleteEmpty) {
        await options.OutputWriter.Status("B3 current verification scope contains no eligible checking units; no assertion was proved.");
      }
      // Worker/result ownership is released before target hooks. Keep diagnostics subscribed for
      // compiler errors until the same resolved AST has finished target compilation/execution.
      compilation.ReleaseOwnedBackend();
      cancellationToken.ThrowIfCancellationRequested();
      var filename = options.DafnyPrintCompiledFile ?? DafnyFile.FileNames(inputs.RootFiles)[0];
      var (authorized, compiled) = await ContinueAsync(receipt, program, cancellationToken,
        () => services.Compile(program, filename, inputs.ForeignFiles, true));
      if (!authorized) { return (int)ExitValue.VERIFICATION_ERROR; }
      if (!compiled) { return (int)ExitValue.COMPILE_ERROR; }
      return (int)await compilation.GetAndReportExitValue();
    } catch (OperationCanceledException) {
      await options.OutputWriter.Status("B3 compilation cancelled.");
      return (int)ExitValue.VERIFICATION_ERROR;
    } catch (UnsupportedFeatureException exception) {
      compilation?.Compilation.Reporter.Error(MessageSource.Compiler, GeneratorErrors.ErrorId.f_unsupported_feature,
        exception.Token, exception.Message);
      return (int)ExitValue.COMPILE_ERROR;
    } catch (UnsupportedInvalidOperationException exception) {
      compilation?.Compilation.Reporter.Error(MessageSource.Compiler, GeneratorErrors.ErrorId.f_unsupported_feature,
        exception.Token, exception.Message);
      return (int)ExitValue.COMPILE_ERROR;
    } catch (Exception exception) {
      await options.OutputWriter.Status("B3 compilation failed: " + exception.Message);
      return (int)ExitValue.DAFNY_ERROR;
    } finally {
      compilation?.Dispose();
      options.XmlSink?.Close();
      inputCancellation.Value = previousInputCancellation;
    }
  }

  internal static string? CheckInvocation(DafnyOptions options) {
    if (options.GetOrOptionDefault(B3OptionBag.VerificationBackend) != B3OptionBag.Backend.B3) {
      return "The modern B3 continuation requires explicit B3 selection";
    }
    if (options.Get(BoogieOptionBag.HiddenNoVerify)) {
      return "B3 does not support --hidden-no-verify; use ordinary --no-verify to record skipped verification";
    }
    if (!options.Verify && !options.Get(BoogieOptionBag.NoVerify)) {
      return "B3 skipped verification requires ordinary --no-verify provenance";
    }
    if (options.Get(BoogieOptionBag.NoVerify) &&
        (options.PrintFile != null || options.PrintSplitFile != null || options.PrintPassiveFile != null)) {
      return "B3 --no-verify does not support pre-VC or VC dumps";
    }
    if (options.Get(VerifyCommand.FilterSymbol) != null || options.Get(VerifyCommand.FilterPosition) != null ||
        options.UserConstrainedProcsToCheck) {
      return "B3 compilation requires the full current verification scope; verification filters are unsupported";
    }
    return null;
  }

  internal static async Task<(bool Authorized, bool Compiled)> ContinueAsync(CliVerificationReceipt receipt,
    Program program, CancellationToken cancellationToken, Func<Task<bool>> compile) {
    if (!receipt.Authorizes(program, cancellationToken)) { return (false, false); }
    cancellationToken.ThrowIfCancellationRequested();
    return (true, await compile());
  }
}

internal sealed record ModernCliServices(
  Func<DafnyOptions, PreparedCliInputs, CliCompilation> CreateCompilation,
  Func<Program, string, ReadOnlyCollection<string>, bool, Task<bool>> Compile) {
  internal static ModernCliServices Default { get; } = new(
    (options, inputs) => CliCompilation.CreatePreparedB3(options, inputs),
    SynchronousCliCompilation.CompileDafnyProgram);
}
