# Verification backend boundary: P1 preparation and scheduling slices

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

This is still a partial P1 migration. Native diagnostic/model/dependency interpretation,
CLI result aggregation/loggers and IDE gutters cross `BoogieVerificationReporting`
to the original native objects. Foreign evidence is rejected explicitly; no split,
implementation, counterexample or proof result is fabricated. This bridge is not
an implementation of alternative backend reporting. The remaining milestones are:

- Replace the reporting bridge with optional diagnostic, model, dependency, logger
  and gutter capabilities, including neutral CLI aggregate results.
- Account for expected/completed work and every non-success outcome end to end,
  including failed preparation, unsupported input, cancellation and worker errors.
  The current scheduler propagates errors, but does not yet audit observable completion
  without a terminal result or repeated/missing task results.
- Cover legacy entry points and opt-in translation/result caches explicitly, and
  repeat default-Boogie compatibility and full harness checks after reporting migration.

No B3 worker, transport, normalization or source/model correspondence is included.
P0 bootstrap acceptance and P2 and later work remain separate.
