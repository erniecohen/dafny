# Finite source proposal for normalized opaque ground projection

This describes the second preparation phase after [typed floor/embedding preparation](real-roundtrip-plan.md).
It is an English source design for review, not an executable checkpoint or
runtime approval. The source parent is exactly
`d115e6001fabdb61ae96c76e8798d90637386df3`. Phase A's 76 focused controls
passed, but no actual Real worker/corpus gate has accepted the product.

## Exact composition

Original G3 and unsigned/If partition validation remains first. Phase A performs
its existing bounded owned capture, all-mask floor preparation, independent
relation check and before/after original-source association. Phase B consumes
only the owned requests returned by that operation, not live original requests.
It does not certify or reinterpret any original source occurrence.

Extend the internal `B3RealPreparedRequests` result with the immutable array of
immutable definition-origin arrays that Phase A already captures in its
`SourceWitness` objects. The only caller uses those owned arrays and the existing
mask IDs. Do not obtain new definitions by rereading a live context after Phase A.
All definition-origin fields are immutable strings. Existing Phase A requests,
evidence and transformation behavior remain unchanged.

Add `B3OpaqueGroundProjection.cs` for the bounded producer and
`B3OpaqueGroundProjectionRelation.cs` for independent classification/lockstep
validation. Add one focused test class with the fixed 15 methods/47 cases listed below. Add this public English model
argument, exact source manifest, release note, and the required issue regression
entry/expectation. This checkpoint adds no CLI option, protocol constructor,
native worker feature, source corpus case, worker fixture, or workflow routing.

The proposed internal entry is `Prepare(ownedPhaseAResult, token, limits)`,
consuming the controlled Phase A result with its immutable requests, mask evidence
and captured definition arrays. It returns immutable submitted requests and flat immutable
per-mask evidence: producer version, mask ID, input program hash, output program
hash, applied flag and decline reason. Classification recipes remain internal
and immutable. A separate relation entry accepts original input, target,
evidence, an independently supplied expected mask ID, the exact owned
definition-origin array and bounded work allowance;
claimed hashes or removal lists do not supply a semantic license.

Retain the already bounded immutable Phase A input requests rather than allocating
another array of captured copies. Enumerate/count the complete owned inputs under
the shared 200000-node/64MiB limits before classification recipes or targets are
allocated; typed/hash validation remains mandatory. Existing `CaptureForRelation`
does not expose aggregate totals and must not be used to collect up to16 full
copies before checking aggregate admission. The separate relation checker may
capture one original/target pair at a time, each with its existing per-request
node/slot/byte caps, releasing that pair before the next mask. Relation work and
any retained relation metadata still share the original-unit allowance. Mask IDs
must be exact unique nonempty bounded strings and all inventories must agree.
Inputs originate only from the controlled immutable Phase A result. Association
checks never claim atomicity against arbitrary ABA.

The backend work key binds both producer versions, each Phase A original and
output hash, each Phase B input and final output hash, mask ID, final owned
configuration digest, and package fingerprint in original mask order. Only the
Phase B submitted requests reach the existing coordinator. Request IDs, unit,
package identity, configuration, obligations, solver settings and original-unit
deadline are retained exactly. No retry, fresh budget or new solver is introduced.

## Exhaustive admission grammar

Typed validation precedes optional classification; malformed data rejects rather
than being repaired or silently accepted as an unchanged request. An ineligible
well-typed program declines to its unchanged owned Phase A input.

Every listed type, every declared function and all parameter bindings, every unit
binding, and every body occurrence are examined, including unused declarations
and statements in otherwise unreachable positions. No dictionary name convention
or historical packet hash is an admission rule. String tables use Ordinal equality
and exact declarations; protocol-validated names cannot be shadowed in this grammar.

* Axioms must be empty and owned definition origins must be empty. No definition
  from another mask or context may be borrowed. Each opaque sort is an exact
  member of the validated `Program.Types` list. The current `RawAstBuilder` maps
  that entire list to user TypeDecls with no domain instantiation; domains/taggers
  are empty and functions have no definition/tag or injective parameters. Reject
  every native word type and word expression anywhere; malformed reserved names
  are already rejected by protocol validation. Bool, Int and Real are primitives,
  never opaque. This argument assumes exactly the current reviewed worker route;
  future interpreted domains/tags/theories require a new review.
* Every function parameter is one of those opaque sorts; its result is an opaque
  sort or Bool. Nullary functions are allowed. Unused functions with primitive
  parameters or Int/Real/word results decline. Applications must refer to the exact
  unique declaration and validated signature; no detached name or unknown symbol
  can be interpreted as a permissive predicate.
