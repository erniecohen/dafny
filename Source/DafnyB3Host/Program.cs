using System.Numerics;
using System.Text.Json;
using DafnyB3Protocol;
using DafnyB3Host;

Request? request = null;
try {
  if (!UnixProcessGroup.IsolateCurrentProcess()) { throw new PlatformNotSupportedException("B3 workers require Unix process-group isolation"); }
  using var standardInput = new StreamReader(Console.OpenStandardInput());
  var input = await WorkerProcessClient.ReadBoundedLineAsync(standardInput, CancellationToken.None);
  request = JsonSerializer.Deserialize<Request>(input, Protocol.JsonOptions)
    ?? throw new InvalidDataException("Missing B3 request");
  ProtocolValidation.ValidateRequest(request);
  var package = WorkerPackage.Load(System.Reflection.Assembly.GetExecutingAssembly().Location);
  if (request.B3Commit != package.Manifest.B3Commit || request.WorkerFingerprint != package.Fingerprint) {
    throw new InvalidDataException("B3 request identifies a different worker build");
  }
  Console.WriteLine(JsonSerializer.Serialize(new WorkerStarted(Protocol.Version, request.RequestId,
    0, Environment.ProcessId, true), Protocol.JsonOptions));
  Console.Out.Flush();
  await SolverIdentity.ValidateAsync(request.Configuration);
  var configuration = SolverConfiguration.Configuration.create(
    RawAstBuilder.S(request.Configuration.SolverExecutable),
    Dafny.Sequence<Dafny.ISequence<Dafny.Rune>>.FromArray(request.Configuration.SolverArguments.Select(RawAstBuilder.S).ToArray()),
    new BigInteger(request.Configuration.TimeoutMilliseconds), new BigInteger(request.Configuration.ResourceLimit),
    new BigInteger(request.Configuration.MaximumResponseCharacters), SolverConfiguration.SolverKind.create_Z3(),
    new BigInteger(request.Configuration.ArithmeticSolver));
  var result = B3Library.__default.CheckAndVerify(RawAstBuilder.Build(request.Program),
    RawAstBuilder.S(request.UnitId), configuration);
  var attempts = result.dtor_attempts.Select(attempt => new Attempt(checked((int)attempt.dtor_sequenceNumber),
    Text(attempt.dtor_obligationId), ConvertOutcome(attempt.dtor_outcome),
    attempt.dtor_outcome.is_Proved ? null : Text(attempt.dtor_outcome.dtor_reason),
    Description: Text(attempt.dtor_description),
    Breadcrumbs: attempt.dtor_breadcrumbs.Select(Text).ToArray())).ToArray();
  var error = result.dtor_error.is_Some ? Text(result.dtor_error.dtor_value) : null;
  var outcome = !result.dtor_complete || error is not null
    ? error?.StartsWith("unsupported:", StringComparison.Ordinal) == true ? Outcome.Unsupported : Outcome.ToolError
    : attempts.Select(attempt => attempt.Outcome).FirstOrDefault(outcome => outcome != Outcome.Verified, Outcome.Verified);
  var completion = new Completion(Protocol.Version, request.RequestId, request.ProgramHash,
    request.UnitId, request.B3Commit, result.dtor_complete, outcome, attempts, error, package.Fingerprint);
  ProtocolValidation.ValidateCompletion(request, completion);
  Console.WriteLine(JsonSerializer.Serialize(completion, Protocol.JsonOptions));
  return 0;
} catch (Exception exception) {
  Console.Error.WriteLine(exception.Message);
  return 70;
}

static string Text(Dafny.ISequence<Dafny.Rune> value) => value.ToVerbatimString(false);
static Outcome ConvertOutcome(Solvers._IProofResult result) {
  if (result.is_Proved) { return Outcome.Verified; }
  if (result.is_Unproved) { return Outcome.Failed; }
  if (result.is_ToolError) { return Outcome.ToolError; }
  var reason = Text(result.dtor_reason);
  return reason.Contains("timeout", StringComparison.OrdinalIgnoreCase) ? Outcome.TimedOut :
    reason.Contains("resource", StringComparison.OrdinalIgnoreCase) ? Outcome.ResourceExhausted : Outcome.Inconclusive;
}
