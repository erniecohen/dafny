# Local obligation lowering

The [requested revisions](obligation-review-change-request.md) govern this change.
The [revision checklist](obligation-revision-checklist.md) records acceptance and
remaining work. PR #168 implements the single-check design; native acceptance is
pending. The development implementation must wait for explicit owner approval
before porting and then obtain its own evidence.

`--consistent-obligation-checks` is experimental and disabled by default. Its
project-file spelling is `consistent-obligation-checks = true`. The option changes
how an existing obligation is prepared and checked. It introduces no background
axiom, body/context term collector, global fuel policy, resource increase or
assertion-isolation setting.

## Local invariant

For an implicit source proposition P, the intended comparison is an immediate,
unannotated `assert P` at the same program point. The implicit check must have the
relevant local support that assertion would provide, check P once, and preserve
the original kind of check's normal publication. An intervening assertion of a
different proposition, explicit proof blocks, labels, custom triggers, reveals
and fuel overrides retain their own semantics.

Support comes from P and legitimate verifier state: frozen actuals/results,
current/old/labeled heaps, receiver and type substitutions, declared well-formedness,
SCC restrictions, frame, and current hide/reveal scope. No preceding or surrounding
body expressions are consulted. The later context/body collection experiments are
excluded from this implementation.

`LowerProposition` extracts the existing assertion splitter and fuel policy for
the actual check. It returns checked/free/both pieces, whether splitting happened,
and the normal post-check summary. It emits no commands. Unsplit conditions retain
the assertion path's second translation; split conditions retain its original
inlining/induction restrictions. Explicit assertions keep their existing translator,
one-shot fuel adjustments, scope and publication.

Implicit lowering uses a fresh translator with the same heaps, layers, receiver,
SCC and frames. It temporarily selects the existing assertion statement context,
then restores the surrounding statement/fuel state. It does not globally change
expression polarity or fuel. Preparation is recorded as checked expression,
declared contract, or guarded introduction; those categories confer no permission
by themselves. Existing WF/contract paths establish permissions in their original
order. `LowerDeclaredProposition` replays the immediate assertion's source-local
WF preparation, then lowers the sole contract check with that same fresh
translator. Inherited source clauses guard this preparation consistently with
reverification. This restores quantifier-local binder and can-call support;
matching only the final expression is insufficient. Free splitter pieces are
not independently proved theorems. The causal evidence and acceptance limits
are recorded in [the preparation diagnosis](obligation-local-preparation-diagnosis.md).

## Contracts and publication

Each method postcondition is checked inside the translated body at every return
and fallthrough, before reveal-scope commands are popped. The implementation
procedure's user `ensures` copies are nonchecking/free. Caller procedures retain
their normal postconditions. Frame/heap boilerplate remains checked as before.
Inherited clauses keep `$_reverifyPost` guarding. Clause order, custom errors and
proof dependencies are retained, so earlier established clauses can support later
ones.

The sole local check uses ordinary assertion publication, matching the previous
checked-ensures continuation. An unsplit assertion already publishes its checked
fact; no additional summary is emitted. After a split, the normal source summary
is published once, after all checked pieces. This is the splitter's existing proof
cut, not an added proof preceding a legacy check. Explicit split assertions retain
check-and-forget publication.

Method-call preconditions use frozen formal/actual substitutions at the pre-call
heap and current visibility. Their local assertions are the sole user-contract
checks; call procedure user `requires` copies are free, while implementation
preconditions remain callee assumptions. Receiver, generics, boxing, old arguments,
reads/modifies frames and termination retain their original order. Old-argument
allocation contracts likewise become nonchecking only at calls whose local
allocation obligation is checked. Other checked boilerplate remains intact.
Ordinary call havoc and outputs govern which established facts survive a call.

Ordinary function preconditions adapt their existing checked assertion rather than
adding a parallel proof. Higher-order calls and `.requires` share the existing
`RequiresN` constructor, including heap, arity, types, receiver and boxed actuals.
Function postcondition checks retain their existing procedure placement, declared
can-call allowance and SCC-height restriction, with the local assertion lowering
applied to that actual checked contract. No self-call result-type assumption is
introduced before its body result is checked.

