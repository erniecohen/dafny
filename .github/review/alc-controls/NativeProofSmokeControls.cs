using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

namespace B3AlcGate;

// Fixed qualified shared-common smoke. Program.Main deliberately has no route to this API.
// Its caller must be the reviewed fixed CI coordinator, never a forwarded Dafny CLI.
internal sealed record PinnedEvidence(string Path, string Sha256);
internal sealed record ProofSmokeInputs(string BaselineDirectory, string CandidateDirectory,
  string BaselineArchive, PinnedEvidence CandidatePackageManifest, string CandidateSourceCommit,
  PinnedEvidence LifecycleReceipt, PinnedEvidence LifecycleSourceManifest,
  PinnedEvidence SolverOriginEvidence, string Solver, string SolverSha256,
  string CgroupParent, string Receipt, PinnedEvidence? NonproofQualification = null);
internal sealed record PackageFilePin(string Path, string Sha256, long Bytes);
internal sealed record ProductPin(string Label, string SourceCommit, string Directory,
  string CoreSha256, string CoreInformationalVersion, PackageFilePin[] Files);
internal sealed record ProofSmokeCaseReceipt(string Name, string Product, string Fixture,
  string FixtureSha256, int ExpectedExitCode, RunReceipt? Run,
  NativeProofEvidenceReceipt? Evidence, bool Passed, string? FailureCode);

