# Content-addressed translation and verification results

This is the implementation design for the cache portion of
https://github.com/erniecohen/dafny/issues/241. It builds on
[the pipeline study](241-pipeline.md), especially sections 3, 5 and 6, and the
front-end design in https://github.com/erniecohen/dafny/pull/258.

## Scope and prerequisites

PR A exposes **`--translation-cache`**, a Boolean opt-in for a bounded,
process-local translation table. It enables the sibling's `--resolution-threads 1`
when no worker count was selected, thereby using its module-qualified names and
`ModuleDefinition.ResolutionKey` (called `ModuleKey` below). Repeated batch
requests in one invocation can hit; separate CLI processes cannot. The table
does not cache verification results or skip current whole-program admission.
PR A enables no on-disk cache. The accepted design of record is
https://github.com/erniecohen/dafny/pull/272, with the binding decisions at
https://github.com/erniecohen/dafny/issues/261#issuecomment-6808221963.
Its order is module ownership, persistent task results, then persistent resolution;
on-disk Boogie reuse still requires the materializer differential test.
PR B exposes `--verification-cache DIR` for persistent per-task solver exchanges.
It does not enable the process-local translation table automatically.
Without the translation option the existing translation, preparation,
solver lifecycle and reporting paths execute without constructing a cache,
hashing inputs, changing names, or opening cache files.

**Implementation boundary:** the process-local table owns full pre-preparation
Boogie graph snapshots and exact emitted text, addressed by SHA-256. A graph
copy preserves reference topology, definition-axiom owners and Boolean literal
aliases; it allocates fresh Boogie node IDs. Unsupported runtime types cause a
miss. This is not an executable deserializer. The snapshot retains source tokens,
proof descriptions and proof-dependency reporting anchors, guarded by exact source
bindings; task attribution is separately rebound to current `ICanVerify` objects.
Text is emitted with token rewriting disabled. Any future persisted representation
needs an explicit source/report codec instead of these process-local references.
Printing process-specific AST identities (`PrintWithUniqueASTIds`) bypasses the
table: fresh graph copies necessarily have different identities, so their debug
text cannot share a stable content hash.

The resolution worker on
https://github.com/erniecohen/dafny/tree/codex/241-parallel-resolution owns the
semantic module key, module-scoped generated names and resolved-module format.
The cache consumes its `ModuleKey`; it does not compute a competing source/import
hash. The key covers refinement bases, nested modules, export views, builtins,
rewriter effects and every other semantic resolution prerequisite, as well as
the module's source, identity, resolver and options. Reuse `CachingResolver`,
its existing resolution dependency graph and `ModuleResolutionResult`; replace
process-local `CloneId` with content identity under the option, rather than
introducing another resolver cache. A textual import list is
insufficient. Unknown dependencies disable reuse.

`ModuleKey` also needs a translation-capability receipt. A resolved snapshot may
be sufficient for typechecking but insufficient for translation: visibility,
reveal/fuel information, system declarations and generated names remain inputs.
The translator contract is:

```text
Translate(ModuleKey, qualified signature selector, admitted resolved module,
          compiler environment, source/report bindings) -> TranslationSnapshot
```

The snapshot must be a deterministic function of this complete descriptor.
The owner key alone does not distinguish every generated abstract signature in
the current interface. Whole-program name,
replacement and cardinality admission run on every transaction before a snapshot
can authorize verification. A receipt from a previous program is never accepted.

## Keys and versioning

Use SHA-256, with a format-specific domain, a component count and an unsigned
64-bit big-endian byte length before each component. Hash bytes, not a runtime
object hash, concatenated strings, a debug printer, or additive checksums. Digest
strings are exactly 64 lowercase hexadecimal characters. Every codec change
bumps its format domain and namespace; unknown versions are misses.

### Translation table

The lookup descriptor contains the semantic `ModuleKey`, the selected resolved
module's fully qualified name, the translation ABI,
the loaded translator and Boogie module identities, the exact captured prelude,
and effective translation options, including ordered pass-through options.
These are the compiler-environment component of the module key, not a substitute
for its semantic dependencies. The value identifies the exact Boogie bytes by
hash and a versioned metadata sidecar by hash.

One resolution result can contain several generated abstract signatures. The
sibling currently gives those signatures their owner's key. Their qualified
names distinguish the selected translation within that keyed result; otherwise
the nested abstract imports in Std's parser builders collide despite emitting
different declarations. This is a translation selector, not another semantic
resolution key.

