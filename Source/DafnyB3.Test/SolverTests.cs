using System.Diagnostics;
using System.Text;
using DafnyB3;
using DafnyB3.Solver;
using Xunit;

public class SmtReaderTests {
  private static async Task<List<SExpr>> ReadAll(string text, int maxBytes = SmtReader.DefaultMaxBytes, int maxDepth = 8) {
    var reader = new SmtReader(new MemoryStream(Encoding.UTF8.GetBytes(text)), maxBytes, maxDepth);
    var all = new List<SExpr>();
    while (await reader.ReadAsync(CancellationToken.None) is { } expr) { all.Add(expr); }
    return all;
  }

  [Fact]
  public async Task LexesStringsQuotedSymbolsAndComments() {
    var all = await ReadAll("; comment ) ( \" |\n(error \"a \"\") ; b\" |x ) ; y| ab:c #x0F)\n  unsat ; tail");
    Assert.Equal(2, all.Count);
    Assert.Equal(new SList([new SAtom("error"), new SString("a \") ; b"), new SQuoted("x ) ; y"),
      new SAtom("ab:c"), new SAtom("#x0F")]), all[0]);
    Assert.Equal(new SAtom("unsat"), all[1]);
    Assert.NotEqual<SExpr>(new SAtom("unsat"), new SQuoted("unsat"));
    Assert.NotEqual<SExpr>(new SAtom("unsat"), new SString("unsat"));
    Assert.Equal("(error \"a \"\") ; b\" |x ) ; y| ab:c #x0F)", all[0].ToString());
  }

  [Fact]
  public async Task EmptyAndLayoutOnlyInputHasNoExpression() {
    Assert.Empty(await ReadAll(""));
    Assert.Empty(await ReadAll(" \n\t; only a comment"));
  }

  [Fact]
  public async Task AcceptsInputAtExactlyTheBounds() {
    Assert.Single(await ReadAll("unsat", maxBytes: 5));
    Assert.Single(await ReadAll("((((((((a))))))))", maxDepth: 8));
  }

  [Theory]
  [InlineData("(unsat")]
  [InlineData("(error \"abc")]
  [InlineData("\"abc")]
  [InlineData("|abc")]
  [InlineData("(a (b)")]
  [InlineData(")")]
  [InlineData("unsat)")]
  [InlineData("a|b|")]
  [InlineData("a\"b\"")]
  [InlineData("\"a\"b")]
  [InlineData("|a|b")]
  [InlineData("|a\\b|")]
  [InlineData("|a\u0001|")]
  [InlineData("\"a\u0001\"")]
  [InlineData("\u0001")]
  [InlineData("; comment \u0001\nunsat")]
  [InlineData("[")]
  [InlineData("unsaté")]
  [InlineData("(((((((((a)))))))))")]
  public async Task RejectsMalformedTruncatedOrUnboundedInput(string text) {
    await Assert.ThrowsAsync<SmtSyntaxException>(() => ReadAll(text));
  }

  [Fact]
  public async Task RejectsOversizedResponsesAndInvalidUtf8() {
    await Assert.ThrowsAsync<SmtSyntaxException>(() => ReadAll("unsat", maxBytes: 4));
    await Assert.ThrowsAsync<SmtSyntaxException>(() => ReadAll(new string(' ', 100) + "a", maxBytes: 100));
    await Assert.ThrowsAsync<SmtSyntaxException>(() => ReadAll("; " + new string('x', 100) + "\na", maxBytes: 50));
    var reader = new SmtReader(new MemoryStream(new byte[] { (byte)'"', 0xff, (byte)'"' }));
    await Assert.ThrowsAsync<SmtSyntaxException>(() => reader.ReadAsync(CancellationToken.None));
    reader = new SmtReader(new MemoryStream(new byte[] { (byte)'|', 0xc3, (byte)'|' }));
    await Assert.ThrowsAsync<SmtSyntaxException>(() => reader.ReadAsync(CancellationToken.None));
  }

