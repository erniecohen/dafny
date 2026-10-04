# Extended newtype bases

The experimental `--extended-newtype-bases` option is disabled by default and
requires `--general-newtypes=true` and `--type-system-refresh=true`. The
[reference manual](../DafnyRef/Types.md#sec-extended-newtype-bases) describes its
nominal typing, operations, visibility, witnesses and runtime restrictions.

## Reduction to existing base rules

For `newtype N<A> = x: B<A> | p(x)`, a scoped operation view follows visible
aliases and newtype layers and substitutes actual arguments at every step.
It stops at hidden, cyclic or unresolved ancestry. The source type remains N;
the view does not redefine global nominal compatibility, container element
conversion or trait implementation.

Base operations retain their declaring receiver and instantiated formal/result
signature. Newtype members retain their nominal receiver. Pattern-bound fields
use the datatype's actual arguments. A nominal persistent update computes the
base result and then checks its newtype constraints, including ghost updates.
A predicate does not change match exhaustiveness or close the base operations
under that predicate.

Introduction checks the original candidate before destination type facts are
available. Every base and predicate check uses that layer's actual substitution.
Existing redirecting-type membership and allocation schemas are instantiated:
nominal membership entails actual-base membership and the ordinary predicate
conditions; allocation forwards to the actual base at the same heap. The
extension introduces no new background schema, extensionality principle,
constructor coherence theorem or arbitrary predicate-closure axiom. This is a
relative source argument, not a consistency proof for the complete verifier.

## Ordinal and function views

Ordinal identity casts preserve the complete ordinal, including non-finite
values. They do not factor through `Offset` and `FromNat`. Integer introduction
still needs nonnegativity; integer elimination needs `IsNat`; subtraction keeps
its ordinary right-operand and offset conditions. Nominal results check their
predicate on the resulting ordinal.

Function operations project the exact declared base family, domain and result.
The resolver owns a refinement proxy before parent flows capture a synthetic
conversion's type, and keeps its target synchronized with that exact signature.
An approximate supertype join must not broaden a contravariant domain.

Constraint substitution retags an existing Boogie handle at the predicate's
instantiated binder type only when source, value and binder have the same full
arrow signature. This preserves the declaration's heap convention for
`requires`, `reads` and application. It creates no new handle or assumed
membership. Exact own-base membership follows the already checked source;
family changes, different actual arguments, stronger frames and destination
predicates retain their checks.

Same-base wrapping preserves the function value. Signature-changing coercions
retain the ordinary base machinery and its current proof boundaries. In the
[public integrated diagnostic](https://github.com/erniecohen/dafny/actions/runs/37233880455),
the four pointwise equality assertions for covariant result and contravariant
input assignment/casts remain unproved in both the direct-base and nominal
controls; their two explicit lambda controls verify. The extension does not add
an axiom to prove those generic coercion equalities or replace the requested
assignment with a lambda.

[Function-value allocation (#132)](https://github.com/erniecohen/dafny/issues/132)
uses one-way allocation consequences and guarded introductions for specific
lambdas and named handles. Empty reads and predicate `true` alone cannot
establish function allocation. Lambda introductions require allocated captures
that may show references and suitable captured previous/labeled heaps; these
are sufficient introduction conditions, not an iff definition of all physical
captures. Nominal, boxed/container and datatype-field paths use the same rule.
Both reference-characteristic modes follow exact instantiated visible newtype
bases, preserving partial/total arrow identity and checked cycle recovery.
A provided head exposes only its explicit characteristics: an advertised
`(!new)` promise can be used without inspecting its RHS, while a hidden newtype
publishes no such promise. This is a separate existing-language repair, not a
feature-gated shortcut. The rule is integrated in source; final acceptance still
requires the fresh focused and full integration gates.

## Suspended codatatype values

Co-equality, prefix equality and lazy representation use the ordinary base
rules. An identity cast adds no constructor node or productivity guard. The
narrow identity rule preserves an existing resolver-approved `CoCall.Yes` only
through exact codatatype conversions ending directly at that call. Every link
has the same full constrained carrier and a destination with no nontrivial
refinement; its result type agrees with its target. Extreme/prefix predicates
use separate rules. ITE, match, let, helper and observation wrappers do not
qualify for this exemption.

The rule omits only the eager full-carrier check newly added at that direct
suspended identity cast. Operand well-formedness, constructor heads/fields,
actual parameters and both nominal result checks remain. It assumes neither
`MkIs` nor `canCall` in place of the omitted assertion. The English justification
is the existing coinductive typing argument for the same productive
constructor-field graph after identity erasure.

Destruction and consuming boundaries remain checked before type facts: field
observations, ordinary helper inputs, match sources, exact-let bindings and
substituted defaults establish existing membership independently. A constrained
co-result cannot prove its own field constraint from the suspended result's
membership. The corresponding paired productive/unsafe/nonvacuity controls and
VC review are required separately from this source argument.

The paired public [development diagnostic](https://github.com/erniecohen/dafny/actions/runs/37236893624)
at `c2400ea1c16327b2f2cab1d6ef9b483a01667fef` and
[shipped diagnostic](https://github.com/erniecohen/dafny/actions/runs/37237107328)
at `c346de8d07f4e38b8cba9bf485cc40b0a3f90b0a` accept the original nominal
codatatype positive source at 21 verified / 0 errors and runtime source at
5 verified / 0 errors. All five backends produce the matching runtime output
with wrapper erasure enabled and disabled: ten combinations on each product
line. These selected sources emit no Dafny-source warnings.

Separate empty-body universal finite-tail observation controls remain unproved:
raw feature-off and feature-on arms finish at 4 verified / 3 errors, and the
nominal arm at 12 verified / 3 errors, on both product lines. The second-node
Copy, MatchCopy and Count postconditions are semantically valid. The ordinary
callability fact for a future suspended recursive application is withheld by
#143; exact identity wrapping does not restore that premise. This is a
conservative automatic proof loss shared by raw and nominal programs. A body
that makes independently checked calls on the typed tail may recover the
contracts; such a proof does not erase the measured empty-body loss. These
results do not establish full codatatype proof completeness or allocation cost.

The subsequent body-only [development proof](https://github.com/erniecohen/dafny/actions/runs/37239169850)
at `875c752617ebda1ff529c32c5194e5c7fa18336b` and
[shipped proof](https://github.com/erniecohen/dafny/actions/runs/37239136136)
at `46e78bdb6b4759a86665059709881d391bc1dbf7` discharge the unchanged universal
contracts: each raw arm verifies 6 obligations and the nominal arm 14, with
0 errors and no Dafny-source warnings. The bodies make independently checked
calls on typed tails. Across those diagnostic corpora, all 150 development and
70 shipped backend/erasure runs exit 0 without warnings. Those totals include
other runtime sources; each original codatatype runtime still has ten matching
combinations. Recovering the contracts through checked proof bodies preserves
the separately measured empty-body automatic proof loss.

## Defaults, characteristics and compilation

Compiled witnesses are checked before use. Ghost witnesses and `witness *` do
not provide executable initializers. A bare declaration may inherit only a
genuine compiled base default. Resolver facts, generic descriptors and compiler
initialization must agree; backend placeholders cannot establish inhabitation.
Equality/reference characteristics and runtime comparability/injectivity follow
the actual visible base. Constrained wrappers over additional or opaque carriers
require invariant parameters, including phantom parameters. Existing cycle,
cardinality and wrapper-erasure restrictions remain active.

The common compiler optimizes an exact datatype identity only with resolved
views, full constrained actual-base equality, semantic runtime-carrier equality,
and no own trait on any newtype path layer. It lowers the operand's bindings in
the original statement context and order, then retains the conversion's nominal
Type/ToType/marker and the outer continuation result type at the terminal value.
The expression compiler uses the same scoped identity rule. This preserves
backend coercion/descriptor decisions and avoids repeating an expression or
adding closure layers. It does not change the verifier's original cast,
predicate, update or witness checks.

The [public benchmark run](https://github.com/erniecohen/dafny/actions/runs/37233978383)
at `b573bf6efadf0bdad83cb26188206e9daabd2771` uses deterministic sources under
[`.github/review/issue113-benchmarks`](../../.github/review/issue113-benchmarks).
All selected resolve/translate/verify cases and five-backend builds/runtimes
accept. C# root/nested/ghost update Work calls allocate 960048 managed bytes in
all five steady samples, equal to B and W-erased, after removing the former
4/4/6 closure layers. This is focused evidence for those unchanged universal
contracts, not a full repository or arbitrary-program performance guarantee.

Proof prices reflect separate checked-cast repairs as well as the feature.
Within that measured product, B/W feature-off and feature-on resource totals
agree. Comparison to the earlier source shows extra datatype membership
obligations and changed RU; those changes cannot be attributed solely to compiler
lowering. RU comparisons use the same solver. The translate harness emits a
recorded legacy CLI deprecation notice. Codatatype C# allocation profiling remains
unavailable in the earlier corpus because of lazy-cache warnings and entry-point
selection; successful unprofiled runs do not supply missing allocation evidence.

## Separate base-language repairs and lifecycle

[Datatype casts (#141)](https://github.com/erniecohen/dafny/issues/141),
[numeric conversions (#142)](https://github.com/erniecohen/dafny/issues/142) and
[codatatype field checks (#143)](https://github.com/erniecohen/dafny/issues/143)
are independent corrections to existing language behavior. Original-value
membership may reject formerly unchecked narrowing; character checks reject
invalid Unicode scalars while retaining valid supplementary values; suspended
co-call rules withhold circular field premises. These corrected losses must
remain visible and use separate regressions; hiding them under the feature
option would leave the original defects intact.

The feature option is recorded in `.doo` compatibility metadata. Missing old
metadata means disabled, and an enabled library requires an enabled client.
Prerequisites are checked before resolution. Operation/default facts belong to
one resolution lifetime: a fresh clone clears resolved characteristics, while a
resolved clone follows the established resolved-field policy. Flag, witness,
base and export changes require fresh scope-sensitive recomputation. No global
operation cache is keyed only by declaration identity.
