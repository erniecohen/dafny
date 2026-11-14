#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace DafnyCore;

/// <summary>
/// One premise of a library record built under --modular-termination with a trust list
/// (https://github.com/erniecohen/dafny/issues/211, milestone M5b): a bodyless {:axiom} ghost
/// declaration of the library's own program, named by its trust list.
/// </summary>
public class ModularTerminationPremise {
  public string Id { get; set; } = null!;
  public string Kind { get; set; } = null!;
  public string StatementSha256 { get; set; } = null!;
  public string ContextSha256 { get; set; } = null!;

  public ModularTerminationPremise() { }

  public ModularTerminationPremise(string id, string kind, string statementSha256, string contextSha256) {
    Id = id;
    Kind = kind;
    StatementSha256 = statementSha256;
    ContextSha256 = contextSha256;
  }

  public bool SameAs(ModularTerminationPremise other) =>
    Id == other.Id && Kind == other.Kind && StatementSha256 == other.StatementSha256 && ContextSha256 == other.ContextSha256;
}

/// <summary>
/// An entry of a trust list: a premise as reviewed, and why it is true.
/// </summary>
public sealed record ModularTerminationTrustEntry(string Id, string Kind, string StatementSha256, string ContextSha256,
  string Reason);

/// <summary>
/// The trust lists that --modular-termination-trust selects, and the digests that bind a premise to
/// an entry.  An entry names one declaration by its fully qualified name, with the digest of its
/// statement (the declaration as Dafny prints it after resolution) and of its context (the termination
/// contracts of the top-level modules that the declaration's module belongs to, imports or refines,
/// transitively).  A premise is admitted only when both digests match, so an edit of the premise or of
/// a definition it is about refuses the record until the entry is reviewed again.  See
/// docs/dev/design/211-m5b-trusted-premises.md.
/// </summary>
public static class ModularTerminationTrustList {
  public const string None = "none";
  public const string StandardLibrary = "standard-library";
  public const string LemmaKind = "lemma";
  public const string GhostFunctionKind = "ghost function";

  private const string ExtensionalityReason =
    "Function extensionality for total arrows on this domain is true in Dafny's model and consistent with its encoding; " +
    "https://github.com/erniecohen/dafny/issues/34 and https://github.com/erniecohen/dafny/pull/346";
  private const string CompletenessReason =
    "The real numbers are a complete ordered field in Dafny's model; Dafny axiomatizes only an ordered field; " +
    "https://github.com/erniecohen/dafny/pull/346";
  private const string OrdinalLemmaReason =
    "A fact of ordinal arithmetic that holds for Dafny's ORDINAL, which Dafny axiomatizes only partially; " +
    "https://github.com/erniecohen/dafny/issues/211";
  private const string OrdinalAxiomReason =
    "Infinity and replacement hold of the ordinals, Dafny's model of ORDINAL, and are consistent with its encoding; " +
    "Std.Ordinal proves Omega, PlusLimit and TimesLimit from them; https://github.com/erniecohen/dafny/issues/354";
  private const string CharsConsistentReason =
    "The two literal tables of HexConversion are inverse to each other, which holds by inspection but is too large to " +
    "verify; https://github.com/erniecohen/dafny/issues/211";

