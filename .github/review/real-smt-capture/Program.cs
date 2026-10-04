using System.Security.Cryptography;
using System.Text.Json;
using Ir = DafnyB3Protocol;

// Diagnostic replay only. The verified worker/library and solver are never built here.
internal static class Program {
  private const string ProtocolDigest = "8ef317f868a9d8fb58aa2c3dbc78d57cf0bff3d7fc6439a011c971fa037292be";
  private const string WorkerFingerprint = "785fdda10ac925f7b14cc5556831843eb6d8a46f0f7b51054bbe84fc3f07d822";
  private const string SolverDigest = "b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23";

  public static async Task<int> Main(string[] args) {
    var settings = Arguments(args);
    Require(Hash(await Bytes(typeof(Ir.Protocol).Assembly.Location, 32 * 1024 * 1024)) == ProtocolDigest,
      "Loaded protocol assembly differs from the frozen diagnostic");
    if (settings["mode"] == "prepare") {
      await Prepare(settings["baseline"], settings["output"]);
      return 0;
    }
    Require(settings["mode"] == "replay", "Unknown diagnostic mode");
    var bytes = await Bytes(settings["request"], 1024 * 1024);
    Require(Hash(bytes) == settings["request-sha256"], "Frozen request file changed");
    var request = Read(bytes);
    var package = await Ir.WorkerPackage.LoadAsync(settings["worker"]);
    Require(package.Fingerprint == WorkerFingerprint && request.WorkerFingerprint == WorkerFingerprint,
      "Worker manifest differs from the verified library package");
    var completion = await new Ir.WorkerProcessClient(settings["dotnet"], new[] { package.WorkerPath })
      .RunAsync(request, CancellationToken.None);
    Ir.ProtocolValidation.ValidateCompletion(request, completion);
    var ids = request.Obligations.Select(x => x.Id).ToArray();
    var covered = completion.Attempts.Select(x => x.ObligationId).Order().SequenceEqual(ids.Order());
    var expected = Enum.Parse<Ir.Outcome>(settings["expected"], ignoreCase: false);
    Require(expected is Ir.Outcome.Verified or Ir.Outcome.Failed, "Invalid mathematical expectation");
    var matched = completion.TraversalCompleted && completion.Error == null && covered &&
      completion.Outcome == expected && (expected == Ir.Outcome.Verified
        ? completion.Attempts.All(x => x.Outcome == Ir.Outcome.Verified)
        : completion.Attempts.Any(x => x.Outcome == Ir.Outcome.Failed));
    await Save(settings["output"], new { diagnosticOnly = true, requestSha256 = Hash(bytes),
      request.RequestId, request.ProgramHash, request.UnitId, expected = expected.ToString(),
      mathematicalMatched = matched, exactCheckCoverage = covered, completion,
      actualProofResourceCount = (long?)null, acceptanceClaimed = false });
    // Unknown and ToolError are retained in the receipt; diagnostic exit zero is not acceptance.
    return 0;
  }

  private static async Task Prepare(string baseline, string output) {
    Directory.CreateDirectory(output);
    var rows = new List<object>();
    foreach (var (name, stem) in new[] { ("conversion", "real-conversion"), ("irrational", "real-irrational") }) {
      var bytes = await Bytes(Path.Combine(baseline, stem + "-unit-0.request.json"), 1024 * 1024);
      var expectedRequestHash = stem == "real-conversion"
        ? "31a80456fa7365f6812cfe7909b5f24b41c78ae9883a2fb24b90e12144bdf579"
        : "2470a4d21d01c1dc224f3cb5802985f6aaf02d1e4ee423a3d8430f9cc8aa10b4";
      Require(Hash(bytes) == expectedRequestHash, "Symbolic control basis differs from the frozen packet");
      var original = Read(bytes);
      Require(original.Program.Axioms.Count == 0, "Controls require the reviewed axiom-free packet");
      foreach (var context in new[] { "pure", "mixed" }) {
        var control = name + "-" + context;
        var body = context == "pure" ? Pure(original.Program.Unit.Body,
          original.Program.Unit.Variables.ToDictionary(x => x.Name, x => x.Type)) : original.Program.Unit.Body;
        var unit = new Ir.Unit("sExplore" + name + context,
          context == "pure" ? original.Program.Unit.Variables.Where(x => x.Type is "int" or "real").ToArray()
            : original.Program.Unit.Variables, body);
        var program = new Ir.Program(context == "pure" ? Array.Empty<string>() : original.Program.Types,
          context == "pure" ? Array.Empty<Ir.Function>() : original.Program.Functions, original.Program.Axioms, unit);
        var request = original with { RequestId = "exploratory-" + control, UnitId = unit.Name,
          Program = program, ProgramHash = Ir.Protocol.GetProgramHash(program) };
        Ir.ProtocolValidation.ValidateRequest(request);
        var path = Path.Combine(output, control + ".request.json");
        await Save(path, request);
        rows.Add(new { name = control, request = Path.GetFileName(path),
          requestSha256 = Hash(await Bytes(path, 1024 * 1024)), request.ProgramHash,
          expected = name == "conversion" ? "Verified" : "Failed", exploratory = true,
          basisProgramHash = original.ProgramHash,
          modification = context == "pure" ? "remove only reviewed opaque/bool ground scaffold; keep arithmetic statements and both checks" : "unit/request identity only; exact original mixed body",
          checkIds = original.Obligations.Select(x => x.Id) });
      }
    }
    await Save(Path.Combine(output, "controls.json"), new { diagnosticOnly = true, rows });
  }

