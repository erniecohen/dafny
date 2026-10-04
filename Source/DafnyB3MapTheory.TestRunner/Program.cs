// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3MapTheory.TestRunner;

/// <summary>Opt-in executable verdict gate for the exact typed BPL map fixtures; never silently skips.</summary>
public static class Program {
  public static async Task<int> Main(string[] args) {
    try {
      var settings = Settings.Parse(args);
      var package = await Ir.WorkerPackage.LoadAsync(settings.Worker);
      var normalizerAssembly = typeof(B3Normalizer).Assembly;
      var assemblyDigest = Digest(await File.ReadAllBytesAsync(normalizerAssembly.Location));
      using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(settings.Fixtures, "cases.json")));
      Require(manifest.RootElement.GetProperty("schemaVersion").GetInt32() == 1, "Unsupported map fixture manifest");
      var entries = manifest.RootElement.GetProperty("cases").EnumerateArray().ToArray();
      Require(entries.Length > 0, "Empty map verdict gate");
      var matched = 0;
      foreach (var entry in entries) {
        var file = entry.GetProperty("file").GetString()!;
        Require(!string.IsNullOrEmpty(file) && file == Path.GetFileName(file), "Invalid fixture name");
        try {
          var bytes = await File.ReadAllBytesAsync(Path.Combine(settings.Fixtures, file));
          var text = System.Text.Encoding.UTF8.GetString(bytes);
          var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
          options.ApplyDefaultOptionsWithoutSettingsDefault();
          var uri = "file:///B3MapTheory/" + Uri.EscapeDataString(file);
          Require(Bpl.Parser.Parse(text, uri, out var source) == 0, "Fixture parse failed");
          Require(source.Resolve(options) == 0, "Fixture resolution failed");
          Require(source.Typecheck(options) == 0, "Fixture typechecking failed");
          Require(source.Implementations.Count() == 1, "Fixture must select one implementation");
          var result = B3Normalizer.Normalize(source, source.Implementations.Single(), options);
          Require(result.Success, "Fixture normalization failed: " + string.Join("; ", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
          var program = result.Program!;
          Require(program.Axioms.Count == entry.GetProperty("expectedAxioms").GetInt32(), "Wrong helper axiom manifest");
          Require(result.Obligations.Count == entry.GetProperty("expectedChecks").GetInt32(), "Wrong static check manifest");
          var origins = result.Approximations.Where(a => a.StartsWith("Monomorphic map helper origin:")).ToArray();
          Require(origins.Length == entry.GetProperty("expectedHelpers").GetInt32(), "Wrong helper origin manifest");
          var request = new Ir.Request(Ir.Protocol.Version, "map-theory-" + Path.GetFileNameWithoutExtension(file),
            Ir.Protocol.NormalizerVersion, Ir.WorkerPackage.UpstreamCommit, Ir.Protocol.GetProgramHash(program),
            program.Unit.Name, program,
            new Ir.Configuration(settings.Solver, new[] { "-in", "-smt2" }, settings.Timeout,
              settings.ResourceLimit, 1048576, 2, "5.1.0", settings.SolverSha256), result.Obligations, package.Fingerprint);
          Ir.ProtocolValidation.ValidateRequest(request);
          if (settings.EmitDirectory is not null) {
            Directory.CreateDirectory(settings.EmitDirectory);
            await File.WriteAllBytesAsync(Path.Combine(settings.EmitDirectory, Path.GetFileNameWithoutExtension(file) + ".request.json"),
              JsonSerializer.SerializeToUtf8Bytes(request, Ir.Protocol.JsonOptions));
          }
          var completion = await new Ir.WorkerProcessClient(settings.Dotnet, new[] { package.WorkerPath })
            .RunAsync(request, CancellationToken.None);
          var expected = Enum.Parse<Ir.Outcome>(entry.GetProperty("expectedB3").GetString()!, ignoreCase: false);
          Require(expected is Ir.Outcome.Verified or Ir.Outcome.Failed, "Unsupported fixture outcome target");
          // Every check in these fixtures is linear and reachable. This is not a general protocol coverage rule.
          var expectedAttempts = entry.GetProperty("expectedAttempts").EnumerateArray()
            .Select(value => Enum.Parse<Ir.Outcome>(value.GetString()!, ignoreCase: false)).ToArray();
          Require(expectedAttempts.Length == result.Obligations.Count &&
            expectedAttempts.All(value => value is Ir.Outcome.Verified or Ir.Outcome.Failed), "Invalid fixture attempt targets");
          var attempts = completion.Attempts.GroupBy(a => a.ObligationId)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
          var covered = result.Obligations.Select(o => o.Id).ToHashSet(StringComparer.Ordinal).SetEquals(attempts.Keys);
          var attemptTargetsMatched = covered && result.Obligations.Select((obligation, i) =>
            attempts[obligation.Id].Length == 1 && attempts[obligation.Id][0].Outcome == expectedAttempts[i]).All(value => value);
          var pass = completion.TraversalCompleted && completion.Error is null && completion.Outcome == expected &&
            covered && attemptTargetsMatched &&
            (expected == Ir.Outcome.Verified ? completion.Attempts.All(a => a.Outcome == Ir.Outcome.Verified) :
              completion.Attempts.Any(a => a.Outcome == Ir.Outcome.Failed));
          if (pass) { matched++; }
          Write(new { kind = "map-theory-case", fixture = file, fixtureSha256 = Digest(bytes),
            normalizerVersion = Ir.Protocol.NormalizerVersion, normalizerAssembly = normalizerAssembly.FullName,
            normalizerAssemblySha256 = assemblyDigest, b3Commit = package.Manifest.B3Commit,
            bootstrapCompiler = package.Manifest.BootstrapCompiler, workerSourceFingerprint = package.Manifest.SourceFingerprint,
            workerFingerprint = package.Fingerprint, solverVersion = "5.1.0", solverSha256 = settings.SolverSha256,
            request.ProgramHash, helperOrigins = origins, expected, completion.Outcome, completion.TraversalCompleted,
            checkCoverageComplete = covered, expectedAttempts, attemptTargetsMatched,
            completion.Attempts, completion.Error, matched = pass });
        } catch (Exception exception) {
          Write(new { kind = "map-theory-case-error", fixture = file, matched = false, error = exception.Message });
        }
      }
      Write(new { kind = "map-theory-summary", matched, total = entries.Length, passed = matched == entries.Length });
      return matched == entries.Length ? 0 : 1;
    } catch (Exception exception) {
      Write(new { kind = "map-theory-tool-error", error = exception.Message });
      return 2;
    }
  }

