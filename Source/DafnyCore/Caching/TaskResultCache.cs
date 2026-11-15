using System;
using System.Text;
using System.Text.Json;

namespace Microsoft.Dafny.Caching;

internal enum CacheTaskOutcome {
  Correct, Errors, OutOfResource, Timeout, OutOfMemory, Cancelled, SolverException, Unknown
}

/// <summary>
/// The prepared-task producer supplies exact pruned input and complete environment bytes.
/// There is deliberately no ModuleKey in a result lookup: identical pruned tasks can survive
/// an imported-body edit even if the containing module was re-resolved and re-translated.
///
/// The task key covers the input and the environment with the effective resource limit
/// abstracted out of both (the producer replaces the limit-setting command and options by a
/// placeholder), plus the effective time limit. The resource limit is instead compared with
/// each stored result's own limit and resource use, under the rules of <see cref="TaskResultCache"/>.
/// </summary>
internal sealed class TaskCacheDescriptor {
  private readonly byte[] input;
  private readonly byte[] environment;
  public uint ResourceLimit { get; }
  public uint TimeLimit { get; }
  public string Key { get; }
  public bool IsResourceBounded => ResourceLimit > 0 && TimeLimit == 0;

  public TaskCacheDescriptor(ReadOnlySpan<byte> limitIndependentInput, ReadOnlySpan<byte> limitIndependentEnvironment,
    uint resourceLimit, uint timeLimit) {
    if (limitIndependentInput.IsEmpty || limitIndependentEnvironment.IsEmpty) {
      throw new ArgumentException("Prepared input and the complete execution environment must be explicit.");
    }
    input = limitIndependentInput.ToArray();
    environment = limitIndependentEnvironment.ToArray();
    ResourceLimit = resourceLimit;
    TimeLimit = timeLimit;
    Key = CacheDigest.Compute("prepared-task-result/v2", input, environment, UInt32(timeLimit));
  }

  internal static byte[] UInt32(uint value) {
    var bytes = new byte[4];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
    return bytes;
  }

  /// <summary>The index entry of one stored result of this task: one per limit-independent outcome
  /// class, and one per exact limit for results whose solver interaction reached that limit.</summary>
  internal string SlotKey(string slot, uint? exactResourceLimit = null) =>
    CacheDigest.Compute("prepared-task-result-slot/v2", Encoding.ASCII.GetBytes(Key), Encoding.ASCII.GetBytes(slot),
      exactResourceLimit is { } limit ? UInt32(limit) : []);

  internal byte[] Input => (byte[])input.Clone();
  internal byte[] Environment => (byte[])environment.Clone();
  internal bool Matches(byte[] taskInput, byte[] context) => taskInput != null && context != null &&
    input.AsSpan().SequenceEqual(taskInput) && environment.AsSpan().SequenceEqual(context);
}

/// <summary>
/// The codec must validate/rebind the complete current assertion, error and coverage inventory
/// before returning a result. Opaque payload bytes never directly become a completed proof.
/// </summary>
internal interface ITaskResultSnapshotLoader<T> where T : class {
  bool TryLoad(TaskCacheDescriptor descriptor, CacheTaskOutcome outcome, long resourceCount,
    byte[] reportData, out T result);
}

/// <summary>
/// Results under the limit-independent task key. With u the largest resource count the solver
/// reported during the task, L the resource limit the result was produced under and L' the
/// current one:
/// <list type="bullet">
/// <item>Valid is reused when L' = 0 (no limit) or L' &gt;= u. A Valid interaction is the same
/// under every such limit, so one record serves them all.</item>
/// <item>Errors whose interaction never reached the limit are reused when L' &gt;= u, under an RU
/// limit only. Errors from an interaction that reached the limit are reused only when L' = L.</item>
/// <item>OutOfResource at L is reused only when L' = L: under a smaller limit a fresh run would
/// stop sooner and report a different resource count.</item>
/// <item>Under a time limit only Valid is published or reused, and the time limit is in the key.</item>
/// </list>
/// The rules only admit a candidate. The replay transport then re-executes the task's adaptive
/// interaction against the recorded responses under the current limit; a different request,
/// outcome or resource count falls back to the solver.
/// </summary>
internal sealed class TaskResultCache {
  internal const int Schema = 2;
  internal const string ValidSlot = "valid";
  internal const string ErrorsSlot = "errors";
  internal const string ExactLimitSlot = "exact-limit";
  internal const string OutOfResourceSlot = "out-of-resource";
  private readonly ContentAddressedCache store;
  private sealed record Record(int Schema, string Slot, byte[] Input, byte[] Environment, uint ResourceLimit,
    uint TimeLimit, CacheTaskOutcome Outcome, long ResourceCount, long PeakResourceCount, byte[] ReportData);

  public TaskResultCache(ContentAddressedCache store) {
    this.store = store;
  }

  internal void Reject(string slotKey) => store.Quarantine(CacheTable.VerificationResult, slotKey);