The direct fixture [boogie-contract-semantics.json](../../Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/assertion-invariance/boogie-contract-semantics.json)
checks the fixed Boogie semantics: free ensures imposes no implementation proof
and remains available at calls; free requires imposes no call check and remains
a callee assumption. Three negative controls require failed local assertions and
retained checked boilerplate to be reported. Validation checks the verifier's
summary counts: successful input processing alone does not mean a proved program.

Iterator final contracts follow the same local exit/free implementation-copy rule,
with their original inherited-clause exclusion. Yield ensures adapts the existing
local check, retaining check-and-forget followed by the original yield-summary and
havoc. Yield preparation uses the body's iterator reads policy and the exact
saved iteration old heap; it does not invent a method reads-frame variable.
Inherited yield clauses retain their preparation/check exclusion.
Opaque-block ensures adapts its original local assertion and attributes;
loop invariants adapt their existing checked pieces, with normal invariant
assumptions and loop-state guards. These paths add no second proof of a clause.

Legacy `ReturnPosition` scope behavior is retained in method, iterator and forall
proof bodies. Previously retained reveals remain available to outer checks;
ordinary nonterminal scope boundaries are retained. Scope preservation includes
Boogie's commands as well as formula/fuel identity.

## Visible types, conversions and allocation

For a visible redirecting type, the membership obligation checks required base
membership and `CanCall(C(v)) ==> C(v)` using the assertion lowering under that
guard. The already translated value is wrapped without reevaluating the source.
Generic substitutions are capture avoiding. Numeric parents use the authoritative
combined predicate used by the existing numeric membership axiom; nonnumeric
parents recurse through the base-membership check.

The existing introduction axiom then derives symbolic `$Is` membership. Its
representation may be published where the original check needs it, but is not
proved again as a second assertion. Can-call is not assumed unconditionally.
Hidden/provided constraints remain abstract; partial/total arrows and non-null
types retain their specialized representation rules.

Assignments, method/function results, constructor arguments, default/constant
values, collection entries and lambda results enter through this type recipe.
Sequence covariance checks close the actual element constraints over the existing
sequence-index range, then derive sequence membership with the existing prelude
axiom. Hidden element constraints remain abstract. No collection/body terms are
searched for support. Quantified array/sequence initializer membership closes its actual
constraint checks over the existing index variables and range. Every guarded
piece is closed; no index variable escapes. Generated universal constraint checks
share the existing positive-universal assertion rule for the checked and assumed
predicate layers before applying that same binder/range closure. The ordinary
source-quantifier path calls the same extracted rule without changing its fuel
policy. A structural control compares recursive predicate layers with an explicit
universal assertion under both resolvers. Its original check-and-forget policy
is retained, so it adds no continuation membership assumption or summary. For
an initializer application, the frozen value's can-call domain is included in
the defining-constraint guard under that range. The existing separate domain
check remains mandatory; no domain fact is assumed unconditionally.

Conversions choose the canonical guarded constraint check or the legacy direct
check according to the option; they never emit both. Primitive range/integrality,
Unicode and ordinal obligations retain their separate necessary checks.

Allocation checks use the boxed predicate of explicit `allocated(e)`, with the
exact current, old or labeled heap. They check one representation. Existing
prelude allocation and box/unbox/type bridges supply the corresponding typed
fact. General allocation assumptions, arrow axioms, heap succession and where
clauses are unchanged. Fresh old arguments/receivers must still fail.

The local soundness argument uses the trusted splitter and existing type/allocation
introduction rules. It does not assert that finite tests prove unbounded trigger
closure, semantic soundness, or uniform solver performance. Independent soundness
review and native acceptance remain necessary.

## Producer audit

`obligation-producers.json` records registered assertion, contract, WF and
permission producers by source file, member, count, token hash and complete file
hash. Its normal Roslyn gate rejects changes and unclassified files. Explicit
capture is diagnostic only; the captured inventory must be reviewed and committed
before the normal gate passes.

