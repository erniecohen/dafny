#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DafnyCore;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

/// <summary>Backend selection and translation flags, independent of a native execution engine.</summary>
public record VerificationPreparationRequest(bool InsertChecksums,
  Func<IVerificationBackend<Bpl.Program>> CreateBackend, Action<Bpl.Program>? Capture = null);

/// <summary>A prepared module and its source/dependency map retain one reporting owner.</summary>
public record ModuleVerificationPreparation(VerificationPreparation Preparation, ProofDependencyManager ProofDependencies);

/// <summary>
/// Shared large-stack translation and backend preparation. The capture callback observes the
/// translator graph before backend mutation; it must copy anything it intends to retain.
/// The backend factory is delayed until after capture, preserving native cache/print ordering.
/// Translation requires no execution engine; only the selected backend owns task preparation.
/// </summary>
public sealed class VerificationPreparationPipeline : IDisposable {
  private readonly SemaphoreSlim mutex = new(1);

  public async Task<ModuleVerificationPreparation> PrepareAsync(
    ResolutionResult resolution, ModuleDefinition moduleDefinition, bool insertChecksums,
    Func<IVerificationBackend<Bpl.Program>> createBackend, CancellationToken cancellationToken,
    Action<Bpl.Program>? capture = null) {
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
      var errorReporter = program.Reporter;

      cancellationToken.ThrowIfCancellationRequested();

      var dependencies = new ProofDependencyManager();
      var boogieProgram = await DafnyMain.LargeStackFactory.StartNew(() => {
        Type.ResetScopes();
        var translatorFlags = new BoogieGenerator.TranslatorFlags(errorReporter.Options) {
          InsertChecksums = insertChecksums,
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
        var threadStart = profile?.BeginThread();
        var translated = translator.DoTranslation(resolution.ResolvedProgram, moduleDefinition);
        if (profile != null) {
          profile.FrontEnd("translate.module", threadStart!.Value, new Dictionary<string, object?> {
            ["module"] = moduleName
          });
          profile.Span("translate", profileStart!.Value, new Dictionary<string, object?> {
            ["module"] = moduleName,
            ["threadAllocBytes"] = GC.GetAllocatedBytesForCurrentThread() - threadAllocated,
            ["declarations"] = translated.TopLevelDeclarations.Count(),
            ["implementations"] = translated.Implementations.Count()
          });
        }
        return translated;
      }, cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      capture?.Invoke(boogieProgram);

      profileStart = profile?.Begin();
      var preparation = await createBackend().PrepareAsync(boogieProgram, cancellationToken);
      if (profile != null) {
        // Boogie's preparation: type checking, inlining, preprocessing, passification and splitting.
        profile.Span("prepare", profileStart!.Value, new Dictionary<string, object?> {
          ["module"] = moduleName,
          ["tasks"] = preparation.IsReady ? preparation.GetWorkOrThrow().Count : (int?)null
        });
      }
      return new ModuleVerificationPreparation(preparation, dependencies);
    }
    finally {
      mutex.Release();
    }
  }

  public void Dispose() {
    mutex.Dispose();
  }
}
