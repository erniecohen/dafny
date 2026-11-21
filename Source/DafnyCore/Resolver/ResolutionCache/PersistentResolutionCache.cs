#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Dafny.Caching;

namespace Microsoft.Dafny;

/// <summary>
/// The persistent per-module resolution cache of issue 261. It is on with a cache store (--cache-dir, its
/// project-file key or DAFNY_CACHE_DIR) and --type-system-refresh; --resolution-cache:false leaves it out.
/// Entries live in the store's <see cref="ArtifactTable.Resolutions"/> table (docs/dev/design/261-cache-store.md).
///
/// A module's entry is keyed by
/// <code>
///   K(m) = H(domain, producer, name(m), L(m without prerequisites), naming prefix(m),
///            [(role, name) of each direct prerequisite], [(name, S(q)) for each prerequisite q of the closure])
/// </code>
/// where S(q) is the structure digest of q's resolution result: the digest of its snapshot with every source
/// coordinate left out. S(q) changes only if q's resolution result changes, so an edit of q that leaves its
/// result's structure unchanged (a comment, a proof body that resolves alike) does not invalidate dependents:
/// this is early cutoff. A module whose snapshot is unsupported has no S, and neither has a dependent.
/// No path and no position is in K: L(m) is the text of the module's pieces (its source split around nested
/// module and import declarations), and every source token of the entry is stored relative to its piece, or
/// to the prerequisite result that refers to it, and rebased onto the current parse on a hit. Text that names
/// a position (a message naming a line, a string naming a source file) makes a module uncacheable.
///
/// An entry is the module's <see cref="ResolutionSnapshot"/> together with a builtin journal: which
/// builtin declarations (tuples, arrows, arrays, bitvectors, pre-type builtins) its resolution demanded.
/// A hit replays the journal into the worker's builtins view, binds the snapshot's externals (statics,
/// context objects, builtins, prerequisites' objects and source tokens), and materializes the module's
/// result. A hit is checked against the current program: an external that is missing or has another
/// type, a builtin whose structure differs, or a source token that is not where the entry puts it is a miss.
/// A miss resolves the module afresh and writes its entry, replacing one that did not apply.
/// </summary>
internal sealed class PersistentResolutionCache {
  private const string KeyDomain = "dafny-resolution-cache-key-v2";
  private const string Magic = "DRES";
  private const int FormatVersion = 2;
  private const string BuiltinTokenOwner = "builtins";

  /// <summary>The release and the build: snapshots hold field layouts, which a rebuild may change.</summary>
  public static string Producer { get; } = ModuleResolutionKey.DafnyVersion + "+" +
    ResolutionGraphSchema.Core.ManifestModule.ModuleVersionId.ToString("N");

  private readonly ArtifactStore store;
  private readonly Program program;
  private readonly SystemModuleManager initialBuiltins;
  private readonly ResolutionBuiltinCatalog catalog;
  private readonly (string Name, Dictionary<string, MemberDecl> Members)[] initialMembers;
  private readonly Lazy<ResolutionTokenIndex> sourceTokens;
  // Presized: a warm run registers every reused module's objects, and growing the table dominated its profile.
  private readonly ConcurrentDictionary<object, ResolutionExternal> owners =
    new(Environment.ProcessorCount, 1 << 16, ReferenceEqualityComparer.Instance);
  private readonly ConcurrentDictionary<uint, ResolutionExternalToken> tokenOwners = new();
  private readonly ConcurrentDictionary<string, Record> records = new(StringComparer.Ordinal);
  private readonly Dictionary<string, HashSet<uint>> ownScopeTokens = new(StringComparer.Ordinal);
  private readonly HashSet<string> ambiguousNames;
  private readonly ResolutionFrames frames;
  private readonly uint lastParseToken;

  /// <summary>
  /// A completed module: its objects, in snapshot order, its scope tokens, its structure digest (null if
  /// uncacheable), and the size of each of its collections (-1 for other objects) when it completed.
  /// </summary>
  private sealed record Record(string? Structure, object[] Owned, uint[] Tokens, int[] Sizes, Token[] SourceTokens) {
    public static readonly Record None = new(null, [], [], [], []);

    /// <summary>The rank of each source token of the module that its result refers to.</summary>
    public Dictionary<Token, int> SourceTokenRanks => sourceTokenRanks ??= SourceTokens.Select((t, i) => (t, i))
      .ToDictionary(p => p.t, p => p.i, (IEqualityComparer<Token>)ReferenceEqualityComparer.Instance);
    private Dictionary<Token, int>? sourceTokenRanks;

    /// <summary>The first collection of this module that has another size now, or null.</summary>
    public int? Changed() {
      for (var i = 0; i < Owned.Length; i++) {
        if (Sizes[i] >= 0 && Size(Owned[i]) != Sizes[i]) { return i; }
      }
      return null;
    }
  }

  /// <summary>The size of a collection that a dependent's resolution must leave alone, or -1.</summary>
  private static int Size(object value, HashSet<object>? effects = null) =>
    value is ICollection collection && effects?.Contains(value) != true ? collection.Count : -1;

  /// <summary>One module to resolve, with what its key needs.</summary>
  public sealed record ModuleContext(ModuleDecl Declaration, string LocalKey, string Prefix,
    IReadOnlyList<(string Role, ModuleDecl Prerequisite)> Direct, IReadOnlyList<ModuleDecl> Closure);

  /// <summary>A worker's mutable state, which a failed hit discards.</summary>
  public sealed record Worker(SystemModuleManager Builtins, ProgramResolver Resolver, Program Program);