  private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  private static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value, Ir.Protocol.JsonOptions));
  private static void Require(bool condition, string message) {
    if (!condition) { throw new InvalidDataException(message); }
  }
  private sealed record Settings(string Worker, string Solver, string SolverSha256, string Fixtures,
    string Dotnet, int Timeout, long ResourceLimit, string? EmitDirectory) {
    public static Settings Parse(string[] args) {
      var values = new Dictionary<string, string>(StringComparer.Ordinal);
      var supported = new HashSet<string> { "--worker", "--solver", "--solver-sha256", "--fixtures", "--dotnet",
        "--timeout-ms", "--rlimit", "--emit-directory" };
      for (var i = 0; i < args.Length; i += 2) {
        Require(i + 1 < args.Length && supported.Contains(args[i]) && values.TryAdd(args[i], args[i + 1]),
          "Expected unique --worker, --solver, --solver-sha256 and optional gate setting/value pairs");
      }
      string Required(string key) {
        Require(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), "Missing required setting " + key);
        return value!;
      }
      var digest = Required("--solver-sha256").ToLowerInvariant();
      Require(digest.Length == 64 && digest.All(Uri.IsHexDigit), "Invalid pinned solver digest");
      var timeout = values.TryGetValue("--timeout-ms", out var milliseconds) ? int.Parse(milliseconds, CultureInfo.InvariantCulture) : 30000;
      var resourceLimit = values.TryGetValue("--rlimit", out var units) ? long.Parse(units, CultureInfo.InvariantCulture) : 1000000;
      Require(timeout > 0 && resourceLimit >= 0, "Invalid gate budget");
      return new Settings(Path.GetFullPath(Required("--worker")), Path.GetFullPath(Required("--solver")), digest,
        values.TryGetValue("--fixtures", out var fixtures) ? Path.GetFullPath(fixtures) : Path.Combine(AppContext.BaseDirectory, "MapTheoryInputs"),
        values.GetValueOrDefault("--dotnet", "dotnet"), timeout, resourceLimit,
        values.TryGetValue("--emit-directory", out var output) ? Path.GetFullPath(output) : null);
    }
  }
}