  /// <summary>
  /// Version 1 of the standard-library trust list, sorted by id.
  /// </summary>
  private static readonly ModularTerminationTrustEntry[] StandardLibraryEntries = [
    new("Std.Axioms.FunctionExtensionality.Nullary", LemmaKind, "d847a8f43ab034e6351057339e883677f7a3d6473fbbeea1d9df72bc02563c3b", "4b66711dad2babe233eaf16094f97f8c508c46b1a06130a3462a4be3e71d7691", ExtensionalityReason),
    new("Std.Axioms.FunctionExtensionality.OnBool", LemmaKind, "772bcd4f033814446e3ad02282ab8902356b1ba8477e259112292bf9bfe8dab9", "4b66711dad2babe233eaf16094f97f8c508c46b1a06130a3462a4be3e71d7691", ExtensionalityReason),
    new("Std.Axioms.FunctionExtensionality.OnInt", LemmaKind, "93a569e38f30f6e3a6f15cddf676bc379dbb21647a6a127a58ceb79ba37adbfd", "4b66711dad2babe233eaf16094f97f8c508c46b1a06130a3462a4be3e71d7691", ExtensionalityReason),
    new("Std.Axioms.FunctionExtensionality.OnIntInt", LemmaKind, "cc5d28bb3befc12237a9421e21db95ee1a5709f67b8899cba9f36f2cfc0d18ba", "4b66711dad2babe233eaf16094f97f8c508c46b1a06130a3462a4be3e71d7691", ExtensionalityReason),
    new("Std.Axioms.FunctionExtensionality.OnOrdinal", LemmaKind, "4927c8b108ac8b93ee73d7ac5a9c39be319124f392a4e0a1fbd7f253e3da734e", "4b66711dad2babe233eaf16094f97f8c508c46b1a06130a3462a4be3e71d7691", ExtensionalityReason),
    new("Std.Axioms.FunctionExtensionality.OnReal", LemmaKind, "64581d25e05bef3d70c3b14c221b08c070c92c79dd35576e3dcee6f34eda8629", "4b66711dad2babe233eaf16094f97f8c508c46b1a06130a3462a4be3e71d7691", ExtensionalityReason),
    new("Std.Axioms.Ordinals.Infinite", LemmaKind, "aed51e4d10676252a263be19ffd749fee717af923eb911c0381b0f52458bf31f", "4b66711dad2babe233eaf16094f97f8c508c46b1a06130a3462a4be3e71d7691", OrdinalAxiomReason),
    new("Std.Axioms.Ordinals.Replacement", LemmaKind, "1a235ca5cda70c581ecc2e1999d32ddd51f50ff521ab0de1841ad937c8853c71", "4b66711dad2babe233eaf16094f97f8c508c46b1a06130a3462a4be3e71d7691", OrdinalAxiomReason),
    new("Std.Axioms.Reals.Complete", LemmaKind, "9e1506689a1fd764e248bf557e7a7c92a55454283bfb64a636de52fa50cc94d8", "4b66711dad2babe233eaf16094f97f8c508c46b1a06130a3462a4be3e71d7691", CompletenessReason),
    new("Std.Ordinal.PlusDefinition", LemmaKind, "eb219ababb1ca42a8fc32cec0bb4f7de27d4098af5e7f080ee5c55464376b452", "1e89b2229497a7de8f7cb739e3dd1d25dc34ee541c310080dba73b24fcb5cf4b", OrdinalLemmaReason),
    new("Std.Ordinal.PlusIncreasingOnLeft", LemmaKind, "a532b56910d471ffd1ede250e6a64c1572a31b76545980cb395aa6020815646e", "1e89b2229497a7de8f7cb739e3dd1d25dc34ee541c310080dba73b24fcb5cf4b", OrdinalLemmaReason),
    new("Std.Ordinal.PlusIsAssociative", LemmaKind, "77370263028849d94c8e3c9710fde5b31232a702f573f3c8d058eac6737727fe", "1e89b2229497a7de8f7cb739e3dd1d25dc34ee541c310080dba73b24fcb5cf4b", OrdinalLemmaReason),
    new("Std.Ordinal.PlusStrictlyIncreasingOnRight", LemmaKind, "24d9b13ef2469074d4bd8ba66fe607c560f7c813803e771b3c76312dd9bda89f", "1e89b2229497a7de8f7cb739e3dd1d25dc34ee541c310080dba73b24fcb5cf4b", OrdinalLemmaReason),
    new("Std.Ordinal.Succ", LemmaKind, "f084528542d58077ea04de89f273af2ac82fc78c049f2d86624c2a0350e51948", "1e89b2229497a7de8f7cb739e3dd1d25dc34ee541c310080dba73b24fcb5cf4b", OrdinalLemmaReason),
    new("Std.Ordinal.TimesDistributesOnLeft", LemmaKind, "e4528df57f3ab44e10ea5d0a42610107732cec0172ffde1e92ff363cda245a1e", "1e89b2229497a7de8f7cb739e3dd1d25dc34ee541c310080dba73b24fcb5cf4b", OrdinalLemmaReason),
    new("Std.Ordinal.TimesIncreasingOnLeft", LemmaKind, "5b44809184e1ea84953d2f61873429ff46f1eced6764ce83c42943e3f419dcba", "1e89b2229497a7de8f7cb739e3dd1d25dc34ee541c310080dba73b24fcb5cf4b", OrdinalLemmaReason),
    new("Std.Ordinal.TimesStrictlyIncreasingOnRight", LemmaKind, "4e847365a9b4f1b806e2fb45138549f586e5de74ba272f064f5fa6de94c5f31e", "1e89b2229497a7de8f7cb739e3dd1d25dc34ee541c310080dba73b24fcb5cf4b", OrdinalLemmaReason),
    new("Std.Strings.HexConversion.CharsConsistent", LemmaKind, "b6e08ac0a93292eb9cccc3b0de7b02effc370cac04f7f5480b5e610c2a1aa3f4", "e923bd6bfce672a1303b2c70285db99055865236562a926348e5db9b9806968d", CharsConsistentReason),
  ];

