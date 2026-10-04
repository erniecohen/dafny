using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace B3AlcGate;

public sealed record Product(string Label, string Directory);
public sealed record AssemblyEntry(string Kind, string RequestedIdentity, string Identity,
  string Context, string Path, string Sha256);

/// <summary>Framework-only host. Never returns null for an unresolved managed dependency.</summary>
internal sealed class ProductContext : AssemblyLoadContext {
  private readonly string directory;
  private readonly AssemblyDependencyResolver resolver;
  private readonly Dictionary<string, string> trusted;
  private readonly List<AssemblyEntry> ledger = [];

  public ProductContext(Product product, string contextName) : base(contextName, isCollectible: true) {
    directory = System.IO.Path.GetFullPath(product.Directory).TrimEnd(System.IO.Path.DirectorySeparatorChar);
    if (directory == System.IO.Path.GetPathRoot(directory)) {
      throw new ArgumentException("The package must have its own directory.");
    }
    trusted = Framework.TrustedPaths();
    if (trusted.ContainsKey("Dafny") || trusted.ContainsKey("DafnyDriver") ||
        trusted.ContainsKey("DafnyCore") || trusted.Keys.Any(n => n.StartsWith("Boogie", StringComparison.OrdinalIgnoreCase))) {
      throw new InvalidOperationException("The host TPA list contains product assemblies.");
    }
    var entry = RequirePackagePath(System.IO.Path.Combine(directory, "Dafny.dll"));
    var deps = RequirePackagePath(System.IO.Path.Combine(directory, "Dafny.deps.json"));
    ledger.Add(new("resolver-entry", "Dafny.dll", AssemblyName.GetAssemblyName(entry).FullName ?? "Dafny", Name ?? "",
      entry, Framework.Sha256(entry)));
    ledger.Add(new("resolver-deps", "Dafny.deps.json", "dependency-manifest", Name ?? "", deps, Framework.Sha256(deps)));
    resolver = new AssemblyDependencyResolver(entry);
  }

  protected override Assembly Load(AssemblyName name) {
    var simpleName = name.Name ?? throw new FileNotFoundException("A dependency has no simple name.");
    if (trusted.TryGetValue(simpleName, out var trustedPath)) {
      var assembly = Default.LoadFromAssemblyName(name);
      if (GetLoadContext(assembly) != Default ||
          !System.IO.Path.GetFullPath(assembly.Location).Equals(trustedPath, StringComparison.Ordinal)) {
        throw new InvalidOperationException("A TPA dependency resolved outside the default runtime context: " + name);
      }
      Record("shared-tpa", name.FullName ?? simpleName, assembly);
      return assembly;
    }

    var path = resolver.ResolveAssemblyToPath(name);
    if (path == null) {
      // Published CLI packages also carry directly referenced DLLs. This never falls back
      // to the host or the other product, including for System.CommandLine/System.Reactive.
      path = string.IsNullOrEmpty(name.CultureName)
        ? System.IO.Path.Combine(directory, simpleName + ".dll")
        : System.IO.Path.Combine(directory, name.CultureName, simpleName + ".dll");
    }
    path = RequirePackagePath(path);
    var privateAssembly = LoadFromAssemblyPath(path);
    if (GetLoadContext(privateAssembly) != this) {
      throw new InvalidOperationException("A private dependency escaped its run context: " + name);
    }
    Record("private-managed", name.FullName ?? simpleName, privateAssembly);
    return privateAssembly;
  }

  protected override nint LoadUnmanagedDll(string name) {
    var path = resolver.ResolveUnmanagedDllToPath(name);
    if (path == null) {
      // Native fallback is not an isolation argument. A future workload that needs
      // host native libraries must explicitly review and ledger that sharing.
      throw new DllNotFoundException("No package-local native dependency mapping for " + name);
    }
    path = RequirePackagePath(path);
    lock (ledger) {
      ledger.Add(new("private-native", name, name, Name ?? "", path, Framework.Sha256(path)));
    }
    return LoadUnmanagedDllFromPath(path);
  }

  public Assembly LoadDriver() {
    var assembly = LoadFromAssemblyPath(RequirePackagePath(System.IO.Path.Combine(directory, "DafnyDriver.dll")));
    Record("private-entry", "DafnyDriver", assembly);
    return assembly;
  }

