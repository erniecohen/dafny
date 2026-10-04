using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.Versioning;

namespace B3AlcGate;

/// <summary>
/// Separate fixed-smoke invocation with persistent exact common Boogie libraries.
/// The ordinary MainWithWriters, arguments, byte-forwarding supervisor, and actual
/// scheduler/weak-reference cleanup are preserved. No shared statics are reset.
/// </summary>
[SupportedOSPlatform("linux")]
internal static class SharedNativeRuns {
  private static int sequence;
  private static int active;
  private static bool poisoned;
  private static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(10);

  public static RunReceipt RunVerification(ProductPin product, SharedCommonLibraries shared, string[] arguments,
    IProofLifecycleSupervisor supervisor, TimeSpan invocationSafetyDeadline, NativeProofSession.Permit permit) {
    ArgumentNullException.ThrowIfNull(permit);
    try {
      permit.Consume(product, shared, arguments, supervisor, invocationSafetyDeadline);
      var receipt = ReviewedInvocationImplementation(product, shared, arguments, supervisor, invocationSafetyDeadline, permit);
      if (receipt.Failure != null) { permit.Fail(); }
      return receipt;
    } catch { permit.Fail(); throw; }
  }

  private static RunReceipt ReviewedInvocationImplementation(ProductPin product, SharedCommonLibraries shared, string[] arguments,
    IProofLifecycleSupervisor supervisor, TimeSpan invocationSafetyDeadline, NativeProofSession.Permit permit) {
    ArgumentNullException.ThrowIfNull(supervisor);
    if (arguments.Length < 3 || arguments[0] != "verify" || invocationSafetyDeadline <= TimeSpan.Zero) {
      throw new ArgumentException("A bounded explicit verify invocation is required.");
    }
    var positions = arguments.Select((argument, index) => (argument, index))
      .Where(p => p.argument == "--solver-path" || p.argument.StartsWith("--solver-path=", StringComparison.Ordinal)).ToArray();
    if (positions.Length != 1 || positions[0].argument != "--solver-path" || positions[0].index + 1 >= arguments.Length ||
        !Path.IsPathFullyQualified(supervisor.WrapperPath) || !File.Exists(supervisor.WrapperPath) ||
        Path.GetFullPath(arguments[positions[0].index + 1]) != Path.GetFullPath(supervisor.WrapperPath)) {
      throw new ArgumentException("Verification must select exactly the configured transparent wrapper with --solver-path PATH.");
    }
    if (Interlocked.CompareExchange(ref active, 1, 0) != 0) {
      throw new InvalidOperationException("Shared-common native invocations must be serial in this host.");
    }
    try {
      if (poisoned) { throw new InvalidOperationException("A prior shared-common invocation failed. Stop this host."); }
      var receipt = RunSerial(product, shared, arguments.ToArray(), supervisor, invocationSafetyDeadline, permit);
      poisoned = receipt.Failure != null;
      return receipt;
    } catch {
      poisoned = true;
      throw;
    } finally { Volatile.Write(ref active, 0); }
  }

  private static RunReceipt RunSerial(ProductPin product, SharedCommonLibraries shared, string[] arguments,
    IProofLifecycleSupervisor supervisor, TimeSpan invocationSafetyDeadline, NativeProofSession.Permit permit) {
    if (!OperatingSystem.IsLinux() || Environment.ProcessorCount > 8) {
      throw new PlatformNotSupportedException("Use a Linux CI host with at most eight visible processors.");
    }
    if (Framework.LiveChildren().Length != 0) {
      throw new InvalidOperationException("The host already owns child processes.");
    }
    shared.Audit("before-native-run-" + product.Label + "-" + (sequence + 1));
    shared.AssertAcceptable();
    var before = Framework.Witness();
    var detached = InvokeAndUnload(product, shared, arguments, supervisor, invocationSafetyDeadline, permit);
    var collected = Collect(detached.Context);
    var children = Framework.LiveChildren();
    var failure = detached.Receipt.Failure;
    if (!collected || children.Length != 0 || before != Framework.Witness()) {
      failure = Append(failure, "Isolation failed: context survived, child processes remain, or framework witness changed.");
    }
    try { permit.BeforeInvocation(); shared.Audit("after-weak-collection-" + product.Label + "-" + sequence); }
    catch (Exception exception) { failure = Append(failure, "Shared Default audit: " + Explain(exception)); }
    return detached.Receipt with { ContextCollected = collected, RemainingDirectChildren = children, Failure = failure };
  }

