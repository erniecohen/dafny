using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Versioning;

namespace B3AlcGate;

/// <summary>
/// Private Dafny and private nonshared dependencies, with exactly the independently
/// audited common Default Assembly objects. This is not a relaxation of ProductContext.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class SharedProductContext : AssemblyLoadContext {
  private readonly ProductPin product;
  private readonly SharedCommonLibraries shared;
  private readonly AssemblyDependencyResolver resolver;
  private readonly Dictionary<string, PackageFilePin> files;
  private readonly List<AssemblyEntry> ledger = [];

  public SharedProductContext(ProductPin product, SharedCommonLibraries shared, string name)
    : base(name, isCollectible: true) {
    this.product = product;
    this.shared = shared;
    files = product.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
    var entry = PackagePath(Path.Combine(product.Directory, "Dafny.dll"));
    var deps = PackagePath(Path.Combine(product.Directory, "Dafny.deps.json"));
    ledger.Add(new("resolver-entry", "Dafny.dll", AssemblyName.GetAssemblyName(entry).FullName ?? "Dafny", Name ?? "",
      entry, Framework.Sha256(entry)));
    ledger.Add(new("resolver-deps", "Dafny.deps.json", "dependency-manifest", Name ?? "", deps, Framework.Sha256(deps)));
    resolver = new AssemblyDependencyResolver(entry);
    shared.RegisterContext(this);
  }

  protected override Assembly Load(AssemblyName requested) {
    shared.DenyUnavailable(requested, "private-load", Name ?? "");
    shared.AssertAcceptable();
    var name = requested.Name ?? throw new FileNotFoundException("A private dependency has no simple name.");
    if (shared.IsShared(name) || shared.IsFramework(name)) {
      var assembly = shared.ResolveShared(requested, out var kind);
      Record(kind, requested.FullName ?? name, assembly);
      return assembly;
    }
    var path = resolver.ResolveAssemblyToPath(requested);
    if (path == null) {
      path = string.IsNullOrEmpty(requested.CultureName) ? Path.Combine(product.Directory, name + ".dll")
        : Path.Combine(product.Directory, requested.CultureName, name + ".dll");
    }
    path = PackagePath(path);
    var identity = AssemblyName.GetAssemblyName(path);
    NativeProofSmokeControls.Require(SharedCommonLibraries.Compatible(requested, identity),
      "private-product-request-identity-mismatch");
    var privateAssembly = LoadFromAssemblyPath(path);
    NativeProofSmokeControls.Require(GetLoadContext(privateAssembly) == this &&
      !shared.IsShared(privateAssembly.GetName().Name ?? "") && !shared.IsFramework(privateAssembly.GetName().Name ?? ""),
      "common-or-framework-assembly-loaded-privately");
    Record("private-managed", requested.FullName ?? name, privateAssembly);
    return privateAssembly;
  }

  protected override nint LoadUnmanagedDll(string name) {
    var path = resolver.ResolveUnmanagedDllToPath(name)
      ?? throw new DllNotFoundException("No package-local native dependency mapping for " + name);
    path = PackagePath(path);
    lock (ledger) { ledger.Add(new("private-native", name, name, Name ?? "", path, Framework.Sha256(path))); }
    return LoadUnmanagedDllFromPath(path);
  }

  public Assembly LoadDriver() {
    var assembly = LoadFromAssemblyPath(PackagePath(Path.Combine(product.Directory, "DafnyDriver.dll")));
    NativeProofSmokeControls.Require(GetLoadContext(assembly) == this, "private-driver-context-mismatch");
    Record("private-entry", "DafnyDriver", assembly);
    return assembly;
  }

  // The nonproof demand control loads this exact pinned core without executing a
  // product entrypoint or accessing DafnyMain, so no scheduler is initialized.
  public void LoadCoreForControl() {
    var path = PackagePath(Path.Combine(product.Directory, "DafnyCore.dll"));
    SharedCommonLibraries.RequireNoControlCoreModuleInitializer(path);
    var core = LoadFromAssemblyPath(path);
    Record("private-control-core", "DafnyCore", core);
    ValidateCore(core);
  }

  private void ValidateCore(Assembly core) {
    NativeProofSmokeControls.Require(core.FullName == NativeProofSmokeControls.CoreIdentity &&
      InformationalVersion(core) == product.CoreInformationalVersion &&
      Framework.Sha256(core.Location) == product.CoreSha256, "private-core-identity-pin-mismatch");
  }

  public string ValidateRuntimeAssembly(Assembly assembly) {
    NativeProofSmokeControls.Require(GetLoadContext(assembly) == this, "private-runtime-assembly-context-mismatch");
    var path = PackagePath(assembly.Location);
    var identity = AssemblyName.GetAssemblyName(path);
    NativeProofSmokeControls.Require(assembly.FullName == identity.FullName, "private-runtime-assembly-identity-mismatch");
    return Framework.Sha256(path);
  }

  public void ValidateCurrentAssemblies() {
    var assemblies = Assemblies.ToArray();
    NativeProofSmokeControls.Require(assemblies.Length <= 512, "private-assembly-count-bound");
    foreach (var assembly in assemblies) {
      var name = assembly.GetName().Name ?? "";
      NativeProofSmokeControls.Require(GetLoadContext(assembly) == this && !shared.IsShared(name) && !shared.IsFramework(name) &&
        !name.Equals(SharedCommonLibraries.UnavailableSimpleName, StringComparison.OrdinalIgnoreCase),
        "unexpected-shared-or-unavailable-assembly-in-private-context");
      PackagePath(assembly.Location);
      Record("private-loaded", assembly.FullName ?? "", assembly);
    }
  }

  public AssemblyEntry[] Snapshot() {
    var core = Assemblies.SingleOrDefault(a => a.GetName().Name == "DafnyCore")
      ?? throw new InvalidOperationException("The native run did not load private DafnyCore.");
    ValidateCore(core);
    ValidateCurrentAssemblies();
    shared.Audit("private-context-final-loader-audit-" + product.Label);
    var commonLedger = shared.CommonAssemblyLedger();
    AssemblyEntry[] detached;
    lock (ledger) {
      // These entries identify the separately audited, preloaded Default closure.
      // They do not claim every common file was directly requested by private Dafny.
      ledger.AddRange(commonLedger);
      detached = ledger.Distinct().OrderBy(e => e.Kind, StringComparer.Ordinal).ThenBy(e => e.Identity, StringComparer.Ordinal).ToArray();
    }
    foreach (var entry in detached.Where(e => e.Kind is "shared-common" or "shared-common-loaded" or "shared-tpa")) {
      shared.ValidateLedger(entry);
    }
    return detached;
  }

  private string PackagePath(string path) {
    var relative = Path.GetRelativePath(product.Directory, Path.GetFullPath(path)).Replace(Path.DirectorySeparatorChar, '/');
    NativeProofSmokeControls.Require(files.TryGetValue(relative, out var pin), "private-dependency-not-in-package-inventory");
    var expected = pin ?? throw new InvalidOperationException("No private dependency inventory pin.");
    var actual = SharedCommonLibraries.PackagePath(product.Directory, relative);
    NativeProofSmokeControls.Require(new FileInfo(actual).Length == expected.Bytes && Framework.Sha256(actual) == expected.Sha256,
      "private-dependency-byte-pin-mismatch");
    return actual;
  }

  private string? InformationalVersion(Assembly assembly) {
    var path = Path.GetFullPath(assembly.Location);
    if (GetLoadContext(assembly) == this) {
      var relative = Path.GetRelativePath(product.Directory, path).Replace(Path.DirectorySeparatorChar, '/');
      NativeProofSmokeControls.Require(files.TryGetValue(relative, out var pin), "private-informational-version-inventory-missing");
      var expected = pin ?? throw new InvalidOperationException("Missing private version metadata pin.");
      return SharedCommonLibraries.InformationalVersionFromMetadata(new(assembly.GetName().Name ?? "", assembly.FullName ?? "",
        path, expected.Sha256, expected.Bytes));
    }
    return null; // Shared identity/path/hash/object is audited without instantiating attributes.
  }

  private void Record(string kind, string requested, Assembly assembly) {
    var path = Path.GetFullPath(assembly.Location);
    var entry = new AssemblyEntry(kind, requested, assembly.FullName ?? "", GetLoadContext(assembly)?.Name ?? "",
      path, Framework.Sha256(path), InformationalVersion(assembly));
    lock (ledger) {
      NativeProofSmokeControls.Require(ledger.Count < 4096, "private-loader-ledger-bound");
      if (!ledger.Contains(entry)) { ledger.Add(entry); }
    }
  }
}
