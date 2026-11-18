using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Dafny;
using Xunit;
using DafnyType = Microsoft.Dafny.Type;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test.Resolver;

[Collection("Cardinality resolution")]
public class ResolutionSnapshotTests : IDisposable {
  private const string Source = """
    module Base { const N: int := 1 }
    module Client { import B = Base lemma Good() ensures B.N == 1 {} }
    """;
  private readonly string directory = Path.Combine(Path.GetTempPath(), "resolution-snapshot-" + Guid.NewGuid().ToString("N"));
  private int serial;

  public ResolutionSnapshotTests() { Directory.CreateDirectory(directory); }
  public void Dispose() { Directory.Delete(directory, true); }

  private sealed record Observation(Program Program, JsonElement[] Events, IReadOnlyList<ModuleResolutionResult> Outputs) {
    public JsonElement Alias => Events.Single(e => e.GetProperty("module").GetString() == "Client.B");
  }

  private sealed class CaptureResolver(Program program) : ProgramResolver(program) {
    public readonly System.Collections.Concurrent.ConcurrentBag<ModuleResolutionResult> Outputs = new();
    protected override ModuleResolutionResult ResolveModuleOutput(ModuleDecl declaration, string key,
      IReadOnlyList<ModuleResolutionResult> prerequisites, Func<ModuleResolutionResult> resolve, out bool cached) {
      var output = base.ResolveModuleOutput(declaration, key, prerequisites, resolve, out cached);
      Outputs.Add(output); return output;
    }
  }

