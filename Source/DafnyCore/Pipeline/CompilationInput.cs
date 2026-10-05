#nullable enable
using System;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny {
  /// <summary>
  /// Contains all the inputs of a Compilation
  /// </summary>
  public record CompilationInput(DafnyOptions Options, int Version, DafnyProject Project) {

    /// <summary>
    /// A constructor-owned CLI input. When present, file admission has already happened once;
    /// Compilation must not reread project roots, libraries or standard input.
    /// </summary>
    public IReadOnlyList<DafnyFile>? PreparedRootFiles { get; init; }


    public override string ToString() {
      return $"URI: {Uri}, Version: {Version}";
    }
    public DocumentUri Uri => Project.Uri;
  }

  public record BufferLine(int LineNumber, int StartIndex, int EndIndex);
}
