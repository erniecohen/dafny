#nullable enable
using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Dafny;

namespace DafnyDriver.Commands;

internal enum CliVerificationDisposition { Disabled, CompleteEmpty, CompleteNonempty, Rejected }
internal enum CliModulePreparationState { NotStarted, Preparing, Completed, Failed }

/// <summary>Selection and configuration bound to a single owned continuation, not a solver observation.</summary>
internal sealed record CliVerificationSelection(
  B3OptionBag.Backend Backend, bool Verify, bool NoVerify, bool HiddenNoVerify,
  string? Worker, string? Solver, uint TimeLimit, uint ResourceLimit, int Cores, int ArithmeticSolver,
  string[] ProverOptions) {
  internal static CliVerificationSelection Capture(DafnyOptions options) => new(
    options.GetOrOptionDefault(B3OptionBag.VerificationBackend), options.Verify,
    options.Get(BoogieOptionBag.NoVerify), options.Get(BoogieOptionBag.HiddenNoVerify),
    options.Get(B3OptionBag.Worker)?.FullName, options.Get(BoogieOptionBag.SolverPath)?.FullName,
    options.TimeLimit, options.ResourceLimit, options.VcsCores,
    options.GetOrOptionDefault(BoogieOptionBag.ArithmeticSolver), options.ProverOptions.ToArray());

  internal bool Matches(DafnyOptions options) {
    var current = Capture(options);
    return Backend == current.Backend && Verify == current.Verify && NoVerify == current.NoVerify &&
      HiddenNoVerify == current.HiddenNoVerify && Worker == current.Worker && Solver == current.Solver &&
      TimeLimit == current.TimeLimit && ResourceLimit == current.ResourceLimit && Cores == current.Cores &&
      ArithmeticSolver == current.ArithmeticSolver && ProverOptions.SequenceEqual(current.ProverOptions);
  }
}

/// <summary>
/// Operational continuation authority for one resolved Program. This is neither an end-to-end
/// soundness proof nor a certificate for trusted dependencies or explicitly excluded declarations.
/// </summary>
internal sealed class CliVerificationReceipt {
  private readonly Microsoft.Dafny.Program? program;
  private readonly DafnyOptions options;
  private readonly CliVerificationSelection selection;
  internal CliVerificationDisposition Disposition { get; }
  internal int ModuleCount { get; }
  internal long UnitCount { get; }
  internal string InventoryHash { get; }
  internal string? Reason { get; }
  internal bool VerificationAttempted => Disposition is CliVerificationDisposition.CompleteEmpty or
    CliVerificationDisposition.CompleteNonempty;

  internal CliVerificationReceipt(Microsoft.Dafny.Program? program, DafnyOptions options,
    CliVerificationSelection selection, CliVerificationDisposition disposition, int moduleCount,
    long unitCount, string inventoryHash, string? reason) {
    this.program = program;
    this.options = options;
    this.selection = selection;
    Disposition = disposition;
    ModuleCount = moduleCount;
    UnitCount = unitCount;
    InventoryHash = inventoryHash;
    Reason = reason;
  }

  internal bool Authorizes(Microsoft.Dafny.Program current, CancellationToken cancellationToken) =>
    !cancellationToken.IsCancellationRequested && ReferenceEquals(program, current) &&
    ReferenceEquals(options, current.Options) && selection.Matches(options) &&
    Disposition is CliVerificationDisposition.Disabled or CliVerificationDisposition.CompleteEmpty or
      CliVerificationDisposition.CompleteNonempty;
}

