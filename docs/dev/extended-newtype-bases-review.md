# Extended newtype bases: source and verification review

The feature elaborates nominal newtype operations into ordinary base operations
and checked introduction. This argument is relative to the base type rules and
the independent repairs listed below; it does not establish completed validation
or consistency of the full verifier.

## Source map

| Responsibility | Source symbols | Required verification boundary |
|---|---|---|
| Default-off option and library compatibility | `CommonOptionBag.ExtendedNewtypeBases`, `DafnyCommands` | Both prerequisites are enabled; a library enabled for the feature requires an enabled client. Missing old metadata means disabled. |
| Base admission and nominality | `PreTypeResolver.ResolvePreTypeSignature`, `OperationPreType`, `IsConversionCompatible` | Admit the additional value families without changing ordinary nominal assignment or unrelated container compatibility. |
| Scoped instantiated ancestry | `NewtypeOperationView.Get`, `NewtypeDecl.ConcreteBaseType`, `GetTypeArgumentsForSuperType` | Substitute actual arguments at each layer; stop at hidden, cyclic, or unresolved ancestry. A nominal target retains its nominal type constructor. |
| Base members and patterns | `FindMember`, `BaseOperationExpression`, `PreTypeResolver.Match`, `MatchFlattener` | Preserve the declaring receiver and actual field/member signatures; own declared members retain the nominal receiver. Hidden operations are unavailable. |
| Exact arrow signatures | `TypeRefinementVisitor.BaseOperationType`, `FlowFromComputedArrowOperationType` | Project the exact declared base family/domain/result; do not broaden a contravariant domain by joining with an approximate signature. Opaque or unresolved views keep the current lower bound. |
| Cast and nominal result checks / predicate retag | `CheckResultToBeInType`, `CheckResultToBeInType_Aux`, `constraintValue`, `CheckSubrange`, `CheckWellformed` | Check the original candidate and substituted constraints before destination facts are assumed. Persistent nominal updates repeat introduction. |
| Membership and allocation | `AddRedirectingTypeDeclAxioms`, `GetSubrangeCheck`, `MayShowReferences`, `NormalizeToAncestorTypeChecked`, lambda/named-handle introductions | Instantiate the existing guarded schemas. Only exact own-base arrow membership is redundant; changed signatures and destination predicates still need proof. |
| Ordinal operations | `ConvertExpression`, `ExpressionTranslator`, `SplitExpr`, `TranslateBinaryExpr` | Preserve the entire ordinal for identity casts; retain finiteness, nonnegativity, subtraction, and nominal result obligations. |
| Codatatype discipline | `CoCallResolution`, `IsCoDatatypeIdentityConversion`, `IsSuspendedCoDatatypeIdentityConversion`, `CheckSuspendedValueMembership`, `MatchVerifier` | Exact identity chains may retain an already approved guard; consuming boundaries still check membership, and a constrained co-result cannot prove its own scalar constraints. |
| Witness and characteristics | `AddWellformednessCheck`, `Types.GetAutoInit`, `UserDefinedType.SupportsEquality`, `ExpressionTester` | Distinguish compiled/ghost/opt-out witnesses; propagate base characteristics and enforce inherited compilable constraints and runtime actual-argument injectivity. |
| Generic compilation and erasure | `IsExactDatatypeNewtypeRuntimeIdentity`, `TrExprOpt`, terminal nominal continuation, `SinglePassCodeGenerator.Expression`, runtime descriptors, `DatatypeWrapperEraser` | Exact full actual-base and semantic runtime equality; retain terminal Type/ToType and outer result type, evaluation count/order and own-trait fallback. Defaults/descriptors/boxing follow the instantiated base. |
| Resolution lifetime | `Cloner`, newtype resolved fields, resolver and language-server lifecycle tests | Fresh resolutions recompute option/scope/default facts; resolved clones preserve only the intended resolved-field metadata. |