  [Fact]
  public async Task TheByteBoundIsPerExpression() {
    Assert.Equal(3, (await ReadAll("abcd abcd abcd", maxBytes: 5)).Count);
  }

  [Theory]
  [InlineData("")]
  [InlineData("a b")]
  [InlineData("true)) (assert false")]
  [InlineData("(x) (y)")]
  [InlineData("; only a comment")]
  public void ParseSingleRejectsAnythingButOneExpression(string text) {
    Assert.Throws<SmtSyntaxException>(() => SmtReader.ParseSingle(text));
  }
}

public class SolverSessionTests {
  private static readonly SolverOptions Options = new(TimeSpan.FromSeconds(10));

  private static SolverCommand Fake(string mode, params string[] rest) => new(
    Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
    new[] { typeof(SolverSessionTests).Assembly.Location, "smt-" + mode }.Concat(rest).ToArray());

  private static async Task<(GoalResult Result, SolverSession Session)> CheckOne(SolverCommand command,
    SolverOptions? options = null, string goal = "(> (+ x 1) x)") {
    var session = await SolverSession.StartAsync(command, options ?? Options);
    await session.DeclareAsync("(declare-const x Int)");
    return (await session.CheckGoalAsync("goal:0", goal), session);
  }

  [Theory]
  [InlineData("unsat")]
  [InlineData("quoted-echo")]
  [InlineData("commented")]
  [InlineData("stderr-flood")]
  public async Task CorrelatedExactUnsatIsAProof(string mode) {
    var (result, session) = await CheckOne(Fake(mode));
    await using (session) {
      Assert.Equal(Outcome.Proved, result.Outcome);
      Assert.Equal("goal:0", result.GoalId);
      Assert.Equal("(assert (not (> (+ x 1) x)))", result.SubmittedAssertion);
      Assert.False(session.Poisoned);
      var all = await session.CloseAsync();
      Assert.Equal([result], all);
      var (stderr, truncated) = await session.StandardErrorAsync();
      Assert.Equal(mode == "stderr-flood", truncated);
      Assert.Equal(mode == "stderr-flood" ? SolverSession.MaxStandardErrorBytes : 0, stderr.Length);
    }
  }

  [Theory]
  [InlineData("sat", Outcome.Failed)]
  [InlineData("unknown-incomplete", Outcome.Unknown)]
  [InlineData("unknown-timeout", Outcome.Timeout)]
  [InlineData("unknown-memout", Outcome.ResourceLimit)]
  [InlineData("unknown-canceled", Outcome.Unknown)]
  public async Task NonProofAnswersKeepTheSessionUsable(string mode, Outcome expected) {
    var (result, session) = await CheckOne(Fake(mode));
    await using (session) {
      Assert.Equal(expected, result.Outcome);
      Assert.False(session.Poisoned);
      Assert.Equal([result], await session.CloseAsync());
    }
  }

  [Theory]
  [InlineData(1000L, null, Outcome.ResourceLimit)]
  [InlineData(null, 1000, Outcome.Timeout)]
  public async Task CanceledIsAttributedOnlyToAConfiguredLimit(long? rlimit, int? timeout, Outcome expected) {
    var (result, session) = await CheckOne(Fake("unknown-canceled"), Options with { ResourceLimit = rlimit, TimeoutMilliseconds = timeout });
    await using (session) { Assert.Equal(expected, result.Outcome); }
  }