  private static bool Reusable(CacheTaskOutcome outcome) =>
    outcome is CacheTaskOutcome.Correct or CacheTaskOutcome.Errors or CacheTaskOutcome.OutOfResource;

  /// <param name="peakResourceCount">The largest resource count the solver reported during the task.</param>
  /// <param name="reachedLimit">Whether any solver answer in the task reported an exhausted limit.</param>
  public CachePublication TryPublish(TaskCacheDescriptor descriptor, bool completedNormally, CacheTaskOutcome outcome,
    long resourceCount, long peakResourceCount, bool reachedLimit, ReadOnlySpan<byte> reportData) {
    if (!completedNormally || !Reusable(outcome) || resourceCount < 0 || peakResourceCount < resourceCount || reportData.IsEmpty) {
      return CachePublication.Unavailable;
    }
    string slot;
    string slotKey;
    uint storedLimit = 0;
    switch (outcome) {
      case CacheTaskOutcome.Correct when !reachedLimit:
        (slot, slotKey) = (ValidSlot, descriptor.SlotKey(ValidSlot));
        break;
      case CacheTaskOutcome.Errors when descriptor.IsResourceBounded && !reachedLimit:
        (slot, slotKey) = (ErrorsSlot, descriptor.SlotKey(ErrorsSlot));
        break;
      case CacheTaskOutcome.Errors when descriptor.IsResourceBounded:
        (slot, slotKey, storedLimit) = (ExactLimitSlot, descriptor.SlotKey(ExactLimitSlot, descriptor.ResourceLimit), descriptor.ResourceLimit);
        break;
      case CacheTaskOutcome.OutOfResource when descriptor.IsResourceBounded:
        (slot, slotKey, storedLimit) = (OutOfResourceSlot, descriptor.SlotKey(OutOfResourceSlot, descriptor.ResourceLimit), descriptor.ResourceLimit);
        break;
      default:
        return CachePublication.Unavailable;
    }
    var record = new Record(Schema, slot, descriptor.Input, descriptor.Environment, storedLimit,
      descriptor.TimeLimit, outcome, resourceCount, peakResourceCount, reportData.ToArray());
    return store.TryPublish(CacheTable.VerificationResult, slotKey, JsonSerializer.SerializeToUtf8Bytes(record));
  }

  public T TryRead<T>(TaskCacheDescriptor descriptor, ITaskResultSnapshotLoader<T> loader) where T : class =>
    TryRead(descriptor, loader, out _);

  public T TryRead<T>(TaskCacheDescriptor descriptor, ITaskResultSnapshotLoader<T> loader, out string slotKey) where T : class {
    var limit = descriptor.ResourceLimit;
    var candidates = descriptor.IsResourceBounded
      ? new[] {
        (ValidSlot, descriptor.SlotKey(ValidSlot)), (ErrorsSlot, descriptor.SlotKey(ErrorsSlot)),
        (ExactLimitSlot, descriptor.SlotKey(ExactLimitSlot, limit)), (OutOfResourceSlot, descriptor.SlotKey(OutOfResourceSlot, limit))
      }
      : new[] { (ValidSlot, descriptor.SlotKey(ValidSlot)) };
    foreach (var (slot, key) in candidates) {
      var record = Read(key);
      if (record == null || record.Schema != Schema || record.Slot != slot || !descriptor.Matches(record.Input, record.Environment) ||
          record.TimeLimit != descriptor.TimeLimit || record.ResourceCount < 0 || record.PeakResourceCount < record.ResourceCount ||
          record.ReportData is not { Length: > 0 }) {
        continue;
      }
      var admitted = slot switch {
        ValidSlot => record.Outcome == CacheTaskOutcome.Correct && record.ResourceLimit == 0 &&
                     (limit == 0 || limit >= record.PeakResourceCount),
        ErrorsSlot => record.Outcome == CacheTaskOutcome.Errors && record.ResourceLimit == 0 &&
                      descriptor.IsResourceBounded && limit >= record.PeakResourceCount,
        ExactLimitSlot => record.Outcome == CacheTaskOutcome.Errors && descriptor.IsResourceBounded && record.ResourceLimit == limit,
        OutOfResourceSlot => record.Outcome == CacheTaskOutcome.OutOfResource && descriptor.IsResourceBounded &&
                             record.ResourceLimit == limit,
        _ => false
      };
      if (admitted && loader.TryLoad(descriptor, record.Outcome, record.ResourceCount, record.ReportData, out var result)) {
        slotKey = key;
        return result;
      }
    }
    slotKey = null;
    return null;
  }

  private Record Read(string key) {
    var bytes = store.TryRead(CacheTable.VerificationResult, key);
    if (bytes == null) {
      return null;
    }
    try {
      return JsonSerializer.Deserialize<Record>(bytes);
    } catch (JsonException) {
      return null;
    }
  }
}
