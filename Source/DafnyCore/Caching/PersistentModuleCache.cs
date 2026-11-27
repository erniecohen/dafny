#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Boogie;
using VC;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny.Caching;

/// <summary>
/// The persistent translation and module-result tables of --persistent-translation, kept in the
/// --verification-cache directory beside the task results.
///
/// The translation table maps a module's translation key, the persistent form of the in-process
/// descriptor of <see cref="ModuleTranslationCache"/> (its resolution key, the source of its
/// dependency closure, the prelude, the effective options and the Dafny and Boogie builds), to the
/// SHA-256 of the module's printed Boogie program, whose text is stored under that hash, and to the
/// structural fingerprint of the Boogie graph (<see cref="BoogieGraphFingerprint"/>).
///
/// The module-result table maps a fingerprint, under one verification environment (the options,
/// the audited solver and toolchain, and the Dafny and Boogie builds), to the result of each of the
/// module's tasks. A manifest is published only when every task the module schedules completed
/// Valid or resource-bounded OutOfResource through the task-result cache, and the module's translation reported nothing. Its tasks are
/// then replayed, flagged as replayed, without translation, preparation, VC generation or a solver.
///
/// Translation is never reloaded from disk: a stored Boogie text is a record of what was
/// translated, never an input. A dependency change that leaves a module's Boogie program unchanged
/// retranslates that module, finds its fingerprint, and replays its results (early cutoff).
/// </summary>
public sealed class PersistentModuleCache {
  private const int Schema = 1;
  private const int ModuleResultSchema = 3;
  private static readonly ConditionalWeakTable<DafnyOptions, PersistentModuleCache> Sessions = new();
  private readonly DafnyOptions options;
  private readonly TaskCacheSession session;
  private readonly Lazy<(string? Key, string? Reason)> environment;
  private long keyHits, graphHits, misses, translationPublications, manifestPublications, bypasses, auditMismatches, auditChecks;
  public long KeyHits => Interlocked.Read(ref keyHits);
  public long GraphHits => Interlocked.Read(ref graphHits);
  public long Misses => Interlocked.Read(ref misses);
  public long TranslationPublications => Interlocked.Read(ref translationPublications);
  public long ManifestPublications => Interlocked.Read(ref manifestPublications);
  public long Bypasses => Interlocked.Read(ref bypasses);
  public long AuditMismatches => Interlocked.Read(ref auditMismatches);
  public long AuditChecks => Interlocked.Read(ref auditChecks);
  public string? LastBypassReason { get; private set; }
  private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> unrecorded = new();
  /// <summary>Why fresh modules were not recorded, by reason, with a count of each.</summary>
  public IReadOnlyDictionary<string, int> UnrecordedReasons => new SortedDictionary<string, int>(unrecorded, StringComparer.Ordinal);
  internal void Unrecorded(string reason) => unrecorded.AddOrUpdate(reason, 1, (_, n) => n + 1);
  public bool Audit { get; }

  private ContentAddressedCache Store => session.Store;
  // The large tier of the same root, where translations and their Boogie text are kept.
  private readonly ArtifactStore? artifacts;

  private PersistentModuleCache(DafnyOptions options, TaskCacheSession session) {
    this.options = options;
    this.session = session;
    Audit = options.Get(CommonOptionBag.PersistentTranslationAudit);
    artifacts = ArtifactStore.For(options);
    environment = new Lazy<(string?, string?)>(ComputeEnvironment, LazyThreadSafetyMode.ExecutionAndPublication);
  }

  public static PersistentModuleCache? TryFor(DafnyOptions options) => Sessions.TryGetValue(options, out var value) ? value : null;

  public static PersistentModuleCache? For(DafnyOptions options) {
    if (!options.Get(CommonOptionBag.PersistentTranslation) || TaskCacheSession.For(options) is not { } session) { return null; }
    return Sessions.GetValue(options, o => new PersistentModuleCache(o, session));
  }

