#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Dafny;

/// <summary>A module's prepared work is checked before refinement or request filtering.</summary>
public sealed class VerificationWorkManifest {
  public ModuleDefinition Module { get; }
  public IReadOnlyDictionary<VerificationWorkId, IVerificationWork> Expected { get; }

  private VerificationWorkManifest(ModuleDefinition module, Dictionary<VerificationWorkId, IVerificationWork> identities) {
    Module = module;
    Expected = new ReadOnlyDictionary<VerificationWorkId, IVerificationWork>(identities);
  }

  public static VerificationWorkManifest Check(ModuleDefinition module, IReadOnlyList<IVerificationWork> work) {
    var identities = new Dictionary<VerificationWorkId, IVerificationWork>();
    foreach (var task in work) {
      var source = task.Source;
      if (source.Id == null || string.IsNullOrEmpty(source.Id.Scope) || !identities.TryAdd(source.Id, task)) {
        throw new InvalidOperationException("Module verification work has an invalid or duplicate identity");
      }
      if (source.CanVerify == null || !ReferenceEquals(source.CanVerify.ContainingModule, module)) {
        throw new InvalidOperationException("Module verification work has a missing or foreign source owner");
      }
    }
    return new(module, identities);
  }

  public VerificationRun StartRun(ICanVerify owner, IEnumerable<IVerificationWork> selected) {
    var run = new VerificationRun(owner, selected) { Manifest = this };
    foreach (var task in run.Work) {
      if (!Expected.TryGetValue(task.Source.Id, out var original) || !ReferenceEquals(original.Source.CanVerify, owner)) {
        throw new InvalidOperationException("Verification request contains work outside its module manifest");
      }
    }
    return run;
  }
}

public record VerificationWorkFailure(VerificationOutcome Outcome, Exception Exception);
public enum VerificationCompletionKind { StreamClosed, CachedResult }

/// <summary>
/// One selected, seeded request. Identity sets, rather than counts, determine completion.
/// Result publication retains backend timing. Audited success additionally requires clean closure
/// or delivery of an already completed cached result for every selected identity.
/// </summary>
public sealed class VerificationRun {
  private readonly object gate = new();
  private readonly Dictionary<VerificationWorkId, IVerificationWork> expected;
  private readonly Dictionary<VerificationWorkId, VerificationWorkResult> completed = new();
  private readonly Dictionary<VerificationWorkId, VerificationWorkFailure> failures = new();
  private readonly Dictionary<VerificationWorkId, VerificationCompletionKind> closures = new();
  private readonly TaskCompletionSource auditCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
  public ICanVerify Owner { get; }
  public VerificationWorkManifest? Manifest { get; internal init; }
  public IReadOnlyList<IVerificationWork> Work { get; }
  public int ExpectedCount => expected.Count;
  public int CompletedCount { get { lock (gate) { return completed.Count; } } }
  public bool IsComplete { get { lock (gate) { return expected.Keys.All(id => completed.ContainsKey(id) || failures.ContainsKey(id)); } } }
  public bool IsClosed { get { lock (gate) { return closures.Count == expected.Count; } } }
  public bool IsVerified { get { lock (gate) { return IsClosed && failures.Count == 0 && completed.Count == expected.Count && completed.Values.All(r => r.IsVerified); } } }
  /// <summary>Settles when every identity has closed, supplied a cached result, or failed. Inspect IsVerified for success.</summary>
  public Task AuditCompletion => auditCompletion.Task;
  public IReadOnlyDictionary<VerificationWorkId, VerificationCompletionKind> Closures {
    get { lock (gate) { return new Dictionary<VerificationWorkId, VerificationCompletionKind>(closures); } }
  }
  public IReadOnlyDictionary<VerificationWorkId, VerificationWorkResult> Results {
    get { lock (gate) { return new Dictionary<VerificationWorkId, VerificationWorkResult>(completed); } }
  }
  public IReadOnlyDictionary<VerificationWorkId, VerificationWorkFailure> Failures {
    get { lock (gate) { return new Dictionary<VerificationWorkId, VerificationWorkFailure>(failures); } }
  }

  public VerificationRun(ICanVerify owner, IEnumerable<IVerificationWork> work) {
    Owner = owner;
    Work = Array.AsReadOnly(work.ToArray());
    expected = new();
    foreach (var task in Work) {
      if (!ReferenceEquals(task.Source.CanVerify, owner) || !expected.TryAdd(task.Source.Id, task)) {
        throw new InvalidOperationException("Verification request has foreign or duplicate work");
      }
    }
    FinishAudit();
  }

  private VerificationWorkId Check(IVerificationWork work) {
    var id = work.Source.Id;
    if (!ReferenceEquals(work.Source.CanVerify, Owner) || !expected.TryGetValue(id, out var task) || !ReferenceEquals(task, work)) {
      throw new InvalidOperationException("Verification response does not belong to this request");
    }
    return id;
  }

  public bool HasResult(IVerificationWork work, VerificationWorkResult result) {
    lock (gate) {
      return expected.TryGetValue(work.Source.Id, out var task) && ReferenceEquals(task, work) &&
        completed.TryGetValue(work.Source.Id, out var recorded) && ReferenceEquals(recorded, result);
    }
  }

  internal void Complete(IVerificationWork work, VerificationWorkResult result) {
    lock (gate) {
      var id = Check(work);
      if (result == null || !Enum.IsDefined(result.Outcome) || failures.ContainsKey(id) || !completed.TryAdd(id, result)) {
        throw new InvalidOperationException("Verification request received an invalid or repeated result");
      }
    }
  }

  internal void Fail(IVerificationWork work, Exception error) {
    lock (gate) {
      var id = expected.Single(pair => ReferenceEquals(pair.Value, work)).Key;
      failures.TryAdd(id, new(error is OperationCanceledException ? VerificationOutcome.Cancelled : VerificationOutcome.ToolError, error));
      FinishAudit();
    }
  }

  internal void Close(IVerificationWork work, VerificationCompletionKind kind) {
    lock (gate) {
      var id = Check(work);
      if (!Enum.IsDefined(kind) || !completed.ContainsKey(id) || failures.ContainsKey(id) || !closures.TryAdd(id, kind)) {
        throw new InvalidOperationException("Verification work closed without a unique result delivery");
      }
      FinishAudit();
    }
  }

  private void FinishAudit() {
    if (expected.Keys.All(id => closures.ContainsKey(id) || failures.ContainsKey(id))) {
      auditCompletion.TrySetResult();
    }
  }
}
