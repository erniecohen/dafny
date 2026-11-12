#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Boogie.SMTLib;

namespace Microsoft.Dafny.Caching;

internal sealed class CacheReplayMismatchException : Exception {
  public CacheReplayMismatchException() : base("Cached solver exchange did not match the current task.") { }
}

/// <summary>
/// A single-task transport. Its owner must provide a complete execution environment, create a
/// fresh instance per split/iteration, and restart the entire task on CacheReplayMismatchException.
/// No result is published until that owner acknowledges normal completion and complete reports.
/// </summary>
internal sealed class CachedTaskSolver : SMTLibSolver {
  private enum Kind { Send, Request, Ping, Problem }
  private enum Mode { Pending, Live, Replay, Closed }
  private sealed record Expression(string Name, Expression[] Arguments) {
    public static Expression Capture(SExpr value, ref long characters, ref int nodes, int depth = 0) {
      if (depth > 128 || value == null || value.Name == null || ++nodes > 100000 ||
          (characters += value.Name?.Length ?? 0) > 32 * 1024 * 1024) {
        throw new JsonException("Unsupported solver response size or depth");
      }
      var children = new List<Expression>();
      foreach (var child in value.Arguments) { children.Add(Capture(child, ref characters, ref nodes, depth + 1)); }
      return new Expression(value.Name, children.ToArray());
    }
    public SExpr Restore() => new(Name, Arguments.Select(child => child.Restore()));
    public bool IsValid(int depth = 0) => depth <= 128 && Name != null && Arguments != null &&
      Arguments.All(child => child != null && child.IsValid(depth + 1));
  }
  private sealed record Step(Kind Kind, string Command, Expression? Response = null);
  private sealed record Exchange(int Schema, Step[] Steps);
  private sealed record Replay(CacheTaskOutcome Outcome, long ResourceCount, Step[] Steps);
  private static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 512 };

  private readonly TaskResultCache cache;
  private readonly Func<byte[]?> environment;
  private readonly Func<SMTLibSolver> createSolver;
  private readonly bool warningsHaveNoEffects;
  private readonly List<Step> steps = new();
  private Mode mode;
  private bool cacheable = true;
  private bool completed;
  private bool terminalComment;
  private bool sealedExchange;
  private int parserErrorSubscriptions;
  private int replayPosition;
  private long retainedCharacters;
  private int retainedResponseNodes;
  private Task? pendingHandshake;
  private SMTLibSolver? solver;
  private TaskCacheDescriptor? descriptor;
  private Replay? replay;

  public CachedTaskSolver(TaskResultCache cache, Func<byte[]?> completeEnvironment,
    Func<SMTLibSolver> createFreshSolver, bool warningsHaveNoEffects = false) {
    this.cache = cache;
    environment = completeEnvironment;
    createSolver = createFreshSolver;
    this.warningsHaveNoEffects = warningsHaveNoEffects;
  }

  public string? ExecutionKey => descriptor?.Key;
  public bool Replayed => replay != null;
  public bool ReplayMismatch { get; private set; }
  public override event Action<string>? ErrorHandler;

  public override void Send(string cmd) {
    if (completed || sealedExchange) { return; }
    RequireOpen();
    var step = new Step(Kind.Send, cmd);
    if (mode == Mode.Replay) {
      Expect(step);
      terminalComment |= cmd is "; Valid" or "; Invalid" or "; Timed out" or "; Out of resource" or "; Out of memory" or "; Undetermined";
      return;
    }
    solver?.Send(cmd);
    Record(step);
    terminalComment |= cmd is "; Valid" or "; Invalid" or "; Timed out" or "; Out of resource" or "; Out of memory" or "; Undetermined";
  }

  public override void NewProblem(string descriptiveName) {
    if (completed || sealedExchange) { return; }
    RequireOpen();
    var step = new Step(Kind.Problem, descriptiveName);
    if (mode == Mode.Replay) {
      Expect(step);
      return;
    }
    solver?.NewProblem(descriptiveName);
    Record(step);
  }

  public override Task PingPong() {
    return pendingHandshake = PingPongCore();
  }

  private async Task PingPongCore() {
    if (completed || sealedExchange) { return; }
    RequireOpen();
    var step = new Step(Kind.Ping, "");
    if (mode == Mode.Replay) {
      try { Expect(step); }
      catch (CacheReplayMismatchException error) {
        // Pinned Checker.GoBackToIdle retires a failed checker only on TimeoutException.
        // Preserve the mismatch flag for our task retry, while releasing its pool slot.
        throw new TimeoutException("Cached transport cannot return to the checker pool.", error);
      }
      if (terminalComment) { sealedExchange = true; }
      return;
    }
    if (solver != null) {
      try {
        await solver.PingPong();
      } catch {
        cacheable = false;
        throw;
      }
    }
    Record(step);
    if (terminalComment) { sealedExchange = true; }
  }

  internal async Task AwaitPendingHandshake() {
    // Checker.GoBackToIdle retires its checker after 100 ms without awaiting the
    // underlying handshake's continuation. Its verification observable can thus
    // complete before that continuation seals this exchange. Native Close wakes
    // the pending transport; a failed handshake still prevents publication.
    if (pendingHandshake != null) {
      try { await pendingHandshake; }
      catch { cacheable = false; }
    }
  }

  public override async Task<SExpr> SendRequest(string request, CancellationToken cancellationToken = default) {
    RequireOpen();
    cancellationToken.ThrowIfCancellationRequested();
    try {
      if (mode == Mode.Pending) {
        // Earlier response-dependent queries are unsupported, so they go straight to the solver.
        if (request == "(check-sat)" && cacheable) {
          var input = JsonSerializer.SerializeToUtf8Bytes(steps.Append(new Step(Kind.Request, request)), JsonOptions);
          var frozen = environment();
          if (frozen != null) {
            var limits = SmtExecutionLimits.Read(steps.Where(s => s.Kind == Kind.Send).Select(s => s.Command));
            if (limits.Supported) {
              descriptor = new TaskCacheDescriptor(input, frozen, limits.Resource, limits.Milliseconds);
              replay = cache.TryRead(descriptor, new ExchangeLoader());
            } else { cacheable = false; }
          } else { cacheable = false; }
          if (replay != null) {
            mode = Mode.Replay;
            replayPosition = steps.Count;
          }
        } else {
          cacheable = false;
        }
        if (mode != Mode.Replay) {
          await StartSolver();
        }
      }
      if (mode == Mode.Replay) {
        return Expect(new Step(Kind.Request, request)).Response!.Restore();
      }
      var response = await solver!.SendRequest(request, cancellationToken);
      if (request == "(get-info :reason-unknown)" &&
          !(response is { Name: ":reason-unknown", ArgCount: 1 } && response[0].Name is
            "incomplete" or "(incomplete quantifiers)" or "(incomplete (theory arithmetic))" or
            "smt tactic failed to show goal to be sat/unsat (incomplete (theory arithmetic))" or
            "canceled" or "max. resource limit exceeded" or "resource limits reached" or "(resource limits reached)")) {
        // Invalid can remain the aggregate outcome after an earlier counterexample even
        // when a subsequent query times out or exhausts memory. Do not cache that report.
        cacheable = false;
      }
      if (cacheable) {
        try {
          Record(new Step(Kind.Request, request, Expression.Capture(response, ref retainedCharacters, ref retainedResponseNodes)));
        } catch (JsonException) {
          cacheable = false;
        }
      }
      return response;
    } catch {
      cacheable = false;
      throw;
    }
  }

  private async Task StartSolver() {
    solver = createSolver();
    solver.ErrorHandler += OnError;
    for (var index = 0; index < parserErrorSubscriptions; index++) {
      solver.AddErrorHandler(RejectObservableError);
    }
    mode = Mode.Live;
    foreach (var step in steps) {
      switch (step.Kind) {
        case Kind.Send: solver.Send(step.Command); break;
        case Kind.Ping: await solver.PingPong(); break;
        case Kind.Problem: solver.NewProblem(step.Command); break;
        default: throw new InvalidOperationException("A pending setup cannot contain responses.");
      }
    }
  }

  private void OnError(string message) {
    RejectObservableError(message);
    ErrorHandler?.Invoke(message);
  }

  private void RejectObservableError(string message) {
    // Pinned HandleProverError treats each line containing "WARNING: " as a
    // warning. VerificationResultCollector.OnWarning is a no-op under None.
    // Such ignored events affect neither the adaptive solver program nor reports.
    // Preserve actual callbacks on the cold path; every other event rejects reuse.
    if (!warningsHaveNoEffects || !message.Replace("\r", "").Split('\n')
          .All(line => line.Contains("WARNING: ", StringComparison.OrdinalIgnoreCase))) {
      cacheable = false;
    }
  }

  public override void AddErrorHandler(Action<string> handler) {
    ErrorHandler += handler;
    parserErrorSubscriptions++;
    // Preserve the underlying parser's explicit error-handler registration behavior.
    solver?.AddErrorHandler(RejectObservableError);
  }

  /// <summary>Call only after all counterexample/coverage extraction and idle handshakes finish.</summary>
  public CachePublication CompleteNormally(CacheTaskOutcome outcome, long resourceCount) {
    if (completed) { return CachePublication.Unavailable; }
    completed = true;
    if (replay != null) {
      if (!sealedExchange || replayPosition != replay.Steps.Length || replay.Outcome != outcome || replay.ResourceCount != resourceCount) {
        cacheable = false;
        throw Mismatch();
      }
      return CachePublication.AlreadyPresent;
    }
    // Boogie may retire a checker after its successful final handshake, before the
    // task's completion observer runs. Closing that sealed transport loses no evidence.
    if (!cacheable || !sealedExchange || descriptor == null || mode is not (Mode.Live or Mode.Closed)) {
      return CachePublication.Unavailable;
    }
    try {
      var exchange = JsonSerializer.SerializeToUtf8Bytes(new Exchange(1, steps.ToArray()), JsonOptions);
      return cache.TryPublish(descriptor, true, outcome, resourceCount, exchange);
    } catch (JsonException) {
      return CachePublication.Unavailable;
    }
  }

  public void Abort() {
    cacheable = false;
    if (replay != null && ReplayMismatch && descriptor != null) { cache.Reject(descriptor); }
  }

  public override async Task<IReadOnlyList<SExpr>> SendRequestsAndCloseInput(IReadOnlyList<string> requests,
    CancellationToken cancellationToken = default) {
    RequireOpen();
    // The batch transport has a different completion protocol and never reuses this table.
    cacheable = false;
    if (mode == Mode.Replay) {
      throw Mismatch();
    }
    if (mode == Mode.Pending) {
      await StartSolver();
    }
    return await solver!.SendRequestsAndCloseInput(requests, cancellationToken);
  }

  private CacheReplayMismatchException Mismatch() {
    ReplayMismatch = true;
    cacheable = false;
    return new CacheReplayMismatchException();
  }

  private Step Expect(Step expected) {
    if (!cacheable || replayPosition >= replay!.Steps.Length) {
      throw Mismatch();
    }
    var actual = replay!.Steps[replayPosition++];
    if (actual.Kind != expected.Kind || actual.Command != expected.Command) {
      cacheable = false;
      throw Mismatch();
    }
    return actual;
  }

  private void Record(Step step) {
    // Retention limits affect only reuse, never the actual solver exchange.
    retainedCharacters += step.Command?.Length ?? 0;
    if (steps.Count >= 100000 || retainedCharacters > 32 * 1024 * 1024) {
      cacheable = false;
    }
    // Pending setup must still be replayed to a real solver, even when it is too large to cache.
    if (cacheable || mode == Mode.Pending) {
      steps.Add(step);
    }
    if (!cacheable) {
      if (mode == Mode.Pending) {
        // Setup is larger than the retention budget. Flush it on a context-free worker
        // before discarding it; subsequent commands go directly to the real solver.
        #pragma warning disable VSTHRD002 // SMTLibSolver.Send is synchronous; StartSolver runs without a synchronization context.
        Task.Run(StartSolver).GetAwaiter().GetResult();
        #pragma warning restore VSTHRD002
      }
      steps.Clear();
    }
  }

  private void RequireOpen() {
    if (mode == Mode.Closed) {
      throw new ObjectDisposedException(nameof(CachedTaskSolver));
    }
  }

  public override void Close() {
    if (mode == Mode.Closed) {
      return;
    }
    mode = Mode.Closed;
    solver?.Close();
  }

  private sealed class ExchangeLoader : ITaskResultSnapshotLoader<Replay> {
    public bool TryLoad(TaskCacheDescriptor descriptor, CacheTaskOutcome outcome, long resourceCount,
      byte[] reportData, out Replay result) {
      result = null!;
      Exchange? exchange;
      try {
        exchange = JsonSerializer.Deserialize<Exchange>(reportData, JsonOptions);
      } catch (JsonException) {
        return false;
      }
      if (exchange == null || exchange.Schema != 1 || exchange.Steps is not { Length: > 0 and <= 100000 } ||
          exchange.Steps.Any(step => step == null || step.Command == null || !Enum.IsDefined(step.Kind) ||
            (step.Kind == Kind.Request ? step.Response == null || !step.Response.IsValid() : step.Response != null))) {
        return false;
      }
      var firstRequest = Array.FindIndex(exchange.Steps, step => step.Kind == Kind.Request);
      if (firstRequest < 0 || exchange.Steps[firstRequest].Command != "(check-sat)") {
        return false;
      }
      var prefix = exchange.Steps.Take(firstRequest).Append(new Step(Kind.Request, "(check-sat)"));
      if (!descriptor.Input.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(prefix, JsonOptions))) {
        return false;
      }
      result = new Replay(outcome, resourceCount, exchange.Steps);
      return true;
    }
  }
}
