using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Extensions.Logging;
using VC;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Various;

/// If any top-level declaration has an attribute `{:neverVerify}`,
/// this verifier will return a task that only completes when cancelled
/// which can be useful to test against race conditions
class SlowVerifier : IProgramVerifier {
  public SlowVerifier(ILogger<DafnyProgramVerifier> logger) {
    verifier = new DafnyProgramVerifier(logger);
  }

  private readonly DafnyProgramVerifier verifier;

  public async Task<IReadOnlyList<IVerificationWorkItem>> GetVerificationTasksAsync(IVerificationBackend backend,
    ResolutionResult resolution, ModuleDefinition moduleDefinition, CancellationToken cancellationToken) {
    var program = resolution.ResolvedProgram;
    var attributes = program.Modules().SelectMany(m => {
      return m.TopLevelDecls.OfType<TopLevelDeclWithMembers>().SelectMany(d => d.Members.Select(member => member.Attributes));
    }).ToList();

    var tasks = await verifier.GetVerificationTasksAsync(backend, resolution, moduleDefinition, cancellationToken);
    if (attributes.Any(a => Attributes.Contains(a, "neverVerify"))) {
      tasks = tasks.Select(t => new NeverVerifiesImplementationTask(t)).ToList();
    }

    return tasks;
  }

  class NeverVerifiesImplementationTask : IVerificationWorkItem {
    private readonly IVerificationWorkItem original;
    private readonly Subject<VerificationStatus> source;

    public NeverVerifiesImplementationTask(IVerificationWorkItem original) {
      this.original = original;
      source = new();
    }

    public IVerificationWorkItem FromSeed(int newSeed) {
      return this;
    }

    public VerificationStatus CacheStatus => new VerificationStale();
    public VerificationIdentity Identity => original.Identity;
    public VerificationSourceInfo Source => original.Source;

    public IObservable<VerificationStatus> TryRun() {
      return source;
    }

    public bool IsIdle => false;

    public void Cancel() {
      source.OnError(new TaskCanceledException());
    }
  }
}
