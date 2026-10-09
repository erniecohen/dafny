# Canonical local exit construction

The general local-exit candidate is `a2a98968c`, with actual-preparation audit
revision/product pin `1e6fa5126`. It extends the independently validated
[caller construction correction](obligation-canonical-caller-native.md).
It has normal compiler and structural acceptance below; focused native gates
are submitted but have no completed result yet. PR #168 remains draft.

## General local policy

For a locally declared clause without a statement expression, use the complete
can-call support already emitted by the standard assertion-WF traversal before
the sole actual postcondition check. Avoid constructing an extra copy before
that traversal. This decision depends on the current clause and its inheritance
or statement-expression effects, never unrelated method-body terms or a target
name. Inherited clauses retain original leading support and reverification
guards. Statement expressions retain original leading support and outer reveal
and visibility effects. Clause order, actual checks/full metadata, fuel,
publication and return/fallthrough points retain their established policy.
The [preparation argument](obligation-certified-preparation.md#canonical-local-exit-construction)
explains the independently licensed WF support. There is no added axiom,
second proof, positive source hint or higher resource limit.

## Completed diagnostic comparison

The [scratch compiler](https://github.com/erniecohen/dafny/actions/runs/38003980400)
completes eighteen original multiset observations at seeds 0/1/7 in both axiom
settings and original full-project settings. Six selected positives verify;
six actual false-entry controls contain Invalid VCs; independent WF checks are
Correct. Six controls match the normal caller compiler's complete target/WF
outcome and resource vectors. Two seed-zero controls also match its complete
solver-command streams; equivalent stream comparisons were not available for
the other seeds. Twelve strict comparisons preserve all sixteen actual checks
and full metadata, typed fresh raw/normalized preparation, complete unique
can-call support/attributes, scope, fuel and publication. Cost falls at zero
and one but rises at seven; uniform improvement is not established.

The earlier [RemoveFactor comparison](https://github.com/erniecohen/dafny/actions/runs/38002913275)
completes six observations with faithful native controls, all thirteen checks
and complete preparation/support retained, and two Invalid false controls.
The same construction change does not repair its original-ceiling exhaustion.
No case-specific product policy is adopted. These are scratch causal diagnostics,
not substitute normal-product proof gates.

## Normal compiler and focused gates

The [normal build](https://github.com/erniecohen/dafny/actions/runs/38005432701),
source `5afd17884`, reports `4.11.0+fcb2042d.review.5841c0e6`. All ten actual
stages pass: both native compilers, both direct Boogie probes, 112 obligation
tests in each mode, all 390 core tests, the reviewed 287-group producer inventory
and editor build. Only changed-source file hashes are refreshed; producer counts,
expression hashes and classifications are independently confirmed unchanged.
New structural controls check support after quantified WF witnesses, inherited
leading support and guards, statement-expression reveals, and independence of
actual exit preparation from unrelated body terms.

Three separate complete focused gates are frozen against this normal compiler:
322 registered observations, 96 unchanged known-library observations, and 68
unchanged known-suite/control observations. The registered helper includes new
paired quantified-exit and genuine negative controls; direct false-call controls
are covered there. Each gate preserves original inputs, project flags, solver,
ceilings, core limits and seed policy, and checks the exact compiler source and
component hashes in addition to its version. These gates are pending; there is
no current native-product result to report yet. No whole-suite or whole-library
iteration was launched for this exit revision.

RemoveFactor, section 13's both-partial classification, complete default-off
cost-policy acceptance, final functional gates and independent soundness review
remain open. The [request](obligation-review-change-request.md) and
[checklist](obligation-revision-checklist.md) govern completion. PR #169 remains
unchanged and its port requires explicit owner approval.