These names identify the relevant implementation sites. Review them in the final
integrated source tree, including both resolver variants and applicable compiler
backends; a source map is not evidence that all those gates have passed.

## Candidate membership precedes destination facts

For a visible declaration `newtype N<A> = x: B<A> | p(x)`, existing membership
rules entail B<A> membership and the predicate's ordinary guarded callability
and truth conditions. The introduction site must discharge those conditions on
the candidate, not assume N membership in order to prove them. Every generic
layer uses its own actual substitution map; the raw declaration base is
insufficient when parameters are reordered or nested.

A representative narrowing check is:

```text
assert Is(original_value, Box(nat));
target_typed_local := original_value;
```

The first line must not instead name a fresh target-typed local whose membership
is already assumed. Constructor membership then recursively constrains nested
fields, lists, collections, tuples, and function-valued fields using the existing
schemas. An opaque target uses its nominal type constructor and actual arguments;
no hidden field or representation fact is exported to prove the assertion.

Constraint well-formedness remains load-bearing. The existing predicate check
can be guarded by callability; the declaration's ordinary well-formedness checks
must establish that guard on valid base candidates. A false callability premise
or inconsistent witness cannot be used to make introduction vacuous. A nominal
persistent update checks the updated candidate under the same rule, while a
base destructor or application retains its declared base result type.

## Ordinal and arrow projections

An ORDINAL-to-N-to-ORDINAL identity has the same complete ordinal at every step.
There is no `Offset`/`FromNat` factorization or new `IsNat` demand. Integer
introduction still checks nonnegativity before `FromNat`; ordinal elimination
to an integer still checks `IsNat` before `Offset`. Subtraction checks its usual
right-operand and offset conditions. If the result has nominal type N, its
predicate is checked on the resulting ordinal.

An arrow newtype's operation projection uses the exact instantiated domain,
result, and `~>`/`-->`/`->` family. The existing membership implication from N to
that same base makes a repeated own-base test redundant after source
well-formedness. Eliding that test requires a resolved visible newtype path and
full type equality retaining constraints and actual arguments. It does not
justify a partial-to-total cast, a stronger read frame, or a different domain or
result.

Same-base wrapping reuses the function value. A legal signature-changing
coercion keeps its ordinary base obligation and representation behavior. The
extension adds no selector-coherence, extensionality, or totality axiom to prove
new equalities between arbitrary function handles at different signatures.
Preserving a declared signature is distinct from proving a new equality theorem
about signature-changing coercions.

## Allocation, defaults, and codatatypes

The newtype allocation schema forwards to the actual base at the same heap. It
must not derive allocation from a predicate `true` or from empty reads.
The existing-language allocation repair adds captured-reference and previous/labeled
heap premises to specific function-value introductions and retains one-way
consequences. Reference classification follows the actual visible base in both
modes, preserving empty-frame arrow family identity. An opaque head's explicit
`(!new)` promise is an interface fact, never permission to substitute its hidden
representation. Checked ancestry handles finite repeated generic declarations and
mixed redirecting cycles without a primitive fallback. Heap
captures, nested arrows in datatypes/containers, and generic reference
characteristics remain relevant. Exact-base compiler erasure is a representation
argument; it is not an allocation proof.

An inherited default is permitted only for a bare declaration when the actual
base has a genuine compiled default. Constrained declarations keep their ordinary
witness obligations. A ghost witness and `witness *` cannot initialize executable
storage. Generic defaults and descriptors must agree with resolver and verifier
classification; arbitrary backend placeholder values are not witnesses.

Codatatype identity wrapping adds no constructor node, guard, rank, or eager
observation. A strengthening cast remains destructive for productivity analysis
when its predicate may inspect the value. In a co-recursive definition, the
validity of ordinary constructor fields must be established independently of a
consequence asserting the recursive result's membership. Withholding an
unjustified circular premise weakens the existing proof context; it does not
need a new background axiom.

## Dependencies and review controls

