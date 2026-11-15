#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Microsoft.Dafny.Caching;

/// <summary>
/// Locations that an identity names by placeholder rather than by absolute path: the Dafny
/// installation, the .NET installation and the solver's directory.  The bytes under them are
/// still identified by their digests, so the same build and solver in another directory, as
/// on another CI runner, has the same identity.
/// </summary>
internal sealed class ImageRoots {
  private readonly (string Path, string Name)[] roots;

  public ImageRoots(IEnumerable<(string Path, string Name)> roots) {
    // The longest root first, so that a root inside another, such as a solver shipped inside
    // the Dafny installation, is named by the innermost placeholder.
    this.roots = roots.Select(r => (Path: Path.TrimEndingDirectorySeparator(Path.GetFullPath(r.Path)), r.Name))
      .Where(r => r.Path.Length > 1).OrderByDescending(r => r.Path.Length).ThenBy(r => r.Name, StringComparer.Ordinal).ToArray();
  }

  public static ImageRoots For(string executable) {
    var list = new List<(string, string)> { (AppContext.BaseDirectory, "<dafny>") };
    if (RuntimeRoot() is { } runtime) { list.Add((runtime.FullName, "<dotnet>")); }
    if (Path.GetDirectoryName(Path.GetFullPath(executable)) is { } solver) { list.Add((solver, "<solver>")); }
    return new ImageRoots(list);
  }

  internal static DirectoryInfo? RuntimeRoot() =>
    new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent?.Parent?.Parent;

  /// <summary>Replaces each path in <paramref name="value"/> that lies under a root by the
  /// root's placeholder.  A root matches only between separators (start, end, '/', '\\', ':',
  /// ';', '='), never inside a longer name.</summary>
  public string Relocate(string value) {
    foreach (var (root, name) in roots) {
      var start = 0;
      while ((start = value.IndexOf(root, start, StringComparison.Ordinal)) >= 0) {
        var end = start + root.Length;
        if ((start == 0 || value[start - 1] is ':' or ';' or '=') &&
            (end == value.Length || value[end] is '/' or '\\' or ':' or ';')) {
          value = value[..start] + name + value[end..];
          start += name.Length;
        } else {
          start = end;
        }
      }
    }
    return value;
  }
}

/// <summary>Content identity for the supported native solver/runtime loading profile.</summary>
internal static class ExecutionImageIdentity {
  internal sealed record Image(SortedDictionary<string, string> Digests,
    Dictionary<string, (long Length, DateTime Modified)> Stamps);
  private static readonly ConcurrentDictionary<string, Image> Images = new(StringComparer.Ordinal);
  private static ImageDigestMemo? memo;
  /// <summary>Images whose bytes this process hashed, and whose digests it took from the memo.</summary>
  public static int ImagesHashed => memo?.Hashed ?? 0;
  public static int ImageDigestsRemembered => memo?.Remembered ?? 0;

