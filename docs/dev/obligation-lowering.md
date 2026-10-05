# Local obligation lowering

`--consistent-obligation-checks` is an experimental, default-off verification option.
It can also be set as `consistent-obligation-checks = true` in `[options]` of a
`dfyconfig.toml`. It changes proof construction, not the source language. It adds
no background axiom, changes no assertion-isolation setting, and increases no
global fuel or resource limit.

## Boundary and baselines

The development baseline is `858e4bfbcf00fbf0146255c0a4b0efdfc4deb66f`
(the actual full commit is recorded in the accompanying validation report).
The shipped-line baseline is `ad1360af0f776c0be6cf57e4f0d38bbdf1ecc365`.
The design was prepared against `4b2742fb9924eed47c7bd40779e4283a1e65ce6d`.
The option preserves the intervening soundness repairs; their guarded assumptions,
allocation/result restrictions, co-recursive boundaries and additional-axiom
policy are independent of this option.

The intended comparison is an unannotated immediate `assert P` before an implicit
check of the same instantiated P, with the same state and visibility. An
assertion before an intervening Q may legitimately help Q. Proof blocks, labels,
reveals, fuel overrides, custom triggers and selective checking keep their own
semantics. Both the inserted check and the implicit check belong to a comparison.

This implementation supplies a common local expression package for source
propositions and guarded visible type introductions. It does not claim equality
of all preprocessing or all unbounded E-matching closures of Z3. Specialized
Boogie obligations and hidden representations have the explicit boundaries
listed below.

## Package and prerequisites

`LowerProposition` constructs checked/free/both pieces using the existing splitter,
a continuation summary, and inputs describing the body context, translator,
induction, inlining height, preparation justification and optional guard. It emits
no command itself. Its semantic source remains the resolved expression; displayed
`GetAssertedExpr` trees are never used as operational predicates.

The expression translator supplies current, old and labeled heaps, receiver,
inter/intra-SCC fuel, self-call limit, frame scope and type representation. The
resolved expression already carries capture-avoiding substitutions, types,
triggers and attributes. Body context supplies hide scope and checking mode.

Preparation has three explicit categories:

* `CheckedExpression`: the explicit assertion's existing ordered WF path has run.
* `DeclaredContract`: use the established contract-WF procedure and frozen actuals
  or result variables. Clause order is preserved; a later clause cannot prepare
  an earlier one.
* `GuardedIntroduction`: check CC implies C for the defining type constraint.
  CC is recorded and retained on checks and publication, never assumed globally.
  Evaluation/WF of the frozen source value is a separate prerequisite.

The categories record the existing justification rather than granting a new
permission. Free splitter pieces are not independently established theorems.
Statement adapters check only checked/both pieces and publish the canonical
summary according to the original statement policy. Procedure adapters preserve
the original checked/free role conventions.

## Occurrence-local policy

The enabled translator does not read or consume `stmtContext` or
`adjustFuelForExists`. Legacy writes remain for default-off translation. Their
complete inventory is: statement-entry reset, assume/expect/assert entry and
continuation in `TrPredicateStatement`, function/quantifier translation in
`ExpressionTranslator`, and the quantifier case in `SplitExpr`.

The immutable policy is `(use, polarity, mayAdjustFuel)`:

| Parent edge | Child policy |
|---|---|
| conjunction/disjunction | same use and polarity for each sibling |
| negation | reverse polarity |
| implication antecedent/consequent | reverse/same polarity |
| forall range/term | reverse/same polarity |
| exists range/term | same/same polarity |
| ITE test | value use; both branches retain parent polarity |
| function/application arguments, receivers, exact-let RHSs and container contents | value use |
| can-call arguments and conditional permission guards | value use |
| equivalence operands in a check | positive check recipe for each operand |
| equality, continuation equivalence and other non-monotone operands | value use |
| old or labeled-old | same policy with the selected heap |
| descendants after fuel selection | mayAdjustFuel becomes false |

