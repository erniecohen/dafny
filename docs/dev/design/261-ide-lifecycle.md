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
metadata. Finalized global-analysis identities, process-level CLI-to-IDE acceptance,
and the remaining cancellation/selection lifecycle matrix are separate work.
