using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Dafny {
  /// <summary>Prepare source-owned work with a caller-selected backend, without requiring an engine.</summary>
  public interface IProgramVerifier {
    Task<ModuleVerificationPreparation> GetVerificationTasksAsync(VerificationPreparationRequest request,
      ResolutionResult resolution,
      ModuleDefinition moduleDefinition,
      CancellationToken cancellationToken);
  }
}
