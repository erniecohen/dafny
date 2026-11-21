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
    /// A one-shot command-line run: the program is parsed and resolved once, and no formatter or
    /// IDE reads the parsed program.  The compilation then keeps neither the clone of the parsed
    /// program nor, in its parser and resolver, the caches meant for the next edit
    /// (issue 241, F4 changes 1 and 2).
    /// </summary>
    public bool OneShot { get; init; }

    public override string ToString() {
      return $"URI: {Uri}, Version: {Version}";
    }
    public DocumentUri Uri => Project.Uri;
  }

  public record BufferLine(int LineNumber, int StartIndex, int EndIndex);
}