  private async Task<Observation> Resolve(string source = Source, string store = "cache", bool enabled = true,
    Action<DafnyOptions>? configure = null, bool errors = false, int threads = 0) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.ResolutionThreads, threads);
    var profile = Path.Combine(directory, "profile-" + serial++ + ".jsonl");
    options.Set(CommonOptionBag.PipelineProfile, profile);
    if (enabled) { options.Set(CommonOptionBag.ResolutionCache, Path.Combine(directory, store)); }
    configure?.Invoke(options);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri("file:///snapshot.dfy"), reporter);
    var resolver = new CaptureResolver(parsed.Program);
    await resolver.Resolve(CancellationToken.None);
    PipelineProfile.For(options)!.Finish("test");
    Assert.Equal(errors, reporter.HasErrors);
    var events = File.ReadLines(profile).Select(s => JsonDocument.Parse(s).RootElement.Clone())
      .Where(e => e.TryGetProperty("stage", out var stage) && stage.GetString() == "resolve.snapshot").ToArray();
    return new(parsed.Program, events, resolver.Outputs.ToArray());
  }

  private string Archive(string store = "cache") => Directory.GetFiles(Path.Combine(directory, store), "*.dres").Single();
  private static string State(JsonElement e) => e.GetProperty("state").GetString()!;
  private static string Boogie(Program program) => string.Join("\n", program.RawModules().OrderBy(m => m.FullName)
    .Select(m => ModuleTranslationCache.Emit(new BoogieGenerator(program.Reporter, new ProofDependencyManager(),
      new(program.Options)).DoTranslation(program, m), program.Options)));

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(3)]
  public async Task WarmLoadSkipsExactlyTheAliasAndUsesCurrentPrerequisite(int threads) {
    var cold = await Resolve(threads: threads);
    Assert.Equal("miss", State(cold.Alias));
    var bytes = File.ReadAllBytes(Archive());
    using (var zip = ZipFile.OpenRead(Archive())) {
      Assert.Equal(new[] { "manifest.toml", "module.cbor" }, zip.Entries.Select(e => e.FullName));
      Assert.All(zip.Entries, entry => Assert.Equal(entry.Length, entry.CompressedLength));
    }
    var warm = await Resolve(threads: threads);
    Assert.Equal("hit", State(warm.Alias));
    Assert.Equal(0, warm.Alias.GetProperty("resolverCalls").GetInt32());
    Assert.All(warm.Events.Where(e => e.GetProperty("module").GetString() != "Client.B"), e => {
      Assert.Equal("bypass", State(e)); Assert.Equal(1, e.GetProperty("resolverCalls").GetInt32());
    });
    Assert.Equal(bytes, File.ReadAllBytes(Archive()));
    Assert.True(warm.Program.CardinalityValidationReceipt?.Succeeded);
    Assert.Equal(Boogie(cold.Program), Boogie(warm.Program));
    var alias = warm.Program.DefaultModule.ModuleDef.TopLevelDecls.OfType<LiteralModuleDecl>()
      .Single(m => m.Name == "Client").ModuleDef.TopLevelDecls.OfType<AliasModuleDecl>().Single();
    var target = warm.Program.DefaultModule.ModuleDef.TopLevelDecls.OfType<LiteralModuleDecl>().Single(m => m.Name == "Base");
    Assert.Same(target, alias.TargetQId.Decl);
    Assert.Null(alias.TargetQId.Def);
    Assert.Null(alias.TargetQId.Sig);
    Assert.Same(target.Signature, alias.Signature);
    Assert.NotSame(cold.Program.DefaultModule.ModuleDef.TopLevelDecls.OfType<LiteralModuleDecl>().Single(m => m.Name == "Base"), target);
  }

  [Fact]
  public async Task OffHasNoCacheEventsOrFilesAndMatchesColdAndWarmOutput() {
    var off = await Resolve(enabled: false);
    Assert.Empty(off.Events);
    Assert.False(Directory.Exists(Path.Combine(directory, "cache")));
    var cold = await Resolve(); var warm = await Resolve();
    Assert.Equal(Boogie(off.Program), Boogie(cold.Program));
    Assert.Equal(Boogie(off.Program), Boogie(warm.Program));
  }

  private static void Rewrite(string path, Func<string, byte[], byte[]> edit, string? extra = null) {
    using var old = new ZipArchive(new MemoryStream(File.ReadAllBytes(path)), ZipArchiveMode.Read);
    using var bytes = new MemoryStream();
    using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true)) {
      foreach (var entry in old.Entries) {
        using var input = entry.Open(); using var data = new MemoryStream(); input.CopyTo(data);
        using var output = zip.CreateEntry(entry.FullName).Open(); output.Write(edit(entry.FullName, data.ToArray()));
      }
      if (extra != null) { using var output = zip.CreateEntry(extra).Open(); output.WriteByte(0); }
    }
    File.WriteAllBytes(path, bytes.ToArray());
  }

  [Theory]
  [InlineData("payload")]
  [InlineData("manifest")]
  [InlineData("tag-with-checksum")]
  [InlineData("duplicate")]
  [InlineData("traversal")]
  [InlineData("truncated")]
  [InlineData("oversized")]
  [InlineData("producer")]
  [InlineData("context")]
  [InlineData("zip-crc")]
  [InlineData("zip-local-crc")]
  [InlineData("zip-metadata")]
  public async Task MalformedOrStaleBytesAreRejected(string mutation) {
    var cold = await Resolve(); var path = Archive();
    if (mutation == "truncated") { File.WriteAllBytes(path, new byte[] { 0x50, 0x4b }); }
    else if (mutation.StartsWith("zip-", StringComparison.Ordinal)) {
      var bytes = File.ReadAllBytes(path);
      var local = bytes.AsSpan().IndexOf(new byte[] { 0x50, 0x4b, 3, 4 });
      var central = bytes.AsSpan().IndexOf(new byte[] { 0x50, 0x4b, 1, 2 });
      if (mutation == "zip-metadata") {
        // A valid alternate timestamp is still outside the canonical producer format.
        bytes[local + 10] ^= 1; bytes[central + 12] ^= 1;
      } else {
        bytes[local + 14] ^= 1;
        if (mutation == "zip-crc") { bytes[central + 16] ^= 1; }
      }
      File.WriteAllBytes(path, bytes);
    }
    else if (mutation == "oversized") {
      // A valid oversized ZIP exercises bounded rejection without a ZIP parsing failure.
      var original = File.ReadAllBytes(path);
      var central = original.AsSpan().IndexOf(new byte[] { 0x50, 0x4b, 1, 2 });
      var end = original.AsSpan().LastIndexOf(new byte[] { 0x50, 0x4b, 5, 6 });
      var padding = ResolutionSnapshots.MaximumArchiveBytes;
      var expanded = new byte[original.Length + padding];
      original.AsSpan(0, central).CopyTo(expanded);
      original.AsSpan(central).CopyTo(expanded.AsSpan(central + padding));
      System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(expanded.AsSpan(end + padding + 16), (uint)(central + padding));
      File.WriteAllBytes(path, expanded);
    }
    else {
      byte[]? altered = null;
      if (mutation == "tag-with-checksum") {
        using var zip = ZipFile.OpenRead(path); using var input = zip.GetEntry("module.cbor")!.Open();
        using var output = new MemoryStream(); await input.CopyToAsync(output); altered = output.ToArray(); altered[0] ^= 1;
      }
      Rewrite(path, (name, data) => {
        if (name == "module.cbor" && mutation == "payload") { data[^1] ^= 1; }
        if (name == "module.cbor" && altered != null) { return altered; }
        if (name == "manifest.toml") {
          var text = Encoding.UTF8.GetString(data);
          if (mutation == "manifest") { text += "format_version = 1\n"; }
          if (mutation == "producer") { text = text.Replace("resolver-mvid:", "other-producer:"); }
          if (mutation == "context") { text = text.Replace("naming_context_sha256 = \"", "naming_context_sha256 = \"0"); }
          if (altered != null) {
            using var zip = ZipFile.OpenRead(path); using var input = zip.GetEntry("module.cbor")!.Open();
            using var original = new MemoryStream(); input.CopyTo(original);
            text = text.Replace(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(original.ToArray())),
              Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(altered)));
          }
          return Encoding.UTF8.GetBytes(text);
        }
        return data;
      }, mutation == "duplicate" ? "module.cbor" : mutation == "traversal" ? "../unexpected" : null);
    }
    var rejected = await Resolve();
    Assert.Equal("rejected", State(rejected.Alias));
    Assert.Equal(1, rejected.Alias.GetProperty("resolverCalls").GetInt32());
    Assert.Equal(Boogie(cold.Program), Boogie(rejected.Program));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OptionsAndSourceChangesCannotUseRelabelledOldPayload(bool sourceChange) {
    var before = await Resolve(); var old = File.ReadAllBytes(Archive());
    var source = sourceChange ? Source.Replace("N: int := 1", "N: int := 2") : Source;
    void Configure(DafnyOptions o) { if (!sourceChange) { o.Set(CommonOptionBag.ModularTermination, true); } }
    var expected = await Resolve(source, "changed", configure: Configure);
    Assert.NotEqual(before.Alias.GetProperty("key").GetString(), expected.Alias.GetProperty("key").GetString());
    File.WriteAllBytes(Archive("changed"), old);
    var rejected = await Resolve(source, "changed", configure: Configure);
    Assert.Equal("rejected", State(rejected.Alias));
    Assert.Equal(Boogie(expected.Program), Boogie(rejected.Program));
  }

  [Fact]
  public void DocumentedNamingTableCollisionIsPreserved() {
    var one = ModuleNamePrefixes.For(new[] { "SnapshotModule4829" });
    var two = ModuleNamePrefixes.For(new[] { "SnapshotModule4829", "SnapshotModule51712" });
    Assert.Equal("Mdwj6dt_", one.Prefix("SnapshotModule4829"));
    Assert.Equal("Mdwj6dtb_", two.Prefix("SnapshotModule4829"));
    Assert.Equal("Mdwj6dti_", two.Prefix("SnapshotModule51712"));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OwnerAndPrerequisitePrefixChangesUseDistinctContexts(bool owner) {
    var source = owner
      ? "module Base {} import SnapshotGraphModule6913 = Base"
      : "module SnapshotGraphModule6913 { datatype D = D(x: int) function F(d: D): D { d.(x := 1) } } module Client { import B = SnapshotGraphModule6913 }";
    var name = owner ? "_module.SnapshotGraphModule6913" : "Client.B";
    var first = await Resolve(source);
    var initial = first.Events.Single(e => e.GetProperty("module").GetString() == name);
    Assert.Equal("miss", State(initial));
    var oldFile = Archive();
    var changed = await Resolve(source + " module SnapshotGraphModule30048 {} ");
    var other = changed.Events.Single(e => e.GetProperty("module").GetString() == name);
    Assert.Equal(initial.GetProperty("key").GetString(), other.GetProperty("key").GetString());
    Assert.True(initial.GetProperty("context").GetString() != other.GetProperty("context").GetString(),
      string.Join(";", changed.Program.ResolutionKeys.Keys.Select(d => d.FullName + "=" + ModuleNamePrefixes.For(changed.Program.ResolutionKeys.Keys.Select(m => m.FullName)).Prefix(d.FullName))));
    Assert.Equal("miss", State(other));
    var files = Directory.GetFiles(Path.Combine(directory, "cache"), "*.dres"); Assert.Equal(2, files.Length);
    var newFile = files.Single(p => p != oldFile);
    File.Copy(oldFile, newFile, true);
    var relabelled = await Resolve(source + " module SnapshotGraphModule30048 {} ");
    Assert.Equal("rejected", State(relabelled.Events.Single(e => e.GetProperty("module").GetString() == name)));
    var original = await Resolve(source);
    Assert.Equal("hit", State(original.Events.Single(e => e.GetProperty("module").GetString() == name)));
  }

  [Fact]
  public async Task UnsupportedViewsErrorsAndCustomPluginProfilesBypass() {
    await Resolve("module Base { export E reveals * } module Client { import B = Base`E }", "views");
    Assert.False(Directory.Exists(Path.Combine(directory, "views")));
    await Resolve("module Base { const X: int := true } module Client { import B = Base }", "errors", errors: true);
    Assert.False(Directory.Exists(Path.Combine(directory, "errors")));
    await Resolve(store: "plugins", configure: o => o.AdditionalPlugins.Add(DafnyOptions.DefaultPlugins[0]));
    Assert.False(Directory.Exists(Path.Combine(directory, "plugins")));
    await Resolve(store: "legacy", configure: o => o.Set(CommonOptionBag.TypeSystemRefresh, false));
    Assert.False(Directory.Exists(Path.Combine(directory, "legacy")));
  }

  private static ResolutionSnapshots.Candidate Candidate(Observation observation, ModuleNamePrefixes? prefixes = null) {
    var alias = observation.Outputs.Single(p => p.ResolvedDeclaration is AliasModuleDecl);
    var target = observation.Outputs.Single(p => ReferenceEquals(p.ResolvedDeclaration,
      ((AliasModuleDecl)alias.ResolvedDeclaration).TargetQId.Root));
    return ResolutionSnapshots.Prepare(alias.ResolvedDeclaration, alias.Key, new[] { target },
      prefixes ?? ModuleNamePrefixes.For(observation.Outputs.Select(p => p.ResolvedDeclaration.FullName)),
      new[] { new ModuleResolutionKey.Prerequisite("import", target.ResolvedDeclaration.FullName, target.Key) })!;
  }

  [Fact]
  public async Task DocumentedCollisionInvalidatesSignatureSelectorContext() {
    var observation = await Resolve(Source.Replace("Base", "SnapshotModule4829"));
    var first = Candidate(observation, ModuleNamePrefixes.For(new[] { "SnapshotModule4829" }));
    var second = Candidate(observation, ModuleNamePrefixes.For(new[] { "SnapshotModule4829", "SnapshotModule51712" }));
    Assert.NotNull(first); Assert.NotNull(second);
    Assert.Equal(first.Key, second.Key); Assert.NotEqual(first.Context, second.Context);
    var store = new ResolutionSnapshots(Path.Combine(directory, "selector"));
    store.Publish(first, observation.Outputs.Single(p => p.ResolvedDeclaration is AliasModuleDecl), default);
    File.Copy(store.PathFor(first), store.PathFor(second));
    Assert.False(store.TryRead(second, default, out var state)); Assert.Equal("rejected", state);
  }

  [Fact]
  public async Task AtomicPublicationCancellationAndConflictingWritersFailClosed() {
    var observation = await Resolve(); var candidate = Candidate(observation);
    Assert.NotNull(candidate);
    var result = observation.Outputs.Single(p => p.ResolvedDeclaration is AliasModuleDecl);
    var store = new ResolutionSnapshots(Path.Combine(directory, "atomic"));
    using var cancelled = new CancellationTokenSource(); await cancelled.CancelAsync();
    Assert.ThrowsAny<OperationCanceledException>(() => store.Publish(candidate, result, cancelled.Token));
    Assert.False(Directory.Exists(Path.Combine(directory, "atomic")));
    Parallel.For(0, 3, _ => store.Publish(candidate, result, default));
    Assert.True(store.TryRead(candidate, default, out _));
    Assert.Single(Directory.GetFiles(Path.Combine(directory, "atomic")));
    Assert.ThrowsAny<OperationCanceledException>(() => store.TryRead(candidate, cancelled.Token, out _));
    var bytes = File.ReadAllBytes(store.PathFor(candidate));
    Rewrite(store.PathFor(candidate), (name, data) => { if (name == "module.cbor") { data[^1] ^= 1; } return data; });
    store.Publish(candidate, result, default);
    Assert.True(File.Exists(store.PathFor(candidate) + ".conflict"));
    File.WriteAllBytes(store.PathFor(candidate), bytes);
    Assert.False(store.TryRead(candidate, default, out var state)); Assert.Equal("rejected", state);
    Assert.Empty(Directory.GetFiles(Path.Combine(directory, "atomic"), "*.tmp"));
  }

  [Fact]
  public async Task ProducerRefusesAnUnexpectedResultShape() {
    var observation = await Resolve(); var candidate = Candidate(observation);
    var output = observation.Outputs.Single(p => p.ResolvedDeclaration is AliasModuleDecl);
    var store = new ResolutionSnapshots(Path.Combine(directory, "bad-producer"));
    var changed = output with { Signatures = new() { [candidate.Target.ModuleDef] = candidate.Signature } };
    store.Publish(candidate, changed, default);
    Assert.False(Directory.Exists(Path.Combine(directory, "bad-producer")));
  }

  [Fact]
  public async Task ALocalHitCannotAuthorizeAFailedCurrentGlobalPass() {
    void Configure(DafnyOptions o) => o.Set(CommonOptionBag.GeneralTraits, CommonOptionBag.GeneralTraitsOptions.Datatype);
    await Resolve(configure: Configure);
    var invalid = await Resolve(Source + " module Unrelated { trait V {} datatype D extends V = D(p: V -> bool) }",
      configure: Configure, errors: true);
    Assert.Equal("hit", State(invalid.Alias));
    Assert.False(invalid.Program.CardinalityValidationReceipt?.Succeeded == true);
    Assert.Throws<InvalidOperationException>(() => BoogieGenerator.Translate(invalid.Program, invalid.Program.Reporter).ToList());
  }

  [Fact]
  public async Task LocationOnlyEditsUseCurrentAliasOrigins() {
    var before = await Resolve();
    var after = await Resolve("\n\n" + Source);
    Assert.Equal("hit", State(after.Alias));
    var old = (AliasModuleDecl)before.Outputs.Single(p => p.ResolvedDeclaration is AliasModuleDecl).ResolvedDeclaration;
    var current = (AliasModuleDecl)after.Outputs.Single(p => p.ResolvedDeclaration is AliasModuleDecl).ResolvedDeclaration;
    Assert.Equal(old.Origin.line + 2, current.Origin.line);
    Assert.NotSame(old.Origin, current.Origin);
  }

  [Fact]
  public async Task EveryRandomContainerBitFlipIsAMiss() {
    var observation = await Resolve(); var candidate = Candidate(observation);
    var store = new ResolutionSnapshots(Path.Combine(directory, "fuzz"));
    var output = observation.Outputs.Single(p => p.ResolvedDeclaration is AliasModuleDecl);
    store.Publish(candidate, output, default);
    var path = store.PathFor(candidate); var bytes = File.ReadAllBytes(path); var random = new Random(241);
    for (var i = 0; i < 100; i++) {
      var changed = (byte[])bytes.Clone(); changed[random.Next(changed.Length)] ^= (byte)(1 << random.Next(8));
      File.WriteAllBytes(path, changed);
      Assert.False(store.TryRead(candidate, default, out var state));
      Assert.Equal("rejected", state);
    }
  }

  [Fact]
  public void ManifestUsesTomlStringRulesForUnicodeMetadata() {
    const string identity = "Unicode 𐐀 \"quoted\" \\ metadata";
    var table = new Tomlyn.Model.TomlTable { ["module_identity"] = identity };
    var bytes = ResolutionSnapshots.EncodeManifest(table);
    var decoded = Tomlyn.Toml.ToModel(Encoding.UTF8.GetString(bytes));
    Assert.Equal(identity, decoded["module_identity"]);
    Assert.Equal(bytes, ResolutionSnapshots.EncodeManifest(table));
  }

  [Theory]
  [InlineData("attributes")]
  [InlineData("exports")]
  [InlineData("qualified-path")]
  [InlineData("enclosing-root")]
  [InlineData("signature-field")]
  [InlineData("definition-field")]
  [InlineData("target-kind")]
  [InlineData("copy-import")]
  [InlineData("copy-view")]
  [InlineData("missing-signature")]
  [InlineData("export-view")]
  [InlineData("default-view")]
  [InlineData("partial-target")]
  [InlineData("diagnostic")]
  [InlineData("alias-signature")]
  [InlineData("alias-target")]
  [InlineData("missing-prerequisite")]
  [InlineData("ambiguous-prerequisite")]
  [InlineData("missing-key")]
  [InlineData("malformed-key")]
  [InlineData("missing-selector")]
  [InlineData("wrong-selector")]
  [InlineData("removed-plugin")]
  [InlineData("reordered-plugins")]
  public async Task UnsupportedProfileStatesHaveNoCandidate(string state) {
    var observation = await Resolve();
    var output = observation.Outputs.Single(p => p.ResolvedDeclaration is AliasModuleDecl);
    var alias = (AliasModuleDecl)output.ResolvedDeclaration;
    var prerequisite = observation.Outputs.Single(p => ReferenceEquals(p.ResolvedDeclaration, alias.TargetQId.Root));
    var target = (LiteralModuleDecl)prerequisite.ResolvedDeclaration;
    var inputs = new[] { prerequisite }; var key = output.Key;
    switch (state) {
      case "attributes": alias.Attributes = new Attributes("unsupported", [], null); break;
      case "exports": alias.Exports.Add(Token.NoToken); break;
      case "qualified-path": alias.TargetQId.Path.Add(alias.TargetQId.Path[0]); break;
      case "enclosing-root": alias.TargetQId.RootIndex = 1; break;
      case "signature-field": alias.TargetQId.Sig = target.Signature; break;
      case "definition-field": alias.TargetQId.SetTarget(target); alias.TargetQId.Sig = null; break;
      case "target-kind": alias.TargetQId.Root = alias; break;
      case "copy-import": target.CopyImport = new CopyImportInfo(false, []); break;
      case "copy-view": target.CopyImportView = new ModuleSignature(); break;
      case "missing-signature": target.Signature = null; break;
      case "export-view": target.Signature.ExportSets.Add("E", null!); break;
      case "default-view": target.DefaultExport = new ModuleSignature(); break;
      case "partial-target": target.ModuleDef.SuccessfullyResolved = false; break;
      case "diagnostic": prerequisite.ErrorReporter.Error(MessageSource.Resolver, Token.NoToken, "unsupported prerequisite"); break;
      case "alias-signature": alias.Signature = new ModuleSignature(); break;
      case "alias-target": alias.TargetQId.RestoreAliasTarget(alias); break;
      case "missing-prerequisite": inputs = []; break;
      case "ambiguous-prerequisite": inputs = [prerequisite, prerequisite]; break;
      case "missing-key": inputs = [prerequisite with { Key = null }]; break;
      case "malformed-key": key = "invalid"; break;
      case "missing-selector": inputs = [prerequisite with { Signatures = new() }]; break;
      case "wrong-selector": inputs = [prerequisite with { Signatures = new() { [target.ModuleDef] = new ModuleSignature() } }]; break;
      case "removed-plugin": alias.Options.Plugins.RemoveAt(0); break;
      case "reordered-plugins": (alias.Options.Plugins[0], alias.Options.Plugins[1]) = (alias.Options.Plugins[1], alias.Options.Plugins[0]); break;
      default: throw new ArgumentException(state);
    }
    Assert.Null(ResolutionSnapshots.Prepare(alias, key, inputs,
      ModuleNamePrefixes.For(observation.Outputs.Select(p => p.ResolvedDeclaration.FullName)),
      new[] { new ModuleResolutionKey.Prerequisite("import", target.FullName, prerequisite.Key) }));
  }

  [Fact]
  public async Task UnwritableStoreStillResolves() {
    File.WriteAllText(Path.Combine(directory, "blocked"), "file instead of directory");
    var normal = await Resolve(enabled: false);
    var rejected = await Resolve(store: "blocked");
    Assert.Equal(Boogie(normal.Program), Boogie(rejected.Program));
  }
}
