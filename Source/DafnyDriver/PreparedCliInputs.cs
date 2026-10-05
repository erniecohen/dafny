#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace DafnyDriver.Commands;

/// <summary>Admitted files for one modern CLI compilation, before parsing begins.</summary>
internal sealed class PreparedCliInputs {
  public IReadOnlyList<DafnyFile> RootFiles { get; }
  public ReadOnlyCollection<string> ForeignFiles { get; }
  public IReadOnlyList<DafnyDiagnostic> AdmissionDiagnostics { get; }

  private PreparedCliInputs(IReadOnlyList<DafnyFile> rootFiles, IReadOnlyList<string> foreignFiles,
    IReadOnlyList<DafnyDiagnostic> diagnostics) {
    RootFiles = Array.AsReadOnly(rootFiles.ToArray());
    ForeignFiles = Array.AsReadOnly(foreignFiles.ToArray());
    AdmissionDiagnostics = Array.AsReadOnly(diagnostics.ToArray());
  }

  public static async Task<(ExitValue ExitValue, PreparedCliInputs? Inputs)> PrepareAsync(
    DafnyOptions options, CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();
    var target = SynchronousCliCompilation.GetBackend(options);
    if (target == null) {
      return (ExitValue.PREPROCESSING_ERROR, null);
    }
    options.Backend = target;
    var admission = new BatchErrorReporter(options);
    var (exitValue, files, foreignFiles) = await SynchronousCliCompilation.GetDafnyFiles(options, admission);
    cancellationToken.ThrowIfCancellationRequested();
    if (exitValue != ExitValue.SUCCESS || admission.ErrorCount != 0 ||
        admission.AllMessages.Any(diagnostic => diagnostic.Level == ErrorLevel.Warning) &&
        !options.Get(CommonOptionBag.AllowWarnings)) {
      ErrorReporter console = options.DiagnosticsFormat switch {
        DafnyOptions.DiagnosticsFormats.PlainText => new ConsoleErrorReporter(options),
        DafnyOptions.DiagnosticsFormats.JSON => new JsonConsoleErrorReporter(options),
        _ => throw new ArgumentOutOfRangeException()
      };
      foreach (var diagnostic in admission.AllMessages) { console.MessageCore(diagnostic); }
      if (exitValue == ExitValue.SUCCESS) {
        exitValue = admission.ErrorCount != 0 ? ExitValue.PREPROCESSING_ERROR : ExitValue.DAFNY_ERROR;
      }
      return (exitValue, null);
    }
    return (ExitValue.SUCCESS, new PreparedCliInputs(files, foreignFiles, admission.AllMessages));
  }
}
