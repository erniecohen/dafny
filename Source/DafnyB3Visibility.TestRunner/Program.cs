// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Visibility.TestRunner;

/// <summary>Strict source-origin, replay and fresh-worker visibility gate; no workerless skip.</summary>
public static class Program {
  public static async Task<int> Main(string[] args) {
    try {
      var settings = Settings.Parse(args);
      var package = await Ir.WorkerPackage.LoadAsync(settings.Worker);
      var compiler = typeof(BoogieGenerator).Assembly;
      var compilerVersion = compiler.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
      Require(compilerVersion == "4.11.0" || compilerVersion.StartsWith("4.11.0+", StringComparison.Ordinal), "Visibility gate requires Dafny 4.11.0");
      if (settings.CompilerVersion is not null) { Require(compilerVersion == settings.CompilerVersion, "Wrong exact compiler version"); }
      var compilerSha256 = Digest(await File.ReadAllBytesAsync(compiler.Location));
      var normalizer = typeof(B3Normalizer).Assembly;
      var normalizerSha256 = Digest(await File.ReadAllBytesAsync(normalizer.Location));
      using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(settings.Fixtures, "cases.json")));
      Require(manifest.RootElement.GetProperty("schemaVersion").GetInt32() == 1, "Unsupported visibility manifest");
      var cases = manifest.RootElement.GetProperty("cases").EnumerateArray().ToArray();
      Require(cases.Length is > 0 and <= 64, "Invalid visibility fixture denominator");
      var matched = 0;
      foreach (var entry in cases) {
        var file = entry.GetProperty("file").GetString()!;
        try {
          var bytes = await ReadFixture(settings, file);
          var results = await NormalizeFixture(entry, bytes);
          Require(results.Count > 0 && results.All(result => result.Success), "Fixture normalization failed: " +
            string.Join("; ", results.SelectMany(result => result.Diagnostics).Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
          var definitions = results.SelectMany(result => result.Contexts!).SelectMany(context => context.Definitions)
            .DistinctBy(origin => origin.Id).ToArray();
          Require(definitions.Length >= entry.GetProperty("minimumLoadedDefinitions").GetInt32(), "Expected active source definitions were not loaded");
          if (entry.TryGetProperty("maximumLoadedDefinitions", out var maximum)) {
            Require(definitions.Length <= maximum.GetInt32(), "Hidden context loaded a forbidden source definition");
          }
          var completions = new List<B3ContextCompletion>(); var programs = new List<object>();
          foreach (var result in results) {
            var requests = Requests(result, settings, package);
            var completion = await B3ContextCoordinator.RunAsync(requests, (request, token) => Launch(request, settings, package, token), CancellationToken.None);
            completions.Add(completion);
            programs.Add(new { unit = result.Program!.Unit.Name, originalChecks = result.Obligations,
              contexts = result.Contexts!.Select(context => new { context.MaskId, programHash = Ir.Protocol.GetProgramHash(context.Program),
                selectedChecks = context.Obligations, sourceDefinitions = context.Definitions }).ToArray() });
          }
          var expected = Expected(entry.GetProperty("expectedB3").GetString()!);
          var allStrict = completions.All(completion => completion.TraversalCompleted && completion.Error is null &&
            completion.Outcome is Ir.Outcome.Verified or Ir.Outcome.Failed &&
            completion.Attempts.All(attempt => attempt.Outcome is Ir.Outcome.Verified or Ir.Outcome.Failed));
          var outcome = completions.Any(completion => completion.Outcome == Ir.Outcome.Failed) ? Ir.Outcome.Failed :
            completions.All(completion => completion.Outcome == Ir.Outcome.Verified) ? Ir.Outcome.Verified : Ir.Outcome.Inconclusive;
          var attemptsMatched = true;
          if (entry.TryGetProperty("expectedAttempts", out var attemptTargets)) {
            // These typed BPL fixtures are linear and reachable. General protocol checks may be unreachable.
            Require(results.Count == 1, "Exact-attempt fixture must select one original unit");
            var targets = attemptTargets.EnumerateArray().Select(value => Expected(value.GetString()!)).ToArray();
            Require(targets.Length == results[0].Obligations.Count, "Wrong exact-attempt static denominator");
            var attempts = completions.Single().Attempts.GroupBy(attempt => attempt.ObligationId)
              .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            attemptsMatched = results[0].Obligations.Select(identity => identity.Id).ToHashSet(StringComparer.Ordinal).SetEquals(attempts.Keys) &&
              results[0].Obligations.Select((identity, index) => attempts[identity.Id].Length == 1 && attempts[identity.Id][0].Outcome == targets[index]).All(value => value);
          }
          var failedWitness = completions.SelectMany(completion => completion.Attempts).Any(attempt => attempt.Outcome == Ir.Outcome.Failed);
          var pass = allStrict && attemptsMatched && outcome == expected && (expected != Ir.Outcome.Failed || failedWitness);
          if (pass) { matched++; }
          Write(new { kind = "visibility-case", fixture = file, fixtureSha256 = Digest(bytes), compilerVersion, compilerSha256,
            normalizerVersion = Ir.Protocol.NormalizerVersion, normalizerAssembly = normalizer.FullName, normalizerSha256,
            b3Commit = package.Manifest.B3Commit, workerFingerprint = package.Fingerprint,
            workerSourceFingerprint = package.Manifest.SourceFingerprint, bootstrapCompiler = package.Manifest.BootstrapCompiler,
            solverVersion = "5.1.0", solverSha256 = settings.SolverSha256, expected, observed = outcome,
            allStrict, attemptsMatched, failedWitness, programs, completions, matched = pass });
        } catch (Exception exception) {
          Write(new { kind = "visibility-case-error", fixture = file, matched = false, error = exception.Message });
        }
      }
      var isolationMatched = false;
      try {
        var file = manifest.RootElement.GetProperty("isolationFixture").GetString()!;
        var entry = cases.Single(entry => entry.GetProperty("file").GetString() == file);
        var results = await NormalizeFixture(entry, await ReadFixture(settings, file));
        Require(results.Count == 1 && results[0].Success, "Isolation fixture normalization failed");
        var requests = Requests(results[0], settings, package);
        Require(requests.Count == 2, "Isolation fixture needs permissive and restrictive contexts");
        var visible = requests.Single(request => request.Program.Axioms.Count > 0);
        var hidden = requests.Single(request => request.Program.Axioms.Count == 0);
        var completions = new List<Ir.Completion>();
        // A repeated hidden check must not inherit the intervening visible worker's definition.
        foreach (var request in new[] { hidden, visible, hidden }) { completions.Add(await Launch(request, settings, package, CancellationToken.None)); }
        var concurrent = await Task.WhenAll(Launch(visible, settings, package, CancellationToken.None), Launch(hidden, settings, package, CancellationToken.None));
        completions.AddRange(concurrent);
        var expected = manifest.RootElement.GetProperty("isolationExpected").EnumerateArray().Select(value => Expected(value.GetString()!)).ToArray();
        Require(expected.Length == completions.Count, "Wrong isolation denominator");
        isolationMatched = completions.Select((completion, index) => completion.TraversalCompleted && completion.Error is null &&
          completion.Outcome == expected[index] && completion.Attempts.Count == 1 && completion.Attempts[0].Outcome == expected[index]).All(value => value);
        if (isolationMatched) { matched++; }
        Write(new { kind = "visibility-session-isolation", fixture = file, expected, completions, matched = isolationMatched,
          solverVersion = "5.1.0", solverSha256 = settings.SolverSha256, workerFingerprint = package.Fingerprint });
      } catch (Exception exception) { Write(new { kind = "visibility-isolation-error", matched = false, error = exception.Message }); }
      var total = cases.Length + 1;
      Write(new { kind = "visibility-summary", matched, total, isolationMatched, passed = matched == total });
      return matched == total ? 0 : 1;
    } catch (Exception exception) { Write(new { kind = "visibility-tool-error", error = exception.Message }); return 2; }
  }