  [Theory]
  [InlineData("unsat-string", Outcome.ToolError)]
  [InlineData("unsat-quoted", Outcome.ToolError)]
  [InlineData("unsat-upper", Outcome.ToolError)]
  [InlineData("unsat-list", Outcome.ToolError)]
  [InlineData("unsat-paren", Outcome.ToolError)]
  [InlineData("unsat-adjacent", Outcome.ToolError)]
  [InlineData("control", Outcome.ToolError)]
  [InlineData("success-check", Outcome.ToolError)]
  [InlineData("error-check", Outcome.ToolError)]
  [InlineData("duplicate-unsat", Outcome.ToolError)]
  [InlineData("error-after-unsat", Outcome.ToolError)]
  [InlineData("reorder", Outcome.ToolError)]
  [InlineData("eof-check", Outcome.ToolError)]
  [InlineData("truncated", Outcome.ToolError)]
  [InlineData("truncated-atom", Outcome.ToolError)]
  [InlineData("oversized", Outcome.ToolError)]
  [InlineData("deep", Outcome.ToolError)]
  [InlineData("invalid-utf8", Outcome.ToolError)]
  [InlineData("error-goal", Outcome.ToolError)]
  [InlineData("omit-goal-ack", Outcome.Timeout)]
  [InlineData("error-push", Outcome.ToolError)]
  [InlineData("omit-push-ack", Outcome.Timeout)]
  [InlineData("stale-push", Outcome.ToolError)]
  [InlineData("error-pop", Outcome.ToolError)]
  [InlineData("omit-pop-ack", Outcome.Timeout)]
  [InlineData("malformed-reason", Outcome.ToolError)]
  [InlineData("stale-echo", Outcome.ToolError)]
  [InlineData("unknown-error", Outcome.ToolError)]
  public async Task MalformedOrUncorrelatedTranscriptIsNeverAProof(string mode, Outcome expected) {
    // A missing acknowledgement stalls the lock-step exchange, so it ends at the deadline.
    var (result, session) = await CheckOne(Fake(mode), Options with { CommandDeadline = TimeSpan.FromSeconds(2) });
    await using (session) {
      Assert.Equal(expected, result.Outcome);
      Assert.True(session.Poisoned);
      await Assert.ThrowsAsync<SolverException>(() => session.CheckGoalAsync("goal:1", "true"));
      await Assert.ThrowsAsync<SolverException>(() => session.DeclareAsync("(declare-const y Int)"));
      await Assert.ThrowsAsync<SolverException>(() => session.CloseAsync());
    }
  }

  [Fact]
  public async Task DuplicatedAcknowledgementIsCaughtAtTheNextSync() {
    var (result, session) = await CheckOne(Fake("duplicate-success"));
    await using (session) {
      Assert.Equal(Outcome.ToolError, result.Outcome);
      Assert.True(session.Poisoned);
    }
  }

  [Theory]
  [InlineData("stale-startup")]
  [InlineData("silent-startup")]
  [InlineData("error-print-success")]
  [InlineData("error-rlimit")]
  [InlineData("wrong-echo")]
  public async Task StartupMustBeAcknowledgedAndAligned(string mode) {
    await Assert.ThrowsAnyAsync<Exception>(() => SolverSession.StartAsync(Fake(mode),
      Options with { CommandDeadline = TimeSpan.FromSeconds(2), ResourceLimit = 1000 }));
  }

  [Theory]
  [InlineData("error-decl")]
  [InlineData("unsupported-decl")]
  public async Task FailedDeclarationPoisonsTheSession(string mode) {
    await using var session = await SolverSession.StartAsync(Fake(mode), Options);
    await Assert.ThrowsAnyAsync<IOException>(() => session.DeclareAsync("(declare-const x Int)"));
    Assert.True(session.Poisoned);
    await Assert.ThrowsAsync<SolverException>(() => session.CheckGoalAsync("goal:0", "true"));
  }

  [Fact]
  public async Task FailedBackgroundAssertionPoisonsTheSession() {
    await using var session = await SolverSession.StartAsync(Fake("error-assert"), Options);
    await Assert.ThrowsAnyAsync<IOException>(() => session.AssertAsync("true"));
    Assert.True(session.Poisoned);
    await Assert.ThrowsAsync<SolverException>(() => session.CheckGoalAsync("goal:0", "true"));
  }

