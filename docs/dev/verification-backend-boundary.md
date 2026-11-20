# Verification backend boundary: P1 preparation, scheduling and reporting

These are the preparation and scheduling slices of [P1](https://github.com/erniecohen/dafny/issues/118).
They introduce work, result and preparation types, the native Boogie adapter,
shared translation/preparation with capture before backend mutation, and neutral scheduling through the CLI/IDE event pipeline.
They add no backend, command-line option, axiom or proof obligation. Native
verification behavior remains the compatibility requirement.

`IVerificationBackend<TInput>` takes a backend's typed input and a cancellation
token, without requiring an `ExecutionEngine`. The Boogie implementation accepts
the existing translated `Microsoft.Boogie.Program` and owns its mutating native
preparation call. It then applies the existing opt-in result-cache wrapper, in
the same order as before. It neither translates again nor proves during preparation.

`VerificationPreparationPipeline` owns synchronized large-stack translation,
scope reset, translation-cache lookup, dependency ownership and cancellation
checks. It takes a backend factory and an optional capture callback, with no
`ExecutionEngine` argument. The callback observes the translator graph before
the backend factory runs or preparation mutates that graph. A caller retaining
the graph must copy it; this is an ordering seam, not an immutable snapshot or
a normalizer. Each backend remains responsible for checking its input.

`DafnyProgramVerifier` uses that shared path, with native printing as the capture
callback and native cache/adapter construction as the delayed factory. Printing
still precedes result-cache lookup and preparation; the Boogie engine still
resolves, typechecks and prepares the graph exactly once. Translation and backend
preparation remain under the same semaphore, including across asynchronous work.
The returned preparation and module-owned proof dependencies travel together.
Failed preparations remain explicit, and exceptions and cancellation propagate;
profiling a failed preparation does not try to enumerate its work.

## States and evidence

`VerificationOutcome` distinguishes verified, failed, inconclusive, timeout,
resource exhaustion, out of memory, cancelled, unsupported and tool error.
Only `Verified` satisfies `IsVerified`; a default-initialized outcome is tool
error. `Bounded` is represented separately:
the native API can report it, and the existing native consumer treats it as
correct. The bridge preserves that historical treatment without claiming an
unbounded proof at the neutral seam. An unrecognized native solver outcome maps
to tool error. Unknown native scheduling statuses are rejected.

Work has source and scope origins, a scope/part identity, seeded variants,
idle/cache state, cancellation and an observable status stream. The adapter
forwards native scheduling, seeding, cancellation, observable completion and
observable errors. It adds no queue or subscription. The source origins retain
their native Dafny ownership, including refinement information.

Results can carry diagnostics, optional metrics and backend-specific evidence.
Missing metrics are not zero cost. Missing diagnostics mean they have not been
materialized, rather than that the backend found no errors. In this slice native
diagnostics, models, counterexamples, assertions and logs remain in the original
`VerificationRunResult` payload and are interpreted by existing native consumers.
The payload retains object identity; no synthetic native object is constructed.

Preparation readiness is separate from proof success. A prepared module can
legitimately contain no work. A failed preparation carries a non-success outcome
and its original exception, and `GetWorkOrThrow` rethrows it. The Boogie adapter
records resolution/type/admission errors this way; cancellation and other
exceptions retain their existing exceptional completion. No empty successful
work list is substituted. The explicit native reporting bridge rejects work or results without native evidence.

## Checked result streams

Scheduling calls `IVerificationWork.TryRunForScheduling`. Its default implementation
validates each newly started backend stream before publishing a terminal result.
Progress is forwarded immediately. Exactly one `VerificationWorkCompleted`
must be followed by clean observable completion; its original result, diagnostics,
metrics and evidence are then forwarded without reconstruction. Every defined
outcome is retained, and only `Verified` denotes proof success at this seam.

Empty or progress-only completion, repeated terminal results, statuses after a
terminal result, unknown status kinds and malformed results produce a
`VerificationException` for the original work and source owner. A pending result
is discarded on observable error, retaining the original exception, including
cancellation. Holding the terminal result until completion prevents a malformed
stream from publishing a successful proof before its protocol error is observed.
This checks the stream envelope, not the truth or completeness of backend evidence.

The Boogie adapter overrides this entry point to retain the native engine's existing
publication point. Withholding native results until observable completion changes
which task log receives prover-reset commands after a query, even when the queries,
diagnostics and proof resources are unchanged. The adapter therefore preserves
native publication and completion timing. The same envelope checks run in audit mode:
the first valid result is forwarded immediately, while missing/repeated results,
trailing statuses and observable errors prevent clean closure. Publication is not
a closure certificate, so a later native error can coexist with an earlier result.
Shared scheduling contains no native type test. Alternative
backend work inherits checked completion unless its adapter explicitly overrides
the scheduling entry point. These callbacks are an implementation contract, not
a sandbox against a malicious backend.

This does not add a timeout to a stream that never closes. P2 transport remains
responsible for liveness and matching worker responses to requested work. Native
cached completion and the native `TryRun() == null` already-running convention
retain their existing behavior. Transport response correlation and reporting
capabilities remain separate work.

The stream controls cover every outcome, withheld and immediate native completion, evidence identity,
errors before/after a result, cancellation, subscription isolation and disposal.
Engine-free scheduling controls reject malformed streams without publishing a
terminal result. Native lit controls cover isolated assertion batches, a failing
assertion, module windows, filters and translation caching.

## Module manifests and request accounting

After preparation, `Compilation` checks the whole module's work before refinement
and request filtering. A scope/part identity must be nonempty and unique within
that module, and every work item must have an owner in that module. The immutable
identity map is published as `ModuleVerificationWorkIdentified`. It describes the
backend's prepared work; it does not independently establish that translation or
the backend generated every required obligation.

Each selected, seeded request gets a distinct `VerificationRun`, linked to the
module manifest and published before its work starts. Seeding must preserve the
original source identity and owner. The run records its expected identities,
original result objects and original errors. Missing identities leave it incomplete;
duplicate, substituted, malformed or foreign results cannot fill the missing work.
All outcomes remain distinct. `IsComplete` describes result/error delivery, while
`IsClosed` requires closure accounting for every identity. `IsVerified` additionally
requires every result to be `Verified`, no execution errors, and complete closure
accounting. A published native result alone cannot satisfy this predicate.
Cancellation errors remain
`Cancelled`; other execution errors remain `ToolError`. Read snapshots cannot
change the ledger, and later errors remain non-success even after a result.

CLI state accepts only results recorded for its current run, once per identity.
It finishes after every selected result has been delivered, preserving the native
publication point and completion order. Late errors from an older request update
that run's ledger and cannot finish or fault a new request for the same source
identities. Synchronous start errors and status-handler errors use the existing
verification-exception path. Preparation exceptions still use the existing
internal-compilation-exception path and never publish an empty ready manifest.

The scheduler records `StreamClosed` only after clean observable completion and a
unique recorded result, or `CachedResult` when no stream starts and an already
completed cache result is delivered. These are distinct in the immutable closure
snapshot; cached delivery does not claim to audit the cache producer's past stream.
`AuditCompletion` settles when every selected identity has closed, delivered a
cached result or failed. It does not itself imply proof success; inspect the ledger.
An already-running task without a cached result remains pending, and no timeout or
liveness guarantee is added. Errors and cancellation retain their original objects
and invalidate success even after publication. Empty selected requests close immediately.

This audits stream envelopes and selected identities, not backend evidence or
translation completeness. Native publication, CLI result delivery and reporting
retain their existing timing; CLI aggregate results do not yet await this separate
audit. Propagating audited success through reporting consumers, IDE handling of
superseded requests and transport-level response correlation remain open.
Native CLI reporting retains its historical treatment of `Bounded`; the neutral
ledger never labels `Bounded` as verified.

## Neutral CLI aggregation and optional diagnostics

`CanVerifyResult.WorkResults` carries the original neutral work/result pairs at the
existing publication point, including the module-window path. CLI summaries consume
neutral outcomes and optional resource metrics. `IVerificationAssertionStatistics`
supplies assertion counts when available; missing counts or resources are reported
as unavailable when the corresponding detail is requested. The native adapter supplies
its original assertion and counterexample counts, preserving native summary arithmetic
and the historical bounded-result summary. The neutral ledger still never treats
`Bounded` as an unbounded proof.

`VerificationWorkItemResult.ReportDiagnostics` forwards materialized diagnostics by
identity or delegates lazy materialization through `IVerificationDiagnosticReporting`.
The Boogie result implements that optional capability and retains the existing native
name, token, runtime limit, model extraction and diagnostic policy. An attempt to pair
native diagnostic evidence with foreign work fails explicitly. A non-verified result
with missing, empty or warning-only diagnostics receives an error at its work origin,
even when a lazy reporter emits no error or another part already reported an error.
The native adapter explicitly retains its legacy diagnostic policy through the
reporting-only `UsesLegacyDiagnosticPolicy` capability; optional reporters default to
false. That policy also identifies the native bounded-result summary. Assertion counts
alone cannot confer that display policy, and neither capability changes proof success. Display labels do
not determine this check. No backend or new command-line option is enabled.

This is a delivery/reporting milestone, not audited CLI acceptance. It deliberately
retains publication timing: CLI aggregates do not wait for `AuditCompletion`, so a native
result may still be reported before a late closure error. Propagating that audit through
all consumers remains a follow-up. `CanVerifyResult.Results` is a temporary native
projection for loggers, dependency consumers and complexity reporting; it rejects foreign
evidence. Batch-progress descriptions, IDE model/gutter/dependency reporting and legacy
entry points retain their native paths. This change does not claim that a foreign backend
can use those capabilities or the complete CLI reporting workflow yet.

## Compatibility and controls

The unit controls exercise all outcomes, optional evidence, immutable work
ownership, native payload identity, seeding, cache state, status order and
cancellation. Actual Boogie preparation has a valid-input control and unresolved
and ill-typed negative controls. Failed preparation must throw through the same
bridge used by the shipped pipeline. The registered lit regression contains
passing and failing assertions in multiple modules and a filtered successful run.

Engine-free preparation controls use a recording backend on actual translated
Dafny modules. They exercise capture before mutation, default/cold/warm graphs,
checksum flags, independent dependency maps, explicit failure outcomes, capture
and backend exceptions, cancellation and serialization. The native lit regression
also compares filtered output with translation caching off and on.

The serial counterfactual runner removes preparation guards, changes outcome
classification, drops seeding/cancellation/statuses/metrics/evidence, omits cache
wrapping and replaces the pipeline's prepared work by an empty list. It also
removes capture, checksum propagation, translation-cache routing, module dependency
ownership and serialization, replaces a failed backend preparation by an
empty ready result, and makes profiling enumerate failed work. Each mutated
build must compile and then fail an executing control; a compilation error does
not count. Source bytes are restored in a `finally` block:

```sh
python3 .github/review/verification-boundary-mutations.py --work work/boundary-mutations
```

Do not build or test concurrently in that checkout while mutating it. Rebuild
the integration harness afterwards to restore its copied executable libraries.

The comparison runner `.github/review/verification-boundary-parity.py` captures
raw CLI stdout/stderr, exit codes, per-procedure SMT logs and per-proof resource
counts. It compares byte hashes without normalizing CLI or SMT contents. The
corpus includes all five lit strata, native bitvectors, failed assertions, both
resolvers, additional axioms, a nonzero seed, resource exhaustion, the standard
library and all six target-specific library configurations. Run both builds from
the same source root and use the same work directory and solver:

```sh
python3 .github/review/verification-boundary-parity.py capture \
  --dafny work/before/Dafny.dll --solver "$Z3" --work work/parity --name before
python3 .github/review/verification-boundary-parity.py capture \
  --dafny Binaries/net10.0/Dafny.dll --solver "$Z3" --work work/parity --name after
python3 .github/review/verification-boundary-parity.py compare --work work/parity
```

Captures resume completed cases. Use a fresh directory after changing sources,
options, solver or binaries. Logs are hashed before temporary SMT files are
removed; the work directory retains the CLI streams and comparison receipts.
Execution receipts are evidence for a particular run, not an unbounded
equivalence proof. Full upstream harness and IDE regression results are separate
acceptance gates; this runner does not replace them.

The CLI reporting controls also run a recording verifier without native work or
result payloads. They cover every defined outcome, absent and available metrics/counts,
filtered assertion summaries, lazy diagnostics that emit no errors, and bounded display
compatibility without changing the ledger's proof outcome. The registered reporting lit
control checks native assertion counts, a failing assertion, module windows and translation
caching. The serial reporting counterfactuals exercise these controls:

```sh
python3 .github/review/verification-reporting-mutations.py --work work/reporting-mutations
```

## Remaining P1 work

`IProgramVerifier` accepts a `VerificationPreparationRequest` and returns explicit
module preparation, without an engine argument. `Compilation` has an engine-free
request-factory constructor; its existing constructor selects the Boogie request.
The native request owns snapshot flags, printing and delayed result-cache construction.

Scheduling, source ownership, refinement filtering, range grouping, seeded work,
cache completion, cancellation, profiling, pipeline events and CLI/IDE task state
use `IVerificationWork` and neutral statuses. Hidden-function metadata is obtained
lazily at the same scheduling point as before. Profiling preserves native outcome
names and metric values; absent foreign metrics remain absent. Verifier mocks use
the same interface and forward optional native reporting evidence when decorating work.

This is still a partial P1 migration. Native model/dependency interpretation, CLI loggers and IDE gutters cross
`BoogieVerificationReporting`
to the original native objects. Foreign evidence is rejected explicitly; no split,
implementation, counterexample or proof result is fabricated. This bridge is not
an implementation of alternative backend reporting. The remaining milestones are:

- Finish optional model, dependency, logger, batch-progress and gutter capabilities.
  CLI aggregate outcomes and diagnostics now have a neutral path; complete foreign
  reporting still needs the remaining capability adapters.
- Finish non-success accounting through reporting consumers, including failed
  preparation, unsupported input, cancellation and worker errors.
  The default scheduling entry point now rejects malformed completion before publishing
  a terminal result. Module manifests and request ledgers now account for prepared
  and selected work identities, observed outcomes and native stream closure.
  Reporting consumers still need to consume the separate audit before claiming
  audited completion. Superseded IDE requests and transport response correlation
  remain open; the native adapter retains its engine's publication timing.
- Migrate legacy entry points to the neutral boundary. The deprecated CLI,
  synchronous build/run path, `DafnyMain.BoogieOnce` and the legacy server still
  invoke native Boogie directly. Legacy CLI compatibility controls cover positive,
  negative and filtered runs; they do not migrate those entry points.
- Complete opt-in translation/result-cache coverage and repeat default-Boogie
  compatibility and full harness checks after reporting migration.

No B3 worker, transport, normalization or source/model correspondence is included.
P0 bootstrap acceptance and P2 and later work remain separate.