A positive existential or negative universal selects decreased check fuel or
increased summary fuel when direct translation must select it. The splitter's
quantifier rules select their own checked/free layers and prevent a second
selection below that root. Positive summary calls to quantifier-bearing predicates
select the existing one-layer summary offset. Siblings inherit the unchanged
parent policy. Custom fuel, visibility, `no_inline`, safe substitution, SCC height
and current-function limits remain enforced by the original machinery.

An equivalence check keeps the splitter's existing layer and translates each
operand with the positive check recipe. In particular, a recursive predicate at
L+1 can be compared with its defining existential body at L. The existing fuel
irrelevance axioms equate permitted layers, so the equivalence in either polarity
still denotes the same proposition. This is a local definition bridge, with no
new reveal, SCC permission, or global fuel increase. Its continuation uses value
operands; negative and non-vacuity controls exercise both equivalence polarities.

A Boolean argument has no monotone relationship to its enclosing function call:
its callee can negate it. Argument, receiver, exact-let RHS and container-value
edges therefore use the value recipe, including the terms in can-call permissions.
Non-Boolean expressions switch to value use at their translation root. Only
logical connective, quantifier and branch-result edges retain proposition polarity.
This preserves the argument's original heap, representation and fuel interface;
it does not equate a fuel-zero argument with a fuel-one argument.

The Boolean value fixtures start with the quantified Boolean proposition as an
explicit input premise. They check congruence of that value through calls,
containers, exact lets and membership, plus false alternatives and non-vacuity
continuations. They do not claim new existential witness inference: omitting the
premise in these examples is rejected by both the baseline and the enabled build.

Fuel usage is a result of a particular translation, separate from diagnostic
counters. Fresh roots/splits observe their own usage; their descendants share
that observation. An earlier unrelated translator counter cannot change whether
the current proposition reports a split.

## Local correctness and cuts

The core uses the existing splitter's correctness obligation: under its established
WF/visibility/recursion prerequisites, successful checked pieces justify the source
proposition and its permitted summary layers. Retaining SCC limits and check/free
roles is essential to this obligation; inlining is not a license to expose a
hidden definition. The implementation does not replace that trusted obligation
with a test of a few examples.

For a guarded type introduction, assume G only inside a proof of `G ==> piece`.
If G is false the resulting implication and publication are vacuous; no
unconditional can-call or membership follows. If G is true, the core's checked
pieces establish C in the same value/type/heap context. Thus publishing
`G ==> summary(C)` is a proof cut after its checks. The original `$Is`/`$IsBox`
check remains and must discharge through the existing introduction axiom.
For nonnumeric parents, parent membership is checked first. Numeric parent
constraints use `GetImpliedTypeConstraint`, the same authoritative combined
constructor used by the numeric type axioms, with generic substitution.

The value wrapper names a translated value; it does not reevaluate its source.
No result-type fact about a function's self call is published before checking
the body value. Function postconditions keep their SCC-height limit and
`CanCallOptions` allowance. The enabled permission traversal preserves that
allowance through exact lets, constant definitions and sequence initializers.
Dropping it can let a false self postcondition justify itself.

Local method preconditions use the frozen formal-actual map at the pre-call heap.
Their canonical checks occur in the existing call position; frame, termination,
boxing and old-allocation checks retain their order. The call's procedure requires
remain checked. Local postconditions are checked at fallthrough and explicit
returns before publishing their summary; procedure ensures remain checked.
Inherited conditions retain the `$_reverifyPost` guard. Filtering, dependency
information and source/error provenance remain on the assertion sinks.

For an immediate duplicate, the same instantiated proposition and complete
logical context give the same core pieces/summary. The first check establishes
only its permitted summary; the second is an administrative cut using that fact.
This is a local sufficient condition for the core's matching interface, not a
promise of identical resources or solver preprocessing for the entire program.
Guards and frozen-variable equalities must be included when comparing different
front ends; they cannot be silently removed by the comparison tool.

## Allocation bridge

