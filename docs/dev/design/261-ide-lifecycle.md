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
metadata. Finalized global-analysis identities and the remaining
cancellation/selection lifecycle matrix are separate work.

## Shared task identities across front ends

The task execution environment distinguishes proof-affecting options from two
front-end lifecycle settings. The transport ABI is now version 4; previous
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
