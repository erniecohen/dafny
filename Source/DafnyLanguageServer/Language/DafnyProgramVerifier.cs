using Microsoft.Boogie;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
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

    public async Task<ModuleVerificationTasks> GetVerificationTasksAsync(ExecutionEngine engine,
      ResolutionResult resolution,
      ModuleDefinition moduleDefinition,
      CancellationToken cancellationToken) {

      var program = resolution.ResolvedProgram;
      var prepared = await preparationPipeline.PrepareAsync(resolution, moduleDefinition,
        0 < engine.Options.VerifySnapshots,
        () => new BoogieVerificationBackend(engine, Caching.TaskCacheSession.For(program.Options)),
        cancellationToken, boogieProgram => {
          if (engine.Options.PrintFile != null) {
            var moduleCount = BoogieGenerator.VerifiableModules(program).Count();
            var fileName = moduleCount > 1
              ? DafnyMain.BoogieProgramSuffix(engine.Options.PrintFile, moduleDefinition.SanitizedName)
              : engine.Options.PrintFile;
            ExecutionEngine.PrintBplFile(engine.Options, fileName, boogieProgram, false, false, engine.Options.PrettyPrint);
          }
        });
      var tasks = BoogieVerificationBackend.GetNativeTasks(prepared.Preparation);
      return new ModuleVerificationTasks(tasks, prepared.ProofDependencies);
    }

    public void Dispose() {
      preparationPipeline.Dispose();
    }
  }
}
