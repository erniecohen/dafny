# Canonical local exit construction

The general local-exit candidate is `a2a98968c`, with actual-preparation audit
revision/product pin `1e6fa5126`. It extends the independently validated
[caller construction correction](obligation-canonical-caller-native.md).
It has normal compiler/structural acceptance and completed registered, known-library and known-suite records below. The complete paired fixture gate also passes. Remaining supported-line acceptance is open; PR #168 remains draft.

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

The complete registered native helper passes **322 observations**: 89 positive
observations with Valid VCs and 233 negative observations with genuine Invalid
VCs. Its output equals the committed expectation exactly; there are no resource,
timeout or inconclusive outcomes. Both resolver/axiom settings are covered where
the helper specifies them. New paired quantified-exit and negative controls,
original issue 100, subset/call preparation, scoped reveals, independent WF and
project-option precedence controls pass.

The unchanged known-library gate completes **96 observations**: all 66 positive
observations verify, all thirty negative controls contain genuine Invalid VCs,
and all reported independent specification-WF checks are Correct. All ten known
cases, including original Remainder and multiset ordinal decrease, are VVV at
seeds 0/1/7 in both axiom settings at original full-project settings and ceilings.
Six fresh unchanged-baseline Remainder observations also verify. The normal
multiset outcome/resource vectors match the selected scratch vectors at all
three seeds. Both normal seed-zero WF and correctness solver-command streams
match their selected scratch streams exactly through the final check-sat.

The unchanged known-suite/control gate completes **68 observations**, with four
genuine Invalid negatives and no reported independent specification-WF failure.
All original issue/subset/guarded-argument controls pass. The six selected
**default-off Power outcome/resource vectors exactly match baseline**; this
sample does not establish complete default-off compatibility. Both axiom
settings have these enabled vectors at seeds 0/1/7:

| Case | Normal local-exit candidate |
| --- | --- |
| Power subtraction | VVV |
| AltPrimeDefinition | VVV |
| Composite | VVR |
| ExtensibleArray.Append | VVV |
| FormArmy | VVV |
| RemoveFactor | RRR |
| Iterator.MoveNext | VVR |

V is Correct and R OutOfResource. Composite improves at zero and one as a result
of this general local-exit policy; no case-specific product tuning is introduced.
The historical both-partial classification continues to govern Composite and
the iterator. A separate completed ten-observation gate now finishes only
RemoveFactor's existing fixed range 0-7: fresh seeds 2-6 in both axiom settings,
combined with the six explicitly reused current observations at 0/1/7.
RemoveFactor exhausts the original combined ceiling at every one of those eight
seeds in both settings. This does not meet section 13's both-partial rule; the
preserved legacy vector is VRRRRRRR. No seed range is expanded or product change
selected to obtain a preferred verdict. Thus the
gate supplies a completed diagnosis, not all-positive suite acceptance.

The complete **688-observation paired assertion-invariance fixture gate passes**
on the same normal compiler: 440 positive observations have only Valid VCs and
all 248 negative observations contain genuine Invalid VCs. Every recorded VC
is Valid or Invalid, with no resource, timeout or inconclusive outcome. All 172
cases run under both resolver modes and both axiom settings at their original
options and ceilings. The earlier result on a different semantic revision is
retained separately; it is not substituted for this fresh gate. All current gates preserve original
inputs, project flags, solver, ceilings, core limits and seed policy and check
exact compiler source/component hashes as well as version. No whole-suite or
whole-library iteration was launched for this exit revision.

## Solver-input shape diagnosis

An offline exact comparison of all six multiset construction-only query pairs
finds identical complete command streams through check-sat except the sole
combined VC assertion. Those VC formulas normalize identically using only
lexical let/binder handling and Boolean implication, negation, associativity,
commutativity, idempotency and true/false identities. Trigger and no-pattern
terms retain their structure modulo lexical binder identities; other annotations,
function/fuel/layer symbols and background assertions remain exact. The audit
includes negative binder-shadowing, capture, trigger-order and implication
controls. This identifies Boolean query shape as the varying input in these
pairs, not a changed background axiom or missing fuel. It is a bounded analysis,
not a claim that all logically equivalent inputs have equal solver cost.

Both seed-zero RemoveFactor construction-only pairs have the same bounded
Boolean-equivalence property. Both current normal seed-zero full solver streams
are exactly equal to their preceding rejected scratch-selected streams and
still exhaust resources. This confirms faithful product reproduction of that
negative result. The comparison is against the caller-only candidate, not a
claim of exact equivalence with the legacy translation.

## Bounded legacy/current trigger and fuel inventory

An additional offline comparison uses the already faithful legacy control and
this normal compiler's saved `RemoveFactor` query at seed zero, with additional
axioms off and unchanged source and ceiling. All 33 background assertions are
exactly equal. The local induction quantifier, including its full ordered
patterns and body, is also structurally equal after a sort-preserving, one-to-one
correspondence of its free parameters and lexical binder normalization.

Two further parameter correspondences are justified by identical typed defining
equalities for the picked value and recursive-call set argument. With that
correspondence, the query-wide unique ground-term inventories match exactly:
ten layered `product` applications, five set differences, three membership
applications and seven product can-call applications. The `product` applications
retain both existing fuel-layer shapes. No term is removed from these selected
inventories; this is an inspection of saved inputs, with no new solver run or
modified proof input.

These observations narrow the missing-support hypothesis for this case. They do
not establish that each term has the same availability on every VC path, that
Z3 makes the same useful instantiations, or that the two complete VCs are
logically equivalent. The bounded audit includes lexical shadowing, alpha-renaming,
capture, repeated-parameter and sort-mismatch controls. The legacy query verifies
and the current combined query reaches its original resource ceiling; the causal
explanation and product remedy remain open.

RemoveFactor, section 13's both-partial classification, complete default-off
cost-policy acceptance, required final repository gates and independent soundness review
remain open. The [request](obligation-review-change-request.md) and
[checklist](obligation-revision-checklist.md) govern completion. PR #169 remains
unchanged and its port requires explicit owner approval.
