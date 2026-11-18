#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dafny;

namespace DafnyCore;

/// <summary>
/// Explicit runtime assumptions, separate from M5b's ghost premises. The allowlist pins both the
/// statement and its interpretation context. No pattern, namespace or user-supplied list is trusted.
/// The operational promise includes total termination; this is assumed, not a runtime liveness proof.
/// </summary>
public static class ModularTerminationRuntimeTrust {
  public const string StandardLibrary = "standard-library";
  public const string Option = "--modular-termination-runtime-trust:standard-library";
  private static readonly ModularTerminationTrustEntry[] Entries = ReadEntries();

  private static ModularTerminationTrustEntry[] ReadEntries() {
    using var stream = typeof(ModularTerminationRuntimeTrust).Assembly
      .GetManifestResourceStream("ModularTerminationRuntimeContracts.tsv")!;
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd().Split('\n').Where(l => l.Length != 0 && !l.StartsWith('#'))
      .Select(l => l.TrimEnd('\r').Split('\t')).Select(p => p.Length == 5
        ? new ModularTerminationTrustEntry(p[0], p[1], p[2], p[3], p[4])
        : throw new InvalidDataException("Malformed runtime contract list"))
      .OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
  }

  public static string? Selected(DafnyOptions options) =>
    ModularTermination.RulesEnabled(options) &&
    options.Get(CommonOptionBag.ModularTerminationRuntimeTrust) == StandardLibrary ? StandardLibrary : null;

  public static string? KindOf(MemberDecl member) => member switch {
    MethodOrConstructor { IsGhost: false } => "runtime method (including termination)",
    Function { IsGhost: false } => "runtime function (including termination)",
    _ => null
  };

  public static string? CandidateId(INode? node) {
    if (node is not MemberDecl member || KindOf(member) == null || member.IsGeneratedByDafny ||
        member.EnclosingClass.EnclosingModuleDefinition.ModuleKind == ModuleKindEnum.Abstract ||
        member.EnclosingClass is TraitDecl && !member.IsStatic) {
      return null;
    }
    var bodyless = member is MethodOrConstructor { Body: null } or Function { Body: null };
    if (!member.HasExternAttribute && !bodyless) {
      return null;
    }
    var module = member.EnclosingClass.EnclosingModuleDefinition.FullDafnyName;
    var owner = member.EnclosingClass is DefaultClassDecl ? "" : member.EnclosingClass.Name + ".";
    return (module.Length == 0 ? "" : module + ".") + owner + member.Name;
  }

  public static ModularTerminationTrustEntry? Find(string id) => Entries.FirstOrDefault(e => e.Id == id);
  public static string Sha256() => ModularTerminationRecords.Sha256(string.Concat(Entries.Select(e =>
    $"{e.Id}\t{e.Kind}\t{e.StatementSha256}\t{e.ContextSha256}\t{e.Reason}\n")));

  public static string? Mismatch(ModularTerminationPremise premise) {
    if (Entries.Any(e => e.Id == premise.Id && e.Kind == premise.Kind &&
        e.StatementSha256 == premise.StatementSha256 && e.ContextSha256 == premise.ContextSha256)) {
      return null;
    }
    var entry = Find(premise.Id);
    if (entry == null) {
      return $"runtime contract '{premise.Id}' is missing from the reviewed runtime list";
    }
    return entry.Kind == premise.Kind && entry.StatementSha256 == premise.StatementSha256 &&
           entry.ContextSha256 == premise.ContextSha256 ? null :
      $"runtime contract '{premise.Id}' differs from its reviewed statement or context";
  }

  public static string? Summary(IReadOnlyList<ModularTerminationPremise> own,
    IReadOnlyList<(string Library, string TrustList, ModularTerminationPremise Premise)> imported) {
    var names = own.Select(p => p.Id).Concat(imported.Select(p => p.Premise.Id))
      .Distinct().Order(StringComparer.Ordinal).ToList();
    return names.Count == 0 ? null : "Runtime contracts ASSUMED, including termination, under " + Option +
      ": " + string.Join(", ", names) + ". These results are conditional on every named runtime contract.";
  }
}