  [Theory]
  [InlineData("exit-nonzero")]
  [InlineData("trailing-exit")]
  public async Task UncleanCloseReportsNoResults(string mode) {
    var (result, session) = await CheckOne(Fake(mode));
    await using (session) {
      Assert.Equal(Outcome.Proved, result.Outcome);
      await Assert.ThrowsAnyAsync<IOException>(() => session.CloseAsync());
      Assert.True(session.Poisoned);
    }
  }

  [Fact]
  public async Task ATranscriptIsExactlyTheIntendedLockStepExchange() {
    var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    try {
      await using (var session = await SolverSession.StartAsync(Fake("record", file), Options with { ResourceLimit = 7 })) {
        await session.DeclareAsync("(declare-fun f ( Int )  Int)");
        await session.AssertAsync("(> (f 0) 0) ; comment");
        Assert.Equal(Outcome.Proved, (await session.CheckGoalAsync("g", "(=  |a b|\n \"s\"\"t\") ; note")).Outcome);
        await session.CloseAsync();
      }
      var lines = (await File.ReadAllLinesAsync(file))
        .Select(line => System.Text.RegularExpressions.Regex.Replace(line, "b3_sync_[0-9a-f]{32}_", "N")).ToArray();
      Assert.Equal([
        "(set-option :print-success true)", "(set-option :rlimit 7)", "(echo \"N0\")",
        "(declare-fun f (Int) Int)", "(assert (> (f 0) 0))",
        "(echo \"N1\")", "(push 1)", "(assert (not (= |a b| \"s\"\"t\")))", "(echo \"N2\")", "(check-sat)",
        "(echo \"N3\")", "(pop 1)", "(echo \"N4\")", "(exit)"
      ], lines);
    } finally {
      File.Delete(file);
    }
  }

  [Theory]
  [InlineData("")]
  [InlineData("true)) (assert false")]
  [InlineData("true) (check-sat")]
  [InlineData("a b")]
  [InlineData("(and true")]
  public async Task GoalAndCommandInjectionIsRejectedBeforeSubmission(string text) {
    await using var session = await SolverSession.StartAsync(Fake("unsat"), Options);
    await Assert.ThrowsAsync<ArgumentException>(() => session.CheckGoalAsync("goal:0", text));
    await Assert.ThrowsAsync<ArgumentException>(() => session.AssertAsync(text));
    await Assert.ThrowsAsync<ArgumentException>(() => session.DeclareAsync(text));
    Assert.False(session.Poisoned);
    Assert.Equal(Outcome.Proved, (await session.CheckGoalAsync("goal:0", "true")).Outcome);
  }

  [Theory]
  [InlineData("(check-sat)")]
  [InlineData("(push 1)")]
  [InlineData("(pop 1)")]
  [InlineData("(assert false)")]
  [InlineData("(echo \"x\")")]
  [InlineData("(set-option :print-success false)")]
  [InlineData("(reset)")]
  [InlineData("(exit)")]
  [InlineData("(|declare-fun| f () Int)")]
  [InlineData("declare-fun")]
  public async Task OnlyDeclarationsAreAcceptedAsDeclarations(string text) {
    await using var session = await SolverSession.StartAsync(Fake("unsat"), Options);
    await Assert.ThrowsAsync<ArgumentException>(() => session.DeclareAsync(text));
    Assert.False(session.Poisoned);
  }

  [Theory]
  [InlineData("")]
  [InlineData("a b")]
  [InlineData("a)")]
  public async Task GoalIdentityIsChecked(string id) {
    await using var session = await SolverSession.StartAsync(Fake("unsat"), Options);
    await Assert.ThrowsAsync<ArgumentException>(() => session.CheckGoalAsync(id, "true"));
  }

  [Fact]
  public async Task UnresponsiveSolverTimesOutAndIsPoisoned() {
    var (result, session) = await CheckOne(Fake("hang-check"), Options with { CommandDeadline = TimeSpan.FromMilliseconds(500) });
    await using (session) {
      Assert.Equal(Outcome.Timeout, result.Outcome);
      Assert.True(session.Poisoned);
    }
  }