* Every Int/Real unit binding is retained in original order. Every opaque binding
  and every Bool binding is removed only after the complete dependency audit.
  Bool bindings are allowed solely for executed literal/copy assignments below.
* Statements are nested Block, Assign, Assume, Check and exactly one terminal
  Return in flattened statement order. No Havoc, Choice, Conditional, Loop,
  Labeled or Exit is eligible. The Return may be nested in ordinary Blocks.
  Empty Blocks after it are allowed, but no further non-Block statement is allowed;
  more than one Return or any Assign/Assume/Check after Return declines. Flattening
  is a bounded classification walk; original Block topology is retained.
* Retained numeric expressions contain only Int/Real unit variables, Boolean/
  Integer/Rational literals, existing well-typed native Operation nodes and Label
  nodes. All operator argument occurrences are recursively audited, even ITE
  alternatives and algebraically irrelevant operands. No Application, Quantifier,
  Let, word expression or removed variable occurs. This grammar may produce Bool
  conditions but has no retained Bool variable. Existing operator meanings and
  literals are preserved exactly; there is no Phase B arithmetic rewrite.
* An Int/Real assignment is retained exactly and its RHS fits that numeric grammar.
  An opaque assignment has ground opaque RHS consisting only of exact opaque
  variables and exact declared applications with recursively opaque arguments.
  It is represented by an empty Block at the same slot, while the model extension
  executes its original singleton-valued assignment.
* A Bool assignment RHS is a Boolean literal or a copy of a Bool binding definitely
  assigned earlier in the flattened prefix. Track a finite assigned-name set in
  statement order. Copy from an unassigned binding declines. Reassignments and
  self-copy after an earlier assignment are allowed, since extension executes old
  values in normal assignment order. No Bool function application RHS is admitted.
  No retained expression may read any Bool binding.
* Assumes are recursively separated only at native typed binary And nodes. Eligible
  erased leaves are literal true, positive equality of same-sort ground opaque
  terms, or a declared Bool-returning function applied to opaque ground arguments.
  Replace each such leaf with literal true, retaining And topology and child order.
  Every remaining leaf must fit the numeric grammar and stays exact. Numeric false
  remains; false is never an erased leaf. Mixed Not/Neq/Or/Implies/Equiv over any
  opaque term, removed Bool read or unsupported constructor declines. Label is
  admitted by the numeric grammar, not used to tunnel an erased opaque leaf.
* Every Check fits the numeric grammar and stays exact, including ID, Learn,
  expression, occurrence slot and original SourceIdentity. No check is erased,
  replaced by true or privileged for having literal-false syntax. Obligation
  arrays/order are identical, even when an ID occurs at multiple body positions.

Target types and functions are empty, with no introduced declarations. Retain all
numeric bindings in original order. Keep every original Block and Return slot,
all retained Assign/Assume/Check objects, and original unchanged expression children.
Only removed Assign slots become empty Blocks and erased Assume leaves become true.
The target is typed/hash validated. Check ID order and expression bytes must match
the complete original occurrence walk, not merely a set comparison.

## Universal model and prefix argument

Take any projected numeric state/model/execution at a Check prefix. Interpret each
removed sort as its own singleton, every opaque-result function by its result
singleton, and every opaque-only Bool-result function as constantly true. Give
opaque initial variables their singleton values. Execute all removed opaque and
Bool assignments in original order; definite assignment makes each admitted Bool
copy meaningful without an invented initial value. Every erased equality,
predicate or true Assume holds at every prefix. No retained term reads an erased
binding or symbol. Retained transitions, assumptions, goals and learned facts
therefore have exactly their projected interpretations. This extends algebraic Real
models without substituting rational witnesses. Conversely restricting any full
execution/model to retained numeric bindings preserves all projected transitions
and goals, since removed assumptions become true. Per-check validity and existence
of a satisfying negated goal context are equivalent in this typed normalized model
space. A false learned numeric fact makes both later contexts inconsistent alike.

This does not reconstruct omitted Dafny/Boogie prelude constraints or establish a
full-source countermodel. It does not establish that the configured arithmetic
solver finds a satisfying model. Unknown remains unsuccessful and all original
Real expected verdicts stay unchanged.

## Independent relation and bounds

The relation checker reconstructs declarations, ground/numeric expression grammar,
definite assignment, terminality and positive Assume classification independently
from the bounded captured input. It must not invoke the producer classifier or
accept its claimed slot recipes as proof. It walks input/target Blocks in lockstep,
requiring exact retained kinds, child counts/order, expressions, targets, checks,
Learn and obligations. Empty replacement Blocks are allowed only at recomputed
removed assignment slots. True replacement leaves are allowed only for recomputed
uniformly true opaque leaves. Other structural or declaration changes reject.

