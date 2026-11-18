#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using Tomlyn;
using Tomlyn.Model;

namespace Microsoft.Dafny;

/// <summary>
/// First closed typed snapshot schema: direct aliases of unrestricted, resolved literal modules.
/// The two records are an owned alias and an external signature reference. No type facts, bodies,
/// diagnostics, scopes or compiler objects are decoded from storage. All other results bypass.
/// </summary>
internal sealed class ResolutionSnapshots {
  internal const string Schema = "alias-reference-v1";
  // This inventory is part of the format identity, independently of the exact producer MVID.
  private const string SchemaInventory = "alias-reference-v1;CBOR=minimal-definite-arrays,unsigned,text-utf8-65535;" +
    "root=[schema,root-id,records];alias=[id=0,tag=1,graph-identity,target-id=1];" +
    "external=[id=1,tag=2,local-key,graph-identity,signature-selector,view=unrestricted-literal-signature];" +
    "current-alias-syntax-and-origin;Decl=external;Signature=external;Def=null;Sig=null;" +
    "owned-signatures=empty;class-members=empty;diagnostics=empty;builtin-requests=empty;manifest=tomlyn-v1";
  internal const int MaximumEntryBytes = 1024 * 1024;
  internal const int MaximumArchiveBytes = 3 * MaximumEntryBytes;
  private static readonly UTF8Encoding Utf8 = new(false, true);
  private readonly string directory;

  internal ResolutionSnapshots(string directory) {
    this.directory = directory;
  }

  internal sealed record Candidate(string Key, string Context, byte[] Manifest, byte[] Payload,
    AliasModuleDecl Alias, LiteralModuleDecl Target, ModuleSignature Signature);

  // Keep the complete sorted projection, not just the owner prefix: a dependency can contain
  // generated names even when this alias and its local key are unchanged.
  internal static string NamingContext(ModuleDecl owner, string key,
    IReadOnlyList<ModuleResolutionResult> prerequisites, ModuleNamePrefixes prefixes) {
    var projection = new List<string[]> { new[] { "owner", key, owner.FullName, prefixes.Prefix(owner.FullName) } };
    foreach (var prerequisite in prerequisites.OrderBy(p => p.ResolvedDeclaration.FullName, StringComparer.Ordinal)
               .ThenBy(p => p.Key, StringComparer.Ordinal)) {
      projection.Add(new[] { "prerequisite", prerequisite.Key, prerequisite.ResolvedDeclaration.FullName,
        prefixes.Prefix(prerequisite.ResolvedDeclaration.FullName) });
      foreach (var signature in prerequisite.Signatures.Keys.OrderBy(m => m.FullName, StringComparer.Ordinal)) {
        projection.Add(new[] { "signature", prerequisite.Key, signature.FullName, prefixes.Prefix(signature.FullName) });
      }
    }
    return JsonSerializer.Serialize(projection);
  }