Explicit `allocated(e)` and matching checked availability obligations use
`$IsAllocBox(Box(value), type, heap)`. The prelude already equates unboxed and boxed
allocation at values of the corresponding representation, and its box/unbox/type
bridges supply the representation fact. Inherently boxed values remain boxed.
The factory keeps the exact current/old/labeled heap and preserves the legacy
null/no-obligation result. Allocation assumptions, arrow axioms, heap succession
and general where clauses are unchanged. A fresh object at an old-argument or
old-receiver site must still fail.

## Producer audit

`obligation-producers.json` records every registered sink/contract/WF/permission
call, assertion construction and assertion-returning factory by file, containing
member, producer, count, token hash and complete file hash. The Roslyn test rejects an unknown producer
file, a changed recorded call or a new factory. Renamed wrappers returning an
assertion/contract are recorded as factories; changes to their bodies change the
hash. This is a source coverage gate, not a proof of every helper's semantics.

| Inventory family | Semantic constructor, prerequisites and status |
|---|---|
| explicit-predicates | resolved source proposition; ordered WF, labels and proof scopes; shared core |
| canonical-proposition-core | tactical conjunction/disjunction, safe body substitution, induction/prefix cases; shared core, existing proof rules retained |
| canonical-obligation-adapters | proposition package, guarded and method exit cuts, allocation fact; shared core |
| guarded-type-introduction | visible redirecting constraint and exact raw CC; shared core plus original membership bridge |
| function-contracts | instantiated requires/ensures and frozen body result; declared WF/self-call/SCC limits; shared core and type recipe |
| method-calls | frozen actuals, caller visibility and pre-call heaps; shared local core plus checked procedure requires |
| method-override-contracts | source contracts through shared spec lowering; frame/termination/override implications retain specialized constructors |
| forall-proof-export | arbitrary bound variables/range, proof checks through core, separate typed exporter; no local-variable fact escapes; forall assign/call algorithms retained |
| assignment-initialization | RHS/field/array/constructor result membership through direct type recipe; simultaneous-evaluation and quantified initializer algorithms retained |
| ordered-expression-wf | non-null, indexing, maps, destructors, numeric definedness, lambda domain/frames; direct operational Boogie factories retained in ordered WF; diagnostic ASTs excluded |
| type-witness-conversion | numeric/bitvector/Unicode/integrality/ordinal bounds and visible converted-value constraints; guarded constraints share core; primitive bounds and witnesses retain specialized rules |
| membership-frame-spec-bridges | symbolic membership, frame containment, type tests and spec split wrappers; source specs share core; axioms/type tests remain symbolic |
| loop-contracts | resolved invariants share split core; frozen for-index membership uses type recipe; lexicographic decreases and frame/order checks retain specialized rules |
| iterator-contracts | source yield/iterator contracts share split core and direct output type recipe; history/heap/yield-count state machine retained |
| constant-initializers | existing initializer WF before constrained result; direct type recipe |
| datatype-constructors | constructor-field WF then constrained result; type recipe; codatatype suspended-membership rules retained |
| termination | lexicographic decreases, boundedness, old/current measures; specialized operational Boogie constructors, no new source predicate/inlining |
| definite-assignment | tracker bits and return availability; internal state invariant, no user-expressible predicate counterpart |
| if-guards | ordered guard WF and binding-guard existence/scoped variables; existing constructor retained |
| match-completeness | constructor query/coverage, pattern and binding types; direct type recipe for bindings, specialized completeness constructor |
| statement-wf-calculations | source calc/assign-such-that checks share splitter; update/existence/heap transition checks retain specialized rules |
| opaque-block-contracts | declared block ensures via split core; block frame/WF and visibility scopes retained |
| let-permissions | exact bindings and separately justified Skolem permissions; self-call context preserved; no witness conflation |
| visibility | reveal/hide availability facts; deliberately retain existing scoped proof rule |
| value-translation | arithmetic/logical operand representation; occurrence policy, no assertion recipe guessed from a value |
| value-permission-translation | heaps/arrow requires/quantifier guards and can-call traversal; values retain original semantics and complete context |
| extreme-prefix-contracts | prefix lemma/predicate proof rule and rank; existing specialized proof construction retained |
| definition-consequence-axioms | definition/result consequences and availability guards; outside local-check migration; no axioms changed |
| assertion-contract-sinks | provenance, checking/filtering/subsumption and checked/free emission; sinks retained |
| diagnostics-only | displayed obligation explanations; deliberately excluded from semantic construction |

