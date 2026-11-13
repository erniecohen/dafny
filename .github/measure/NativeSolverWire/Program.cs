using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.CommandLine;
using Microsoft.Dafny;
using DafnyDriver.Commands;
using Microsoft.Boogie;
using Microsoft.Boogie.SMTLib;

var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(args[0]))!;
Directory.SetCurrentDirectory(request.Cwd);
Directory.CreateDirectory(request.Destination);
var verify = DafnyNewCli.RootCommand.Children.OfType<Command>().Single(c => c.Name == "verify");
string mode = "", current = "";
var counter = 0;
var taskStarts = 0;
var captured = new List<WeakReference<RecordingSolver>>();
DafnyNewCli.SetHandlerUsingDafnyOptionsContinuation(verify, async (options, context) => {
  options.PrintFile = Path.Combine(request.LiveDirectory ?? Path.Combine(request.Destination, "live"), "program.bpl");
  var original = options.CreateSolver;
  options.CreateSolver = (library, solver) => {
    var recording = new RecordingSolver(original(library, solver), current, Interlocked.Increment(ref counter), () => Interlocked.Increment(ref taskStarts), request.CaptureNativeInput);
    lock (captured) { captured.Add(new WeakReference<RecordingSolver>(recording)); }
    return recording;
  };
  return await VerifyCommand.HandleVerification(options);
});
foreach (var selected in request.Modes) {
  mode = selected;
  current = Path.Combine(request.Destination, mode);
  Directory.CreateDirectory(current);
  var live = request.LiveDirectory ?? Path.Combine(request.Destination, "live");
  if (Directory.Exists(live)) Directory.Delete(live, true);
  Directory.CreateDirectory(live);
  if (mode == "cold") {
    var cache = typeof(DafnyOptions).Assembly.GetType("Microsoft.Dafny.ModuleTranslationCache")!;
    cache.GetProperty("ProcessCache")!.GetValue(null)!.GetType().GetMethod("Clear")!.Invoke(cache.GetProperty("ProcessCache")!.GetValue(null), null);
  }
  counter = 0; taskStarts = 0;
  captured.Clear();
  // Preserve malformed original-option diagnostics: observation options must
  // precede original options, so a missing trailing value cannot consume one.
  var observationOptions = new[] { "--log-format", "json;LogFileName=" + Path.Combine(live,"report.json") }.Concat(
    mode is "cold" or "warm" ? new[] { "--translation-cache" } : mode == "named" ? new[] { "--resolution-threads", "1" } : Array.Empty<string>());
  var command = request.Arguments.Take(1).Concat(observationOptions).Concat(request.Arguments.Skip(1)).ToArray();
  var stdout = new StringWriter(); var stderr = new StringWriter(); var clock = Stopwatch.StartNew();
  int code;
  try { code = await DafnyBackwardsCompatibleCli.MainWithWriters(stdout, stderr, TextReader.Null, command); }
  catch (Exception error) { code = -100; stderr.WriteLine(error); }
  foreach (var reference in captured.ToArray()) { if (reference.TryGetTarget(out var solver)) { await solver.FinishRecording(); } }
  foreach (var file in Directory.GetFiles(live)) File.Move(file, Path.Combine(current, Path.GetFileName(file)), true);
  File.WriteAllText(Path.Combine(current,"stdout"), stdout.ToString());
  File.WriteAllText(Path.Combine(current,"stderr"), stderr.ToString());
  File.WriteAllText(Path.Combine(current,"meta.json"), JsonSerializer.Serialize(new { code, command, wall=clock.Elapsed.TotalSeconds, solvers=counter, taskStarts }));
  var modules = Directory.GetFiles(current,"*.bpl").ToDictionary(Path.GetFileName, p=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p))));
  File.WriteAllText(Path.Combine(current,"boogie.json"),JsonSerializer.Serialize(modules));
  Console.WriteLine(mode+" "+code+" "+clock.Elapsed.TotalSeconds);
}
record Request(string Cwd, string[] Arguments, string Destination, string[] Modes, string LiveDirectory, bool CaptureNativeInput = false);