  public PersistentResolutionCache(ArtifactStore store, Program program, SystemModuleManager initialBuiltins,
    ResolutionBuiltinCatalog catalog, IReadOnlyDictionary<TopLevelDeclWithMembers, Dictionary<string, MemberDecl>> initialMembers,
    IReadOnlyList<ModuleDecl> declarations, IReadOnlyList<(Token Start, Token End)[]> pieces) {
    this.store = store;
    this.program = program;
    this.initialBuiltins = initialBuiltins;
    this.catalog = catalog;
    // Every worker starts with these member tables of the builtin declarations, so they are shared, not owned.
    this.initialMembers = initialMembers.Select(p => (p.Key.FullName, p.Value)).OrderBy(p => p.FullName, StringComparer.Ordinal).ToArray();
    sourceTokens = new(() => ResolutionTokenIndex.For(program, []));
    var all = declarations.ToList();
    ambiguousNames = all.GroupBy(d => d.FullName).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
    frames = new ResolutionFrames(all.Select((d, i) => (d.FullName, pieces[i])).Where(p => !ambiguousNames.Contains(p.FullName)));
    foreach (var declaration in all) {
      // A module's own scope, and those of its export sets, which its dependents see through its result.
      if (declaration is LiteralModuleDecl literal && ResolutionGraphSchema.Tokens(literal.ModuleDef.VisibilityScope) is { Count: 1 } own) {
        var tokens = new HashSet<uint> { own.Min };
        foreach (var export in literal.ModuleDef.TopLevelDecls.OfType<ModuleExportDecl>()) {
          if (ResolutionGraphSchema.Tokens(export.ThisScope) is { Count: 1 } exportTokens) { tokens.Add(exportTokens.Min); }
        }
        ownScopeTokens[declaration.FullName] = tokens;
      }
    }
    // Scope tokens are allocated in increasing order. Those of the parsed program are at most this one.
    lastParseToken = ResolutionGraphSchema.Tokens(new VisibilityScope("resolution-cache")).Single();
  }

  // ---------------------------------------------------------------- key