  private sealed record Detached(RunReceipt Receipt, WeakReference Context);

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static Detached InvokeAndUnload(ProductPin product, SharedCommonLibraries shared, string[] arguments,
    IProofLifecycleSupervisor supervisor, TimeSpan invocationSafetyDeadline, NativeProofSession.Permit permit) {
    SharedProductContext? context = null;
    IProofRunScope? proofScope = null;
    var output = new BoundedWriter();
    var error = new BoundedWriter();
    int? exitCode = null;
    string? failure = null;
    var ledger = Array.Empty<AssemblyEntry>();
    var schedulerCleanup = "not loaded";
    ProofCleanup? proofCleanup = null;
    var recordedArguments = arguments.ToArray();
    try {
      proofScope = supervisor.BeginRun(new Product(product.Label, product.Directory), Array.AsReadOnly(recordedArguments));
      if (proofScope == null) { throw new InvalidOperationException("The supervisor did not create a proof ownership scope."); }
      context = new SharedProductContext(product, shared, "b3-shared-common-native-run-" + Interlocked.Increment(ref sequence));
      using (context.EnterContextualReflection()) {
        var driver = context.LoadDriver();
        var entry = driver.GetType("Microsoft.Dafny.DafnyBackwardsCompatibleCli", throwOnError: true)!;
        var method = entry.GetMethod("MainWithWriters", BindingFlags.Public | BindingFlags.Static,
          binder: null, types: [typeof(TextWriter), typeof(TextWriter), typeof(TextReader), typeof(string[])], modifiers: null)
          ?? throw new MissingMethodException(entry.FullName, "MainWithWriters");
        if (method.ReturnType != typeof(Task<int>)) {
          throw new InvalidOperationException("MainWithWriters must return the shared framework Task<int>.");
        }
        permit.BeforeInvocation();
        var task = (Task<int>)(method.Invoke(null, [output, error, new StringReader(""), arguments.ToArray()])
          ?? throw new InvalidOperationException("The entrypoint returned null."));
        if (!task.Wait(invocationSafetyDeadline)) {
          throw new TimeoutException("Invocation safety deadline exceeded. Stop this host; no next run is permitted.");
        }
        exitCode = task.GetAwaiter().GetResult();
        shared.AssertAcceptable();
      }
    } catch (Exception exception) {
      // Nothing private (Exception, Type, Assembly, task, option, delegate) is returned.
      failure = Explain(exception);
    } finally {
      try {
        if (proofScope != null) {
          proofCleanup = proofScope.StopAndAssertNoOwnedSolvers(CleanupDeadline);
          if (proofCleanup.RecordedSolverGroups < 1 || proofCleanup.LiveOwnedProcesses != 0 ||
              string.IsNullOrWhiteSpace(proofCleanup.Evidence)) {
            throw new InvalidOperationException("Verification has no complete zero-owned-solver cleanup receipt.");
          }
        }
      } catch (Exception exception) { failure = Append(failure, "Proof cleanup: " + Explain(exception)); }
      try { proofScope?.Dispose(); }
      catch (Exception exception) { failure = Append(failure, "Proof scope disposal: " + Explain(exception)); }
      if (context != null) {
        try { schedulerCleanup = DisposeScheduler(context); }
        catch (Exception exception) { failure = Append(failure, "Scheduler cleanup: " + Explain(exception)); }
        try { ledger = context.Snapshot(); }
        catch (Exception exception) { failure = Append(failure, "Loader audit: " + Explain(exception)); }
      }
    }
    // Default common assemblies remain alive by design. Only the private product
    // context is expected to collect; external shared callbacks retaining it fail.
    var weak = new WeakReference(context, trackResurrection: true);
    if (context != null) { shared.RetireContext(context); context.Unload(); }
    return new(new(product.Label, "fixed-native-smoke-with-common-boogie", recordedArguments, exitCode,
      output.Text, error.Text, failure, ledger, schedulerCleanup, false, [], proofCleanup), weak);
  }

  private static string DisposeScheduler(SharedProductContext context) {
    var core = context.Assemblies.SingleOrDefault(a => a.GetName().Name == "DafnyCore");
    if (core == null) { return "DafnyCore was not loaded"; }
    var type = core.GetType("Microsoft.Dafny.DafnyMain", throwOnError: true)!;
    var field = type.GetField("LargeThreadScheduler", BindingFlags.Public | BindingFlags.Static)
      ?? throw new MissingFieldException(type.FullName, "LargeThreadScheduler");
    if (field.GetValue(null) is not IDisposable scheduler) {
      throw new InvalidOperationException("The private large-stack scheduler is not disposable.");
    }
    scheduler.Dispose();
    return "LargeThreadScheduler.Dispose called; actual collection checked after leaving the frame";
  }

  private static bool Collect(WeakReference context) {
    var deadline = Stopwatch.StartNew();
    while (context.IsAlive && deadline.Elapsed < CleanupDeadline) {
      GC.Collect();
      GC.WaitForPendingFinalizers();
      GC.Collect();
      Thread.Sleep(50);
    }
    return !context.IsAlive;
  }

  private static string Explain(Exception exception) {
    for (var depth = 0; depth < 8 && exception.InnerException is { } inner; depth++) {
      if (exception is AggregateException aggregate && aggregate.InnerExceptions.Count != 1) { break; }
      if (exception is not (TargetInvocationException or AggregateException)) { break; }
      exception = inner;
    }
    var text = exception.GetType().FullName + ": " + exception.Message;
    return text.Length <= 4096 ? text : text[..4096];
  }
  private static string Append(string? prior, string next) => prior == null ? next : prior + "\n" + next;

  private sealed class BoundedWriter : TextWriter {
    private readonly StringBuilder buffer = new();
    public override Encoding Encoding => Encoding.UTF8;
    public string Text { get { lock (buffer) { return buffer.ToString(); } } }
    public override void Write(char value) {
      lock (buffer) {
        if (buffer.Length >= 131072) { throw new IOException("Control output exceeded the 128 Ki-character safety cap."); }
        buffer.Append(value);
      }
    }
    public override void Write(string? value) {
      if (value == null) { return; }
      lock (buffer) {
        if (value.Length > 131072 - buffer.Length) { throw new IOException("Control output exceeded the 128 Ki-character safety cap."); }
        buffer.Append(value);
      }
    }
  }
}
