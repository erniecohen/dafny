using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Runtime.Versioning;

namespace B3AlcGate;

internal sealed record SharedFilePin(string Name, string Identity, string Path, string Sha256, long Bytes);
internal sealed record SharedReferencePin(string Owner, string RequestedIdentity, string Resolution,
  string ResolvedIdentity, string Path, string Sha256, string? PackageRelativePath,
  string OwnerIdentity, string OwnerSha256, int MetadataFlags);
internal sealed record SharedUnavailableReferencePin(string Owner, string OwnerIdentity, string OwnerSha256,
  string RequestedIdentity, int MetadataFlags, bool BaselineResolverReturnedNull, bool CandidateResolverReturnedNull,
  bool BaselineMetadataCatalogAbsent, bool CandidateMetadataCatalogAbsent, bool ActualTpaCatalogAbsent,
  string[] ScopedTypeReferences, string PublicPackageSha256, string PublicPackageAsset,
  string DeclaredPublicSourceCommit, string DeclarationKind, string SourceEvidenceSha256);
internal sealed record SharedMetadataOwnerInventory(string Owner, string OwnerIdentity, string OwnerSha256, int AssemblyReferenceCount);
internal sealed record SharedPackageMetadataCatalog(string Product, SharedFilePin[] ManagedFiles);
internal sealed record SharedUnavailableDemand(int Sequence, string Route, string Context, string RequestedIdentity,
  string MetadataOwnerIdentity, string MetadataOwnerSha256, bool ExactDeclaredIdentity, string DenialCode);
internal sealed record SharedRuntimeAssemblyLoad(int Sequence, string Kind, string Identity, string Context,
  string Path, string Sha256, bool Validated, string? Failure);
internal sealed record SharedScopeState(bool Poisoned, string[] Failures, SharedUnavailableDemand[] UnavailableDemands);
internal sealed record SharedContextAudit(string Name, bool IsCollectible, string Kind);
internal sealed record DefaultAssemblyPin(int ObjectSlot, string Kind, string Identity, string Path, string Sha256);
internal sealed record SharedDefaultAudit(string Phase, DefaultAssemblyPin[] Assemblies, SharedContextAudit[] Contexts);
internal sealed record SharedCommonReceipt(string Scope, string[] Roots, SharedFilePin[] CommonFiles,
  SharedReferencePin[] MetadataReferences, SharedUnavailableReferencePin[] UnavailableMetadataReferences,
  SharedPackageMetadataCatalog[] PackageMetadataCatalogs, SharedMetadataOwnerInventory[] MetadataOwners, SharedFilePin[] FrameworkFiles, SharedDefaultAudit[] DefaultAudits,
  bool CompleteMetadataInventory, bool CompleteMetadataAvailability, SharedScopeState RuntimeDemandState,
  SharedRuntimeAssemblyLoad[] RuntimeAssemblyLoads,
  bool SameAssemblyObjectsRequired, bool OrdinaryPackageResolverSelectionsRequired,
  bool PersistentBoogieState, bool FreshBoogieStateEstablished,
  bool NativeQueryOrCostParityEstablished, string[] RequiredLaterControls);

