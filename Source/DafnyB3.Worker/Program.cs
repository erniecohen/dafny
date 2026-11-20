using DafnyB3;

// Explicitly invoked transport scaffold. No B3 implementation is admitted by this milestone.
// In particular, the original B3 CLI's exit status cannot become a proof result.
if (args.Length != 1 || args[0] != "--protocol-v1") { return 2; }
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
try {
  var input = Console.OpenStandardInput();
  var request = await Protocol.ReadAsync<Request>(input, deadline.Token)
    ?? throw new ProtocolException("Missing request");
  Protocol.Validate(request);
  if (await Protocol.ReadAsync<Request>(input, deadline.Token) != null) {
    throw new ProtocolException("Only one request is permitted");
  }
  var output = Console.OpenStandardOutput();
  var sequence = 0;
  foreach (var obligation in request.Obligations) {
    await Protocol.WriteAsync(output, new Response(Protocol.Version, request.RequestId, request.UnitId,
      sequence++, "result", obligation, Outcome.Unsupported, 0), deadline.Token);
  }
  await Protocol.WriteAsync(output, new Response(Protocol.Version, request.RequestId, request.UnitId,
    sequence, "complete", null, Outcome.Unsupported, request.Obligations.Length), deadline.Token);
  return 1;
} catch (Exception) {
  // Exception text can contain arbitrary peer data. Keep the executable diagnostic fixed and bounded.
  Console.Error.WriteLine("B3 transport request failed");
  return 2;
}
