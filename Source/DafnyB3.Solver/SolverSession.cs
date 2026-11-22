using System.Diagnostics;
using System.Text;

namespace DafnyB3.Solver;

public sealed record SolverCommand(string Executable, IReadOnlyList<string> Arguments);

/// <summary>
/// Limits for one session. <see cref="CommandDeadline"/> is a wall-clock safety cap on each exchange,
/// including a check; <see cref="ResourceLimit"/> and <see cref="TimeoutMilliseconds"/> are passed to the
/// solver as options, so that the solver itself reports a limit as <c>unknown</c>.
/// </summary>
public sealed record SolverOptions(TimeSpan CommandDeadline, long? ResourceLimit = null, int? TimeoutMilliseconds = null,
  int MaxResponseBytes = SmtReader.DefaultMaxBytes, int MaxDepth = SmtReader.DefaultMaxDepth);

/// <summary>
/// The verdict for one goal. <see cref="Outcome.Proved"/> means: the session was aligned, the push and the
/// assertion of exactly <see cref="SubmittedAssertion"/> were acknowledged, the check-sat that followed was
/// answered by the exact atom <c>unsat</c>, and the transcript was still aligned after the answer and the pop.
/// </summary>
public sealed record GoalResult(string GoalId, Outcome Outcome, string SubmittedAssertion, string? Reason);

public sealed class SolverException(string message) : IOException(message);

/// <summary>
/// A lock-step SMT-LIB session with one solver process. Every command but <c>check-sat</c>, <c>echo</c> and
/// <c>get-info</c> must be acknowledged by exactly <c>success</c> (<c>:print-success</c>); an echo of a fresh
/// nonce brackets each check so that a stale, duplicated, missing or reordered response misaligns the
/// transcript and is caught. Any failure poisons the session: it kills the solver, and every later call fails.
/// Only <see cref="CheckGoalAsync"/> can produce <see cref="Outcome.Proved"/>, and only as described on
/// <see cref="GoalResult"/>. A caller must also require <see cref="CloseAsync"/> to succeed before it
/// reports any proof from the session.
/// </summary>
public sealed class SolverSession : IAsyncDisposable {
  public const int MaxStandardErrorBytes = 8192;
  private const int MaxGoalIdLength = 128;

  private static readonly HashSet<string> DeclarationCommands = new(StringComparer.Ordinal) {
    "declare-sort", "define-sort", "declare-const", "declare-fun", "define-fun", "define-fun-rec",
    "define-funs-rec", "declare-datatype", "declare-datatypes"
  };
  private static readonly SAtom Success = new("success");

  private readonly Process process;
  private readonly SolverOptions options;
  private readonly SmtReader reader;
  private readonly Stream input;
  private readonly Task<(string Text, bool Truncated)> drain;
  private readonly CancellationTokenSource lifetime = new();
  private readonly List<GoalResult> results = new();
  private readonly string sessionNonce = Guid.NewGuid().ToString("N");
  private int syncCounter;
  private bool poisoned;
  private bool closed;
  private bool disposed;

  public bool Poisoned => poisoned;

  private SolverSession(Process process, SolverOptions options) {
    this.process = process;
    this.options = options;
    input = process.StandardInput.BaseStream;
    reader = new SmtReader(process.StandardOutput.BaseStream, options.MaxResponseBytes, options.MaxDepth);
    drain = DrainAsync(process.StandardError.BaseStream, lifetime.Token);
  }

  public static async Task<SolverSession> StartAsync(SolverCommand command, SolverOptions options,
    CancellationToken cancellationToken = default) {
    if (options.CommandDeadline <= TimeSpan.Zero || options.CommandDeadline > TimeSpan.FromHours(1) ||
        options.ResourceLimit is <= 0 || options.TimeoutMilliseconds is <= 0 ||
        options.MaxResponseBytes <= 0 || options.MaxDepth <= 0) {
      throw new ArgumentOutOfRangeException(nameof(options));
    }
    var process = new Process {
      StartInfo = new ProcessStartInfo(command.Executable) {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
      }
    };
    foreach (var argument in command.Arguments) { process.StartInfo.ArgumentList.Add(argument); }
    try {
      if (!process.Start()) { throw new SolverException("Solver did not start"); }
    } catch {
      process.Dispose();
      throw;
    }
    var session = new SolverSession(process, options);
    try {
      await session.RunAsync(async token => {
        await session.CommandAsync("(set-option :print-success true)", token);
        if (options.ResourceLimit is { } rlimit) { await session.CommandAsync($"(set-option :rlimit {rlimit})", token); }
        if (options.TimeoutMilliseconds is { } ms) { await session.CommandAsync($"(set-option :timeout {ms})", token); }
        await session.SyncAsync(token);
        return 0;
      }, cancellationToken);
    } catch {
      await session.DisposeAsync();
      throw;
    }
    return session;
  }