  [Fact]
  public async Task CallerCancellationIsCancelledNotProved() {
    await using var session = await SolverSession.StartAsync(Fake("hang-check"), Options);
    using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
    var result = await session.CheckGoalAsync("goal:0", "true", cancel.Token);
    Assert.Equal(Outcome.Cancelled, result.Outcome);
    Assert.True(session.Poisoned);
  }

  [Fact]
  public async Task StartupFailureIsAnError() {
    await Assert.ThrowsAnyAsync<Exception>(() => SolverSession.StartAsync(
      new SolverCommand(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), []), Options));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task OptionsAreChecked(int value) {
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => SolverSession.StartAsync(Fake("unsat"),
      Options with { ResourceLimit = value }));
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => SolverSession.StartAsync(Fake("unsat"),
      Options with { TimeoutMilliseconds = value }));
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => SolverSession.StartAsync(Fake("unsat"),
      Options with { CommandDeadline = TimeSpan.FromSeconds(value) }));
  }

  private static async Task<int[]> Pids(string file) {
    using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (!File.Exists(file)) { await Task.Delay(10, wait.Token); }
    var pids = (await File.ReadAllTextAsync(file)).Split(',').Select(int.Parse).ToArray();
    Assert.Equal(2, pids.Length);
    return pids;
  }

  private static async Task AssertReaped(int[] pids) {
    foreach (var pid in pids) {
      using var reaped = new CancellationTokenSource(TimeSpan.FromSeconds(3));
      while (IsRunning(pid) && !reaped.IsCancellationRequested) { await Task.Delay(10); }
      Assert.False(IsRunning(pid), $"Solver process {pid} survived");
    }
  }

  private static async Task CleanUp(string file) {
    if (File.Exists(file)) {
      foreach (var pid in (await File.ReadAllTextAsync(file)).Split(',').Select(int.Parse)) {
        try { using var p = Process.GetProcessById(pid); p.Kill(entireProcessTree: true); } catch (Exception) { }
      }
    }
    File.Delete(file);
  }

  [Fact]
  public async Task FailuresReapTheSolverTreeBeforeDisposalRepeatedly() {
    for (var attempt = 0; attempt < 3; attempt++) {
      var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      try {
        var (result, session) = await CheckOne(Fake("tree", file), Options with { CommandDeadline = TimeSpan.FromMilliseconds(700) });
        await using (session) {
          Assert.Equal(Outcome.Timeout, result.Outcome);
          // A poisoned session has already killed its solver, before its owner disposes it.
          await AssertReaped(await Pids(file));
        }
      } finally {
        await CleanUp(file);
      }
    }
  }

  [Fact]
  public async Task DisposalWithoutCloseReapsTheSolverTree() {
    var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    try {
      int[] pids;
      await using (var session = await SolverSession.StartAsync(Fake("tree", file), Options)) {
        await session.DeclareAsync("(declare-const x Int)");
        pids = await Pids(file);
        Assert.False(session.Poisoned);
      }
      await AssertReaped(pids);
    } finally {
      await CleanUp(file);
    }
  }

  private static bool IsRunning(int pid) {
    try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
    catch (ArgumentException) { return false; }
  }
}