  private static async Task<List<B3NormalizationResult>> NormalizeFixture(JsonElement entry, byte[] bytes) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    var file = entry.GetProperty("file").GetString()!;
    var uri = new Uri("file:///B3Visibility/" + Uri.EscapeDataString(file));
    var text = Encoding.UTF8.GetString(bytes);
    var sources = new List<Bpl.Program>();
    if (entry.GetProperty("kind").GetString() == "boogie") {
      Require(Bpl.Parser.Parse(text, uri.AbsoluteUri, out var source) == 0, "Fixture parsing failed");
      Require(source.Resolve(options) == 0 && source.Typecheck(options) == 0, "Fixture resolution/typechecking failed");
      var axioms = source.TopLevelDeclarations.OfType<Bpl.Axiom>().ToArray();
      var ordinal = entry.GetProperty("ownedAxiom").GetInt32();
      Require(ordinal >= 0 && ordinal < axioms.Length, "Wrong owned source axiom ordinal");
      // This typed BPL fixture explicitly supplies the same ownership metadata consumed by native pruning.
      var owner = source.Functions.Single(function => function.Name == entry.GetProperty("owner").GetString());
      axioms[ordinal].CanHide = true; owner.OtherDefinitionAxioms.Add(axioms[ordinal]); sources.Add(source);
    } else {
      Require(entry.GetProperty("kind").GetString() == "dafny", "Unsupported fixture kind");
      Microsoft.Dafny.Type.ResetScopes();
      var reporter = new BatchErrorReporter(options);
      var parsed = await ProgramParser.Parse(text, uri, reporter);
      await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
      Require(!reporter.HasErrors, "Dafny resolution failed: " + string.Join("; ", reporter.AllMessages.Select(message => message.Message)));
      foreach (var (_, source) in BoogieGenerator.Translate(parsed.Program, reporter)) {
        Require(source.Resolve(options) == 0 && source.Typecheck(options) == 0, "Emitted Boogie resolution/typechecking failed"); sources.Add(source);
      }
      Require(!reporter.HasErrors, "Dafny translation failed");
    }
    return sources.SelectMany(source => source.Implementations.Select(implementation => B3Normalizer.Normalize(source, implementation, options))).ToList();
  }
  private static IReadOnlyList<Ir.Request> Requests(B3NormalizationResult result, Settings settings, Ir.WorkerPackage package) {
    Require(result.Program is not null && result.Contexts is not null, "Missing original normalized unit or contexts");
    B3DefinitionContexts.ValidatePartition(result.Program!, result.Obligations, result.Contexts!, Bpl.Token.NoToken);
    var configuration = new Ir.Configuration(settings.Solver, new[] { "-in", "-smt2" }, settings.Timeout,
      settings.ResourceLimit, 1048576, 2, "5.1.0", settings.SolverSha256);
    return result.Contexts!.Select(context => new Ir.Request(Ir.Protocol.Version, Guid.NewGuid().ToString("N"),
      Ir.Protocol.NormalizerVersion, package.Manifest.B3Commit, Ir.Protocol.GetProgramHash(context.Program), context.Program.Unit.Name,
      context.Program, configuration, context.Obligations, package.Fingerprint)).ToArray();
  }
  private static async Task<Ir.Completion> Launch(Ir.Request request, Settings settings, Ir.WorkerPackage package, CancellationToken token) {
    Ir.ProtocolValidation.ValidateRequest(request);
    var completion = await new Ir.WorkerProcessClient(settings.Dotnet, new[] { package.WorkerPath }).RunAsync(request, token);
    Ir.ProtocolValidation.ValidateCompletion(request, completion); return completion;
  }
  private static async Task<byte[]> ReadFixture(Settings settings, string file) {
    Require(!string.IsNullOrEmpty(file) && file == Path.GetFileName(file), "Invalid fixture name");
    var bytes = await File.ReadAllBytesAsync(Path.Combine(settings.Fixtures, file)); Require(bytes.Length <= 65536, "Fixture exceeds its bound"); return bytes;
  }
  private static Ir.Outcome Expected(string text) {
    var expected = Enum.Parse<Ir.Outcome>(text, false);
    Require(expected is Ir.Outcome.Verified or Ir.Outcome.Failed, "Invalid strict fixture outcome"); return expected;
  }
  private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  private static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value, Ir.Protocol.JsonOptions));
  private static void Require(bool condition, string message) { if (!condition) { throw new InvalidDataException(message); } }
  private sealed record Settings(string Worker, string Solver, string SolverSha256, string Fixtures,
    string Dotnet, int Timeout, long ResourceLimit, string? CompilerVersion) {
    public static Settings Parse(string[] args) {
      var values = new Dictionary<string, string>(StringComparer.Ordinal);
      var supported = new HashSet<string> { "--worker", "--solver", "--solver-sha256", "--fixtures", "--dotnet", "--timeout-ms", "--rlimit", "--compiler-version" };
      for (var i = 0; i < args.Length; i += 2) {
        Require(i + 1 < args.Length && supported.Contains(args[i]) && values.TryAdd(args[i], args[i + 1]), "Expected unique setting/value pairs");
      }
      string Required(string key) { Require(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), "Missing " + key); return value!; }
      var digest = Required("--solver-sha256").ToLowerInvariant(); Require(digest.Length == 64 && digest.All(Uri.IsHexDigit), "Invalid pinned solver digest");
      var timeout = values.TryGetValue("--timeout-ms", out var milliseconds) ? int.Parse(milliseconds, CultureInfo.InvariantCulture) : 30000;
      var rlimit = values.TryGetValue("--rlimit", out var units) ? long.Parse(units, CultureInfo.InvariantCulture) : 1000000;
      Require(timeout > 0 && rlimit >= 0, "Invalid fixture budget");
      return new Settings(Path.GetFullPath(Required("--worker")), Path.GetFullPath(Required("--solver")), digest,
        values.TryGetValue("--fixtures", out var fixtures) ? Path.GetFullPath(fixtures) : Path.Combine(AppContext.BaseDirectory, "VisibilityInputs"),
        values.GetValueOrDefault("--dotnet", "dotnet"), timeout, rlimit, values.GetValueOrDefault("--compiler-version"));
    }
  }
}