In-process namespace version `translation-in-process-v2` length-delimits its
UTF-8 strings with `BinaryWriter` (the future disk codec uses the framing above).
It uses the loaded assemblies' MVIDs; no assembly can change within that table's
lifetime. Source filenames, coordinates and the original text of files in the
translation closure are additional binding inputs: the sibling's semantic key
deliberately excludes trivia and source locations, whereas BPL `captureState`
strings and reporting retain them. This is conservative: an edit to a shared
source file can invalidate several otherwise unchanged modules. Reporting-only
invocation timestamps and print destinations are excluded.

Every snapshot header reserves nullable `ObservableInterfaceHash`. PR A leaves
it null: the current sibling interface supplies a full resolution key, not an
observable-interface key. It never treats that full key as an interface hash.
Later resolution early cutoff can fill this slot without changing translation or
result-table ownership. PR B's task key remains independent of both header hashes.

The snapshot includes all state needed to load an equivalent fresh Boogie graph:
source origins and ranges, assertion and proof-dependency mappings, definition
axiom ownership, identity/aliasing information with observable meaning, and any
translator diagnostics or state delta. The textual Boogie object is useful for
inspection and `--boogie-print`; **reparsing a `.bpl` dump alone is not an accepted
snapshot loader**. It can lose identity-sensitive literal behavior, dependency
ownership and Dafny origin data. Do not change `/structuralLiterals` to conceal
that difference. The loader must establish fidelity before the translation
lookup is enabled, and must load a fresh mutable graph per consumer.

On a warm resolution and translation hit an unchanged module is neither
re-resolved nor re-translated. With only translation caching enabled, resolution
still runs. A failed/unsupported resolved-snapshot load also resolves normally;
its fresh `ModuleKey` can still authorize a translation hit. A translation hit
returns a distinct module-owned `ProofDependencyManager` together with its graph.
`ModuleVerificationTasks` carries that manager through preparation, `Compilation`,
`BoogieUpdate` and `CanVerifyResult`; loggers and coverage use this module owner.
Neither cold translation nor a hit unions module maps into the program manager. The
in-process version retains reporting-only source anchors under exact bindings;
verification task attribution must reference current symbols. A missing or ambiguous
current-symbol binding discards the candidate before tasks or diagnostics escape.
The disk version must reconstruct all source/report bindings explicitly.

History-free naming is required in both resolvers and in translation. In the
current source, inspect `BoogieGenerator.CurrentIdGenerator`, imported function
translation with `codeContext = f` in `BoogieGenerator.Methods.cs`,
`ProofDependencyManager.GetProofDependencyId`, member `AssignUniqueName` calls,
`SystemModuleManager.Bitwidths`, system declaration discovery and visible-module
iteration in `BoogieGenerator.DoTranslation`. Resetting one translator's local
counter does not isolate imported declarations or program-wide dependency ids.
The cache worker does not modify these sites concurrently with their owner.

### Per-task result table

The unit is one prepared manual split and seed iteration, including all solver
queries needed to discover its counterexamples. A filename, whole module hash,
Boogie `impl.Id`, or a pretty-printed list of pruned declarations is not its key.

Construct the descriptor after pruning, normalization, type erasure, naming,
VC generation and effective per-declaration option selection. It contains:

- The exact ordered initial solver command stream, including background,
  prelude-derived axioms, declarations, VC and solver options. Preserve order,
  whitespace and names; do not sort or alpha-normalize for a hit.
- The actual solver executable digest and validated version, executable
  arguments, solver factory/dialect, runtime/platform and relevant execution
  dependencies. Unsupported plugins, mutable executables and unknown dependencies
  bypass. A version string alone cannot establish solver identity.
- All effective and ordered raw result-affecting options: RU limit, initial and
  derived seeds, `/normalizeDeclarationOrder`, prune mode, isolation, splitting,
  error limits, arithmetic/encoding settings, `--boogie` and solver options.
  Referenced preamble/configuration files contribute their actual captured bytes.
- Protocol and result codec versions and the report-map ABI. Do not hash all
  source files into each task: unrelated bodies must not invalidate unchanged
  pruned tasks. Location-dependent solver names are retained as actual inputs;
  report data is rebound against the current split's stable assertion map.

The descriptor is immutable before lookup. A preliminary module lookup can locate
candidates, but cannot authorize a result hit in place of computing the current
pruned descriptor. A result entry must include the descriptor bytes, not merely
repeat its own digest, and lookup compares those bytes with the current descriptor.

### Reproducible execution and records

Both cache-enabled cold and warm executions use a fresh independent solver
history for each task. Skipping a hit must not change a miss's solver history.
This opt-in protocol, including restart behavior, is part of the key. Default
execution is unchanged. A completed `Valid` result is reusable even under a
nonzero time limit. Errors and out-of-resource results require a positive effective
RU limit and zero effective elapsed-time limit, including declaration and prover
overrides. Enforce eligibility after overrides and normal report completion.