/// <summary>
/// Owned preparation/result ledger. Preparation completes before parts are enumerated; an empty
/// result sequence cannot manufacture a missing module inventory or successful resolution.
/// </summary>
internal sealed class CliVerificationLedger {
  private sealed class ModuleInventory {
    internal CliModulePreparationState State;
    internal bool Released;
    internal readonly Dictionary<VerificationIdentity, UnitInventory> Units = new();
  }
  private sealed record UnitInventory(IVerificationWorkItem Task, VerificationIdentity Identity, ICanVerify Owner) {
    internal bool Completed;
  }
  private readonly Microsoft.Dafny.Program program;
  private readonly DafnyOptions options;
  private readonly CliVerificationSelection selection;
  private readonly HashSet<ICanVerify> scope;
  private readonly Dictionary<ModuleDefinition, ModuleInventory> modules;
  private readonly HashSet<VerificationIdentity> identities = new();
  private readonly IncrementalHash inventoryHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  private string? rejection;
  private long unitCount;
  private bool sealedLedger;

  internal CliVerificationLedger(Microsoft.Dafny.Program program, IEnumerable<ModuleDefinition> modules,
    IEnumerable<ICanVerify> scope) {
    this.program = program;
    options = program.Options;
    selection = CliVerificationSelection.Capture(options);
    this.scope = new HashSet<ICanVerify>(scope, System.Collections.Generic.ReferenceEqualityComparer.Instance);
    this.modules = new Dictionary<ModuleDefinition, ModuleInventory>(
      System.Collections.Generic.ReferenceEqualityComparer.Instance);
    foreach (var module in modules) {
      if (!this.modules.TryAdd(module, new ModuleInventory())) {
        Reject("A module occurs twice in the current verification scope");
      }
    }
    if (this.scope.Any(owner => !this.modules.ContainsKey(owner.ContainingModule))) {
      Reject("An eligible verification owner has no admitted module");
    }
    if (selection.Backend != B3OptionBag.Backend.B3 || !selection.Verify || selection.NoVerify || selection.HiddenNoVerify) {
      Reject("Enabled B3 verification requires its current explicit selection and verification policy");
    }
  }

  internal void BeginPreparation(ModuleDefinition module) {
    EnsureOpen();
    if (!modules.TryGetValue(module, out var inventory) || inventory.State != CliModulePreparationState.NotStarted) {
      Reject("A module preparation is missing, repeated or outside the current scope");
      return;
    }
    inventory.State = CliModulePreparationState.Preparing;
  }

