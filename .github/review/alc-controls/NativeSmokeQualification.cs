using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;

namespace B3AlcGate;

/// <summary>Captured exact nonproof qualification, never a caller approval flag.</summary>
[SupportedOSPlatform("linux")]
internal sealed class NativeSmokeQualification {
  internal const string SourceCommit = "71fc9dcff677f80d43ef3cb7c767806513c438a4";
  internal const string ZipSha256 = "4f4b07ddac0ca8d5d8876a349ce6871d12d0dcf774c757971dabe29bf8ce7a29";
  internal const long ZipBytes = 80709394;
  internal const string SummarySha256 = "74950dca1707fc774add3ee067ea4939ec76dfeb16c1edb2fa1f5af894b8688b";
  internal const string SourceSha256 = "4230adf2ba624d42574d59ed95d4af1e28346d3260ffb79bf4249f1c80097a05";
  internal const string CoordinatorSha256 = "383c8bffa310bf0ede4322e8679fa7026cf3e3733504404ee987d08f1d262b81";
  private const string Prefix = "out/b3-native-compile/";
  private static readonly string[] Names = ["default-unavailable-demand", "private-unavailable-demand", "reactive-event-unavailable-demand"];
  private static readonly string[] Receipts = ["25205d9db1cf088d937a374ba8044725cfc82d3f886ccb3de9a009375f24b0b1",
    "aca632876dbb7b9b05e5625b122d93d5982c4d40954bc3513673cabbf1d7cadc",
    "09ddb7180ba82ce709cffc64da49c7fa62d872226cca0bfb6256e0493cf8546b"];
  private static readonly Dictionary<string, string> Bundle = new(StringComparer.Ordinal) {
    ["B3AlcGate.dll"] = "f769dc26690a4175706a9758332b56438c4da32e690b3ed09fcef342e4678139",
    ["B3AlcGate.deps.json"] = "3f19663789fcabc493fe5e88617cc68bdcb406e03c6aade5e36accc22b090107",
    ["B3AlcGate.runtimeconfig.json"] = "97c9700542b659150b230c3578b29530fd76ab01ec66a92cd16945e0245713df",
    ["source-manifest.json"] = SourceSha256
  };
  private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 64 };
  private readonly string packet;
  private readonly string currentManifest;
  private readonly string currentManifestSha;
  internal sealed record BuiltFilePin(string Path, string Sha256, long Bytes);
  private readonly Dictionary<string, BuiltFilePin> currentBundle;
  private readonly ProductPin[] products;
  private readonly SharedFilePin[] framework;
  private readonly SharedFilePin[] common;
  private readonly JsonElement summary;
  private NativeSmokeQualification(string packet, string currentManifest, string currentManifestSha,
    Dictionary<string, BuiltFilePin> currentBundle, ProductPin[] products, SharedFilePin[] framework,
    SharedFilePin[] common, JsonElement summary) {
    this.packet = packet; this.currentManifest = currentManifest; this.currentManifestSha = currentManifestSha;
    this.currentBundle = currentBundle; this.products = products; this.framework = framework;
    this.common = common; this.summary = summary;
  }

  public static NativeSmokeQualification Validate(PinnedEvidence? input) {
    R(input != null && input.Sha256 == ZipSha256, "exact-qualified-nonproof-zip-required");
    var packet = Path.GetFullPath(input!.Path);
    R(new FileInfo(packet).LinkTarget == null && new FileInfo(packet).Length == ZipBytes, "qualified-zip-file-bound");
    var captured = NativeProofSmokeControls.ReadBytes(packet, ZipBytes);
    R(captured.LongLength == ZipBytes && NativeProofSmokeControls.Sha256(captured) == ZipSha256, "qualified-zip-byte-pin");
    using var memory = new MemoryStream(captured, writable: false);
    using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
    R(archive.Entries.Count is > 0 and <= 8192, "qualified-zip-inventory-bound");
    var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
    long total = 0;
    foreach (var entry in archive.Entries) {
      var name = entry.FullName.TrimEnd('/');
      var unixType = (entry.ExternalAttributes >> 16) & 0xf000;
      R(name.StartsWith(Prefix, StringComparison.Ordinal) && !name.Contains('\\') &&
        name.Split('/').All(part => part is not ("" or "." or "..")) &&
        unixType is 0 or 0x8000 or 0x4000 && entry.Length is >= 0 and <= 268435456 &&
        entries.TryAdd(entry.FullName, entry), "qualified-zip-unsafe-inventory");
      total = checked(total + entry.Length);
      R(total <= 1073741824, "qualified-zip-expanded-bound");
    }
    byte[] Entry(string name, long bound, string digest) {
      R(entries.TryGetValue(Prefix + name, out var entry) && entry.Length <= bound, "qualified-entry-required");
      var pinnedEntry = entry!;
      using var stream = pinnedEntry.Open();
      var bytes = new byte[checked((int)pinnedEntry.Length)];
      stream.ReadExactly(bytes);
      R(stream.ReadByte() == -1 && NativeProofSmokeControls.Sha256(bytes) == digest, "qualified-entry-byte-pin");
      return bytes;
    }
    using var summary = JsonDocument.Parse(Entry("summary.json", 2097152, SummarySha256));
    var s = summary.RootElement;
    R(s.GetProperty("passed").GetBoolean() && s.GetProperty("head").GetString() == SourceCommit &&
      s.GetProperty("sourceManifestSha256").GetString() == SourceSha256 &&
      s.GetProperty("coordinatorManifestSha256").GetString() == CoordinatorSha256 &&
      !s.GetProperty("nativeProofEnabled").GetBoolean() && !s.GetProperty("solverExecuted").GetBoolean() &&
      !s.GetProperty("nativeQueryOrResourceParityEstablished").GetBoolean() &&
      s.GetProperty("remainingCoordinatorChildren").GetArrayLength() == 0, "qualified-summary-required");
    R(s.GetProperty("fixedControlOrder").EnumerateArray().Select(e => e.GetString()).SequenceEqual(Names),
      "qualified-exact-three-order");
    var checks = s.GetProperty("immutableInputChecks").EnumerateArray().ToArray();
    var expected = new[] { "before-build", "after-build-sources", "after-build" }
      .Concat(Names.SelectMany(n => new[] { "before-" + n, "after-" + n })).Append("final").ToArray();
    R(checks.Select(c => c.GetProperty("boundary").GetString()).SequenceEqual(expected) &&
      checks.All(c => c.GetProperty("passed").GetBoolean() && c.GetProperty("sourceManifestSha256").GetString() == SourceSha256 &&
        c.GetProperty("coordinatorManifestSha256").GetString() == CoordinatorSha256 &&
        c.GetProperty("declaredSourceFilesChecked").GetInt32() == 38 && c.GetProperty("declaredCoordinatorFilesChecked").GetInt32() == 5),
      "qualified-ten-integrity-boundaries");
    var stages = s.GetProperty("stages").EnumerateArray().ToArray();
    R(stages.Select(e => e.GetProperty("stage").GetString()).SequenceEqual(
      new[] { "source-head", "baseline-input", "archived-candidate-input", "nonproof-harness-build" }.Concat(Names)),
      "qualified-seven-stage-order");
    foreach (var stage in stages) {
      R(stage.GetProperty("passed").GetBoolean() && stage.GetProperty("exitCode").GetInt32() == 0 &&
        !stage.GetProperty("poisoned").GetBoolean() && stage.GetProperty("failures").GetArrayLength() == 0 &&
        stage.GetProperty("signals").GetArrayLength() == 0 && stage.GetProperty("signalCount").GetInt32() == 0 &&
        stage.GetProperty("remainingDirectChildren").GetArrayLength() == 0 &&
        stage.GetProperty("ownedProcesses").EnumerateArray().All(p => p.GetProperty("exitObserved").GetBoolean() &&
          p.GetProperty("reaped").GetBoolean()), "qualified-owned-stage-outcome");
      if (Names.Contains(stage.GetProperty("stage").GetString(), StringComparer.Ordinal)) {
        R(stage.GetProperty("rejectObservedDescendants").GetBoolean() &&
          stage.GetProperty("ownedProcesses").GetArrayLength() == 1 &&
          stage.GetProperty("ownedProcesses")[0].GetProperty("isRoot").GetBoolean() &&
          !stage.GetProperty("subreaperAdoptionRequired").GetBoolean() &&
          stage.GetProperty("transientDescendantObservations").GetArrayLength() == 0, "qualified-fresh-one-root-host");
      }
    }
    using var qualifiedSource = JsonDocument.Parse(Entry("harness/source-manifest.json", 131072, SourceSha256));
    var ownManifest = Path.Combine(AppContext.BaseDirectory, "source-manifest.json");
    var ownBytes = NativeProofSmokeControls.ReadBytes(ownManifest, 262144);
    using var own = JsonDocument.Parse(ownBytes);
    var currentBundle = new Dictionary<string, BuiltFilePin>(StringComparer.Ordinal);
    foreach (var name in new[] { "B3AlcGate.dll", "B3AlcGate.deps.json", "B3AlcGate.runtimeconfig.json", "source-manifest.json" }) {
      var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, name));
      var file = new FileInfo(path);
      R(file.Exists && file.LinkTarget == null && file.Length is > 0 and <= 16777216, "current-built-file-bound");
      var bytes = name == "source-manifest.json" ? ownBytes : NativeProofSmokeControls.ReadBytes(path, 16777216);
      R(bytes.LongLength == file.Length, "current-built-file-size");
      currentBundle.Add(name, new(path, NativeProofSmokeControls.Sha256(bytes), bytes.LongLength));
    }
    R(typeof(NativeSmokeQualification).Assembly.Location == currentBundle["B3AlcGate.dll"].Path &&
      currentBundle["B3AlcGate.dll"].Sha256 != Bundle["B3AlcGate.dll"] &&
      currentBundle["source-manifest.json"].Sha256 == NativeProofSmokeControls.Sha256(ownBytes),
      "current-proof-build-distinct-from-qualified-nonproof-build");
    var oldPins = Pins(qualifiedSource.RootElement);
    var ownPins = Pins(own.RootElement);
    R(oldPins.Count == 38 && oldPins.All(p => p.Key is "NativeProofSmokeControls.cs" or "SharedNativeRuns.cs" ||
      ownPins.TryGetValue(p.Key, out var current) && current == p.Value), "qualified-other-thirty-six-source-files-unchanged");
    foreach (var (name, hash) in Bundle) { _ = Entry("harness/" + name, 16777216, hash); }
    R(s.GetProperty("harnessAssemblySha256").GetString() == Bundle["B3AlcGate.dll"], "qualified-harness-identity");
    var controlPins = s.GetProperty("controls").EnumerateArray().ToArray();
    R(controlPins.Length == 3, "qualified-three-receipt-pins");
    ProductPin[]? products = null;
    SharedFilePin[]? framework = null;
    SharedFilePin[]? common = null;
    for (var index = 0; index < Names.Length; index++) {
      var pin = controlPins[index];
      R(pin.GetProperty("name").GetString() == Names[index] && pin.GetProperty("passed").GetBoolean() &&
        pin.GetProperty("receiptSha256").GetString() == Receipts[index], "qualified-control-receipt-linkage");
      using var control = JsonDocument.Parse(Entry(Names[index] + ".json", 4194304, Receipts[index]));
      var c = control.RootElement;
      ValidateControl(c, index);
      _ = Entry(Names[index] + "-inputs.json", 65536, pin.GetProperty("inputsSha256").GetString()!);
      var cp = JsonSerializer.Deserialize<ProductPin[]>(c.GetProperty("products"), Json)!;
      var fp = JsonSerializer.Deserialize<SharedFilePin[]>(c.GetProperty("commonClosure").GetProperty("frameworkFiles"), Json)!;
      var bp = JsonSerializer.Deserialize<SharedFilePin[]>(c.GetProperty("commonClosure").GetProperty("commonFiles"), Json)!;
      R(cp.Length == 2 && fp.Length == 168 && bp.Length == 18, "qualified-complete-catalog-cardinality");
      if (products == null) { products = cp; framework = fp; common = bp; }
      else { R(ProductEqual(products, cp) && framework!.SequenceEqual(fp) && common!.SequenceEqual(bp), "qualified-catalogs-agree"); }
    }
    var dotnet = s.GetProperty("dotnetExecutable");
    R(Environment.ProcessPath == dotnet.GetProperty("path").GetString() &&
      new FileInfo(Environment.ProcessPath!).Length == dotnet.GetProperty("bytes").GetInt64() &&
      Framework.Sha256(Environment.ProcessPath!) == dotnet.GetProperty("sha256").GetString(), "qualified-actual-dotnet-image-required");
    var result = new NativeSmokeQualification(packet, ownManifest, NativeProofSmokeControls.Sha256(ownBytes),
      currentBundle, products!, framework!, common!, s.Clone());
    result.RecheckRuntime();
    return result;
  }

  private static void ValidateControl(JsonElement c, int index) {
    var o = c.GetProperty("observation");
    var closure = c.GetProperty("commonClosure");
    var state = closure.GetProperty("runtimeDemandState");
    R(c.GetProperty("schemaVersion").GetInt32() == 2 && c.GetProperty("passed").GetBoolean() &&
      c.GetProperty("failure").ValueKind == JsonValueKind.Null && c.GetProperty("control").GetString() == Names[index] &&
      c.GetProperty("sourceManifestSha256").GetString() == SourceSha256 &&
      c.GetProperty("harnessAssemblySha256").GetString() == Bundle["B3AlcGate.dll"] &&
      !c.GetProperty("ordinaryProofCliEnabled").GetBoolean() && !c.GetProperty("nativeProofExecuted").GetBoolean() &&
      !c.GetProperty("solverExecuted").GetBoolean() && c.GetProperty("onlyDefaultContextRemains").GetBoolean() &&
      c.GetProperty("remainingDirectChildren").GetArrayLength() == 0 &&
      o.GetProperty("triggerApiInvoked").GetBoolean() && o.GetProperty("denialExceptionObserved").GetBoolean() &&
      o.GetProperty("contextCollected").GetBoolean() && state.GetProperty("poisoned").GetBoolean() &&
      state.GetProperty("failures").EnumerateArray().Select(e => e.GetString()).SequenceEqual(new[] { SharedCommonLibraries.DenialCode }),
      "qualified-denial-and-cleanup-facts");
    var demands = state.GetProperty("unavailableDemands");
    R(demands.GetArrayLength() == 1, "qualified-exact-one-poisoned-demand");
    var demand = demands[0];
    R(demand.GetProperty("sequence").GetInt32() == 1 && demand.GetProperty("exactDeclaredIdentity").GetBoolean() &&
      demand.GetProperty("requestedIdentity").GetString() == SharedCommonLibraries.UnavailableIdentity &&
      demand.GetProperty("metadataOwnerSha256").GetString() == SharedCommonLibraries.UnavailableOwnerSha256 &&
      demand.GetProperty("route").GetString() == (index == 1 ? "private-load" : "default-resolving") &&
      demand.GetProperty("denialCode").GetString() == SharedCommonLibraries.DenialCode, "qualified-exact-denial-route");
    var chain = o.GetProperty("exceptionChain");
    var nodes = chain.GetProperty("nodes").EnumerateArray().ToArray();
    R(chain.GetProperty("complete").GetBoolean() && !chain.GetProperty("truncated").GetBoolean() &&
      chain.GetProperty("captureFailure").ValueKind == JsonValueKind.Null &&
      chain.GetProperty("rejectedAggregateInnerCount").ValueKind == JsonValueKind.Null &&
      nodes.Length == (index == 2 ? 3 : 2), "qualified-complete-linear-exception-chain");
    var bytes = 0;
    for (var depth = 0; depth < nodes.Length; depth++) {
      var node = nodes[depth];
      var terminal = depth == nodes.Length - 1;
      var type = node.GetProperty("type").GetString()!;
      var file = node.GetProperty("fileName");
      var message = node.GetProperty("message").GetString()!;
      R(type == (index == 2 && depth == 0 ? "System.Reflection.TargetInvocationException" : "System.IO.FileNotFoundException") &&
        node.GetProperty("knownFrameworkType").GetBoolean() && node.GetProperty("depth").GetInt32() == depth &&
        node.GetProperty("aggregateInnerCount").ValueKind == JsonValueKind.Null &&
        (terminal ? node.GetProperty("innerDepth").ValueKind == JsonValueKind.Null : node.GetProperty("innerDepth").GetInt32() == depth + 1) &&
        (type == "System.IO.FileNotFoundException" ? file.GetString() == SharedCommonLibraries.UnavailableIdentity : file.ValueKind == JsonValueKind.Null) &&
        (terminal ? message == SharedCommonLibraries.DenialCode : message != SharedCommonLibraries.DenialCode),
        "qualified-exact-terminal-framework-denial");
      bytes = checked(bytes + Encoding.UTF8.GetByteCount(type) + Encoding.UTF8.GetByteCount(message) +
        (file.ValueKind == JsonValueKind.Null ? 0 : Encoding.UTF8.GetByteCount(file.GetString()!)));
    }
    R(bytes == chain.GetProperty("textUtf8Bytes").GetInt32() && bytes == (index == 2 ? 620 : 520), "qualified-bounded-chain-bytes");
    var selection = o.GetProperty("selectedMethod");
    if (index == 2) {
      R(selection.GetProperty("selectionApi").GetString() == "System.Type.GetMethod(genericParameterCount:0,Public|Static,Type/string)" &&
        selection.GetProperty("declaringType").GetString() == "System.Reactive.Linq.Observable" &&
        selection.GetProperty("methodName").GetString() == "FromEventPattern" &&
        selection.GetProperty("genericArity").GetInt32() == 0 && selection.GetProperty("metadataToken").GetInt32() == 0x06000771 &&
        selection.GetProperty("assemblySha256").GetString() == SharedCommonLibraries.UnavailableOwnerSha256 &&
        selection.GetProperty("parameterTypeNames").EnumerateArray().Select(e => e.GetString()).SequenceEqual(new[] { "System.Type", "System.String" }) &&
        new[] { "manifestModuleMatched", "isPublic", "isStatic", "isNongeneric" }.All(p => selection.GetProperty(p).GetBoolean()),
        "qualified-actual-nongeneric-reactive-invocation");
    } else { R(selection.ValueKind == JsonValueKind.Null, "qualified-no-fabricated-selected-method"); }
  }

  public void MatchProducts(ProductPin[] actual) => R(ProductEqual(products, actual), "qualified-exact-original-product-inventories");
  private static bool ProductEqual(ProductPin[] a, ProductPin[] b) => a.Length == b.Length &&
    a.Zip(b).All(pair => pair.First.Label == pair.Second.Label && pair.First.SourceCommit == pair.Second.SourceCommit &&
      pair.First.Directory == pair.Second.Directory && pair.First.CoreSha256 == pair.Second.CoreSha256 &&
      pair.First.CoreInformationalVersion == pair.Second.CoreInformationalVersion && pair.First.Files.SequenceEqual(pair.Second.Files));
  public void MatchShared(SharedCommonReceipt actual) {
    R(actual.FrameworkFiles.SequenceEqual(framework) && actual.CommonFiles.SequenceEqual(common),
      "qualified-actual-common-and-framework-catalogs");
  }
  public void RecheckRuntime() {
    R(new FileInfo(packet).Length == ZipBytes && Framework.Sha256(packet) == ZipSha256 &&
      Framework.Sha256(currentManifest) == currentManifestSha, "qualified-input-bytes-changed");
    foreach (var pin in currentBundle.Values) {
      var file = new FileInfo(pin.Path);
      R(file.Exists && file.LinkTarget == null && file.Length == pin.Bytes &&
        Framework.Sha256(pin.Path) == pin.Sha256, "current-built-harness-changed");
    }
    var trusted = Framework.TrustedPaths();
    trusted.Remove(typeof(NativeSmokeQualification).Assembly.GetName().Name!);
    R(trusted.Count == 168 && trusted.Count == framework.Length, "qualified-complete-actual-tpa-inventory");
    foreach (var pin in framework) {
      R(trusted.TryGetValue(pin.Name, out var path) && path == pin.Path &&
        new FileInfo(path).LinkTarget == null && new FileInfo(path).Length == pin.Bytes &&
        Framework.Sha256(path) == pin.Sha256 && AssemblyName.GetAssemblyName(path).FullName == pin.Identity,
        "qualified-actual-tpa-image-identity-required");
    }
    var dotnet = summary.GetProperty("dotnetExecutable");
    R(Environment.ProcessPath == dotnet.GetProperty("path").GetString() &&
      new FileInfo(Environment.ProcessPath!).Length == dotnet.GetProperty("bytes").GetInt64() &&
      Framework.Sha256(Environment.ProcessPath!) == dotnet.GetProperty("sha256").GetString(), "qualified-dotnet-changed");
  }
  public object Snapshot() => new {
    publicRun = 37238572256L, sourceCommit = SourceCommit, artifactId = 11316129057L,
    zipSha256 = ZipSha256, zipBytes = ZipBytes, summarySha256 = SummarySha256,
    sourceManifestSha256 = SourceSha256, coordinatorManifestSha256 = CoordinatorSha256,
    receiptSha256 = Receipts, originalQualifiedSourceFilesUnchanged = 36, originalLifecycleSourceFilesUnchanged = 19,
    exact168ActualTpaFilesRehashed = true, oldAssemblyObjectsOrSeedsComparedAcrossHosts = false,
    currentBuiltHarnessFiles = currentBundle, currentBuildRehashedAtEveryBoundary = true,
    qualifiedControls = Names, nativeProofPreviouslyEstablished = false
  };
  private static Dictionary<string, PackageFilePin> Pins(JsonElement manifest) => manifest.GetProperty("files")
    .EnumerateArray().Select(f => new PackageFilePin(f.GetProperty("path").GetString()!, f.GetProperty("sha256").GetString()!,
      f.GetProperty("bytes").GetInt64())).ToDictionary(f => f.Path, StringComparer.Ordinal);
  private static void R(bool condition, string code) => NativeProofSmokeControls.Require(condition, code);
}
