using System.Diagnostics;
using System.Text;
using DafnyB3;

// Adversarial test peer; never part of the worker executable.
if (args.Length == 0) { return 2; }
var mode = args[0];
if (mode == "hang-child") { await Task.Delay(Timeout.Infinite); return 2; }
if (mode.StartsWith("smt-")) { return await FakeSolver.RunAsync(mode[4..], args[1..]); }
if (mode == "no-read") { await Task.Delay(Timeout.Infinite); return 2; }
if (mode == "close") { return 0; }
var input = Console.OpenStandardInput();
var request = await Protocol.ReadAsync<Request>(input, CancellationToken.None);
if (request == null) { return 2; }
if (mode == "tree") {
  using var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) {
    ArgumentList = { typeof(TransportTests).Assembly.Location, "hang-child" }, UseShellExecute = false
  })!;
  File.WriteAllText(args[1], $"{Environment.ProcessId},{child.Id}");
  await Task.Delay(Timeout.Infinite);
}
if (mode == "stderr") { await Console.OpenStandardError().WriteAsync(new byte[1024 * 1024]); }
if (mode == "hang" || mode == "stderr-hang") {
  if (mode == "stderr-hang") {
    while (true) { await Console.OpenStandardError().WriteAsync(new byte[4096]); }
  }
  await Task.Delay(Timeout.Infinite);
}
var output = Console.OpenStandardOutput();
if (mode == "malformed") { Console.WriteLine("not JSON"); return 0; }
if (mode == "truncated") { Console.Write("{\"Version\":1"); return 0; }
if (mode == "large") { Console.Write(new string('x', Protocol.MaxFrameBytes + 1)); return 0; }
if (mode == "invalid-utf8") { await output.WriteAsync(new byte[] { 255, 10 }); return 0; }
if (mode == "empty") { return 0; }
var sequence = 0;
foreach (var id in request.Obligations) {
  var response = new Response(Protocol.Version, request.RequestId, request.UnitId, sequence++, "result", id,
    mode == "unknown" ? Outcome.Unknown : Outcome.Proved, 0);
  response = mode switch {
    "version" => response with { Version = 2 },
    "stale" => response with { RequestId = "old" },
    "unit" => response with { UnitId = "other" },
    "sequence" => response with { Sequence = 42 },
    "foreign" => response with { ObligationId = "foreign" },
    _ => response
  };
  if (mode == "missing") { break; }
  await Protocol.WriteAsync(output, response, CancellationToken.None);
  if (mode == "duplicate") { await Protocol.WriteAsync(output, response, CancellationToken.None); }
}
var complete = new Response(Protocol.Version, request.RequestId, request.UnitId, sequence, "complete", null,
  mode == "unknown" ? Outcome.Unknown : Outcome.Proved, request.Obligations.Length);
if (mode == "count") { complete = complete with { Completed = 0 }; }
if (mode != "no-completion") { await Protocol.WriteAsync(output, complete, CancellationToken.None); }
if (mode == "trailing") { Console.WriteLine("{}"); }
if (mode == "late-error") { Console.WriteLine("solver error"); }
if (mode == "no-exit") { await Task.Delay(Timeout.Infinite); }
return mode == "crash" ? 3 : 0;