/// <summary>
/// A separate, explicitly persistent Default-context common-library scope. This does
/// not change the private-all-library argument-control ProductContext or its audit.
/// Only exact framework files and the recursively pinned common metadata closure are
/// permitted in Default. All returned shared Assembly objects are retained and checked.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class SharedCommonLibraries : IDisposable {
  private const int MaximumCommonFiles = 64;
  private const int MaximumFrameworkFiles = 512;
  private const int MaximumReferencesPerFile = 256;
  private const int MaximumAudits = 32;
  private const long MaximumAssemblyBytes = 67108864;
  private static readonly Dictionary<string, string> BoogieRoots = new(StringComparer.Ordinal) {
    ["Boogie.AbstractInterpretation"] = "6c353eb690a35be73ec499c40535765b3a0e39320ece907a07a6c5fa38fa867c",
    ["Boogie.BaseTypes"] = "e77d97b447cc824edb938a769f6d6d043aae724db6fa4ab15da9b136cc19be7d",
    ["Boogie.CodeContractsExtender"] = "2b261295b7d91f4ce1a126639f242cef34b0a7c1f7518d45f5b53a4cd6c81511",
    ["Boogie.Concurrency"] = "3ef2f0a9472138be33da9b0df79c37d3420556b51138a844715b1c2f506801cc",
    ["Boogie.Core"] = "0a3c1939aff2ec9f38012c83ed30aff32cbba634beb1c5397402dcfa443dbe0e",
    ["Boogie.ExecutionEngine"] = "0fac5e00abadddccb6b1ce3feaebe0e15828593b98910c91e9fad726c4693182",
    ["Boogie.Graph"] = "b1ebd9c9c2cc4cea3e85c65ce8244a7f2d6d4424899b64cf979b901d3c8fcd44",
    ["Boogie.Houdini"] = "87236e790306b0a1f14ffee7bffac02e664a34d4f2d51f37e34411fd6c4b621e",
    ["Boogie.Model"] = "97a89874065fda019efb001c97a88d1d327552c0e60ecc7b9c83b32315c6582e",
    ["Boogie.Provers.LeanAuto"] = "6672c832928406e2fb1bfd5939d528f0af8a53ec8fe6bfc4d35e00db0d652759",
    ["Boogie.Provers.SMTLib"] = "a97bf95e5002e50c1b85373be2da57c15c2413841bf62606ef6e95f8d4493824",
    ["Boogie.VCExpr"] = "cfb45433edfeff6beca744e51228a38935287aa6878b4062614270694fc26a0c",
    ["Boogie.VCGeneration"] = "69154f9a1b3c8463be99ab9262187591e606373510cff7b64930674aecf2fc88"
  };

  internal const string UnavailableSimpleName = "System.Runtime.InteropServices.WindowsRuntime";
  internal const string UnavailableIdentity = "System.Runtime.InteropServices.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
  internal const string UnavailableOwnerSha256 = "19f0112cd1f5172ee2688e96ddd44ada39a4bb1cb2315a154b63e9064f6e3dc0";
  internal const string DenialCode = "declared-unavailable-assembly-demand-denied";
  private const int MaximumDemands = 8;
  private const int MaximumRuntimeLoads = 1024;
  private sealed record MetadataReference(AssemblyName Name, int Flags, string[] ScopedTypeReferences);
  private sealed record MetadataFile(SharedFilePin Pin, AssemblyName Name, MetadataReference[] References);
  private readonly Dictionary<string, MetadataFile> framework = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, MetadataFile> common = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, Assembly> commonObjects = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, Assembly> frameworkObjects = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<Assembly, int> slots = new(ReferenceEqualityComparer.Instance);
  private readonly List<SharedReferencePin> references = [];
  private readonly List<SharedDefaultAudit> audits = [];
  private readonly List<SharedUnavailableReferencePin> unavailable = [];
  private readonly List<SharedPackageMetadataCatalog> packageCatalogs = [];
  private readonly List<SharedUnavailableDemand> demands = [];
  private readonly List<string> failures = [];
  private readonly List<SharedRuntimeAssemblyLoad> runtimeLoads = [];
  private bool poisoned;
  private SharedProductContext? ownedContext;
  private readonly object gate = new();
  private readonly Assembly harness = typeof(SharedCommonLibraries).Assembly;
  private readonly SharedFilePin harnessPin;
  private bool resolverInstalled;
  private bool loadMonitorInstalled;
  private bool disposed;
  private bool preloaded;

  private SharedCommonLibraries() {
    var trusted = Framework.TrustedPaths();
    var harnessName = harness.GetName().Name ?? throw new InvalidOperationException("Harness identity is missing.");
    trusted.Remove(harnessName);
    NativeProofSmokeControls.Require(trusted.Count is > 0 and <= MaximumFrameworkFiles &&
      trusted.Keys.All(n => !IsDafny(n) && !n.StartsWith("Boogie", StringComparison.OrdinalIgnoreCase)),
      "shared-scope-framework-catalog-invalid");
    var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
    foreach (var (name, path) in trusted.OrderBy(p => p.Key, StringComparer.Ordinal)) {
      // The dependency-free host admits the actual runtime directory only, rather
      // than treating arbitrary app-provided entries in TPA as trusted framework.
      NativeProofSmokeControls.Require(Path.GetDirectoryName(path) == runtimeDirectory,
        "shared-scope-nonruntime-tpa-file");
      var file = ReadMetadata(path);
      NativeProofSmokeControls.Require(file.Name.Name == name, "shared-scope-tpa-name-mismatch");
      framework.Add(name, file);
    }
    harnessPin = ReadMetadata(harness.Location).Pin;
    Audit("initial-framework-only");
  }

  public static SharedCommonLibraries CreateAndPreload(ProductPin baseline, ProductPin candidate) {
    var result = new SharedCommonLibraries();
    try {
      result.BuildClosure(baseline, candidate);
      NativeProofSmokeControls.Require(result.unavailable.Count == 1, "exact-one-declared-unavailable-reference-required");
      result.Audit("complete-metadata-inventory-before-common-load");
      // Default.Resolving never captures a private context or private product type.
      // Every dependency has already been recursively pinned before this handler or
      // the first ordinary common Assembly object can be created.
      AppDomain.CurrentDomain.AssemblyLoad += result.OnAssemblyLoad;
      result.loadMonitorInstalled = true;
      AssemblyLoadContext.Default.Resolving += result.ResolveDefault;
      result.resolverInstalled = true;
      foreach (var name in result.common.Keys.Order(StringComparer.Ordinal).ToArray()) {
        result.LoadCommon(name);
      }
      result.preloaded = true;
      result.Audit("common-closure-preloaded-before-product-load");
      return result;
    } catch {
      // Loaded Default assemblies are not unloadable. A preflight/load failure
      // poisons this one-use host; the caller must stop and cannot try a new set.
      result.Poison("common-preflight-or-preload-failed");
      result.Dispose();
      throw;
    }
  }

  private void BuildClosure(ProductPin baseline, ProductPin candidate) {
    var left = baseline.Files.ToDictionary(p => p.Path, StringComparer.Ordinal);
    var right = candidate.Files.ToDictionary(p => p.Path, StringComparer.Ordinal);
    var leftResolver = new AssemblyDependencyResolver(PackagePath(baseline.Directory, "Dafny.dll"));
    var rightResolver = new AssemblyDependencyResolver(PackagePath(candidate.Directory, "Dafny.dll"));
    var leftMetadata = new Dictionary<string, MetadataFile>(StringComparer.Ordinal);
    var rightMetadata = new Dictionary<string, MetadataFile>(StringComparer.Ordinal);
    // Read the complete managed package catalogs before classifying any missing
    // reference; a hidden assembly with the missing simple name is still present.
    var leftCatalog = PackageMetadataCatalog(baseline, left, leftMetadata);
    var rightCatalog = PackageMetadataCatalog(candidate, right, rightMetadata);
    packageCatalogs.Add(new(baseline.Label, leftCatalog.Select(f => f.Pin).OrderBy(f => f.Path, StringComparer.Ordinal).ToArray()));
    packageCatalogs.Add(new(candidate.Label, rightCatalog.Select(f => f.Pin).OrderBy(f => f.Path, StringComparer.Ordinal).ToArray()));
    foreach (var files in new[] { left, right }) {
      var boogie = files.Keys.Where(p => p.StartsWith("Boogie.", StringComparison.Ordinal) && p.EndsWith(".dll", StringComparison.Ordinal))
        .Select(p => Path.GetFileNameWithoutExtension(p)!).Order(StringComparer.Ordinal).ToArray();
      NativeProofSmokeControls.Require(boogie.SequenceEqual(BoogieRoots.Keys.Order(StringComparer.Ordinal)),
        "exact-thirteen-common-boogie-roots-required");
    }
    var pending = new Queue<AssemblyName>(BoogieRoots.Keys.Order(StringComparer.Ordinal).Select(n => new AssemblyName(n)));
    while (pending.TryDequeue(out var requested)) {
      var name = requested.Name!;
      if (common.ContainsKey(name)) { continue; }
      NativeProofSmokeControls.Require(common.Count < MaximumCommonFiles && !framework.ContainsKey(name) && !IsDafny(name) &&
        name != harness.GetName().Name && SimpleName(name), "common-metadata-closure-bound-or-name");
      var relative = ResolveRelative(baseline, leftResolver, requested);
      var otherRelative = ResolveRelative(candidate, rightResolver, requested);
      NativeProofSmokeControls.Require(relative == otherRelative, "common-package-resolver-selection-different");
      NativeProofSmokeControls.Require(left.TryGetValue(relative, out var leftPin) && right.TryGetValue(relative, out var rightPin) &&
        leftPin == rightPin && leftPin.Bytes is > 0 and <= MaximumAssemblyBytes,
        "common-metadata-dependency-missing-or-different");
      var file = ReadPackageMetadata(baseline, left, leftMetadata, relative);
      var other = ReadPackageMetadata(candidate, right, rightMetadata, relative);
      NativeProofSmokeControls.Require(file.Pin.Sha256 == left[relative].Sha256 && file.Pin.Bytes == left[relative].Bytes &&
        other.Pin.Sha256 == file.Pin.Sha256 && other.Pin.Bytes == file.Pin.Bytes &&
        file.Name.FullName == other.Name.FullName && file.Name.Name == name,
        "common-metadata-file-pin-mismatch");
      if (BoogieRoots.TryGetValue(name, out var hash)) {
        NativeProofSmokeControls.Require(relative == name + ".dll" && file.Pin.Sha256 == hash,
          "common-boogie-root-reviewed-byte-pin-mismatch");
      }
      common.Add(name, file);
      foreach (var reference in file.References) {
        var target = reference.Name.Name ?? throw new InvalidOperationException("Metadata reference has no name.");
        if (target.Equals(UnavailableSimpleName, StringComparison.OrdinalIgnoreCase)) {
          ClassifyUnavailable(file, reference, leftResolver, rightResolver, leftCatalog, rightCatalog);
          continue;
        }
        NativeProofSmokeControls.Require(!IsDafny(target) && target != harness.GetName().Name,
          "common-library-reference-to-private-product");
        if (!framework.ContainsKey(target)) { pending.Enqueue(reference.Name); }
      }
      NativeProofSmokeControls.Require(pending.Count <= MaximumCommonFiles * MaximumReferencesPerFile,
        "common-metadata-pending-reference-bound");
    }
    foreach (var file in common.Values.OrderBy(f => f.Pin.Name, StringComparer.Ordinal)) {
      foreach (var reference in file.References) {
        var name = reference.Name.Name!;
        if (name.Equals(UnavailableSimpleName, StringComparison.OrdinalIgnoreCase)) { continue; }
        var isFramework = framework.TryGetValue(name, out var resolved);
        if (!isFramework) { common.TryGetValue(name, out resolved); }
        NativeProofSmokeControls.Require(resolved != null && Compatible(reference.Name, resolved.Name),
          "common-metadata-reference-identity-mismatch");
        var target = resolved ?? throw new InvalidOperationException("No pinned metadata target.");
        string? relative = null;
        if (!isFramework) {
          relative = ResolveRelative(baseline, leftResolver, reference.Name);
          var otherRelative = ResolveRelative(candidate, rightResolver, reference.Name);
          NativeProofSmokeControls.Require(relative == otherRelative &&
            PackagePath(baseline.Directory, relative) == target.Pin.Path,
            "conflicting-common-metadata-package-resolver-selection");
        }
        references.Add(new(file.Pin.Name, reference.Name.FullName ?? name, isFramework ? "shared-tpa" : "shared-common",
          target.Pin.Identity, target.Pin.Path, target.Pin.Sha256, relative, file.Pin.Identity, file.Pin.Sha256, reference.Flags));
      }
    }
  }


  private MetadataFile[] PackageMetadataCatalog(ProductPin product, Dictionary<string, PackageFilePin> files,
    Dictionary<string, MetadataFile> cache) {
    var result = new List<MetadataFile>();
    long inspectedPeBytes = 0;
    foreach (var relative in files.Keys.Order(StringComparer.Ordinal)) {
      var path = PackagePath(product.Directory, relative);
      using (var header = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
        // Managed .NET assemblies are PE images. Inspect all package files instead
        // of assuming their extension; renamed managed images remain inventoried.
        if (header.ReadByte() != 'M' || header.ReadByte() != 'Z') { continue; }
      }
      var bytes = NativeProofSmokeControls.ReadBytes(path, MaximumAssemblyBytes);
      inspectedPeBytes = checked(inspectedPeBytes + bytes.LongLength);
      NativeProofSmokeControls.Require(inspectedPeBytes <= 1073741824, "package-pe-metadata-catalog-total-byte-bound");
      NativeProofSmokeControls.Require(bytes.LongLength == files[relative].Bytes &&
        NativeProofSmokeControls.Sha256(bytes) == files[relative].Sha256, "package-metadata-catalog-byte-pin-mismatch");
      using var stream = new MemoryStream(bytes, writable: false);
      using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
      if (!pe.HasMetadata) { continue; } // Native PE files do not supply managed identity.
      NativeProofSmokeControls.Require(result.Count < MaximumFrameworkFiles, "package-managed-metadata-catalog-bound");
      result.Add(ReadPackageMetadata(product, files, cache, relative));
    }
    // Resource cultures and runtime-specific assets may legitimately repeat a
    // simple name. Every file remains in this absence catalog; resolver selection
    // for actual common dependencies is independently checked below.
    return result.ToArray();
  }

  private void ClassifyUnavailable(MetadataFile owner, MetadataReference reference,
    AssemblyDependencyResolver baselineResolver, AssemblyDependencyResolver candidateResolver,
    MetadataFile[] baselineCatalog, MetadataFile[] candidateCatalog) {
    NativeProofSmokeControls.Require(owner.Pin.Name == "System.Reactive" && owner.Pin.Sha256 == UnavailableOwnerSha256 &&
      reference.Name.FullName == UnavailableIdentity && reference.Flags == 0 &&
      reference.ScopedTypeReferences.SequenceEqual(new[] { "System.Runtime.InteropServices.WindowsRuntime.EventRegistrationToken" }),
      "unreviewed-unavailable-metadata-edge");
    var leftAbsent = baselineResolver.ResolveAssemblyToPath(reference.Name) == null;
    var rightAbsent = candidateResolver.ResolveAssemblyToPath(reference.Name) == null;
    var leftCatalogAbsent = baselineCatalog.All(f => !f.Pin.Name.Equals(UnavailableSimpleName, StringComparison.OrdinalIgnoreCase));
    var rightCatalogAbsent = candidateCatalog.All(f => !f.Pin.Name.Equals(UnavailableSimpleName, StringComparison.OrdinalIgnoreCase));
    var tpaAbsent = !framework.ContainsKey(UnavailableSimpleName);
    NativeProofSmokeControls.Require(leftAbsent && rightAbsent && leftCatalogAbsent && rightCatalogAbsent && tpaAbsent,
      "declared-unavailable-reference-is-actually-present");
    NativeProofSmokeControls.Require(unavailable.Count == 0, "duplicate-declared-unavailable-reference");
    unavailable.Add(new(owner.Pin.Name, owner.Pin.Identity, owner.Pin.Sha256, reference.Name.FullName!, reference.Flags,
      leftAbsent, rightAbsent, leftCatalogAbsent, rightCatalogAbsent, tpaAbsent, reference.ScopedTypeReferences.ToArray(),
      "8a703a9f0f425f483f01eca033026801118e63bf69962fe32c5e780b0794a83f", "lib/netstandard2.0/System.Reactive.dll",
      "7fe23fedde3d0c462ea3dd851debcf9ccb0f52f6", "publisher-source-declaration-not-signed-build-origin",
      "7166e6c53486b9ab3e77a7072328e9f841d7bf1797c550718e170b29913944cc"));
  }

  // These detached facts identify the inventoried metadata owner, not an inferred
  // call stack. They are written under the same lock as proof acceptance before throw.
  public void DenyUnavailable(AssemblyName requested, string route, string context) {
    if (!string.Equals(requested.Name, UnavailableSimpleName, StringComparison.OrdinalIgnoreCase)) { return; }
    lock (gate) {
      poisoned = true;
      AddFailure(DenialCode);
      NativeProofSmokeControls.Require(unavailable.Count == 1, "unavailable-denial-without-reviewed-metadata");
      if (demands.Count >= MaximumDemands) {
        AddFailure("unavailable-demand-ledger-bound");
        throw new InvalidOperationException("unavailable-demand-ledger-bound");
      }
      var owner = unavailable[0];
      demands.Add(new(demands.Count + 1, route, context, requested.FullName ?? requested.Name ?? "", owner.OwnerIdentity,
        owner.OwnerSha256, requested.FullName == UnavailableIdentity, DenialCode));
      throw new FileNotFoundException(DenialCode, requested.FullName);
    }
  }

  public void Poison(string reason) { lock (gate) { poisoned = true; AddFailure(reason); } }
  private void AddFailure(string reason) {
    if (!failures.Contains(reason)) { if (failures.Count < 16) { failures.Add(reason); } else { failures[15] = "scope-failure-ledger-bound"; } }
  }
  public SharedScopeState DemandState() { lock (gate) { return new(poisoned, failures.ToArray(), demands.ToArray()); } }
  public void AssertAcceptable() {
    lock (gate) { NativeProofSmokeControls.Require(!disposed && !poisoned && demands.Count == 0,
      "shared-scope-poisoned-or-unavailable-demand-observed"); }
  }
  public void RegisterContext(SharedProductContext context) {
    lock (gate) {
      NativeProofSmokeControls.Require(!disposed && !poisoned && ownedContext == null, "shared-private-context-admission-failed");
      ownedContext = context;
    }
  }
  public void RetireContext(SharedProductContext context) {
    lock (gate) {
      NativeProofSmokeControls.Require(ReferenceEquals(context, ownedContext), "unowned-private-context-retirement");
      ownedContext = null;
    }
  }

  public Assembly CommonForNonproofControl(string name) {
    lock (gate) {
      AssertAcceptable();
      NativeProofSmokeControls.Require(preloaded && commonObjects.ContainsKey(name), "nonproof-control-common-object-missing");
      var assembly = commonObjects[name];
      ValidateObject(assembly, common[name]);
      CheckFile(common[name].Pin);
      return assembly;
    }
  }

  // This runtime ledger closes the gap between boundary snapshots: an assembly
  // loaded in a transient extra ALC still poisons the host even if later unloaded.
  // Private Assembly/Type/ALC references are never retained in this detached ledger.
  private void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args) {
    lock (gate) {
      // A callback already captured by the runtime may race with unsubscription.
      // This disposable nonproof host must not publish success after retirement.
      if (disposed) { Environment.Exit(126); }
      var assembly = args.LoadedAssembly;
      string kind = "unknown", identity = "", contextName = "", path = "", hash = "";
      string? failure = null;
      try {
        NativeProofSmokeControls.Require(!disposed && runtimeLoads.Count < MaximumRuntimeLoads,
          "runtime-assembly-load-ledger-bound-or-retired-scope");
        var context = AssemblyLoadContext.GetLoadContext(assembly);
        identity = assembly.FullName ?? "";
        contextName = context?.Name ?? "";
        path = Path.GetFullPath(assembly.Location);
        var name = assembly.GetName().Name ?? "";
        NativeProofSmokeControls.Require(!name.Equals(UnavailableSimpleName, StringComparison.OrdinalIgnoreCase),
          "declared-unavailable-assembly-actually-loaded");
        if (context == AssemblyLoadContext.Default) {
          if (framework.TryGetValue(name, out var frameworkFile)) {
            ValidateObject(assembly, frameworkFile); CheckFile(frameworkFile.Pin);
            if (frameworkObjects.TryGetValue(name, out var existing)) {
              NativeProofSmokeControls.Require(ReferenceEquals(existing, assembly), "runtime-framework-object-changed");
            } else { frameworkObjects.Add(name, assembly); }
            kind = "shared-tpa"; hash = frameworkFile.Pin.Sha256;
          } else if (common.TryGetValue(name, out var commonFile)) {
            ValidateObject(assembly, commonFile); CheckFile(commonFile.Pin);
            if (commonObjects.TryGetValue(name, out var existing)) {
              NativeProofSmokeControls.Require(ReferenceEquals(existing, assembly), "runtime-common-object-changed");
            }
            kind = "shared-common"; hash = commonFile.Pin.Sha256;
          } else { throw new InvalidOperationException("Unknown Default assembly load."); }
        } else if (context != null && ReferenceEquals(context, ownedContext)) {
          NativeProofSmokeControls.Require(!framework.ContainsKey(name) && !common.ContainsKey(name),
            "runtime-private-shared-assembly-copy");
          hash = ownedContext!.ValidateRuntimeAssembly(assembly);
          kind = "private-package";
        } else { throw new InvalidOperationException("Unknown or retired AssemblyLoadContext load."); }
      } catch (Exception exception) {
        poisoned = true; AddFailure("runtime-assembly-load-audit-failed");
        failure = exception.GetType().Name + ": " + exception.Message;
        if (failure.Length > 4096) { failure = failure[..4096]; }
      }
      if (runtimeLoads.Count < MaximumRuntimeLoads) {
        runtimeLoads.Add(new(runtimeLoads.Count + 1, kind, identity, contextName, path, hash, failure == null, failure));
      }
      // Do not rely on exceptions escaping a notification event. Immutable poison
      // plus zero-demand/complete-runtime-ledger acceptance is authoritative.
    }
  }

  public bool IsShared(string name) => common.ContainsKey(name);
  public bool IsFramework(string name) => framework.ContainsKey(name);

  public Assembly ResolveShared(AssemblyName requested, out string kind) {
    lock (gate) {
      DenyUnavailable(requested, "shared-request", "Default");
      AssertAcceptable();
      NativeProofSmokeControls.Require(preloaded && !disposed, "shared-common-scope-not-ready");
      var name = requested.Name ?? throw new FileNotFoundException("Requested assembly has no name.");
      if (common.TryGetValue(name, out var file)) {
        NativeProofSmokeControls.Require(Compatible(requested, file.Name), "shared-common-request-identity-mismatch");
        var assembly = commonObjects[name];
        ValidateObject(assembly, file);
        kind = "shared-common";
        return assembly;
      }
      if (framework.TryGetValue(name, out file)) {
        NativeProofSmokeControls.Require(Compatible(requested, file.Name), "shared-framework-request-identity-mismatch");
        var assembly = LoadFramework(file);
        kind = "shared-tpa";
        return assembly;
      }
      throw new FileNotFoundException("The dependency is not in the exact shared closure.", name);
    }
  }

  private Assembly ResolveDefault(AssemblyLoadContext context, AssemblyName requested) {
    lock (gate) {
      if (disposed) { Environment.Exit(126); }
      NativeProofSmokeControls.Require(context == AssemblyLoadContext.Default && !disposed,
        "unexpected-default-resolution-context");
      DenyUnavailable(requested, "default-resolving", "Default");
      AssertAcceptable();
      var name = requested.Name ?? throw new FileNotFoundException("Default requested assembly has no name.");
      if (common.TryGetValue(name, out var file)) {
        NativeProofSmokeControls.Require(Compatible(requested, file.Name), "default-common-reference-identity-mismatch");
        return LoadCommon(name);
      }
      if (framework.TryGetValue(name, out file)) {
        NativeProofSmokeControls.Require(Compatible(requested, file.Name), "default-framework-reference-identity-mismatch");
        return LoadFramework(file);
      }
      // Do not let Default bind private Dafny or an unreviewed helper by event fallback.
      Poison("default-resolution-outside-reviewed-closure");
      throw new FileNotFoundException("Default dependency is outside the reviewed shared closure.", requested.FullName);
    }
  }

  private Assembly LoadCommon(string name) {
    lock (gate) {
      if (commonObjects.TryGetValue(name, out var existing)) { return existing; }
      var file = common[name];
      CheckFile(file.Pin);
      var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(file.Pin.Path);
      ValidateObject(assembly, file);
      commonObjects.Add(name, assembly);
      return assembly;
    }
  }

  private Assembly LoadFramework(MetadataFile file) {
    var name = file.Pin.Name;
    CheckFile(file.Pin);
    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(file.Pin.Path);
    ValidateObject(assembly, file);
    if (frameworkObjects.TryGetValue(name, out var existing)) {
      NativeProofSmokeControls.Require(ReferenceEquals(existing, assembly), "framework-assembly-object-changed");
    } else { frameworkObjects.Add(name, assembly); }
    return assembly;
  }

  public void Audit(string phase) {
    lock (gate) {
      try { AuditLocked(phase); }
      catch { poisoned = true; AddFailure("assembly-or-context-audit-failed"); throw; }
    }
  }
  private void AuditLocked(string phase) {
      NativeProofSmokeControls.Require(!disposed && audits.Count < MaximumAudits, "shared-default-audit-bound");
      var contexts = AssemblyLoadContext.All.ToArray();
      NativeProofSmokeControls.Require(contexts.Length == (ownedContext == null ? 1 : 2) &&
        contexts.All(c => c == AssemblyLoadContext.Default || ReferenceEquals(c, ownedContext)),
        "unknown-or-surviving-assembly-load-context");
      ownedContext?.ValidateCurrentAssemblies();
      var contextPins = contexts.Select(c => new SharedContextAudit(c.Name ?? "", c.IsCollectible,
        c == AssemblyLoadContext.Default ? "default" : "owned-private")).OrderBy(c => c.Kind, StringComparer.Ordinal).ToArray();
      var assemblies = AssemblyLoadContext.Default.Assemblies.ToArray();
      NativeProofSmokeControls.Require(assemblies.Length <= MaximumFrameworkFiles + MaximumCommonFiles + 1,
        "default-assembly-count-bound");
      var entries = new List<DefaultAssemblyPin>();
      var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var assembly in assemblies) {
        var name = assembly.GetName().Name ?? "";
        NativeProofSmokeControls.Require(names.Add(name), "duplicate-default-assembly-simple-name");
        string kind;
        SharedFilePin pin;
        if (ReferenceEquals(assembly, harness)) {
          pin = harnessPin;
          kind = "harness";
          NativeProofSmokeControls.Require(AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default &&
            assembly.FullName == pin.Identity && Path.GetFullPath(assembly.Location) == pin.Path,
            "harness-default-object-mismatch");
        } else if (framework.TryGetValue(name, out var frameworkFile)) {
          ValidateObject(assembly, frameworkFile);
          if (frameworkObjects.TryGetValue(name, out var old)) {
            NativeProofSmokeControls.Require(ReferenceEquals(assembly, old), "framework-assembly-object-changed");
          } else { frameworkObjects.Add(name, assembly); }
          pin = frameworkFile.Pin;
          kind = "shared-tpa";
        } else if (common.TryGetValue(name, out var commonFile) && commonObjects.TryGetValue(name, out var shared)) {
          ValidateObject(assembly, commonFile);
          NativeProofSmokeControls.Require(ReferenceEquals(assembly, shared), "common-assembly-object-changed");
          pin = commonFile.Pin;
          kind = "shared-common";
        } else {
          throw new InvalidOperationException("An unreviewed assembly reached Default: " + assembly.FullName);
        }
        CheckFile(pin);
        if (!slots.TryGetValue(assembly, out var slot)) { slot = slots.Count + 1; slots.Add(assembly, slot); }
        entries.Add(new(slot, kind, pin.Identity, pin.Path, pin.Sha256));
      }
      if (preloaded) {
        NativeProofSmokeControls.Require(commonObjects.Count == common.Count && commonObjects.Values.All(assemblies.Contains),
          "complete-common-default-object-set-required");
      }
      NativeProofSmokeControls.Require(frameworkObjects.Values.All(assemblies.Contains), "prior-framework-default-object-disappeared");
      audits.Add(new(phase, entries.OrderBy(e => e.Identity, StringComparer.Ordinal).ToArray(), contextPins));
  }

  public SharedCommonReceipt Snapshot() {
    lock (gate) {
      return new("exact-common-managed-inventory-with-explicit-unavailable-metadata-and-runtime-denial", BoogieRoots.Keys.Order(StringComparer.Ordinal).ToArray(),
        common.Values.Select(v => v.Pin).OrderBy(p => p.Name, StringComparer.Ordinal).ToArray(),
        references.OrderBy(r => r.Owner, StringComparer.Ordinal).ThenBy(r => r.RequestedIdentity, StringComparer.Ordinal).ToArray(),
        unavailable.ToArray(), packageCatalogs.ToArray(),
        common.Values.OrderBy(f => f.Pin.Name, StringComparer.Ordinal).Select(f => new SharedMetadataOwnerInventory(
          f.Pin.Name, f.Pin.Identity, f.Pin.Sha256, f.References.Length)).ToArray(),
        framework.Values.Select(v => v.Pin).OrderBy(p => p.Name, StringComparer.Ordinal).ToArray(), audits.ToArray(),
        true, false, DemandState(), runtimeLoads.ToArray(), true, true, true, false, false, ["repeat", "interleaved", "reversed-order"]);
    }
  }

  public AssemblyEntry[] CommonAssemblyLedger() {
    lock (gate) {
      NativeProofSmokeControls.Require(preloaded && !disposed, "shared-common-scope-not-ready");
      return commonObjects.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => {
        var file = common[p.Key];
        ValidateObject(p.Value, file);
        CheckFile(file.Pin);
        return new AssemblyEntry("shared-common-loaded", "audited-preloaded-closure", file.Pin.Identity,
          "Default", file.Pin.Path, file.Pin.Sha256,
          InformationalVersionFromMetadata(file.Pin));
      }).ToArray();
    }
  }

  public void ValidateLedger(AssemblyEntry entry) {
    lock (gate) {
      var name = new AssemblyName(entry.Identity).Name ?? "";
      var catalog = entry.Kind is "shared-common" or "shared-common-loaded" ? common : entry.Kind == "shared-tpa" ? framework
        : throw new InvalidOperationException("The ledger entry is not a shared dependency.");
      NativeProofSmokeControls.Require(catalog.TryGetValue(name, out var file) && entry.Context == "Default" &&
        entry.Identity == file.Pin.Identity && entry.Path == file.Pin.Path && entry.Sha256 == file.Pin.Sha256,
        "exact-shared-loader-ledger-pin-mismatch");
    }
  }

  public SharedCommonReceipt CloseAndSnapshot(string phase) {
    lock (gate) {
      // Acceptance facts, final audit and removal of only our owned callbacks share
      // one lock. A pre-captured late callback terminates the disposable host.
      Audit(phase);
      var receipt = Snapshot();
      Dispose();
      return receipt;
    }
  }

  public void Dispose() {
    lock (gate) {
      if (resolverInstalled) { AssemblyLoadContext.Default.Resolving -= ResolveDefault; resolverInstalled = false; }
      if (loadMonitorInstalled) { AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad; loadMonitorInstalled = false; }
      disposed = true;
      if (ownedContext != null) { poisoned = true; AddFailure("scope-disposed-with-owned-context"); }
    }
  }

  private static MetadataFile ReadMetadata(string path) {
    path = Path.GetFullPath(path);
    var bytes = NativeProofSmokeControls.ReadBytes(path, MaximumAssemblyBytes);
    NativeProofSmokeControls.Require(bytes.Length > 0 && new FileInfo(path).LinkTarget == null,
      "shared-assembly-regular-nonsymlink-file-required");
    using var stream = new MemoryStream(bytes, writable: false);
    using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
    NativeProofSmokeControls.Require(pe.HasMetadata, "shared-dependency-not-managed-pe");
    var metadata = pe.GetMetadataReader();
    NativeProofSmokeControls.Require(metadata.IsAssembly, "shared-dependency-not-assembly");
    var definition = metadata.GetAssemblyDefinition();
    var name = Identity(metadata, definition.Name, definition.Version, definition.Culture,
      definition.PublicKey, definition.Flags, definition: true);
    NativeProofSmokeControls.Require(metadata.TypeReferences.Count <= 8192, "metadata-type-reference-count-bound");
    NativeProofSmokeControls.Require(metadata.AssemblyReferences.Count <= MaximumReferencesPerFile,
      "shared-metadata-reference-bound");
    var requested = metadata.AssemblyReferences.Select(handle => {
      var reference = metadata.GetAssemblyReference(handle);
      var identity = Identity(metadata, reference.Name, reference.Version, reference.Culture,
        reference.PublicKeyOrToken, reference.Flags, definition: false);
      var typeReferences = metadata.TypeReferences.Select(typeHandle => metadata.GetTypeReference(typeHandle))
        .Where(t => t.ResolutionScope == handle)
        .Select(t => metadata.GetString(t.Namespace) + "." + metadata.GetString(t.Name))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
      NativeProofSmokeControls.Require(typeReferences.Length <= 1024, "metadata-scoped-type-reference-bound");
      return new MetadataReference(identity, (int)reference.Flags, typeReferences);
    }).ToArray();
    NativeProofSmokeControls.Require(requested.Length <= MaximumReferencesPerFile && name.Name != null && SimpleName(name.Name),
      "shared-metadata-reference-bound");
    return new(new(name.Name!, name.FullName!, path, NativeProofSmokeControls.Sha256(bytes), bytes.Length), name, requested);
  }

  private static AssemblyName Identity(MetadataReader metadata, StringHandle nameHandle, Version version,
    StringHandle culture, BlobHandle keyHandle, AssemblyFlags flags, bool definition) {
    NativeProofSmokeControls.Require((flags & AssemblyFlags.WindowsRuntime) == 0 && (flags & AssemblyFlags.Retargetable) == 0,
      "unsupported-shared-metadata-identity-flags");
    var name = metadata.GetString(nameHandle);
    NativeProofSmokeControls.Require(SimpleName(name), "invalid-shared-metadata-simple-name");
    var identity = new AssemblyName { Name = name, Version = version,
      CultureName = culture.IsNil ? "" : metadata.GetString(culture) };
    byte[] key = keyHandle.IsNil ? [] : metadata.GetBlobBytes(keyHandle);
    NativeProofSmokeControls.Require(identity.CultureName!.Length <= 80 && key.Length <= 16384 &&
      (definition || (flags & AssemblyFlags.PublicKey) != 0 || key.Length is 0 or 8),
      "shared-metadata-identity-bound");
    if (key.Length > 0 && (definition || (flags & AssemblyFlags.PublicKey) != 0)) { identity.SetPublicKey(key); }
    else { identity.SetPublicKeyToken(key); }
    return identity;
  }

  internal static bool Compatible(AssemblyName requested, AssemblyName actual) =>
    string.Equals(requested.Name, actual.Name, StringComparison.OrdinalIgnoreCase) &&
    string.Equals(requested.CultureName ?? "", actual.CultureName ?? "", StringComparison.OrdinalIgnoreCase) &&
    (requested.GetPublicKeyToken() ?? []).SequenceEqual(actual.GetPublicKeyToken() ?? []) &&
    requested.ContentType == actual.ContentType && requested.Version != null && actual.Version != null &&
    actual.Version >= requested.Version;

  private static void ValidateObject(Assembly assembly, MetadataFile file) {
    NativeProofSmokeControls.Require(AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default &&
      assembly.FullName == file.Pin.Identity && Path.GetFullPath(assembly.Location) == file.Pin.Path,
      "shared-default-assembly-identity-or-origin-mismatch");
  }

  internal static string? InformationalVersionFromMetadata(SharedFilePin pin) {
    var bytes = NativeProofSmokeControls.ReadBytes(pin.Path, MaximumAssemblyBytes);
    NativeProofSmokeControls.Require(bytes.LongLength == pin.Bytes && NativeProofSmokeControls.Sha256(bytes) == pin.Sha256,
      "informational-version-metadata-pin-mismatch");
    using var stream = new MemoryStream(bytes, writable: false);
    using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
    var metadata = pe.GetMetadataReader();
    string? result = null;
    var count = 0;
    foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes()) {
      NativeProofSmokeControls.Require(++count <= 256, "assembly-attribute-metadata-count-bound");
      var attribute = metadata.GetCustomAttribute(handle);
      if (attribute.Constructor.Kind != HandleKind.MemberReference) { continue; }
      var member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
      if (member.Parent.Kind != HandleKind.TypeReference) { continue; }
      var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
      if (metadata.GetString(type.Namespace) != "System.Reflection" ||
          metadata.GetString(type.Name) != "AssemblyInformationalVersionAttribute") { continue; }
      NativeProofSmokeControls.Require(result == null, "duplicate-informational-version-metadata");
      var value = metadata.GetBlobReader(attribute.Value);
      NativeProofSmokeControls.Require(value.ReadUInt16() == 1, "informational-version-attribute-prolog");
      result = value.ReadSerializedString();
      NativeProofSmokeControls.Require(result != null && result.Length <= 256 && value.RemainingBytes == 2 && value.ReadUInt16() == 0,
        "informational-version-attribute-metadata-shape");
    }
    return result;
  }

  internal static void RequireNoControlCoreModuleInitializer(string path) {
    var bytes = NativeProofSmokeControls.ReadBytes(path, MaximumAssemblyBytes);
    using var stream = new MemoryStream(bytes, writable: false);
    using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
    var metadata = pe.GetMetadataReader();
    NativeProofSmokeControls.Require(metadata.TypeDefinitions.Count <= 65536, "control-core-type-definition-bound");
    foreach (var handle in metadata.TypeDefinitions) {
      var type = metadata.GetTypeDefinition(handle);
      if (metadata.GetString(type.Name) != "<Module>") { continue; }
      NativeProofSmokeControls.Require(type.GetMethods().All(m => metadata.GetString(metadata.GetMethodDefinition(m).Name) != ".cctor"),
        "control-core-module-initializer-is-outside-nonproof-scope");
    }
  }

  private static void CheckFile(SharedFilePin pin) => NativeProofSmokeControls.Require(
    new FileInfo(pin.Path).Length == pin.Bytes && Framework.Sha256(pin.Path) == pin.Sha256,
    "shared-default-assembly-file-changed");

  private static string ResolveRelative(ProductPin product, AssemblyDependencyResolver resolver, AssemblyName requested) {
    NativeProofSmokeControls.Require(string.IsNullOrEmpty(requested.CultureName) && requested.Name != null && SimpleName(requested.Name),
      "unsupported-common-package-reference-name-or-culture");
    var path = resolver.ResolveAssemblyToPath(requested) ?? Path.Combine(product.Directory, requested.Name + ".dll");
    var relative = Path.GetRelativePath(product.Directory, Path.GetFullPath(path)).Replace(Path.DirectorySeparatorChar, '/');
    PackagePath(product.Directory, relative);
    return relative;
  }

  private static MetadataFile ReadPackageMetadata(ProductPin product, Dictionary<string, PackageFilePin> files,
    Dictionary<string, MetadataFile> cache, string relative) {
    NativeProofSmokeControls.Require(files.TryGetValue(relative, out var pin), "resolved-common-dependency-not-in-package-inventory");
    var expected = pin ?? throw new InvalidOperationException("No common dependency inventory pin.");
    if (cache.TryGetValue(relative, out var existing)) { return existing; }
    var file = ReadMetadata(PackagePath(product.Directory, relative));
    NativeProofSmokeControls.Require(file.Pin.Sha256 == expected.Sha256 && file.Pin.Bytes == expected.Bytes,
      "resolved-common-dependency-byte-pin-mismatch");
    cache.Add(relative, file);
    return file;
  }

  internal static string PackagePath(string directory, string relative) {
    directory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
    var path = Path.GetFullPath(Path.Combine(directory, relative));
    NativeProofSmokeControls.Require(path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(path),
      "dependency-outside-pinned-product-package");
    for (FileSystemInfo? current = new FileInfo(path); current != null; current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent) {
      NativeProofSmokeControls.Require(current.LinkTarget == null, "symlinked-pinned-product-dependency");
      if (current.FullName == directory) { break; }
    }
    return path;
  }

  private static bool IsDafny(string name) => name.StartsWith("Dafny", StringComparison.OrdinalIgnoreCase);
  private static bool SimpleName(string name) => name.Length is > 0 and <= 128 && name.All(c =>
    char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') && name is not ("." or "..");
}