  private static Ir.Statement Pure(Ir.Statement statement, IReadOnlyDictionary<string, string> types) => statement switch {
    Ir.Block block => new Ir.Block(block.Statements.Select(x => Pure(x, types)).ToArray()),
    Ir.Assign assign when types[assign.Variable] is "int" or "real" => assign,
    Ir.Assign => new Ir.Block(Array.Empty<Ir.Statement>()),
    Ir.Assume assume when ContainsApplication(assume.Condition) => new Ir.Block(Array.Empty<Ir.Statement>()),
    Ir.Assume or Ir.Check or Ir.Return => statement,
    _ => throw new InvalidDataException("Unexpected statement in sealed linear symbolic controls")
  };

  private static bool ContainsApplication(Ir.Expression root) {
    var pending = new Stack<(Ir.Expression, int)>(); pending.Push((root, 0)); var count = 0;
    while (pending.Count > 0) {
      var (expr, depth) = pending.Pop(); Require(++count <= 1000 && depth <= 32, "Control expression bound");
      if (expr is Ir.Application) { return true; }
      if (expr is Ir.Operation op) { foreach (var arg in op.Arguments) { pending.Push((arg, depth + 1)); } }
      else { Require(expr is Ir.Variable or Ir.BooleanLiteral or Ir.IntegerLiteral or Ir.RationalLiteral,
        "Unexpected expression in sealed symbolic controls"); }
    }
    return false;
  }

  private static Ir.Request Read(byte[] bytes) {
    var request = JsonSerializer.Deserialize<Ir.Request>(bytes, Ir.Protocol.JsonOptions)
      ?? throw new InvalidDataException("Null request");
    Require(bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(request, Ir.Protocol.JsonOptions)),
      "Request reserialization is not byte-identical");
    Ir.ProtocolValidation.ValidateRequest(request);
    Require(request.Version == 2 && request.NormalizerVersion == "experimental-2" &&
      request.Configuration.TimeoutMilliseconds == 20000 && request.Configuration.ResourceLimit == 200000 &&
      request.Configuration.ArithmeticSolver == 2 && request.Configuration.SolverVersion == "5.1.0" &&
      request.Configuration.SolverSha256 == SolverDigest &&
      request.Configuration.SolverArguments.SequenceEqual(new[] { "-in", "-smt2" }), "Baseline pins differ");
    return request;
  }

  private static Dictionary<string, string> Arguments(string[] args) {
    Require(args.Length % 2 == 0 && args.Length <= 16, "Expected bounded named arguments");
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i += 2) {
      Require(args[i].StartsWith("--", StringComparison.Ordinal) && args[i + 1].Length <= 4096,
        "Invalid diagnostic argument");
      Require(result.TryAdd(args[i][2..], args[i + 1]), "Duplicate argument");
    }
    var mode = result.GetValueOrDefault("mode");
    var expected = mode == "prepare" ? new[] { "mode", "baseline", "output" } :
      new[] { "mode", "request", "request-sha256", "worker", "dotnet", "expected", "output" };
    Require(result.Keys.Order().SequenceEqual(expected.Order()), "Unknown/missing argument");
    return result;
  }
  private static async Task<byte[]> Bytes(string path, int maximum) {
    var info = new FileInfo(path); Require(info.Exists && info.Length > 0 && info.Length <= maximum &&
      info.LinkTarget == null, "Expected bounded regular input");
    var result = await File.ReadAllBytesAsync(path);
    Require(result.Length == info.Length && result.Length <= maximum, "Input length changed"); return result;
  }
  private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  private static Task Save(string path, object value) => File.WriteAllBytesAsync(path,
    JsonSerializer.SerializeToUtf8Bytes(value, Ir.Protocol.JsonOptions));
  private static void Require(bool condition, string message) { if (!condition) { throw new InvalidDataException(message); } }
}