sealed class RecordingSolver : SMTLibSolver {
  private readonly SMTLibSolver inner;
  private readonly System.Action taskStarted;
  private readonly IncrementalHash commandHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  private long commandLength;
  private void Record(string kind, string value) {
    var bytes = JsonSerializer.SerializeToUtf8Bytes(new[] { kind, value });
    var frame = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((long)bytes.Length));
    commandHash.AppendData(frame); commandHash.AppendData(bytes); commandLength += frame.Length + bytes.Length;
  }
  private readonly string directory;
  private readonly int number;
  private string problem = "";
  private int query;
  private bool closed;
  private readonly WireWriter wire;
  private readonly List<Task> pings = new();
  private readonly object lifecycle = new();
  private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private int startedProblems, cleanupPings;
  private string[] lastPingFrames = Array.Empty<string>();
  private bool finalRecorded;
  private void Changed() { var previous=changed; changed=new(TaskCreationOptions.RunContinuationsAsynchronously); previous.TrySetResult(); }
  private void RecordFinal() {
    lock(lifecycle) {
      if(finalRecorded) return;
      finalRecorded=true;
      var snapshot=wire.Snapshot();
      var record=new { problem,query=0,kind="wire-final",sha256=snapshot.Hash,length=snapshot.Length };
      File.AppendAllText(Path.Combine(directory,"solver-"+number+".jsonl"),JsonSerializer.Serialize(record)+"\n");
    }
  }
  public RecordingSolver(SMTLibSolver inner, string directory, int number, System.Action taskStarted, bool captureNativeInput) {
    this.taskStarted=taskStarted;
    this.inner=inner;this.directory=directory;this.number=number;
    var field = inner.GetType().GetField("toProver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
      ?? throw new InvalidOperationException("native solver input writer not found");
    var original = (TextWriter)field.GetValue(inner)!;
    wire = new WireWriter(original, record => File.AppendAllText(
      Path.Combine(directory, "solver-"+number+".jsonl"), JsonSerializer.Serialize(record)+"\n"), captureNativeInput ? new BufferedStream(new FileStream(
        Path.Combine(directory, "solver-"+number+".smt2"), FileMode.Create, FileAccess.Write, FileShare.Read), 65536) : null);
    field.SetValue(inner, wire);
  }
  public async Task FinishRecording() {
    // Completed status can reach the CLI before Boogie's async enumerator invokes
    // its existing GoBackToIdle handshake. Await that passive lifecycle event for
    // every started problem, then await only the handshakes already invoked.
    // No request is inserted, suppressed, reordered or normalized.
    while(true) {
      Task next;
      lock(lifecycle) { if(closed || cleanupPings>=startedProblems) break; next=changed.Task; }
      try { await next.WaitAsync(TimeSpan.FromMinutes(3)); }
      catch(TimeoutException) {
        File.WriteAllText(Path.Combine(directory,"solver-"+number+".lifecycle.json"),JsonSerializer.Serialize(new {problem,closed,startedProblems,cleanupPings,pings=pings.Count,lastPingFrames}));
        throw;
      }
    }
    Task[] pending; lock(lifecycle) { pending=pings.ToArray(); }
    foreach(var ping in pending) { try { await ping; } catch { } }
    RecordFinal();
  }
  public override event Action<string> ErrorHandler { add { inner.ErrorHandler+=value; } remove { inner.ErrorHandler-=value; } }
  public override void Send(string command) {
    Record("send",command); inner.Send(command);
    // The reset establishes an empty solver checkpoint. Its following bytes,
    // rather than previous tasks or the checkpoint operation itself, are owned
    // by the next task. A fresh process starts at the equivalent empty checkpoint.
    if (command == "(reset)") wire.BeginTask("reset");
  }
  public override void NewProblem(string name) { lock(lifecycle) { problem=name; startedProblems++; } wire.NameTask(name); taskStarted(); Record("problem",name);inner.NewProblem(name); }
  public override Task PingPong() {
    var frames = new StackTrace().GetFrames().Select(frame => frame.GetMethod()).Where(method => method != null).ToArray();
    lastPingFrames = frames.Select(method => method.DeclaringType?.FullName+"."+method.Name).ToArray();
    var isCleanup = frames.Any(method => method.DeclaringType?.FullName?.StartsWith("Microsoft.Boogie.") == true &&
      (method.Name == "GoBackToIdle" || method.DeclaringType.Name.StartsWith("<GoBackToIdle>")));
    // Idle cleanup is physical-pool bookkeeping, outside the completed task.
    if (isCleanup) wire.FinishTask();
    Record("ping","");var ping=inner.PingPong();
    lock(lifecycle) { pings.Add(ping); if(isCleanup) cleanupPings++; Changed(); }
    return ping;
  }
  public override async Task<SExpr> SendRequest(string command, CancellationToken token=default) {
    Record("request",command);
    if(command=="(check-sat)") {
      var record=new { problem, query=++query, sha256=Convert.ToHexStringLower(commandHash.GetCurrentHash()), length=commandLength };
      File.AppendAllText(Path.Combine(directory,"solver-"+number+".jsonl"),JsonSerializer.Serialize(record)+"\n");
    }
    return await inner.SendRequest(command,token);
  }
  public override void AddErrorHandler(Action<string> handler) => inner.AddErrorHandler(handler);
  public override Task<IReadOnlyList<SExpr>> SendRequestsAndCloseInput(IReadOnlyList<string> requests,CancellationToken token=default) {
    foreach (var request in requests) { Record("batch-request",request); }
    return inner.SendRequestsAndCloseInput(requests,token);
  }
  public override void Close() {
    try { wire.FinishTask(); inner.Close(); }
    finally {
      lock(lifecycle) {
      if (!closed) {
        closed = true; Changed();

        var record = new { problem, query = 0, kind = "final", sha256 = Convert.ToHexStringLower(commandHash.GetCurrentHash()), length = commandLength };
        File.AppendAllText(Path.Combine(directory,"solver-"+number+".jsonl"),JsonSerializer.Serialize(record)+"\n");
      }
      }
      RecordFinal();
    }
  }
}