  internal sealed record TranslationRecord(int Schema, string BoogieHash, string Fingerprint);
  internal sealed record SymbolEntry(string Type, string Name, string File, int StartLine, int StartColumn, int EndLine, int EndColumn);
  internal sealed record TaskLocation(int Line, int Column, int Offset);
  internal sealed record TaskEntry(SymbolEntry Symbol, bool Reverified, string ScopeId, int Split, int Seed, bool Scheduled,
    int VcNum, int MaxCounterExamples, int ResourceCount, int Asserts, long RunTimeTicks, string? ExecutionKey,
    List<string>? Hidden = null, SolverOutcome Outcome = SolverOutcome.Valid, uint TimeLimit = 0, uint ResourceLimit = 0,
    TaskLocation? Diagnostic = null, List<StoredAssertionReport>? ReportAssertions = null);
  internal sealed record ModuleManifest(int Schema, string Module, string BoogieHash, string Fingerprint, List<TaskEntry> Tasks);

  /// <summary>A module's translation as the persistent tables see it, or why they cannot record it.</summary>
  internal sealed record ModuleIdentity(string Key, string? BoogieHash, string? Fingerprint, string? Text);

  /// <summary>
  /// Why no module of this compilation may be replayed: a requested output that only a fresh
  /// translation, preparation or run reproduces. Recording is unaffected.
  /// </summary>
  internal string? ReplayBypass() {
    // Ordinary JSON consumes assertion locations/descriptions in addition to the CSV fields.
    // Dependency reporting and the remaining loggers still require fresh preparation.
    if (options.Get(CommonOptionBag.VerificationLogFormat).Any(format =>
          format != "csv" && !format.StartsWith("csv;", StringComparison.Ordinal) &&
          format != "json" && !format.StartsWith("json;", StringComparison.Ordinal))) { return "verification logs"; }
    if (options.Get(CommonOptionBag.ProgressOption) == CommonOptionBag.ProgressLevel.Batch) { return "per-task progress"; }
    if (options.TrackVerificationCoverage || options.Get(CommonOptionBag.VerificationCoverageReport) != null ||
        options.Get(CommonOptionBag.WarnContradictoryAssumptions) || options.Get(CommonOptionBag.WarnRedundantAssumptions) ||
        options.Get(CommonOptionBag.SuggestProofRefactoring)) { return "proof-dependency reporting"; }
    if (options.ExtractCounterexample) { return "counterexample extraction"; }
    if (options.PrintFile != null || options.PrintPassiveFile != null || options.PrintSplitFile != null) { return "Boogie printing"; }
    if (options.ProverLogFilePath != null) { return "solver logs"; }
    if (options.VerifySnapshots > 0) { return "Boogie snapshots"; }
    foreach (var (option, value) in options.Options.OptionArguments) {
      if (option.Name is "filter-position" && value is string { Length: > 0 }) { return "position filter"; }
      if (option.Name is "filter-position-batch" && value is IEnumerable<string> batch && batch.Any()) { return "position filter"; }
    }
    return null;
  }

  /// <summary>The module's translation key, or null with a reason when the module is translated afresh.</summary>
  internal string? Key(Program program, ModuleDefinition module, BoogieGenerator.TranslatorFlags flags) {
    if (ModuleTranslationCache.BypassReason(program, module, flags) is { } reason) {
      Bypass(reason);
      return null;
    }
    var names = module.TranslationInputs!.ModuleNames;
    if (module.ResolutionIdentity == null ||
        program.RawModules().Any(m => names.Contains(m.FullName) && m.ResolutionIdentity == null)) {
      Bypass("a module without a resolution identity");
      return null;
    }
    return ModuleTranslationCache.PersistentDescriptor(program, module, flags);
  }

  /// <summary>The key of this run's module-results environment, or null when results are not recorded.</summary>
  internal string? EnvironmentDigest => environment.Value.Item1;