Two direct membership producers stay deliberately specialized: type-test value
translation is a symbolic Boolean test, and quantified array/sequence initializer
membership binds all indices under its range with forget/subsumption semantics.
The latter has no single already-evaluated scalar point to which the direct
introduction recipe can be applied safely. Its arbitrary-index recipe is retained;
the scalar constructor/collection/lambda adapters do not claim to cover it.
Partial/total arrow subtypes, non-null declarations and provided/opaque types
also retain their specialized or abstract symbolic introduction rules. An AST
containing a hidden constraint does not authorize exposing it.

Higher-order `RequiresN` has a shared semantic constructor and uses current heap for both `.requires` and call WF.
`ApplyN` selects `$OneHeap` for read-effect-free functions; that distinction is
intentional. Arrow type arguments, receiver, arity and boxed actuals are preserved.
`.reads`'s domain requirement is a separate check; it is not discarded because
the result is a set. Its declared frame and termination prerequisites remain.

## Evidence and acceptance

The typed fingerprint normalizes lexical binder alpha-renaming only. It retains
function/operator applications, quantifier kinds and ordered multi-patterns,
attributes, types, ground identifiers, `Lit`, box/unbox, membership, heaps, fuel,
can-call and checked/free roles within the pieces and summary. The package
content fingerprint excludes the separate input guard and preparation metadata;
pair tests check those explicitly, and complete command emission retains them. It substitutes no administrative
temporary or independent witness. Complete Boogie emission and selected passive
Boogie/SMT inputs accompany the structural package tests; proof dependencies and
scope availability are reviewed in command emission as well as expression content.
The observer runs before Boogie resolution, so some internal expression type
fields are recorded as `unresolved`. The content fingerprint alone therefore
does not certify complete typed package equivalence; the emitted declarations,
preparation/guard checks and command-level comparisons remain separate evidence.

The paired runner preserves original examples, records hashes, commands, solver
checksum, exit/outcomes and total/maximum-batch resource counts. The unchanged
#100 source must pass at the existing 16,000,000 limit, with no batch isolation.
The final quantified assertion and `assert true` are explanatory comparisons.
Subset #2107's short/recursive variants and #4217/#2170/#2185/#5148 remain separate
cases. Historical reports are baselined before interpreting a success as a fix.

In #100, the final explicit assertion supplies a local postcondition check and
its permitted quantified summary before the procedure's ensures check. The
enabled method-exit adapter supplies that same local recipe without changing
the source. The forall proof body also retains an obligation continuation when
it is the method's final statement; its local scopes no longer depend on a later
assertion. The original checked procedure ensures and quantified export remain.
The recorded successful reproducer uses one ordinary batch for the lemma; a
new assertion-isolation setting or fresh per-assertion budget is not its repair.

The complete let-bound self-postcondition contradiction tracked in
[issue 166](https://github.com/erniecohen/dafny/issues/166) is a separate,
pre-existing default-path soundness defect. The enabled permission traversal
rejects it; default-off compatibility does not imply that the baseline theory
is sound.

Negative controls cover false/recursive subset constraints, guarded permissions,
casts and both sides of numeric bounds, non-null/index/domain/destructor/division
checks, ordinary/higher-order/receiver/generic preconditions, fresh old arguments,
self postconditions, hide/reveal/proof scopes, early returns, loop and yield
contracts, and empty forall domains. Parsing failures, timeouts and resource
exhaustion are not accepted negative evidence.

The required compatibility evidence includes the verifier suite and standard
library with the option off, both additional-axiom settings, controlled resource
comparisons against the exact baseline, and an enabled sweep with changed verdicts
and cost regressions reported. A diagnostic workflow wrapper that exits zero is
only a container for outcomes. The validation report must say which underlying
gates completed and which remain pending. Independent soundness review is
required before merging; tests and this local argument do not replace it.
