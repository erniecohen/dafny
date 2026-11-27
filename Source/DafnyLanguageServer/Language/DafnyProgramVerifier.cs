using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Dafny.LanguageServer.Language {
  /// <summary>
  /// dafny-lang based implementation of the program verifier. Since it makes use of static members,
  /// any access is synchronized. Moreover, it ensures that exactly one instance exists over the whole
  /// application lifetime.
  /// </summary>
  /// <remarks>
  /// dafny-lang makes use of static members and assembly loading. Since thread-safety of this is not guaranteed,
  /// this verifier serializes all invocations.
  /// </remarks>
  public class DafnyProgramVerifier : IProgramVerifier {
    private readonly ILogger<DafnyProgramVerifier> logger;
    private readonly VerificationPreparationPipeline preparationPipeline = new();

    public DafnyProgramVerifier(ILogger<DafnyProgramVerifier> logger) {
      this.logger = logger;
    }

    public Task<ModuleVerificationPreparation> GetVerificationTasksAsync(VerificationPreparationRequest request,
      ResolutionResult resolution,
      ModuleDefinition moduleDefinition,
      CancellationToken cancellationToken) =>
      preparationPipeline.PrepareAsync(resolution, moduleDefinition, request.InsertChecksums,
        request.CreateBackend, cancellationToken, request.Capture, requiresNativeTasks: request.RequiresNativeTasks);

    public void Dispose() {
      preparationPipeline.Dispose();
    }
  }
}