  private string? EnvironmentKey(out string? reason) {
    var (key, why) = environment.Value;
    reason = why;
    return key;
  }

  private (string?, string?) ComputeEnvironment() {
    var identity = session.EnvironmentIdentity(out var reason);
    if (identity == null) { return (null, reason ?? "unsupported execution environment"); }
    var parts = new List<string> {
      typeof(BoogieGenerator).Module.ModuleVersionId.ToString(), typeof(Bpl.Program).Module.ModuleVersionId.ToString()
    };
    parts.AddRange(PersistentCacheOptions.EnvironmentParts(options));
    var components = new List<ReadOnlyMemory<byte>> { identity };
    components.AddRange(parts.Select(p => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(p)));
    return (CacheDigest.Compute("module-results-environment-v1", components.ToArray()), null);
  }

  // The graph fingerprint determines the Boogie program; its text, which also records the source
  // coordinates the fingerprint encodes relatively, is not part of the key.
  private static string ManifestKey(string fingerprint, string environment) =>
    CacheDigest.Compute("module-results-v4", Encoding.UTF8.GetBytes(fingerprint), Encoding.UTF8.GetBytes(environment));

  /// <summary>The stored translation of <paramref name="key"/>, if any.</summary>
  internal TranslationRecord? ReadTranslation(string key) {
    if (artifacts == null || Store.IsQuarantined(CacheTable.Translation, key)) { return null; }
    var bytes = artifacts.TryRead(ArtifactTable.Translations, key);
    if (bytes == null) { return null; }
    try {
      var record = JsonSerializer.Deserialize<TranslationRecord>(bytes);
      if (record is not { Schema: Schema } || !CacheDigest.IsValid(record.BoogieHash) || !CacheDigest.IsValid(record.Fingerprint)) {
        return null;
      }
      // The text it names is not read, but is in use as long as the record is.
      artifacts.Touch(ArtifactTable.Translations, TextKey(record.BoogieHash));
      return record;
    } catch (JsonException) { return null; }
  }

  // The large-tier key of the Boogie text whose hash is boogieHash, apart from every translation key.
  private static string TextKey(string boogieHash) => CacheDigest.Compute("boogie-text-v1", Encoding.UTF8.GetBytes(boogieHash));

  /// <summary>
  /// The fresh translation <paramref name="identity"/>, recorded under its key with its text. In
  /// audit mode, a stored record whose graph differs is reported.
  /// </summary>
  /// <remarks>
  /// A key is a resolution identity, which leaves out where the source is (another checkout, lines
  /// inserted above), so translations under one key may differ in the source positions and paths
  /// that their Boogie texts record. Only the graph fingerprint, which records them relatively and
  /// is all that a hit replays by, must agree; the text stored is that of the first translation.
  /// </remarks>
  internal void PublishTranslation(ModuleIdentity identity, ErrorReporter reporter, IOrigin origin) {
    if (identity.Text == null || identity.BoogieHash == null || identity.Fingerprint == null) { return; }
    var record = new TranslationRecord(Schema, identity.BoogieHash, identity.Fingerprint);
    if (Audit && ReadTranslation(identity.Key) is { } stored) {
      Interlocked.Increment(ref auditChecks);
      if (stored.Fingerprint != record.Fingerprint) {
        AuditMismatch(reporter, origin, $"the stored translation differs from a fresh one (Boogie {stored.BoogieHash} " +
          $"and graph {stored.Fingerprint}, fresh Boogie {record.BoogieHash} and graph {record.Fingerprint})");
      }
      // A second, different publication would disable the key: the first stands.
      return;
    }
    if (artifacts == null || Store.IsQuarantined(CacheTable.Translation, identity.Key)) { return; }
    var text = Encoding.UTF8.GetBytes(identity.Text);
    if (CacheDigest.Blob(text) != identity.BoogieHash ||
        artifacts.TryPublish(ArtifactTable.Translations, TextKey(identity.BoogieHash), BlobEncoding.Encode(text)) == CachePublication.Unavailable) {
      return;
    }
    switch (artifacts.TryPublish(ArtifactTable.Translations, identity.Key, JsonSerializer.SerializeToUtf8Bytes(record))) {
      case CachePublication.Stored: Interlocked.Increment(ref translationPublications); break;
      case CachePublication.AlreadyPresent when ReadTranslation(identity.Key) is { } present && present.Fingerprint != record.Fingerprint:
        // Two translations under one key: neither is used again, and the key is never published again.
        Store.Quarantine(CacheTable.Translation, identity.Key);
        if (Audit) { AuditMismatch(reporter, origin, "two translations were published under one key"); }
        break;
    }
  }

