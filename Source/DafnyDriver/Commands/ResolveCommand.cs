using System;
using System.CommandLine;
using DafnyDriver.Commands;

namespace Microsoft.Dafny;

static class ResolveCommand {

  /// <summary>The front-end profile (#241) also applies to resolution alone.</summary>
  private static readonly Option[] ProfileOptions = [
    CommonOptionBag.PipelineProfile,
    CommonOptionBag.PipelineProfileHeap,
    CommonOptionBag.PipelineProfileGcEvents,
    CommonOptionBag.PipelineProfileHold
  ];

  public static Command Create() {
    var result = new Command("resolve", "Only check for parse and type resolution errors.");
    result.AddArgument(DafnyCommands.FilesArgument);
    foreach (var option in DafnyCommands.ConsoleOutputOptions.Concat(DafnyCommands.ResolverOptions).Concat(ProfileOptions)) {
      result.AddOption(option);
    }
    DafnyNewCli.SetHandlerUsingDafnyOptionsContinuation(result, async (options, _) => {
      options.Set(DafnyFile.DoNotVerifyDependencies, true);
      var compilation = CliCompilation.Create(options);
      compilation.Compilation.ShouldProcessSolverOptions = false;
      compilation.Start();
      await compilation.Resolution;

      var value = await compilation.GetAndReportExitValue();
      if (value == ExitValue.SUCCESS) {
        await options.OutputWriter.Status("\nDafny program verifier did not attempt verification");
      }
      PipelineProfile.For(options)?.Finish("resolve");
      return (int)value;
    });
    return result;
  }
}