using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Dafny.LanguageServer.Workspace;
using VC;

namespace Microsoft.Dafny {
  /// <summary>A module graph and its proof-label/source map have one reporting owner.</summary>
  public record ModuleVerificationTasks(IReadOnlyList<IVerificationTask> Tasks, ProofDependencyManager ProofDependencies);

  public record AssertionBatchResult(Implementation Implementation, VerificationRunResult Result);

  /// <summary>
  /// Implementations of this interface are responsible to verify the correctness of a program.
  /// </summary>
  public interface IProgramVerifier {
    Task<ModuleVerificationTasks> GetVerificationTasksAsync(ExecutionEngine engine,
      ResolutionResult resolution,
      ModuleDefinition moduleDefinition,
      CancellationToken cancellationToken);
  }
}