  /// <summary>The stored Boogie text of a module translation, by its hash.</summary>
  internal string? ReadBoogieText(string boogieHash) {
    if (artifacts?.TryRead(ArtifactTable.Translations, TextKey(boogieHash)) is not { } stored) { return null; }
    // Stored compressed; returned only if it is the text of that hash.
    var text = BlobEncoding.TryDecode(stored, 64 * 1024 * 1024) ?? stored;
    return CacheDigest.Blob(text) == boogieHash ? Encoding.UTF8.GetString(text) : null;
  }

  private ModuleManifest? ReadManifest(string fingerprint, out string? reason) {
    if (EnvironmentKey(out reason) is not { } environmentKey) { return null; }
    var bytes = Store.TryRead(CacheTable.ModuleResult, ManifestKey(fingerprint, environmentKey));
    if (bytes == null) { reason = "no stored module result"; return null; }
    try {
      var manifest = JsonSerializer.Deserialize<ModuleManifest>(bytes);
      if (manifest is { Schema: ModuleResultSchema } && manifest.Fingerprint == fingerprint && manifest.Tasks != null &&
          manifest.Tasks.All(entry => ReplayableOutcome(entry.Outcome, entry.TimeLimit, entry.ResourceLimit) &&
            (entry.Outcome == SolverOutcome.Valid || entry.Diagnostic != null))) {
        return manifest;
      }
    } catch (JsonException) { }
    reason = "unreadable module result";
    return null;
  }

  /// <summary>
  /// The tasks of <paramref name="module"/> replayed from the results stored for its translation
  /// <paramref name="boogieHash"/>/<paramref name="fingerprint"/>, or null.
  /// </summary>
  internal IReadOnlyList<IVerificationTask>? TryReplay(Program program, ModuleDefinition module, string fingerprint,
    bool byKey, out string? missReason) {
    missReason = null;
    if (ReplayBypass() is { } bypass) { Bypass(bypass); missReason = bypass; return null; }
    var manifest = ReadManifest(fingerprint, out var reason);
    if (manifest == null) { Interlocked.Increment(ref misses); LastBypassReason = missReason = reason; return null; }
    var locations = SourceLocations.For(program);
    var json = options.Get(CommonOptionBag.VerificationLogFormat).Any(format =>
      format == "json" || format.StartsWith("json;", StringComparison.Ordinal));
    var symbols = SymbolExtensions.GetSymbolDescendants(program.DefaultModule).OfType<ICanVerify>()
      .GroupBy(locations.Symbol).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
    var functions = symbols.Values.OfType<Function>().GroupBy(f => f.FullDafnyName).Where(g => g.Count() == 1)
      .ToDictionary(g => g.Key, g => g.Single());
    var tasks = new List<IVerificationTask>(manifest.Tasks.Count);
    // Reporting groups assertion batches by scope-token identity, just as fresh tasks do.
    var scopes = new Dictionary<(SymbolEntry Symbol, bool Reverified), CanVerifyOrigin>();
    foreach (var entry in manifest.Tasks) {
      var hidden = new List<Function>();
      if (!symbols.TryGetValue(entry.Symbol, out var canVerify) ||
          (entry.Hidden ?? []).Any(name => { if (functions.TryGetValue(name, out var f)) { hidden.Add(f); return false; } return true; })) {
        Interlocked.Increment(ref misses); LastBypassReason = missReason = "a stored task names no current symbol";
        return null;
      }
      var scope = (entry.Symbol, entry.Reverified);
      if (!scopes.TryGetValue(scope, out var scopeToken)) {
        scopes.Add(scope, scopeToken = new CanVerifyOrigin(canVerify) { IsReverifiedInheritedCheck = entry.Reverified });
      }
      IToken token = canVerify.Origin;
      if (entry.Diagnostic is { } diagnostic) {
        var current = canVerify.StartToken;
        var position = current.pos + diagnostic.Offset;
        while (current != null && current.pos < position && current != canVerify.EndToken) { current = current.Next; }
        if (current == null || current.pos != position || current.line != canVerify.StartToken.line + diagnostic.Line ||
            current.col != diagnostic.Column) {
          Interlocked.Increment(ref misses); LastBypassReason = missReason = "a stored diagnostic names no current token";
          return null;
        }
        token = current;
      }
      var assertions = json && entry.Scheduled ? PersistentAssertionReports.Restore(locations, entry.ReportAssertions, entry.Asserts) : null;
      if (json && entry.Scheduled && assertions == null) {
        Interlocked.Increment(ref misses); LastBypassReason = missReason = "a stored task has no current assertion report";
        return null;
      }
      tasks.Add(new PersistedTask(canVerify, scopeToken, entry, hidden, token, assertions));
    }
    Interlocked.Increment(ref byKey ? ref keyHits : ref graphHits);
    return tasks;
  }

