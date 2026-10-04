using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;

namespace Microsoft.Dafny {
  /// <summary>
  /// Implementations of this interface are responsible to verify the correctness of a program.
  /// </summary>
  public interface IProgramVerifier {
    Task<IReadOnlyList<IVerificationWorkItem>> GetVerificationTasksAsync(IVerificationBackend backend,
      ResolutionResult resolution,
      ModuleDefinition moduleDefinition,
      CancellationToken cancellationToken);
  }
}
