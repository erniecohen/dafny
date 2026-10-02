using System;
using System.Collections.Generic;

namespace Microsoft.Dafny;

/// <summary>
/// Ends only the current module's legacy inference when it exposes a nominal type cycle.
/// The phase boundary reports an ordinary resolution error and restores its local state.
/// </summary>
internal sealed class RedirectingTypeCycleException : Exception {
  internal IReadOnlyList<RedirectingTypeDecl> Cycle { get; }

  internal RedirectingTypeCycleException(IReadOnlyList<RedirectingTypeDecl> cycle) {
    Cycle = cycle;
  }
}
