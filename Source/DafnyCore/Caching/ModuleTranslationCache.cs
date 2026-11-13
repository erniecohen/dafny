#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DafnyCore.Verifier;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

/// <summary>
/// Process-local translation snapshots over ModuleDefinition.ResolutionKey;
/// the namespace additionally checks prelude, effective flags/options and source bindings.
/// No verification result or current whole-program admission is cached.
/// </summary>
public sealed class ModuleTranslationCache {
  private sealed record Snapshot(string ModuleKey, string? ObservableInterfaceHash, string BoogieHash, string Text,
    Bpl.Program Template, Dictionary<string, ProofDependency> Dependencies,
    Dictionary<string, (Declaration Decl, HashSet<ProofDependency> Deps)> Members,
    DafnyDiagnostic[] Diagnostics);
  private readonly Dictionary<string, LinkedListNode<(string Key, Snapshot Value)>> entries = new(StringComparer.Ordinal);
  private readonly Dictionary<string, string> texts = new(StringComparer.Ordinal);
  private readonly LinkedList<(string Key, Snapshot Value)> lru = new();
  private readonly object gate = new();
  private long textBytes;
  private readonly long maximumTextBytes;
  private readonly int maximumEntries;
  public static ModuleTranslationCache ProcessCache => ProcessHolder.Instance;
  private static class ProcessHolder { internal static readonly ModuleTranslationCache Instance = new(); }
  public long Hits { get; private set; }
  public long Translations { get; private set; }
  public long Bypasses { get; private set; }
  public string? LastBypassReason { get; private set; }
  public int EntryCount { get { lock (gate) { return entries.Count; } } }
  public long RetainedTextBytes { get { lock (gate) { return textBytes; } } }

  public ModuleTranslationCache(long maximumTextBytes = 512L * 1024 * 1024, int maximumEntries = 256) {
    if (maximumTextBytes <= 0 || maximumEntries <= 0) { throw new ArgumentOutOfRangeException(); }
    this.maximumTextBytes = maximumTextBytes;
    this.maximumEntries = maximumEntries;
  }

  public Bpl.Program Translate(Program program, ModuleDefinition module, BoogieGenerator.TranslatorFlags flags,
    CancellationToken cancellationToken) {
    return Translate(program, module, flags, cancellationToken, out _);
  }

  public Bpl.Program Translate(Program program, ModuleDefinition module, BoogieGenerator.TranslatorFlags flags,
    CancellationToken cancellationToken, out bool reused) {
    return TranslateModule(program, module, flags, cancellationToken, out reused).Program;
  }

  public sealed record ModuleTranslation(Bpl.Program Program, ProofDependencyManager ProofDependencies);

