#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Microsoft.Dafny.Caching;

/// <summary>Version probes are observations about executable bytes, never about a path alone.</summary>
internal static class SolverVersionCache {
  private sealed record Record(int Schema, string Executable, string Version);
  public static Version? ReadOrProbe(string directory, string executable, Func<string, Version?> probe) {
    try {
      using var stream = File.OpenRead(executable);
      var digest = Convert.ToHexStringLower(SHA256.HashData(stream));
      var environment = new System.Collections.Generic.SortedDictionary<string, string>(StringComparer.Ordinal);
      foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables()) {
        environment[(string)entry.Key] = CacheDigest.Blob(Encoding.UTF8.GetBytes((string)entry.Value!));
      }
      var key = CacheDigest.Compute("validated-solver-version/v1", Encoding.UTF8.GetBytes(digest),
        Encoding.UTF8.GetBytes(System.Runtime.InteropServices.RuntimeInformation.OSDescription),
        JsonSerializer.SerializeToUtf8Bytes(environment),
        JsonSerializer.SerializeToUtf8Bytes(ExecutionImageIdentity.Read(executable)));
      var store = new ContentAddressedCache(directory);
      var payload = store.TryRead(CacheTable.SolverIdentity, key);
      if (payload != null) {
        try {
          var record = JsonSerializer.Deserialize<Record>(payload);
          if (record is { Schema: 1 } && record.Executable == digest && Version.TryParse(record.Version, out var cached)) {
            return cached;
          }
        } catch (JsonException) { }
      }
      var version = probe(executable);
      // Recheck after the probe; replacement during inspection cannot certify different bytes.
      using var current = File.OpenRead(executable);
      if (version != null && digest == Convert.ToHexStringLower(SHA256.HashData(current))) {
        store.TryPublish(CacheTable.SolverIdentity, key, JsonSerializer.SerializeToUtf8Bytes(new Record(1, digest, version.ToString())));
      }
      return version;
    } catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {
      return probe(executable);
    }
  }
}
