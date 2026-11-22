using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Extensions.Logging;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Various;

/// If any top-level declaration has an attribute `{:neverVerify}`,
/// this verifier will return a task that only completes when cancelled
/// which can be useful to test against race conditions
class SlowVerifier : IProgramVerifier {
  public SlowVerifier(ILogger<DafnyProgramVerifier> logger) {
    verifier = new DafnyProgramVerifier(logger);
  }

  private readonly DafnyProgramVerifier verifier;

  public async Task<ModuleVerificationPreparation> GetVerificationTasksAsync(VerificationPreparationRequest request,
    ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
    var program = resolution.ResolvedProgram;
    var attributes = program.Modules().SelectMany(m => {
      return m.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(d => d.Members.Select(member => member.Attributes));
    }).ToList();

    var tasks = await verifier.GetVerificationTasksAsync(request, resolution, moduleDefinition, cancellationToken);
    if (attributes.Any(a => Attributes.Contains(a, "neverVerify"))) {
      tasks = tasks with { Preparation = VerificationPreparation.Ready(tasks.Preparation.GetWorkOrThrow().Select(t => new NeverVerifiesImplementationTask(t))) };
    }

    return tasks;
  }

  class NeverVerifiesImplementationTask : IVerificationWork {
    private readonly IVerificationWork original;
    private readonly Subject<VerificationWorkStatus> source;

    public NeverVerifiesImplementationTask(IVerificationWork original) {
      this.original = original;
      source = new();
    }

    public IVerificationWork FromSeed(int newSeed) {
      return this;
    }

    public VerificationWorkStatus CacheStatus => new VerificationWorkStale();
    public VerificationSource Source => original.Source;
    public IReadOnlyList<Microsoft.Dafny.Function> HiddenFunctions => original.HiddenFunctions;
    public IVerificationPayload? Payload => original.Payload;

    public IObservable<VerificationWorkStatus> TryRun() {
      return source;
    }

    public bool IsIdle => false;

    public void Cancel() {
      source.OnError(new TaskCanceledException());
    }
  }
}