  public ModuleTranslation TranslateModule(Program program, ModuleDefinition module, BoogieGenerator.TranslatorFlags flags,
    CancellationToken cancellationToken, out bool reused) {
    reused = false;
    var manager = new ProofDependencyManager();
    // Even a hit must not bypass the current transaction's global soundness admission.
    BoogieGenerator.RequireCardinalityValidation(program);
    if (!program.ModuleSigs.ContainsKey(module)) {
      throw new InvalidOperationException("Translation requires a module from the current admitted program");
    }
    cancellationToken.ThrowIfCancellationRequested();
    var options = program.Options;
    if (module.ResolutionKey == null || module.TranslationInputs == null ||
        // Resolution currently exposes L, not the finalized post-global G/R view.
        // Modular termination reads downstream open-trait/dispatch facts; recompute until keyed.
        options.Get(CommonOptionBag.ModularTermination) || options.PrintWithUniqueASTIds ||
        // Model extraction consumes graph/source report state not covered by the snapshot codec.
        options.ExtractCounterexample ||
        options.AdditionalPlugins.Count != 0 || options.AdditionalPluginArguments.Count != 0 ||
        flags.ObligationLowered != null || flags.ObligationPrepared != null) {
      lock (gate) { Bypasses++; LastBypassReason = "missing module key, nonlocal names, debug identities or external callbacks"; }
      var snapshot = Fresh(program, module, flags, program.Reporter, manager);
      return new ModuleTranslation(BoogieSnapshotCloner.TryClone(snapshot, out var fresh, out _) ? fresh! : snapshot, manager);
    }
    var key = Descriptor(program, module, flags);
    lock (gate) {
      if (entries.TryGetValue(key, out var node)) {
        var symbols = SymbolExtensions.GetSymbolDescendants(program.DefaultModule).OfType<ICanVerify>()
          .GroupBy(BoogieSnapshotCloner.SymbolKey).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        if (Hash(node.Value.Value.Text) != node.Value.Value.BoogieHash ||
            !BoogieSnapshotCloner.TryClone(node.Value.Value.Template, out var graph, out _, symbols)) {
          Remove(node);
        } else {
          cancellationToken.ThrowIfCancellationRequested();
          Replay(program, manager, node.Value.Value);
          lru.Remove(node);
          lru.AddFirst(node);
          Hits++;
          reused = true;
          return new ModuleTranslation(graph!, manager);
        }
      }
      var reporter = new BatchErrorReporter(options);
      var translated = Fresh(program, module, flags, reporter, manager);
      foreach (var diagnostic in reporter.AllMessages) { program.Reporter.MessageCore(diagnostic); }
      cancellationToken.ThrowIfCancellationRequested();
      string? reason = null;
      Bpl.Program? consumer = null;
      var copied = BoogieSnapshotCloner.TryClone(translated, out consumer, out reason);
      if (reporter.HasErrors || !copied) {
        Bypasses++; LastBypassReason = reporter.HasErrors ? "translation diagnostics" : reason;
        return new ModuleTranslation(copied ? consumer! : translated, manager);
      }
      var text = Emit(translated, options);
      var snapshot = new Snapshot(module.ResolutionKey, null, Hash(text), text, translated,
        manager.ProofDependenciesById.ToDictionary(),
        manager.idsByMemberName.ToDictionary(p => p.Key, p => (p.Value.Decl, p.Value.Deps.ToHashSet())),
        reporter.AllMessages.ToArray());
      // The retained raw snapshot is never prepared. Default, cold and warm
      // consumers all perform one materialization through the same graph copier.
      if (Encoding.UTF8.GetByteCount(text) <= maximumTextBytes) {
        var added = lru.AddFirst((key, snapshot));
        entries.Add(key, added);
        texts.TryAdd(snapshot.BoogieHash, text);
        textBytes += Encoding.UTF8.GetByteCount(text);
        while (textBytes > maximumTextBytes || entries.Count > maximumEntries) { Remove(lru.Last!); }
      }
      return new ModuleTranslation(consumer!, manager);
    }
  }

  private Bpl.Program Fresh(Program program, ModuleDefinition module, BoogieGenerator.TranslatorFlags flags, ErrorReporter reporter,
    ProofDependencyManager manager) {
    lock (gate) { Translations++; }
    return new BoogieGenerator(reporter, manager, flags).DoTranslationForSnapshot(program, module);
  }

  public bool TryGetBoogieText(string hash, out string? text) {
    lock (gate) { return texts.TryGetValue(hash, out text); }
  }

  public void Clear() {
    lock (gate) { entries.Clear(); texts.Clear(); lru.Clear(); textBytes = 0; }
  }

  private void Remove(LinkedListNode<(string Key, Snapshot Value)> node) {
    entries.Remove(node.Value.Key);
    lru.Remove(node);
    textBytes -= Encoding.UTF8.GetByteCount(node.Value.Value.Text);
    if (!entries.Values.Any(n => n.Value.Value.BoogieHash == node.Value.Value.BoogieHash)) { texts.Remove(node.Value.Value.BoogieHash); }
  }

  private static void Replay(Program program, ProofDependencyManager manager, Snapshot snapshot) {
    lock (manager.SyncRoot) {
      foreach (var dependency in snapshot.Dependencies) { manager.ProofDependenciesById[dependency.Key] = dependency.Value; }
      foreach (var member in snapshot.Members) {
        if (!manager.idsByMemberName.TryGetValue(member.Key, out var current)) {
          manager.idsByMemberName[member.Key] = (member.Value.Decl, new HashSet<ProofDependency>(member.Value.Deps));
        } else { current.Deps.UnionWith(member.Value.Deps); }
      }
    }
    foreach (var diagnostic in snapshot.Diagnostics) { program.Reporter.MessageCore(diagnostic); }
  }

  internal static string Emit(Bpl.Program graph, DafnyOptions options) {
    using var text = new StringWriter(CultureInfo.InvariantCulture);
    using (var writer = new Bpl.TokenTextWriter("translation-cache", text, false, options.PrettyPrint, options)) { graph.Emit(writer); }
    return text.ToString();
  }

