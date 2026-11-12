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

/// <summary>Content identity for the supported native solver/runtime loading profile.</summary>
internal static class ExecutionImageIdentity {
  private sealed record Image(SortedDictionary<string, string> Digests,
    Dictionary<string, (long Length, DateTime Modified)> Stamps);
  private static readonly ConcurrentDictionary<string, Image> Images = new(StringComparer.Ordinal);

  public static SortedDictionary<string, string> Read(string executable) {
    foreach (var variable in new[] { "LD_PRELOAD", "LD_LIBRARY_PATH", "DYLD_INSERT_LIBRARIES", "DYLD_LIBRARY_PATH",
               "DYLD_FRAMEWORK_PATH", "DOTNET_STARTUP_HOOKS", "DOTNET_ADDITIONAL_DEPS", "CORECLR_ENABLE_PROFILING", "COR_ENABLE_PROFILING" }) {
      if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(variable))) {
        throw new NotSupportedException("custom native/runtime loading environment");
      }
    }
    var image = Images.GetOrAdd(Path.GetFullPath(executable), Capture);
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
    return new(image.Digests, StringComparer.Ordinal);
  }

  private static Image Capture(string executable) {
    var files = new HashSet<string>(StringComparer.Ordinal) { executable };
    if (System.Environment.ProcessPath is { } host) { files.Add(host); }
    foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
               .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
               .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dylib"))
               .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.so"))) { files.Add(file); }
    var runtimeRoot = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent?.Parent?.Parent;
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
        var ownId = Run("/usr/bin/otool", "-D", image).Split('\n').Skip(1).FirstOrDefault()?.Trim();
        foreach (var line in Run("/usr/bin/otool", "-L", image).Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l))) {
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
            var commands = Run("/usr/bin/otool", "-l", image);
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
        foreach (var line in Run("/usr/bin/ldd", image).Split('\n')) {
          var tokens = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
          if (tokens.Length == 0 || tokens[0].StartsWith("linux-vdso", StringComparison.Ordinal)) { continue; }
          var dependency = tokens.Length > 2 && tokens[1] == "=>" ? tokens[2] : tokens[0];
          if (!Path.IsPathRooted(dependency) || !File.Exists(dependency)) {
            throw new NotSupportedException("unresolved native dependency " + dependency);
          }
          files.Add(dependency);
        }
      }
    } else { throw new NotSupportedException("native dependency enumeration is unsupported on this platform"); }
    var manifest = new SortedDictionary<string, string>(StringComparer.Ordinal);
    var stamps = new Dictionary<string, (long, DateTime)>(StringComparer.Ordinal);
    foreach (var file in files.Order(StringComparer.Ordinal)) {
      var before = new FileInfo(file);
      var stamp = (before.Length, before.LastWriteTimeUtc);
      using var stream = File.OpenRead(file);
      manifest[file] = Convert.ToHexStringLower(SHA256.HashData(stream));
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
