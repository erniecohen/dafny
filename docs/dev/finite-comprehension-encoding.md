# Finite collection definitions

Issue [#82](https://github.com/erniecohen/dafny/issues/82) concerns an unrestricted
membership-preservation law from backend Boolean maps to finite sets. The
conversion function remains total, but its membership equations are supplied
only for actual finite source expressions under their source contexts.

## Relative model and obligations

Interpret `Set` as finite subsets of the box carrier. For a backend map `m`,
interpret `Set#FromBoogieMap(m)` as its support when that support is finite, and
as the empty set otherwise. The latter choice is a model construction; it is
not asserted to the solver. A site-specific defining equation is satisfied
when the characteristic predicate has finite support. This is an argument
relative to the remaining typed backend interpretation, not a consistency
certificate for the complete Dafny prelude.

For each admitted definition the translator must establish:

1. **Finite support:** the predicate selects finitely many canonical boxes.
2. **Admission:** source type, allocation, range, permission, and map-collision
   obligations have been checked or occur in the enclosing availability guard.
3. **Identity:** the equation defines the actual translated set/domain, with
   the same heap, type arguments, captures, fuel, and reveal environment.

The definition is outside its own comprehension witnesses, so an empty source
witness type still defines an empty result. It stays inside all enclosing
source guards, including empty outer types, branch conditions, good heaps,
function availability, and lambda argument conditions. A finite map equation
defines the key domain of its actual `Map#Glue` value; value equality is required
only at selected keys. Duplicate keys with equal values remain legal.

Simple typed-unbox predicates retain their source `IsBox` guard. That guard
provides the inverse boxing law needed to make each selected raw box canonical.
General images use equality with the actual boxed source term. The general-map
witness relation includes source types and required allocation, and its chosen
witness depends on the complete translated relation, including captures.

## Map choices and captured environments

The domain of a general map is the boxed image of its source key expression.
For each key, its value is obtained from one source witness satisfying the
same typed range/key relation. Every component of that witness receives the
complete translated relation as its argument, so a single tuple choice can
satisfy their coordinated property. Equal key expressions at different source
environments do not force an incompatible shared witness.

The old representation passed only the key to per-site witness projections.
For a source function with body `map x: int | x == n :: 0 := x`, the chosen
witness for key `0` consequently had no argument for the captured `n`.
Using the source function at both `n = 0` and `n = 1` could constrain that
same projection to equal both integers. The analogous defect occurs for
`imap`: this choice problem is separate from the finite-support conversion
principle. Removing only the finite-set bridge cannot repair it.

The capture regressions request the two legitimate map values and then assert
`false`; the repaired representation must preserve both values while rejecting
that final assertion. Choice is a function of the complete witness predicate,
including key, heap, type constraints, and source captures. This can be
interpreted by selecting one tuple from each inhabited relation and returning
its components; no injectivity assumption about source keys is required.
The collision obligation still requires only that equal keys have equal values,
so repeated keys with identical values remain accepted.

The translator materializes the relation as a family indexed by the boxed key:
`family[key]` is the complete typed witness predicate for that key. Both the
map value and its choice property use the same translated family. The property
names that family by a universally quantified alias constrained to equal the
actual source family, and triggers on each `projection(family[key])` access. Thus value use directly activates the joint
choice while Boogie's trigger resolver never traverses an inline lambda body.
Lambda lifting preserves the family's current heap, type, and source captures
as arguments; it does not rely on equating unrelated characteristic maps.

The inhabitance premise uses equality elimination when a witness is fixed by
a source equation: `exists x :: x == t && P(x)` becomes `P(t)` if `t` is
independent of `x`. All source type, subset, allocation, range, and boxed-key
constraints in `P` remain after substitution. Fresh declaration identities
prevent capture, and self-dependent equations retain their existential binder.
This avoids asking the solver to invent an arithmetic witness solely to enable
an already justified choice fact; it introduces no additional choice principle.

For a direct tuple key containing each source witness exactly once, the
translator also exposes candidate witnesses obtained from the corresponding
tuple fields of the unboxed key. The premise retains the original existential
and adds the complete predicate at those candidates: `E || P(candidate)`.
Since `P(candidate)` implies `E = exists witnesses :: P(witnesses)`, this is
logically the same premise. Every source type, subset, allocation, range, and
canonical boxed-key equation remains in `P`; no tuple inverse or off-domain
key assumption is introduced. Field positions follow constructor arguments,
including when bounds discovery reverses the source binders.

## Aliases in solver patterns

Finite-view and lambda-family aliases are universally quantified and guarded
by equality to their actual translated characteristic map or handle family.
For any context `G` and source term `t`, the formula
`forall a :: a == t && G ==> Facts(a)` is equivalent to the original
`G ==> Facts(t)`: substitute `t` for `a` in one direction, and substitute the
premise equality in the other. The alias adds no arbitrary-map membership law
and preserves all enclosing source guards and allocated-image premises.

The quantified alias remains a variable in solver patterns. A local `let`
instead expands before pattern checking; after lambda lifting, a conditional
capture can then expose an interpreted `if` expression inside a pattern.
Equality-guarded aliases prevent that expansion without changing the source
value, predicate, choice relation, or availability condition.

An outer finite comprehension can capture a source binder in an expression
such as `n + i` while its nested choice or finite-view fact has only alias
patterns. If no source trigger is available, the common permission quantifier
uses a multi-pattern of canonical boxes for its retained native integer, real,
or Boolean binders. This fallback requires finite source bounds and known
nonempty binder types. It preserves the existing quantifier body, all source
and type guards, and the prior binder-trimming result. Explicit or disabled
source triggers, unbounded witnesses, and modeled box representations receive
no fallback.

Pure arrow calls use the distinguished `$OneHeap` selector. An additional
multi-pattern pairs that exact application with `IsGoodHeap(futureHeap)` to
cover the otherwise absent future-heap binder. It changes matching only:
the consequence still requires allocated typed formals and the actual
formation-heap succession. Ordinary selector patterns are retained, and
arrows with a nonempty reads clause receive no such pattern.

Pure-arrow subtype checks can need child permissions or datatype constructor
facts before producing a result application. A lambda's inferred source result
can also have a base type while its required result retains a subset constraint,
so their selector type arguments differ. For a lambda with no reads expressions,
an additional multi-pattern uses `AtLayer(family, layer)`, the native `IsBox`
type of every boxed formal, and `IsGoodHeap(futureHeap)`. It instantiates the same
universal consequence independently of the target arrow's result type. The
source types, body, exact closure family, allocation, range, and formation-heap
guards are unchanged. No inhabitant is assumed, so an empty domain grants no
additional child permission.

Heap-independent source definitions sometimes require a translator heap for
lambda or application syntax. They use `$OneHeap` as a placeholder; their
well-formedness proof remains generic in a good source heap. For these exact
placeholder contexts, that proof can be instantiated with the chosen good
formal-argument heap. No succession from `$OneHeap` is needed. Actual current,
previous, and labeled heap contexts keep their formation-heap relation, and
all argument allocation, type, range, and child permission guards remain.

For a lambda with no reads expressions, its constant-empty footprint definition
`D` has no free formal, heap, layer, or range variable. The translator emits `D`
once outside the guarded child facts: `D && (forall x :: G ==> D && F)` is
equivalent to `D && (forall x :: G ==> F)`. This also removes its duplicate inside
a range-conditioned branch while retaining every child and range fact and guard.
When there are no body or range call facts, `D` alone entails the whole guarded
consequence, so that redundant quantifier is omitted too. The value,
precondition, and permissions are unchanged.

Lambda common facts also include source-local instances of the existing
allocated-arrow result theorem. For the actual selector heap `h`, the premises
are `IsGoodHeap(h)`, allocation of the function at its base arrow type,
allocation of every boxed actual argument, and `RequiresN(..., h, ...)`.
Together they imply allocation of `ApplyN(..., h, ...)` at its result type in
that same heap. This is direct universal instantiation of the existing nested
allocation axiom in `BoogieGenerator.Types.cs`; commuting its binders introduces
no new principle. All enclosing lambda type, allocation, range, formation, and
permission guards remain. Pure applications use `$OneHeap` consistently in
both the selector and the allocation target. No relation to an actual current
or previous heap is inferred, and global arrow axioms and patterns are unchanged. A generic
composition control returns a freshly allocated reference, proves its exact
value and that it was unallocated in a labeled earlier heap, and still rejects
a final assertion of false. This exercises the intermediate callback result
without granting allocation in an unrelated heap.

## Reference carrier admission

A reference type is a finite source image carrier only relative to one valid
heap's finite allocation universe. Source well-formedness checks assume allocated
parameters, whereas shared `#requires` and body axioms can be used with merely
typed parameters. Allocation-independent source membership in a captured
`iset<object?>` therefore cannot justify an unconditional finite definition.
For example, a legal source function can return
`set x: object? | x in xs` for an allocated `iset` argument `xs`; its shared
axioms must not define the same finite conversion for arbitrary infinite
unallocated reference isets.

When all source witness bounds are genuinely finite, the ordinary defining
equation is unchanged. Bounds supplied specifically by `allocated(x)` additionally
require their actual heap to be good. When admission instead relies on a finite
image carrier that may contain references, the equation has the premise

```text
exists h: Heap :: IsGoodHeap(h) &&
  (forall b: Box :: characteristicMap[b] ==> IsAllocBox(b, imageType, h))
```

The predicate and converted value are unchanged. The premise establishes that
the selected canonical image lies in one uniformly finite allocated universe.
For sets of reference collections, it gives the finite powerset of that universe;
for map-value carriers it combines finite allocated key and value carriers.
A valid source caller can supply its actual heap. Existentially naming that heap
also handles a heap-independent source function without making its translated
value depend on an arbitrary distinguished heap.

The allocation premise uses the characteristic-map selection directly, avoiding
a circular assertion about the allocation of the converted finite set itself.
It is a condition built from existing source allocation predicates, not a new
universal finite-support oracle. It neither intersects the source predicate with
allocation nor extends membership equations to unrelated backend maps.

The reference-typed `AllocFreeBoundedPool` finite branch is not selected by the
ordinary resolver: its constructor is reached there only when the witness type
cannot involve references. The only other construction sites create the
`nat`/`ORDINAL` index pools of generated extreme predicates. Clones preserve
these types. Its reference branch must not be used to justify a new admission
route without the same valid-heap argument.

## Common function and lambda contexts

An opaque function's postcondition can contain a finite comprehension even when
its body returns a literal collection. Callers must obtain that comprehension's
membership definition through the ordinary function consequence, without
revealing the body and without enabling optional verification facts. The
opaque-contract regression derives legitimate set membership and two distinct
captured map values, then a separate control rejects a live `assert false`.

Lambda source well-formedness checks havoc a good successor heap and introduce
formal variables with `GetWhereClause(..., ISALLOC)`. Common lambda facts must
therefore guard their child call facts and nonempty-footprint definitions with
both the boxed source type and allocatedness in that same lambda heap, as well
as the actual formation-heap succession and source range. An effect-free arrow
has a total empty `.reads` selector, including arguments outside the allocated
formal universe or where its body precondition is false. Its characteristic
predicate is the constant false map, independent of arguments, heap, and layer.
This exact closed definition is supplied once at formation without the lambda's
formal domain guard; copies under the guarded body or range are redundant.
Enclosing source guards and all child-call guards remain intact. The value and
its `.requires` translation retain their own descriptor; a stronger availability
premise for facts does not change the lambda's predicate or footprint. In
particular, merely typed unallocated backend arguments do not inherit child
function permissions that were checked only for allocated source formals.

Ordinary function definitions reject direct dependence on `allocated(q)`.
The source allocation control instead uses an effectful reference-argument
lambda whose call precondition follows from a `nat` field, allocates a fresh
object, and uses the resulting lambda and footprint before rejecting `false`.
The structured encoding audit separately checks the allocation guard at the
actual boxed argument and heap; source tests do not expose arbitrary unallocated
backend references as admitted actual arguments.

## Construction and consumer inventory

| Construction | Origin and predicate | Finite justification | Fact consumer |
|---|---|---|---|
| Simple finite set | `TranslateSetComprehension`; typed unboxed element satisfies the range | An admitted source bound or finite result carrier | Common comprehension call facts and well-formed result check |
| General finite set image | Same translator; typed source witnesses, range, equality with boxed term | Finite witness image or independent finite image carrier | Same common facts; definitions outside own witnesses |
| Finite map domain | `TranslateMapComprehension`; typed witnesses, range, canonical boxed key | Finite witness domain or finite key carrier | Common map facts, collision check, map result check |
| Infinite set/map | Same source translators, backend Boolean map | No finite conversion or finite defining equation | Existing infinite membership encoding |
| Lambda reads footprint | `TrLambdaExpr`; canonical boxed reference satisfying `InRWClause` | Finite union of explicit finite collections/references, or uniformly finite allocated references of one valid heap | Lambda common facts under its actual heap, layer, argument, and availability context |
| Named-function `ReadsN` | `FunctionHandle`; canonical boxed source reads predicate | Same reads argument, admitted under the function-handle availability boundary | First-class `.reads` use and permission/frame checks |
| Function-valued reads clause | `FrameArrowToObjectSet`; generated `_reads` comprehension over arguments and an object result | One valid heap's uniformly finite allocated-reference universe; pointwise finite outputs alone are insufficient | The ordinary generated-comprehension common facts and frame checks |
| Inferred reads decrease | `InferDecreasesClause`; sequence/multiset membership comprehension | Finite input collection | Ordinary comprehension translation |
| Field/index location set | `MemoryLocationSetComprehension`; reference collection mapped to field/index tuples | Finite input collection, or admitted finite reference domain | Ordinary image comprehension translation and location extraction |
| Clone/substitution | AST cloner and `Substituter` | Preserves source formation and bound metadata | Retranslation must retain the current environment; no global AST-only expression cache |

The bridge and direct finite-result searches cover the named conversion symbols,
`ReadsN` membership equations, and the generated comprehensions above. Permission
checks in `InRWClause_Aux` are separate from the finite boxed-footprint wrapper.
Wildcard reads keep their existing allocated-reference predicate.

## Bounds inventory

`BoundsDiscovery` includes implied source type constraints, discovers bounds
backwards through the binder list, and can reverse that list to obtain a better
dependency order. A selected dependent bound cannot use a still-unbounded later
variable. Inductively, each finite outer choice has a finite fiber, so the
complete witness domain is a finite union of finite fibers.

| Resolver route | Justification or restriction |
|---|---|
| `BoolBoundedPool`, `CharBoundedPool` | Finite scalar carrier |
| `DatatypeBoundedPool` | Finite enumeration: no type parameters and no constructor fields |
| Two-sided `IntBoundedPool` | Fixed finite integer interval; reversed or equal endpoints give an empty interval |
| `ExactBoundedPool` | At most one value, after the dependency check |
| `SetBoundedPool`, `MapBoundedPool` | Membership in an admitted finite set/domain; infinite set/map membership lacks the `Finite` virtue |
| `SeqBoundedPool`, `MultiSetBoundedPool` | Finite sequence range or finite multiset support |
| `SubSetBoundedPool` | Subsets of one fixed finite set form a finite powerset; an infinite upper set does not grant `Finite` |
| `SuperSetBoundedPool` | Supplies no finite virtue |
| `ExplicitAllocatedBoundedPool` | Uniform finite allocated universe in one valid heap; the resolver preserves allocation-independence restrictions |
| `AllocFreeBoundedPool` for reference types | Reference result carrier is finite in admitted source contexts; it is not a claim that arbitrary mathematical references are finite |
| `OlderBoundedPool`, `SpecialAllocIndependenceAllocatedBoundedPool` | Allocation restrictions only; they supply no finite virtue |
| Finite image carrier | `HasFinitePossibleValues` admits images over Boolean/character/enumeration/reference carriers, and actual finite map-value carriers, even when source witnesses are infinite |
| `DatatypeInclusionBoundedPool` | A rank inequality is allocation independent, but does not establish finite support |

### Finite key domains and finite map-value carriers

`HasFinitePossibleValues` describes a carrier of whole values. For a map type
both the key carrier and the value carrier must be finite: a partial map then
has finitely many domain subsets and finitely many assignments per domain.
A finite key carrier alone does not make all map values finite in number;
`map<bool, int>` has infinitely many values, for example `map[true := i]` for
every integer `i`.

`BoundsDiscovery` therefore checks a set comprehension's element carrier or a
map comprehension's key carrier directly. This preserves unbounded Boolean-key
maps with arbitrary integer values, but rejects unbounded sets of those maps
and maps using those complete maps as keys. Explicit finite witness bounds
still admit either construction. Both resolver modes use this same formation
check. Collection newtypes are normalized to their ancestor set/map before
extracting that element/key carrier, without changing the underlying finite
carrier classification.

### Why datatype rank is not a finite bound

Consider distinct datatypes:

```dafny
datatype Leaf = Leaf(i: int)
datatype Container = Container(children: iset<Leaf>)
```

The legal infinite value `iset i: int | true :: Leaf(i)` contains a distinct leaf
for every integer. The existing constructor rank axiom forces the rank of every
member below the rank of the enclosing `Container` value. `RankLt` translates
to an integer inequality on `DtRank`, not to a finite structural-subterm relation.
Consequently, `set d: Leaf | d < Container(children)` cannot be justified as a
finite set by its rank condition. This obstruction follows from constructor
membership/rank axioms, not from choosing a convenient but avoidable rank model.

The repair removes only the `Finite` virtue of that pool. A rank filter remains
legal alongside membership in an actual finite collection, and a comprehension
whose result carrier is independently finite remains legal. Infinite `iset` and
`imap` formation is preserved. The bound-rejection regressions use the existing
finite-result diagnostic under both resolver modes; the positive counterpart
also establishes the constructor rank facts that explain the rejection.

## Conditional backend control

`.github/review/issue82-boundary.py` records solver input, version, options,
output, and resource statistics for three ground fragments. The repaired
fragment has active empty/singleton source definitions and an explicit extra
diagonal-map equation. One ground instance of the removed arbitrary-map bridge
makes it contradictory. Removing the extra diagonal equation makes that same
old-bridge instance satisfiable again.

At `s = C(diagonal)` and `b = BoxSet(s)`, the retained forward boxing law gives
`UnboxSet(b) = s`. The extra diagonal instance gives
`diagonal[b] = !Member(s,b)`. The removed bridge instance would give
`Member(s,b) = diagonal[b]`, hence a Boolean equals its negation. Every ground
formula is an instance of a stated schema, so the test is deterministic without
assuming that SMT arrays automatically contain a diagonal function.

Expected results are `sat`, `unsat`, and `sat`, respectively. Timeout or
`unknown` fails the control. The test is a conditional backend contradiction;
it does not claim an executed Dafny-source false proof. A satisfiable ground
fragment also does not certify the complete repaired backend theory. Real
Dafny-generated mixed finite/infinite queries, live assertion-failure controls,
and the full prescribed repository gates remain separate validation obligations.

## Executable pipeline audit

The `Finite-support encoding and boundary controls` CI job runs
`.github/review/issue82-audit.py` against the packaged verifier and Z3 5.1.0.
It captures the companion source's Boogie before and after lambda lifting and
named, actual solver queries from that source and the mixed finite/infinite
control. `.github/review/issue82-encoding.py` parses those artifacts to check
scope, typed witnesses, canonical representation, capture identity, and valid
patterns. Targeted mutations must be detected, including equivalent renamed
preservation wrappers; a symbol-name search alone is insufficient.

A backend map returning an already-finite `Set` is not a characteristic map:
selecting such a value and testing its membership does not convert an arbitrary
Boolean predicate into a finite set. The SMT boundary check distinguishes these
operations structurally and retains the arbitrary Boolean-map mutations.

The isolated diagonal fixture must return `sat`, `unsat`, and `sat` for the
repaired, old-bridge mutation, and mutation without the extra diagonal controls.
The real mixed source must fail solely at its live assertion of false. Missing
pipeline stages, invalid Boogie, solver failure, or a safety timeout fail the job.