  private static string Descriptor(Program program, ModuleDefinition module, BoogieGenerator.TranslatorFlags flags) {
    var parts = DescriptorParts(program, module, flags);
    using var bytes = new MemoryStream();
    using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
      writer.Write(parts.Count); foreach (var part in parts) { writer.Write(part); }
    }
    return Hash(bytes.ToArray());
  }

  internal static List<string> DescriptorParts(Program program, ModuleDefinition module, BoogieGenerator.TranslatorFlags flags) {
    // A resolution result can contain several generated abstract signatures. They
    // share the owner's resolution key but have distinct translated declarations.
    var parts = new List<string> { "translation-in-process-v2", module.ResolutionKey!, module.FullName,
      typeof(BoogieGenerator).Module.ModuleVersionId.ToString(), typeof(Bpl.Program).Module.ModuleVersionId.ToString(),
      flags.PreludeText ?? File.ReadAllText(BoogieGenerator.PreludePath(program.Options)),
      flags.InsertChecksums.ToString(), flags.ReportRanges.ToString(), flags.UniqueIdPrefix == null ? "null" : "string:" + flags.UniqueIdPrefix };
    // Source coordinates and captureState strings are observable in BPL/reporting but deliberately
    // absent from a semantic resolution key. Bind source files from this module's closure only.
    var names = module.TranslationInputs!.ModuleNames;
    var closure = program.RawModules().Where(m => names.Contains(m.FullName) || ReferenceEquals(m, module))
      .OrderBy(m => m.FullName, StringComparer.Ordinal).ToArray();
    var uris = closure.Select(m => m.Origin.Uri).Where(u => u != null).ToHashSet();
    parts.Add(closure.Length.ToString(CultureInfo.InvariantCulture));
    foreach (var member in closure) {
      parts.Add(member.FullName);
      parts.Add(member.Origin.Uri?.ToString() ?? "");
      parts.Add(member.StartToken.line.ToString(CultureInfo.InvariantCulture));
      parts.Add(member.StartToken.col.ToString(CultureInfo.InvariantCulture));
      parts.Add(member.EndToken.line.ToString(CultureInfo.InvariantCulture));
      parts.Add(member.EndToken.col.ToString(CultureInfo.InvariantCulture));
    }
    var files = program.Files.Where(f => uris.Contains(f.Origin.Uri)).OrderBy(f => f.Origin.Uri.ToString(), StringComparer.Ordinal).ToArray();
    parts.Add(files.Length.ToString(CultureInfo.InvariantCulture));
    foreach (var file in files) {
      parts.Add(file.Origin.Uri.ToString());
      parts.Add(file.Origin.EntireRangeWithFallback.PrintOriginal());
    }
    foreach (var member in typeof(DafnyOptions).GetMembers(BindingFlags.Public | BindingFlags.Instance).OrderBy(m => m.Name, StringComparer.Ordinal)) {
      // A cold result-cache version probe increments telemetry; it cannot affect
      // translation and must not distinguish an otherwise identical warm module.
      if (member.Name is "PrintFile" or "LogPrefix" or "FileTimestamp" or "DafnyPrintFile" or "DafnyPrintResolvedFile" or
          nameof(DafnyOptions.VerificationCacheVersionProbes)) { continue; }
      var valueType = member switch { FieldInfo f => f.FieldType, PropertyInfo p => p.PropertyType, _ => null };
      if (valueType == null || !(valueType.IsPrimitive || valueType.IsEnum || valueType == typeof(string) ||
          typeof(IEnumerable<string>).IsAssignableFrom(valueType))) { continue; }
      object? value = member switch {
        FieldInfo field => field.GetValue(program.Options),
        PropertyInfo property when property.GetIndexParameters().Length == 0 && property.CanRead => property.GetValue(program.Options),
        _ => null
      };
      if (value is string or bool or int or uint or long or ulong or double or float or Enum) {
        parts.Add(member.Name); parts.Add("scalar"); parts.Add(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
      } else if (value is IEnumerable<string> strings) {
        var items = strings.ToArray();
        parts.Add(member.Name); parts.Add("sequence"); parts.Add(items.Length.ToString(CultureInfo.InvariantCulture)); parts.AddRange(items);
      }
    }
    return parts;
  }

  private static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
  private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