// Observe the exact characters passed to the native process writer, including
// internal (echo "ping") handshakes and adaptive requests. The native StreamWriter
// encoding/newline are retained; no solver command or option is inserted.
sealed class WireWriter(TextWriter inner, Action<object> publish, Stream? capture = null) : TextWriter {
  private readonly object gate = new();
  private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  private long length;
  private IncrementalHash taskHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  private long taskLength;
  private long taskStartOffset;
  private int taskOrdinal;
  private string prefixHash = Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>()));
  private string? taskName;
  private string checkpoint = "fresh-process";
  private bool active = true;
  public void BeginTask(string kind) {
    lock(gate) {
      if (taskName != null) throw new InvalidOperationException("reset before task completion: "+taskName);
      taskHash.Dispose(); taskHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
      taskLength=0; checkpoint=kind; active=true; taskStartOffset=length;
      prefixHash=Convert.ToHexStringLower(hash.GetCurrentHash());
    }
  }
  public void NameTask(string name) {
    lock(gate) {
      if (!active || taskName != null) throw new InvalidOperationException("missing/overlapping task checkpoint: "+name);
      taskName=name;
    }
  }
  public void FinishTask() {
    lock(gate) {
      if (taskName != null) {
        capture?.Flush();
        publish(new { kind="task-wire", problem=taskName, checkpoint, ordinal=++taskOrdinal,
          start=taskStartOffset, end=length, prefixSha256=prefixHash,
          sha256=Convert.ToHexStringLower(taskHash.GetCurrentHash()), length=taskLength });
        taskName=null;
      }
      active=false;
    }
  }
  public override Encoding Encoding => inner.Encoding;
  private void Add(string value) { var bytes=Encoding.GetBytes(value); hash.AppendData(bytes); capture?.Write(bytes); length+=bytes.Length; if(active) { taskHash.AppendData(bytes); taskLength+=bytes.Length; } }
  public override void WriteLine(string? value) { lock(gate) { inner.WriteLine(value); Add((value ?? "")+inner.NewLine); } }
  public override void Write(string? value) { lock(gate) { inner.Write(value); if(value != null) Add(value); } }
  public override void Write(char value) { lock(gate) { inner.Write(value); Add(value.ToString()); } }
  public override void Flush() { lock(gate) { inner.Flush(); } }
  public (string Hash,long Length) Snapshot() { lock(gate) { capture?.Flush(); return (Convert.ToHexStringLower(hash.GetCurrentHash()),length); } }
}
