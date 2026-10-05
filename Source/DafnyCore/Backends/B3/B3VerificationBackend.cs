#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DafnyB3Protocol;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

/// <summary>Consumes typed pre-VC IR without constructing a Boogie execution engine.</summary>
public sealed class B3VerificationBackend : IVerificationBackend {
  private readonly DafnyOptions options;
  private readonly SemaphoreSlim workers;
  private readonly CancellationTokenSource lifetime = new();
  public string Name => "b3";
  public VerificationCapabilities Capabilities => VerificationCapabilities.None;

  public B3VerificationBackend(DafnyOptions options) {
    this.options = options;
    workers = new SemaphoreSlim(Math.Max(1, options.VcsCores));
  }

  public async Task<IReadOnlyList<IVerificationWorkItem>> PrepareAsync(VerificationPreparation input,
    CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();
    var program = input.IntermediateProgram;
    var implementations = program.Implementations.Where(implementation =>
      ((Bpl.ExecutionEngineOptions)options).UserWantsToCheckRoutine(implementation.VerboseName)).OrderBy(implementation => implementation.Name, StringComparer.Ordinal).ToList();
    var configurationError = input.TranslationHasErrors ? "B3 cannot verify an incomplete or erroneous shared translation" : CheckConfiguration(options);
    var configurationOutcome = input.TranslationHasErrors ? VerificationOutcome.ToolError : VerificationOutcome.Unsupported;
    WorkerPackage? package = null;
    if (configurationError == null) {
      try {
        var path = options.Get(B3OptionBag.Worker)?.FullName ?? Path.Combine(AppContext.BaseDirectory, "b3", "DafnyB3Host.dll");
        package = await WorkerPackage.LoadAsync(path, cancellationToken);
      } catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or TimeoutException) {
        configurationOutcome = VerificationOutcome.ToolError;
        configurationError = "B3 worker package is unavailable or invalid: " + exception.Message;
      }
    }
    var sink = new ErrorSink();
    if (configurationError == null && (program.Resolve(options, sink) != 0 || program.Typecheck(options, sink) != 0)) {
      configurationOutcome = VerificationOutcome.ToolError;
      configurationError = "B3 pre-VC resolution/typechecking failed: " + string.Join("; ", sink.Messages);
    }
    if (configurationError == null) {
      // IsSkipVerification reads implementation.Proc, which is linked by pre-VC resolution.
      implementations.RemoveAll(implementation => implementation.IsSkipVerification(options));
    }
    string? solverDigest = null;
    if (configurationError == null) {
      try { solverDigest = await SolverDigestAsync(SolverPath(), checked((int)options.TimeLimit * 1000), cancellationToken); }
      catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or TimeoutException) {
        configurationOutcome = VerificationOutcome.ToolError;
        configurationError = "B3 solver identity could not be captured: " + exception.Message;
      }
    }
    if (configurationError != null && implementations.Count == 0) {
      throw new InvalidDataException(configurationError);
    }
    var tasks = new List<IVerificationWorkItem>();
    foreach (var implementation in implementations) {
      cancellationToken.ThrowIfCancellationRequested();
      if (implementation.tok is not CanVerifyOrigin origin) {
        throw new InvalidDataException("B3 implementation has no Dafny verification owner: " + implementation.Name);
      }
      var kind = implementation.Name.StartsWith("CheckWellformed", StringComparison.OrdinalIgnoreCase)
        ? VerificationUnitKind.Wellformedness : implementation.Name.StartsWith("OverrideCheck", StringComparison.Ordinal)
          ? VerificationUnitKind.Override : VerificationUnitKind.Body;
      var source = new VerificationSourceInfo(origin.CanVerify, origin, origin, kind,
        implementation.VerboseName, "entire B3 verification unit", Array.Empty<Function>());
      if (configurationError != null) {
        tasks.Add(Blocked(source, implementation.Name, configurationOutcome, configurationError));
        continue;
      }
      var normalized = B3Normalizer.Normalize(program, implementation, options);
      if (!normalized.Success || normalized.Program == null) {
        var diagnostics = normalized.Diagnostics.Select(diagnostic => Diagnostic(
          BoogieGenerator.ToDafnyToken(diagnostic.Token), diagnostic.Code + ": " + diagnostic.Message)).ToArray();
        tasks.Add(new B3WorkItem(new VerificationIdentity(implementation.Name, "b3:" + implementation.Name, 0, 0), source,
          _ => Task.FromResult(new VerificationResult(VerificationOutcome.Unsupported, diagnostics,
            Array.Empty<VerificationAssertion>(), false, DateTime.UtcNow, TimeSpan.Zero, null, Math.Max(1, diagnostics.Length)))));
        continue;
      }
      var config = new Configuration(SolverPath(), new[] { "-in", "-smt2" }, checked((int)options.TimeLimit * 1000),
        options.ResourceLimit, 1024 * 1024, options.GetOrOptionDefault(BoogieOptionBag.ArithmeticSolver),
        "5.1.0", solverDigest!);
      var contexts = normalized.Contexts ?? new[] { new B3VerificationContext("legacy", normalized.Program,
        normalized.Obligations, Array.Empty<B3DefinitionOrigin>()) };
      B3DefinitionContexts.ValidatePartition(normalized.Program, normalized.Obligations, contexts, implementation.tok);
      B3RealPreparedRequests prepared;
      B3OpaqueGroundPreparedRequests projected;
      try {
        // Internal headers only: capture bounds must precede hashing/ProtocolValidation on live trees.
        var originals = contexts.Select(context => new Request(Protocol.Version, Guid.NewGuid().ToString("N"), Protocol.NormalizerVersion,
          package!.Manifest.B3Commit, string.Empty, context.Program.Unit.Name,
          context.Program, config, context.Obligations, package.Fingerprint)).ToArray();
        prepared = B3RealContextPreparation.Prepare(contexts, originals, implementation.tok);
        projected = B3OpaqueGroundProjection.Prepare(prepared, implementation.tok);
      } catch (B3RealPreparationRejection rejection) {
        tasks.Add(Blocked(source, implementation.Name, VerificationOutcome.Unsupported,
          "b3_real_preparation: " + rejection.Message));
        continue;
      }
      var requests = projected.Requests;
      // Bind original source snapshots, the checked relation version and final submitted bytes separately.
      // Configuration digests come only from the owned forwarded requests, in the same mask order.
      var preparationKey = B3RealContextPreparation.ProducerVersion + ":" + B3OpaqueGroundProjection.ProducerVersion + ":" +
        string.Join(":", prepared.Evidence.Select((evidence, index) => evidence.MaskId + ":" + evidence.OriginalProgramHash + ":" +
          evidence.FinalProgramHash + ":" + projected.Evidence[index].InputProgramHash + ":" + projected.Evidence[index].FinalProgramHash + ":" +
          B3RealContextPreparation.ConfigurationHash(requests[index].Configuration)));
      // Work keys also bind worker/library bytes and forwarded limits/options.
      var key = "b3:" + preparationKey + ":" + string.Join(":", requests.Select(request => request.ProgramHash)) + ":" + package!.Fingerprint;
      var origins = normalized.Obligations.ToDictionary(obligation => obligation.Id, obligation => SourceOriginFor(obligation, source.Origin));
      tasks.Add(new B3WorkItem(new VerificationIdentity(implementation.Name, key, 0, 0), source,
        token => RunAsync(requests, package, normalized.Obligations, origins, source, token)));
    }
    return tasks;
  }

  private async Task<VerificationResult> RunAsync(IReadOnlyList<Request> requests, WorkerPackage package,
    IReadOnlyList<SourceIdentity> obligations, IReadOnlyDictionary<string, IOrigin> origins, VerificationSourceInfo source, CancellationToken token) {
    var started = DateTime.UtcNow;
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
    B3ContextCompletion completion;
    completion = await B3ContextCoordinator.RunAsync(requests, async (request, contextToken) => {
      await workers.WaitAsync(contextToken);
      try {
        // Revalidate before each fresh process. No worker, solver or context cache is reused across masks.
        if ((await WorkerPackage.LoadAsync(package.WorkerPath, contextToken)).Fingerprint != package.Fingerprint) {
          throw new InvalidDataException("B3 worker package changed after preparation");
        }
        var executable = package.WorkerPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "dotnet" : package.WorkerPath;
        var arguments = executable == "dotnet" ? new[] { package.WorkerPath } : Array.Empty<string>();
        return await new WorkerProcessClient(executable, arguments).RunAsync(request, contextToken);
      } finally { workers.Release(); }
    }, linked.Token);
    var assertions = completion.Attempts.Select(attempt => new VerificationAssertion(attempt.ObligationId,
      origins[attempt.ObligationId], null, obligations.First(o => o.Id == attempt.ObligationId).Description,
      ConvertOutcome(attempt.Outcome))).ToArray();
    var diagnostics = completion.Attempts.Where(attempt => attempt.Outcome != Outcome.Verified)
      .Select(attempt => Diagnostic(origins[attempt.ObligationId],
        "B3 " + attempt.Outcome.ToString().ToLowerInvariant() + ": " +
        obligations.First(o => o.Id == attempt.ObligationId).Description +
        (attempt.Reason == null ? "" : " (" + attempt.Reason + ")"))).ToList();
    if (completion.Error != null || completion.Outcome != Outcome.Verified && diagnostics.Count == 0) {
      diagnostics.Add(Diagnostic(source.Origin, "B3 " + completion.Outcome.ToString().ToLowerInvariant() + ": " +
        (completion.Error ?? "verification did not complete successfully")));
    }
    var errorCount = diagnostics.Count;
    var hitErrorLimit = options.ErrorLimit > 0 && errorCount >= options.ErrorLimit;
    var reported = options.ErrorLimit > 0 ? diagnostics.Take(options.ErrorLimit).ToArray() : diagnostics.ToArray();
    return new VerificationResult(ConvertOutcome(completion.Outcome), reported, assertions,
      completion.TraversalCompleted, started, DateTime.UtcNow - started, null, errorCount, hitErrorLimit);
  }

  public static string? ValidateInvocation(DafnyOptions options) {
    var error = CheckConfiguration(options);
    if (error != null) { return error; }
    try { WorkerPackage.Load(options.Get(B3OptionBag.Worker)?.FullName ?? Path.Combine(AppContext.BaseDirectory, "b3", "DafnyB3Host.dll")); }
    catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or TimeoutException) {
      return "B3 worker package is unavailable or invalid: " + exception.Message;
    }
    return null;
  }
  private static string? CheckConfiguration(DafnyOptions options) {
    if (options.VerifySnapshots > 0) { return "B3 does not support --cache-verification; use 0"; }
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) { return "B3 workers currently require Unix process-group isolation"; }
    if (options.TimeLimit == 0 || options.TimeLimit > int.MaxValue / 1000) { return "B3 requires --verification-time-limit between 1 and 2147483 seconds"; }
    if (options.GetOrOptionDefault(BoogieOptionBag.ArithmeticSolver) != 2) { return "B3 currently supports --arithmetic-solver 2 only"; }
    if (!options.IsUsingZ3()) { return "B3 currently supports Z3 only"; }
    if (options.ProverHelpRequested || options.PrintSplitFile != null || options.PrintPassiveFile != null ||
        options.Get(BoogieOptionBag.IsolateAssertions) || options.Get(CommonOptionBag.ExtractCounterexample) ||
        options.Get(CommonOptionBag.AnalyzeProofs) || options.Get(CommonOptionBag.WarnContradictoryAssumptions) ||
        options.Get(CommonOptionBag.WarnRedundantAssumptions) || options.Get(CommonOptionBag.SuggestProofRefactoring) ||
        options.Get(CommonOptionBag.DisableNonLinearArithmetic) || options.Get(CommonOptionBag.ShowProofObligationExpressions) ||
        options.Get(CommonOptionBag.VerificationCoverageReport) != null ||
        options.Get(CommonOptionBag.VerificationLogFormat)?.Any() == true ||
        options.Get(BoogieOptionBag.BoogieArguments)?.Any() == true || options.Get(BoogieOptionBag.SolverOption)?.Any() == true ||
        options.Get(BoogieOptionBag.SolverPlugin) != null || options.Get(BoogieOptionBag.SolverLog) != null) {
      return "The requested proof-analysis, isolation, logging or custom Boogie/solver options are unsupported by B3";
    }
    var path = SolverPath(options);
    if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return "B3 requires an available --solver-path"; }
    return null;
  }
  private string SolverPath() => SolverPath(options);
  private static string SolverPath(DafnyOptions options) => options.Get(BoogieOptionBag.SolverPath)?.FullName ??
    options.ProverOptions.LastOrDefault(option => option.StartsWith("PROVER_PATH=", StringComparison.Ordinal))?.Substring("PROVER_PATH=".Length) ?? "";
  private static async Task<string> SolverDigestAsync(string path, int timeoutMilliseconds, CancellationToken cancellationToken) {
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    deadline.CancelAfter(Math.Min(timeoutMilliseconds, SolverFileIdentity.MaximumCaptureMilliseconds));
    try {
      return await SolverFileIdentity.ComputeAsync(path, deadline.Token);
    } catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested) {
      throw new TimeoutException("B3 solver identity capture exceeded its deadline", exception);
    }
  }
  private static IOrigin SourceOriginFor(SourceIdentity identity, IOrigin fallback) =>
    Uri.TryCreate(identity.Uri, UriKind.Absolute, out var uri)
      ? new Token(identity.Line, identity.Column) { Uri = uri } : fallback;
  private static DafnyDiagnostic Diagnostic(IOrigin origin, string message) => new(MessageSource.Verifier,
    "", origin.ReportingRange, new[] { message }, ErrorLevel.Error, Array.Empty<DafnyRelatedInformation>());
  private static IVerificationWorkItem Blocked(VerificationSourceInfo source, string name, VerificationOutcome outcome, string message) =>
    new B3WorkItem(new VerificationIdentity(name, "b3:" + name, 0, 0), source, _ => Task.FromResult(
      new VerificationResult(outcome, new[] { Diagnostic(source.Origin, message) }, Array.Empty<VerificationAssertion>(),
        false, DateTime.UtcNow, TimeSpan.Zero, null, 1)));
  private static VerificationOutcome ConvertOutcome(Outcome outcome) => outcome switch {
    Outcome.Verified => VerificationOutcome.Verified, Outcome.Failed => VerificationOutcome.Failed,
    Outcome.Inconclusive => VerificationOutcome.Unknown, Outcome.TimedOut => VerificationOutcome.TimedOut,
    Outcome.ResourceExhausted => VerificationOutcome.OutOfResource, Outcome.OutOfMemory => VerificationOutcome.OutOfMemory,
    Outcome.Cancelled => VerificationOutcome.Cancelled, Outcome.Unsupported => VerificationOutcome.Unsupported,
    _ => VerificationOutcome.ToolError
  };
  public void Dispose() { lifetime.Cancel(); }
  private sealed class ErrorSink : Bpl.IErrorSink {
    public List<string> Messages { get; } = new();
    public void Error(Bpl.IToken token, string message) => Messages.Add(message);
  }
}