/// <summary>Positive and negative controls against the pinned Z3 5.1.0 release. A missing solver fails.</summary>
public class RealZ3Tests {
  private static readonly Lazy<string> Z3 = new(() => {
    var candidates = new List<string>();
    if (Environment.GetEnvironmentVariable("Z3") is { Length: > 0 } fromEnvironment) { candidates.Add(fromEnvironment); }
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent) {
      candidates.Add(Path.Combine(dir.FullName, "Binaries", "z3", "bin", "z3-5.1.0"));
    }
    foreach (var candidate in candidates.Where(File.Exists)) {
      using var probe = Process.Start(new ProcessStartInfo(candidate, "--version") { RedirectStandardOutput = true })!;
      var version = probe.StandardOutput.ReadToEnd();
      probe.WaitForExit();
      Assert.StartsWith("Z3 version 5.1.0 ", version);
      return candidate;
    }
    throw new InvalidOperationException("Set Z3 to a Z3 5.1.0 executable (or install Binaries/z3/bin/z3-5.1.0)");
  });

  private static Task<SolverSession> Start(SolverOptions? options = null) =>
    SolverSession.StartAsync(new SolverCommand(Z3.Value, ["-in", "-smt2"]), options ?? new SolverOptions(TimeSpan.FromSeconds(60)));

  [Fact]
  public async Task ValidGoalsAreProvedAndInvalidGoalsAreNot() {
    await using var session = await Start();
    await session.DeclareAsync("(declare-const x Int)");
    await session.DeclareAsync("(declare-fun |f x| (Int) Int)");
    Assert.Equal(Outcome.Proved, (await session.CheckGoalAsync("valid", "(> (+ x 1) x)")).Outcome);
    Assert.Equal(Outcome.Failed, (await session.CheckGoalAsync("invalid", "(> x 0)")).Outcome);
    // The previous goal's negation was popped: if it leaked, this goal would be unsat (proved).
    Assert.Equal(Outcome.Failed, (await session.CheckGoalAsync("scoped", "(<= x 0)")).Outcome);
    Assert.Equal(Outcome.Failed, (await session.CheckGoalAsync("false", "false")).Outcome);
    Assert.Equal(Outcome.Failed, (await session.CheckGoalAsync("unassumed", "(> (|f x| 3) 3)")).Outcome);
    await session.AssertAsync("(forall ((y Int)) (! (> (|f x| y) y) :pattern ((|f x| y))))");
    Assert.Equal(Outcome.Proved, (await session.CheckGoalAsync("background", "(> (|f x| 3) 3)")).Outcome);
    var all = await session.CloseAsync();
    Assert.Equal(["valid", "invalid", "scoped", "false", "unassumed", "background"], all.Select(r => r.GoalId));
  }

  [Theory]
  [InlineData("(+ x true)")]
  [InlineData("(> undeclared 0)")]
  public async Task IllTypedGoalIsAToolErrorAndPoisons(string goal) {
    await using var session = await Start();
    await session.DeclareAsync("(declare-const x Int)");
    var result = await session.CheckGoalAsync("bad", goal);
    Assert.Equal(Outcome.ToolError, result.Outcome);
    Assert.True(session.Poisoned);
    await Assert.ThrowsAsync<SolverException>(() => session.CloseAsync());
  }

  [Fact]
  public async Task IllTypedDeclarationPoisons() {
    await using var session = await Start();
    await Assert.ThrowsAnyAsync<IOException>(() => session.DeclareAsync("(declare-fun f (NoSuchSort) Int)"));
    Assert.True(session.Poisoned);
  }

  private const string Loop = "(forall ((y Int)) (! (> (f y) (f (g y))) :pattern ((f y))))";

  private static async Task<GoalResult> CheckLoop(SolverOptions options) {
    await using var session = await Start(options);
    await session.DeclareAsync("(declare-fun f (Int) Int)");
    await session.DeclareAsync("(declare-fun g (Int) Int)");
    await session.AssertAsync(Loop);
    await session.AssertAsync("(forall ((y Int)) (! (= (g y) (+ y 1)) :pattern ((g y))))");
    var result = await session.CheckGoalAsync("loop", "(<= (f 0) 0)");
    await session.CloseAsync();
    return result;
  }

  [Fact]
  public async Task ResourceLimitIsNotAProof() {
    var result = await CheckLoop(new SolverOptions(TimeSpan.FromSeconds(60), ResourceLimit: 200000));
    Assert.Equal(Outcome.ResourceLimit, result.Outcome);
  }

  [Fact]
  public async Task SolverTimeoutIsNotAProof() {
    var result = await CheckLoop(new SolverOptions(TimeSpan.FromSeconds(60), TimeoutMilliseconds: 300));
    Assert.Equal(Outcome.Timeout, result.Outcome);
  }
}