  public string? Key(ModuleContext context) {
    if (ambiguousNames.Contains(context.Declaration.FullName)) { return null; }
    var structures = new List<(string, string)>();
    foreach (var prerequisite in context.Closure) {
      if (!records.TryGetValue(prerequisite.FullName, out var record) || record.Structure == null) { return null; }
      structures.Add((prerequisite.FullName, record.Structure));
    }
    using var bytes = new MemoryStream();
    using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
      writer.Write(KeyDomain);
      writer.Write(Producer);
      writer.Write(context.Declaration.FullName);
      writer.Write(context.LocalKey);
      writer.Write(context.Prefix);
      var direct = context.Direct.Select(p => (p.Role, p.Prerequisite.FullName)).Order().ToArray();
      writer.Write(direct.Length);
      foreach (var (role, name) in direct) { writer.Write(role); writer.Write(name); }
      writer.Write(structures.Count);
      foreach (var (name, structure) in structures.OrderBy(p => p.Item1, StringComparer.Ordinal)) {
        writer.Write(name);
        writer.Write(structure);
      }
    }
    return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
  }

  /// <summary>
  /// Whether a run caches resolution: with a cache store, --type-system-refresh, --resolution-cache (on by default)
  /// and no plugins, which have no snapshot contract.
  /// </summary>
  public static bool Enabled(DafnyOptions options) =>
    options.Get(CommonOptionBag.TypeSystemRefresh) && options.GetOrOptionDefault(CommonOptionBag.ResolutionCache) &&
    CacheRoot.Directory(options) != null && !options.Plugins.Except(DafnyOptions.DefaultPlugins).Any();

  // ---------------------------------------------------------------- resolve

  /// <summary>
  /// The module's output, from its entry or else from <paramref name="resolve"/>. <paramref name="state"/>
  /// says which: "hit", "stored", "uncacheable: ..." or "invalid: ..." (an entry that did not apply).
  /// </summary>
  public ModuleResolutionResult Resolve(ModuleContext context, Func<Worker> worker, Action recreate,
    Func<ModuleResolutionResult> resolve, Func<SystemModuleManager, ModuleResolutionResult, IReadOnlyList<ModuleResolutionResult>, bool> cacheable,
    IReadOnlyList<ModuleResolutionResult> closure, out string state, out string? key) {
    var name = context.Declaration.FullName;
    key = Key(context);
    if (key == null) {
      state = "uncacheable: prerequisite";
      return Uncacheable(name, resolve());
    }
    var invalid = (string?)null;
    if (store.TryRead(ArtifactTable.Resolutions, key) is { } entry) {
      try {
        var hit = Hit(context, key, entry, worker());
        state = "hit";
        if (program.Options.Get(CommonOptionBag.ResolutionCacheCheck)) { Check(context, hit, recreate, resolve, worker, closure, ref state); }
        return hit;
      } catch (Exception error) when (error is ResolutionSnapshotInvalid or ResolutionSnapshotUnsupported or IOException or EndOfStreamException
                                        or InvalidDataException or FormatException or OverflowException) {
        invalid = error.Message;
        recreate();
      }
    }
    var output = resolve();
    if (!cacheable(worker().Builtins, output, closure)) {
      state = "uncacheable: output";
      return Uncacheable(name, output);
    }
    try {
      // An entry that did not apply is replaced. The module's structure is known even if the entry is not
      // written, so dependents can still be cached.
      state = Store(context, key, output, worker(), replace: invalid != null) switch {
        CachePublication.Stored or CachePublication.AlreadyPresent => invalid == null ? "stored" : "invalid: " + invalid,
        var publication => "unwritten: " + publication
      };
    } catch (ResolutionSnapshotUnsupported error) {
      state = "uncacheable: " + error.Message;
      return Uncacheable(name, output);
    }
    return output;
  }

  /// <summary>
  /// The oracle of --resolution-cache-check: resolve the module afresh, with the same prerequisites, and compare
  /// the graph of that result with the graph the entry materialized. A difference is an error of the module.
  /// </summary>
  private void Check(ModuleContext context, ModuleResolutionResult hit, Action recreate, Func<ModuleResolutionResult> resolve,
    Func<Worker> worker, IReadOnlyList<ModuleResolutionResult> closure, ref string state) {
    recreate();
    var fresh = resolve();
    var shared = Named(context, worker()).Where(p => !p.Value.EndsWith(".worker", StringComparison.Ordinal))
      .Select(p => p.Key).ToHashSet(ReferenceEqualityComparer.Instance);
    var closureNames = context.Closure.Select(d => d.FullName).ToHashSet(StringComparer.Ordinal);
    var equality = new ResolutionGraphEquality(value => shared.Contains(value) ||
      (owners.TryGetValue(value, out var owner) && closureNames.Contains(owner.Name)) ||
      (value is Token token && sourceTokens.Value.TryGet(token, out _)));
    // The fresh resolution sees the parsed declarations of its prerequisites; the reused one, their results.
    for (var i = 0; i < context.Closure.Count && i < closure.Count; i++) {
      equality.Pair(context.Closure[i], closure[i].ResolvedDeclaration);
      if (context.Closure[i] is LiteralModuleDecl parsed && closure[i].ResolvedDeclaration is LiteralModuleDecl reused) {
        equality.Pair(parsed.ModuleDef, reused.ModuleDef);
      }
    }
    var freshRoots = Roots(fresh, worker().Builtins);
    var difference = fresh.ErrorReporter.HasErrors
      ? "the fresh resolution has errors: " + fresh.ErrorReporter.AllMessagesByLevel[ErrorLevel.Error].FirstOrDefault()?.Message
      : equality.Difference(freshRoots.Append(ViewTables(fresh.Builtins)).ToArray(),
        Roots(hit, hit.Builtins).Append(ViewTables(hit.Builtins)).ToArray());
    // The fresh resolution also registered its traits with the traits they extend; only the reused ones stay.
    Replay((List<TraitDecl>)freshRoots[^1]!, true);
    if (difference == null) {
      state = "hit (checked " + equality.Compared.ToString(CultureInfo.InvariantCulture) + " objects)";
      return;
    }
    state = "mismatch: " + difference;
    hit.ErrorReporter.Error(MessageSource.Resolver, "resolution-cache-check", context.Declaration.Origin,
      "the reused resolution of module " + context.Declaration.FullName + " differs from a fresh one at " + difference);
  }

  /// <summary>The builtin tables of a view that a module's output leaves for its dependents, in a canonical order.</summary>
  private static object[] ViewTables(SystemModuleManager view) {
    object[] Sorted<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> table, Func<TKey, string> name) =>
      table.OrderBy(p => name(p.Key), StringComparer.Ordinal).Select(p => (object)new object?[] { name(p.Key), p.Value }).ToArray();
    string Number(int n) => n.ToString("D6", CultureInfo.InvariantCulture);
    return [
      view.SystemModule.SourceDecls.ToArray(),
      Sorted(view.PreTypeBuiltins, n => n),
      Sorted(view.ArrowTypeDecls, Number), Sorted(view.PartialArrowTypeDecls, Number), Sorted(view.TotalArrowTypeDecls, Number),
      Sorted(view.arrayTypeDecls, Number),
      Sorted(view.Tuples, shape => string.Concat(shape.Select(g => g ? '1' : '0'))),
      view.Bitwidths.Order().Select(n => (object)n).ToArray()
    ];
  }

  /// <summary>A module that is resolved without the cache, such as a member of a recursive module group.</summary>
  public void Uncacheable(ModuleDecl declaration) => records[declaration.FullName] = Record.None;

  private ModuleResolutionResult Uncacheable(string name, ModuleResolutionResult output) {
    records[name] = Record.None;
    return output;
  }

  // ---------------------------------------------------------------- miss

  private CachePublication Store(ModuleContext context, string key, ModuleResolutionResult output, Worker worker, bool replace) {
    var name = context.Declaration.FullName;
    var view = worker.Builtins;
    var journal = BuiltinJournal.Of(view);
    var anchors = Anchors(view, journal, worker);
    var closureNames = context.Closure.Select(d => d.FullName).ToHashSet(StringComparer.Ordinal);
    var named = Named(context, worker);
    var ancestorTokens = AncestorTokens(context);
    var ownTokens = ownScopeTokens.GetValueOrDefault(name) ?? [];
    var world = new ResolutionSnapshot.CaptureWorld {
      Object = value => {
        if (ResolutionGraphSchema.Statics.TryGetValue(value, out var staticName)) {
          return new ResolutionExternal(ResolutionExternal.Kinds.Static, staticName, 0);
        }
        if (ResolutionGraphSchema.SingletonName(value) is { } singleton) {
          return new ResolutionExternal(ResolutionExternal.Kinds.Static, singleton, 0);
        }
        if (named.TryGetValue(value, out var namedName)) { return new ResolutionExternal(ResolutionExternal.Kinds.Named, namedName, 0); }
        if (anchors.Objects.TryGetValue(value, out var anchor)) { return anchor; }
        if (owners.TryGetValue(value, out var owner)) {
          if (!closureNames.Contains(owner.Name)) { throw new ResolutionSnapshotUnsupported("object of " + owner.Name + ": " + value.GetType()); }
          return owner;
        }
        return null;
      },
      Token = token => {
        if (tokenOwners.TryGetValue(token, out var owner)) {
          if (!closureNames.Contains(owner.Name)) { throw new ResolutionSnapshotUnsupported("scope of " + owner.Name); }
          return owner;
        }
        if (anchors.Tokens.TryGetValue(token, out var anchor)) { return anchor; }
        if (ancestorTokens.TryGetValue(token, out var ancestor)) { return ancestor; }
        if (token <= lastParseToken && !ownTokens.Contains(token)) { throw new ResolutionSnapshotUnsupported("scope of another module"); }
        return null;
      },
      SourceTokens = sourceTokens.Value,
      Module = name,
      Frames = frames,
      PrerequisiteToken = (module, token) => closureNames.Contains(module) && records.TryGetValue(module, out var record) &&
                                             record.SourceTokenRanks.TryGetValue(token, out var rank) ? rank : null
    };
    // A module whose resolution changed a collection of a prerequisite, other than by its effects, would lose that
    // change on a hit.
    foreach (var prerequisite in context.Closure) {
      if (records.TryGetValue(prerequisite.FullName, out var record) && record.Changed() is { } index) {
        throw new ResolutionSnapshotUnsupported("changes " + record.Owned[index].GetType().Name + " of " + prerequisite.FullName);
      }
    }
    // A hit rebases tokens, but not text: a message that names a coordinate would keep the old one.
    if (output.ErrorReporter.AllMessages.FirstOrDefault(m => NamesPosition(m.Message) ||
          m.RelatedInformation.Any(r => NamesPosition(r.Message))) is { } positional) {
      throw new ResolutionSnapshotUnsupported("a message names a position: " + positional.ErrorId);
    }
    var contextual = Contextual(output);
    var captured = ResolutionSnapshot.Capture(Roots(output, view), world, contextual);
    CheckOwnership(output, captured.Owned);
    var usedAnchors = captured.Anchors.Order(StringComparer.Ordinal).ToArray();
    var manifest = new Manifest(key, Producer, name, captured.Structure, journal,
      usedAnchors.Select(a => (a, anchors.Guard(a))).ToArray(), anchors.TokenCount);
    var publication = store.TryPublish(ArtifactTable.Resolutions, key, Encode(manifest, captured.Payload), replace);
    Register(name, captured.Structure, captured.Owned, captured.OwnedTokens, captured.SourceTokens);
    return publication;
  }

  private static readonly Regex PositionText = new(@"\bline \d+|\(\d+,\d+\)|\.dfy[:(]\d", RegexOptions.CultureInvariant);
  private static bool NamesPosition(string text) => PositionText.IsMatch(text);

  private static object?[] Roots(ModuleResolutionResult output, SystemModuleManager view) => [
    output.ResolvedDeclaration, output.Signatures, output.ClassMembers, output.TranslationInputs, output.SystemClassMembers,
    view.MaxNonGhostTupleSizeToken, view.MaxNonGhostTupleSizeUsed, output.ErrorReporter.AllMessages, Effects(output)
  ];

  /// <summary>
  /// What a module's resolution wrote into declarations of other modules and of the builtins: each trait of the
  /// module that extends a trait elsewhere is recorded in that trait's <see cref="TraitDecl.TraitDeclsCanBeDowncastedTo"/>.
  /// The list holds (parent, trait) pairs, flattened. A hit replays them.
  /// </summary>
  private static List<TraitDecl> Effects(ModuleResolutionResult output) {
    var modules = output.Signatures.Keys.ToHashSet(ReferenceEqualityComparer.Instance);
    var result = new List<TraitDecl>();
    foreach (var module in output.Signatures.Keys) {
      foreach (var trait in module.TopLevelDecls.OfType<TraitDecl>()) {
        foreach (var parent in trait.ParentTraitHeads) {
          if (modules.Contains(parent.EnclosingModuleDefinition)) { continue; }
          lock (parent.TraitDeclsCanBeDowncastedTo) {
            if (!parent.TraitDeclsCanBeDowncastedTo.Contains(trait)) { continue; }
          }
          result.Add(parent);
          result.Add(trait);
        }
      }
    }
    return result;
  }

  private static void Replay(List<TraitDecl> effects, bool undo) {
    for (var i = 0; i + 1 < effects.Count; i += 2) {
      var subtraits = effects[i].TraitDeclsCanBeDowncastedTo;
      lock (subtraits) {
        if (undo) { subtraits.Remove(effects[i + 1]); } else { subtraits.Add(effects[i + 1]); }
      }
    }
  }

  private static HashSet<object> Contextual(ModuleResolutionResult output) {
    var result = new HashSet<object>(ReferenceEqualityComparer.Instance) { output.ResolvedDeclaration };
    if (output.ResolvedDeclaration is LiteralModuleDecl literal) { result.Add(literal.ModuleDef); }
    return result;
  }

  /// <summary>
  /// A module's snapshot must not copy a declaration or module of another module. The parser builds the
  /// self-synonym of a declaration in the default module while that declaration is still in its file's module,
  /// and leaves it there, so the default module also owns declarations of the files' modules.
  /// </summary>
  private void CheckOwnership(ModuleResolutionResult output, object[] owned) {
    var set = new HashSet<object>(owned, ReferenceEqualityComparer.Instance);
    if (output.ResolvedDeclaration is LiteralModuleDecl { ModuleDef.IsDefaultModule: true }) { set.UnionWith(program.Files); }
    // The module's resolution builds a view of the module for each export set, to check that the set is consistent.
    var views = owned.OfType<ModuleExportDecl>().Select(e => e.EffectiveModule).Where(v => v != null)
      .ToHashSet(ReferenceEqualityComparer.Instance);
    foreach (var value in owned) {
      switch (value) {
        case ModuleDefinition definition when !output.Signatures.ContainsKey(definition) && !views.Contains(definition):
          throw new ResolutionSnapshotUnsupported("copies module " + definition.FullName);
        case TopLevelDecl declaration when !ReferenceEquals(declaration, output.ResolvedDeclaration) &&
                                           declaration.EnclosingModuleDefinition is { } module && !set.Contains(module):
          throw new ResolutionSnapshotUnsupported("copies declaration " + declaration.FullName);
        case MemberDecl member when member.EnclosingClass is { } enclosing && !set.Contains(enclosing):
          throw new ResolutionSnapshotUnsupported("copies member " + member.FullName);
      }
    }
  }

  private void Register(string name, string structure, object[] owned, uint[] tokens, Token[] sourceTokens) {
    for (var i = 0; i < owned.Length; i++) {
      // A value record belongs to no module: a clone shares its original's, and a snapshot holds it by value.
      if (!owned[i].GetType().IsValueType && !ResolutionGraphSchema.IsValueRecord(owned[i].GetType())) {
        owners.TryAdd(owned[i], new ResolutionExternal(ResolutionExternal.Kinds.Prerequisite, name, i));
      }
    }
    for (var i = 0; i < tokens.Length; i++) {
      tokenOwners.TryAdd(tokens[i], new ResolutionExternalToken(ResolutionExternal.Kinds.Prerequisite, name, i));
    }
    // A dependent's traits are recorded in this module's traits: those lists may grow (see Effects).
    var effects = owned.OfType<TraitDecl>().Select(t => (object)t.TraitDeclsCanBeDowncastedTo).ToHashSet(ReferenceEqualityComparer.Instance);
    var sizes = new int[owned.Length];
    for (var i = 0; i < owned.Length; i++) {
      sizes[i] = Size(owned[i], effects);
    }
    records[name] = new Record(structure, owned, tokens, sizes, sourceTokens);
  }

  // ---------------------------------------------------------------- hit

  private ModuleResolutionResult Hit(ModuleContext context, string key, byte[] file, Worker worker) {
    var (manifest, payload) = Read(file);
    var name = context.Declaration.FullName;
    if (manifest.Key != key || manifest.Producer != Producer || manifest.Name != name) {
      throw new ResolutionSnapshotInvalid("entry of another module");
    }
    var view = worker.Builtins;
    manifest.Journal.Replay(view, new ModuleResolver(worker.Resolver, context.Declaration.Options));
    var anchors = Anchors(view, manifest.Journal, worker);
    if (anchors.TokenCount != manifest.BuiltinTokens) { throw new ResolutionSnapshotInvalid("builtin scopes differ"); }
    foreach (var (anchor, guard) in manifest.Anchors) {
      if (anchors.Guard(anchor) != guard) { throw new ResolutionSnapshotInvalid("builtin " + anchor + " differs"); }
    }
    var closureNames = context.Closure.Select(d => d.FullName).ToHashSet(StringComparer.Ordinal);
    var named = Named(context, worker).ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);
    var ancestorTokens = AncestorTokens(context).ToDictionary(p => p.Value, p => p.Key);
    var world = new ResolutionSnapshot.MaterializeWorld {
      Object = external => external.Kind switch {
        ResolutionExternal.Kinds.Static => ResolutionGraphSchema.StaticsByName.GetValueOrDefault(external.Name) ??
                                           ResolutionGraphSchema.Singleton(external.Name) ??
                                           throw new ResolutionSnapshotInvalid("no static " + external.Name),
        ResolutionExternal.Kinds.Named => named.GetValueOrDefault(external.Name) ?? throw new ResolutionSnapshotInvalid("no " + external.Name),
        ResolutionExternal.Kinds.Anchor => anchors.Object(external.Name, external.Index),
        ResolutionExternal.Kinds.Prerequisite => closureNames.Contains(external.Name) && records.TryGetValue(external.Name, out var record) &&
                                                 external.Index >= 0 && external.Index < record.Owned.Length
          ? record.Owned[external.Index] : throw new ResolutionSnapshotInvalid("no object of " + external.Name),
        _ => throw new ResolutionSnapshotInvalid("bad external")
      },
      Token = external => external.Kind switch {
        ResolutionExternal.Kinds.Anchor => anchors.Token(external.Rank),
        ResolutionExternal.Kinds.Named => ancestorTokens.TryGetValue(external, out var token) ? token
          : throw new ResolutionSnapshotInvalid("no scope of " + external.Name),
        ResolutionExternal.Kinds.Prerequisite => closureNames.Contains(external.Name) && records.TryGetValue(external.Name, out var record) &&
                                                 external.Rank >= 0 && external.Rank < record.Tokens.Length
          ? record.Tokens[external.Rank] : throw new ResolutionSnapshotInvalid("no scope of " + external.Name),
        _ => throw new ResolutionSnapshotInvalid("bad external scope")
      },
      SourceTokens = sourceTokens.Value,
      Module = name,
      Frames = frames,
      PrerequisiteToken = (module, rank) => closureNames.Contains(module) && records.TryGetValue(module, out var record) &&
                                            rank >= 0 && rank < record.SourceTokens.Length
        ? record.SourceTokens[rank] : throw new ResolutionSnapshotInvalid("no token of " + module)
    };
    var materialized = ResolutionSnapshot.Materialize(payload, world);
    if (materialized.Roots.Length != 9 || materialized.Roots[0] is not ModuleDecl resolved ||
        materialized.Roots[1] is not Dictionary<ModuleDefinition, ModuleSignature> signatures ||
        materialized.Roots[2] is not Dictionary<TopLevelDeclWithMembers, Dictionary<string, MemberDecl>> members ||
        materialized.Roots[3] is not ModuleTranslationInputs translationInputs ||
        materialized.Roots[4] is not Dictionary<TopLevelDeclWithMembers, Dictionary<string, MemberDecl>> systemMembers ||
        materialized.Roots[5] is not (null or IOrigin) || materialized.Roots[6] is not int maxTupleSize ||
        materialized.Roots[7] is not List<DafnyDiagnostic> messages || messages.Any(m => m.Level == ErrorLevel.Error) ||
        materialized.Roots[8] is not List<TraitDecl> effects || effects.Count % 2 != 0 ||
        resolved.GetType() != context.Declaration.GetType()) {
      throw new ResolutionSnapshotInvalid("bad roots");
    }
    CopyContextual(context.Declaration, resolved);
    if (resolved is LiteralModuleDecl literal && context.Declaration is LiteralModuleDecl parsed) {
      CopyContextual(parsed.ModuleDef, literal.ModuleDef);
    }
    // Only the builtins the module's output uses remain in its view, in the order of its fresh resolution.
    view.SetMaxNonGhostTupleSize(maxTupleSize, (IOrigin?)materialized.Roots[5]!);
    view.RestrictResolutionOutput(translationInputs);
    manifest.Journal.Order(view);
    Register(name, manifest.Structure, materialized.Owned, materialized.OwnedTokens, materialized.SourceTokens);
    Replay(effects, false);
    // The module's warnings and information messages, which its fresh resolution reported.
    var reporter = new BatchErrorReporter(context.Declaration.Options);
    foreach (var message in messages) { reporter.MessageCore(message); }
    return new ModuleResolutionResult(resolved, reporter, signatures, members) {
      Key = program.ResolutionKeys[context.Declaration],
      Identity = program.ResolutionIdentities[context.Declaration],
      Builtins = view,
      SystemClassMembers = systemMembers,
      TranslationInputs = translationInputs
    };
  }

  private static void CopyContextual(object source, object target) {
    foreach (var field in ResolutionGraphSchema.Fields(source.GetType())) {
      if (ResolutionGraphSchema.IsContextual(field)) { field.SetValue(target, field.GetValue(source)); }
    }
  }

  // ---------------------------------------------------------------- context objects

  private Dictionary<object, string> Named(ModuleContext context, Worker worker) {
    var result = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
    void Add(object? value, string name) {
      if (value != null) { result.TryAdd(value, name); }
    }
    Add(context.Declaration.Options, "options.module");
    Add(program.Options, "options.program");
    // A refinement or an export copies a prerequisite's declarations with the options of that prerequisite's file.
    foreach (var prerequisite in context.Closure.OrderBy(d => d.FullName, StringComparer.Ordinal)) {
      Add(prerequisite.Options, "options.prerequisite:" + prerequisite.FullName);
    }
    Add(program.Compilation, "compilation");
    Add(program, "program");
    Add(worker.Program, "program.worker");
    Add(program.Reporter, "reporter");
    Add(worker.Program.Reporter, "reporter.worker");
    var k = 0;
    for (var ancestor = context.Declaration.EnclosingModuleDefinition; ancestor != null; ancestor = ancestor.EnclosingModule) {
      Add(ancestor, "ancestor:" + k.ToString(CultureInfo.InvariantCulture));
      Add(ancestor.VisibilityScope, "ancestor-scope:" + k.ToString(CultureInfo.InvariantCulture));
      k++;
    }
    for (var i = 0; i < program.Files.Count; i++) { Add(program.Files[i], "file:" + i.ToString(CultureInfo.InvariantCulture)); }
    return result;
  }

  private static Dictionary<uint, ResolutionExternalToken> AncestorTokens(ModuleContext context) {
    var result = new Dictionary<uint, ResolutionExternalToken>();
    var k = 0;
    for (var ancestor = context.Declaration.EnclosingModuleDefinition; ancestor != null; ancestor = ancestor.EnclosingModule) {
      var rank = 0;
      foreach (var token in ResolutionGraphSchema.Tokens(ancestor.VisibilityScope).ToArray()) {
        result.TryAdd(token, new ResolutionExternalToken(ResolutionExternal.Kinds.Named,
          "ancestor:" + k.ToString(CultureInfo.InvariantCulture), rank++));
      }
      k++;
    }
    return result;
  }

  // ---------------------------------------------------------------- builtins

  private BuiltinAnchors Anchors(SystemModuleManager view, BuiltinJournal journal, Worker worker) {
    var singles = new List<(string, object?)> {
      ("module:view", view.SystemModule), ("module:initial", initialBuiltins.SystemModule),
      ("module:program", program.SystemModuleManager.SystemModule), ("module:factory", catalog.Factory.SystemModule),
      ("module:worker", worker.Program.SystemModuleManager.SystemModule),
      ("signature:view", view.systemNameInfo), ("signature:program", program.SystemModuleManager.systemNameInfo),
      ("scope:system", view.SystemModule.VisibilityScope)
    };
    var regions = new List<(string, object)>();
    var declarations = view.SystemModule.SourceDecls;
    foreach (var declarationName in journal.SourceDecls) {
      var matches = declarations.Where(d => d.Name == declarationName).ToArray();
      if (matches.Length != 1) { throw new ResolutionSnapshotInvalid("builtin " + declarationName); }
      regions.Add(("decl:" + declarationName, matches[0]));
    }
    foreach (var pretype in journal.Pretypes) {
      if (!view.PreTypeBuiltins.TryGetValue(pretype, out var declaration)) { throw new ResolutionSnapshotInvalid("builtin " + pretype); }
      regions.Add(("pre:" + pretype, declaration));
    }
    for (var i = 0; i < initialMembers.Length; i++) {
      regions.Add(("members:" + i.ToString(CultureInfo.InvariantCulture) + ":" + initialMembers[i].Name, initialMembers[i].Members));
    }
    for (var i = 0; i < view.valuetypeDecls.Length; i++) {
      regions.Add(("value:" + i.ToString(CultureInfo.InvariantCulture), view.valuetypeDecls[i]));
    }
    var index = sourceTokens.Value;
    return BuiltinAnchors.For(singles, regions, value => value is ModuleDefinition || owners.ContainsKey(value) ||
      ResolutionGraphSchema.Statics.ContainsKey(value) || ResolutionGraphSchema.SingletonName(value) != null || (value is Token token && index.TryGet(token, out _)));
  }

  /// <summary>
  /// The builtin objects a module may share, each named by its anchor (a builtin declaration, or one of the
  /// system modules and signatures) and its index in the breadth-first order of the anchor's region: the objects
  /// reachable from the anchor without passing another anchor, a module, a static or a source token.
  /// </summary>
  private sealed class BuiltinAnchors {
    public readonly Dictionary<object, ResolutionExternal> Objects = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<uint, ResolutionExternalToken> Tokens = new();
    private readonly Dictionary<string, List<object>> regions = new(StringComparer.Ordinal);
    private uint[] tokenList = [];
    public int TokenCount => tokenList.Length;

    public static BuiltinAnchors For(IEnumerable<(string Name, object? Root)> singles, IEnumerable<(string Name, object Root)> regionRoots,
      Func<object, bool> stop) {
      var result = new BuiltinAnchors();
      foreach (var (name, root) in singles) {
        if (root != null && !result.Objects.ContainsKey(root)) {
          result.Objects.Add(root, new ResolutionExternal(ResolutionExternal.Kinds.Anchor, name, 0));
          result.regions[name] = [root];
        }
      }
      var roots = regionRoots.Where(r => !result.Objects.ContainsKey(r.Root)).GroupBy(r => r.Root, ReferenceEqualityComparer.Instance)
        .Select(g => g.First()).OrderBy(r => r.Name, StringComparer.Ordinal).ToArray();
      foreach (var (name, root) in roots) {
        result.Objects.Add(root, new ResolutionExternal(ResolutionExternal.Kinds.Anchor, name, 0));
        result.regions[name] = [root];
      }
      foreach (var (name, _) in roots) {
        var region = result.regions[name];
        for (var i = 0; i < region.Count; i++) {
          object?[] children;
          try {
            children = ResolutionSnapshot.Children(region[i]);
          } catch (ResolutionSnapshotUnsupported) {
            continue;
          }
          foreach (var child in children) {
            // A dependent's traits are recorded in a builtin trait (object); they are the dependent's effects.
            if (region[i] is TraitDecl trait && ReferenceEquals(child, trait.TraitDeclsCanBeDowncastedTo)) { continue; }
            if (child == null || ResolutionGraphSchema.IsScalar(child.GetType()) || child.GetType().IsValueType ||
                result.Objects.ContainsKey(child) || stop(child)) {
              continue;
            }
            result.Objects.Add(child, new ResolutionExternal(ResolutionExternal.Kinds.Anchor, name, region.Count));
            region.Add(child);
          }
        }
      }
      var tokens = new SortedSet<uint>();
      foreach (var scope in result.regions.Values.SelectMany(r => r).OfType<VisibilityScope>()) {
        tokens.UnionWith(ResolutionGraphSchema.Tokens(scope));
      }
      result.tokenList = tokens.ToArray();
      for (var i = 0; i < result.tokenList.Length; i++) {
        result.Tokens[result.tokenList[i]] = new ResolutionExternalToken(ResolutionExternal.Kinds.Anchor, BuiltinTokenOwner, i);
      }
      return result;
    }

    public object Object(string name, int index) =>
      regions.TryGetValue(name, out var region) && index >= 0 && index < region.Count
        ? region[index] : throw new ResolutionSnapshotInvalid("no builtin " + name + "#" + index);

    public uint Token(int rank) =>
      rank >= 0 && rank < tokenList.Length ? tokenList[rank] : throw new ResolutionSnapshotInvalid("no builtin scope");

    /// <summary>The size and type sequence of an anchor's region.</summary>
    public string Guard(string name) {
      if (!regions.TryGetValue(name, out var region)) { return "absent"; }
      var types = string.Join(";", region.Select(o => o.GetType().FullName));
      return region.Count.ToString(CultureInfo.InvariantCulture) + ":" +
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(types)));
    }
  }

  /// <summary>
  /// The builtins of a module's output view: what its resolution demanded, and so what a hit demands again.
  /// </summary>
  internal sealed record BuiltinJournal(bool[][] Tuples, int[] Arrays, int[] Arrows, bool Bounded, bool BoundedArrowAdded,
    int[] Bitvectors, string[] Pretypes, string[] SourceDecls) {
    private static string Shape(bool[] shape) => string.Concat(shape.Select(b => b ? '1' : '0'));
    private static readonly Regex BitvectorName = new("^bv(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant);

    public static BuiltinJournal Of(SystemModuleManager view) {
      var sourceDecls = view.SystemModule.SourceDecls.Select(d => d.Name).ToArray();
      return new(view.TupleShapes.Select(s => s.ToArray()).OrderBy(s => s.Length).ThenBy(Shape, StringComparer.Ordinal).ToArray(),
        view.arrayTypeDecls.Keys.Order().ToArray(), view.ArrowTypeDecls.Keys.Order().ToArray(),
        view.BoundedDecl != null, view.BoundedArrowAdded,
        sourceDecls.Select(n => BitvectorName.Match(n)).Where(m => m.Success)
          .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Order().ToArray(),
        view.PreTypeBuiltins.Keys.Order(StringComparer.Ordinal).ToArray(), sourceDecls);
    }

    public void Replay(SystemModuleManager view, ModuleResolver resolver) {
      foreach (var shape in Tuples) { view.TupleType(Token.NoToken, shape.Length, true, shape.ToList()); }
      foreach (var dimensions in Arrays) {
        var (_, modify) = SystemModuleManager.ArrayType(Token.NoToken, dimensions, null, true);
        modify(view);
      }
      foreach (var arity in Arrows) { view.CreateArrowTypeDecl(arity); }
      if (Bounded) { view.CreateBoundedTypeDecl(); }
      view.BoundedArrowAdded = BoundedArrowAdded;
      foreach (var width in Bitvectors) { resolver.AddBitvectorTypeDecl("bv" + width.ToString(CultureInfo.InvariantCulture), width); }
      foreach (var pretype in Pretypes) { PreTypeResolver.ReplayBuiltin(resolver, pretype); }
      if (Bounded && view.BoundedDecl == null) { throw new ResolutionSnapshotInvalid("bounded is unavailable"); }
    }

    /// <summary>Order the view's system declarations as in the module's fresh resolution.</summary>
    public void Order(SystemModuleManager view) {
      var declarations = view.SystemModule.SourceDecls;
      if (!declarations.Select(d => d.Name).Order(StringComparer.Ordinal).SequenceEqual(SourceDecls.Order(StringComparer.Ordinal))) {
        throw new ResolutionSnapshotInvalid("builtin declarations differ");
      }
      var position = SourceDecls.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i);
      var ordered = declarations.OrderBy(d => position[d.Name]).ToList();
      declarations.Clear();
      declarations.AddRange(ordered);
    }

    public void Write(BinaryWriter writer) {
      writer.Write7BitEncodedInt(Tuples.Length);
      foreach (var shape in Tuples) { writer.Write(Shape(shape)); }
      WriteInts(writer, Arrays);
      WriteInts(writer, Arrows);
      writer.Write(Bounded);
      writer.Write(BoundedArrowAdded);
      WriteInts(writer, Bitvectors);
      WriteStrings(writer, Pretypes);
      WriteStrings(writer, SourceDecls);
    }

    public static BuiltinJournal Read(BinaryReader reader) {
      var tuples = new bool[Count(reader)][];
      for (var i = 0; i < tuples.Length; i++) {
        var text = reader.ReadString();
        tuples[i] = text.Select(c => c switch { '1' => true, '0' => false, _ => throw new ResolutionSnapshotInvalid("bad tuple") }).ToArray();
      }
      return new(tuples, ReadInts(reader), ReadInts(reader), reader.ReadBoolean(), reader.ReadBoolean(), ReadInts(reader),
        ReadStrings(reader), ReadStrings(reader));
    }
  }

  // ---------------------------------------------------------------- file format

  private sealed record Manifest(string Key, string Producer, string Name, string Structure, BuiltinJournal Journal,
    (string Anchor, string Guard)[] Anchors, int BuiltinTokens);

  /// <summary>
  /// "DRES", the format version, the manifest and the snapshot, then the SHA-256 of everything before it.
  /// The store publishes it atomically, so a reader sees a complete entry or none.
  /// </summary>
  private static byte[] Encode(Manifest manifest, byte[] payload) {
    using var bytes = new MemoryStream();
    using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
      writer.Write(Encoding.ASCII.GetBytes(Magic));
      writer.Write(FormatVersion);
      writer.Write(manifest.Key);
      writer.Write(manifest.Producer);
      writer.Write(manifest.Name);
      writer.Write(manifest.Structure);
      manifest.Journal.Write(writer);
      writer.Write7BitEncodedInt(manifest.Anchors.Length);
      foreach (var (anchor, guard) in manifest.Anchors) { writer.Write(anchor); writer.Write(guard); }
      writer.Write(manifest.BuiltinTokens);
      writer.Write7BitEncodedInt(payload.Length);
      writer.Write(payload);
    }
    var body = bytes.ToArray();
    return [.. body, .. SHA256.HashData(body)];
  }

  private static (Manifest, byte[]) Read(byte[] file) {
    if (file.Length < 4 + 4 + 32) { throw new ResolutionSnapshotInvalid("truncated entry"); }
    var body = file.AsSpan(0, file.Length - 32);
    if (!SHA256.HashData(body).AsSpan().SequenceEqual(file.AsSpan(file.Length - 32))) {
      throw new ResolutionSnapshotInvalid("corrupt entry");
    }
    using var stream = new MemoryStream(file, 0, file.Length - 32, false);
    using var reader = new BinaryReader(stream, Encoding.UTF8);
    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != Magic || reader.ReadInt32() != FormatVersion) {
      throw new ResolutionSnapshotInvalid("not an entry of this format");
    }
    var key = reader.ReadString();
    var producer = reader.ReadString();
    var name = reader.ReadString();
    var structure = reader.ReadString();
    var journal = BuiltinJournal.Read(reader);
    var anchors = new (string, string)[Count(reader)];
    for (var i = 0; i < anchors.Length; i++) { anchors[i] = (reader.ReadString(), reader.ReadString()); }
    var builtinTokens = reader.ReadInt32();
    var length = Count(reader);
    var payload = reader.ReadBytes(length);
    if (payload.Length != length || stream.Position != stream.Length) { throw new ResolutionSnapshotInvalid("bad entry length"); }
    return (new Manifest(key, producer, name, structure, journal, anchors, builtinTokens), payload);
  }

  private static int Count(BinaryReader reader) {
    var n = reader.Read7BitEncodedInt();
    if (n < 0 || n > reader.BaseStream.Length - reader.BaseStream.Position + 1) { throw new ResolutionSnapshotInvalid("bad count"); }
    return n;
  }

  private static void WriteInts(BinaryWriter writer, int[] values) {
    writer.Write7BitEncodedInt(values.Length);
    foreach (var value in values) { writer.Write7BitEncodedInt(value); }
  }

  private static int[] ReadInts(BinaryReader reader) {
    var values = new int[Count(reader)];
    for (var i = 0; i < values.Length; i++) { values[i] = reader.Read7BitEncodedInt(); }
    return values;
  }

  private static void WriteStrings(BinaryWriter writer, string[] values) {
    writer.Write7BitEncodedInt(values.Length);
    foreach (var value in values) { writer.Write(value); }
  }

  private static string[] ReadStrings(BinaryReader reader) {
    var values = new string[Count(reader)];
    for (var i = 0; i < values.Length; i++) { values[i] = reader.ReadString(); }
    return values;
  }
}
