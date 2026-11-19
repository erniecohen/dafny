#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using Microsoft.Dafny;

namespace DafnyCore;

/// <summary>
/// The group inventory of a .doo built with --recursive-modules. This binds the complete retained
/// source to its group partition; it is an integrity record, with the ordinary .doo trust model,
/// not a signature or a proof certificate. Clients recompute the partition before resolution.
/// </summary>
public sealed class RecursiveModuleManifest {
  public long Schema { get; set; } = 1;
  public string ProgramSha256 { get; set; } = "";
  public string ManifestSha256 { get; set; } = "";
  public List<RecursiveModuleRecord> Groups { get; set; } = [];
  public string RecordSha256 { get; set; } = "";

  public string Digest() => ModularTerminationRecords.Sha256(JsonSerializer.Serialize(
    new { Schema, ProgramSha256, ManifestSha256, Groups }));

  public static string ManifestDigest(DooFile.ManifestData manifest) {
    // Normalize enums through the same TOML conversion as the archive writer, and sort options:
    // the in-memory producer and the deserialized consumer may use different CLR value types.
    var header = new DooFile.ManifestData {
      DooFileVersion = manifest.DooFileVersion,
      DafnyVersion = manifest.DafnyVersion,
      SolverIdentifier = manifest.SolverIdentifier,
      SolverVersion = manifest.SolverVersion,
      Options = manifest.Options.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary()
    };
    using var writer = new StringWriter();
    header.Write(writer);
    return ModularTerminationRecords.Sha256(writer.ToString());
  }

  public static RecursiveModuleManifest Create(DooFile file, IEnumerable<RecursiveModuleGroup> groups) {
    var record = new RecursiveModuleManifest {
      ProgramSha256 = ModularTerminationRecords.Sha256(file.ProgramText),
      ManifestSha256 = ManifestDigest(file.Manifest),
      Groups = Inventory(groups)
    };
    record.RecordSha256 = record.Digest();
    return record;
  }

  public static List<RecursiveModuleRecord> Inventory(IEnumerable<RecursiveModuleGroup> groups) => groups
    .Select(g => new RecursiveModuleRecord {
      Members = g.Members.Select(m => m.ModuleDef.FullDafnyName).Order(StringComparer.Ordinal).ToList(),
      Abstract = g.Members[0].ModuleDef.ModuleKind == ModuleKindEnum.Abstract
    }).OrderBy(g => g.Members[0], StringComparer.Ordinal).ToList();

  public bool Matches(IEnumerable<RecursiveModuleGroup> groups) =>
    JsonSerializer.Serialize(Groups) == JsonSerializer.Serialize(Inventory(groups));

  /// <summary>
  /// An archive is one producer unit. No component can contain both its declarations and another
  /// archive's or the client's declarations. Even a consistently rehashed record must describe the
  /// partition computed from the loaded declarations, rather than supplying resolution facts.
  /// </summary>
  public static void CheckBoundaries(Program program, IReadOnlyList<RecursiveModuleGroup> groups,
    ErrorReporter reporter) {
    var archives = program.Compilation.RecursiveModuleLibraries;
    foreach (var group in groups) {
      var archiveMembers = group.Members.Where(m => archives.ContainsKey(m.Origin.Uri)).ToList();
      // Preserve the source/include unit used before archive support. ShouldVerify also excludes
      // ordinary included declarations, which may still belong to this source unit.
      _ = group.Members[0].ShouldVerify(program.Compilation);
      var unverified = group.Members.Any(m => program.Compilation.UrisToVerify?.Contains(m.Origin.Uri) == false);
      if (archiveMembers.Count != 0 && group.Members.Any(m => m.Origin.Uri != archiveMembers[0].Origin.Uri) ||
          unverified && archiveMembers.Count != group.Members.Count) {
        reporter.Error(MessageSource.Resolver, group.Members[0].Origin,
          "a recursive module group must be wholly contained in the program's sources or in one .doo library; " +
          "a client cannot join or extend a library group");
      }
    }
    foreach (var (uri, record) in archives) {
      var actual = groups.Where(g => g.Members.Any(m => m.Origin.Uri == uri)).ToList();
      if (record == null ? actual.Count != 0 : !record.Matches(actual)) {
        reporter.Error(MessageSource.Resolver, Token.Cli,
          "a .doo library's recursive module group inventory does not match its loaded declarations; " +
          "a group cannot be split, extended or partially loaded");
      }
    }
  }
}

public sealed class RecursiveModuleRecord {
  public List<string> Members { get; set; } = [];
  public bool Abstract { get; set; }
}