  /// <summary>
  /// The digests of the images that a solver run from <paramref name="executable"/> loads, named
  /// relative to <see cref="ImageRoots.For"/>.  Digests, and the macOS load-command listings that
  /// enumerate dependencies, are remembered under <paramref name="cacheDirectory"/>
  /// (<see cref="ImageDigestMemo"/>).
  /// </summary>
  public static SortedDictionary<string, string> Read(string executable, string? cacheDirectory) {
    foreach (var variable in new[] { "LD_PRELOAD", "LD_LIBRARY_PATH", "DYLD_INSERT_LIBRARIES", "DYLD_LIBRARY_PATH",
               "DYLD_FRAMEWORK_PATH", "DOTNET_STARTUP_HOOKS", "DOTNET_ADDITIONAL_DEPS", "CORECLR_ENABLE_PROFILING", "COR_ENABLE_PROFILING" }) {
      if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(variable))) {
        throw new NotSupportedException("custom native/runtime loading environment");
      }
    }
    var roots = ImageRoots.For(executable);
    var image = Images.GetOrAdd(Path.GetFullPath(executable), path => Capture(path, memo ??= new ImageDigestMemo(cacheDirectory)));
    if (OperatingSystem.IsLinux() && LinuxMappedImages().Any(path => !image.Digests.ContainsKey(path))) {
      // dlopen dependencies (notably ICU/OpenSSL) need not appear in ldd output.
      // An unrecorded late load changes this invocation's supported profile.
      throw new NotSupportedException("a native execution image was loaded after identity capture");
    }
    // Loaded code must remain immutable throughout this supported invocation. Do not
    // certify replacement files against images already loaded by the current process.
    foreach (var (path, stamp) in image.Stamps) {
      var info = new FileInfo(path);
      if (!info.Exists || (info.Length, info.LastWriteTimeUtc) != stamp) {
        throw new NotSupportedException("an execution image changed during this invocation");
      }
    }
    return Relocate(image.Digests, roots);
  }

  internal static SortedDictionary<string, string> Relocate(IReadOnlyDictionary<string, string> digests, ImageRoots roots) {
    var relocated = new SortedDictionary<string, string>(StringComparer.Ordinal);
    foreach (var (path, digest) in digests) {
      if (!relocated.TryAdd(roots.Relocate(path), digest)) {
        throw new NotSupportedException("two execution images have the same relocated name");
      }
    }
    return relocated;
  }

  private static Image Capture(string executable, ImageDigestMemo memo) {
    var files = new HashSet<string>(StringComparer.Ordinal) { executable };
    if (System.Environment.ProcessPath is { } host) { files.Add(host); }
    foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
               .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
               .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dylib"))
               .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.so"))) { files.Add(file); }
    var runtimeRoot = ImageRoots.RuntimeRoot();
    var hostFxr = runtimeRoot == null ? null : Path.Combine(runtimeRoot.FullName, "host", "fxr");
    if (hostFxr == null || !Directory.Exists(hostFxr)) {
      throw new NotSupportedException("runtime host dependency identity is unavailable");
    }
    foreach (var file in Directory.GetFiles(hostFxr, "*", SearchOption.AllDirectories)
               .Where(f => f.EndsWith(".dylib", StringComparison.Ordinal) || f.EndsWith(".so", StringComparison.Ordinal))) {
      files.Add(file);
    }
    if (OperatingSystem.IsMacOS()) {
      var shared = new[] { "/System/Volumes/Preboot/Cryptexes/OS/System/Library/dyld", "/System/Library/dyld" }
        .FirstOrDefault(Directory.Exists) ?? throw new NotSupportedException("missing native shared-cache identity");
      var caches = Directory.GetFiles(shared, "dyld_shared_cache_*").Where(f => !f.EndsWith(".map", StringComparison.Ordinal)).ToArray();
      if (caches.Length == 0) { throw new NotSupportedException("missing native shared-cache bytes"); }
      foreach (var cache in caches) { files.Add(cache); }
      var pending = new Queue<string>(files.Where(f => !f.EndsWith(".dll", StringComparison.Ordinal) && !caches.Contains(f)));
      var seen = new HashSet<string>(StringComparer.Ordinal);
      while (pending.TryDequeue(out var image)) {
        if (!seen.Add(image)) { continue; }
        var ownId = memo.Inspect(image, "-D", () => Run("/usr/bin/otool", "-D", image)).Split('\n').Skip(1).FirstOrDefault()?.Trim();
        foreach (var line in memo.Inspect(image, "-L", () => Run("/usr/bin/otool", "-L", image)).Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l))) {
          var dependency = line.Trim().Split(" (", StringSplitOptions.None)[0];
          if (dependency == ownId) { continue; }
          if (dependency.StartsWith("/usr/lib/", StringComparison.Ordinal) || dependency.StartsWith("/System/Library/", StringComparison.Ordinal)) {
            // These paths are backed by the complete OS shared-cache bytes above.
            if (File.Exists(dependency) && files.Add(dependency)) { pending.Enqueue(dependency); }
            continue;
          }
          if (dependency.StartsWith("@loader_path/", StringComparison.Ordinal)) {
            dependency = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(image)!, dependency[13..]));
          }
          if (dependency.StartsWith("@rpath/", StringComparison.Ordinal)) {
            var commands = memo.Inspect(image, "-l", () => Run("/usr/bin/otool", "-l", image));
            var paths = System.Text.RegularExpressions.Regex.Matches(commands, @"cmd LC_RPATH\s+cmdsize \d+\s+path (.*?) \(offset");
            if (paths.Count != 1 || paths[0].Groups[1].Value != "@loader_path") {
              throw new NotSupportedException("unsupported native rpath search");
            }
            dependency = Path.Combine(Path.GetDirectoryName(image)!, dependency[7..]);
          }
          if (!Path.IsPathRooted(dependency) || !File.Exists(dependency)) {
            throw new NotSupportedException("unresolved native dependency " + dependency);
          }
          if (files.Add(dependency)) { pending.Enqueue(dependency); }
        }
      }
    } else if (OperatingSystem.IsLinux()) {
      var mappedImages = LinuxMappedImages().ToHashSet(StringComparer.Ordinal);
      foreach (var mapped in mappedImages) { files.Add(mapped); }
      // Optional runtime providers can reference packages absent from this installation.
      // Hash their bytes, but enumerate dependency closure only for images actually loaded
      // by this process and the selected child executable. A later unrecorded dlopen still
      // rejects reuse in Read, including any newly loaded transitive dependency.
      foreach (var image in LinuxDependencyRoots(files, mappedImages, executable, System.Environment.ProcessPath)) {
        foreach (var dependency in LinuxDependencies(Run("/usr/bin/ldd", image))) {
          if (!File.Exists(dependency)) {
            throw new NotSupportedException("unresolved native dependency " + dependency);
          }
          files.Add(dependency);
        }
      }
    } else { throw new NotSupportedException("native dependency enumeration is unsupported on this platform"); }
    return Digests(files, memo);
  }

  internal static Image Digests(IEnumerable<string> files, ImageDigestMemo memo) {
    var manifest = new SortedDictionary<string, string>(StringComparer.Ordinal);
    var stamps = new Dictionary<string, (long, DateTime)>(StringComparer.Ordinal);
    foreach (var file in files.Order(StringComparer.Ordinal)) {
      var before = new FileInfo(file);
      var stamp = (before.Length, before.LastWriteTimeUtc);
      manifest[file] = memo.Digest(file);
      var after = new FileInfo(file);
      if (stamp != (after.Length, after.LastWriteTimeUtc)) {
        throw new NotSupportedException("an execution image changed while hashing");
      }
      stamps.Add(file, stamp);
    }
    return new Image(manifest, stamps);
  }

  internal static IReadOnlyList<string> LinuxDependencyRoots(IEnumerable<string> files,
    ISet<string> mappedImages, string executable, string? host) => files.Where(file =>
      (file == executable || file == host || mappedImages.Contains(file)) &&
      !file.EndsWith(".dll", StringComparison.Ordinal)).ToArray();

  internal static IReadOnlyList<string> LinuxDependencies(string listing) {
    var lines = listing.Split('\n').Select(line => line.Trim()).Where(line => line.Length != 0).ToArray();
    // The ELF loader itself is mapped executable code, but has no DT_NEEDED entries.
    // ldd reports that exact zero-dependency case as "statically linked". Its image
    // bytes are still included in the identity; all other output must name a file.
    if (lines.Length == 1 && lines[0] == "statically linked") { return Array.Empty<string>(); }
    var dependencies = new List<string>();
    foreach (var line in lines) {
      var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
      if (tokens[0].StartsWith("linux-vdso", StringComparison.Ordinal)) { continue; }
      var dependency = tokens.Length > 2 && tokens[1] == "=>" ? tokens[2] : tokens[0];
      if (!Path.IsPathRooted(dependency)) {
        throw new NotSupportedException("unresolved native dependency " + dependency);
      }
      dependencies.Add(dependency);
    }
    return dependencies;
  }

  private static IEnumerable<string> LinuxMappedImages() {
    foreach (var line in File.ReadLines("/proc/self/maps")) {
      var columns = line.Split((char[]?)null, 6, StringSplitOptions.RemoveEmptyEntries);
      if (columns.Length != 6 || !columns[5].StartsWith('/')) { continue; }
      var path = columns[5];
      // CLR JIT mappings are generated from the already pinned managed producer.
      if (path.StartsWith("/memfd:", StringComparison.Ordinal)) { continue; }
      if (!columns[1].Contains('x') && !path.Contains(".so", StringComparison.Ordinal) &&
          !path.EndsWith(".dll", StringComparison.Ordinal)) { continue; }
      if (path.EndsWith(" (deleted)", StringComparison.Ordinal) || !File.Exists(path)) {
        throw new NotSupportedException("unidentifiable mapped native execution image");
      }
      yield return path;
    }
  }

  private static string Run(string executable, params string[] arguments) {
    var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) { info.ArgumentList.Add(argument); }
    using var process = Process.Start(info) ?? throw new IOException("native dependency inspection could not start");
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0) { throw new NotSupportedException("native dependency inspection failed: " + error); }
    return output;
  }
}