  /// <summary>
  /// The prepared tasks of a fresh module, observed so that its results are recorded once every
  /// task the module schedules has completed Valid through the task-result cache. In audit mode,
  /// those results are compared with any stored ones.
  /// </summary>
  internal IReadOnlyList<IVerificationTask> Record(Program program, ModuleDefinition module, ModuleIdentity identity,
    IReadOnlyList<IVerificationTask> tasks) {
    if (identity.Text == null || identity.Fingerprint == null || identity.BoogieHash == null) { return tasks; }
    if (EnvironmentKey(out var reason) is not { } environmentKey) { Bypass(reason ?? "unsupported execution environment"); return tasks; }
    var recorder = new ModuleRecorder(this, program, module, identity, environmentKey, tasks.Count);
    var recording = tasks.Select((task, index) => (IVerificationTask)new RecordingTask(task, recorder, index)).ToList();
    recorder.Start(); // A module of no scheduled tasks is complete now.
    return recording;
  }

  private void Bypass(string reason) {
    Interlocked.Increment(ref bypasses);
    LastBypassReason = reason;
  }

  private void AuditMismatch(ErrorReporter reporter, IOrigin origin, string message) {
    Interlocked.Increment(ref auditMismatches);
    reporter.Error(MessageSource.Verifier, ResolutionErrors.ErrorId.none, origin, "persistent translation audit: " + message);
  }


  // Failed interactions are deterministic only under a resource bound, without a time bound.
  // Counterexamples require a richer current-report snapshot and remain on the task path.
  internal static bool ReplayableOutcome(SolverOutcome outcome, uint timeLimit, uint resourceLimit, int counterexamples = 0) =>
    counterexamples == 0 && (outcome == SolverOutcome.Valid ||
    outcome == SolverOutcome.OutOfResource && timeLimit == 0 && resourceLimit > 0);

  /// <summary>Whether Compilation runs this task: a copy from a refinement base runs only its own checks.</summary>
  internal static bool Scheduled(IVerificationTask task) {
    var origin = (CanVerifyOrigin)task.ScopeToken;
    return !origin.CanVerify.Origin.IsCopy || origin.IsReverifiedInheritedCheck;
  }