Cache `Correct`, `Errors` and RU-bounded `OutOfResource`. Never reuse wall-clock
timeouts, memory exhaustion, cancellation, crashes, protocol errors or unknown
outcomes. Publish only after the entire task and all required reporting payloads
have completed. An observed `unsat` is insufficient if coverage/model extraction
subsequently fails. Results contain:

```text
descriptor and full key; schema/protocol/toolchain/solver identity
current-task binding inventory and assertion/covered-element identifiers
outcome; exact RU; original RU limit and seed
counterexample models, error/path information and related source locations
coverage, warnings and diagnostics needed by all supported report modes
payload digests and producer provenance
replay provenance, kept separate from current invocation timing observations
```

Reconstruct errors and coverage through the same current reporting code as fresh
results. Validate every binding before publishing a completed status. A mismatch
causes a fresh task execution; it cannot become an empty counterexample set or
an empty covered-element set. New invalid program diagnostics remain authoritative.

The pinned Boogie modern API exposes `IVerificationTask`, but the complete SMT
input is assembled later in `Split.BeginCheck` and
`SMTLibInteractiveTheoremProver.Check`. The complete-result codec/lookup boundary
therefore needs either a narrow Boogie prepared-task API or a rigorously isolated
solver-transport implementation. A transport implementation must buffer setup
before starting the solver, record the entire adaptive request/response exchange,
validate replay commands, and invalidate/restart the task on any divergence.
It must also expose task completion, outcome, RU and cancellation to publication.
A cache of independent `check-sat` responses is not the per-task table.
Do not use the legacy implementation-result cache as a shortcut.

The recommended prerequisite is a narrow modern Boogie API, default null:

1. `PrepareTask` freezes the ordered pruned command plan, assertion/report map,
   derived options and solver protocol **without starting a physical solver**.
   Today `VerificationTask.StartRun` obtains a checker before `Split.BeginCheck`
   builds the VC, and the interactive prover constructor starts its process.
   Merely wrapping `IVerificationTask.TryRun` cannot meet zero solver calls.
2. The store compares the complete current descriptor and supplies only bounded
   immutable data. A Boogie-owned loader binds it into the current split's
   assertions, reporter and model view. There is no arbitrary object-graph
   deserialization or stale AST token reuse.
3. A miss executes the frozen plan with a fresh solver. A completion callback
   runs after model/coverage extraction and successful finalization, associates
   the outcome and RU with that same descriptor, and then authorizes publication.
   Cancellation or failure before this boundary cannot publish.
4. Binding or protocol divergence discards the candidate and restarts the entire
   task with fresh task/report state and no persistent reads, before forwarding
   a completed status or partial cached diagnostics. This retry belongs at the
   modern task lifecycle boundary, not in an independent `check-sat` memoizer.

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

## File layout, atomic publication and concurrency

PR A publishes a complete snapshot into its process-local table under one lock;
concurrent callers cannot observe a partial record. Processes share no table or
files. A failed copy or hash mismatch evicts the entry and translates normally.
It uses LRU eviction, bounded by 256 entries and 512 MiB of retained text. This
is not a strict managed-heap bound: graph and source/report anchors also consume
memory. `Clear` discards the table. No persistent-format claim follows from this
in-process implementation. PR B implements the disk store for results and solver
version observations; the translation namespace remains reserved.

All paths below are relative to the user-selected directory:

```text
v1/
  blobs/sha256/ab/abcdef.../data      # immutable payload bytes
  translations/ab/abcdef.../data    # JSON: lookup key -> payload hash, length, codec
  results/ab/abcdef.../data         # JSON: lookup key -> payload hash, length, codec
  solver-identities/ab/abcdef.../data # validated version, keyed by image/environment
  disabled/translations/ab/abcdef.../data # persistent conflicting-key marker
  disabled/results/ab/abcdef.../data # persistent conflicting-key marker
```

The two tables share the object store, but never their key domains. Payloads may
be a bounded manifest referencing further hashed objects. Absolute paths and
paths obtained from a record are never followed; derive paths only from validated
digests and a fixed table enumeration. Readers bound file sizes before allocation,
validate schema, table, key, length, digest and the typed payload, and reject
trailing or missing payload data. JSON is data, never executable deserialization.

