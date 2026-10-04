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

namespace B3RealTriage;

/// <summary>Strict diagnostic of unchanged actual-Dafny Real requests; not a full gate.</summary>
public static class Program {
  private const long BaselineLimit = 200000;
  private const int TimeoutMilliseconds = 20000;
  private const string SourceFingerprint = "a6ecb742096ddf2f4a6dbcfefb847fdb17ff1fbda7bacf0fafd445f60d843976";
  private const string LibraryDigest = "9461fe3bb77dfabf981af767b586025e76bda574534f2829b59bf5c462955536";
  private const string SolverDigest = "b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23";

  public static async Task<int> Main(string[] args) {
    Settings? settings = null;
    try {
      settings = Settings.Parse(args);
      Require(!Directory.Exists(settings.Output), "Output must be a new directory");
      Directory.CreateDirectory(settings.Output);
      var core = typeof(B3Normalizer).Assembly;
      Require(Digest(core.Location) == Digest(Path.Combine(settings.Compiler, "DafnyCore.dll")),
        "Loaded normalizer differs from captured compiler");
      var coreVersion = core.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
      Require(coreVersion == settings.CompilerVersion, "Loaded Core informational version differs from the fresh CLI version");
      Require(Digest(typeof(Bpl.Program).Assembly.Location) == Digest(Path.Combine(settings.Compiler, Path.GetFileName(typeof(Bpl.Program).Assembly.Location))),
        "Loaded Boogie differs from captured compiler");
      Require(Digest(typeof(Ir.Request).Assembly.Location) == Digest(Path.Combine(settings.Compiler, "DafnyB3Protocol.dll")),
        "Loaded protocol differs from captured compiler");
      Require(Digest(settings.Solver) == SolverDigest, "Solver differs from the original pinned solver file");
      var compilerFiles = Directory.GetFiles(settings.Compiler, "*.dll").OrderBy(file => file, StringComparer.Ordinal).ToArray();
      Require(compilerFiles.Length is > 0 and <= 512 && compilerFiles.Sum(file => new FileInfo(file).Length) <= 1024L * 1024 * 1024,
        "Compiler reference inventory exceeds bound");
      var compilerReferences = compilerFiles.Select(file => new {
        file = Path.GetFileName(file), assemblyIdentity = AssemblyName.GetAssemblyName(file).FullName, sha256 = Digest(file)
      }).ToArray();
      var package = await Ir.WorkerPackage.LoadAsync(settings.Worker);
      Require(package.Fingerprint == settings.WorkerFingerprint && package.Manifest.SourceFingerprint == SourceFingerprint &&
        package.Manifest.Files["B3Library.dll"] == LibraryDigest && Ir.Protocol.Version == 2 &&
        Ir.Protocol.NormalizerVersion == "experimental-2", "Worker differs from the verified original library/package pins");
      var identity = new {
        coreAssembly = core.FullName, coreInformationalVersion = coreVersion,
        coreSha256 = Digest(core.Location), protocolSha256 = Digest(typeof(Ir.Request).Assembly.Location),
        boogieAssembly = typeof(Bpl.Program).Assembly.FullName, boogieAssemblySha256 = Digest(typeof(Bpl.Program).Assembly.Location),
        compilerReferences, coreDeclaredReferences = core.GetReferencedAssemblies().Select(reference => reference.FullName),
        settings.CompilerVersion, settings.CompilerSource, package.Fingerprint, package.Manifest,
        solverVersion = "5.1.0", solverSha256 = SolverDigest, actualProofResourceCount = (long?)null
      };
      await Save(settings.Output, "identity.json", identity);
      var manifest = JsonSerializer.Deserialize<FixtureManifest>(await File.ReadAllBytesAsync(Path.Combine(settings.Fixtures, "cases.json")), Ir.Protocol.JsonOptions)!;
      Require(manifest.SchemaVersion == 1 && manifest.Cases.Count == 3 &&
        manifest.Cases.Select(fixture => fixture.Name).ToHashSet().SetEquals(new[] { "real-universal", "real-conversion", "real-irrational" }) &&
        manifest.Cases.All(fixture => fixture.File == fixture.Name + ".dfy" && fixture.ExpectedUnits == (fixture.Name == "real-universal" ? 2 : 1) &&
          fixture.Expected == (fixture.Name == "real-irrational" ? Ir.Outcome.Failed : Ir.Outcome.Verified) &&
          fixture.RequiredFailureLine == (fixture.Name == "real-irrational" ? 4 : 0)), "Expected the fixed three-case diagnostic manifest");
      var rows = new List<object>(); var baselineMatched = 0; var sourceCasesPrepared = 0;
      foreach (var fixture in manifest.Cases) {
        var directory = Path.Combine(settings.Output, fixture.Name); Directory.CreateDirectory(directory);
        try {
          var bytes = await File.ReadAllBytesAsync(Path.Combine(settings.Fixtures, "original", fixture.File));
          Require(bytes.Length is > 0 and <= 65536 && Digest(bytes) == fixture.Sha256, "Original source fixture bytes changed");
          await File.WriteAllBytesAsync(Path.Combine(directory, fixture.File), bytes);
          Microsoft.Dafny.Type.ResetScopes();
          var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
          options.ApplyDefaultOptionsWithoutSettingsDefault();
          options.DafnyPrelude = Path.Combine(settings.Compiler, "DafnyPrelude.bpl");
          options.TimeLimit = TimeoutMilliseconds / 1000; options.ResourceLimit = (uint)BaselineLimit; options.VcsCores = 1;
          options.Set(BoogieOptionBag.ArithmeticSolver, 2);
          options.Set(CommonOptionBag.AllowWarnings, false); options.FailOnWarnings = true;
          options.Set(Snippets.ShowSnippets, false); options.UseBaseNameForFileName = true;
          var reporter = new BatchErrorReporter(options);
          var parsed = await ProgramParser.Parse(Encoding.UTF8.GetString(bytes), new Uri("file:///B3RealTriage/original/" + fixture.File), reporter);
          Require(!reporter.HasErrors, "Source parse rejected");
          await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
          Require(!reporter.HasErrors, "Source resolution rejected");
          var prepared = new List<(Ir.Program Program, IReadOnlyList<Ir.SourceIdentity> Obligations, string Key)>();
          var moduleOrdinal = 0;
          foreach (var (_, source) in BoogieGenerator.Translate(parsed.Program, reporter)) {
            var sink = new ErrorSink();
            Require(source.Resolve(options, sink) == 0 && source.Typecheck(options, sink) == 0,
              "Boogie typed artifact rejected: " + string.Join("; ", sink.Errors));
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using (var writer = new Bpl.TokenTextWriter(output, options)) { source.Emit(writer); }
            Require(output.GetStringBuilder().Length <= Ir.Protocol.MaximumMessageBytes, "Typed source dump exceeds bound");
            var before = output.ToString();
            await File.WriteAllTextAsync(Path.Combine(directory, "module-" + moduleOrdinal + ".typed.bpl"), before);
            var selected = source.Implementations.Where(implementation =>
                ((Bpl.ExecutionEngineOptions)options).UserWantsToCheckRoutine(implementation.VerboseName) && !implementation.IsSkipVerification(options))
              .OrderBy(implementation => implementation.Name, StringComparer.Ordinal).ToArray();
            foreach (var implementation in selected) {
              var normalized = B3Normalizer.Normalize(source, implementation, options);
              Require(normalized.Success && normalized.Program != null,
                "Normalization rejected: " + string.Join("; ", normalized.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
              var program = normalized.Program!; var key = "unit-" + prepared.Count;
              var checks = Checks(program.Unit.Body);
              Require(checks.Count > 0 && checks.Select(check => check.Check.ObligationId).Distinct().Count() == checks.Count &&
                checks.Select(check => check.Check.ObligationId).ToHashSet().SetEquals(normalized.Obligations.Select(obligation => obligation.Id)),
                "Original checks and obligation ledger differ");
              await Save(directory, key + ".program.json", program);
              await Save(directory, key + ".ledger.json", new {
                originalModuleOrdinal = moduleOrdinal, implementation.Name, implementation.VerboseName,
                programHash = Ir.Protocol.GetProgramHash(program), normalized.Obligations,
                checks = checks.Select(check => new { check.Path, check.Check, source = normalized.Obligations.Single(obligation => obligation.Id == check.Check.ObligationId) }),
                normalized.Approximations, sourceSha256 = fixture.Sha256
              });
              prepared.Add((program, normalized.Obligations, key));
            }
            using var afterOutput = new StringWriter(CultureInfo.InvariantCulture);
            using (var writer = new Bpl.TokenTextWriter(afterOutput, options)) { source.Emit(writer); }
            Require(before == afterOutput.ToString(), "Normalization mutated the emitted typed source");
            moduleOrdinal++;
          }
          await Save(directory, "frontend.json", new {
            errors = reporter.ErrorCount, warnings = reporter.WarningCount, options.FailOnWarnings,
            failCompilation = reporter.FailCompilation, reporter.FailCompilationMessage,
            diagnostics = reporter.AllMessages.Select(message => new { level = message.Level.ToString(), source = message.Source.ToString(),
              message.ErrorId, message.Message, line = message.Range.StartToken.line, column = message.Range.StartToken.col }),
            options = new { options.TypeEncodingMethod, options.UseSubsumption, options.TimeLimit, options.ResourceLimit,
              arithmeticSolver = options.GetOrOptionDefault(BoogieOptionBag.ArithmeticSolver), options.VcsCores, additionalAxioms = false }
          });
          Require(!reporter.HasErrors && prepared.Count == fixture.ExpectedUnits, "Incomplete selected source-unit denominator");
          sourceCasesPrepared++;
          foreach (var budget in new[] { BaselineLimit }.Concat(settings.Exploratory)) {
            var unitRows = new List<object>(); var unitMatches = 0;
            foreach (var (program, obligations, key) in prepared) {
              var currentPackage = await Ir.WorkerPackage.LoadAsync(settings.Worker);
              Require(currentPackage.Fingerprint == package.Fingerprint, "Worker package changed before launch");
              var request = new Ir.Request(Ir.Protocol.Version, fixture.Name + "-" + key + "-" + budget,
                Ir.Protocol.NormalizerVersion, package.Manifest.B3Commit, Ir.Protocol.GetProgramHash(program), program.Unit.Name, program,
                new Ir.Configuration(settings.Solver, new[] { "-in", "-smt2" }, TimeoutMilliseconds, budget, 1048576, 2,
                  "5.1.0", SolverDigest), obligations, package.Fingerprint);
              Ir.ProtocolValidation.ValidateRequest(request);
              var prefix = key + ".rlimit-" + budget;
              await Save(directory, prefix + ".request.json", request);
              var completion = await new Ir.WorkerProcessClient(settings.Dotnet, new[] { package.WorkerPath }).RunAsync(request, CancellationToken.None);
              await Save(directory, prefix + ".completion.json", completion);
              var attempted = completion.Attempts.Select(attempt => attempt.ObligationId).ToHashSet(StringComparer.Ordinal);
              var covered = attempted.SetEquals(obligations.Select(obligation => obligation.Id));
              var requiredFailure = fixture.Expected == Ir.Outcome.Failed && completion.Attempts.Any(attempt => attempt.Outcome == Ir.Outcome.Failed &&
                obligations.Single(obligation => obligation.Id == attempt.ObligationId).Line == fixture.RequiredFailureLine);
              var matched = completion.TraversalCompleted && completion.Error == null && covered && completion.Outcome == fixture.Expected &&
                (fixture.Expected == Ir.Outcome.Verified ? completion.Attempts.All(attempt => attempt.Outcome == Ir.Outcome.Verified) : requiredFailure);
              if (matched) { unitMatches++; }
              unitRows.Add(new { key, request.ProgramHash, expected = fixture.Expected, completion.Outcome, completion.TraversalCompleted,
                completion.Error, checkCoverageComplete = covered, expectedFailedSourceLineObserved = requiredFailure,
                completion.Attempts, matched, actualProofResourceCount = (long?)null });
            }
            var workersMatched = unitMatches == prepared.Count;
            var strictMatched = workersMatched && !reporter.FailCompilation;
            if (budget == BaselineLimit && strictMatched) { baselineMatched++; }
            rows.Add(new { fixture = fixture.Name, fixture.Sha256, budget, exploratory = budget != BaselineLimit,
              timeoutMilliseconds = TimeoutMilliseconds, expectedUnits = fixture.ExpectedUnits, actualUnits = prepared.Count,
              frontendAccepted = !reporter.FailCompilation, workersMatched, strictMatched, units = unitRows });
          }
        } catch (Exception exception) {
          rows.Add(new { fixture = fixture.Name, preparationFailed = true, strictMatched = false,
            errorType = exception.GetType().Name, error = exception.Message });
        }
      }
      var loadedBoogie = AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
        assembly.GetName().Name?.StartsWith("Boogie.", StringComparison.Ordinal) == true).OrderBy(assembly => assembly.FullName, StringComparer.Ordinal).ToArray();
      var observedBoogieReferences = new List<object>();
      foreach (var assembly in loadedBoogie) {
        var name = Path.GetFileName(assembly.Location); var sha = Digest(assembly.Location);
        Require(compilerReferences.Any(reference => reference.file == name && reference.assemblyIdentity == assembly.FullName && reference.sha256 == sha),
          "Observed loaded Boogie assembly differs from the captured compiler references");
        observedBoogieReferences.Add(new { file = name, assemblyIdentity = assembly.FullName, sha256 = sha,
          declaredReferences = assembly.GetReferencedAssemblies().Select(reference => reference.FullName) });
      }
      await Save(settings.Output, "observed-boogie-references.json", new { observedBoogieReferences,
        identityEvidenceOnly = true, signedAttestationClaimed = false });
      await Save(settings.Output, "summary.json", new { diagnosticOnly = true, identity, sourceCasesPrepared,
        expectedSourceCases = 3, baselineMatched, expectedBaselineCases = 3, baselineAllMatched = baselineMatched == 3,
        rows, actualProofResourceCount = (long?)null, sourceProgramsOrQueriesRewritten = false });
      return baselineMatched == 3 && sourceCasesPrepared == 3 ? 0 : 1;
    } catch (Exception exception) {
      Console.Error.WriteLine(exception.GetType().Name + ": " + exception.Message);
      if (settings != null && Directory.Exists(settings.Output)) {
        await Save(settings.Output, "tool-error.json", new { diagnosticOnly = true, strictMatched = false, error = exception.Message });
      }
      return 2;
    }
  }

  private static List<(string Path, Ir.Check Check)> Checks(Ir.Statement root) {
    var result = new List<(string Path, Ir.Check Check)>();
    var pending = new Stack<(Ir.Statement Statement, string Path)>(); pending.Push((root, "body")); var count = 0;
    while (pending.Count > 0) {
      var (statement, path) = pending.Pop(); Require(++count <= Ir.Protocol.MaximumNodes, "Static check traversal exceeds bound");
      if (statement is Ir.Check check) { result.Add((path, check)); }
      IReadOnlyList<Ir.Statement> children = statement switch {
        Ir.Block block => block.Statements, Ir.Choice choice => choice.Branches,
        Ir.Conditional conditional => new Ir.Statement[] { conditional.Then, conditional.Else },
        Ir.Loop loop => new[] { loop.Body }, Ir.Labeled labeled => new[] { labeled.Body },
        Ir.Assign or Ir.Havoc or Ir.Check or Ir.Assume or Ir.Exit or Ir.Return => Array.Empty<Ir.Statement>(),
        _ => throw new InvalidDataException("Unknown statement in static ledger")
      };
      for (var i = children.Count - 1; i >= 0; i--) { pending.Push((children[i], path + "/" + i)); }
    }
    return result;
  }
  private static Task Save(string directory, string file, object value) =>
    File.WriteAllBytesAsync(Path.Combine(directory, file), JsonSerializer.SerializeToUtf8Bytes(value, Ir.Protocol.JsonOptions));
  private static string Digest(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant(); }
  private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  private static void Require(bool condition, string message) { if (!condition) { throw new InvalidDataException(message); } }
  private sealed class ErrorSink : Bpl.IErrorSink {
    public readonly List<string> Errors = new();
    public void Error(Bpl.IToken token, string message) => Errors.Add(token.line + ":" + token.col + ": " + message);
  }
  private sealed record FixtureManifest(int SchemaVersion, IReadOnlyList<Fixture> Cases);
  private sealed record Fixture(string Name, string File, string Sha256, int ExpectedUnits, Ir.Outcome Expected, int RequiredFailureLine);
  private sealed record Settings(string Compiler, string CompilerVersion, string CompilerSource, string Fixtures,
    string Worker, string WorkerFingerprint, string Solver, string Dotnet, string Output, IReadOnlyList<long> Exploratory) {
    public static Settings Parse(string[] args) {
      var values = new Dictionary<string, string>();
      var supported = new HashSet<string> { "--compiler", "--compiler-version", "--compiler-source", "--fixtures", "--worker",
        "--worker-fingerprint", "--solver", "--dotnet", "--output", "--exploratory-rlimits" };
      for (var i = 0; i < args.Length; i += 2) {
        Require(i + 1 < args.Length && supported.Contains(args[i]) && values.TryAdd(args[i], args[i + 1]), "Invalid or duplicate setting");
      }
      string Required(string key) { Require(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), "Missing " + key); return value!; }
      var source = Required("--compiler-source"); var version = Required("--compiler-version"); var fingerprint = Required("--worker-fingerprint");
      Require(source.Length == 40 && source.All(Uri.IsHexDigit) && version.StartsWith("4.11.0+", StringComparison.Ordinal) &&
        fingerprint.Length == 64 && fingerprint.All(Uri.IsHexDigit), "Invalid source/version/package identity");
      var budgets = values.TryGetValue("--exploratory-rlimits", out var extra) ? extra.Split(',').Select(value => long.Parse(value, CultureInfo.InvariantCulture)).ToArray() : Array.Empty<long>();
      Require(budgets.Length <= 3 && budgets.Distinct().Count() == budgets.Length && budgets.All(budget => budget > BaselineLimit && budget <= uint.MaxValue), "Invalid exploratory budgets");
      return new Settings(Path.GetFullPath(Required("--compiler")), version, source, Path.GetFullPath(Required("--fixtures")),
        Path.GetFullPath(Required("--worker")), fingerprint, Path.GetFullPath(Required("--solver")), Path.GetFullPath(Required("--dotnet")),
        Path.GetFullPath(Required("--output")), budgets);
    }
  }
}
