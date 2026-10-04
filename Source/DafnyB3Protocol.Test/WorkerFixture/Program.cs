using System.Diagnostics;
using System.Text.Json;
using DafnyB3Protocol;

var mode = args[0];
var request = JsonSerializer.Deserialize<Request>(await Console.In.ReadLineAsync() ?? "", Protocol.JsonOptions)!;
if (!UnixProcessGroup.IsolateCurrentProcess()) { return 70; }
var started = new WorkerStarted(Protocol.Version, request.RequestId, 0, Environment.ProcessId, true);
Console.WriteLine(JsonSerializer.Serialize(started, Protocol.JsonOptions));
Console.Out.Flush();
if (mode == "crash-with-child" || mode == "hang-with-child") {
  var child = Process.Start(new ProcessStartInfo("/bin/sleep", "600") { UseShellExecute = false })!;
  await File.WriteAllTextAsync(args[1], child.Id.ToString());
  if (mode == "crash-with-child") { return 0; }
  await Task.Delay(Timeout.Infinite);
}
if (mode == "missing") { return 0; }
if (mode == "truncated") { Console.Write("{\"version\":1"); return 0; }
if (mode == "stderr") {
  for (var i = 0; i < 100; i++) { Console.Error.WriteLine(new string('e', 4096)); }
}
var completion = new Completion(Protocol.Version, request.RequestId, request.ProgramHash,
  request.UnitId, request.B3Commit, true, Outcome.Verified,
  new[] { new Attempt(0, "sO0", Outcome.Verified, null) }, null, request.WorkerFingerprint);
if (mode == "wrong-id") { completion = completion with { RequestId = "wrong" }; }
Console.WriteLine(JsonSerializer.Serialize(completion, Protocol.JsonOptions));
return 0;