Write payloads first. Write `data` in a uniquely named temporary directory
beside the destination, flush bytes and close, then atomically rename the
nonempty directory. An existing nonempty directory cannot be replaced by a
portable directory rename. Do not assume a file-move API implements an atomic
no-replace operation on every filesystem: a check followed by ordinary rename
has a race. Publish the lookup record last by the same directory operation. Readers see a complete
record or a miss. A crash can leave an orphaned blob or a temporary file; neither
authorizes reuse. Only a writer removes its own temporary files.

Concurrent identical writers need no shared mutable index or process lock.
If the destination already exists, read and validate it. Identical content is
success. A malformed existing record or a different payload for the same key is
not overwritten: publish a persistent disabled-key marker and recompute. Readers
check the marker before and after loading. The conflict is available in separate,
explicit cache telemetry; normal report output is unchanged. Conflicting outcome
or RU under one descriptor is a determinism failure, never last-writer-wins.

Missing directories, permissions, partial records, eviction races, malformed
JSON, digest mismatches, incompatible versions, failed bindings and unknown
eligibility mean cache misses. Cache I/O failure cannot fail verification or
turn an unverified task into a verified task. Out-of-memory and cancellation are
not swallowed as ordinary I/O failures. A content digest detects accidental
corruption; it does not authenticate a forged result. Directories require trusted
writers. Never share writable caches between untrusted PR jobs and trusted builds.

## Eviction and resource bounds

Limit individual records and payloads before allocation, and bound retained
in-memory descriptors independently of disk storage. No unbounded process-wide
AST or result dictionary is needed. The initial store has no automatic global
LRU index. Users may remove the entire `v1` namespace to evict it; concurrent
reads/writes either finish with fully checked immutable data or miss. To reclaim
a fixed amount, stop cache writers and delete old table records, then unreferenced
blobs and abandoned temporary files. Disabled-key markers are removed only by
explicit namespace eviction, so automatic age-based removal cannot resurrect a
known conflict. Filesystem quotas can provide a strict disk bound; writes past
the quota become misses. A later bounded background eviction policy must preserve
these invariants and must not race in-flight writers by deleting their objects.

## Equivalence argument

Assume collision resistance, trusted record producers and a deterministic
resource-bounded solver protocol. Whole-program admission precedes either table.
The translation descriptor covers every translation-observable prerequisite; history-free
translation and the faithful snapshot loader establish the same fresh Boogie
artifact and report state on a hit as on translation. No assertion or axiom is
added by reuse. The result key describes the exact pruned task and execution
environment; completion and binding checks establish the same outcome, RU,
counterexamples and coverage. Replacing that computation with its stored result
therefore preserves the ordered completed task stream. Each miss has the same
independent solver history with or without other hits. Reporting the current
complete task inventory preserves diagnostics and the exit code.

This is a conditional argument until history independence, snapshot fidelity,
descriptor completeness and result replay have executable differential tests.
A stored success is not a proof certificate and does not strengthen trust in
the baseline solver. Unsupported cases recompute through the existing path.

## Delivery and acceptance

PR A includes this design, opt-in history-free translation, the translation
table, faithful snapshot loading, CLI/help/reference documentation, a release
note, focused tests, `.github/review/base` and its regression-list entry.
It consumes the resolution worker's key/naming interface after coordination.
PR B adds the complete per-task result table and its own product/review metadata.
Both target `review/4.11.0` and remain unmerged for the coordinator.

Required gates, with exact source identities and complete outputs retained:

1. Option-off suite and standard-library verdicts, RU and raw output match the
   base; options do not cause default hashing, naming or solver-lifecycle changes.
2. The same module, including imports, translated alone, after other modules, in
   the full program, and with an unrelated module added emits byte-identical BPL
   under the option. Compare actual bytes, not renumbered output. Include lambdas,
   reveal/fuel, both resolvers, refinements, exports, bitvectors and dependencies.
3. Cold and warm translation snapshots produce identical solver-input descriptors,
   task inventory, diagnostics, exit, per-task outcomes and RU. Count actual
   resolution and translation invocations, including restarted or bypassed work.
4. Cold and warm result runs have byte-identical stdout/stderr and equal exits,
   identical per-task outcome/RU rows, and zero solver checks for unchanged eligible
   tasks. Cover valid, invalid and RU-exhausted tasks, warning/coverage modes,
   multiple counterexamples, filters, derived seeds and current source rebinding.
5. A single function-body edit reruns exactly tasks with changed pruned descriptors.
   Compare descriptor sets and count real solver calls. A whole-source checksum
   cannot pass. Also edit an imported function in A whose definition B's task T
   does not reach after pruning: re-resolve and re-translate B, then reuse T and
   every other unchanged pruned task with zero solver calls. If B's entire Boogie
   stays identical despite its changed resolution key, all its eligible tasks hit.
   Key results independently of `ModuleKey` to enable this early cutoff. Change
   prelude, solver bytes/version, RU, seed, normalization,
   prune/split/isolation and ordered `--boogie` options as invalidation controls.
