# Persistent cache use by IDE consumers

The persistent options remain opt-in. The language server uses the same resolver,
preparation pipeline and task transport as the CLI. Its current report consumers
also need native implementation, assertion, counterexample and split graphs for
gutters, hover details, counterexample requests and explicitly seeded verification.
The detached reports in the whole-module result table do not encode those graphs.

The IDE compilation factory therefore requests native tasks explicitly.
The shared verifier forwards that request; the CLI leaves it off. Both whole-module
lookup routes (the unchanged translation key and early cutoff after fresh
translation) honor that requirement, including parallel preparation. Translation
and preparation still construct current native graphs. The existing task transport
can replay complete solver responses against those graphs, preserving native
reporting and `FromSeed` without constructing placeholder implementations or splits.
Recording and the independent persistent audit remain enabled. The CLI keeps its
whole-module fast path.

Acceptance exercises an independent cache-disabled route, cold publication, a new
language-server instance over the same store, unsaved overlays, a failing edit and
revert, cancellation of a controlled pending generation, explicit seeds, and an
audited run. Compare every completed VC's identity, outcome, assertion count and
resource count; require actual transport hits and no solver starts in the
successful warm case. A cold module manifest must exist, so the native requirement
is tested against an eligible whole-module hit rather than an incidental miss.
Mutating either replay guard or removing the IDE's native-task request must fail.

This closes the native-evidence eligibility boundary, not all of the IDE milestone
in [the content-addressed cache design](261-content-addressed-caches.md). Persistent
whole-module IDE replay needs a complete report adapter and current-source assertion
metadata. Finalized global-analysis identities are separate work. The scheduling
acceptance covered below uses current native tasks and solver-response replay.

## Shared task identities across front ends

The task execution environment distinguishes proof-affecting options from
front-end lifecycle settings. The transport ABI is now version 5; previous
execution descriptors cannot match the new environment bytes.

* `SMTLibOptions.RunningBoogieFromCommandLine` binds the current console's
  cancellation event. In the pinned Boogie release, its only read is the
  subscription in [SMTLibProcess](https://github.com/erniecohen/boogie/blob/a93a02c83aacb6849ca6518071c20e9bdc802213/Source/Provers/SMTLib/SMTLibProcess.cs).
  It changes no solver command or response. Each current task still receives its
  own cancellation token; cancelled or incomplete exchanges are not published.
* `CoreOptions.VerifySnapshots` values at or below zero disable legacy result
  lookup and insertion on the modern task route. The CLI's negative sentinel and
  IDE's zero therefore use the same descriptor value. Positive levels remain
  distinct. The file-discovery behavior of Boogie's `ProcessFiles` is outside the
  [GetVerificationTasks route](https://github.com/erniecohen/boogie/blob/a93a02c83aacb6849ca6518071c20e9bdc802213/Source/ExecutionEngine/ExecutionEngine.cs)
  used here; the prover's use of this option compares the level with three.
* Applying project defaults can append the same `PROVER_PATH=` assignment more
  than once. This occurs across edits and document close/reopen. In the pinned
  [ProverOptions parser](https://github.com/erniecohen/boogie/blob/a93a02c83aacb6849ca6518071c20e9bdc802213/Source/Provers/SMTLib/ProverUtil.cs),
  each such option only assigns `ProverPath`. When every path setting is the
  same exact `PROVER_PATH=` assignment, retain one in the descriptor. Differing
  assignments, colon/bare syntax and every other option retain their original
  order and multiplicity. The live options are never modified. The effective
  solver path and audited executable identity remain keyed separately. This
  version-5 treatment prevents redundant defaults from encoding document history.

Only those exact interface properties receive this treatment. All other execution
options, ordered used types, solver/toolchain identity and complete lowered solver
input remain in the descriptor. Replay still checks each adaptive request against
its recorded response. This does not change the native task ownership requirement
or permit whole-module CLI reports to become IDE task graphs.

The stdio acceptance test populates a store in the CLI and requires genuine IDE
transport hits with identical execution keys and per-VC outcomes/resource counts;
it also exercises the reverse direction with CLI whole-module replay disabled.
Independent stores provide fresh oracles. Positive legacy levels and explicit
seeds must miss an incompatible prior result. Separate project-root tests use
matching declaration and file names with different assertions, compare fresh,
cold and restarted warm results, and require current native implementation URIs
and counterexamples. An edit/revert in one root must not alter the other root.

## Manual selection, save and close/reopen acceptance

The scheduling checks drive client requests rather than requesting whole-document
verification through a test helper. They run with one and two translation workers.

| Operation | Required observation |
|---|---|
| Open in `Never` or `Save` mode | Both verifiable symbols remain unprepared. A request outside any symbol returns false. |
| Select the positive symbol | Only it completes; the unselected failing symbol stays unprepared and produces no counterexample. |
| Save | In `Never`, the unselected symbol still needs an explicit request. In `Save`, it completes and exposes its expected failure and native model. |
| Fresh/cold/restarted warm/audit | Every completed VC agrees in identity, verdict, assertion count and resource count. Warm requires actual transport hits and no solver starts; audit requires fresh solver starts, no task hits or publication, comparisons and no mismatches. |
| Explicitly cancel pending work twice | The controlled task's observable must terminate with cancellation, not just remain initially stale. No task result is published. |
| Edit after cancellation, then close/reopen | Completed facts agree with the independent fresh oracle. New graphs and current native origins replace the old generation; no cancellation diagnostics or counterexamples survive. Reopen and another server restart both require persistent hits without solver starts. |

The stdio regression independently checks that manual mode does not run on open,
an invalid selection or save, and that a valid manual request replays after a
server restart with fresh per-VC facts. Unit controls retain differing path
settings and repeated non-path solver options. Mutating path normalization,
selection, save gating or explicit cancellation must fail the corresponding
control. Controlled pending work makes cancellation deterministic; this is not
a stress test of every possible in-flight solver timing.

## Independent audit responses

The audit must bypass task-response replay as well as whole-module replay.
Previously, a fresh translation and preparation could still consume persistent
solver responses. Audit mode now constructs the same task execution descriptor
but neither reads nor publishes task exchanges. The module recorder can compare
the stored key, verdict and resource count against an actual fresh solver run,
without replacing the evidence under comparison. The pinned modern task route
does not reuse the engine's legacy implementation-result cache; its wiring is
unchanged.

Transport controls populate a store with a different resource count and require
the audit to observe the fresh count, leaving the original bytes unchanged. An
empty-store audit must not publish. Client checks require fresh solver starts on
successive audited generations with both new and reused solver engines. The
stdio regression requires every audited task to report non-replayed timing.
Mutating either transport guard or the session's audit wiring must fail.