internal sealed class B3WorkItem(VerificationIdentity identity, VerificationSourceInfo source,
  Func<CancellationToken, Task<VerificationResult>> run) : IVerificationWorkItem {
  private readonly object sync = new();
  private CancellationTokenSource? cancellation;
  private VerificationStatus status = new VerificationStale();
  public VerificationIdentity Identity => identity;
  public VerificationSourceInfo Source => source;
  public VerificationStatus CacheStatus { get { lock (sync) { return status; } } }
  public bool IsIdle => CacheStatus is not VerificationRunning and not VerificationQueued;
  public IVerificationWorkItem FromSeed(int newSeed) => newSeed == 0 ? this :
    new B3WorkItem(identity with { RandomSeed = newSeed }, source, _ => Task.FromResult(new VerificationResult(
      VerificationOutcome.Unsupported, new[] { new DafnyDiagnostic(MessageSource.Verifier, "", source.Origin.ReportingRange,
        new[] { "B3 does not support seeded verification" }, ErrorLevel.Error, Array.Empty<DafnyRelatedInformation>()) },
      Array.Empty<VerificationAssertion>(), false, DateTime.UtcNow, TimeSpan.Zero, null, 1)));
  public IObservable<VerificationStatus>? TryRun() {
    lock (sync) {
      if (!IsIdle) { return null; }
      var runCancellation = new CancellationTokenSource();
      cancellation = runCancellation;
      status = new VerificationQueued();
      var finished = false;
      var subscribed = 0;
      return Observable.Create<VerificationStatus>(observer => {
        if (Interlocked.Exchange(ref subscribed, 1) != 0) {
          observer.OnError(new InvalidOperationException("A B3 verification run has only one subscription"));
          return Disposable.Empty;
        }
        var token = runCancellation.Token;
        observer.OnNext(new VerificationQueued());
        _ = ExecuteAsync();
        // Disposal belongs to this generation, even when completion starts a new run.
        return Disposable.Create(() => {
          lock (sync) { if (!finished) { runCancellation.Cancel(); } }
        });
        async Task ExecuteAsync() {
          try {
            lock (sync) { status = new VerificationRunning(); }
            observer.OnNext(new VerificationRunning());
            VerificationResult result;
            try {
              token.ThrowIfCancellationRequested();
              result = await run(token);
              token.ThrowIfCancellationRequested();
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
              result = new VerificationResult(VerificationOutcome.Cancelled,
                Array.Empty<DafnyDiagnostic>(), Array.Empty<VerificationAssertion>(), false,
                DateTime.UtcNow, TimeSpan.Zero, null, 0);
            }
            lock (sync) {
              // Cancellation and terminal publication have one linearization point.
              if (token.IsCancellationRequested) {
                result = new VerificationResult(VerificationOutcome.Cancelled,
                  Array.Empty<DafnyDiagnostic>(), Array.Empty<VerificationAssertion>(), false,
                  result.StartTime, result.RunTime, null, 0);
              }
              status = new VerificationCompleted(result);
              observer.OnNext(new VerificationCompleted(result));
              observer.OnCompleted();
            }
          } catch (Exception exception) {
            lock (sync) { if (ReferenceEquals(cancellation, runCancellation)) { status = new VerificationStale(); } }
            observer.OnError(exception);
          } finally {
            lock (sync) {
              finished = true;
              if (ReferenceEquals(cancellation, runCancellation)) { cancellation = null; }
              runCancellation.Dispose();
            }
          }
        }
      });
    }
  }
  public void Cancel() { lock (sync) { cancellation?.Cancel(); } }
}
