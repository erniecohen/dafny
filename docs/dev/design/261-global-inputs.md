# Frozen global translation inputs

`--translation-global-inputs` is an opt-in step toward the finalized identities
in [the cache design](261-content-addressed-caches.md). It permits eligible
modular-termination translations in the existing process-local and persistent
tables. It changes neither resolution nor the meaning of a termination check.

For each current module, `TranslationGlobalInputs` computes a projection from
the current `ModularTerminationAnalysis`. It includes every member of each
resolved module/import/export view in the local dependency closure, inherited
and prefix members, and builtin members. For each possible caller view in that
closure, and the absent caller, it records dispatch, argument-only and
dispatch-reachability answers. False answers are data too. Canonical nominal
member kinds/names and caller names bind the answers; conflicting answers under
one nominal name make the projection ineligible. No source paths, global
ordinals or unrelated program digest enter this projection.

The final translation-resolution identity hashes a versioned frame containing
the existing local resolution identity and the projection digest. The ordinary
translation descriptor adds that identity to its existing producer, prelude,
options, source binding and closure inputs. Local resolution tables continue to
use local keys and identities, so global dependencies introduce no resolver
cycle. An unrelated module's classification changes no projection unless it
changes an answer actually included in the closure.

All three translation query sites consume the frozen projection. A read of an
unlisted member/caller pair returns the current native answer and permanently
marks that module view incomplete for this compilation. Both snapshot publishers
check completeness after translation; no incomplete result is published. A warm
entry therefore came from a completed translation whose reads were all covered.
Producer identities distinguish changes to this query/projection contract.
External plugins and generated test runners retain conservative bypasses.
Cardinality admission and current resolver/rewriter diagnostics still run before
reuse. This does not authorize reuse across other unclassified postpass effects.

Controls compare native, frozen cold and frozen warm Boogie and raw solver
commands. A native-solver regression compares verdicts, diagnostics and every
non-timing CSV field across fresh/cold/warm/audit runs, with real hit and solver
start checks, failed-bound and reachable-false controls, corrupted records and
source edits. An injected analysis-answer change with unchanged local identity
checks the final-key boundary. Unlisted-read controls exercise both publishers.
The existing persistent audit remains the independent result/graph oracle.

Whole-module IDE report adapters remain separate work: IDE consumers still
require current native tasks and use late solver-response reuse. Export-view
projection precision is an invalidation optimization, not permission to omit
negative global answers.