  internal static Candidate? Prepare(ModuleDecl owner, string key,
    IReadOnlyList<ModuleResolutionResult> prerequisites, ModuleNamePrefixes prefixes,
    IReadOnlyList<ModuleResolutionKey.Prerequisite> directPrerequisites) {
    // These checks define the whole supported schema; unsupported/partial/error graphs do not load
    // or publish. An alias's source graph is current, private input, not yesterday's live result.
    if (!owner.Options.Get(CommonOptionBag.TypeSystemRefresh) || !owner.Options.Plugins.SequenceEqual(DafnyOptions.DefaultPlugins) ||
        owner is not AliasModuleDecl alias || alias.Attributes != null || alias.Exports.Count != 0 ||
        alias.TargetQId.Path.Count != 1 || alias.TargetQId.RootIndex != 0 ||
        alias.TargetQId.Sig != null || alias.TargetQId.Def != null ||
        alias.TargetQId.Root is not LiteralModuleDecl target || target.CopyImport != null ||
        target.CopyImportView != null || target.Signature == null || target.Signature.ExportSets.Count != 0 ||
        !ReferenceEquals(target.Signature, target.DefaultExport) || !target.ModuleDef.SuccessfullyResolved ||
        prerequisites.Any(p => p.ErrorReporter.AllMessages.Any() || p.Signatures.Keys.Any(m => !m.SuccessfullyResolved)) ||
        alias.Signature != null && !ReferenceEquals(alias.Signature, target.Signature) ||
        alias.TargetQId.Decl != null && !ReferenceEquals(alias.TargetQId.Decl, target)) {
      return null;
    }
    // Graph identity plus full key, kind, and view select a single *current* prerequisite.
    var matches = prerequisites.Where(p => ReferenceEquals(p.ResolvedDeclaration, target)).ToArray();
    if (matches.Length != 1 || matches[0].Key == null || key.Length != 64) { return null; }
    var targetOutput = matches[0];
    if (!targetOutput.Signatures.TryGetValue(target.ModuleDef, out var signature) ||
        !ReferenceEquals(signature, target.Signature)) { return null; }
    if (new[] { alias.FullName, target.FullName, target.ModuleDef.FullName }
        .Any(name => Utf8.GetByteCount(name) > ushort.MaxValue)) { return null; }
    var naming = NamingContext(owner, key, prerequisites, prefixes);
    var context = Hash(Utf8.GetBytes("dafny-resolution-naming-v1\n" + naming));
    // Canonical CBOR: [schema, root-id, [[alias-id, tag, identity, target-id],
    //                                 [target-id, tag, key, identity, signature, view]]].
    // Closed record/field inventory; small integer IDs retain the one external-reference edge.
    using var payload = new MemoryStream();
    payload.WriteByte(0x83); WriteText(payload, Schema); payload.WriteByte(0);
    payload.WriteByte(0x82);
    payload.WriteByte(0x84); payload.WriteByte(0); payload.WriteByte(1);
    WriteText(payload, alias.FullName); payload.WriteByte(1);
    payload.WriteByte(0x86); payload.WriteByte(1); payload.WriteByte(2);
    WriteText(payload, targetOutput.Key); WriteText(payload, target.FullName);
    WriteText(payload, target.ModuleDef.FullName); WriteText(payload, "unrestricted-literal-signature");
    var bytes = payload.ToArray();
    var options = JsonSerializer.Serialize(ModuleResolutionKey.Options(owner.Options)
      .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new[] { p.Key, p.Value }));
    var dependencies = JsonSerializer.Serialize(directPrerequisites.OrderBy(p => p.Role, StringComparer.Ordinal)
      .ThenBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Key, StringComparer.Ordinal)
      .Select(p => new[] { p.Role, p.Name, p.Key }));
    var manifest = new TomlTable { ["format_version"] = 1 };
    void Field(string name, string value) => manifest.Add(name, value);
    Field("dafny_version", owner.Options.VersionNumber);
    Field("producer_fingerprint", ModuleResolutionKey.DafnyVersion);
    Field("resolution_schema", Hash(Utf8.GetBytes(SchemaInventory)));
    Field("profile", "refreshed-direct-alias-default-plugins-v1");
    Field("module_key", key); Field("module_identity", owner.FullName);
    Field("options", options); Field("prerequisites", dependencies);
    Field("naming_context", naming); Field("naming_context_sha256", context);
    manifest.Add("payload_length", bytes.Length);
    Field("payload_sha256", Hash(bytes));
    var manifestBytes = EncodeManifest(manifest);
    if (manifestBytes.Length > MaximumEntryBytes || bytes.Length > MaximumEntryBytes) { return null; }
    return new(key, context, manifestBytes, bytes, alias, target, signature);
  }

  internal static byte[] EncodeManifest(TomlTable manifest) => Utf8.GetBytes(Toml.FromModel(manifest));

  internal string PathFor(Candidate candidate) => Path.Combine(directory, candidate.Key + "." + candidate.Context + ".dres");

  internal bool TryRead(Candidate candidate, CancellationToken cancellationToken, out string state) {
    state = "miss";
    try {
      cancellationToken.ThrowIfCancellationRequested();
      var path = PathFor(candidate);
      if (File.Exists(path + ".conflict")) { state = "rejected"; return false; }
      if (!File.Exists(path)) { return false; }
      using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
      if (file.Length > MaximumArchiveBytes) { state = "rejected"; return false; }
      using var zip = new ZipArchive(file, ZipArchiveMode.Read);
      // Exact canonical manifests reject unknown/duplicate/malformed fields, stale producer/options,
      // naming projections and checksums. Exact typed records reject unknown tags/IDs/references,
      // even if an altered payload has a newly computed checksum. Nothing is partially materialized.
      if (zip.Entries.Count != 2 || zip.Entries[0].FullName != "manifest.toml" ||
          zip.Entries[1].FullName != "module.cbor" ||
          !EqualEntry(zip.Entries[0], candidate.Manifest) || !EqualEntry(zip.Entries[1], candidate.Payload) ||
          File.Exists(path + ".conflict")) {
        state = "rejected";
        return false;
      }
      cancellationToken.ThrowIfCancellationRequested();
      state = "hit";
      return true;
    } catch (Exception error) when (StorageFailure(error)) {
      state = "rejected";
      return false;
    }
  }

  internal static ModuleResolutionResult Materialize(Candidate candidate) {
    // Only mutate this compilation's parsed alias, after the entire file was accepted. Its tokens,
    // visibility scopes, options and parent are already current; the target is the current prerequisite.
    var reporter = new BatchErrorReporter(candidate.Alias.Options);
    // Restore the typed edge without invoking any target-resolution routine. SetTarget would
    // additionally populate Def/Sig, which the ordinary alias resolver leaves unset.
    candidate.Alias.TargetQId.RestoreAliasTarget(candidate.Target);
    candidate.Alias.Signature = candidate.Signature;
    return new(candidate.Alias, reporter, new(), new());
  }

  internal void Publish(Candidate candidate, ModuleResolutionResult result, CancellationToken cancellationToken) {
    // This is a control on the producer as well as on the loader: only the exact supported result
    // can publish, and no diagnostic is silently omitted by the small schema.
    if (!ReferenceEquals(result.ResolvedDeclaration, candidate.Alias) || result.ErrorReporter.AllMessages.Any() ||
        result.Signatures.Count != 0 || result.ClassMembers.Count != 0 ||
        !ReferenceEquals(candidate.Alias.Signature, candidate.Signature) ||
        !ReferenceEquals(candidate.Alias.TargetQId.Decl, candidate.Target) ||
        candidate.Alias.TargetQId.Sig != null || candidate.Alias.TargetQId.Def != null) { return; }
    string? temporary = null;
    try {
      cancellationToken.ThrowIfCancellationRequested();
      if (!Directory.Exists(directory)) {
        if (OperatingSystem.IsWindows()) { Directory.CreateDirectory(directory); }
        else { Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
      }
      var final = PathFor(candidate);
      if (File.Exists(final + ".conflict")) { return; }
      temporary = Path.Combine(directory, ".snapshot-" + Guid.NewGuid().ToString("N") + ".tmp");
      var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
      if (!OperatingSystem.IsWindows()) { options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; }
      using (var file = new FileStream(temporary, options)) {
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true)) {
          WriteEntry(zip, "manifest.toml", candidate.Manifest);
          WriteEntry(zip, "module.cbor", candidate.Payload);
        }
        file.Flush(true);
      }
      cancellationToken.ThrowIfCancellationRequested();
      try { File.Move(temporary, final, false); }
      catch (IOException) when (File.Exists(final)) {
        if (!TryRead(candidate, cancellationToken, out _)) {
          // Never replace a final record, including a corrupt one. A dispute disables the pair.
          using var marker = new FileStream(final + ".conflict", FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
      }
    } catch (Exception error) when (StorageFailure(error)) {
      // Read-only stores, interrupted/competing publishers and storage failures cannot change resolution.
    } finally {
      if (temporary != null) {
        try { File.Delete(temporary); } catch (Exception error) when (StorageFailure(error)) { }
      }
    }
  }

  private static bool EqualEntry(ZipArchiveEntry entry, byte[] expected) {
    if (entry.Length != expected.Length || entry.Length > MaximumEntryBytes) { return false; }
    using var input = entry.Open();
    var bytes = new byte[expected.Length];
    input.ReadExactly(bytes);
    return input.ReadByte() == -1 && expected.AsSpan().SequenceEqual(bytes);
  }

  private static void WriteEntry(ZipArchive archive, string name, byte[] bytes) {
    var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
    entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    using var stream = entry.Open(); stream.Write(bytes);
  }

  private static void WriteText(Stream stream, string text) {
    var bytes = Utf8.GetBytes(text);
    if (bytes.Length < 24) { stream.WriteByte((byte)(0x60 + bytes.Length)); }
    else if (bytes.Length <= byte.MaxValue) { stream.WriteByte(0x78); stream.WriteByte((byte)bytes.Length); }
    else if (bytes.Length <= ushort.MaxValue) {
      stream.WriteByte(0x79); stream.WriteByte((byte)(bytes.Length >> 8)); stream.WriteByte((byte)bytes.Length);
    } else { throw new ArgumentException("Snapshot string exceeds the schema limit"); }
    stream.Write(bytes);
  }

  private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
  private static bool StorageFailure(Exception error) => error is IOException or UnauthorizedAccessException or
    InvalidDataException or SecurityException or NotSupportedException or ArgumentException;
}
