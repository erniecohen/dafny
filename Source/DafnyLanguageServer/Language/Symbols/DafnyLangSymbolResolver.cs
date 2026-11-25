using System;
using System.CommandLine;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;

namespace Microsoft.Dafny.LanguageServer.Language.Symbols {
  /// <summary>
  /// Symbol resolver that utilizes dafny-lang to resolve the symbols.
  /// </summary>
  /// <remarks>
  /// dafny-lang makes use of static members and assembly loading. Since thread-safety of this is not guaranteed,
  /// this resolver serializes all invocations.
  /// </remarks>
  public class DafnyLangSymbolResolver : ISymbolResolver {

    public static readonly Option<bool> UseCaching = new("--use-caching", () => true,
      "Use caching to speed up analysis done by the Dafny IDE after each text edit.") {
      IsHidden = true
    };

    private readonly ILogger logger;
    private readonly ILogger<CachingResolver> innerLogger;
    private readonly SemaphoreSlim resolverMutex = new(1);
    private readonly TelemetryPublisherBase telemetryPublisher;

    /// <param name="oneShot">
    /// The resolver serves one resolution only, as in a command-line run, so it keeps no resolution
    /// cache for a later one: each resolution gets an empty cache of its own, which is what the first
    /// resolution sees anyway, and which is garbage once it is done.
    /// </param>
    public DafnyLangSymbolResolver(ILogger<DafnyLangSymbolResolver> logger, ILogger<CachingResolver> innerLogger,
      TelemetryPublisherBase telemetryPublisher, bool oneShot = false) {
      this.logger = logger;
      this.innerLogger = innerLogger;
      this.telemetryPublisher = telemetryPublisher;
      resolutionCache = oneShot ? null : new ResolutionCache();
    }

    private readonly ResolutionCache? resolutionCache;
    public async Task ResolveSymbols(Compilation compilation, Program program, CancellationToken cancellationToken) {
      // TODO The resolution requires mutual exclusion since it sets static variables of classes like Microsoft.Dafny.Type.
      //      Although, the variables are marked "ThreadStatic" - thus it might not be necessary. But there might be
      //      other classes as well.
      await resolverMutex.WaitAsync(cancellationToken);
      try {
        await RunDafnyResolver(compilation, program, cancellationToken);
      }
      finally {
        resolverMutex.Release();
      }
    }

    private async Task RunDafnyResolver(Compilation compilation, Program program, CancellationToken cancellationToken) {
      var beforeResolution = DateTime.Now;
      try {
        var resolver = program.Options.Get(UseCaching)
          ? new CachingResolver(program, innerLogger, telemetryPublisher, resolutionCache ?? new ResolutionCache())
          : new ProgramResolver(program);
        await resolver.Resolve(cancellationToken);
        if (compilation.HasErrors) {
          logger.LogDebug($"encountered errors while resolving {compilation.Project.Uri}");
        }
      }
      finally {
        telemetryPublisher.PublishTime("Resolution", compilation.Project.Uri.ToString(), DateTime.Now - beforeResolution);
      }
    }
  }
}