  public AssemblyEntry[] Snapshot() {
    foreach (var assembly in Assemblies) {
      if (GetLoadContext(assembly) != this || trusted.ContainsKey(assembly.GetName().Name ?? "")) {
        throw new InvalidOperationException("A shared assembly was loaded privately.");
      }
      RequirePackagePath(assembly.Location);
      Record("private-loaded", assembly.FullName ?? "", assembly);
    }
    // Default-context contamination is a failure, including reflection load bypasses.
    foreach (var assembly in Default.Assemblies) {
      if (assembly == typeof(ProductContext).Assembly) {
        continue;
      }
      var name = assembly.GetName().Name ?? "";
      if (!trusted.ContainsKey(name)) {
        throw new InvalidOperationException("A non-TPA assembly reached the host context: " + assembly.FullName);
      }
    }
    lock (ledger) {
      return ledger.Distinct().OrderBy(e => e.Kind, StringComparer.Ordinal)
        .ThenBy(e => e.Identity, StringComparer.Ordinal).ToArray();
    }
  }

  private string RequirePackagePath(string path) {
    path = System.IO.Path.GetFullPath(path);
    if (!path.StartsWith(directory + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path)) {
      throw new FileNotFoundException("A dependency is missing or outside its product package.", path);
    }
    // Do not hide a dependency outside the package behind a file/directory symlink.
    for (var current = new FileInfo(path) as FileSystemInfo; current != null && current.FullName != directory;
         current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent) {
      if (current.LinkTarget != null) {
        throw new IOException("Symlinked package dependencies are outside this prototype's scope: " + path);
      }
    }
    if (new DirectoryInfo(directory).LinkTarget != null) {
      throw new IOException("The product directory must not be a symlink.");
    }
    return path;
  }

  private void Record(string kind, string requested, Assembly assembly) {
    var path = System.IO.Path.GetFullPath(assembly.Location);
    var entry = new AssemblyEntry(kind, requested, assembly.FullName ?? "", GetLoadContext(assembly)?.Name ?? "",
      path, Framework.Sha256(path));
    lock (ledger) {
      if (!ledger.Contains(entry)) {
        ledger.Add(entry);
      }
    }
  }
}

public sealed record FrameworkWitness(string Runtime, string CoreLibIdentity, string CoreLibPath, string CoreLibSha256,
  string NumericsIdentity, string NumericsPath, string NumericsSha256, int Big4294967295, int Big4294967296,
  int StringHash, int HashCodeWitness);

internal static class Framework {
  public static string Sha256(string path) {
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
  }

  public static Dictionary<string, string> TrustedPaths() {
    var paths = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)
      ?? throw new InvalidOperationException("No trusted platform assembly list.");
    return paths.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
      .ToDictionary(p => System.IO.Path.GetFileNameWithoutExtension(p)!, System.IO.Path.GetFullPath, StringComparer.OrdinalIgnoreCase);
  }

  public static FrameworkWitness Witness() {
    var core = typeof(object).Assembly;
    var numerics = typeof(System.Numerics.BigInteger).Assembly;
    if (AssemblyLoadContext.GetLoadContext(core) != AssemblyLoadContext.Default ||
        AssemblyLoadContext.GetLoadContext(numerics) != AssemblyLoadContext.Default) {
      throw new InvalidOperationException("The framework witness is not shared by the host.");
    }
    return new(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, core.FullName ?? "", core.Location,
      Sha256(core.Location), numerics.FullName ?? "", numerics.Location, Sha256(numerics.Location),
      new System.Numerics.BigInteger(4294967295UL).GetHashCode(), new System.Numerics.BigInteger(4294967296UL).GetHashCode(),
      "b3-alc-common-framework-witness".GetHashCode(), HashCode.Combine(0x3b, 0x51));
  }

  public static int[] LiveChildren() {
    if (!OperatingSystem.IsLinux()) {
      throw new PlatformNotSupportedException("This source-only prototype targets bounded Linux scratch CI.");
    }
    var children = new HashSet<int>();
    foreach (var thread in Directory.EnumerateDirectories("/proc/self/task")) {
      try {
        var text = File.ReadAllText(System.IO.Path.Combine(thread, "children"));
        if (text.Length > 8192) {
          throw new IOException("The child-process ledger exceeded its bound.");
        }
        foreach (var id in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
          children.Add(int.Parse(id, System.Globalization.CultureInfo.InvariantCulture));
        }
      } catch (FileNotFoundException) {
        // A host thread may exit while its proc entry is inspected.
      } catch (DirectoryNotFoundException) {
        // Same race for the thread's task directory.
      }
    }
    return children.Order().ToArray();
  }
}