  private sealed class ModuleRecorder(PersistentModuleCache cache, Program program, ModuleDefinition module,
    ModuleIdentity identity, string environmentKey, int count) {
    private readonly object gate = new();
    private readonly TaskEntry?[] entries = new TaskEntry?[count];
    private int recorded;
    private bool failed;

    public void Complete(int index, IVerificationTask task, Completed completed) {
      TaskEntry? entry = null;
      var result = completed.Result;
      var origin = (CanVerifyOrigin)task.ScopeToken;
      // The functions the task hides are reported by name (Compilation's "hidden functions" hint),
      // which names only the hidden Boogie functions of Dafny functions.
      var hidden = VerificationTaskFacts.HiddenFunctions(task).ToList();
      var timeLimit = task.Split.Implementation.GetTimeLimit(cache.options);
      var resourceLimit = task.Split.Implementation.GetResourceLimit(cache.options);
      TaskLocation? diagnostic = null;
      if (result.Outcome == SolverOutcome.OutOfResource) {
        var token = BoogieGenerator.ToDafnyToken(task.Token).ReportingRange.StartToken;
        var start = origin.CanVerify.StartToken;
        if (token.Uri == start.Uri && token.pos >= start.pos && token.pos <= origin.CanVerify.EndToken.pos) {
          diagnostic = new TaskLocation(token.line - start.line, token.col, token.pos - start.pos);
        }
      }
      if (ReplayableOutcome(result.Outcome, timeLimit, resourceLimit, result.CounterExamples.Count) &&
          (result.Outcome == SolverOutcome.Valid || diagnostic != null) &&
          task is IReplayedVerificationTask { ExecutionKey: { } executionKey }) {
        entry = new TaskEntry(SourceLocations.For(program).Symbol(origin.CanVerify), origin.IsReverifiedInheritedCheck, task.ScopeId, task.Split.SplitIndex,
          task.Split.RandomSeed, true, result.VcNum, result.MaxCounterExamples, result.ResourceCount, result.Asserts.Count,
          result.RunTime.Ticks, executionKey, hidden.Count == 0 ? null : hidden.Select(f => f.FullDafnyName).Distinct().Order(StringComparer.Ordinal).ToList(),
          result.Outcome, timeLimit, resourceLimit, diagnostic,
          PersistentAssertionReports.Capture(SourceLocations.For(program), result));
      }
      string? reason = entry != null ? null
        : result.Outcome != SolverOutcome.Valid ? "a task that was not valid"
        : "a task the result cache did not key";
      lock (gate) {
        if (failed || entries[index] != null) { return; }
        if (entry == null) { failed = true; cache.Unrecorded(reason!); return; }
        entries[index] = entry;
        recorded++;
      }
      TryFinish();
    }

    public void Unscheduled(int index, IVerificationTask task) {
      var origin = (CanVerifyOrigin)task.ScopeToken;
      var entry = new TaskEntry(SourceLocations.For(program).Symbol(origin.CanVerify), origin.IsReverifiedInheritedCheck, task.ScopeId, task.Split.SplitIndex,
        task.Split.RandomSeed, false, 0, 0, 0, 0, 0, null);
      lock (gate) {
        if (entries[index] != null) { return; }
        entries[index] = entry;
        recorded++;
      }
      TryFinish();
    }

    public void Fail(string reason) {
      lock (gate) {
        if (failed) { return; }
        failed = true;
      }
      cache.Unrecorded(reason);
    }

    public void Start() => TryFinish();

    private void TryFinish() {
      List<TaskEntry> tasks;
      lock (gate) {
        if (failed || recorded != entries.Length) { return; }
        failed = true; // Publish once.
        tasks = entries.Select(e => e!).ToList();
      }
      var manifest = new ModuleManifest(ModuleResultSchema, module.FullName, identity.BoogieHash!, identity.Fingerprint!, tasks);
      var key = ManifestKey(identity.Fingerprint!, environmentKey);
      if (cache.Store.TryRead(CacheTable.ModuleResult, key) is { } storedBytes) {
        // Stored results differ from fresh ones at least in their wall-clock time, and a second,
        // different publication would disable the key: the first stands.
        if (cache.Audit) {
          Interlocked.Increment(ref cache.auditChecks);
          ModuleManifest? stored = null;
          try { stored = JsonSerializer.Deserialize<ModuleManifest>(storedBytes); } catch (JsonException) { }
          if (stored == null || !Same(stored, manifest)) {
            cache.AuditMismatch(program.Reporter, module.Origin, $"the stored results of module {module.FullName} differ from fresh ones");
          }
        }
        return;
      }
      if (cache.Store.TryPublish(CacheTable.ModuleResult, key, JsonSerializer.SerializeToUtf8Bytes(manifest)) == CachePublication.Stored) {
        Interlocked.Increment(ref cache.manifestPublications);
      }
    }

    // Wall-clock time is not reproducible; every other recorded field is.
    private static bool Same(ModuleManifest stored, ModuleManifest fresh) =>
      stored.Schema == fresh.Schema && stored.Fingerprint == fresh.Fingerprint &&
      stored.Tasks.Count == fresh.Tasks.Count &&
      stored.Tasks.Zip(fresh.Tasks).All(p =>
        // Record equality compares lists by reference; compare hidden functions and report assertions by content.
        p.First with { RunTimeTicks = 0, Hidden = null, ReportAssertions = null } == p.Second with { RunTimeTicks = 0, Hidden = null, ReportAssertions = null } &&
        (p.First.Hidden ?? []).SequenceEqual(p.Second.Hidden ?? [], StringComparer.Ordinal) &&
        ((p.First.ReportAssertions == null && p.Second.ReportAssertions == null) ||
          (p.First.ReportAssertions != null && p.Second.ReportAssertions != null && p.First.ReportAssertions.SequenceEqual(p.Second.ReportAssertions))));
  }