  /// <summary>
  /// The trust list that this run selects, or null for none.  It has an effect only under
  /// --modular-termination.
  /// </summary>
  public static string? Selected(DafnyOptions options) {
    if (!options.Get(CommonOptionBag.ModularTermination)) {
      return null;
    }
    var name = options.Get(CommonOptionBag.ModularTerminationTrust);
    return name is null or None ? null : name;
  }

  /// <summary>
  /// The line that the verifier's summary adds when a run's results assume trusted premises: those of
  /// the library it builds, if any, and those recorded by the libraries it admitted.  Null when there
  /// are none, so that the output of every other run is unchanged.
  /// </summary>
  public static string? Summary(IReadOnlyList<ModularTerminationPremise> own, string? ownList,
    IReadOnlyList<(string Library, string TrustList, ModularTerminationPremise Premise)> fromLibraries) {
    var count = own.Count + fromLibraries.Count;
    if (count == 0) {
      return null;
    }
    var lists = fromLibraries.Select(p => p.TrustList).ToList();
    if (own.Count != 0 && ownList != null) {
      lists.Add(ownList);
    }
    lists = lists.Distinct().Order(StringComparer.Ordinal).ToList();
    var libraries = fromLibraries.Select(p => p.Library).Distinct().Count();
    var from = libraries == 1 ? "1 library" : $"{libraries} libraries";
    var where = own.Count == 0 ? $"from {from}"
      : fromLibraries.Count == 0 ? "in this library"
      : $"{own.Count} in this library and {fromLibraries.Count} from {from}";
    return $"Under --modular-termination these results assume {count} trusted premise{(count == 1 ? "" : "s")} " +
           $"of the {string.Join(" and ", lists)} trust list{(lists.Count == 1 ? "" : "s")}, {where}; dafny audit lists them";
  }

  public static IReadOnlyList<ModularTerminationTrustEntry>? Entries(string? name) =>
    name == StandardLibrary ? StandardLibraryEntries : null;

  public static ModularTerminationTrustEntry? Find(string? name, string id) =>
    Entries(name)?.FirstOrDefault(e => e.Id == id);

  /// <summary>
  /// The digest of a whole list, its reasons included.
  /// </summary>
  public static string Sha256(string name) {
    var sb = new StringBuilder();
    sb.Append("trust_list\t").Append(name).Append('\n');
    foreach (var e in Entries(name) ?? []) {
      sb.Append(string.Join("\t", e.Id, e.Kind, e.StatementSha256, e.ContextSha256, e.Reason)).Append('\n');
    }
    return ModularTerminationRecords.Sha256(sb.ToString());
  }

  /// <summary>
  /// The digest of the premise list of a record, in its order.
  /// </summary>
  public static string PremisesSha256(IEnumerable<ModularTerminationPremise> premises) {
    return ModularTerminationRecords.Sha256(string.Concat(premises.Select(p =>
      $"{p.Id}\t{p.Kind}\t{p.StatementSha256}\t{p.ContextSha256}\n")));
  }