[SupportedOSPlatform("linux")]
internal static class NativeProofSmokeControls {
  internal const string BaselineSource = "b07c038737d6713b6d1a5848d7568bdc972de7dd";
  internal const string BaselineArchiveSha256 = "679b3569a3e188cdea5d9c061a463ef4adab849584c7d6990434bd87f80f1bae";
  internal const string BaselineCoreSha256 = "9e4eaf6a52cc7ed29d8df5e2185ce00032382a9e9be54688bbcfda2262c865b9";
  internal const string BaselineCoreVersion = "4.11.0+fcb2042d.review.a171069d";
  internal const string CoreIdentity = "DafnyCore, Version=4.11.0.0, Culture=neutral, PublicKeyToken=null";
  internal const string LifecycleReceiptSha256 = "60992bd42a47fbc9e3fca7294253d182947dd8ddfd4977d76361818bac2576bd";
  internal const string LifecycleManifestSha256 = "e25b41f0d0fb6c29708d64cccc5c4b945bd64f177054156db667688243cf9a42";
  internal const string LifecycleHarnessSha256 = "ec2e3910a0cdc4871a1c1c52b540c6e302b09fd83d1e47e509ab1d91855db680";
  private static int entered;
  private static readonly (string Name, string File, string Routine, int Exit, string Sha256)[] Fixtures = [
    ("true", "true.dfy", "NativeTrue", 0, "bf2c7f7498a9221291dbc5244c3725f0fb9af93b741638595bdc11f3b83d24e6"),
    ("reachable-false", "false.dfy", "NativeFalse", 4, "a50e9615c8b49eefe55a458f6f4df613f11164406074b9a011f7f389aad2dc8e"),
    ("fuel", "fuel.dfy", "NativeFuel", 0, "55a2d36861839f955e222ab8b2ae6f838645ef013e8610b588f391bbe61c3206")
  ];
  private static readonly string[] LifecycleNames = ["missing-delegation", "wrong-solver-pin", "sealed-version", "sealed-mutation",
    "creator-thread-exit", "binary-streams-eof", "relay-broken-pipe", "timeout-reparented-descendant",
    "live-descendant-cap", "refused-late-launch", "malformed-completion"];
  // A lifecycle receipt remains applicable only while these exact implementation and
  // fixed-control files match its reviewed source manifest. A new harness binary has
  // its own hash: equality of the old/new assembly hashes is neither assumed nor required.
  private static readonly string[] LifecycleComponents = ["NativeProofSupervisor.cs", "native-solver-wrapper.py",
    "NativeLifecycleControls.cs", "lifecycle-control-fixtures.json", "lifecycle-seal-probe.py", "native-lifecycle-fixture.c"];
  private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true, MaxDepth = 32 };
  [DllImport("libc", EntryPoint = "geteuid")] private static extern uint GetEffectiveUserId();

  // This is a fixed six-case API, not an arbitrary-command entry point. No argument
  // array, budgets, input source, seeds, backend or solver options come from its caller.
  public static int Run(ProofSmokeInputs input) {
    return RunQualifiedImplementation(input);
  }

  private static int RunQualifiedImplementation(ProofSmokeInputs input) {
    Require(Interlocked.CompareExchange(ref entered, 1, 0) == 0, "fixed-smoke-sequence-is-single-use-per-host");
    var cases = new List<ProofSmokeCaseReceipt>();
    ProductPin[] products = [];
    JsonElement? prerequisites = null;
    FrameworkWitness? framework = null;
    SharedCommonLibraries? shared = null;
    NativeSmokeQualification? qualification = null;
    NativeProofSession? session = null;
    string? failure = null;
    string? evidenceRoot = null;
    try {
      Require(OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64,
        "linux-x64-required");
      Require(GetEffectiveUserId() == 0, "explicit-privileged-ci-required");
      Require(Environment.ProcessorCount <= 8 && Framework.LiveChildren().Length == 0 && NoProductLoaded() && HostHasOnlyReviewedFramework(),
        "clean-shared-framework-host-required");
      Require(new[] { "LD_PRELOAD", "LD_LIBRARY_PATH", "LD_AUDIT" }.All(name => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name))),
        "unreviewed-native-loader-environment");
      var ownManifest = ReadJson(Path.Combine(AppContext.BaseDirectory, "source-manifest.json"), 131072);
      using (ownManifest) { ValidateOwnManifest(ownManifest.RootElement); }
      // Capture and independently inspect exact successful nonproof bytes before
      // any common library or private product can load. No approval flag exists.
      qualification = NativeSmokeQualification.Validate(input.NonproofQualification);
      var solver = Path.GetFullPath(input.Solver);
      var solverBytes = ReadBytes(solver, 268435456);
      Require(input.SolverSha256 == NativeElfOrigin.SolverSha256 && Sha256(solverBytes) == input.SolverSha256,
        "solver-pin-mismatch");
      var originInspection = NativeElfOrigin.Inspect(solverBytes);
      prerequisites = ValidatePrerequisites(input, originInspection);
      products = [Baseline(input), Candidate(input)];
      qualification.MatchProducts(products);
      Require(!string.Equals(products[0].Directory, products[1].Directory, StringComparison.Ordinal), "distinct-product-packages-required");
      foreach (var product in products) { CheckPackage(product); }
      var parent = Path.GetFullPath(input.CgroupParent).TrimEnd('/');
      Require(parent.StartsWith("/sys/fs/cgroup/", StringComparison.Ordinal) &&
        File.ReadAllText(Path.Combine(parent, "pids.max")).Trim() == "300", "exclusive-delegation-pids-300-required");
      // Establish all static fixture hashes before the first product can load.
      foreach (var fixture in Fixtures) {
        Require(Framework.Sha256(FixturePath(fixture.File)) == fixture.Sha256, "fixed-proof-fixture-mismatch");
      }
      var receiptParent = Path.GetDirectoryName(Path.GetFullPath(input.Receipt)) ?? throw new SmokeFailure("receipt-parent-required");
      Require(Directory.Exists(receiptParent) && !File.Exists(input.Receipt), "new-receipt-in-existing-directory-required");
      evidenceRoot = Path.Combine(receiptParent, "proof-smoke-evidence-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(evidenceRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
      framework = Framework.Witness();
      // A distinct scope: one exact common closure is persistent in Default. Every
      // Dafny run remains private and collectible; no Boogie static reset is attempted.
      // All prerequisite receipts, packages, solver/fixture pins were checked first.
      shared = SharedCommonLibraries.CreateAndPreload(products[0], products[1]);
      shared.AssertAcceptable();
      session = NativeProofSession.Create(qualification, products, shared, evidenceRoot);
      // Pair order is fixed. The same absolute source and unchanged host framework seed
      // are used by both product packages. Each preceding ALC must actually die first.
      foreach (var fixture in Fixtures) {
        foreach (var product in products) {
          var name = product.Label + "/" + fixture.Name;
          RunReceipt? run = null;
          NativeProofEvidenceReceipt? evidence = null;
          string? caseFailure = null;
          try {
            session.Boundary();
            Require(Framework.LiveChildren().Length == 0 && Framework.Witness() == framework, "prior-case-lifecycle-incomplete");
            CheckPackage(product); // No swapped package or dependency file between runs.
            var caseDirectory = Path.Combine(evidenceRoot, name.Replace('/', '-'));
            Directory.CreateDirectory(caseDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var json = Path.Combine(caseDirectory, "batches.json");
            var csv = Path.Combine(caseDirectory, "batches.csv");
            var supervisor = new NativeProofSupervisor(solver, input.SolverSha256, parent, caseDirectory);
            // These are explicit smoke budgets, not the later whole-suite default-cost
            // gate. The native translator, Boogie packages and outgoing SMT bytes remain
            // those of each pinned ordinary product. No backend/seed/solver overrides.
            var arguments = new[] { "verify", FixturePath(fixture.File), "--cores", "1",
              "--verification-time-limit", "20", "--resource-limit", "200000",
              "--solver-path", supervisor.WrapperPath, "--log-format", "json;LogFileName=" + json,
              "--log-format", "csv;LogFileName=" + csv };
            var permit = session.Admit(product, FixturePath(fixture.File), arguments, supervisor);
            run = SharedNativeRuns.RunVerification(product, shared, arguments, supervisor, TimeSpan.FromSeconds(60), permit);
            session.Boundary();
            Require(run.Failure == null && run.ContextCollected && run.RemainingDirectChildren.Length == 0,
              "native-run-cleanup-or-weak-collection-failed");
            Require(NoDefaultDafnyLoaded(), "post-collection-default-dafny-contamination");
            Require(run.SchedulerCleanup == "LargeThreadScheduler.Dispose called; actual collection checked after leaving the frame",
              "scheduler-disposal-unproven");
            Require(run.ExitCode == fixture.Exit, "unexpected-native-cli-verdict");
            Require(run.ProofCleanup is { RecordedSolverGroups: > 0, LiveOwnedProcesses: 0 }, "missing-owned-cleanup");
            ValidateLoader(product, run, shared);
            if (fixture.Exit == 4) {
              Require((run.Output + run.ErrorOutput).Contains("assertion might not hold", StringComparison.Ordinal),
                "reachable-false-diagnostic-missing");
            }
            evidence = NativeProofEvidence.Read(run, json, csv, FixturePath(fixture.File), fixture.Routine, fixture.Exit == 4);
            shared.AssertAcceptable();
            shared.Audit("after-native-evidence-" + name);
            session.AcceptCase(permit);
            Require(Framework.Witness() == framework && Framework.LiveChildren().Length == 0 && NoDefaultDafnyLoaded(),
              "post-evidence-lifecycle-failed");
          } catch (Exception exception) {
            caseFailure = Code(exception); session.Poison("fixed-native-case-failed");
          }
          cases.Add(new(name, product.Label, "proof-fixtures/" + fixture.File, fixture.Sha256, fixture.Exit,
            run, evidence, caseFailure == null, caseFailure));
          if (caseFailure != null) { failure = caseFailure; goto complete; }
        }
      }
    } catch (Exception exception) {
      failure = Code(exception); session?.Poison("fixed-native-preparation-or-sequence-failed");
    }
    complete:
    // Default common libraries deliberately remain loaded. Removing only our owned
    // resolver callback is audited; no product/framework state or handler surgery.
    SharedCommonReceipt? commonClosure = null;
    if (shared != null) {
      try {
        if (failure == null) { session!.Boundary(); }
        // Final audit, detached receipt and only our callback retirement share
        // the unchanged scope lock. A late captured callback still kills the host.
        commonClosure = shared.CloseAndSnapshot("fixed-sequence-complete");
      } catch (Exception exception) {
        failure ??= Code(exception);
        try { commonClosure = shared.Snapshot(); } catch { failure ??= "shared-final-snapshot-failed"; }
        finally { shared.Dispose(); }
      }
    }
    var expected = Fixtures.SelectMany(f => new[] { "baseline/" + f.Name, "candidate/" + f.Name }).ToArray();
    var passed = failure == null && session is { Complete: true } &&
      cases.Select(c => c.Name).SequenceEqual(expected) && cases.All(c => c.Passed) &&
      commonClosure is { CompleteMetadataInventory: true, CompleteMetadataAvailability: false } &&
      !commonClosure.RuntimeDemandState.Poisoned && commonClosure.RuntimeDemandState.Failures.Length == 0 &&
      commonClosure.RuntimeDemandState.UnavailableDemands.Length == 0 && commonClosure.DefaultAudits.Length == 28 &&
      commonClosure.RuntimeAssemblyLoads.All(load => load.Validated && load.Failure == null) &&
      AssemblyLoadContext.All.Count() == 1 && NoDefaultDafnyLoaded() && Framework.LiveChildren().Length == 0 &&
      framework == Framework.Witness();
    var receipt = JsonSerializer.SerializeToUtf8Bytes(new {
      schemaVersion = 2, scope = "prototype/six-fixed-qualified-shared-native-proof-smoke-controls", passed, failureCode = failure,
      sourceManifestSha256 = Framework.Sha256(Path.Combine(AppContext.BaseDirectory, "source-manifest.json")),
      harnessAssemblySha256 = Framework.Sha256(typeof(Program).Assembly.Location), effectiveUid = GetEffectiveUserId(),
      privilegeScope = "explicit-privileged-ci-no-unprivileged-deployment-claim", commonFramework = framework,
      culture = CultureInfo.CurrentCulture.Name, uiCulture = CultureInfo.CurrentUICulture.Name,
      workingDirectory = Environment.CurrentDirectory, visibleProcessors = Environment.ProcessorCount,
      baselineArchiveSha256 = BaselineArchiveSha256, baselineSource = BaselineSource,
      products, prerequisites, nonproofQualification = qualification?.Snapshot(), commonClosure, fixedCaseOrder = expected, runs = cases,
      solverSha256 = input.SolverSha256, solverVersion = "5.1.0",
      smokeBudgets = new { cores = 1, verificationTimeLimitSeconds = 20, resourceLimit = 200000, invocationSafetySeconds = 60 },
      ordinaryProofCliEnabled = false, nativeQueryOrResourceParityEstablished = false,
      persistentBoogieState = true, freshBoogieStateEstablished = false,
      requiredLaterControls = new[] { "repeat", "interleaved", "reversed-order" },
      evidenceRoot, onlyDefaultContextRemains = AssemblyLoadContext.All.Count() == 1, remainingDirectChildren = Framework.LiveChildren()
    }, Json);
    Require(receipt.Length <= 8388608, "smoke-receipt-bound-exceeded");
    // Do not erase an existing receipt on any failure, including preflight rejection.
    using (var file = new FileStream(input.Receipt, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(receipt); }
    return passed ? 0 : 1;
  }

  private static JsonElement ValidatePrerequisites(ProofSmokeInputs input, NativeElfOriginReceipt originInspection) {
    Require(input.LifecycleReceipt.Sha256 == LifecycleReceiptSha256 && input.LifecycleSourceManifest.Sha256 == LifecycleManifestSha256,
      "inspected-public-eleven-control-receipt-required");
    using var lifecycle = ReadPinned(input.LifecycleReceipt, 1048576);
    using var oldManifest = ReadPinned(input.LifecycleSourceManifest, 131072);
    using var ownManifest = ReadJson(Path.Combine(AppContext.BaseDirectory, "source-manifest.json"), 131072);
    var root = lifecycle.RootElement;
    Require(root.GetProperty("schemaVersion").GetInt32() == 1 && root.GetProperty("scope").GetString() == "prototype/native-lifecycle-controls" &&
      root.GetProperty("passed").GetBoolean() && root.GetProperty("failureCode").ValueKind == JsonValueKind.Null &&
      root.GetProperty("noProductLoaded").GetBoolean() && !root.GetProperty("invokesVerification").GetBoolean() &&
      root.GetProperty("effectiveUid").GetUInt32() == 0 && root.GetProperty("delegatedPidCeiling").GetInt32() == 300 &&
      root.GetProperty("solverSha256").GetString() == input.SolverSha256 &&
      root.GetProperty("sourceManifestSha256").GetString() == input.LifecycleSourceManifest.Sha256 &&
      root.GetProperty("harnessAssemblySha256").GetString() == LifecycleHarnessSha256, "complete-inspected-lifecycle-receipt-required");
    var controls = root.GetProperty("controls").EnumerateArray().ToArray();
    Require(controls.Length == 11 && controls.Select(c => c.GetProperty("name").GetString()).SequenceEqual(LifecycleNames), "eleven-lifecycle-controls-required");
    foreach (var control in controls) {
      var name = control.GetProperty("name").GetString();
      var preflight = name is "missing-delegation" or "wrong-solver-pin";
      var controlledFailure = name is "relay-broken-pipe" or "live-descendant-cap" or "malformed-completion";
      Require(control.GetProperty("passed").GetBoolean() && control.GetProperty("noProductLoaded").GetBoolean() &&
        control.GetProperty("zeroOwnedMembers").GetBoolean() && control.GetProperty("failureCode").ValueKind == JsonValueKind.Null &&
        control.GetProperty("ownedLeafRemoved").GetBoolean() == !preflight &&
        control.GetProperty("supervisorFailure").GetBoolean() == controlledFailure,
        "individual-lifecycle-control-incomplete");
      if (!preflight) { Require(Digest(control.GetProperty("cleanupSha256").GetString() ?? ""), "lifecycle-cleanup-hash-missing"); }
      Require(control.GetProperty("fallbackUsed").GetBoolean() == (name == "live-descendant-cap"), "unexpected-lifecycle-fallback");
    }
    var oldFiles = ManifestPins(oldManifest.RootElement);
    var ownFiles = ManifestPins(ownManifest.RootElement);
    Require(oldFiles.Count == 19, "exact-prior-nineteen-source-inventory-required");
    foreach (var (path, pin) in oldFiles) {
      Require(ownFiles.TryGetValue(path, out var current) && current == pin, "prior-lifecycle-source-file-changed");
    }
    var matched = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var component in LifecycleComponents) {
      Require(oldFiles.TryGetValue(component, out var oldPin) && ownFiles.TryGetValue(component, out var newPin) && oldPin == newPin,
        "lifecycle-implementation-changed-requalification-required");
      matched[component] = ownFiles[component].Sha256;
    }
    // Separate reviewed evidence is required because fd execution changes /proc/self/exe
    // and native $ORIGIN lookup. Inspect actual pinned bytes and bind the exact public
    // source analysis, rather than accept a standalone passed/approved boolean.
    // This is technical evidence for the fixed image/modes, not a signed compiler-origin
    // certificate or a theorem about every possible solver executable.
    using var origin = ReadPinned(input.SolverOriginEvidence, 65536);
    var accepted = origin.RootElement;
    Require(accepted.GetProperty("schemaVersion").GetInt32() == 1 &&
      accepted.GetProperty("scope").GetString() == "pinned-z3-executable-origin-technical-evidence" &&
      accepted.GetProperty("solverSha256").GetString() == input.SolverSha256 && accepted.GetProperty("solverVersion").GetString() == "5.1.0" &&
      accepted.GetProperty("sourceTag").GetString() == "z3-5.1.0" && accepted.GetProperty("sourceCommit").GetString() == NativeElfOrigin.Z3SourceCommit &&
      accepted.GetProperty("linkStrategy").GetString() == "direct-core-objects-no-libz3" &&
      accepted.GetProperty("inputStrategy").GetString() == "-in-selects-stdin-no-input-filename", "pinned-z3-origin-source-evidence-required");
    var sourceFiles = accepted.GetProperty("sourceFiles").EnumerateArray().ToArray();
    Require(sourceFiles.Length == NativeElfOrigin.SourceFiles.Length && sourceFiles.Select(f =>
      (f.GetProperty("path").GetString(), f.GetProperty("sha256").GetString())).SequenceEqual(
        NativeElfOrigin.SourceFiles.Select(f => ((string?)f.Path, (string?)f.Sha256))), "exact-public-z3-source-pins-required");
    var modes = accepted.GetProperty("permittedArguments").EnumerateArray()
      .Select(mode => mode.EnumerateArray().Select(value => value.GetString() ?? "").ToArray()).ToArray();
    Require(modes.Length == 2 && modes[0].SequenceEqual(new[] { "-version" }) && modes[1].SequenceEqual(new[] { "-smt2", "-in" }),
      "fixed-z3-launch-modes-required");
    var declaredImage = accepted.GetProperty("expectedActualImageInspection");
    Require(declaredImage.GetProperty("elfClass").GetInt32() == originInspection.ElfClass &&
      declaredImage.GetProperty("endianness").GetString() == originInspection.Endianness &&
      declaredImage.GetProperty("machine").GetInt32() == originInspection.Machine &&
      declaredImage.GetProperty("interpreter").GetString() == originInspection.Interpreter &&
      declaredImage.GetProperty("neededLibraries").EnumerateArray().Select(e => e.GetString()).SequenceEqual(originInspection.NeededLibraries) &&
      declaredImage.GetProperty("rpathRunpath").GetArrayLength() == originInspection.RpathRunpath.Length &&
      declaredImage.GetProperty("forbiddenOriginStringsFound").GetArrayLength() == originInspection.ForbiddenOriginStringsFound.Length &&
      declaredImage.GetProperty("forbiddenUndefinedSymbolsFound").GetArrayLength() == originInspection.ForbiddenUndefinedSymbolsFound.Length,
      "declared-and-actual-elf-inspection-mismatch");
    return JsonSerializer.SerializeToElement(new {
      lifecycleReceiptSha256 = input.LifecycleReceipt.Sha256,
      lifecycleSourceManifestSha256 = input.LifecycleSourceManifest.Sha256,
      priorLifecycleHarnessAssemblySha256 = root.GetProperty("harnessAssemblySha256").GetString(),
      priorLifecycleSourceFiles = oldFiles.Values.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray(),
      matchedComponents = matched, solverOriginEvidenceSha256 = input.SolverOriginEvidence.Sha256,
      solverOriginEvidence = accepted.Clone(), actualSolverImageInspection = originInspection,
      evidenceKind = "exact-image-and-public-source-inspection-not-signed-origin"
    }, Json);
  }

  internal static ProductPin[] MetadataControlProducts(ProofSmokeInputs input) {
    var products = new[] { Baseline(input), Candidate(input) };
    Require(products[0].Directory != products[1].Directory, "distinct-product-packages-required");
    foreach (var product in products) { CheckPackage(product); }
    return products;
  }

  private static ProductPin Baseline(ProofSmokeInputs input) {
    var archive = ReadBytes(input.BaselineArchive, 268435456);
    Require(Sha256(archive) == BaselineArchiveSha256, "original-public-baseline-archive-required");
    var files = new Dictionary<string, PackageFilePin>(StringComparer.Ordinal);
    using var compressed = new MemoryStream(archive, writable: false);
    using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
    using var tar = new TarReader(gzip);
    long total = 0;
    while (tar.GetNextEntry() is { } entry) {
      var name = entry.Name.TrimEnd('/');
      Require(name == "dafny" || name.StartsWith("dafny/", StringComparison.Ordinal), "baseline-archive-path-outside-package");
      if (entry.EntryType == TarEntryType.Directory) { continue; }
      Require(entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile, "baseline-archive-nonregular-entry");
      var relative = name["dafny/".Length..];
      Require(RelativeFile(relative) && entry.Length is >= 0 and <= 268435456 && files.Count < 4096,
        "baseline-archive-entry-bound");
      total = checked(total + entry.Length);
      Require(total <= 1073741824 && entry.DataStream != null, "baseline-archive-total-bound");
      var hash = Convert.ToHexString(SHA256.HashData(entry.DataStream!)).ToLowerInvariant();
      Require(files.TryAdd(relative, new(relative, hash, entry.Length)), "duplicate-baseline-archive-path");
    }
    Require(files.TryGetValue("DafnyCore.dll", out var core) && core.Sha256 == BaselineCoreSha256,
      "baseline-core-archive-pin-mismatch");
    return new("baseline", BaselineSource, Path.GetFullPath(input.BaselineDirectory), BaselineCoreSha256, BaselineCoreVersion,
      files.Values.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray());
  }

  private static ProductPin Candidate(ProofSmokeInputs input) {
    using var manifest = ReadPinned(input.CandidatePackageManifest, 1048576);
    var root = manifest.RootElement;
    Require(input.CandidateSourceCommit.Length == 40 && input.CandidateSourceCommit.All(Uri.IsHexDigit) &&
      root.GetProperty("schemaVersion").GetInt32() == 1 && root.GetProperty("sourceCommit").GetString() == input.CandidateSourceCommit &&
      root.GetProperty("coreInformationalVersion").GetString() == "4.11.0+" + input.CandidateSourceCommit,
      "exact-candidate-source-build-required");
    var files = root.GetProperty("files").EnumerateArray().Select(f => new PackageFilePin(
      f.GetProperty("path").GetString() ?? "", f.GetProperty("sha256").GetString() ?? "", f.GetProperty("bytes").GetInt64())).ToArray();
    Require(files.Length is > 0 and <= 4096 && files.All(f => RelativeFile(f.Path) && Digest(f.Sha256) && f.Bytes is >= 0 and <= 268435456) &&
      files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == files.Length, "candidate-package-inventory-invalid");
    var core = files.Single(f => f.Path == "DafnyCore.dll");
    Require(root.GetProperty("coreSha256").GetString() == core.Sha256, "candidate-core-inventory-mismatch");
    return new("candidate", input.CandidateSourceCommit, Path.GetFullPath(input.CandidateDirectory), core.Sha256,
      "4.11.0+" + input.CandidateSourceCommit, files);
  }

  internal static void CheckPackage(ProductPin pin) {
    Require(Directory.Exists(pin.Directory) && new DirectoryInfo(pin.Directory).LinkTarget == null,
      "existing-nonsymlink-product-package-required");
    var paths = new List<string>();
    var directories = new Stack<string>();
    directories.Push(pin.Directory);
    var visited = 0;
    while (directories.TryPop(out var directory)) {
      Require(++visited <= 4096, "product-package-directory-bound");
      foreach (var item in new DirectoryInfo(directory).EnumerateFileSystemInfos()) {
        Require(item.LinkTarget == null, "symlinked-product-package");
        if (item is DirectoryInfo child) { directories.Push(child.FullName); }
        else {
          Require(paths.Count < 4096, "product-package-file-bound");
          paths.Add(Path.GetRelativePath(pin.Directory, item.FullName).Replace(Path.DirectorySeparatorChar, '/'));
        }
      }
    }
    var actual = paths.Order(StringComparer.Ordinal).ToArray();
    Require(actual.SequenceEqual(pin.Files.Select(f => f.Path).Order(StringComparer.Ordinal)), "exact-package-file-inventory-required");
    foreach (var item in pin.Files) {
      var path = Path.Combine(pin.Directory, item.Path);
      for (FileSystemInfo? current = new FileInfo(path); current != null && current.FullName != pin.Directory;
           current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent) {
        Require(current.LinkTarget == null, "symlinked-product-package");
      }
      Require(new FileInfo(path).Length == item.Bytes && Framework.Sha256(path) == item.Sha256, "product-package-file-pin-mismatch");
    }
  }

  private static void ValidateLoader(ProductPin pin, RunReceipt run, SharedCommonLibraries shared) {
    var core = run.LoaderLedger.Single(e => e.Kind == "private-loaded" && e.Identity == CoreIdentity);
    Require(core.Sha256 == pin.CoreSha256 && core.InformationalVersion == pin.CoreInformationalVersion &&
      Path.GetFullPath(core.Path) == Path.Combine(pin.Directory, "DafnyCore.dll"), "private-core-identity-pin-mismatch");
    Require(run.LoaderLedger.Any(e => e.Kind == "shared-common" && e.Identity.StartsWith("Boogie.", StringComparison.Ordinal)) &&
      run.LoaderLedger.Any(e => e.Kind == "shared-common-loaded" && e.Identity.StartsWith("Boogie.Provers.SMTLib,", StringComparison.Ordinal)),
      "exact-common-native-boogie-prover-scope-missing");
    foreach (var entry in run.LoaderLedger.Where(e => e.Kind is "shared-common" or "shared-common-loaded" or "shared-tpa")) {
      shared.ValidateLedger(entry);
    }
    var expected = pin.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
    foreach (var entry in run.LoaderLedger.Where(e => e.Kind.StartsWith("private-", StringComparison.Ordinal) || e.Kind.StartsWith("resolver-", StringComparison.Ordinal))) {
      var relative = Path.GetRelativePath(pin.Directory, entry.Path).Replace(Path.DirectorySeparatorChar, '/');
      Require(expected.TryGetValue(relative, out var file) && file.Sha256 == entry.Sha256, "loaded-package-dependency-pin-mismatch");
    }
    Require(run.LoaderLedger.Where(e => e.Kind == "private-loaded").Select(e => e.Context).Distinct().Count() == 1,
      "private-product-context-not-unique");
  }

  private static void ValidateOwnManifest(JsonElement manifest) {
    Require(manifest.GetProperty("schemaVersion").GetInt32() == 1, "source-manifest-schema");
    foreach (var pin in ManifestPins(manifest).Values) {
      var path = Path.Combine(AppContext.BaseDirectory, pin.Path);
      // C# and documentation source files need not be copied beside the binary. The
      // reviewed manifest still binds them; executable/static data are present there.
      if (File.Exists(path)) { Require(new FileInfo(path).Length == pin.Bytes && Framework.Sha256(path) == pin.Sha256, "deployed-source-manifest-mismatch"); }
    }
    foreach (var required in new[] { "native-solver-wrapper.py", "proof-fixtures/true.dfy", "proof-fixtures/false.dfy", "proof-fixtures/fuel.dfy" }) {
      var pin = ManifestPins(manifest)[required];
      Require(File.Exists(Path.Combine(AppContext.BaseDirectory, required)) && Framework.Sha256(Path.Combine(AppContext.BaseDirectory, required)) == pin.Sha256,
        "deployed-required-fixture-missing");
    }
  }

  private static Dictionary<string, PackageFilePin> ManifestPins(JsonElement manifest) {
    return manifest.GetProperty("files").EnumerateArray().Select(f => new PackageFilePin(
      f.GetProperty("path").GetString() ?? "", f.GetProperty("sha256").GetString() ?? "", f.GetProperty("bytes").GetInt64()))
      .ToDictionary(f => f.Path, StringComparer.Ordinal);
  }
  private static bool NoProductLoaded() => !AssemblyLoadContext.Default.Assemblies.Any(a =>
    (a.GetName().Name ?? "").StartsWith("Dafny", StringComparison.OrdinalIgnoreCase) ||
    (a.GetName().Name ?? "").StartsWith("Boogie", StringComparison.OrdinalIgnoreCase));
  private static bool NoDefaultDafnyLoaded() => !AssemblyLoadContext.Default.Assemblies.Any(a =>
    (a.GetName().Name ?? "").StartsWith("Dafny", StringComparison.OrdinalIgnoreCase));
  private static bool HostHasOnlyReviewedFramework() {
    var trusted = Framework.TrustedPaths();
    return AssemblyLoadContext.Default.Assemblies.All(assembly => assembly == typeof(Program).Assembly ||
      (trusted.TryGetValue(assembly.GetName().Name ?? "", out var path) && Path.GetFullPath(assembly.Location) == path));
  }
  private static string FixturePath(string file) => Path.Combine(AppContext.BaseDirectory, "proof-fixtures", file);
  private static bool RelativeFile(string path) => !Path.IsPathRooted(path) && !path.Contains('\\') &&
    path.Split('/').All(part => part is not ("" or "." or ".."));
  internal static bool Digest(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
  internal static byte[] ReadBytes(string path, long bound) {
    using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    Require(file.Length <= bound && file.Length <= int.MaxValue, "evidence-file-bound");
    var bytes = new byte[(int)file.Length];
    file.ReadExactly(bytes);
    Require(file.ReadByte() == -1, "evidence-file-grew-during-read");
    return bytes;
  }
  internal static JsonDocument ReadJson(string path, long bound) => JsonDocument.Parse(ReadBytes(path, bound), new JsonDocumentOptions { MaxDepth = 64 });
  private static JsonDocument ReadPinned(PinnedEvidence pin, long bound) {
    var bytes = ReadBytes(pin.Path, bound);
    Require(Digest(pin.Sha256) && Sha256(bytes) == pin.Sha256, "prerequisite-evidence-pin-mismatch");
    return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
  }
  internal static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  internal static void Require(bool condition, string code) { if (!condition) { throw new SmokeFailure(code); } }
  private static string Code(Exception exception) => exception is SmokeFailure ? exception.Message : exception.GetType().Name;
  internal sealed class SmokeFailure(string code) : Exception(code);
}