  /// <summary>Submits one declaration or definition command and requires its acknowledgement.</summary>
  public Task DeclareAsync(string command, CancellationToken cancellationToken = default) {
    var parsed = Parse(command);
    if (parsed is not SList { Items: [SAtom head, ..] } || !DeclarationCommands.Contains(head.Text)) {
      throw new ArgumentException("Not a declaration command", nameof(command));
    }
    return RunAsync(token => CommandAsync(parsed.ToString(), token), cancellationToken);
  }

  /// <summary>Asserts one background term at the outermost level and requires its acknowledgement.</summary>
  public Task AssertAsync(string term, CancellationToken cancellationToken = default) {
    var parsed = Parse(term);
    return RunAsync(token => CommandAsync($"(assert {parsed})", token), cancellationToken);
  }

  /// <summary>
  /// Checks one goal in its own scope: push, assert its negation, check-sat, pop. Solver-side failures
  /// return a non-proof outcome and poison the session; they never throw past this call.
  /// </summary>
  public async Task<GoalResult> CheckGoalAsync(string goalId, string goalTerm, CancellationToken cancellationToken = default) {
    if (goalId is not { Length: > 0 and <= MaxGoalIdLength } ||
        !goalId.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or ':')) {
      throw new ArgumentException("Invalid goal identity", nameof(goalId));
    }
    var negated = $"(assert (not {Parse(goalTerm)}))";
    if (poisoned || closed) { throw new SolverException("Solver session is not usable"); }
    GoalResult result;
    try {
      result = await RunAsync(async token => {
        await SyncAsync(token);
        await CommandAsync("(push 1)", token);
        await CommandAsync(negated, token);
        await SyncAsync(token);
        await SendAsync("(check-sat)", token);
        var answer = await ReadAsync(token);
        var (outcome, reason) = answer switch {
          SAtom { Text: "unsat" } => (Outcome.Proved, (string?)null),
          SAtom { Text: "sat" } => (Outcome.Failed, null),
          SAtom { Text: "unknown" } => await ReasonUnknownAsync(token),
          _ => throw Error(answer, "Unexpected check-sat response")
        };
        await SyncAsync(token);
        await CommandAsync("(pop 1)", token);
        await SyncAsync(token);
        return new GoalResult(goalId, outcome, negated, reason);
      }, cancellationToken);
    } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
      result = new GoalResult(goalId, Outcome.Cancelled, negated, "Cancelled");
    } catch (OperationCanceledException) {
      result = new GoalResult(goalId, Outcome.Timeout, negated, "Solver exchange exceeded its deadline");
    } catch (Exception) {
      result = new GoalResult(goalId, Outcome.ToolError, negated, "Solver session failed");
    }
    results.Add(result);
    return result;
  }

  /// <summary>
  /// Ends the session: <c>(exit)</c> must be acknowledged, followed by end of output and exit status zero.
  /// Returns every goal result, in order. Throws, and reports no result, for a poisoned session.
  /// </summary>
  public async Task<IReadOnlyList<GoalResult>> CloseAsync(CancellationToken cancellationToken = default) {
    await RunAsync(async token => {
      await CommandAsync("(exit)", token);
      if (await reader.ReadAsync(token) is { } trailing) { throw Error(trailing, "Trailing solver output"); }
      input.Close();
      await process.WaitForExitAsync(token);
      if (process.ExitCode != 0) { throw new SolverException("Solver exited unsuccessfully"); }
      return 0;
    }, cancellationToken);
    closed = true;
    await DisposeAsync();
    return results.ToArray();
  }

  /// <summary>The retained (bounded) solver standard error, for diagnostics only.</summary>
  public async Task<(string Text, bool Truncated)> StandardErrorAsync() {
    try { return await drain.WaitAsync(TimeSpan.FromSeconds(5)); }
    catch (Exception) { return ("", true); }
  }

  public async ValueTask DisposeAsync() {
    if (disposed) { return; }
    disposed = true;
    if (!closed) { poisoned = true; }
    lifetime.Cancel();
    try {
      if (!process.HasExited) { process.Kill(entireProcessTree: true); }
    } catch (InvalidOperationException) { /* Already exited. */ }
    try {
      using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await process.WaitForExitAsync(reap.Token);
    } catch (OperationCanceledException) { /* Bounded reap. */ }
    try { await drain.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { /* Observed. */ }
    process.Dispose();
    lifetime.Dispose();
  }

  private static SExpr Parse(string text) {
    try { return SmtReader.ParseSingle(text); }
    catch (SmtSyntaxException) { throw new ArgumentException("Expected exactly one well-formed S-expression"); }
  }

  // Runs one exchange under the per-exchange deadline. Any exception poisons and kills the session.
  private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> exchange, CancellationToken cancellationToken) {
    if (poisoned || closed) { throw new SolverException("Solver session is not usable"); }
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
    deadline.CancelAfter(options.CommandDeadline);
    try {
      return await exchange(deadline.Token);
    } catch {
      poisoned = true;
      lifetime.Cancel();
      try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
      catch (InvalidOperationException) { /* Already exited. */ }
      throw;
    }
  }

  private async Task RunAsync(Func<CancellationToken, Task> exchange, CancellationToken cancellationToken) =>
    await RunAsync(async token => { await exchange(token); return 0; }, cancellationToken);

  private async Task SendAsync(string command, CancellationToken token) {
    var bytes = Encoding.UTF8.GetBytes(command + "\n");
    await input.WriteAsync(bytes, token);
    await input.FlushAsync(token);
  }

  private async Task<SExpr> ReadAsync(CancellationToken token) =>
    await reader.ReadAsync(token) ?? throw new SolverException("Solver output ended");

  private async Task CommandAsync(string command, CancellationToken token) {
    await SendAsync(command, token);
    var response = await ReadAsync(token);
    if (!Success.Equals(response)) { throw Error(response, "Command was not acknowledged"); }
  }

  // An echo of a fresh nonce must come back exactly as the next response (Z3 prints it without quotes).
  private async Task SyncAsync(CancellationToken token) {
    var nonce = $"b3_sync_{sessionNonce}_{syncCounter++}";
    await SendAsync($"(echo \"{nonce}\")", token);
    var response = await ReadAsync(token);
    var echoed = response switch { SAtom atom => atom.Text, SString text => text.Text, _ => null };
    if (echoed != nonce) {
      throw Error(response, "Solver transcript is misaligned");
    }
  }

  private async Task<(Outcome, string?)> ReasonUnknownAsync(CancellationToken token) {
    await SendAsync("(get-info :reason-unknown)", token);
    var response = await ReadAsync(token);
    var reason = response is SList { Items: [SAtom { Text: ":reason-unknown" }, var value] }
      ? value switch { SString literal => literal.Text, SAtom atom => atom.Text, SQuoted quoted => quoted.Text, _ => null }
      : null;
    if (reason == null) { throw Error(response, "Malformed reason-unknown response"); }
    var text = reason.ToLowerInvariant();
    var outcome =
      text.Contains("timeout") ? Outcome.Timeout :
      text.Contains("memout") || text.Contains("memory") || text.Contains("resource") || text.Contains("rlimit")
        ? Outcome.ResourceLimit :
      // Z3 reports an exhausted :rlimit as "canceled"; attribute it only to a limit this session configured.
      text.Contains("canceled") && options.ResourceLimit != null ? Outcome.ResourceLimit :
      text.Contains("canceled") && options.TimeoutMilliseconds != null ? Outcome.Timeout :
      Outcome.Unknown;
    return (outcome, reason.Length > 256 ? reason[..256] : reason);
  }

  private static SolverException Error(SExpr response, string message) =>
    new(response is SList { Items: [SAtom { Text: "error" }, ..] } ? "Solver reported an error" : message);

  private static async Task<(string, bool)> DrainAsync(Stream stream, CancellationToken token) {
    var retained = new MemoryStream();
    var buffer = new byte[4096];
    var truncated = false;
    int count;
    while ((count = await stream.ReadAsync(buffer, token)) != 0) {
      var keep = Math.Min(count, MaxStandardErrorBytes - (int)retained.Length);
      retained.Write(buffer, 0, keep);
      truncated |= keep != count;
    }
    return (Encoding.UTF8.GetString(retained.ToArray()), truncated);
  }
}
