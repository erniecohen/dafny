using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace B3AlcGate;

public sealed record RunReceipt(string Product, string Control, string[] Arguments, int? ExitCode,
  string Output, string ErrorOutput, string? Failure, AssemblyEntry[] LoaderLedger,
  string SchedulerCleanup, bool ContextCollected, int[] RemainingDirectChildren, ProofCleanup? ProofCleanup);

/// <summary>
/// No implementation ships in this prototype. Verification requires an audited transparent
/// solver wrapper/supervisor that owns every solver process group and its descendant ledger.
/// The wrapper must forward bytes unchanged, and cleanup must occur after CLI results/logs.
/// </summary>
public interface IProofLifecycleSupervisor {
  string WrapperPath { get; }
  IProofRunScope BeginRun(Product product, IReadOnlyList<string> arguments);
}

public interface IProofRunScope : IDisposable {
  ProofCleanup StopAndAssertNoOwnedSolvers(TimeSpan safetyDeadline);
}

public sealed record ProofCleanup(int RecordedSolverGroups, int LiveOwnedProcesses, string Evidence);

public static class Runs {
  private static int sequence;
  private static int active;
  private static bool poisoned;
  private static readonly TimeSpan ControlDeadline = TimeSpan.FromSeconds(30);
  private static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(10);

  public static RunReceipt RunControl(Product product, string name, string[] arguments) {
    var allowed = name switch {
      "help" => new[] { "--help" },
      "malformed-command" => new[] { "__b3_alc_invalid_command_6f60f0eb__" },
      _ => throw new ArgumentException("Only the two non-verifying controls are implemented.")
    };
    if (!arguments.SequenceEqual(allowed, StringComparer.Ordinal)) {
      throw new ArgumentException("Control arguments must match the static fixture exactly.");
    }
    if (name == "malformed-command" && File.Exists(arguments[0])) {
      throw new IOException("The invalid-command fixture unexpectedly names an existing file.");
    }
    return Run(product, name, arguments.ToArray(), null, ControlDeadline);
  }

  // Deliberately not exposed by Program.Main. The complete native-cost harness and
  // lifecycle supervisor are separate future work, not an implicit verification gate.
  public static RunReceipt RunVerification(Product product, string[] arguments,
    IProofLifecycleSupervisor? supervisor, TimeSpan invocationSafetyDeadline) {
    ArgumentNullException.ThrowIfNull(supervisor);
    if (arguments.Length < 3 || arguments[0] != "verify" || invocationSafetyDeadline <= TimeSpan.Zero) {
      throw new ArgumentException("A bounded explicit verify invocation is required.");
    }
    var positions = arguments.Select((argument, index) => (argument, index))
      .Where(p => p.argument == "--solver-path" || p.argument.StartsWith("--solver-path=", StringComparison.Ordinal)).ToArray();
    if (positions.Length != 1 || positions[0].argument != "--solver-path" || positions[0].index + 1 >= arguments.Length ||
        !System.IO.Path.IsPathFullyQualified(supervisor.WrapperPath) || !File.Exists(supervisor.WrapperPath) ||
        System.IO.Path.GetFullPath(arguments[positions[0].index + 1]) != System.IO.Path.GetFullPath(supervisor.WrapperPath)) {
      throw new ArgumentException("Verification must select exactly the configured transparent wrapper with --solver-path PATH.");
    }
    return Run(product, "verification-entrypoint-only", arguments.ToArray(), supervisor, invocationSafetyDeadline);
  }

  private static RunReceipt Run(Product product, string name, string[] arguments,
    IProofLifecycleSupervisor? supervisor, TimeSpan invocationSafetyDeadline) {
    if (Interlocked.CompareExchange(ref active, 1, 0) != 0) {
      throw new InvalidOperationException("ALC invocations must be serial in this host.");
    }
    try {
      if (poisoned) {
        throw new InvalidOperationException("A prior invocation failed its lifecycle/loader checks. Start a new host.");
      }
      var receipt = RunSerial(product, name, arguments, supervisor, invocationSafetyDeadline);
      poisoned = receipt.Failure != null;
      return receipt;
    } catch {
      poisoned = true;
      throw;
    } finally {
      Volatile.Write(ref active, 0);
    }
  }

