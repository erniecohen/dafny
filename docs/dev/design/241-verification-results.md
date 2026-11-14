# Persistent verification-task result cache

This standalone opt-in change implements `--verification-cache DIR` against the
shipped translator. It does not enable parallel resolution or change generated
names, and has no dependency on the translation table or its default-on naming
prerequisite. The owner decision is
https://github.com/erniecohen/dafny/pull/262#issuecomment-6826006467.
The persistence rules follow
https://github.com/erniecohen/dafny/pull/272 and
https://github.com/erniecohen/dafny/issues/261#issuecomment-6808221963.

## Key and scope

A lookup identifies one prepared manual split and seed iteration. Its key is a
versioned length-delimited SHA-256 over the exact ordered initial solver commands,
the complete effective prover/solver options and adaptive response-program identity,
ordered UsedTypes inputs, and the solver/runtime/environment identity. Prelude,
pruning, assertion isolation, normalization, split options, elapsed-time limits,
random seeds and raw Boogie options contribute through those frozen inputs. The
effective resource limit does not: the last `:rlimit` command that sets it, and the
options that carry it, are replaced by a placeholder before hashing
(https://github.com/erniecohen/dafny/issues/261).
No module key or file hash substitutes for the prepared task input. Editing one
function recomputes precisely tasks whose complete prepared input changes; a
translation naming change may legitimately change additional task inputs.

The payload stores the exact descriptor, completed outcome and RU, and complete
subsequent command/response exchange. Current Boogie reconstructs models,
counterexamples, error and coverage data against current assertions. Each record also
stores the largest resource count u the solver reported and the limit L it depends
on. Under a current limit L' (0 for none), Valid is reused when L' = 0 or L' >= u;
errors when L' >= u, or only when L' = L if the solver reported an exhausted limit
during the task; OutOfResource at L only when L' = L, since under a smaller limit a fresh
run stops sooner and reports a smaller count. The task key has one index slot for valid,
one for errors, and one per L for OutOfResource and for errors that reached the limit. Errors and OutOfResource require a
positive effective RU bound and zero effective elapsed-time limit. Timed failures,
crashes, cancellation, memory failures, unknowns and incomplete handshakes never
publish. Unknown option types and unsupported side effects bypass.

## Storage, publication and concurrency

`DIR/v1/results/xx/<task-key>/data` holds a bounded lookup record with schema,
table, key, payload SHA-256 and length. Payloads reside at
`DIR/v1/blobs/sha256/xx/<payload-hash>/data`; validated solver-version probes use
`DIR/v1/solver-identities/xx/<key>/data`. The schema, key domains and transport ABI
are versioned independently; incompatible formats miss. A reserved translation
table in the generic store is unused by this feature.

Publish payloads before lookup records. A writer creates a uniquely named private
temporary directory beside the destination, writes and flushes `data` to disk,
then renames the directory atomically. A nonempty published directory cannot be
replaced by another writer. Readers see a complete object or a miss. Concurrent
writers compare already-published bytes; conflicting values quarantine the key
under `DIR/v1/disabled/<table>/xx/<key>`. Readers check quarantine both before and
after reading. Lookup validates schema, table, exact key, bounded lengths, payload
hash, descriptor and replay commands. Any corruption, I/O failure or mismatch
means recompute. An interrupted publication may leave an unreferenced blob or
private temporary directory; neither is a result. Atomic visibility does not
promise filesystem durability across every possible power failure; missing or
invalid objects recompute. Only mutually trusted local writers may share DIR.

There is no automatic disk eviction in v1. Delete `DIR/v1` to evict the namespace;
stop writers before selective mark/sweep collection. In-memory transcripts are
bounded; oversized or unsupported tasks flush to a native solver and stop caching.

## Independence and equivalence

With the option absent, no session, transport, storage or new timing schema is
constructed. Translation, task preparation, the shipped solver factory and default
reporting remain on their existing paths. The disabled branch must reproduce the
shipped product's Boogie, verdicts and RU exactly.

Enabled cold and warm use identical independent checker/prover lifecycles. Replay
is accepted only if every actual adaptive operation matches the stored exchange,
including terminal synchronization. Current Boogie determines the outcome and
binds reports, and the wrapper verifies exact stored outcome and RU before
exposing completion. Divergence discards the entire candidate and retries the
original split/seed with persistent reads disabled. No partial replay may report a
result. Warm must reproduce enabled cold stdout/stderr/exit, outcomes, RU and all
untimed report fields, with zero native solver/version-probe calls for unchanged
supported tasks. Replayed timing fields identify current replay cost.

Resolution, global validation and translation always run for this result-only
feature. It neither accepts nor substitutes a persistent resolution result.
A future `--resolution-cache` can feed the normal preparation pipeline, but the
result lookup still validates actual solver input and complete execution identity.
No translator or resolver invariance claim is inferred from a result-cache hit.

The integration regression checks native fresh-process replay, all per-task
outcomes/RU and counterexample reports, a one-function edit against an independent
edited cold oracle, concurrent publication, corruption, replay divergence and
quarantine, and effective time/RU overrides. The option inventory is a drift gate.
Five fresh processes per cold/warm/edit measurement report wall/CPU, solver calls
and RU with source/toolchain pins; historical stacked measurements are not evidence
for this independently based revision. No PR is merged by this work.

### Exact proposed Boogie patch (not implemented)

Against Boogie tag `v3.5.5+review.c5a9f2ef`, add the following public API in
`Source/ExecutionEngine/PreparedTaskCache.cs` and the nullable engine property
shown below. `ImmutableArray<byte>` values are defensive copies made by Boogie;
the external store never receives a mutable Boogie graph. `VC.VerificationRunResult`
and the existing `SolverOutcome` remain the reporting types.

```csharp
namespace Microsoft.Boogie;

public sealed record PreparedTaskDescriptor(
  int FormatVersion,
  System.Collections.Immutable.ImmutableArray<byte> InitialSolverCommands,
  System.Collections.Immutable.ImmutableArray<byte> AdaptiveResponseProgram,
  System.Collections.Immutable.ImmutableArray<byte> OrderedUsedTypesInputs,
  System.Collections.Immutable.ImmutableArray<byte> EffectiveOptions,
  System.Collections.Immutable.ImmutableArray<byte> SolverIdentity,
  System.Collections.Immutable.ImmutableArray<byte> BindingInventory,
  bool Reusable,
  string? BypassReason);

public sealed record PreparedTaskResult(
  int FormatVersion,
  System.Collections.Immutable.ImmutableArray<byte> Descriptor,
  System.Collections.Immutable.ImmutableArray<byte> ReportPayload);

public interface IPreparedTaskCache {
  System.Threading.Tasks.ValueTask<PreparedTaskResult?> LookupAsync(
    PreparedTaskDescriptor descriptor,
    System.Threading.CancellationToken cancellationToken);
  System.Threading.Tasks.ValueTask PublishAsync(
    PreparedTaskDescriptor descriptor,
    PreparedTaskResult result,
    System.Threading.CancellationToken cancellationToken);
}

public interface IPreparedVerificationTask : System.IAsyncDisposable {
  PreparedTaskDescriptor Descriptor { get; }
  // Null rejects the entire candidate without emitting partial status/report data.
  System.Threading.Tasks.ValueTask<VC.VerificationRunResult?> TryReplayAsync(
    PreparedTaskResult result,
    System.Threading.CancellationToken cancellationToken);
  System.Threading.Tasks.ValueTask<VC.VerificationRunResult> RunAsync(
    System.Threading.CancellationToken cancellationToken);
  // Null for timed failures, crash, cancellation or incomplete report. Valid may be timed.
  PreparedTaskResult? EncodeCompletedResult(VC.VerificationRunResult result);
}

// Add to IVerificationTask; implement in VerificationTask, after FromSeed:
// ValueTask<IPreparedVerificationTask> PrepareAsync(CancellationToken token);
// Add to ExecutionEngine, default null:
// public IPreparedTaskCache? PreparedTaskCache { get; set; }
```

`PrepareAsync` freezes one manual split and seed iteration before obtaining a
checker or constructing a process-backed prover. The descriptor's solver input is
the exact initial ordered command stream after pruning, normalization, VC generation
and all effective settings; options include adaptive counterexample-query policy.
`SolverIdentity` contains executable SHA-256, validated version, arguments, dialect
and execution dependencies. Capture these bytes before lookup, with bounded buffers.
The binding inventory identifies current assertions, coverage labels, path/model
variables and source/report ABI. Descriptor serialization is versioned and canonical.

`TryReplayAsync` validates the **entire** descriptor and bounded Boogie-owned report
codec, then binds counterexamples, models and coverage to this task's graph. It
reconstructs `VerificationRunResult` using current assertions and current execution
timestamps, retaining stored outcome/RU. It returns null on any mismatch, with no
observable partial replay. `RunAsync` then executes the frozen plan with a fresh
physical solver; replay divergence must never continue a partially replayed run.
Only completed reproducible outcomes can be encoded, after report extraction.
The modern `TryRun` lifecycle calls these methods when the engine property is
non-null; with null it follows the existing path exactly. Lookup/publication I/O
errors are misses; cancellation propagates. Existing consumers need not adopt the
new entry point. The patch also needs zero-process-start, replay-negative-control
and lifecycle tests in Boogie before Dafny PR B can depend on it.

This API remains a proposed Boogie patch; neither PR implements it in Boogie.
PR B instead uses the existing `CommandLineOptions.CreateSolver` transport hook
and the explicit lifecycle/retry boundary described below. It must pass the same
completeness and differential gates; publishing a draft is not acceptance.

CLI filters select today's task inventory. Each task independently hits or runs;
all selected tasks contribute to the current aggregate exit code. Initially bypass
persistent reads for `--filter-position-batch` and unsupported side effects or
callbacks. Solver logs, trace/timing output, model/split files and verification
plugins either have tested replay support or bypass. CSV/JSON per-task outcomes
and RU are identical on cold and warm runs; actual execution timestamps and
durations describe the current invocation. Default stdout, stderr and exit code
must be byte-identical between cache-enabled cold and warm runs.

## PR B transport implementation and acceptance boundary

`TaskCacheSession` installs a solver factory only under `--verification-cache`.
It forces `/restartProver`, so every manual split/seed iteration has an independent
physical solver history. `CachedTaskSolver` buffers setup without creating Z3.
Immediately before the first `(check-sat)` it freezes the exact setup stream and
the complete pinned `SMTLibOptions`, inherited `CoreOptions`/`PrintOptions`, and
`SMTLibSolverOptions` values. Interface properties and public solver fields are
enumerated, with unsupported runtime values causing a miss. `UsedTypes` is not
inferred from pruned declarations: its ordered lowered type inputs are encoded
separately. Output writers bind to the current report; only the pinned factory
and installed transport are admitted. Solver logs, external preambles, plugins,
inspectors, batch transport, repeated filter batches, adaptive timeout diagnostics,
keep-going subdivisions and unsupported loader environments bypass.
Solver transport verbosity and displayed prover warnings also bypass: replay
cannot omit their observable transport events. With `PrintProverWarnings=None`,
the pinned `VerificationResultCollector` inherits a warning callback that does
nothing. A message is ignored for admission only if every line is classified as
a warning by pinned `HandleProverError` (case-insensitive `WARNING: `); the cold
callback still runs. Such ignored events affect neither reports nor the adaptive
program. Genuine errors, mixed warning/error messages and parser failures still
reject publication. Unit controls and native Std pattern-warning tasks exercise
this distinction.

The environment descriptor includes an ABI identifier and actual bytes of all
application/runtime managed assemblies, the native solver and host, hostfxr,
runtime native libraries and resolved native dependencies. The macOS profile
also hashes the actual OS dyld shared-cache files; Linux resolves dependencies
with `ldd` and records actual mapped libraries, including dlopen-only ICU/OpenSSL
inputs. An unrecorded later Linux native load bypasses. V1 admits only the exact
native executables from the public Z3 5.1.0 macOS arm64 and Linux x64 releases
used by the fork's CI. Both archive checksums were validated before recording
their executable digests; arbitrary native wrappers cannot establish a complete
dependency profile merely by reporting that version. Other solver builds,
native launcher scripts and Windows bypass. All environment
variable values are represented by digests. Image digests are memoized only within
an invocation that treats code files as immutable; file length/mtime changes
disable reuse for that invocation rather than certifying new files against old
loaded code. The initial native dependency enumeration is conservative and can
add appreciable cold and warm startup cost. A validated solver-version record
avoids even a version-probe process on a supported warm run. Unknown dependency
resolution runs normally instead of accepting a version string as identity.

The fixed response program is identified by these implementation bytes and its
complete option state before the first query. The stored exchange includes all
subsequent sends, requests, parsed S-expressions and synchronization operations.
It is never used as a response-dependent substitute for the descriptor. Replay
checks each current command against that exchange, while pinned Boogie builds
counterexamples, assertion bindings and coverage using the current task. The
wrapper withholds `Completed` until the task observable closes and any pending
final idle handshake finishes. The pinned checker can retire after a 100 ms
cleanup deadline while that handshake's continuation is still pending; observable
completion alone therefore does not establish that the exchange is sealed.
Restarting the native solver alone also leaves Boogie setup buffers in the
checker. To make task ownership independent of the preceding task's retirement
timing, each completed cached task retires its checker after the terminal
handshake, using the pinned pool's existing `TimeoutException` cleanup path.
Both cold and replay therefore start the next task with a fresh Boogie prover
as well as an independent native solver history. This cleanup exception is not
a verification timeout and is consumed by the existing pool protocol.
Whole-task retries disable persistent lookup while retaining the same exclusive
checker lifecycle, so retrying one divergent exchange does not change the next
task's setup stream.
Divergence discards all buffered completion data and retries
`FromSeed` with persistent reads disabled and a fresh solver. Divergent keys are
quarantined persistently, so a later invocation recomputes without replaying the
same rejected candidate. The pinned checker's idle cleanup retires failed slots
through its `TimeoutException` path; the cache wrapper retains a separate mismatch
flag and retries the entire task, rather than reporting that cleanup as a proof
timeout. A result is published
only after normal completion and exact outcome/RU agreement. The terminal comment
and subsequent idle handshake seal the exchange before a checker can be reused.
Retiring a checker does not discard a successful handshake, including one whose
continuation finishes after retirement. A failed or absent final handshake cannot
publish a result. Retirement timing does not decide the proof outcome: only the
completed Valid or eligible RU-bounded outcome can be reused. The default
verification path never constructs this transport or waits on this extra barrier.

`SmtExecutionLimits` lexes the actual initial command stream, respecting comments,
strings and quoted symbols. Last effective `:timeout` and `:rlimit` commands win,
including raw prover-option overrides. Only an explicit inventory of scalar
Boolean/numeric SMT options is admitted. Unknown controls, string-valued options,
external logging and parallel/deadline controls bypass rather than silently
skipping their side effects on a hit.
Top-level initialization commands also have an explicit inventory. Solver-side
`include`/load commands and unknown forms require an input-content manifest and
therefore bypass V1. Raw option or logic strings cannot introduce such commands
while retaining result-cache admission.
Completed Valid is eligible under any elapsed-time limit that equals its own and any
resource limit that covers its use; Errors/OutOfResource additionally require
positive RU and zero timeout. Timeout, memory failure, cancellation,
protocol error and unknown outcomes are never reused. Retention is bounded by
100,000 steps/nodes, response depth 128 and 32 MiB of retained command/response
characters; oversized setup is flushed to a real solver and caching stops.
Payload and record limits are 64 MiB and 4 KiB. No automatic disk eviction is
implemented: delete the namespace, or stop writers before selective collection.

JSON marks replayed task/scope timing with `timingReplayed: true`; the opt-in CSV
schema adds `TimingReplayed`. These durations measure current replay/reporting
work, not fresh solver execution. Cold/warm equality excludes timing fields and
these provenance markers. Stdout, stderr, exit code, task outcomes, RU, assertion
inventory and report contents remain the differential obligations. The default
CSV schema and ordering are unchanged. The opt-in CSV sorts by task name so
replay latency cannot reorder rows.
The JSON logger retains asynchronous split completion order. Its complete split
rows are compared by VC number/seed, preserving every field and multiplicity;
row order is not a task identity.

The total modern CLI and inherited prover/solver input inventory is recorded in
[241-option-audit.tsv](241-option-audit.tsv). The driver unit drift gate compares
all names, aliases and runtime types and rejects unclassified additions/removals.
Input-stage options have already run before the frozen SMT boundary; residual
execution parameters are additionally encoded through the complete interfaces.
Report/selection/setup stages execute against the current compilation rather than
a stored rendered report. Legacy `--boogie` settings either flow into those
interfaces/actual commands or select a bypassed external profile; the opt-in
transport is available only on the modern verify command.

## Stable numeric solver inputs

The enabled session selects the existing Boogie deterministic numeric content-hash
path before preparing a task, alongside its existing fresh-checker policy. The
[dependency-local default translator](https://github.com/erniecohen/dafny/pull/285)
also selects deterministic numeric hashes. An explicit legacy numeric-hash setting
can reintroduce CLR-process-seed ordering; the enabled session supersedes that
setting, like its independent solver-history policy. The option-off path preserves
both the shipped default and explicit overrides. This uses the pinned Boogie
implementation and changes no Boogie API. The effective value is included in each
task execution key.

The result cache composes with `--translation-cache`: a reused module snapshot
supplies the current compilation's Boogie graph and proof map, then its prepared
tasks pass through the result cache. Solver-version probe counts are telemetry,
so the translation descriptor excludes them. The combined regression requires a
warm invocation to reuse both tables, start no solver and reproduce uncached
outcomes, RU, stdout, stderr and exit.


The public `--verification-cache` option requires `--type-system-refresh`.
After CLI/project option binding, legacy-type selection is rejected with a clear
option error and exit 2, before parsing, resolution, solver setup or cache I/O.
It does not silently bypass the cache. An ordinary option-off legacy invocation
retains its shipped behavior. Native cache qualification selects the refreshed
type system explicitly; Std project profiles already select it.

The admission revision passes the Release build, all 492 Core tests and native
CLI tests for implicit legacy typing, explicit false, project false and CLI
overrides of a project true value. Invalid source deliberately confirms rejection
precedes parsing; no profile or cache directory is created. An ordinary legacy
option-off request remains accepted. Native regression/cutoff controls explicitly
select refreshed typing and retain zero warm solver calls, exact report replay,
corruption/race/limit controls and the 0/4/1/1 imported-edit call counts. A focused
passive native-writer comparison preserves option-off Boogie, task commands,
outcomes/RU, reports/stdout/stderr/exit against the prior producer under both type
systems. These focused checks supplement the frozen broader cohorts; they do not
relabel them as runs of the new managed-image namespace.


### Refreshed CLI admission measurements

Product source `470f93a90fc27d1e73b338fa8aa20d81d282993b` repeats five
fresh-process samples per scenario on the two-module, three-task regression
program with `--type-system-refresh`. Median [minimum, maximum], seconds:

| Scenario | Wall | CPU | Solver calls | Reported RU |
|---|---|---|---:|---:|
| Cold | 10.445 [5.960, 15.350] | 8.482 [5.251, 9.251] | 3 | 7068 |
| Warm | 8.608 [5.715, 13.322] | 6.535 [5.072, 8.765] | 0 | 7068 |
| One-function edit | 9.242 [6.169, 17.796] | 7.049 [5.405, 11.913] | 1 | 7189 |

Cold has one solver-version probe; warm/edit have none. All five warm pairs
replay exact stdout/stderr/exit and untimed reports/RU. Each edit reruns exactly
one changed key and replays two. Exit 4 includes the intentional reachable-false
control. Other work was concurrent; wall/CPU ranges are descriptive, not isolated
latency claims. The [measurement summary](241-result-cache-refresh-measurements.json)
records product identity and binary hashes. The earlier full-Std measurements
retain their original producer; their project already selects refreshed typing.

## Native process history control

The [fixed-input causal replay](241-native-process-history.md) establishes why
logical reset is insufficient for reproducible per-task reuse: the same native
task gives 20,558 or 20,534 RU after different prior histories, each repeated five
times, while fresh processes give 20,536. No translation difference is involved.
The enabled cache already isolates reusable tasks with fresh native solving;
option-off retains the shipped pooled lifecycle. No new default restart policy
or Boogie lifecycle change is introduced by this qualification.
