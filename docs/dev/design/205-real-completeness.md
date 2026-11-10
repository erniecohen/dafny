# Design and review: completeness of `real` (#205)

**Status:** design only; no product implementation. Prepared for the work request in
[issue #205](https://github.com/erniecohen/dafny/issues/205#issuecomment-6757564935).
The inspected source is `review/4.11.0` at
[`62bae981952337d312cbb3d43c39f9813cca6084`](https://github.com/erniecohen/dafny/tree/62bae981952337d312cbb3d43c39f9813cca6084).
All source paths below refer to that commit. AI assistance: ChatGPT, including
independent encoding, mathematical, and trigger reviews.

## 1. Decision

Add a **compiler-owned, ghost-only completeness lemma over `iset<real>`**, under
`--additional-axioms`. A call supplies one guarded completeness instance for its
actual set and two explicit witnesses. Do not install a global completeness
quantifier over sets. Keep native real arithmetic, `.Floor`, conversions, and
ordinary function-value semantics unchanged.

The provisional source interface is:

```dafny
// Compiler-owned system declarations, available with --additional-axioms.
// The source below describes their contract; it is not a user {:axiom} module.
class RealCompleteness {
  static ghost predicate {:transparent} upper(s: iset<real>, b: real) {
    forall x: real {:trigger x in s} :: x in s ==> x <= b
  }

  static lemma complete(s: iset<real>, w: real, b: real) returns (u: real)
    ensures w in s && upper(s, b) ==> upper(s, u)
    ensures w in s && upper(s, b) ==>
      forall v: real {:trigger upper(s, v)} :: upper(s, v) ==> u <= v
}
```

`complete` has **no preconditions**. Its conclusions retain both guards. A call
on an empty or unbounded set is legal and supplies no LUB property; its result is
unspecified. This permits meaningful live negative tests rather than tests that
stop at a failed precondition. An ordinary verified wrapper with nonempty and
boundedness preconditions gives callers the convenient unguarded LUB contract.

This is option (a) implemented as an explicit local inference rule, called (a′)
below. It adds the same trusted mathematical fact as a prelude axiom; it does not
pretend to derive completeness from field laws. It removes the downstream
**user** bridge axiom by moving one small, documented principle into the verifier's
trusted interface. No root, continuity, polynomial, cut-bijection, or new
function-space axiom is needed.

The critical implementation requirement is **no new emission without a use**.
With the option off, neither registration nor translation constructs new nodes.
With it on and no use of this family, the verification output must equal that of
the previous build with the same option setting. Trigger inactivity is weaker:
the experiments below found substantial cost changes from globally present
axioms even when their completeness-instantiation count was zero.

## 2. Exact mathematical fact and representation

### 2.1 Domain

Use `iset<real>`. A nonempty finite `set<real>` already has a maximum in an ordered
field; finite-set completeness cannot supply the supremum of an interval or the
set used by the IVT proof. A verified finite-set wrapper is harmless, but is not
the core principle.

A total, heap-independent `p: real -> bool` can be wrapped by the ordinary
comprehension `iset x: real | p(x)`. Keep `Apply`, `Requires`, `Reads`, captured
heaps, and arrow allocation out of the trusted addition. An arbitrary partial or
heap-reading predicate is not silently treated as a total pure predicate.

For native real variables define the following notation:

```text
m(s,x)       = s[$Box(x)]
typed(s)     = $Is(s, TISet(TReal))
upper(s,b)   = forall x:real. m(s,x) ==> x <= b
lub(s,u)     = upper(s,u) && forall b:real. upper(s,b) ==> u <= b
eligible(s)  = typed(s) && (exists w:real. m(s,w))
                       && (exists b:real. upper(s,b))
```

The mathematical addition is exactly:

```text
forall s:ISet. eligible(s) ==> exists u:real. lub(s,u).
```

The implementation uses its witness-parameterized consequence:

```text
forall s:ISet,w:real,b:real.
  typed(s) && m(s,w) && upper(s,b) ==> exists u:real. lub(s,u).
```

These are equivalent for typed sets by choosing the two witnesses. There is no
assumption that every set is eligible, and no artificial infinity inhabits `real`.

### 2.2 Local lowering and Skolemization

For a particular call, evaluate and check the source actuals through the usual
call machinery, including their types and well-formedness, and preserve ordinary
ghost/output-assignment checks. With `s0,w0,b0` the evaluated actuals, the logical
core is:

```boogie
havoc u;
assume $Is(s0, TISet(TReal)) && s0[$Box(w0)] && U(s0, b0) ==>
  U(s0, u) &&
  (forall v: real :: { U(s0, v) } U(s0, v) ==> u <= v);
```

`u` is a fresh local real witness, not a universal variable. This is a specified
nondeterministic ghost result. The existence of a satisfying result is the one
trusted completeness obligation. If the guard is false, any real result satisfies
the postcondition. Use the ordinary Boogie procedure call with this guarded ensures; the
havoc/assume display gives its logical meaning. Its declaration must be emitted
only when needed. No new custom statement-lowering path is required.

There is **no global Skolem function `Sup(s)` in the primitive** and no outer
E-matching trigger for sets. Calls whose guards hold, on the same set, return the same mathematical value,
by uniqueness of its LUB; prove that fact in Dafny. Eligibility of the set alone
does not constrain a raw call with a bad member witness or a bad bound. Calls with false guards need not return the same value.

A separate verified source library can define a canonical ghost `sup(s)` by
unique choice: call a no-output `existsLub` wrapper in a statement expression,
then choose `u` satisfying `lub(s,u)`. A total wrapper may explicitly return `0.0`
when the set is empty or unbounded. That fallback is the wrapper's definition,
not an asserted property of the primitive. Alternatively expose the partial
wrapper requiring eligibility; the recommended first release supplies the total
wrapper plus the conventional checked LUB lemma. Function values of this wrapper
use the ordinary Dafny function/arrow translation, not a new special-function
handle path. Those ordinary function/choice axioms can quantify over sets: the
claim that there is no outer set quantifier is about the trusted primitive, not
all generated axioms of the derived `sup` wrapper. Profile wrappers and nested
uses separately before transferring the primitive's cost or closure claims.

### 2.3 The upper-bound helper

`U` is a genuine predicate symbol, not an abbreviation that disappears before
trigger selection. Its logical definition is:

```boogie
function U(s: ISet, b: real): bool;
axiom (forall s: ISet, b: real :: { U(s, b) }
  U(s, b) <==>
    (forall x: real :: { s[$Box(x)] } s[$Box(x)] ==> x <= b));
```

This definition is not a new mathematical assumption: interpret `U` by the
right-hand side. The production path uses the ordinary, compiler-owned
source predicate, explicitly transparent, and its normal permission/type guards. The display suppresses
those already-existing guards to show the exact new mathematical content and
patterns. Do not replace a normal source predicate call by an unrelated prelude
symbol, or introduce an unguarded `#canCall` shortcut. The evidence includes the
actual Dafny-generated form with its permission and boxing machinery.

A user who has proved the raw universal bound must expose `upper(s,b)` before
using leastness. The checked wrapper and an ordinary `least(s,u,b)` lemma make
this step explicit. A goal `u <= b` alone does not necessarily create the
`upper(s,b)` term needed by that trigger. This is a deliberate, predictable proof
interface, not a claim of automatic discovery of every useful bound.

## 3. Triggers: decisions and closure argument

### 3.1 Patterns in the selected design

| Quantifier | Trigger | How the required term arises |
| --- | --- | --- |
| Completeness over sets | **None: no such quantifier is emitted** | An explicit ghost call supplies one local instance. |
| Outer definition of `U(s,b)` | `{ U(s,b) }` | A bound premise, a returned upper-bound fact, or an explicit checked helper call. |
| Pointwise bound, with `s,b` fixed | `{ s[$Box(x)] }` | Membership in the source hypothesis, set comprehension, or goal. |
| Leastness of local witness `u` | `{ U(s,v) }` | The caller supplies the candidate bound through `upper(s,v)`. |
| Existential LUB result in a wrapper | No user E-matching pattern | Local existential witness/choice, justified by the primitive and ordinary proof. |

Each pattern covers all variables bound by its quantifier. Variables fixed by an
outer instance or source call are constants at that point; they do not require
additional pattern terms. Stable production QIDs should identify the upper
pointwise quantifier and leastness separately. Inspect the emitted SMT instead of
assuming that a source `{:trigger}` survives preprocessing unchanged.

The new family constructs neither a new set argument nor another primitive call.
For each explicit call the witness is fixed once. An upper-bound term activates
only its definition; pointwise upperness adds an inequality and no new upper-bound
term; leastness adds an inequality and no new upper-bound term. The reverse
direction of `U`'s definition can produce a counterexample membership witness
for a false bound, one per existing `(s,b)` instance; it does not create another
`U` bound argument. Include those witnesses in the finite term closure. Thus this family
has no self-generating outer matching chain. A finite set of explicit calls and
bound/membership terms gives a finite family-local set of matching obligations.

This is **not** a termination theorem for the combined solver. User quantified
facts may manufacture new bounds, memberships, or sets; existing comprehension
and boxing axioms may expose terms; E-matching works modulo equalities; and MBQI
is a different instantiation mechanism. Keep all existing solver settings and
limits, test interactions, and make only this scoped closure claim. The
[Z3 guide](https://microsoft.github.io/z3guide/docs/logic/Quantifiers/) describes
these distinctions and the risks of cross-products in multi-patterns.

Unrelated real arithmetic contains neither a call nor this predicate. It causes
no completeness instantiation. More strongly, the new family is absent from the
translated verification context, which is what preserves preprocessing and
resource counts.

### 3.2 Alternatives compared

All flat global candidates use the same guarded formula:

```text
forall s,n,b.
  m(s,n) && U(s,b) ==>
  U(s,Sup(s)) && forall v {U(s,v)}. U(s,v) ==> Sup(s) <= v
```

| Alternative | Outer trigger | Advantages | Disadvantages / decision |
| --- | --- | --- | --- |
| A. Membership and bound | `{ m(s,n), U(s,b) }` | Finds sets without a new API term. | Fires on ordinary bounded-set work; pairs every member witness with every bound; creates useless guarded instances. Reject. |
| B. Supremum plus witnesses | `{ Sup(s), m(s,n), U(s,b) }` | Suppresses activation when no supremum term is present. | Keeps witness-pair fanout once active; a `{Sup(s)}` singleton is invalid for this three-variable quantifier. Reject. |
| C. Nested supremum | `{ Sup(s) }` on `forall s`; existential nonempty/bounded guard | Compact and mathematically natural. | Nested quantifier preprocessing and witness generation are less predictable; dormant global output still changes costs. Retain only as a fallback comparator. |
| D. Private request | `{ request(s,n,b) }` | One explicit activation, no request terms produced in the conclusion. | Still adds a global quantified family and a marker protocol; declaration/pruning overhead remains unless demand-local. Acceptable fallback, unnecessary here. |
| E. Local ghost call | No outer quantifier | No witness cross-product, raw guards testable, no unused-family output, ordinary local proof interface. | Requires a sealed built-in call and explicit library wrappers. **Recommend.** |

Do not use a trigger on arbitrary real boxes or arithmetic comparisons for
leastness. It can make newly generated counterexample witnesses into new candidate
bounds. The inner-pattern experiments below test this separately from outer
activation.

## 4. Soundness argument

### 4.1 Intended arithmetic and relative model

Start with an intended model of the existing encoding, including whichever
existing additional-axiom families are enabled, with native real carrier the
standard real numbers, integer carrier the integers, and the usual arithmetic,
order, integer embedding and floor. For each represented `s:ISet`, form its actual
mathematical real slice

```text
A_s = { r in R | select(s, box_real(r)) = true }.
```

Interpret `U(s,b)` as “b bounds A_s above”. If the call guard is true, `w` witnesses
that this slice is nonempty and `b` bounds it; real completeness supplies its
unique LUB. Return that value. Otherwise return any real, for example zero. The
local postcondition holds in both cases. All existing symbols retain their
interpretations, so existing additional-axiom facts retain theirs too. This is a
relative soundness argument for the new principle, not a new proof of the
consistency of every existing Dafny encoding rule.

The existing family includes bitvector/integer round trips, literal identities,
heap succession instances, allocation instances, override call-layer instances,
and locally checked call permissions. None refers to this new witness rule. The
argument changes no integer, bitvector, heap, allocation, arrow, or function
interpretation. Existing proof/termination soundness obligations remain trusted
as before; completeness must not be used to justify a circularly assumed source
contract.

### 4.2 Boxing and represented maps

`ISet` is represented by `[Box]bool`, and membership of a real is
`s[$Box(x)]` (`DafnyPrelude.bpl`, infinite-set section). Quantify native reals and
box them. Do not unbox arbitrary elements as reals without `$IsBox` guards, and do
not restore the removed unconditional reverse boxing equation.

**Do not claim that `ISet` is the full powerset of the universal `Box` domain.**
`ISet` values are themselves boxed injectively through the forward box/unbox law;
full `P(Box)` plus that injection would contradict Cantor. The encoding uses
represented, extensional maps (a Henkin interpretation), not a promise that every
subset of the universal box carrier is represented. Every represented map still
has an actual real slice, and the argument needs only the supremum of that slice.
It adds no powerset-surjectivity or comprehension axiom.

For a fixed old signature this is an expansion by the upper predicate and the
new witness rule. Source comprehensions mentioning the derived `sup` still rely
on the ordinary trusted comprehension translation and its intended typed
semantics. Do not overstate the argument as preserving an arbitrary fixed Henkin
carrier under every possible newly definable comprehension.

Completeness is not a first-order categorical characterization of the standard
reals in the pure ordered-field signature. Once a set sort and membership are
encoded, the displayed assertion **is first-order over represented sets**. The
issue's first-order limitation should be understood with that distinction.

### 4.3 Empty and unbounded sets

For an empty set, every real is an upper bound. Unguarded leastness would imply
`u <= u - 1`. For the set of all reals, unguarded upperness would imply
`u + 1 <= u`. These are genuine contradictions, not performance concerns.

The raw guarded contract asserts neither conclusion unless both premises hold.
An arbitrary `w` on an empty set does not satisfy membership. An arbitrary `b` on
an unbounded set is not an upper bound. Calling the primitive never assumes
either premise. The fallback result has no specified value, and tests must
reject assertions fixing it to zero merely because zero was used in the model.

### 4.4 Function values, heaps, floor, casts, and literals

A set formed from a heap snapshot is already a value. Its membership may depend
on that captured snapshot, but the supremum of that value adds no fresh heap
observation. The pure predicate wrapper uses the existing arrow definedness and
comprehension checks and adds no blanket `Requires`, empty-read, allocation, or
function-extensionality assertion.

`DafnyPrelude.bpl` defines `Int` with Boogie's native real-to-integer conversion,
`Real` with the integer embedding, and `_System.real.Floor(x)` as `Int(x)`.
`r.Floor` remains mathematical floor, including for negative fractions. The
existing `real as int` check still requires `Real(Int(r)) == r`; it does not
become a rounding cast. Exact decimal real literals remain the corresponding
rational numbers. The new witness rule respects all these interpretations.

The primitive and all choice wrappers are ghost-only. A supremum can be irrational;
compiled real values use rational representations, including C#'s `BigRational`.
This proposal supplies no executable arbitrary-real representation or algorithm.
Existing ghost erasure checks must reject observation of a ghost supremum in
compiled data or output.

## 5. Acceptance lemmas and arbitrary-degree IVT

The public LUB theorem should have this shape, with no user axiom:

```dafny
lemma existsLub(s: iset<real>)
  requires exists w: real :: w in s
  requires exists b: real :: upper(s, b)
  ensures exists u: real :: upper(s, u) &&
    forall b: real :: upper(s, b) ==> u <= b
```

Choose `w,b`, call the primitive, and return the existential witness. Also prove
uniqueness and a leastness-use helper that takes an explicit upper-bound premise.

For coefficient sequences use Horner evaluation, least-significant coefficient
first:

```text
p([],x)       = 0
p(c,x)        = c[0] + x*p(c[1..],x), when |c| > 0
```

The theorem must quantify a **symbolic `c:seq<real>`**, not enumerate degrees:

```dafny
lemma polynomialIVT(c: seq<real>, a: real, b: real) returns (r: real)
  requires a < b
  requires p(c, a) < 0.0 < p(c, b)
  ensures a < r < b && p(c, r) == 0.0
```

Here is a concrete proof plan that introduces no continuity axiom. Let
`M = 1 + |a| + |b|`, and define recursively

```text
A([],M) = 0
A(c,M)  = |c[0]| + M*A(c[1..],M)
L([],M) = 0
L(c,M)  = A(c[1..],M) + M*L(c[1..],M).
```

Prove by sequence induction that, for `|x|,|y| <= M`,

```text
|p(c,x)| <= A(c,M)
|p(c,x)-p(c,y)| <= L(c,M)*|x-y|.
```

For the second induction step use
`x*q(x)-y*q(y) = (x-y)*q(x) + y*(q(x)-q(y))`, triangle inequality and
nonnegative multiplication bounds. The proof is ordinary real algebra and
induction and can verify with completeness disabled.

Let `S = iset x:real | a <= x <= b && p(c,x) <= 0`. It contains `a` and is bounded
by `b`. Obtain its LUB `u`; then `a <= u <= b`. Put `L=L(c,M)`.

- If `p(c,u)<0`, then `u<b`. Choose
  `d=min((b-u)/2, -p(c,u)/(2*(L+1)))`. It is positive; `u+d` lies in the interval;
  the Lipschitz estimate makes its polynomial value negative. Thus `u+d` belongs
  to `S`, contradicting upperness of `u`.
- If `p(c,u)>0`, then `a<u`. Choose
  `d=min((u-a)/2, p(c,u)/(2*(L+1)))`. For any `x` in `S` with `x>u-d`, upperness
  gives `x<=u`, and the estimate forces `p(c,x)>0`, a contradiction. Hence `u-d`
  is an upper bound of `S`, contradicting leastness of `u`.
- Therefore `p(c,u)=0`, and the strict endpoint signs imply `a<u<b`.

This supplies both a useful implementation decomposition and an intuitive
completeness argument: the new principle gives precisely the missing limit
point; all polynomial reasoning is finite sequence induction and ordered-field
algebra. Add a square-root-of-two corollary as a useful smoke test, but do not
mistake that fixed-degree result for the arbitrary-degree acceptance theorem.

The measured main IVT has an existential conclusion; a separately verified
wrapper returns a root with exactly the contract above.

The attached scratch proof is an experiment with one visibly marked temporary
`{:axiom}` placeholder for the proposed contract. Its ordinary proof obligations
measure feasibility of the design; it is not the proposed shipped library, and
its verification must not be reported as eliminating user axioms in today's
product. Running the current auditor on it reports exactly one finding, the
explicit temporary axiom on `complete`. The implementation milestone replaces that single placeholder with the
compiler-owned call and runs `dafny audit` on the resulting public proof module.

## 6. Measurements and interpretation

### 6.1 Protocol and scope

The [evidence directory](205-real-completeness-evidence/README.md) contains the
proof sources, portable runners, formula-preserving replay scripts, manifests,
results, profiles and compact corpus records. Measurements use a pristine build
of `62bae981`, except the abstract kernel, which needs only Z3. The executable
identifies itself as `4.11.0+issue205-design-62bae981`. The fixed Boogie packages
were checksum-verified before building.

Actual Z3 4.12.1 and 5.1.0 executables came from the corresponding `z3-solver`
PyPI wheels. Their executable SHA-256 hashes are respectively:

```text
4.12.1 b14068b578fe6f2ad90d46183ba73b1c4ee071de8caccc455b9fcc6d3eb1d320
5.1.0  54bae839dd54e262edac6f755fc99659ce2a121301faff20a3e3b94478dcead0
```

These are identified reproducible builds, **not** the fork's separately pinned
GitHub-release binaries. Production CI must still use its own pins. Resource
units are solver counters, not elapsed-time estimates; quantifier counts are
observed total instances, with per-QID counts retained in the evidence.

For the acceptance proof, clean source runs use the shared option ON, one core,
`/normalizeDeclarationOrder:0 /restartProver /emitDebugInformation:1`, a
2,000,000-RU limit per batch and a 30-second safety timeout. Pattern comparisons
replay each captured VC in a fresh process, preserve its formulas, and change
only selected pattern annotations. A hash of the parsed formula after removing
patterns checks that invariant. The selected replay totals equal the clean
source-run totals. Solver profiling is enabled **offline**: an early attempt to
profile live emitted stderr that disrupted one Boogie interaction before it sent
`check-sat`; clean verification and offline replay fixed that instrumentation
problem without changing the source.

The main proof has SHA-256
`7e39148cdb6dd5a54d90ad5bdba958914bcb9ec5976174bb479d97e63ac00a02`.
It contains one temporary guard-total axiom and no `assume`. The current auditor
reports exactly that one assumption. This is a proof and encoding experiment,
not a product implementation or an axiom-free library claim.

### 6.2 Full acceptance proof: three inner-pattern choices

This is the **complete symbolic coefficient-sequence development**, including
the LUB wrapper, recursive polynomial algebra/bounds, arbitrary-degree IVT, and
square-root-of-two corollary. The explicit-point variant uses membership for
pointwise upperness and `upper(s,b)` for leastness. The broad-point variant
changes only the pointwise trigger to the native real-box term. The automatic
variant removes the selected explicit pointwise/leastness patterns and lets Z3
infer them.

| Solver | Patterns | Total RU | Quantifier instances | Proved batches |
| --- | --- | ---: | ---: | ---: |
| 4.12.1 | **Explicit membership / upper** | **1,940,734** | **2,043** | **90 / 90** |
| 4.12.1 | Broad real-box pointwise trigger | 3,653,293 | 2,540 | 90 / 90 |
| 4.12.1 | Automatic patterns | 3,947,881 | 2,498 | 88 / 90 |
| 5.1.0 | **Explicit membership / upper** | **2,476,611** | **3,655** | **90 / 90** |
| 5.1.0 | Broad real-box pointwise trigger | 3,891,059 | 4,729 | 90 / 90 |
| 5.1.0 | Automatic patterns | 3,900,951 | 4,771 | 90 / 90 |

The selected source proof verifies **90 / 0** on both resolvers with both
solvers, with the same RU total for each solver. On 4.12.1, automatic patterns
leave IVT split 31 unproved with arithmetic incompleteness below the cap
(53,169 total RU), and split 46 reaches exactly its 2,000,000 query-RU cap
(2,016,932 including setup), also reporting arithmetic incompleteness. No
wall-time timeout occurred. These rows include the failed batches' resources.

A separately verified ordinary wrapper supplies total ghost `sup`, uniqueness,
and a returning-root IVT interface. It verifies **4 / 0** in all four
solver/resolver combinations: 55,141 RU on 4.12.1 and 56,084 RU on 5.1.0 under its
recorded process-reuse protocol. Those small wrapper totals should not be mixed
with the fresh-process totals above.

### 6.3 Actual boxed-set kernel and live controls

The smaller guard-total primitive experiment goes through native Dafny
`ISet`/boxing/function-permission translation. On both resolvers, with the
existing shared option OFF and ON, its four positive lemmas verify and its five
negative assertions fail exactly where intended. Total recorded RU is 81,222
on 4.12.1 and 81,807 on 5.1.0. The temporary axiom is deliberately independent
of the product option in this experiment; this matrix checks interactions, not
future option gating.

The negative cases are a bounded nonempty set, an empty set, an unbounded set,
and two attempts to fix invalid-call results to zero. The stronger full-proof
negative file has three live `assert false` controls and explicitly exposes the
critical terms (`upper(empty,u-1)` and `u+1 in allReals`). All three fail on both
solvers and resolvers, costing 40,644 / 42,049 RU respectively. Guard-mutation
checks on 5.1.0 confirm sensitivity: removing nonemptiness makes the empty-set
false assertion verify; removing boundedness makes the unbounded-set false
assertion verify. Removing the temporary primitive contract altogether makes
exactly the checked LUB wrapper's two postconditions fail; the ordinary
polynomial algebra continues to verify.

A particularly bad inner alternative is a broad real-box trigger for
**leastness**. Formula-identical 5.1.0 replays give:

| Obligation | Selected RU / instances | Broad leastness RU / instances | Broad result |
| --- | ---: | ---: | --- |
| Checked LUB wrapper | 7,319 / 6 | 71,003 / 612 | Unknown: incomplete quantifiers |
| Upper-bound use | 7,651 / 18 | 70,101 / 623 | Unknown: incomplete quantifiers |
| Leastness use | 7,005 / 5 | 43,923 / 361 | Unknown: incomplete quantifiers |
| Existential LUB | 7,635 / 21 | 2,004,850 / 2,227 | Unknown: canceled at 2,000,000 query RU |

The selected pattern proves all four. Broad box terms both miss relevant
upper-bound requests and invite unrelated instantiations. This is evidence of
poor proof search, not a claim that the alternative formula is inconsistent.
The same failure categories occur on 4.12.1.

Vacuity failures with `unknown` are failed proofs of false, not SAT certificates.
The separate intended-model argument establishes the mathematical consistency
claim. Keep both forms of evidence.

### 6.4 Outer activation choices in an abstract kernel

This isolates the outer activation decision. It uses an uninterpreted set sort,
real membership, the exact upper-bound definition, and the guarded formulas of
section 3. It is **not** the complete Dafny representation. Each cell below is
**RU / total instances / outer completeness instances**, Z3 5.1.0:

| Kernel | Membership/bound | Nested `{Sup(s)}` | Explicit request | Local instance |
| --- | ---: | ---: | ---: | ---: |
| Upper-bound obligation | 1,804 / 10 / 4 | 1,963 / 10 / 1 | 1,744 / 7 / 1 | **1,481 / 6 / 0** |
| Leastness obligation | 1,787 / 8 / 3 | 1,925 / 9 / 1 | 1,745 / 6 / 1 | **1,387 / 4 / 0** |
| IVT final sign step only | 2,320 / 11 / 4 | 2,726 / 18 / 1 | 2,321 / 9 / 1 | **2,085 / 8 / 0** |
| One request, 64 membership terms | 10,258 / 262 / 130 | 8,155 / 136 / 1 | 7,806 / 133 / 1 | **7,442 / 132 / 0** |
| 20 bounded sets, no request | 10,615 / 120 / 40 | 4,906 / 20 / 0 | 4,848 / 20 / 0 | **4,294 / 20 / 0** |

The last local row exactly equals its no-completeness baseline. The flat
`Sup`-plus-witness multi-pattern still makes 130 outer instances in the
64-member case (10,260 RU / 262 total instances). It suppresses passive
activation but does not remove witness fanout. A single nested-sup input need
not mean exactly one profile entry or instance after preprocessing: the
interval kernel gives five completeness instances. Do not specify “exactly once
per set” as a solver guarantee.

The full 4.12.1 matrix is in the evidence. For example its 64-member row is
8,084 / 262 / 130 for broad activation, 8,167 / 201 / 1 for nested supremum,
7,438 / 133 / 1 for explicit request, and 7,102 / 132 / 0 for local activation.
Its passive 20-set row is 9,109 / 120 / 40, 4,437 / 20 / 0,
4,383 / 20 / 0, and 3,862 / 20 / 0 respectively. These comparisons support the
same decision on both solvers.

### 6.5 Existing real-heavy VCs: dormant globals still cost

These are genuine pinned Dafny VCs from existing tests. The existing shared
option is **OFF** in this small isolation experiment; candidate global
completeness families are inserted into replay inputs with unchanged original
formulas/options. No candidate completes an outer instantiation, and every
solver-result sequence stays unchanged. Nevertheless, costs move:

| Existing test | Baseline RU | Broad global | Nested global | Request global | Local/no-use |
| --- | ---: | ---: | ---: | ---: | ---: |
| `dafny0/RealTypes.dfy` | 22,142 | 26,012 | 26,302 | 26,102 | **22,142** |
| `dafny0/RealCompare.dfy` | 98,309 | 109,145 | 109,957 | 109,397 | **98,309** |
| `dafny0/TypeConversions.dfy` | 446,417 | 238,564 | 239,144 | 238,744 | **446,417** |
| `dafny0/TypeMembers.dfy` | 164,023 | 187,365 | 189,408 | 187,904 | **164,023** |
| `dafny4/Regression3.dfy` | 16,427 | 18,174 | 18,348 | 18,228 | **16,427** |

Table: Z3 5.1.0. The costs can decrease as well as increase; either direction
violates an exact no-use identity promise. Total instance counts are retained
in the evidence: for example `RealTypes` remains 66 in every variant, while
`TypeConversions` changes from 100 to 80, with **zero completeness instances**.
The additions alter preprocessing/search in the old theory.

Both versions were measured. On 4.12.1, `TypeConversions` goes from 314,553 RU
and 92 total instances to 370,891 RU and 83 instances with the broad global;
its other global alternatives also differ. Local/no-use input bytes, query
results, RU and instance counts equal baseline on all five files and both
versions, because that reference transformation adds no declaration or formula.
This demonstrates the required emission policy. It does not validate an
unwritten compiler implementation of that policy.

### 6.6 Full suite and standard-library costs with the existing option on

These are **measured pristine baselines**, using the repository's standing ON
harness and committed verdict files, not invented candidate costs. Every
existing verdict matched. The source census found no references to the proposed
family in the suite or standard-library sources.

| Corpus / solver | Coverage | Declaration outcomes | Measured total RU | Expected-verdict differences |
| --- | --- | --- | ---: | ---: |
| Suite / 5.1.0 | 1,163 programs; 6,922 logged declarations; 7,634 VC batches | 5,173 Correct; 1,731 Errors; 18 OutOfResource | **1,555,572,761** | **0 / 1,163** |
| Full Std / 4.12.1 | 2,190 declarations; 7,724 VC batches; all six target-specific passes | 2,190 Correct | **1,018,791,320** | **0 / 2,197 rows** |
| Full Std / 5.1.0 | 2,190 declarations; 7,724 VC batches; all six target-specific passes | 2,171 Correct; 2 Errors; 17 OutOfResource | **1,185,435,799** | **0 / 2,197 rows** |

The suite includes intentional negative/parse/resolution/CLI tests; “Errors” does
not mean a newly introduced regression. Its existing ON harness retains
original test overrides, including 18 first-RUN options explicitly setting
`--additional-axioms=false`. It also retains original limits where a test supplies
them. All 1,163 command/verdict rows are covered. Forty-one programs produce no
JSON after early diagnostics; one existing malformed logger-option fixture
produces an empty JSON file and zero verified proofs. These cases stay in the
coverage record rather than disappearing from totals. There was no safety
timeout. Declaration and batch resource sums agree for all three runs; standard
library JSON coverage is complete.

The implementation's required marginal #205 cost on these no-use inputs is
zero, with unchanged emitted verification programs. **That is a release
requirement, not a measured future product result.** Actual candidate OFF and ON
runs cannot exist before an implementation, which this work request expressly
excludes. Milestone 2 must compare against these baseline procedures and its
freshly rebased baseline using CI's binary pins. If those tests expose an enabled
cost problem, first repair emission/pruning; choose a separate option only with
recorded evidence and a new decision.


## 7. Option, packaging, and alternatives

### 7.1 Shared option and comparisons

Use `--additional-axioms`, including its existing project-file setting and legacy
spelling. The measured no-use advantage comes from local emission; there is no
evidence requiring a new switch for the selected design. A separate option is
justified only if the implementation's measured effect under the shared option
is a problem, as `AGENTS.md` requires. Do not introduce one preemptively.

Distinguish three comparisons:

1. Baseline OFF versus candidate OFF: identical Boogie/SMT, verdicts and RU.
2. Baseline ON versus candidate ON on programs not using completeness: the same
   identity requirement, isolating the marginal effect of #205.
3. Candidate ON versus OFF: existing shared-family effects can already differ;
   do not attribute them to completeness. Newly using the primitive is unavailable
   OFF and supported ON; the acceptance proofs use ON.

### 7.2 Library boundary

`CommonOptionBag.AdditionalAxioms` is registered as `OptionScope.Cli`;
`docs/dev/additional-axioms.md` expressly says it is not a `.doo` semantic
compatibility requirement. This setting alone supplies no explicit binary-capability boundary. The current
`.doo` format does retain source bodies and reparses/resolves them on import, so
an OFF importer may already reject a retained reference to the unavailable
builtin. Do not assert a demonstrated escape, or rely on that incidental route
without testing it. Deliberately enforce the supported packaging policy.

For the first implementation, place the completeness proof package in a separate
**opt-in source project**, outside the default `Std/dfyconfig.toml` inclusion tree.
Require its verification with `--additional-axioms`; CLI false must override any
project true setting and reject the intrinsic use. Keep field-algebra and
polynomial-bound lemmas that need no completeness eligible for ordinary `Std`.
Do not publish a preverified completeness `.doo` in this first milestone. Also
reject `.doo` production from a resolved program containing any reference to the
canonical `complete`, before the library backend switches to its pre-resolution
`AfterParsingClone`. Conservatively scan all loaded declaration bodies and
contracts, including unused wrappers; this avoids needing a theorem-dependency
analysis. The diagnostic should direct users to source inclusion or project-as-root
verification. `--library <project.toml>` auto-builds a `.doo`, so it must observe
the same restriction. This narrow new-operation restriction leaves all existing
library builds unchanged.

For a later binary package, add a transitive required-capability manifest for
this family and reject import without the option, including indirect re-export.
Do not change all historical meanings of the shared CLI option accidentally.
A source opt-in package is the smaller first PR; binary packaging is a separate,
explicitly reviewed feature. Existing arbitrary user-produced `.doo` files remain
subject to their existing trust policy; this design must not imply that banning
one shipped package establishes a universal theorem-dependency system.

### 7.3 Options (a), (b), (c), and (a′)

| Choice | Trust and benefits | Costs and decision |
| --- | --- | --- |
| (a) Global prelude completeness | One short true principle; retains native arithmetic; directly supplies LUB. | Global quantifier and dormant preprocessing costs; difficult to promise no-use identity. Reject the eager-global form. |
| (a′) Local compiler-owned completeness call | Same one principle, explicit activation, small trusted contract, native arithmetic, no new arrow axioms. | Requires builtin registration, precise helper/interface, opt-in packaging, and audits. **Recommended.** |
| (b) Verified Dedekind cuts plus bridge | Valuable construction; bridge trust is visible to the auditor; users can reason directly about constructed reals. | A bijection/order/field bridge remains a user/library axiom and exposes a much larger interface. Moving it does not remove trust. Keep cuts as an optional library; do not use the bridge as the permanent core solution. |
| (c) Define builtin `real` by cuts | Completeness can be proved from the construction's foundational assumptions. | Replaces the native arithmetic boundary, creates quotient/representation and automation burdens, changes runtime/compiler work. Incompatible with this focused preservation goal. |
| Real-closed-field or root-only facts | Smaller route to polynomial roots in fixed cases. | Does not provide LUB for arbitrary represented bounded real sets, so misses acceptance criterion 1. |

From complete ordered-field structure, construct the canonical rational cut of
`r`, prove injectivity by rational density, and use LUB for surjectivity onto the
nonempty proper downward-closed rational cuts with no greatest element. Prove preservation of arithmetic and order
in the library. This explains how (a′) can eventually replace the downstream
bridge with a theorem. It is a separate substantial proof task, not needed to
trust or implement the local LUB rule. Do not assert that just adding LUB makes
an arbitrary pre-existing bridge definition verify automatically.

## 8. Required code changes

The following is an implementation plan, not a patch in this PR.

1. **Compiler-owned declarations.** In
   `Source/DafnyCore/AST/SystemModuleManager.cs`, add an internal declaration kind
   or identity marker for the ghost system class, its definitional `upper`, and
   `complete`. Use ordinary identifier-based class/static-member syntax. Do not
   add `real.Sup` syntax: `real` is reserved, and the current static basic-type
   resolution paths are not a ready-made API. Register only under the shared
   option, with all input/output types fixed and ghost status fixed. Construct a canonical
   `ClassDecl` marked not for compilation, an explicitly transparent ordinary
   `Predicate`, and a canonical bodyless `Lemma`. These built-in ASTs must be
   fully resolved, including quantifier variables, member selections and triggers:
   `ProgramResolver.ResolveSystemModule` does not automatically resolve arbitrary
   injected class members in both modes. `SystemModuleManager.ArrowSubtypeConstraint`
   gives a precedent for fully resolved built-in quantifiers. A user class,
   string name, extern name, or `{:builtin}` attribute must never acquire trust.
2. **Demand and system translation.** In
   `Source/DafnyCore/Verifier/BoogieGenerator.cs`, the system-declaration traversal
   currently eagerly translates class members. Exclude these new declarations
   unless referenced in the translated verification context. Check before
   allocating declarations, IDs, helper terms or axioms. Demand must be local to
   the translated module/declaration context that can use the call, not a global
   file-wide inventory that perturbs sibling proofs. Ordinary Boogie dependency
   pruning must preserve unrelated-VC identity; prove it on sibling proofs. Emit
   one canonical helper definition on demand. The existing `referencedMembers`
   set is initially empty and is populated during call translation, after the
   current system traversal: do not use it as an uninitialized early filter.
   Precompute a closure of resolved references to the new declarations, including
   `upper` values/contracts and class-type use, or defer only this class until
   demand is observed. Choose the precomputed closure for the first implementation
   to keep declaration order explicit; confirm its no-use path does no Boogie
   construction. Review system-module cache hashing
   (`SystemModuleManager.MyHash`) and include the new availability state in ON-only
   cache identity if needed; retain the original OFF hash contribution and output.
3. **Ordinary method contracts.** Use `BoogieGenerator.Methods.cs`
   (`AddMethod_Top`/`AddMethod`) to emit the canonical bodyless lemma procedure.
   Reuse `Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs`
   (`TrCallStmt`/`ProcessCallStmt`) without a new semantic fast path: it already
   preserves argument well-formedness, subrange checks, ghostness, frames and
   result assignments, and instantiates ensures locally. Add only identity-based
   demand/audit plumbing where required. No completeness fact goes into a general
   `CanCallAssumption` collector or a caller-independent function definition.
4. **Quantifiers.** Use the existing ordinary predicate translation for `upper`
   and explicit source/generated patterns for membership and leastness. Preserve
   type and permission guards. Add stable QIDs, and compare the actual SMT with
   the measured final-shape kernel. Never repair a trigger by assuming an
   unproved upper-bound premise or arrow permission.
5. **Resolution, cloning and ghost checks.** Both resolvers must recognize the
   compiler-owned class/member declarations and preserve their identity through
   relevant cloning and import paths. Validate direct calls, forall proofs,
   statement-expression wrappers, module export, shadowing, and attempts to use
   results in compiled code. An ordinary forall-call statement requires a lemma
   without outputs; `complete` therefore uses an explicit forall proof with a
   local output or the verified no-output existential wrapper, not a new
   forall-call export rule. The selected lemma interface avoids introducing a
   new special-function handle translation; derived `sup` is an ordinary ghost
   function.
6. **Option and audit.** Reuse
   `Source/DafnyCore/Options/CommonOptionBag.cs` and `Options/DafnyCommands.cs`;
   update the existing option's help text. Because it now controls availability of
   built-in declarations, expose this same option to resolver-capable commands,
   especially `audit` (`AuditCommand.Options` currently has resolver options,
   while this option is only in verification options). Move it into the shared
   resolver option list without duplicate registration, retaining `OptionScope.Cli`. Record the builtin completeness
   dependency explicitly in `Auditor/Auditor.cs`, `Auditor/Assumption.cs`, and
   `Auditor/AuditReport.cs` as appropriate. Do not label it an unproved user
   function, and do not suppress audit of genuine user axioms. The current
   report walks ordinary modules and skips included declarations, so merely
   attaching an assumption to the system lemma can hide the dependency from a
   caller's report. Report canonical call-site dependencies or explicitly walk
   builtin dependency edges; test direct and included-source clients.
7. **Library and packaging.** In `Source/DafnyCore/Backends/Library/LibraryBackend.cs`,
   check the resolved program before selecting `AfterParsingClone`; reject a
   binary build with any canonical completeness reference until capability
   metadata exists. Cover the auto-build entry in `Source/DafnyDriver/DafnyNewCli.cs`
   and source import behavior in `DooFile.cs`/`DafnyFile.cs`. Add the separate
   opt-in source project described above, checked
   `lub`, `existsLub`, `uniqueLub`, `least`, total `sup`, polynomial evaluation,
   magnitude/Lipschitz lemmas, and the arbitrary-degree IVT. Split algebra that
   verifies OFF from completeness-dependent results. No shipped `{:axiom}`, free
   ensures, `assume`, or bodyless user lemma is acceptable as the final bridge.
8. **Documentation and gates.** Update `docs/dev/additional-axioms.md`,
   `docs/DafnyRef/Types.md`, relevant option documentation/help, and a release-note
   fragment. Add the integration driver and fixtures under the existing
   `git-issues`/`Inputs` paths; append the driver to `.github/review/regressions`
   and update `.github/review/base` for product commits per `REVIEW.md`. Reuse
   `.github/review/lit-verdicts.py`, `std-verdicts.py`, and the additional-axiom
   comparison jobs. Any expected-verdict update is a separate explained commit.

No change to native `real` operators, `Floor`, conversion checks, finite-set
axioms, function-value allocation, or compiler rational arithmetic is required.
Do not put unused new declarations in the unconditional prelude merely to avoid
writing demand-aware generation.

## 9. Test plan and release gates

### 9.1 Positive and negative proof tests

Run with both resolvers and both supported solver versions:

- General LUB theorem for a symbolic `iset<real>`, uniqueness, explicit leastness
  use, set extensional equality, finite-set conversion and total predicate wrapper.
- Open and closed bounded intervals; irrational supremum/root example; negative
  and translated intervals; sets captured from a heap snapshot under ordinary
  permissions; boxed real membership and subsets/newtypes where applicable.
- Full symbolic coefficient-sequence IVT and all its ordinary algebraic lemmas.
  No degree bound, continuity axiom, root axiom, or hidden cut bridge.
- `assert false` after a live bounded/nonempty call, after an empty-set call, and
  after an all-reals/unbounded-set call. Guard-total raw calls must reach their
  postconditions. Also reject a fixed invalid-result value and false leastness.
- Raw formula mutation tests: drop nonemptiness and derive contradiction from
  empty-set leastness; drop boundedness and derive contradiction from all-real
  upperness. These show the controls are capable of exposing the relevant bug.
- Confirm `Floor` on negative fractions and an irrational ghost witness remains
  consistent; reject an unjustified `as int`; preserve exact literal equalities.
- Reject compiled use of the ghost result; check ordinary higher-order use of
  the derived ghost `sup` wrapper and total-predicate conversion.
- Reject intrinsic use with the option OFF and reject user lookalike declarations
  attempting to obtain its trust. Test same-module and cross-module unrelated
  proofs for emission isolation. Test source imports and, before any binary
  release, transitive `.doo` capability enforcement.

A failed proof of false is a necessary control, not a model certificate. Record
whether the solver returned `sat`, `unknown`/incomplete, resource exhaustion or
wall-time timeout. Require the intended assertion location to fail; a parse,
resolution, precondition, or missing-witness error is not a vacuity control.

### 9.2 Identity and costs

Use pristine pinned baseline versus the implementation at the same flags and
source inputs. Compare complete Boogie and solver queries, excluding only
explicitly identified incidental path/header differences; do not normalize away
semantic commands or declaration order. Verify OFF identity on the suite and
standard library. Verify ON no-use identity for the marginal #205 addition.

The suite has its existing 16,000,000-RU cap, original options and
`/normalizeDeclarationOrder:0`. The source library uses its own limits and
`/deterministicLiteralHashes:1 /restartProver`; retain the existing target-specific
configuration. Record each program/declaration/VC, outcomes, resource count,
quantifier total and per-QID counts where profiled. Preserve unmatched rows and
every timeout or inconclusive result. Do not compare totals from different
coverage sets or quietly discard failing proofs.

For active-use costs, use the exact acceptance source and the three-or-more
alternatives with identical arithmetic/proof scaffolding, solver options and
limits. Separate outer activation from inner-pattern comparisons and from the
cost of the analytic library itself. Profile repeat calls, multiple independent
sets, many members/bounds, nested derived suprema, equality aliases, opaque
predicates, captured heaps, and interactions with existing additional families.

The design experiments below are evidence for the chosen shape. The implementation
still has to pass its own product-level identity and CI gates; an experiment
that deliberately emits nothing on an unchanged query does not test unwritten
registration, traversal or serialization code.

## 10. Milestones (one focused PR each)

1. **This design and evidence.** Review formulas, trigger choice, model argument,
   actual measurements and severity-ranked findings. No product change.
2. **Sealed ghost primitive and local emission.** Shared option, exact guarded
   contract, definitional helper, direct-call lowering, audit/help/manual/release
   notes, binary-build rejection for this new dependency, both-resolver positive
   and live vacuity controls, OFF and ON-no-use
   identity, suite/library gates. No full analysis library yet.
3. **Opt-in LUB source library.** Checked wrappers, existential theorem,
   uniqueness, total derived ghost `sup`, finite-set/predicate wrappers,
   source-import and temporary binary-build restrictions and their tests. No
   `.doo` release. Re-run cost gates.
4. **Polynomial algebra and continuity bound.** Horner sequence evaluation,
   magnitude and Lipschitz induction, OFF-verifying proof modules and their cost
   record. No further trusted principle.
5. **Arbitrary-degree IVT and client examples.** Join milestones 3 and 4; audit
   for zero user axioms, run the full acceptance/vacuity matrix and active-use
   profiles. This satisfies the issue's mathematical acceptance criteria.
6. **Optional later work, separately scoped.** Binary capability metadata and
   enforcement if desired; import/prove the cut construction and derive its
   canonical isomorphism with builtin reals. Neither is a prerequisite for the
   local primitive or the IVT.

## 11. Critical review

The review considered the intended real model, raw guards, actual boxed-set VCs,
source proof obligations, production integration, and activation/packaging. The
recommended direction is suitable for implementation after design review.
**Approval of this document is not approval to merge an unmeasured product change.**
The product-level gates remain mandatory.

| Severity | Finding and alternatives | Disposition / required evidence |
| --- | --- | --- |
| Critical if introduced | Dropping nonemptiness makes empty-set leastness contradictory; dropping boundedness makes all-real upperness contradictory. A partial API alone can conceal missing raw guards. | Resolved in the contract by a guard-total raw primitive. Live source controls and guard mutations exercise both guards. Retain the guarded formula in production. |
| Critical if introduced | Unrestricted reverse unboxing or treating every box as a real changes the fork's repaired representation. | Quantify native reals and box them; retain type guards. No new reverse-cast rule. |
| High soundness-argument defect | A model using the full powerset of universal `Box` conflicts with injective boxing of sets. | Use represented maps and real slices in a relative intended-model argument. This is not a proof that all existing Dafny axioms are consistent. |
| High activation/performance | Ordinary membership/bound triggers enumerate witness products and fire during unrelated bounded-set work. A `Sup` pattern alone cannot bind the flat formula's witness variables. | Reject the broad and flat-witness options. Local calls remove the trusted outer set quantifier; the measured explicit-request variant is only a fallback. |
| High proof reliability | Broad real-box leastness patterns lose the actual upper-bound target and instantiate on unrelated reals. Auto patterns also lose two full-proof batches on 4.12.1. | Select the explicit membership and upper-bound patterns. Actual boxed-set and full arbitrary-degree proof measurements support this selection; preserve their source/SMT regression cases. |
| High no-use identity | Zero completeness instances does not mean unchanged costs. Registering a new system class also does not imply its declarations are pruned. | Global dormant replay changed real-heavy VC costs. Require no-use emission control plus actual baseline/candidate Boogie/SMT and RU identity; current experiments do not validate unwritten registration code. |
| High integration | System classes are expected largely resolved already; the current system translation eagerly emits members; early `referencedMembers` is empty. | Construct fully resolved canonical ASTs, explicitly compute demand, and reuse ordinary contract translation. Verify both resolvers, cache identity and sibling-VC pruning. |
| High execution boundary | Arbitrary suprema need not be rational; an executable primitive would exceed current runtime real representations. | Ghost-only primitive/wrappers; compile-negative flow tests. No runtime arithmetic replacement. |
| High activation policy | A CLI verification option is not itself binary capability metadata. Merely placing a module outside default `Std` does not specify all packaging paths. | First release uses source inclusion and conservatively rejects binary production with the new dependency. Later `.doo` support requires explicit capability policy and transitive tests. Existing source-preserving re-resolution may also reject OFF imports; test it, do not claim an observed escape. |
| Medium interface | An arithmetic goal alone need not expose `upper(s,b)`. More automatic/broader patterns cost predictability. | Supply checked leastness helpers and document the explicit bound premise. The proof prototype demonstrates the interface is sufficient for IVT. |
| Medium quantified wrappers | Ordinary derived `sup` choice/function axioms can quantify over sets and interact with recursive client definitions. | Do not extend the primitive's closure claim to every wrapper. Verify total `sup` and profile wrapper/nested use in its own milestone. |
| Medium measurement validity | Abstract-kernel RU is not end-to-end Dafny cost; a final-sign IVT kernel is not the arbitrary-degree theorem; profiled live solver stderr can disrupt orchestration. | Separate evidence layers, retain exact input/output/provenance, and use clean source verification plus offline profiling. The full symbolic-sequence proof and real encoding were measured as separate layers. |
| Medium remaining release gate | No product implementation exists, so actual option-OFF/no-use structural identity, audit integration and full baseline-versus-candidate costs cannot yet have passed. | Explicit implementation milestones and required comparisons; no fabricated candidate totals or implied release approval. |
| Low contract precision | Two raw calls on the same eligible set can have different results when one uses an invalid witness or bound. | Uniqueness applies when both call guards hold. The derived total `sup` has its own explicit fallback definition. |

The main alternatives at each decision point are therefore settled: infinite
represented sets; one local guarded existential/witness principle; explicit
membership/upper-bound inner patterns; the shared option; ordinary canonical
system declarations with ordinary method-contract translation; a source-only
first library release with an enforced binary-build boundary. The remaining
uncertainties concern implementation gates and wrapper scaling, not an open
choice between unmeasured trigger designs.


## 12. Pinned code references

| Topic | Path and relevant location at `62bae981` |
| --- | --- |
| Fork trust, option, workflow rules | `AGENTS.md`; `REVIEW.md`; `docs/dev/additional-axioms.md` |
| Native `Int`, `Real`, `Floor` | `Source/DafnyCore/DafnyPrelude.bpl`, lines 597–604 |
| Boxing and typed reverse casts | Same file, lines 185–207, 224–226, 246–248 |
| Real/iset type and allocation predicates | Same file, lines 259–278, 321–337 |
| Finite versus infinite sets | Same file, lines 690–706, 834–892 |
| Iset comprehension lowering | `Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs`, lines 790–836 |
| Literal translation | Same file, `LiteralExpr`/`BigDec` translation around lines 1182–1185 |
| Real-to-int integrality check | `Source/DafnyCore/Verifier/BoogieGenerator.Types.cs`, lines 1362–1375, 1480–1488 |
| Function-value constraints | Same file, arrow axioms around lines 30–465 |
| System AST resolution | `Source/DafnyCore/Resolver/ProgramResolver.cs`, `ResolveSystemModule`, around lines 150–181; `SystemModuleManager.ArrowSubtypeConstraint` |
| Binary packaging | `Source/DafnyCore/Backends/Library/LibraryBackend.cs`, lines 57–63; `DooFile.cs`, lines 143–155; `Source/DafnyDriver/DafnyNewCli.cs`, `--library` project handling |
| Existing builtin member registration | `Source/DafnyCore/AST/SystemModuleManager.cs`, `Floor` and `AddRotateMember` around lines 165–219 |
| System declaration traversal | `Source/DafnyCore/Verifier/BoogieGenerator.cs`, lines 802–832 |
| Local call/output checks | `Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs`, `TrCallStmt` and `ProcessCallStmt` |
| Procedure contracts | `Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs`, `AddMethod` |
| Function handle path avoided by this design | `BoogieGenerator.ExpressionTranslator.cs`, `TranslateMemberSelectExpr`; `BoogieGenerator.Functions.cs`, `FunctionHandle` around lines 597–841 |
| Shared option definition/registration | `Source/DafnyCore/Options/CommonOptionBag.cs`, lines 244, 480, 709; `Options/DafnyCommands.cs`, lines 23–24 |
| Triggered-definition pruning warning | `Source/DafnyCore/Verifier/BoogieGenerator.cs`, `AddOtherDefinition`, lines 58–80 |
| Default standard-library inclusion | `Source/DafnyStandardLibraries/src/Std/dfyconfig.toml`; `Source/DafnyStandardLibraries/Makefile` |
| Executable rational representation | `Source/DafnyCore/Backends/CSharp/CsharpCodeGenerator.cs`; `Source/DafnyRuntime/DafnyRuntime.cs` (`BigRational`) |
| Baseline/candidate measurement harness | `.github/review/lit-verdicts.py`; `.github/review/std-verdicts.py`; `.github/review/additional-axioms.py`; `.github/workflows/review.yml` |