| Family | Constructor and scope |
| --- | --- |
| explicit-predicates, canonical-proposition-core | Existing resolved-source assertion splitting, ordered WF, proof scopes and induction; explicit publication unchanged. |
| canonical-obligation-adapters | Actual guarded/exit checks and their permitted post-check publication; canonical allocation representation. |
| guarded-type-introduction | Visible defining constraint under its can-call guard, necessary base membership, then derived symbolic representation. |
| method-calls, method-override-contracts | Frozen actual/pre-call checks and local body exits; free interface copies; override/frame/termination implications retain their specialized proof rules. |
| function-contracts | Existing function precondition and SCC-limited postcondition checks; result membership through the type recipe. |
| assignment-initialization, constant-initializers, datatype-constructors | Ordered evaluation/WF and type recipe, including range-bound quantified initializer checks. Codatatype suspended membership retains its specialized rule. |
| loop-contracts, iterator-contracts, opaque-block-contracts | Actual invariant/yield/block/exit clauses use local assertion lowering; loop guards, iterator history/yield state, frames and publication retained. |
| ordered-expression-wf | Nullness, bounds, map/set domains, destructor queries and division/modulo use their operational Boogie constructors at ordered WF sites. These already express the same immediate scalar assertion predicates; they need no added proof package. |
| type-witness-conversion | Guarded visible conversion constraints share the recipe; primitive range, integrality, character and ordinal facts retain their exact operational formulas. |
| membership-frame-spec-bridges | Type membership adapters share the recipe. Type-test value translation remains a Boolean symbolic test rather than a proof obligation. |
| forall-proof-export, statement-wf-calculations | Actual source proof/calc checks use assertion lowering; arbitrary-bound-variable export retains its trusted scope and quantification; existence/update/heap-state obligations retain specialized proof rules. |
| termination | Lexicographic decrease/boundedness over frozen measures; specialized proof rule, not a single independently assertable source clause. |
| definite-assignment | Internal tracker-bit state invariant; no source-expression counterpart. |
| if-guards, match-completeness | Ordered guard/query/domain predicates, pattern membership and binding scopes; existential witness/completeness proof rules retained. |
| let-permissions, visibility | Exact binding/Skolem permissions and hide/reveal availability; neither is an extra proof of P. |
| value-translation, value-permission-translation | Value semantics, can-call traversal and heap/arrow representation; no body-term discovery or global fuel policy. |
| extreme-prefix-contracts | Rank/prefix proof construction retains its specialized rule and SCC boundary. |
| definition-consequence-axioms | Existing definitions, guarded consequences and result axioms; outside local-check migration and unchanged. |
| assertion-contract-sinks, diagnostics-only | Checking/filtering/provenance sinks retained. Display-only obligation ASTs are never used as operational predicates. |

This inventory supplies coverage and makes exclusions reviewable; it does not
replace semantic review of the specialized constructors or native paired tests.

## Fingerprints and required evidence

The fingerprint uses resolved binder identities and lexical unresolved-name
scopes, with depth counting declarations. Nested same-name binders cannot collapse
outer and inner variables; alpha-equivalent renaming remains equivalent. Function
applications, binary/unary opcode identities, quantifier kinds, ordered trigger patterns, attributes, types,
ground identifiers, heap/fuel, can-call and check/free roles remain significant.
Package-content comparisons also check guards/preparation separately. Complete
commands and typed declarations are separate evidence from pre-resolution hashes.

Acceptance requires the original issue 100 at its unchanged resource ceiling,
subset and method/function precondition positives without redundant assertions,
paired quantified and scalar checks, active reveal scopes, no-body-leakage and
no-duplicate-contract controls, and expected negative outcomes. Parser failures,
timeouts and resource exhaustion are not accepted negative evidence.

The supported baseline is `ab210b78b50adb5a192542897f0a00cdb40f3c35`.
Default-off suite/library verdict and resource comparisons and enabled sweeps must
use the exact supported fork, fixed Boogie and Z3 5.1.0. Stable errors are
investigated from generated Boogie before any repair. Cases partially successful
on both baseline and candidate seeds are resource/solver variance, not source
correctness repairs. Source hints, caps and batching remain unchanged.

Historical additive-design results are retained in the validation reports for
regression triage, and do not validate this replacement. PR #169 remains pending
approval and requires independent development-line evidence after porting.

Opaque contracts preserve the same fresh translator and consumed existential
adjustment from source-local WF through the sole guarded proposition check,
as declared exits do. Recursive-predicate equality controls exercise this
handoff under both resolvers; a nonrecursive predicate cannot establish fuel
identity. Original command publication remains separate from checked content.