  /// <summary>A prepared task whose completion is recorded for its module.</summary>
  private sealed class RecordingTask : IVerificationTask, IReplayedVerificationTask {
    private readonly IVerificationTask inner;
    private readonly ModuleRecorder recorder;
    private readonly int index;

    public RecordingTask(IVerificationTask inner, ModuleRecorder recorder, int index) {
      this.inner = inner;
      this.recorder = recorder;
      this.index = index;
      if (!Scheduled(inner)) { recorder.Unscheduled(index, inner); }
    }

    public bool IsReplayed => inner is IReplayedVerificationTask { IsReplayed: true };
    public string? ExecutionKey => (inner as IReplayedVerificationTask)?.ExecutionKey;
    // A reseeded run is not the task that was prepared; it is neither recorded nor replayed.
    public IVerificationTask FromSeed(int newSeed) { recorder.Fail("a reseeded task"); return inner.FromSeed(newSeed); }
    public IVerificationStatus CacheStatus => inner.CacheStatus;
    public ManualSplit Split => inner.Split;
    public IToken ScopeToken => inner.ScopeToken;
    public string ScopeId => inner.ScopeId;
    public IToken Token => inner.Token;
    public bool IsIdle => inner.IsIdle;
    public void Cancel() => inner.Cancel();

    public IObservable<IVerificationStatus>? TryRun() {
      var updates = inner.TryRun();
      if (updates == null) {
        if (inner.CacheStatus is Completed cached) { recorder.Complete(index, this, cached); }
        return null;
      }
      return updates.Do(update => {
        if (update is Completed completed) { recorder.Complete(index, this, completed); }
      }, _ => recorder.Fail("a task that failed to run"));
    }
  }

  /// <summary>
  /// A task of a module replayed from the module-result table. It has no Boogie split: only the
  /// results that the replay-eligible reports read are available.
  /// </summary>
  internal sealed class PersistedTask : IVerificationTask, IReplayedVerificationTask {
    private static readonly List<AssertCmd> NoAsserts = new();
    private readonly TaskEntry entry;
    private readonly Completed? completed;