  internal bool CompletePreparation(ModuleDefinition module, IReadOnlyList<IVerificationWorkItem> tasks) {
    EnsureOpen();
    if (!modules.TryGetValue(module, out var inventory) || inventory.State != CliModulePreparationState.Preparing) {
      Reject("A completed module has no matching preparation");
      return false;
    }
    foreach (var task in tasks) {
      if (task == null || task.Identity == null || string.IsNullOrEmpty(task.Identity.Key) ||
          string.IsNullOrEmpty(task.Identity.ScopeId) || !scope.Contains(task.Source.CanVerify) ||
          !ReferenceEquals(task.Source.CanVerify.ContainingModule, module) ||
          !identities.Add(task.Identity) || inventory.Units.ContainsKey(task.Identity)) {
        Reject("A prepared checking unit has a duplicate identity, unknown owner or out-of-scope module");
        inventory.State = CliModulePreparationState.Failed;
        return false;
      }
      inventory.Units.Add(task.Identity, new UnitInventory(task, task.Identity, task.Source.CanVerify));
      unitCount = checked(unitCount + 1);
      Append(task.Identity.ScopeId);
      Append(task.Identity.Key);
      Append(task.Identity.BatchId.ToString(System.Globalization.CultureInfo.InvariantCulture));
      Append(task.Identity.RandomSeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
      Append(task.Source.CanVerify.FullDafnyName);
    }
    inventory.State = CliModulePreparationState.Completed;
    return rejection == null;
  }

  internal void FailedPreparation(ModuleDefinition module, string reason) {
    EnsureOpen();
    if (modules.TryGetValue(module, out var inventory)) { inventory.State = CliModulePreparationState.Failed; }
    Reject(reason);
  }

  internal void CompleteUnit(ModuleDefinition module, IVerificationWorkItem task, VerificationResult result) {
    EnsureOpen();
    if (!modules.TryGetValue(module, out var inventory) || inventory.State != CliModulePreparationState.Completed ||
        !inventory.Units.TryGetValue(task.Identity, out var expected) || !ReferenceEquals(expected.Task, task) ||
        !ReferenceEquals(expected.Owner, task.Source.CanVerify) || expected.Identity != task.Identity || expected.Completed) {
      Reject("A terminal checking unit is missing, repeated or differs from its prepared identity");
      return;
    }
    expected.Completed = true;
    if (!result.IsVerified) {
      Reject("A checking unit did not finish with a complete verified result: " + result.Outcome);
    }
  }

  internal void ReleaseModule(ModuleDefinition module) {
    EnsureOpen();
    if (!modules.TryGetValue(module, out var inventory) || inventory.State != CliModulePreparationState.Completed ||
        inventory.Released || inventory.Units.Values.Any(unit => !unit.Completed ||
          unit.Identity != unit.Task.Identity || !ReferenceEquals(unit.Owner, unit.Task.Source.CanVerify))) {
      Reject("A module's complete prepared/result inventory was not released intact");
      return;
    }
    inventory.Released = true;
    // Keep only the finite identity ledger and digest/count; release normalized task/AST closures.
    inventory.Units.Clear();
  }

  internal void Reject(string reason) { rejection ??= reason; }

  internal CliVerificationReceipt Seal(bool diagnosticsAccepted, CancellationToken cancellationToken) {
    EnsureOpen();
    sealedLedger = true;
    if (cancellationToken.IsCancellationRequested) { Reject("Verification was cancelled"); }
    if (!diagnosticsAccepted) { Reject("The compilation has fatal diagnostics"); }
    if (!selection.Matches(options)) { Reject("The verification selection changed during compilation"); }
    if (modules.Values.Any(m => m.State != CliModulePreparationState.Completed || !m.Released)) {
      Reject("Not every admitted module and checking unit completed");
    }
    var digest = Convert.ToHexString(inventoryHash.GetHashAndReset()).ToLowerInvariant();
    inventoryHash.Dispose();
    var disposition = rejection != null ? CliVerificationDisposition.Rejected : unitCount == 0
      ? CliVerificationDisposition.CompleteEmpty : CliVerificationDisposition.CompleteNonempty;
    return new CliVerificationReceipt(program, options, selection, disposition, modules.Count, unitCount, digest,
      rejection ?? (unitCount == 0 ? "Successful current-scope admission/preparation produced no eligible checking units" : null));
  }

  internal static CliVerificationReceipt Disabled(Microsoft.Dafny.Program program) {
    var selection = CliVerificationSelection.Capture(program.Options);
    var allowed = selection.Backend == B3OptionBag.Backend.B3 && selection.NoVerify && !selection.Verify && !selection.HiddenNoVerify;
    return new CliVerificationReceipt(program, program.Options, selection, allowed
      ? CliVerificationDisposition.Disabled : CliVerificationDisposition.Rejected, 0, 0, "",
      allowed ? "Ordinary --no-verify; verification was not attempted" : "Skipped verification lacks ordinary --no-verify provenance");
  }

  internal static CliVerificationReceipt Rejected(Microsoft.Dafny.Program? program, DafnyOptions options, string reason) =>
    new(program, options, CliVerificationSelection.Capture(options), CliVerificationDisposition.Rejected, 0, 0, "", reason);

  private void EnsureOpen() {
    if (sealedLedger) { throw new InvalidOperationException("The verification ledger is already sealed"); }
  }
  private void Append(string value) {
    Span<byte> length = stackalloc byte[sizeof(long)];
    BinaryPrimitives.WriteInt64LittleEndian(length, Encoding.UTF8.GetByteCount(value));
    inventoryHash.AppendData(length);
    Span<byte> bytes = stackalloc byte[1024];
    var remaining = value.AsSpan();
    var encoder = Encoding.UTF8.GetEncoder();
    bool complete;
    do {
      encoder.Convert(remaining, bytes, true, out var usedChars, out var usedBytes, out complete);
      inventoryHash.AppendData(bytes[..usedBytes]);
      remaining = remaining[usedChars..];
    } while (!complete);
  }
}
