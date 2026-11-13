using Microsoft.Boogie;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DafnyCore;
using Microsoft.Dafny.LanguageServer.Workspace;

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
    private readonly SemaphoreSlim mutex = new(1);

    public DafnyProgramVerifier(ILogger<DafnyProgramVerifier> logger) {
      this.logger = logger;
    }

    public async Task<ModuleVerificationTasks> GetVerificationTasksAsync(ExecutionEngine engine,
      ResolutionResult resolution,
      ModuleDefinition moduleDefinition,
      CancellationToken cancellationToken) {

      if (!BoogieGenerator.ShouldVerifyModule(resolution.ResolvedProgram, moduleDefinition)) {
        throw new Exception("tried to get verification tasks for a module that is not verified");
      }

      var profile = PipelineProfile.For(resolution.ResolvedProgram.Options);
      var moduleName = profile != null ? PipelineProfile.ModuleName(moduleDefinition) : null;
      var profileStart = profile?.Begin();
      await mutex.WaitAsync(cancellationToken);
      try {
        if (profile != null) {
          profile.Span("translate.wait", profileStart!.Value, new Dictionary<string, object?> { ["module"] = moduleName });
          profileStart = profile.Begin();
        }

        var program = resolution.ResolvedProgram;
        var errorReporter = (ObservableErrorReporter)program.Reporter;

        cancellationToken.ThrowIfCancellationRequested();

        var dependencies = new ProofDependencyManager();
        var boogieProgram = await DafnyMain.LargeStackFactory.StartNew(() => {
          Type.ResetScopes();
          var translatorFlags = new BoogieGenerator.TranslatorFlags(errorReporter.Options) {
            InsertChecksums = 0 < engine.Options.VerifySnapshots,
            ReportRanges = program.Options.Get(Snippets.ShowSnippets),
            PreludeText = program.PreludeText
          };
          if (program.Options.Get(CommonOptionBag.TranslationCache)) {
            var cache = ModuleTranslationCache.ProcessCache;
            var module = cache.TranslateModule(program, moduleDefinition, translatorFlags, cancellationToken, out var reused);
            var cached = module.Program;
            dependencies = module.ProofDependencies;
            if (profile != null) {
              profile.Span("translate", profileStart!.Value, new Dictionary<string, object?> {
                ["module"] = moduleName, ["cached"] = reused,
                ["declarations"] = cached.TopLevelDeclarations.Count(), ["implementations"] = cached.Implementations.Count()
              });
            }
            return cached;
          }
          var threadAllocated = profile != null ? GC.GetAllocatedBytesForCurrentThread() : 0;
          var translator = new BoogieGenerator(errorReporter, dependencies, translatorFlags);
          if (profile != null) {
            // The constructor reads, parses and resolves the prelude, once per module.
            profile.Span("translate.prelude", profileStart!.Value, new Dictionary<string, object?> {
              ["module"] = moduleName,
              ["threadAllocBytes"] = GC.GetAllocatedBytesForCurrentThread() - threadAllocated
            });
            profileStart = profile.Begin();
            threadAllocated = GC.GetAllocatedBytesForCurrentThread();
          }
          var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);
          if (profile != null) {
            profile.Span("translate", profileStart!.Value, new Dictionary<string, object?> {
              ["module"] = moduleName,
              ["threadAllocBytes"] = GC.GetAllocatedBytesForCurrentThread() - threadAllocated,
              ["declarations"] = translated.TopLevelDeclarations.Count(),
              ["implementations"] = translated.Implementations.Count()
            });
          }
          return translated;
        }, cancellationToken);
        var suffix = moduleDefinition.SanitizedName;

        cancellationToken.ThrowIfCancellationRequested();

        if (engine.Options.PrintFile != null) {
          var moduleCount = BoogieGenerator.VerifiableModules(program).Count();
          var fileName = moduleCount > 1 ? DafnyMain.BoogieProgramSuffix(engine.Options.PrintFile, suffix) : engine.Options.PrintFile;
          ExecutionEngine.PrintBplFile(engine.Options, fileName, boogieProgram, false, false, engine.Options.PrettyPrint);
        }

        profileStart = profile?.Begin();
        var resultCache = Caching.TaskCacheSession.For(program.Options);
        var tasks = await engine.GetVerificationTasks(boogieProgram, cancellationToken);
        if (resultCache != null) { tasks = tasks.Select(resultCache.Wrap).ToList(); }
        if (profile != null) {
          // Boogie's preparation: type checking, inlining, preprocessing, passification and splitting.
          profile.Span("prepare", profileStart!.Value, new Dictionary<string, object?> {
            ["module"] = moduleName,
            ["tasks"] = tasks.Count
          });
        }
        return new ModuleVerificationTasks(tasks, dependencies);
      }
      finally {
        mutex.Release();
      }
    }

    public void Dispose() {
      mutex.Dispose();
    }
  }
}
