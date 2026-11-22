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

      // --persistent-translation: a module whose translation key has stored results is replayed
      // without translation; otherwise its fresh translation is recorded, and replayed when its
      // regenerated Boogie program has stored results.
      var persistent = Caching.PersistentModuleCache.For(program.Options);
      Caching.PersistentModuleCache.ModuleIdentity? identity = null;
      if (persistent != null) {
        // As for an in-process hit, a replay must not bypass the global soundness admission.
        BoogieGenerator.RequireCardinalityValidation(program);
        var keyFlags = new BoogieGenerator.TranslatorFlags(errorReporter.Options) {
          InsertChecksums = insertChecksums,
          ReportRanges = program.Options.Get(Snippets.ShowSnippets),
          PreludeText = program.PreludeText
        };
        if (persistent.Key(program, moduleDefinition, keyFlags) is { } key) {
          // The keys this module is looked up under, for the option classification test.
          profile?.Point("persistent-keys", new Dictionary<string, object?> {
            ["module"] = moduleDefinition.FullName, ["resolution"] = moduleDefinition.ResolutionIdentity,
            ["translation"] = key, ["results"] = persistent.EnvironmentDigest
          });
          identity = new Caching.PersistentModuleCache.ModuleIdentity(key, null, null, null);
          if (!persistent.Audit && persistent.ReadTranslation(key) is { } stored &&
              persistent.TryReplay(program, moduleDefinition, stored.Fingerprint, true, out _) is { } replayed) {
            profile?.Point("persistent-module", new Dictionary<string, object?> {
              ["module"] = moduleName, ["hit"] = "translation-key", ["tasks"] = replayed.Count
            });
            return new ModuleVerificationPreparation(
              VerificationPreparation.Ready(replayed.Select(task => new BoogieVerificationWork(task))), new ProofDependencyManager());
          }
        }
      }

      var dependencies = new ProofDependencyManager();
      var boogieProgram = await DafnyMain.LargeStackFactory.StartNew(() => {
        Type.ResetScopes();
        var translatorFlags = new BoogieGenerator.TranslatorFlags(errorReporter.Options) {
          InsertChecksums = insertChecksums,
          ReportRanges = program.Options.Get(Snippets.ShowSnippets),
          PreludeText = program.PreludeText
        };
        if (identity != null) {
          var persistentAllocated = profile != null ? GC.GetAllocatedBytesForCurrentThread() : 0;
          Bpl.Program graph;
          string? text;
          string? unrecorded = null;
          var reused = false;
          if (program.Options.Get(CommonOptionBag.TranslationCache)) {
            var module = ModuleTranslationCache.ProcessCache.TranslateModule(program, moduleDefinition, translatorFlags,
              cancellationToken, out reused);
            graph = module.Program;
            text = module.Text;
            dependencies = module.ProofDependencies;
            if (text == null) { unrecorded = "translation not snapshotted or reported diagnostics"; }
          } else {
            // The translator's reports are batched, as for an in-process cache miss, to learn
            // whether this translation reported anything; only a silent one is recorded.
            var reporter = new BatchErrorReporter(program.Options);
            var snapshot = new BoogieGenerator(reporter, dependencies, translatorFlags).DoTranslationForSnapshot(program, moduleDefinition);
            foreach (var diagnostic in reporter.AllMessages) { errorReporter.MessageCore(diagnostic); }
            var copied = BoogieSnapshotCloner.TryClone(snapshot, out var consumer, out var cloneFailure);
            graph = copied ? consumer! : snapshot;
            // Translator hints (Info) are printed only with tooltips, which every key includes; a
            // translation whose only reports are hints that this run does not print is silent.
            var reported = reporter.AllMessages.Where(m => m.Level != ErrorLevel.Info || program.Options.PrintTooltips).ToList();
            text = copied && reported.Count == 0 ? ModuleTranslationCache.Emit(snapshot, program.Options) : null;
            unrecorded = !copied ? "unsupported graph: " + cloneFailure
              : reported.Count != 0 ? "translation diagnostics (" + reported[0].Level + ")" : null;
          }
          // The unprepared graph: preparation mutates it.
          string? unfingerprinted = null;
          var locations = Caching.SourceLocations.For(program);
          var fingerprint = text != null
            ? Caching.BoogieGraphFingerprint.TryCompute(graph, program.Options.EmitDebugInformation, locations, out unfingerprinted) : null;
          // The stored text names source files by their content, as the fingerprint does.
          text = text != null ? locations.Relabel(text) : null;
          unrecorded ??= unfingerprinted != null ? "unsupported graph: " + unfingerprinted : null;
          if (unrecorded != null) { persistent!.Unrecorded(unrecorded); }
          identity = identity with {
            BoogieHash = text != null ? Caching.CacheDigest.Blob(System.Text.Encoding.UTF8.GetBytes(text)) : null,
            Fingerprint = fingerprint, Text = fingerprint != null ? text : null
          };
          if (profile != null) {
            profile.Span("translate", profileStart!.Value, new Dictionary<string, object?> {
              ["module"] = moduleName, ["cached"] = reused, ["persistent"] = identity.Text != null, ["unrecorded"] = unrecorded,
              ["threadAllocBytes"] = GC.GetAllocatedBytesForCurrentThread() - persistentAllocated,
              ["declarations"] = graph.TopLevelDeclarations.Count(), ["implementations"] = graph.Implementations.Count()
            });
          }
          return graph;
        }
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

      if (persistent != null && identity is { Text: not null }) {
        persistent.PublishTranslation(identity, errorReporter, moduleDefinition.Origin);
        string? missReason = "audit";
        if (!persistent.Audit &&
            persistent.TryReplay(program, moduleDefinition, identity.Fingerprint!, false, out missReason) is { } replayed) {
          profile?.Point("persistent-module", new Dictionary<string, object?> {
            ["module"] = moduleName, ["hit"] = "boogie-graph", ["tasks"] = replayed.Count
          });
          return new ModuleVerificationPreparation(
            VerificationPreparation.Ready(replayed.Select(task => new BoogieVerificationWork(task))), dependencies);
        }
        profile?.Point("persistent-module", new Dictionary<string, object?> {
          ["module"] = moduleName, ["hit"] = null, ["reason"] = missReason
        });
      }

      profileStart = profile?.Begin();
      var preparation = await createBackend().PrepareAsync(boogieProgram, cancellationToken);
      if (persistent != null && identity is { Text: not null } && preparation.IsReady &&
          preparation.GetWorkOrThrow().All(work => work is BoogieVerificationWork)) {
        var recorded = persistent.Record(program, moduleDefinition, identity, BoogieVerificationBackend.GetNativeTasks(preparation));
        preparation = VerificationPreparation.Ready(recorded.Select(task => new BoogieVerificationWork(task)));
      }
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
