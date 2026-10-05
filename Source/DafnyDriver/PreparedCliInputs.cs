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

  private PreparedCliInputs(IReadOnlyList<DafnyFile> rootFiles, IReadOnlyList<string> foreignFiles) {
    RootFiles = Array.AsReadOnly(rootFiles.ToArray());
    ForeignFiles = Array.AsReadOnly(foreignFiles.ToArray());
  }

  public static async Task<(ExitValue ExitValue, PreparedCliInputs? Inputs)> PrepareAsync(
    DafnyOptions options, CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();
    var target = SynchronousCliCompilation.GetBackend(options);
    if (target == null) {
      return (ExitValue.PREPROCESSING_ERROR, null);
    }
    options.Backend = target;
    var (exitValue, files, foreignFiles) = await SynchronousCliCompilation.GetDafnyFiles(options);
    cancellationToken.ThrowIfCancellationRequested();
    return exitValue == ExitValue.SUCCESS
      ? (exitValue, new PreparedCliInputs(files, foreignFiles)) : (exitValue, null);
  }
}
