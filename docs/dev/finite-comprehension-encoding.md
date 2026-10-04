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
check.

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