6. Truncate and corrupt records and blobs; use incompatible codecs; evict during
   lookup; race identical and conflicting writers in separate processes. Every
   uncertain case misses. Cancellation/crash/timeout/unknown never publishes.
7. For both a multi-module program and the standard library, collect at least five
   cold runs, five unchanged warm runs and five warm runs after the same one-body
   edit, on the requested 18-core configuration. Report wall time, CPU and actual
   solver calls as medians and ranges. Include both a leaf-module edit and a widely
   imported module edit, reporting each dependent module's hit/rerun counts. Keep
   outcomes/RU alongside timing data.
   Unsupported/bypassed work and solver version probes are counted separately.

The translation-only benchmark in `.github/measure/TranslationCache` reports
front-end wall/CPU time, translation/hit/bypass counts and zero solver calls (it
does not invoke preparation or solving). Each cold/warm/edit sample starts a fresh
process; warm/edit samples first populate that process's table, outside the timed
interval, then resolve a new current program and translate it. The edit changes
one function in a separate unrelated module alongside Std. This distinguishes
translation savings from resolution or solver savings, neither claimed by PR A.

## Accepted-review implementation boundaries

Resolution owns the L/G/R split: L is the local semantic DAG key, G is the
current observable post-global-analysis projection (including negative answers),
and R finalizes translation identity. Builtin identity is taken after the catalog
is complete, including tuple arities and bitwidths. This branch consumes the
resolution worker's keys and naming; it does not independently invent those keys.
Until that finalized interface is published, `--modular-termination` always
bypasses the translation table: its downstream open-trait/dispatch facts are not
in the current L key. The process-local descriptor retains
its conservative source/environment checks and makes no persistent-resolution or
early-cutoff claim.

The complete task key must freeze the finite adaptive response policy **before**
the first solver request. A recorded response-dependent transcript alone is not
that policy. The exact initialization commands include the consequences of the
ordered declarations read by Boogie's `UsedTypes`; record those inputs explicitly
as well. Model/error/coverage decoding must use current task bindings. Unknown
factories, option values, protocol branches or unsupported side effects force a
fresh execution. Publication occurs only after normal task completion and full
report reconstruction. No module-level success summary can authorize a hit.

The warm reference is cache-enabled cold execution. CSV/JSON timing fields are
excluded from equality and must identify replayed observations. Verdicts,
diagnostics, exact per-task RU and solver input remain equal. Separately audit
legacy default-off versus option-on costs; do not hide naming-related cost or
verdict changes by calling them cache hits.


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

The environment descriptor includes an ABI identifier and actual bytes of all
application/runtime managed assemblies, the native solver and host, hostfxr,
runtime native libraries and resolved native dependencies. The macOS profile
also hashes the actual OS dyld shared-cache files; Linux resolves dependencies
with `ldd`. Native launcher scripts and Windows are unsupported. All environment
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
wrapper withholds `Completed` until the task observable closes, including final
idle synchronization. Divergence discards all buffered completion data and retries
`FromSeed` with persistent reads disabled and a fresh solver. A result is published
only after normal completion and exact outcome/RU agreement. The terminal comment
and subsequent idle handshake seal the exchange before a checker can be reused.

`SmtExecutionLimits` lexes the actual initial command stream, respecting comments,
strings and quoted symbols. Last effective `:timeout` and `:rlimit` commands win,
including raw prover-option overrides. Unknown deadline/thread controls bypass.
Completed Valid is eligible under any limit; Errors/OutOfResource additionally
require positive RU and zero timeout. Timeout, memory failure, cancellation,
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

The total modern CLI and inherited prover/solver input inventory is recorded in
[241-option-audit.tsv](241-option-audit.tsv). The driver unit drift gate compares
all names, aliases and runtime types and rejects unclassified additions/removals.
Input-stage options have already run before the frozen SMT boundary; residual
execution parameters are additionally encoded through the complete interfaces.
Report/selection/setup stages execute against the current compilation rather than
a stored rendered report. Legacy `--boogie` settings either flow into those
interfaces/actual commands or select a bypassed external profile; the opt-in
transport is available only on the modern verify command.

The implementation remains draft until the complete local suite/Std comparison,
negative controls, option-classification drift gate and five-run measurements are
recorded. The inherited module naming/materialization comparison currently has a
strict on/off RU/verdict discrepancy; cache-enabled cold/warm agreement alone
cannot establish the stronger default-on/off acceptance gate.
