using System;
using System.Text.Json;

namespace Microsoft.Dafny.Caching;

internal enum CacheTaskOutcome {
  Correct, Errors, OutOfResource, Timeout, OutOfMemory, Cancelled, SolverException, Unknown
}

/// <summary>
/// The prepared-task producer supplies exact pruned input and complete environment bytes.
/// There is deliberately no ModuleKey in a result lookup: identical pruned tasks can survive
/// an imported-body edit even if the containing module was re-resolved and re-translated.
/// </summary>
internal sealed class TaskCacheDescriptor {
  private readonly byte[] input;
  private readonly byte[] environment;
  public uint ResourceLimit { get; }
  public uint TimeLimit { get; }
  public string Key { get; }
  public bool IsResourceBounded => ResourceLimit > 0 && TimeLimit == 0;

  public TaskCacheDescriptor(ReadOnlySpan<byte> exactPrunedInput, ReadOnlySpan<byte> completeEnvironment,
    uint resourceLimit, uint timeLimit) {
    if (exactPrunedInput.IsEmpty || completeEnvironment.IsEmpty) {
      throw new ArgumentException("Prepared input and the complete execution environment must be explicit.");
    }
    input = exactPrunedInput.ToArray();
    environment = completeEnvironment.ToArray();
    ResourceLimit = resourceLimit;
    TimeLimit = timeLimit;
    Span<byte> limits = stackalloc byte[8];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(limits, resourceLimit);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(limits[4..], timeLimit);
    Key = CacheDigest.Compute("prepared-task-result/v1", input, environment, limits.ToArray());
  }

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

internal sealed class TaskResultCache {
  private readonly ContentAddressedCache store;
  private sealed record Record(int Schema, byte[] Input, byte[] Environment, uint ResourceLimit,
    uint TimeLimit, CacheTaskOutcome Outcome, long ResourceCount, byte[] ReportData);

  public TaskResultCache(ContentAddressedCache store) {
    this.store = store;
  }

  internal void Reject(TaskCacheDescriptor descriptor) => store.Quarantine(CacheTable.VerificationResult, descriptor.Key);

  private static bool Reusable(CacheTaskOutcome outcome) =>
    outcome is CacheTaskOutcome.Correct or CacheTaskOutcome.Errors or CacheTaskOutcome.OutOfResource;

  public CachePublication TryPublish(TaskCacheDescriptor descriptor, bool completedNormally,
    CacheTaskOutcome outcome, long resourceCount, ReadOnlySpan<byte> reportData) {
    if (!completedNormally || !(outcome == CacheTaskOutcome.Correct || descriptor.IsResourceBounded) || !Reusable(outcome) || resourceCount < 0 || reportData.IsEmpty) {
      return CachePublication.Unavailable;
    }
    var record = new Record(1, descriptor.Input, descriptor.Environment, descriptor.ResourceLimit,
      descriptor.TimeLimit, outcome, resourceCount, reportData.ToArray());
    return store.TryPublish(CacheTable.VerificationResult, descriptor.Key, JsonSerializer.SerializeToUtf8Bytes(record));
  }

  public T TryRead<T>(TaskCacheDescriptor descriptor, ITaskResultSnapshotLoader<T> loader) where T : class {
    var bytes = store.TryRead(CacheTable.VerificationResult, descriptor.Key);
    if (bytes == null) {
      return null;
    }
    Record record;
    try {
      record = JsonSerializer.Deserialize<Record>(bytes);
    } catch (JsonException) {
      return null;
    }
    if (record == null || record.Schema != 1 || !descriptor.Matches(record.Input, record.Environment) ||
        record.ResourceLimit != descriptor.ResourceLimit || record.TimeLimit != descriptor.TimeLimit ||
        (!descriptor.IsResourceBounded && record.Outcome != CacheTaskOutcome.Correct) ||
        !Reusable(record.Outcome) || record.ResourceCount < 0 || record.ReportData is not { Length: > 0 }) {
      return null;
    }
    return loader.TryLoad(descriptor, record.Outcome, record.ResourceCount, record.ReportData, out var result)
      ? result : null;
  }
}