There is also an explicit unchanged/fallback relation branch: Applied=false must
mean the complete original Request and target Request are byte-exact equal, with
matching input/output hashes, expected producer and independently supplied mask
ID. A semantic decline or cap fallback need not meet the transformed grammar.
This branch never licenses a changed request. The transformed branch requires
Applied=true, different input/output program hashes, unchanged headers/config/
obligations, and the complete independently recomputed classification above.
Evidence may not supply its own expected mask ID; reuse under another mask rejects
even when two masks have identical program bytes. Decline reasons describe a
fallback, and cannot authorize any mutation.

Existing request limits remain 100000 nodes/depth128/32MiB and aggregate
16 contexts/200000 nodes/64MiB. Proposed optional work maximum is 800000 events per
original unit, shared across all masks and separate from Phase A's bounded work.
Classification charges every declaration, signature binding, expression/statement
occurrence, slot record and set/table membership action before allocation/insertion.
Witness slots are capped at 200000 aggregate, with an injectable smaller limit for
the fixed below/at/above controls. Metadata bytes for each occurrence are charged
conservatively at six bytes per UTF16 unit plus quotes against the same aggregate
64MiB witness budget before any output encoder allocation. Limit injections cannot
exceed production caps and must be positive.

Source recipes count complete projected output nodes/depth/bytes and reserve the
independent relation work before materializing any target. Projection does not
duplicate input subtrees; counts include each actual occurrence and replacement
slot. Metadata/config/declaration headers remain subject to the same bounded
serialization and aggregate allowance. Maximum retained storage is bounded input
snapshots plus admitted targets plus capped flat recipes/evidence. A semantic
decline affects only that mask and forwards its exact owned Phase A input. If any
optional unit work/witness/growth/aggregate cap is exhausted, discard all Phase B
recipes and use all owned Phase A inputs, never a partial transformed unit. Invalid
input, changed association, incorrect hash, failed independent relation or malformed
evidence rejects the whole preparation as Unsupported. Existing Phase A behavior
and fallbacks are not weakened.

## Review and execution boundary

Freeze this design and the exact 15-method/47-case census below before
source implementation. Bind the source parent/current hashes and all new source
files in a product manifest; regenerate the fork product base for the actual new
product commit. Do not edit or waive old tests, source expectations, protocol, vendor,
worker, solver or corpus. No build/test/verifier execution is claimed by this source checkpoint.

Source-only implementation follows independent semantic review. Complete
independent actual-source review must precede publication. A later separate English
routing design will select exact focused compilation/structural controls and strict
worker/source anti-vacuity fixtures; this proposal does not approve routing or a
runtime dispatch. Full/default/library/corpus/backend acceptance stays pending.

Clarify the existing six-row unsafe-assumption theory without changing its method
name or47-case denominator: the `removed false` row is a non-erasure control.
It supplies literal numeric false in an exact And assumption topology and requires
that false leaf remain exact (and rejects a forged target replacing it by true).
It must not assert decline merely because false is present. The other five rows
continue to require semantic decline for their unsafe opaque/Bool dependencies.

## Fixed structural census

These are fixed source controls; none is reported as executed by this checkpoint.
The numeric-false row requires exact retention and rejects forged erasure, while
the other five unsafe-assumption rows require an unchanged semantic decline.

Phase B: 15 structural methods / 47 cases:

| Method | Exact rows / cases |
| --- | --- |
| `WholeOriginalIrrationalContextHasSingletonExtension` | Fact (1) |
| `PositiveOpaqueEqualityAndPredicateConjunctionsAreEligible` | equality, predicate, conjunction (3) |
| `RemovedBooleanCopiesAreDefinitelyAssignedAndUnused` | true chain, false chain (2) |
| `RetainedChecksLearningOriginsAndPrefixOrderRemainExact` | Fact (1) |
| `FullRealCarrierAndIrrationalConstraintRemainUnchanged` | Fact (1) |
| `UnsafeRemovedAssumptionGrammarDeclinesProjection` | numeric false non-erasure, Not predicate, opaque Neq, Or predicates, Boolean read, implication (6) |
| `NumericCoupledFunctionSignaturesDeclineProjection` | Int argument, Real argument, Bool argument, Int result, Real result (5) |
| `AxiomsAndUnknownTypeDependenciesDeclineProjection` | active axiom, unused axiom, native word type (3) |
| `RetainedDependencyOnRemovedVariableDeclinesProjection` | opaque Check equality, Boolean-copy Check read, numeric ITE reading removed Boolean (3) |
| `OpaqueAssignmentTypeOrSymbolMutationIsRejected` | changed signature, undeclared function, assignment type mismatch (3), invalid typed inputs |
| `NonStraightLineControlDeclinesProjection` | Havoc, Choice, Conditional, Loop, Labeled, early Return (6) |
| `BindersWordsAndRetainedApplicationsDeclineProjection` | Quantifier, Let, word operation, numeric UF application (4) |
| `GroundCertificateIsBoundToInputAndFinalBytes` | original hash, output hash, inserted Assume, changed Check (4) |
| `ProjectionBoundsDeclineWithoutAllocatingOversizedWitness` | below, at, above same witness limit (3) |
| `G3DefinitionContextsCannotBorrowSingletonCertificate` | nonempty definition mask, different-context certificate reuse (2) |