[Issue #132](https://github.com/erniecohen/dafny/issues/132) supplies the independently
reviewed function-allocation rule now integrated in source. Its one-way reads/result
consequences and specific captured-value introductions remain separate from this
feature. Both characteristic modes forward through exact visible nominal bases;
partial/total arrow subsets and explicit provided `(!new)` promises are retained.
Hidden definitions and cyclic/unresolved ancestry establish no reference freedom.
The rule is not accepted by source presence alone: direct function, boxed/container,
datatype-field and newtype-mediated controls plus fresh required full gates remain
necessary. A passing arrow application does not close that review.

[Issue #141](https://github.com/erniecohen/dafny/issues/141) owns existing datatype
narrowing membership, [#142](https://github.com/erniecohen/dafny/issues/142) owns
existing numeric character conversions, and
[#143](https://github.com/erniecohen/dafny/issues/143) owns the ordinary codatatype
field-check circularity. These remain separate existing-language repairs with
separate regressions and reviews. Their corrected rules must be consumed by the
feature; a feature-gated workaround cannot substitute for those repairs. No new
background schema is proposed by this integration argument.

Each positive universal contract needs a paired unsafe introduction and an
inhabited `assert false` rejection control. Scope controls must distinguish
provided versus revealed bases; generic controls must change actual arguments;
arrow controls must distinguish applicability, read frame, and allocation;
codatatype controls must reject invalid fields without losing productive valid
definitions. Default-off verdict and resource comparisons use the independently
repaired baseline. The final integration gates and public performance evidence
are separate obligations from this source/verification argument.

## Current focused evidence and publication boundary

The [b573 public matrix](https://github.com/erniecohen/dafny/actions/runs/37233978383)
accepts 336/336 cases at each source/proof stage, 140/140 backend builds,
420/420 matching runtimes and 140/140 matching C# steady profiles. C# updates
now match base allocations; generic tuple C#/Go failure is repaired. These are
exact-source focused results. Translator legacy CLI notices are retained, and
codatatype allocation profiling remains unavailable in its earlier corpus.

The [820 integrated controls](https://github.com/erniecohen/dafny/actions/runs/37233880455)
still cannot prove four generic pointwise arrow-coercion equalities in both the
direct and nominal controls, although the explicit lambda controls verify.
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

Both current product lines now contain the reviewed allocation source and admit
all three arrow families. The pending focused diagnostics and required full gates
still determine final acceptance; these older exact-source diagnostics do not
stand in for them.

## Exact predicate and terminal types

After instantiated base constraints are checked, an arrow predicate substitutes
the existing Boogie value at its declared instantiated binder type only when
source, wrapper and binder have identical full arrow types. This keeps reads,
requires and application on the declaration's heap convention. It changes no
handle, assumes no membership and does not erase a destination predicate.
Constraint callability and the declaration's ordinary well-formedness remain
load-bearing; a false guard cannot be treated as an inhabited source witness.

Compiler statement lowering separately retains a fresh terminal conversion with
the original nominal Type/ToType/marker and captured outer result type. It
recurses through the same writers and ordering, so exact lets become locals
without losing the backend's type/descriptor/boxing decisions. The strict guard
uses semantic equality with actual constraints retained, excludes own-trait
newtype paths, and rejects hidden/unresolved/cyclic or differing carriers.
The public b573 tuple and update controls measure this repair; they do not
justify erasing a signature-changing or cross-argument cast.

The suspended codatatype exception likewise requires every identity link's
Type/ToType agreement and a direct endpoint CoCall.Yes. Its constructor guard
comes from the existing resolver; no new guard is created. Only the added eager
carrier assertion disappears. All original predicate, constructor field and
consuming-boundary checks remain. The coinductive source argument is supported by the selected c240/c346
productive and unsafe controls above. It still needs the required fresh gates
on the final integrated source; the conservative empty-body finite-observation
loss and missing allocation profile remain separate limitations.
