# Content-addressed translation and verification results

This is the implementation design for the cache portion of
https://github.com/erniecohen/dafny/issues/241. It builds on
[the pipeline study](241-pipeline.md), especially sections 3, 5 and 6, and the
front-end design in https://github.com/erniecohen/dafny/pull/258.

## Scope and prerequisites

Use two independent opt-in options: `--translation-cache DIR` for the translation
table, and `--verification-cache DIR` for the result table. Each uses the same
storage format but a distinct table. Neither option implies the other or
`--resolution-cache`. Without either option the existing translation, preparation,
solver lifecycle and reporting paths execute without constructing a cache,
hashing inputs, changing names, or opening cache files.

**Implementation status:** this document specifies the integration contract.
The storage primitive can be developed and tested independently. A storage test
is not evidence that either command-line cache works. Neither option may be
exposed as a working cache until its prerequisites and acceptance gates below
are complete.

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
Translate(ModuleKey, admitted resolved module) -> TranslationSnapshot
```

The snapshot must be a deterministic function of that key. Whole-program name,
replacement and cardinality admission run on every transaction before a snapshot
can authorize verification. A receipt from a previous program is never accepted.

## Keys and versioning

Use SHA-256, with a format-specific domain, a component count and an unsigned
64-bit big-endian byte length before each component. Hash bytes, not a runtime
object hash, concatenated strings, a debug printer, or additive checksums. Digest
strings are exactly 64 lowercase hexadecimal characters. Every codec change
bumps its format domain and namespace; unknown versions are misses.

### Translation table

The lookup descriptor contains the semantic `ModuleKey`, the translation ABI,
the loaded translator and Boogie assembly digests, the exact captured prelude,
and effective translation options, including ordered pass-through options.
These are the compiler-environment component of the module key, not a substitute
for its semantic dependencies. The value identifies the exact Boogie bytes by
hash and a versioned metadata sidecar by hash.

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
must restore report mappings into the current `ProofDependencyManager` without
reusing references to an older compilation.

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
execution is unchanged. Effective elapsed-time limits, including declaration and
solver-option overrides, must be zero for reusable records. Require an explicit
positive RU limit. Enforce eligibility at the task boundary, after overrides.

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
historical timings, identified as historical and separate from current timings
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

This API is a prerequisite in the Boogie dependency rather than a change to the
sibling worker's resolution/naming sites. A Dafny-only transport alternative
needs an equally explicit lifecycle/retry proof and remains unaccepted until it
passes the same gates. The implementation scope of that dependency prerequisite
must be settled before result reuse is enabled.

CLI filters select today's task inventory. Each task independently hits or runs;
all selected tasks contribute to the current aggregate exit code. Initially bypass
persistent reads for `--filter-position-batch` and unsupported side effects or
callbacks. Solver logs, trace/timing output, model/split files and verification
plugins either have tested replay support or bypass. CSV/JSON per-task outcomes
and RU are identical on cold and warm runs; actual execution timestamps and
durations describe the current invocation. Default stdout, stderr and exit code
must be byte-identical between cache-enabled cold and warm runs.

## File layout, atomic publication and concurrency

All paths below are relative to the user-selected directory:

```text
v1/
  blobs/sha256/ab/abcdef.../data      # immutable payload bytes
  translations/ab/abcdef.../data    # JSON: lookup key -> payload hash, length, codec
  results/ab/abcdef.../data         # JSON: lookup key -> payload hash, length, codec
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
The module key covers every translation-observable prerequisite; history-free
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

No performance numbers or feature acceptance are claimed by this design.
