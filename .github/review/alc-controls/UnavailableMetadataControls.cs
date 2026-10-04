using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Text.Json;

namespace B3AlcGate;

internal sealed record UnavailableControlInputs(string Control, string BaselineDirectory, string CandidateDirectory,
  string BaselineArchive, PinnedEvidence CandidatePackageManifest, string CandidateSourceCommit,
  PinnedEvidence SourceManifest, string Receipt);
internal sealed record UnavailableControlObservation(bool DenialExceptionObserved, bool TriggerApiInvoked, string TriggerApi, string? ExceptionSummary,
  AssemblyEntry[] PrivateLoaderLedger, string SchedulerCleanup, bool ContextCollected);

/// <summary>Exactly one fixed nonproof control per disposable host. Never runs Dafny or a solver.</summary>
[SupportedOSPlatform("linux")]
internal static class UnavailableMetadataControls {
  internal static readonly string[] Names = ["default-unavailable-demand", "private-unavailable-demand", "reactive-event-unavailable-demand"];
  private static int entered;
  private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true, MaxDepth = 64 };
  private static readonly TimeSpan CollectionDeadline = TimeSpan.FromSeconds(10);

  // Explicit accessors retain no subscriber or product object. The pinned public
  // Type/string overload performs its ordinary reflection against this static event.
  public static class OrdinaryEventFixture {
    public static event EventHandler Happened { add { } remove { } }
  }

  public static int Run(UnavailableControlInputs input) {
    NativeProofSmokeControls.Require(Interlocked.CompareExchange(ref entered, 1, 0) == 0, "nonproof-control-host-single-use");
    SharedCommonLibraries? shared = null;
    ProductPin[] products = [];
    FrameworkWitness? before = null;
    UnavailableControlObservation? observation = null;
    string? failure = null;
    SharedCommonReceipt? closure = null;
    string? ownManifestSha256 = null;
    var stopwatch = Stopwatch.StartNew();
    try {
      NativeProofSmokeControls.Require(OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
        Environment.ProcessorCount <= 8, "bounded-linux-x64-nonproof-host-required");
      NativeProofSmokeControls.Require(Names.Contains(input.Control, StringComparer.Ordinal), "unknown-fixed-nonproof-control");
      NativeProofSmokeControls.Require(AssemblyLoadContext.All.Count() == 1 && Framework.LiveChildren().Length == 0,
        "initial-default-only-no-children-required");
      before = Framework.Witness();
      // Validate a fresh manifest and exact package bytes before any product loads.
      // No solver/cgroup/lifecycle inputs are accepted by this nonproof entrypoint.
      ownManifestSha256 = ValidateSourceManifest(input.SourceManifest);
      var receipt = Path.GetFullPath(input.Receipt);
      NativeProofSmokeControls.Require(!File.Exists(receipt) && Directory.Exists(Path.GetDirectoryName(receipt)),
        "new-nonproof-receipt-in-existing-directory-required");
      products = NativeProofSmokeControls.MetadataControlProducts(new(input.BaselineDirectory, input.CandidateDirectory,
        input.BaselineArchive, input.CandidatePackageManifest, input.CandidateSourceCommit,
        new("", ""), new("", ""), new("", ""), "", "", "", ""));
      shared = SharedCommonLibraries.CreateAndPreload(products[0], products[1]);
      shared.AssertAcceptable();
      switch (input.Control) {
        case "default-unavailable-demand":
          shared.Audit("before-default-negative-demand");
          observation = ObserveDefaultDemand();
          break;
        case "private-unavailable-demand":
          shared.Audit("before-private-negative-demand");
          observation = ObservePrivateDemand(products[0], shared);
          break;
        case "reactive-event-unavailable-demand":
          shared.Audit("before-reactive-event-negative-demand");
          observation = ObserveReactiveDemand(shared);
          break;
        default: throw new InvalidOperationException("Fixed registry dispatch failed.");
      }
      var state = shared.DemandState();
      var expectedRoute = input.Control == "private-unavailable-demand" ? "private-load" : "default-resolving";
      NativeProofSmokeControls.Require(observation.DenialExceptionObserved && observation.TriggerApiInvoked && state.Poisoned &&
        state.Failures.SequenceEqual(new[] { SharedCommonLibraries.DenialCode }) && state.UnavailableDemands.Length == 1 &&
        state.UnavailableDemands[0].Route == expectedRoute && state.UnavailableDemands[0].ExactDeclaredIdentity &&
        state.UnavailableDemands[0].RequestedIdentity == SharedCommonLibraries.UnavailableIdentity &&
        state.UnavailableDemands[0].MetadataOwnerSha256 == SharedCommonLibraries.UnavailableOwnerSha256 &&
        state.UnavailableDemands[0].DenialCode == SharedCommonLibraries.DenialCode,
        "exact-controlled-denial-and-immutable-poison-required");
      var acceptanceRejected = false;
      try { shared.AssertAcceptable(); }
      catch (NativeProofSmokeControls.SmokeFailure exception) {
        acceptanceRejected = exception.Message == "shared-scope-poisoned-or-unavailable-demand-observed";
      }
      NativeProofSmokeControls.Require(acceptanceRejected, "caught-denial-cannot-restore-acceptance");
      NativeProofSmokeControls.Require(observation.ContextCollected && before == Framework.Witness() && Framework.LiveChildren().Length == 0,
        "nonproof-post-denial-cleanup-required");
      shared.Audit("nonproof-negative-control-complete");
    } catch (Exception exception) { failure = Explain(exception); }
    finally {
      if (shared != null) {
        try { closure = shared.CloseAndSnapshot("nonproof-final-audit-and-retirement"); }
        catch (Exception exception) {
          failure = Append(failure, "Final audit: " + Explain(exception));
          closure = shared.Snapshot(); shared.Dispose();
        }
      }
    }
    var remaining = Framework.LiveChildren();
    if (remaining.Length != 0 || AssemblyLoadContext.All.Count() != 1 || (before != null && before != Framework.Witness())) {
      failure = Append(failure, "Nonproof final host isolation failed.");
    }
    var passed = failure == null && observation is { DenialExceptionObserved: true, ContextCollected: true } &&
      closure is { CompleteMetadataInventory: true, CompleteMetadataAvailability: false } && closure.RuntimeDemandState.Poisoned &&
      closure.RuntimeDemandState.Failures.SequenceEqual(new[] { SharedCommonLibraries.DenialCode }) &&
      closure.RuntimeDemandState.UnavailableDemands.Length == 1 &&
      closure.RuntimeDemandState.UnavailableDemands[0].ExactDeclaredIdentity &&
      closure.RuntimeAssemblyLoads.All(load => load.Validated);
    var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
      schemaVersion = 1, scope = "prototype/fixed-disposable-nonproof-unavailable-metadata-denial-control",
      control = input.Control, expectedControlNames = Names, passed, failure,
      ordinaryProofCliEnabled = false, nativeProofExecuted = false, solverExecuted = false,
      completeMetadataInventory = closure?.CompleteMetadataInventory == true, completeMetadataAvailability = false,
      harnessAssemblySha256 = Framework.Sha256(typeof(UnavailableMetadataControls).Assembly.Location),
      sourceManifestSha256 = ownManifestSha256, commonFramework = before, products, observation,
      commonClosure = closure, remainingDirectChildren = remaining, onlyDefaultContextRemains = AssemblyLoadContext.All.Count() == 1,
      stopwatchSafetyMilliseconds = stopwatch.ElapsedMilliseconds,
      hostMustTerminateAfterControl = true, nativeQueryOrResourceParityEstablished = false
    }, Json);
    using (var stream = new FileStream(input.Receipt, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); }
    Console.WriteLine("nonproof-control-receipt-sha256=" + NativeProofSmokeControls.Sha256(bytes));
    return passed ? 0 : 1;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static UnavailableControlObservation ObserveDefaultDemand() {
    Exception? caught = null;
    try { AssemblyLoadContext.Default.LoadFromAssemblyName(new(SharedCommonLibraries.UnavailableIdentity)); }
    catch (Exception exception) { caught = exception; }
    return new(IsDenial(caught), true, "AssemblyLoadContext.Default.LoadFromAssemblyName(exact-unavailable-identity)", caught == null ? null : Explain(caught), [], "not initialized: no product entrypoint invoked", true);
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static UnavailableControlObservation ObserveReactiveDemand(SharedCommonLibraries shared) {
    Exception? caught = null;
    var invoked = false;
    try {
      var reactive = shared.CommonForNonproofControl("System.Reactive");
      var observable = reactive.GetType("System.Reactive.Linq.Observable", throwOnError: true)!;
      var method = observable.GetMethod("FromEventPattern", BindingFlags.Public | BindingFlags.Static,
        binder: null, types: [typeof(Type), typeof(string)], modifiers: null)
        ?? throw new MissingMethodException("Exact public Reactive FromEventPattern(Type,string) overload is missing.");
      NativeProofSmokeControls.Require(!method.IsGenericMethodDefinition && method.DeclaringType == observable,
        "exact-public-reactive-trigger-overload-required");
      invoked = true;
      method.Invoke(null, [typeof(OrdinaryEventFixture), nameof(OrdinaryEventFixture.Happened)]);
    } catch (Exception exception) { caught = exception; }
    return new(IsDenial(caught), invoked, "System.Reactive.Linq.Observable.FromEventPattern(Type,string)", caught == null ? null : Explain(caught), [], "not initialized: pinned Reactive public surface only", true);
  }

  private sealed record DetachedPrivate(UnavailableControlObservation Observation, WeakReference Context);

  private static UnavailableControlObservation ObservePrivateDemand(ProductPin product, SharedCommonLibraries shared) {
    var detached = InvokePrivateDemandAndUnload(product, shared);
    var deadline = Stopwatch.StartNew();
    while (detached.Context.IsAlive && deadline.Elapsed < CollectionDeadline) {
      GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Thread.Sleep(50);
    }
    return detached.Observation with { ContextCollected = !detached.Context.IsAlive };
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static DetachedPrivate InvokePrivateDemandAndUnload(ProductPin product, SharedCommonLibraries shared) {
    SharedProductContext? context = null;
    string? exceptionSummary = null;
    bool denial = false;
    string? cleanupFailure = null;
    var ledger = Array.Empty<AssemblyEntry>();
    try {
      context = new SharedProductContext(product, shared, "unavailable-private-negative-control");
      context.LoadCoreForControl();
      shared.Audit("private-core-pinned-before-demand");
      using (context.EnterContextualReflection()) {
        try { context.LoadFromAssemblyName(new(SharedCommonLibraries.UnavailableIdentity)); }
        catch (Exception exception) { denial = IsDenial(exception); exceptionSummary = Explain(exception); }
      }
      ledger = context.Snapshot();
    } catch (Exception exception) {
      // Return only detached text even on audit/initialization failure, so the
      // caller still performs bounded actual weak collection and fails the control.
      cleanupFailure = Explain(exception);
      denial = false;
      shared.Poison("private-negative-control-setup-or-audit-failed");
    } finally {
      if (context != null) {
        try { shared.RetireContext(context); }
        catch (Exception exception) { denial = false; cleanupFailure = Append(cleanupFailure, Explain(exception)); }
        finally { context.Unload(); }
      }
    }
    // Detached only strings/records. No Assembly, Type, exception or scheduler root
    // survives this noinline frame. DafnyMain was never accessed or initialized.
    return new(new(denial, denial, "Owned SharedProductContext.LoadFromAssemblyName(exact-unavailable-identity)", cleanupFailure == null ? exceptionSummary : Append(exceptionSummary, cleanupFailure), ledger, "not initialized: only pinned DafnyCore assembly metadata accessed", false),
      new WeakReference(context, trackResurrection: true));
  }

  private static string ValidateSourceManifest(PinnedEvidence pin) {
    var bytes = NativeProofSmokeControls.ReadBytes(pin.Path, 262144);
    NativeProofSmokeControls.Require(NativeProofSmokeControls.Digest(pin.Sha256) && NativeProofSmokeControls.Sha256(bytes) == pin.Sha256,
      "nonproof-source-manifest-pin-mismatch");
    using var manifest = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
    var root = manifest.RootElement;
    NativeProofSmokeControls.Require(root.GetProperty("schemaVersion").GetInt32() == 1 &&
      root.GetProperty("unavailableMetadataBoundary").GetProperty("nativeProofEnabled").GetBoolean() == false &&
      root.GetProperty("unavailableMetadataBoundary").GetProperty("completeMetadataAvailability").GetBoolean() == false,
      "nonproof-source-manifest-scope-mismatch");
    var files = root.GetProperty("files").EnumerateArray().ToArray();
    NativeProofSmokeControls.Require(files.Length is > 0 and <= 64 && files.Select(f => f.GetProperty("path").GetString()).Distinct().Count() == files.Length,
      "nonproof-source-manifest-file-bound");
    foreach (var required in new[] { "UnavailableMetadataControls.cs", "UnavailableMetadataProgram.cs", "SharedCommonLibraries.cs", "SharedProductContext.cs", "UNAVAILABLE-METADATA-SOURCE-EVIDENCE.json" }) {
      var file = files.Single(f => f.GetProperty("path").GetString() == required);
      NativeProofSmokeControls.Require(NativeProofSmokeControls.Digest(file.GetProperty("sha256").GetString() ?? ""),
        "nonproof-source-required-component-pin");
    }
    NativeProofSmokeControls.Require(files.Single(f => f.GetProperty("path").GetString() == "UNAVAILABLE-METADATA-SOURCE-EVIDENCE.json")
      .GetProperty("sha256").GetString() == "7166e6c53486b9ab3e77a7072328e9f841d7bf1797c550718e170b29913944cc",
      "nonproof-source-evidence-pin-mismatch");
    return pin.Sha256;
  }

  private static bool IsDenial(Exception? exception) {
    for (var depth = 0; exception != null && depth < 8; depth++, exception = exception.InnerException) {
      if (exception is FileNotFoundException && exception.Message == SharedCommonLibraries.DenialCode) { return true; }
    }
    return false;
  }
  private static string Explain(Exception exception) {
    while ((exception is TargetInvocationException or AggregateException) && exception.InnerException is { } inner) { exception = inner; }
    var text = exception.GetType().FullName + ": " + exception.Message;
    return text.Length <= 4096 ? text : text[..4096];
  }
  private static string Append(string? prior, string next) => prior == null ? next : prior + "\n" + next;
}