    public PersistedTask(ICanVerify canVerify, CanVerifyOrigin scopeToken, TaskEntry entry, IReadOnlyList<Function> hiddenFunctions,
      IToken? token = null, IReadOnlyList<VerificationAssertionReport>? reportAssertions = null) {
      this.entry = entry;
      HiddenFunctions = hiddenFunctions;
      ScopeToken = scopeToken;
      Token = token ?? canVerify.Origin;
      ReportAssertions = reportAssertions;
      if (entry.Scheduled) {
        // The result supplies assertion counts; ordinary JSON reads the separately restored report metadata.
        var placeholder = new AssertCmd(Bpl.Token.NoToken, Expr.True);
        var asserts = entry.Asserts == 0 ? NoAsserts : Enumerable.Repeat(placeholder, entry.Asserts).ToList();
        completed = new Completed(new VerificationRunResult(entry.VcNum, 0, DateTime.MinValue, entry.Outcome,
          TimeSpan.FromTicks(entry.RunTimeTicks), entry.MaxCounterExamples, new List<Counterexample>(), asserts,
          Array.Empty<TrackedNodeComponent>(), entry.ResourceCount, Microsoft.Boogie.SMTLib.SolverKind.Z3, Array.Empty<Bpl.Declaration>()));
      }
    }

    public bool IsReplayed => true;
    public string? ExecutionKey => entry.ExecutionKey;
    public int SplitIndex => entry.Split;
    public int RandomSeed => entry.Seed;
    public uint TimeLimit => entry.TimeLimit;
    public IReadOnlyList<VerificationAssertionReport>? ReportAssertions { get; }
    public IReadOnlyList<Function> HiddenFunctions { get; }
    public IVerificationTask FromSeed(int newSeed) =>
      throw new NotSupportedException("a task replayed from a stored module result cannot be reseeded");
    public IVerificationStatus CacheStatus => completed ?? (IVerificationStatus)new Stale();
    public ManualSplit Split => throw new InvalidOperationException("a task replayed from a stored module result has no Boogie split");
    public IToken ScopeToken { get; }
    public string ScopeId => entry.ScopeId;
    public IToken Token { get; }
    public bool IsIdle => true;
    public void Cancel() { }

    public IObservable<IVerificationStatus>? TryRun() => completed != null ? null
      : throw new InvalidOperationException("a task that its module does not schedule was run");
  }
}

/// <summary>Task facts that a task replayed from a stored module result supplies without a Boogie split.</summary>
public static class VerificationTaskFacts {
  public static IReadOnlyList<VerificationAssertionReport>? AssertionReport(IVerificationTask? task) =>
    task is PersistentModuleCache.PersistedTask persisted ? persisted.ReportAssertions : null;

  public static int SplitIndex(IVerificationTask task) =>
    task is PersistentModuleCache.PersistedTask persisted ? persisted.SplitIndex : task.Split.SplitIndex;

  public static bool HasSplit(IVerificationTask task) => task is not PersistentModuleCache.PersistedTask;

  /// <summary>The Dafny functions whose bodies the task hides.</summary>
  public static IEnumerable<Function> HiddenFunctions(IVerificationTask task) =>
    task is PersistentModuleCache.PersistedTask persisted ? persisted.HiddenFunctions
      : task.Split.HiddenFunctions.Select(f => f.tok).OfType<FromDafnyNode>().Select(n => n.Node).OfType<Function>();

  public static uint TimeLimit(IVerificationTask task, DafnyOptions options) =>
    task is PersistentModuleCache.PersistedTask persisted ? persisted.TimeLimit : task.Split.Implementation.GetTimeLimit(options);

  public static int RandomSeed(IVerificationTask task) =>
    task is PersistentModuleCache.PersistedTask persisted ? persisted.RandomSeed : task.Split.RandomSeed;

  public static IToken SplitToken(IVerificationTask task) =>
    task is PersistentModuleCache.PersistedTask persisted ? persisted.Token : task.Split.Token;
}