  private static RunReceipt RunSerial(Product product, string name, string[] arguments,
    IProofLifecycleSupervisor? supervisor, TimeSpan invocationSafetyDeadline) {
    if (!OperatingSystem.IsLinux() || Environment.ProcessorCount > 8) {
      throw new PlatformNotSupportedException("Use a Linux CI host with at most eight visible processors.");
    }
    if (Framework.LiveChildren().Length != 0) {
      throw new InvalidOperationException("The host already owns child processes; this bounded prototype requires an empty baseline.");
    }
    var before = Framework.Witness();
    var detached = InvokeAndUnload(product, name, arguments, supervisor, invocationSafetyDeadline);
    var collected = Collect(detached.Context);
    var children = Framework.LiveChildren();
    var failure = detached.Receipt.Failure;
    if (!collected || children.Length != 0 || before != Framework.Witness()) {
      failure = Append(failure, "Isolation failed: context survived, child processes remain, or framework witness changed.");
    }
    return detached.Receipt with { ContextCollected = collected, RemainingDirectChildren = children, Failure = failure };
  }

  private sealed record Detached(RunReceipt Receipt, WeakReference Context);

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static Detached InvokeAndUnload(Product product, string name, string[] arguments,
    IProofLifecycleSupervisor? supervisor, TimeSpan invocationSafetyDeadline) {
    ProductContext? context = null;
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
      proofScope = supervisor?.BeginRun(product, Array.AsReadOnly(recordedArguments));
      if (supervisor != null && proofScope == null) {
        throw new InvalidOperationException("A configured supervisor did not create a proof ownership scope.");
      }
      context = new ProductContext(product, "b3-alc-run-" + Interlocked.Increment(ref sequence));
      using (context.EnterContextualReflection()) {
        var driver = context.LoadDriver();
        var entry = driver.GetType("Microsoft.Dafny.DafnyBackwardsCompatibleCli", throwOnError: true)!;
        var method = entry.GetMethod("MainWithWriters", BindingFlags.Public | BindingFlags.Static,
          binder: null, types: [typeof(TextWriter), typeof(TextWriter), typeof(TextReader), typeof(string[])], modifiers: null)
          ?? throw new MissingMethodException(entry.FullName, "MainWithWriters");
        if (method.ReturnType != typeof(Task<int>)) {
          throw new InvalidOperationException("MainWithWriters must return the shared framework Task<int>.");
        }
        var task = (Task<int>)(method.Invoke(null, [output, error, new StringReader(""), arguments.ToArray()])
          ?? throw new InvalidOperationException("The entrypoint returned null."));
        if (!task.Wait(invocationSafetyDeadline)) {
          throw new TimeoutException("Invocation safety deadline exceeded. Stop this host; no next run is permitted.");
        }
        exitCode = task.GetAwaiter().GetResult();
      }
    } catch (Exception exception) {
      // Never let a private Exception/Type/Task escape the no-inline frame.
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
      } catch (Exception exception) {
        failure = Append(failure, "Proof cleanup: " + Explain(exception));
      }
      try {
        proofScope?.Dispose();
      } catch (Exception exception) {
        failure = Append(failure, "Proof scope disposal: " + Explain(exception));
      }
      if (context != null) {
        try {
          schedulerCleanup = DisposeScheduler(context);
        } catch (Exception exception) {
          failure = Append(failure, "Scheduler cleanup: " + Explain(exception));
        }
        try {
          ledger = context.Snapshot();
        } catch (Exception exception) {
          failure = Append(failure, "Loader audit: " + Explain(exception));
        }
      }
    }
    // All strong loader/reflection handles stay in this frame. A long weak reference
    // follows finalization and must become dead before the next invocation can begin.
    var weak = new WeakReference(context, trackResurrection: true);
    context?.Unload();
    return new(new(product.Label, name, recordedArguments, exitCode, output.Text, error.Text, failure, ledger,
      schedulerCleanup, false, [], proofCleanup), weak);
  }

  private static string DisposeScheduler(ProductContext context) {
    var core = context.Assemblies.SingleOrDefault(a => a.GetName().Name == "DafnyCore");
    if (core == null) {
      return "DafnyCore was not loaded";
    }
    var type = core.GetType("Microsoft.Dafny.DafnyMain", throwOnError: true)!;
    var field = type.GetField("LargeThreadScheduler", BindingFlags.Public | BindingFlags.Static)
      ?? throw new MissingFieldException(type.FullName, "LargeThreadScheduler");
    // This may initialize DafnyMain even for --help. The bounded processor check
    // contains the effect; immediately dispose its per-context worker threads.
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
    while ((exception is TargetInvocationException or AggregateException) && exception.InnerException is { } inner) {
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
        if (buffer.Length >= 131072) {
          throw new IOException("Control output exceeded the 128 Ki-character safety cap.");
        }
        buffer.Append(value);
      }
    }
    public override void Write(string? value) {
      if (value == null) { return; }
      lock (buffer) {
        if (value.Length > 131072 - buffer.Length) {
          throw new IOException("Control output exceeded the 128 Ki-character safety cap.");
        }
        buffer.Append(value);
      }
    }
  }
}