## Implemented source checkpoint

The producer version is `opaque-ground-singletons-1`. The producer and independent
checker are in `Source/DafnyCore/Backends/B3/B3OpaqueGroundProjection.cs` and
`B3OpaqueGroundProjectionRelation.cs`. `B3RealContextPreparation` returns its
already captured immutable definition-origin arrays. `B3VerificationBackend`
composes the two preparations after original partition validation and binds both
versions, the original/Phase A/Phase B hashes, mask order, final owned configuration
digests and the package fingerprint. The shared original-unit deadline and
completion mapping are unchanged.

The first audit requires every nested collection to be an initialized
`ImmutableArray`, matching Phase A's controlled producer. It walks all expression
and statement constructors, including unsupported barrier subtrees, before any
hash/JSON encoder or recipe. It rejects inconsistent base/derived expression types.
It charges the original 100000-node/depth128 and 400000-slot per-request bounds,
200000 nodes/800000 slots for all masks, escaped-string upper bytes and bounded
actual serialized request bytes. Mask/evidence/definition strings are included in
escaped-text admission. This is a controlled producer boundary, with no arbitrary
atomicity or source-certificate claim for caller-forged result records.

Before target arrays, all masks receive a conservative output reserve of escaped
text plus512 bytes per visited node,64 per input slot and4096 per header. Copies
and replacement slots are counted by occurrence; projection introduces no larger
expression subtree. The same32MiB/64MiB and depth/node bounds apply. Recipes and
table/set insertions are charged to a200000-slot and800000-work shared optional
allowance. Relation allowance is reserved as12 times admitted nodes plus4 times
admitted slots plus128 per mask, before target materialization. Optional reserve,
work or witness exhaustion forwards every owned Phase A request unchanged.
Well-typed semantic ineligibility forwards that mask unchanged. Invalid ownership,
association, types/hashes/evidence or a failed relation rejects preparation.

The independent relation captures just one original/target pair per call, under
Phase A's bounded capture admission, and releases that local pair before proceeding
to the next mask. Its budget is shared across the original unit. It reclassifies
all declarations, the ground/numeric grammar, definite Bool assignments and exact
terminal Return independently. It compares retained expressions in lockstep rather
than serializing every ancestor or trusting producer recipes. An unchanged request
has an explicit byte-exact relation branch; it need not meet the projected grammar.
No extra relation pair is retained to obtain an aggregate snapshot.

`B3OpaqueGroundProjectionTests.cs` has exactly the15 methods/47 cases above. The
fixtures construct current schema3 typed IR; they do not claim to be a historical
Dafny emission or a worker verdict. The numeric-false row retains the false child
of an And while erasing its opaque predicate sibling and rejects a forged true
replacement. The below/at/above rows use one measured witness-slot requirement,
with an additional assertion that exhaustion falls back across16 masks. The
malformed-typed row also rejects a base/derived Type disagreement.

`git-issues/git-issue-124.dfy` contains two original source checks for the ordinary
backend: reassertion of its unchanged `x*x==2.0` precondition, and literal false
under the realizable `x==0.0` precondition. The proposed expectation requires the
positive filter to verify and the negative filter to report one assertion error.
That expectation is unexecuted. The existing B3 irrational corpus, solver options,
old expectation files, workflows, protocol and vendored worker/library sources
are unchanged. The prior Phase A manifest remains historical; the separate Phase B
manifest binds current composition and unchanged correspondence dependencies.

This checkpoint has no compilation, structural-test, proof, worker or corpus
execution evidence. Unknown remains unsuccessful. Native/source parity, proof
cost and Real acceptance require separately reviewed runtime gates.