  /// <summary>
  /// The fully qualified name of a declaration that a trust list may name, or null: a lemma, or a ghost
  /// function or predicate, declared at module level, with {:axiom} or @Axiom, without a body, not
  /// {:extern} and not generated by Dafny.
  /// </summary>
  public static string? CandidateId(INode node) {
    if (node is not MemberDecl { EnclosingClass: DefaultClassDecl } member || KindOf(member) is null ||
        !member.HasAxiomAttribute || member.HasExternAttribute || member.IsGeneratedByDafny) {
      return null;
    }
    var module = member.EnclosingClass.EnclosingModuleDefinition.FullDafnyName;
    return module.Length == 0 ? member.Name : $"{module}.{member.Name}";
  }

  public static string? KindOf(MemberDecl member) => member switch {
    Lemma { Body: null } => LemmaKind,
    Function { Body: null, IsGhost: true, ByMethodBody: null } and not ExtremePredicate and not PrefixPredicate
      and not TwoStateFunction => GhostFunctionKind,
    _ => null
  };

  /// <summary>
  /// The digest of a declaration's statement: the declaration as Dafny's printer prints it after
  /// resolution, attributes, signature and specification included.
  /// </summary>
  public static string StatementSha256(MemberDecl member, DafnyOptions options) {
    var wr = new StringWriter { NewLine = "\n" };
    var printer = new Printer(wr, options, PrintModes.Serialization);
    switch (member) {
      case Function f:
        printer.PrintFunction(f, 0, false);
        break;
      case MethodOrConstructor m:
        printer.PrintMethod(m, 0, false);
        break;
    }
    return ModularTerminationRecords.Sha256(wr.ToString());
  }

  /// <summary>
  /// Computes the premises of a resolved program: each candidate whose id the selected list names, with
  /// its digests.  The context digests use the termination contracts of the program's own modules,
  /// computed from its retained text, and those of the libraries it loaded.
  /// </summary>
  public sealed class Evaluation {
    public IReadOnlyDictionary<MemberDecl, ModularTerminationPremise> Premises { get; }

    private Evaluation(IReadOnlyDictionary<MemberDecl, ModularTerminationPremise> premises) {
      Premises = premises;
    }

    public static async Task<(Evaluation? Evaluation, string? Error)> Compute(Program program, string programText, Uri uri) {
      var (contracts, error) = await ModularTerminationContracts.Compute(program.Options, programText, uri);
      if (contracts == null) {
        return (null, error);
      }
      return (Compute(program, contracts, program.Compilation.ModularTerminationImports), null);
    }

    /// <param name="ownUris">When given, only declarations in these files are considered.</param>
    public static Evaluation Compute(Program program, ModularTerminationContracts contracts,
      IReadOnlyList<ModularTerminationContractRef> imports, ISet<Uri>? ownUris = null) {
      var name = Selected(program.Options);
      var digests = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var import in imports) {
        digests[import.Module] = import.ContractSha256;
      }
      foreach (var export in contracts.Exports) {
        digests[export.Module] = export.ContractSha256;
      }
      var graph = new ContextGraph(program, digests.Keys.ToHashSet(StringComparer.Ordinal));
      var premises = new Dictionary<MemberDecl, ModularTerminationPremise>();
      foreach (var node in program.DefaultModule.Descendants()) {
        if (node is not MemberDecl member || CandidateId(member) is not { } id || Find(name, id) == null) {
          continue;
        }
        if (ownUris != null && (member.Origin?.Uri == null || !ownUris.Contains(member.Origin.Uri))) {
          continue;
        }
        var keys = graph.Closure(member.EnclosingClass.EnclosingModuleDefinition);
        var context = string.Concat(keys.Select(k => $"{k}\t{digests.GetValueOrDefault(k, "missing")}\n"));
        premises[member] = new ModularTerminationPremise(id, KindOf(member)!, StatementSha256(member, program.Options),
          ModularTerminationRecords.Sha256(context));
      }
      return new Evaluation(premises);
    }

