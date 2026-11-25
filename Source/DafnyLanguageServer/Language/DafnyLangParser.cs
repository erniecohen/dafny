using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Language.Symbols;
using Microsoft.Dafny.LanguageServer.Workspace;

namespace Microsoft.Dafny.LanguageServer.Language {
  /// <summary>
  /// Parser implementation that makes use of the parse of dafny-lang. It may only be initialized exactly once since
  /// it requires initial setup of static members.
  /// </summary>
  /// <remarks>
  /// dafny-lang makes use of static members and assembly loading. Since thread-safety of this is not guaranteed,
  /// this parser serializes all invocations.
  /// </remarks>
  public sealed class DafnyLangParser : IDafnyParser {
    private readonly DafnyOptions options;
    private readonly IFileSystem fileSystem;
    private readonly TelemetryPublisherBase telemetryPublisher;
    private readonly ILogger<DafnyLangParser> logger;
    private readonly SemaphoreSlim mutex = new(1);
    private readonly ProgramParser? programParser;
    private readonly Func<ProgramParser> createProgramParser;

    /// <param name="oneShot">
    /// The parser serves one parse only, as in a command-line run, so it keeps no parse cache, which
    /// holds an uncloned copy of every file's syntax tree, for a later one: each parse gets a parser of
    /// its own, with the empty cache the first parse sees anyway.
    /// </param>
    public DafnyLangParser(DafnyOptions options, IFileSystem fileSystem, TelemetryPublisherBase telemetryPublisher,
      ILogger<DafnyLangParser> logger, ILogger<CachingParser> innerParserLogger, bool oneShot = false) {
      this.options = options;
      this.fileSystem = fileSystem;
      this.telemetryPublisher = telemetryPublisher;
      this.logger = logger;
      createProgramParser = () => options.Get(DafnyLangSymbolResolver.UseCaching)
        ? new CachingParser(innerParserLogger, fileSystem, telemetryPublisher)
        : new ProgramParser(innerParserLogger, fileSystem);
      programParser = oneShot ? null : createProgramParser();
    }

    public async Task<ProgramParseResult> Parse(Compilation compilation, CancellationToken cancellationToken) {
      await mutex.WaitAsync(cancellationToken);

      var rootFiles = await compilation.RootFiles;
      var beforeParsing = DateTime.Now;
      try {
        return await (programParser ?? createProgramParser()).ParseFiles(compilation.Project.ProjectName, rootFiles, compilation.Reporter, cancellationToken);
      }
      finally {
        telemetryPublisher.PublishTime("Parse", compilation.Project.Uri.ToString(), DateTime.Now - beforeParsing);
        mutex.Release();
      }
    }
  }
}