    /// <summary>
    /// Why a computed premise does not match its entry in the selected list, or null.
    /// </summary>
    public static string? Mismatch(DafnyOptions options, ModularTerminationPremise premise) {
      var name = Selected(options);
      if (Find(name, premise.Id) is not { } entry) {
        return $"'{premise.Id}' is not on the {name ?? None} trust list";
      }
      var changed = new List<string>();
      if (entry.Kind != premise.Kind) {
        changed.Add($"its kind is {premise.Kind}, the list has {entry.Kind}");
      }
      if (entry.StatementSha256 != premise.StatementSha256) {
        changed.Add($"its statement digest is {premise.StatementSha256}, the list has {entry.StatementSha256}");
      }
      if (entry.ContextSha256 != premise.ContextSha256) {
        changed.Add($"its context digest is {premise.ContextSha256}, the list has {entry.ContextSha256}");
      }
      return changed.Count == 0 ? null
        : $"the trusted premise '{premise.Id}' does not match its entry in the {name} trust list: {string.Join("; ", changed)}";
    }
  }

  /// <summary>
  /// The import and refinement graph of a program, between the keys of its termination contracts: the
  /// default module's own declarations ("_default"), each top-level module, and each prefix-named
  /// module.  Key K depends on key K' when a module that belongs to K imports or refines a module that
  /// belongs to K', contains it, or is contained in it.
  /// </summary>
  private sealed class ContextGraph {
    private readonly HashSet<string> keys;
    private readonly Dictionary<string, SortedSet<string>> edges = new(StringComparer.Ordinal);

    public ContextGraph(Program program, HashSet<string> keys) {
      this.keys = keys;
      foreach (var module in Modules(program.DefaultModuleDef)) {
        var owners = Owners(module);
        foreach (var target in Dependencies(module).SelectMany(Targets)) {
          foreach (var owner in owners) {
            if (!edges.TryGetValue(owner, out var set)) {
              edges[owner] = set = new SortedSet<string>(StringComparer.Ordinal);
            }
            set.Add(target);
          }
        }
      }
    }

    public IReadOnlyList<string> Closure(ModuleDefinition module) {
      var result = new SortedSet<string>(StringComparer.Ordinal);
      var pending = new Stack<string>(Owners(module));
      while (pending.Count != 0) {
        var key = pending.Pop();
        if (!result.Add(key)) {
          continue;
        }
        foreach (var next in edges.GetValueOrDefault(key) ?? []) {
          pending.Push(next);
        }
      }
      return result.ToList();
    }

    private static IEnumerable<ModuleDefinition> Modules(ModuleDefinition module) {
      yield return module;
      foreach (var literal in module.TopLevelDecls.OfType<LiteralModuleDecl>()) {
        foreach (var nested in Modules(literal.ModuleDef)) {
          yield return nested;
        }
      }
    }

    private static IEnumerable<ModuleDefinition> Dependencies(ModuleDefinition module) {
      if (module.Implements?.Target.Decl?.Signature?.ModuleDef is { } refined) {
        yield return refined;
      }
      foreach (var decl in module.TopLevelDecls) {
        var target = decl switch {
          AliasModuleDecl alias => alias.Signature?.ModuleDef ?? alias.TargetQId?.Decl?.Signature?.ModuleDef,
          AbstractModuleDecl abs => abs.OriginalSignature?.ModuleDef ?? abs.Signature?.ModuleDef,
          _ => null
        };
        if (target != null) {
          yield return target;
        }
      }
    }

    /// <summary>The keys a module belongs to.</summary>
    private IEnumerable<string> Owners(ModuleDefinition module) {
      var name = module.FullDafnyName;
      if (name.Length == 0) {
        return keys.Contains(ModularTerminationContracts.DefaultModuleKey) ? [ModularTerminationContracts.DefaultModuleKey] : [];
      }
      return keys.Where(k => k == name || name.StartsWith(k + ".", StringComparison.Ordinal)).ToList();
    }

    /// <summary>The keys a dependency on a module reaches: the keys it belongs to, and those of the modules it contains.</summary>
    private IEnumerable<string> Targets(ModuleDefinition module) {
      var name = module.FullDafnyName;
      return name.Length == 0 ? Owners(module)
        : Owners(module).Concat(keys.Where(k => k.StartsWith(name + ".", StringComparison.Ordinal)));
    }
  }
}
