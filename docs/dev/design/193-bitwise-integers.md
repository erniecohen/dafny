# Bounded integer bitwise operations and the bitvector/sequence bridge

Design and critical review for [issue 193](https://github.com/erniecohen/dafny/issues/193),
answering [the design request](https://github.com/erniecohen/dafny/issues/193#issuecomment-6757568251).
This is a design deliverable, not a production implementation.

**Source reviewed:** `review/4.11.0` at
[`62bae981952337d312cbb3d43c39f9813cca6084`](https://github.com/erniecohen/dafny/tree/62bae981952337d312cbb3d43c39f9813cca6084).
Paths and symbols below refer to that immutable tree. The binding policies are
`AGENTS.md`, `REVIEW.md`, and `docs/dev/additional-axioms.md` at that revision.

**Publication update:** drift through
[`5f8855a4d9314e37f87d9900682ad71cc8434a55`](https://github.com/erniecohen/dafny/compare/62bae981952337d312cbb3d43c39f9813cca6084...5f8855a4d9314e37f87d9900682ad71cc8434a55)
was also reviewed. The backend, runtime, width-analysis and separate-compilation
findings remain applicable. That revision adds issue 97's lambda-precondition
family to the shared additional-axioms mode; implementation validation must
include it. All measurements below remain tied to the earlier source pin.

## 1. Recommendation and scope

Give bounded integer newtypes **integer semantics directly**. Verify their bitwise
expressions against ordinary, terminating integer reference functions, and compile
them to operations on their existing integer representation. Do not insert a
bitvector conversion in either path. Treat interoperability with existing `bvN`
expressions as a separate problem: expose a small collection of true native-BV
consequences through finite local instances under `--additional-axioms`.

Use `seq<bool>` for the public little-endian view. Define it and prove its
properties in Dafny. The existing `Std.Arithmetic.LittleEndianNat` already supplies
sequence/value inverses, injectivity, and carry/borrow infrastructure. No new
sequence axioms or primitive `bit` type are needed.

The first surface supports unsigned, bounded values in integer-based newtypes:

```dafny
// Proposed syntax; requires --integer-bitwise.
newtype {:bitWidth 64} U64 = n: nat | n < 0x1_0000_0000_0000_0000
newtype {:bitWidth 12} U12 = n: int | 0 <= n < 0x1000

function mask(a: U64, b: U64): U64 { a & !b }
function top(a: U64): U64 { a >> 63 }
```

`bitWidth` is a semantic property, independent of `{:nativeType}` and the backend.
The operator for complement is Dafny's existing **`!`**, not `~`. Ordinary integer
newtype arithmetic remains checked mathematical arithmetic. Modular `+`, `-`, and
`*` in the bridge specification refer to `bvN`; they do not change integer
newtypes into wrapping arithmetic types.

The language change uses a new default-off `--integer-bitwise` option. The native
BV bridge facts join the existing default-off `--additional-axioms` option.
Neither option implies the other. The initial release does not expose unbounded
integer operators or negative signed ranges. Explicit library functions remain
available for specifying such extensions later.

**Review decision:** the architecture is recommended for staged implementation.
It is not approval to ship an axiom family or a compiler implementation without
the proof, vacuity, compatibility, and performance gates below. Measurements in
this document distinguish real reference proofs, native proofs, deliberately
trusted bridge projections, and the vacuous baseline. In particular, a cheap
consumer of an assumed bridge is not a proof of that bridge.

## 2. Exact mathematical definitions

All divisions and remainders in this section are Euclidean integer operations.
Widths `n` are natural numbers. Define `P(0)=1` and `P(n+1)=2*P(n)`, so `P(n)=2^n`.
Let `norm(n,a)=a % P(n)` for every integer `a`. These are ordinary recursive
definitions, not additional axioms.

The following definitions are total, including for negative integer arguments;
the initial operator surface still requires unsigned operands.

```text
bitsNat(0, a)       = []
bitsNat(n+1, a)     = [a % 2 == 1] + bitsNat(n, a / 2)

value([])          = 0
value([b] + s)     = (if b then 1 else 0) + 2*value(s)

andNat(0, a, b)    = 0
andNat(n+1, a, b)  = (if a%2==1 && b%2==1 then 1 else 0)
                     + 2*andNat(n, a/2, b/2)

notNat(n, a)       = P(n)-1-norm(n,a)
orNat(n, a, b)     = notNat(n, andNat(n, notNat(n,a), notNat(n,b)))
xorNat(n, a, b)    = andNat(n, orNat(n,a,b), notNat(n,andNat(n,a,b)))
andNotNat(n,a,b)   = andNat(n,a,notNat(n,b))

shlNat(n,a,k)      = if k<0 || n<=k then 0
                     else (norm(n,a)*P(k)) % P(n)
shrNat(n,a,k)      = if k<0 || n<=k then 0
                     else norm(n,a)/P(k)

popNat(0,a)        = 0
popNat(n+1,a)      = a%2 + popNat(n,a/2)
```

Here `k` is an integer; the conditional prevents an application of `P` at a
negative argument. The out-of-domain branches make the reference functions total,
not the source operations legal. The source rule is `0 <= k <= n`. In particular,
shift by the width is legal and produces zero.

Define rotations separately at zero width. For `n>0`, put `r=k % n`, using integer
remainder, and `u=norm(n,a)`:

```text
rolNat(0,a,k) = 0
rorNat(0,a,k) = 0
rolNat(n,a,k) = (u*P(r)) % P(n) + u/P(n-r)
rorNat(n,a,k) = u/P(r) + (u%P(r))*P(n-r)       // n>0
```

The reference rotation is total for every integer `k`. The first source release
retains the existing BV count restriction `0 <= k <= n`, including identity at
both endpoints. Count normalization in the reference model is never same-width
bitvector addition or subtraction.

For a bit query define `bit(n,a,i)` by `bitsNat(n,a)[i]`, with `0 <= i < n`.
Its arithmetic characterization is `(a / P(i)) % 2 == 1`. The public population
count is `popNat(n,a)` and returns `nat`, bounded above by `n`.

These definitions intentionally separate three things: the mathematical width,
the original newtype predicate, and the chosen target representation. Masking at
the mathematical width cannot justify an arbitrary extra newtype predicate.

### 2.1 The sequence model and bijections

For native `bvN`, write `U_N(x)` for the existing `x as int` and `I_N(a)` for the
native integer-to-BV operation. The source cast into `bvN` retains its ordinary
fit checks even though the underlying SMT operation is total.

```text
bits_N(x)       = bitsNat(N, U_N(x))
fromBits_N(s)   = value(s) as bvN, requiring |s| = N
```

The ordinary library must prove the following scalar theorems. Parameters are
universal in the mathematical statement, but the initial API uses lemma
parameters, not automatic globally quantified postconditions.

1. `|bitsNat(n,a)| = n` and `value(bitsNat(n,a)) = norm(n,a)`.
2. `0 <= value(s) < P(|s|)` and `bitsNat(|s|,value(s)) = s`.
3. For `|s|=|t|`, `value(s)=value(t)` iff `s=t`.
4. `fromBits_N(bits_N(x))=x` and, for `|s|=N`,
   `bits_N(fromBits_N(s))=s`.
5. For `0 <= i < n`, pointwise AND, OR, XOR and complement have their Boolean
   meanings. For example,
   `bitsNat(n,andNat(n,a,b))[i] = (bitsNat(n,a)[i] && bitsNat(n,b)[i])`.
6. For unsigned `a`, `0 <= k <= n`, and `0 <= i < n`, the left-shift bit is
   `i>=k && bitsNat(n,a)[i-k]`; the right-shift bit is
   `i+k<n && bitsNat(n,a)[i+k]`. Interpret the right side with guarded selection.
7. For `n>0`, rotation-left bit `i` is bit `(i-k)%n` of the input; rotation-right
   bit `i` is bit `(i+k)%n`. At zero width there are no indices.
8. `popNat(n,a)` is the number of true entries of `bitsNat(n,a)`.

Proof strategy: induction on width for the reference functions; induction on
sequence length for the inverse; the quotient/remainder identity
`a = 2*(a/2) + a%2` connects the two. Fixed-length injectivity follows by comparing
the low bit and dividing the remaining value by two. Shift equations follow from
Euclidean division by powers of two; the rotation sum combines disjoint fields.
The arithmetic carry and borrow proofs reuse the existing sequence library.

`Source/DafnyStandardLibraries/src/Std/Arithmetic/LittleEndianNat.dfy` is an
abstract module with `BASE()>1` and digit values below `BASE()`. Refine it at two,
map `false/true` to `0/1`, and reuse `FromNatWithLen`, `LemmaNatSeqNat`,
`LemmaSeqNatSeq`, `LemmaSeqEq`, `SeqAdd`, `LemmaSeqAdd`, `SeqSub`, and `LemmaSeqSub`.
Prove the bool/digit adapter and equality to the reference recursions. Do not
install the module's `...Auto` lemmas globally merely for convenience.

For the total integer definitions, apply the natural-number library to
`norm(n,a)`, and prove that normalization preserves the first n bits; do not
pass a possibly negative a to a natural-number API. Use `FromNatWithLen`, not
the minimal-length `FromNat`, which maps zero to the empty sequence.

### 2.2 Public view API

The mandatory, width-generic API uses ordinary verified ghost library functions
`bitsNat(n: nat, a: int): seq<bool>` and `value(s: seq<bool>): nat`. Thus the
canonical view of an arbitrary existing bitvector is `bitsNat(N, x as int)`; no
width-polymorphic BV type is needed. Supply examples and ordinary scalar adapter
lemmas for widths 8, 16, 32, and 64, without limiting the mathematical library to
those widths.

An ergonomic ghost `x.Bits()` member may be added for `bvN` and annotated
newtypes in its own small PR. It lowers to the same definition with the static
width; it adds no axiom. Do not block the core feature on this sugar. Reversing
the view is an explicit checked cast: `value(s) as bvN`, or `value(s) as T` for a
newtype. The latter must additionally establish T's original predicate. A generic
`FromBits<T>` cannot infer the width or assume arbitrary T membership and is not
part of the first interface.

## 3. Native BV facts: exact basis and classification

For each positive concrete width `N`, let `M=2^N`, `U=nat_from_bvN`, and
`I=nat_to_bvN`. Names such as `and_bvN` are the actual generated function families.
The native meanings originate in `BoogieGenerator.cs`, not in a handwritten
width-specific block of `DafnyPrelude.bpl`.

### 3.1 Existing facts, unchanged

```boogie
axiom (forall x: bvN :: { U(x) }
  0 <= U(x) && U(x) < M && U(x) == native_unsigned_bv2int(x));

// Existing --additional-axioms member, issue 33:
axiom (forall a: int :: { U(I(a)) }
  0 <= a && a < M ==> U(I(a)) == a);
```

The native inverse `I(U(x))=x` is proved with the existing native theory; it is
not another added axiom. Width zero uses the existing singleton and zero
conversions, not an SMT `BitVec 0` sort. All source fit checks remain.

### 3.2 Ten basic bridge schemata

The following are exact mathematical formulas, with `x,y:bvN` and `k:int`.
**The proposed translator emits only local instances at actual source terms. It
does not emit a top-level universal quantifier for these schemata.** Each such
instance is a trusted additional fact and must have the written justification
and controls required by the fork.

| ID | Formula | Status |
|---|---|---|
| A | `U(and_bvN(x,y)) = andNat(N,U(x),U(y))` | New local additional fact |
| C | `U(not_bvN(x)) = M-1-U(x)` | New local additional fact |
| + | `U(add_bvN(x,y)) = (U(x)+U(y)) % M` | New local additional fact |
| − | `U(sub_bvN(x,y)) = (U(x)-U(y)) % M` | New local additional fact |
| × | `U(mul_bvN(x,y)) = (U(x)*U(y)) % M` | New local additional fact |
| / | `U(y)!=0 ==> U(div_bvN(x,y)) = U(x)/U(y)` | New guarded local fact |
| % | `U(y)!=0 ==> U(mod_bvN(x,y)) = U(x)%U(y)` | New guarded local fact |
| L | `0<=k<=N ==> U(LeftShift_bvN(x,I(k))) = (U(x)*P(k)) % M` | New guarded local fact |
| R | `0<=k<=N ==> U(RightShift_bvN(x,I(k))) = U(x)/P(k)` | New guarded local fact |
| ≤ | `le_bvN(x,y) <==> U(x)<=U(y)` | New local additional fact |

For a source count `c:bvK`, put `s=U_K(c)` and keep the guard `s<=N`. The guarded
instance uses `I_N(s)` as the native count and s as the arithmetic exponent.
The guard ensures that this conversion does not wrap. Do not assert the same
equation for an arbitrary large c or mistake a wrapped count for the source
integer count. When the existing
shift optimization introduces an intermediate width, use its actual source
count and conversion obligations, not an assumed same-width shape.

This is a small practical basis, not a claim of independence or a uniquely
minimal axiom set. All ten formulas are consequences of native semantics plus
the reference definitions. Adding no facts at all is logically sufficient, but
the current solver often does not find the mixed-theory proofs at useful cost.
The practical goal is to minimize the trusted interface while proving the
derived operations once.

| Required operation or property | How it is obtained |
|---|---|
| AND, complement | A/C plus the verified reference properties |
| OR | Native proof of `a \| b == !(!a & !b)`, then A/C and the reference definition |
| XOR | Native proof of `a ^ b == (a \| b) & !(a & b)`, then the preceding lemmas |
| ANDNOT | Abbreviation `a & !b`, then A/C; no new primitive |
| Integer meaning of `+ - * / %` on BV | The five arithmetic facts, preserving the nonzero guard |
| Shift meanings | L/R plus power-of-two and bit-index library lemmas |
| Single-bit access | Right shift, AND with one, and the verified low-bit theorem |
| Equality | `I(U(x))=x` gives injectivity; equality congruence gives the other direction |
| `< > >=` | Native Boolean/order identities plus ≤ and equality |
| Rotations | Shift/OR decomposition with the source count bounds, using the repaired native path |
| Popcount | Verified fold over the reference bits; compiler optimization is a separate refinement obligation |
| Sequence bijections and pointwise equations | Ordinary Dafny induction, no new trusted sequence fact |

The native identities in this table are actual proof obligations, not new
axioms. At any concrete width a user can prove them in a width-specialized
scalar lemma. The compiler may mechanically generate adapters for widths used
by a program, but it must run their proof or use a separately reviewed native
identity, not mark generated adapters verified by fiat.

A direct OR/XOR/rotation value fact is a redundant accelerator, not part of the
initial trusted basis. The measurements include broader direct-contract
projections to study client cost. A later PR may add such an accelerator only
with the derivation from this basis, actual integration measurements, and its
own live controls. Do not silently promote all benchmark contracts to axioms.

### 3.3 Combining a bridge with range-checked source casts

When an observed expression is explicitly `U(op(I(a),I(b)))`, the translator
may materialize the *composed* instance:

```text
0<=a<M && 0<=b<M
  ==> U(and_bvN(I(a),I(b))) = andNat(N,a,b)
```

It is derived by substituting `I(a),I(b)` in A and the existing bounded round
trips. Unary, arithmetic and count-bearing cases are analogous, with the full
original guards. This is a local syntactic optimization, not an unconditional
cast axiom or permission to ignore a source fit check. The design probes compare
the separate BV-argument contract with this directly composed source contract.
The difference matters: a solver can spend substantial resources discovering
the round trips even after it has the operation equation.

## 4. Instantiation, triggers, and matching-loop analysis

### 4.1 Recommended implementation

Emit scalar facts only in the local verification context that contains an
eligible source occurrence. A method parameter is a ground SMT constant in that
context; “ground” does not mean a numeric literal. The new facts have **no
quantifier and no trigger**. Deduplicate within one emitted assumption, not
across methods, branches, or verification conditions.

Eligibility starts at an **explicit integer observation**, such as
`(a & b) as int`, not at every BV operation. Otherwise enabling the shared option
would unnecessarily convert pure BV queries into mixed integer/BV queries.
For Boolean BV comparisons, initially require direct integer-to-BV operands or
an explicit integer observation in the same scalar expression. Pure native
OR/XOR helper proofs should remain native-only. Composed cast instances require
actual resolved integer-to-BV conversion nodes of the matching width, with
integer ancestors; do not infer them from printed text or from a real-valued cast.

Use the existing distinction between `CanCallAssumptionForVerification` and
`CanCallAssumption` in `Verifier/BoogieGenerator.ExpressionTranslator.cs` as the
integration boundary. Do not put these facts into shared function-definition,
consequence, type, or let axioms. Keep the normal well-formedness checks, literal
markers, boxing, and native expressions. Only the local consequence is added.

Initially, the collector visits scalar expression structure and preserves its
existing path guards. It stops at a binder, lambda, comprehension, and an
expression that changes heap context. It does not walk a called function's
body. A local source expression may contain function applications as operands,
but their normal permissions and type checks remain in force. Total reference
definitions need no newly invented precondition permission.

Reject an eligible parent operation if an operand *contains* an excluded
binder, old-expression, comprehension, lambda, or statement-expression subtree;
merely stopping traversal into it would still leave it captured in the
parent's proposed fact.

Checking only for a binder node in the visited subtree is insufficient:
`BoogieGenerator.TrForallStmt.cs` and comprehension well-formedness call the can-call builder on
stripped or substituted bodies. Add an explicit scalar-emission context, default
disabled for exported/binder callers, and reject candidates with free source
`BoundVar` nodes or internal `BoogieWrapper`/`BoogieFunctionCall` operands unless
the enclosing translation has explicitly established their local bindings.
Forall statement proof bodies can opt in after their arbitrary values have been
materialized as locals. Preserve the caller's can-call options unchanged. No
bridge instance grants a permission to an operand's user-defined function.

An ordinary scalar Dafny lemma can contain the relevant expression and prove
its contract using these instances. Quantified clients invoke that lemma inside
a `forall` statement body, where its arbitrary bound value is represented by a
local constant. This supports arbitrary finite widths and quantified properties
without automatic harvesting beneath every binder in the first release. It is
not a promise that every quantified expression will verify with an empty body.

Automatic binder-aware collection is optional later work. It must preserve
bound variables, type antecedents, heaps, layers, and conditional evaluation;
it needs its own review and measurements. A naive tree visitor that lifts a
bound expression into a free assumption is unacceptable.

### 4.2 Every quantified family in scope

| Family | Trigger policy and argument |
|---|---|
| Existing native conversion wrapper | Keep `{U(x)}`. Its body exposes the native interpretation and range, not a deeper wrapper application. This proposal does not alter it. |
| Existing bounded round trip | Keep `{U(I(a))}`. It introduces no deeper conversion term. Retain issue 33/74 stress and interaction controls. |
| New bridge schemata | No quantified emission; only finite local instances. No E-matching loop can originate from a new bridge trigger because none is installed. |
| Ordinary recursive reference definitions | Use normal Dafny function/fuel translation, unchanged. Recursive expansion is governed by the existing fuel/layer mechanism; no unbounded hand-written recurrence axiom is added. Audit the actual emitted definition triggers in the implementation. |
| Scalar library theorems | Ordinary verified lemma calls. No new `...Auto` theorem or globally quantified postcondition is required. |
| Pointwise/extensional proof bodies | Prove a scalar index theorem or use the ordinary `forall` statement translation. If an optional quantified convenience theorem is later exported, list and measure its actual emitted patterns separately. |

This is a claim about the new instantiation mechanism, not a proof that the
entire existing Dafny prelude plus arbitrary user quantifiers is loop-free.
Adding a true ground fact can still change solver search, equality classes, and
the behavior of existing triggers. That is why full cost measurements remain
mandatory even without new quantified axioms.

### 4.3 Alternatives and why they lose

**Trigger on a bit selection.** An apparently attractive rule is

```text
forall i { bit(r,i) } :: 0<=i<N ==> bit(r,i)=bit(x,(i-k)%N)
```

for `r=rotateLeft(x,k)`. In the satisfiable case `r=x`—zero and an all-ones word
are examples—E-matching modulo equality can generate `bit(x,(i-k)%N)`, then
`bit(x,(((i-k)%N)-k)%N)`, and continue. A semantic bound on `i` does not prevent
creation of the new terms. Shift rules and a recursive extractor
`bit(a,i) -> bit(a/2,i-1)` have analogous problems. Do not use these triggers.

**Trigger on the whole observed operation.** A global arithmetic rule triggered
by `{U(add_bvN(x,y))}` exposes only operand observations and arithmetic. This
has a better within-family term-dependency argument than a selection trigger.
It still applies module-wide and interacts with conversions, equality, and
other families; no measurement justifies preferring it to finite instances here.
The comparison alternative `{U(x),U(y)}` also creates a Cartesian product of
observations. Explicit local comparison instances avoid that cost.

**A globally recursive arithmetic/bit theory.** This is compact to write but
duplicates Dafny's verified recursion and introduces its own fuel/induction and
matching problems. Keep the arithmetic reference functions as ordinary Dafny
definitions and prove the useful facts once.

## 5. Soundness argument and solver boundary

Interpret a positive-width bitvector as a Boolean vector indexed by `[0,N)`.
Interpret `U` as its little-endian weighted sum and `I(a)` as the vector of
`a mod 2^N`. Interpret the reference functions by their terminating definitions.
The native fixed-size bitvector theory specifies exactly these operations; its
unsigned conversion is the one Dafny wraps, despite historical differences in
SMT operator naming. See the primary
[SMT-LIB fixed-size bitvector specification](https://smt-lib.org/theories-FixedSizeBitVectors.shtml).

For A, split both operands into their low Boolean bit and quotient by two and
induct on N. For C, complement changes each weight's coefficient from `b` to
`1-b`, whose sum is `2^N-1-U(x)`. Native addition, subtraction, and multiplication
are integer arithmetic modulo `2^N`. Unsigned division and remainder agree with
integer division/remainder when the divisor is nonzero. Order compares the
weighted sums. Multiplication/division by powers of two gives L/R, including
zero result at count N. These arguments establish each basic bridge in the same
model; they are not separate ad hoc assumptions about unrelated functions.

The reference library adds definitions and proved theorems. The ten facts and
their composed cast instances are consequences of native semantics plus those
definitions. Therefore, relative to that trusted theory T and any compatible
existing additional-fact families S,

```text
Models(T union S union newFacts) = Models(T union S).
```

Inventory S in the implementation review. The reviewed source pin includes the bounded round trip,
closed integer literal identities, heap-succession instances, allocation
application patterns, override call-layer instances, and assign-such-that call
permissions. The newer publication revision also includes issue 97's guarded
source-lambda precondition equivalence. Test the proposed facts together with
that family and any further families present at implementation time. This
argument is relative to their soundness;
it does not establish consistency of the whole verifier or correctness of Z3's
implementation. A vacuity test is a diagnostic, not a substitute for the model.

Never drop the following distinctions:

- `a as bvN` requires fit even though native `I(a)` is total modulo conversion.
- Native BV division/remainder at zero have total SMT meanings; source division
  remains illegal there. The proposed integer bridge is guarded.
- Width zero has one value and the empty bit view. Test it before a `%N` rotation
  expression and emit no positive-width native family for it.
- The original predicate of an integer newtype is not implied merely by a masked
  result. It must be checked at every operation, including synthetic members.

### 5.1 Issue 147 has already been repaired

At this pin, `BoogieGenerator.cs::AddBitvectorRotateFunction` keeps native
extended rotations at positive powers of two, and uses shift/OR definitions at
other positive widths. `docs/dev/bitvector-rotation-soundness.md` records the
proof and regression controls.

The faulty rewrite composes counts modulo `2^N` before reducing modulo N.
Reduction is preserved for every count exactly when `N` divides `2^N`, i.e. at
positive powers of two. Thus the known unsound composition is absent from the
non-power-of-two translation. The shift/OR helper's intended rotation meaning
still depends on the source check `0<=k<=N`; do not assert it for arbitrary
out-of-range helper arguments.

New integer rotations use the integer reference formulas and do not generate
native rotation symbols. The compatibility proofs for existing `bvN` preserve
the shipped repair. This rules out the identified rewrite failure in those
paths; it cannot guarantee that every other native-solver rewrite is correct.

## 6. Surface, resolution, and separate compilation

### 6.1 Width and eligibility

The first implementation requires an explicit `{:bitWidth N}` on a revealed,
integer-based newtype. N is a positive compile-time integer, independent of type
parameters. Type synonyms preserve the underlying declaration's descriptor;
a distinct nested newtype must initially declare its own width. Do not infer
one from a native storage type or a name such as `u64`.

Store a separate immutable descriptor, for example `UnsignedBitWidth(N)`, on
the resolved declaration and the resolved bitwise expressions. Resolve the
literal width before operator inference. Validate the underlying integer range
after the type constraint is resolved. Initially reuse the sound interval
discovery used by native-type analysis, separated from its backend selection:
it must establish a nonnegative interval within `[0,2^N)`. If it cannot, issue a
targeted diagnostic and require an explicit recognizable bound in the constraint.
Do not assume an unproved range from the annotation. A later declaration-proof
mechanism can accept more complicated constraints without changing semantics.

This initial rule accepts both examples in section 1 and stricter predicates
conjoined with their bounds. The source count rule is `0<=k<=N`; a bit-index
query instead requires `0<=i<N`. A declaration whose predicate is only `n<256`
over `int` does not establish the missing lower bound.

Native type analysis cannot do this job as currently written. It is target
dependent, chooses a sufficient carrier rather than the declared mathematical
width, and runs late. In `NativeTypeAnalysis.cs::FigureOutNativeTypeForIntegerNewtype`,
`TargetTypeCoversAllBitPatterns` specifically distinguishes an exact carrier
range from stricter constraints. `NewtypeDecl.cs::NativeType.Bitwidth` is zero
for signed carriers and JavaScript `number`; it is not a general semantic-width
field.

Inference from an exact, recognizable `[0,2^N)` constraint is a reasonable
later convenience. It should be separately specified and tested. Starting with
the annotation prevents a change in compiler target or native-type selection
from changing complement and rotation meaning. Rewriting a predicate should
not silently select a different width.

### 6.2 Operations and obligations

Support `&`, `|`, `^`, unary `!`, `<<`, and `>>` with existing precedence and
associativity. Binary bitwise operands must have the same resolved newtype;
normal literal inference applies. Distinct same-width newtypes need explicit
conversions. Counts may use the existing integer/BV count forms, but use natural
or integer source counts in examples to avoid unnecessary conversion proofs.

The result type is the operand newtype, with a proof obligation for its **whole
original predicate**, just as for existing newtype arithmetic. The descriptor
does not state closure. For example:

```dafny
newtype {:bitWidth 8} Nonzero = n: nat | 0 < n < 256
// !255 and 1 & 2 are zero, so neither can be returned as Nonzero.
```

Synthetic `.RotateLeft(k)` and `.RotateRight(k)` return the same type, with both
the count and result-membership checks. `.PopCount()` returns `nat`; a named
`bit(i)` query returns `bool`. Introduce these in their own milestone, after the
binary/unary path. Resolve member collisions as errors; do not silently replace
a user-defined member with a special operation.

Compute derived operations in the integer model and check the final cast to T.
In particular, a named ANDNOT or rotate helper must not demand that every
intermediate mathematical complement/shift belongs to an extra-constrained T.
The source expression `a & !b` does check its intermediate `!b`, as its ordinary
typing requires. A reference definition over integers can avoid that unnecessary
intermediate constraint while checking its final result.

Existing `+ - * / %`, integer order and equality keep their meanings. Explicit
wrapping library functions can expose `(a+b)%2^N`, but this design does not alter
the arithmetic operators on integer newtypes. An opaque export hides the integer
representation and therefore hides the builtin bitwise capability initially;
export ordinary wrappers if clients need it. A copied `bitWidth` attribute on
an abstract export is not permission to recover that representation.

### 6.3 Both resolvers and issue 194

Add gated, descriptor-aware constraints to both resolvers, rather than making
the existing `IsBitvector` predicate true of integer types. Keep the current
paths intact when the language option is false. Record distinct resolved integer
bitwise operations, or an equally explicit width-bearing lowered node, so the
verifier and compilers never discover an integer where they expect
`AsBitVectorType.Width`.

Relevant legacy paths are
`Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs`
(unary, shift, and bitwise cases) and `XConstraint.cs` (`BooleanBits` and
`IsBitvector`). The refreshed paths are
`Resolver/PreType/PreTypeResolve.Expressions.cs`, `PreTypeConstraints.cs`,
`UnderspecificationDetector.cs`, and `TypeRefinementVisitor.cs`.
`AST/Expressions/Operators/UnaryOpExpr.cs`, `BinaryExpr.cs`, and
`Resolver/ConstantFolder.cs` also require explicit new cases.

For [issue 194](https://github.com/erniecohen/dafny/issues/194), this capability is
an applicable builtin operator. Keep that design's builtin-first resolution;
otherwise its ordinary user-operator call resolution may apply. A bridge
collector recognizes the resolved builtin native-BV operation identity, never
the source token `&` or a user operator with the same spelling. User-defined
operators do not inherit these axioms or compiler intrinsics. Equality of these
integer newtypes remains the existing builtin equality. Compound assignment, if
enabled by issue 194, reuses the same checked operation and evaluates the target
location once; this proposal adds no second assignment grammar.

### 6.4 Canonical reference functions and packaging

Add an **embedded ordinary Dafny source module**, provisionally `DafnyBitMath`,
with the exact reference definitions. Load it through the ordinary parse,
resolution, and translation pipeline before resolving clients whenever either
option is enabled. Start with this simple deterministic loading rule; optimize
demand loading only after the full enabled-mode measurements. With both options
off, do not construct or register the support module.

The existing standard-library loading paths provide the pattern:
`Pipeline/Compilation.cs`, `DafnyDriver/Legacy/SynchronousCliCompilation.cs`,
`DafnyFile.cs`, and `AST/Grammar/ProgramParser.cs`. Merely inserting unverified
recursive special-function contracts into `_System` is not acceptable: the
system module has a different resolution lifecycle.

In consumer verification, treat only that trusted bundled input as a preverified
library, using the existing library-input skip policy; do not reverify its
definitions and add new proof counts on every invocation. Its exact bytes are
verified independently before release as described below. This privilege comes
only from the trusted bundle loader, not a user source attribute or DOO claim.
Keep dedicated operation nodes through resolution and bind their model callees
after full program resolution, or explicitly add the support-module dependency
before resolving them. Injecting a sibling module alone does not establish
resolution order for a client that uses operators without importing the model.

The trusted loader retains a private map from operation kind to the resolved
declaration objects parsed from the known embedded bytes. Intrinsic lowering
and bridge RHS references use that map. Names, attributes, source URIs, and
untrusted DOO metadata cannot establish canonical identity. Aliases to the real
declaration are fine; a new declaration with the same spelling gets no special
meaning. Diagnose a source-name collision with the injected module when it is
enabled. A user refinement must not replace the original identity used internally.

The public ordinary form is:

```dafny
import Math = DafnyBitMath
// Canonical view, with explicit width:
// Math.bitsNat(64, x as int)
// Reverse after proving the bound:
// Math.value(s) as bv64
```

The initial proof companion is a separate optional source/binary distribution,
not an unconditional dependency of the default `Std` project. It contains the
LittleEndianNat refinement, bool adapter, and scalar native-BV helpers. Build
that companion with `--integer-bitwise` so its dependency on the support module
is recorded. An output library that exposes an injected model dependency must
carry that requirement; reject building such a library without the language
option rather than silently creating an unloadable artifact. Internal bridge
use under `--additional-axioms` alone does not change an existing library's
semantic interface.

The canonical reference definitions and initial bit-view API are **ghost**.
Runtime integer-bitwise expressions retain dedicated resolved operation nodes;
only verifier lowering calls the ghost reference functions. This avoids compiling
an unused arbitrary-integer/sequence module on a target such as C++ that cannot
compile its general representation. Executable rotate/popcount expressions must
likewise retain runtime operation identity; do not source-desugar them into
ghost function calls. Ghost `.Bits()` sugar can desugar completely.

Register `IntegerBitwise` as a global language option with
`OptionCompatibility.OptionLibraryImpliesLocalError`, following the existing
standard-library precedent in `Options/CommonOptionBag.cs`. An old/off-built
library loads into an enabled client; an enabled library requires an enabled
client. `--additional-axioms` remains the existing verification option, not a
new semantic-compatibility requirement for ordinary BV proofs.

Capability recognition uses the declaration/module's recorded language mode,
not just the root invocation's mode. DOO reload restores those module options;
an off-built library loaded by an enabled client must keep its formerly inert
attributes inert. Global support-module availability does not alter that rule.

DOO persists pre-resolution **source**, not the width descriptor. Preserve the
attribute, recompute the descriptor on reload, and reload the canonical model
from the installed trusted resource. Do not deserialize a private intrinsic tag
from arbitrary source or a library. The model dependency must name the support
ABI/version; a mismatch is an error. The serializer should not duplicate the
injected module as a second user-owned declaration. `DooFile.cs` and
`Backends/Library/LibraryBackend.cs` are therefore part of the change.

Split the companion's verification targets. Verify the exact support source and
the pure integer/sequence proofs as explicit ordinary source inputs with **both
feature options disabled** in CI. Verify native-BV adapters separately with the
additional facts enabled; prove the pure native OR/XOR identities without the
new bridges. Check termination,
preconditions, and every written contract. Audit for `assume`, `{:axiom}`,
bodyless unproved lemmas, and verification-skipping attributes. The pure-model proof run
must not use the accelerators whose meanings it helps justify.

## 7. Compilation on every backend

The contract is direct target integer code, with no runtime conversion through
`bvN` and no materialized bit sequence. Native carriers use native operations
where available. Arbitrary-precision targets use their integer runtime. Python
and wide JavaScript values cannot honestly be promised one fixed-width hardware
instruction per source operation.

Carry N independently through runtime-type simplification and code generation.
For full-range types with N=8/16/32/64 the usual native carrier is straightforward;
for N=12 in a 16-bit carrier, mask at 12. A stricter type may choose a carrier
smaller than N, so an intermediate complement or rotate may require a wider
unsigned carrier or arbitrary-precision helper even if the final value fits T.

Requirements common to all targets:

- Evaluate operands and count once, using existing temporary/IIFE mechanisms.
- Build masks from exact compile-time integers. Never emit `(1 << carrierWidth)-1`
  where the host masks or rejects the shift count.
- Handle `k==N` before a host shift when needed: shift result zero, rotate identity.
- Use unsigned carriers for promotions and logical right shift. Normalize narrow
  promoted results and mask at N, not at the target's automatically selected width.
- Perform the final representation conversion after the semantic operation.
  Conversion of a host popcount result to Dafny `nat` is normal integer adaptation,
  not a BV intermediate.
- A generated intrinsic must implement the integer reference function under the
  verified source preconditions. Mathematical proof success alone does not check
  the target's masking, signed promotion, or undefined-shift behavior.

| Backend | Native carrier path | Other widths and required work |
|---|---|---|
| C# | Unsigned `& \| ^ ~ << >>`; `BitOperations.RotateLeft/Right` for matching 32/64-bit widths; `BitOperations.PopCount` | `BigInteger` bitwise operators, masking, shifts, and shift/OR rotation. Use chunked popcount or a portable loop where the target runtime lacks a suitable API. |
| Java | Primitive `& \| ^ ~ << >>>`, explicitly zero-extending byte/short carriers; `Integer/Long.rotateLeft/Right` and `bitCount` at matching widths | `BigInteger.and/or/xor/not/shiftLeft/shiftRight/bitCount`, with N-bit masking. Guard Java's modulo-32/64 shift counts. |
| Go | Unsigned native operations, including `&^` for ANDNOT; `math/bits.RotateLeft8/16/32/64` and `OnesCount*` | Existing `_dafny.Int`/`big.Int` operations and masks; update both runtime copies where applicable. Right rotations can negate a bounded host count. |
| Python | Direct Python integer bitwise operators; `int.bit_count()` where supported, with a compatible population-count loop fallback; masked shift/OR rotations | The current runtime supports Python >=3.8, while `bit_count()` needs 3.10. Preserve that minimum or separately approve changing it. Python integers already provide the arbitrary-precision path. Mask complement/left shift at N; nonnegative inputs make right shift logical. |
| JavaScript | For N<=32, host operators plus unsigned normalization and exact masks; fixed-word popcount and guarded rotations | For native `number` widths 33..53, use correct 32-bit limbs or an explicit integer helper. For wider BigNumber values use repaired limb helpers, or a separately designed BigInt representation. Existing wide helpers cannot be reused unchanged; see below. |
| C++ | Unsigned fixed carriers and masks, guarded shift/OR rotations, compiler popcount builtin with a portable fallback | Current backend lacks arbitrary integers/nonnative newtypes. Give a precise unsupported-target diagnostic when an operation needs a wider carrier. Do not emit C++20 `std::rotl`/`std::popcount`: the current build selects C++17. |
| Rust | Unsigned u8/u16/u32/u64/u128, `rotate_left/right` at matching widths, `count_ones()`, explicit full-width shift handling | Carry semantic width through DAST and the Dafny-written compiler. Current pipeline lists rotations and nonnative newtypes as unsupported; the native operation path must be added. Retain explicit diagnostics for unsupported nonnative cases until separately implemented. |

For a nonnative width such as 65 or 257, the mathematical specification and
verification remain the same. C#, Java, Go, Python and JavaScript must use a
correct arbitrary-precision integer path. The initial C++/Rust exclusions must
be documented and tested as diagnostics; silently omitting those target rows
is not portable compilation acceptance.

### 7.1 Existing JavaScript defect found during the design audit

The pinned runtime's helpers take chunks modulo `2^53` and then apply
JavaScript's 32-bit bitwise operators. The public design diagnostic, using the
unchanged `DafnyRuntime.js` and BigNumber 9.1.2, reports:

| Direct runtime invocation | Actual | Required |
|---|---:|---:|
| `BitwiseAnd(2^31,2^31)` | -2,147,483,648 | 2,147,483,648 |
| `BitwiseAnd(2^32,2^32)` | 0 | 4,294,967,296 |
| `BitwiseOr(2^32,2^32)` | 0 | 4,294,967,296 |
| `BitwiseXor(2^32,0)` | 0 | 4,294,967,296 |
| `BitwiseXor(2^63,0)` | 0 | 9,223,372,036,854,775,808 |

This is a direct runtime reproduction, not an end-to-end compiled Dafny test.
The defect is tracked in [issue 246](https://github.com/erniecohen/dafny/issues/246).
The [diagnostic source](193-evidence/check-runtime.cjs) and
[recorded public output](193-evidence/javascript-diagnostic.txt) are retained here.
Evidence is in the `issue193-javascript-runtime` artifact of
[the public design run](https://github.com/erniecohen/dafny/actions/runs/42754841657).
The runtime SHA256 is
`f03542e162ef10523aeae806114eb9bfb800587cfc2f1590701c6d136ab67bb8`.
Correcting or bypassing these helpers is a P1 prerequisite for their use by this
feature. A new helper should use actual 32-bit limbs, normalize each limb to an
unsigned value, and preserve every high limb; test it end to end as well.

## 8. Concrete source change inventory

This inventory is for later workers; none of these product changes is in the
design PR. Prefer explicit integer-bitwise nodes/metadata over changing the
meaning of `Type.IsBitVectorType` throughout the compiler.

| Area / paths relative to `Source/DafnyCore/` unless otherwise stated | Required change |
|---|---|
| `Options/CommonOptionBag.cs`, `Options/OptionRegistry.cs`, `Options/OptionCompatibility.cs` | Register/help/document the language option, global library compatibility, project configuration, and legacy spelling. Keep shared axiom option semantics intact. |
| `Pipeline/Compilation.cs`; `Source/DafnyDriver/Legacy/SynchronousCliCompilation.cs`; `DafnyFile.cs`; `AST/Grammar/ProgramParser.cs` | Load the fixed ordinary reference source before resolution under either option; retain provenance; preserve both-off path exactly. |
| New embedded `DafnyBitMath.dfy` and optional proof companion | Total reference definitions, independently verified contracts, bool/digit correspondence, scalar native adapters. Include packaging/source-hash tests. |
| `AST/TypeDeclarations/NewtypeDecl.cs`, `AST/Cloner.cs`, `AST/Modules/ScopeCloner.cs` | Width descriptor, unresolved/resolved clone policy, provenance preservation, and opaque-export behavior. `ScopeCloner` copies attributes while hiding a newtype; do not recover capability from that attribute alone. |
| `Resolver/NativeTypeAnalysis.cs`, `Resolver/ModuleResolver.cs` | Extract backend-independent interval validation; establish metadata before operator inference; keep native selection separate and later. |
| Legacy and refreshed resolver files listed in section 6.3 | New gated bit-capability constraints and same-type rules, diagnostics, result type and count handling. Do not globally broaden old constraints. |
| `AST/Expressions/Operators/UnaryOpExpr.cs`, `BinaryExpr.cs`; `Resolver/ConstantFolder.cs` | Distinct resolved integer operations and constant folding with exact N-bit masks, leaving ordinary arithmetic folding unchanged. |
| `AST/SystemModuleManager.cs` and `Resolver/ModuleResolver.cs` BV member registration | Both the legacy generic `_bv` member path and refreshed width-specific path need any new ghost `.Bits()` member. Newtype rotate/popcount members use descriptor identity and collision checks. |
| `Verifier/Expressions/TranslateBinaryExpr.cs`; `Verifier/BoogieGenerator.ExpressionTranslator.cs` | Direct integer reference calls for new operations; canonical resolved identities; local BV bridge collection at integer observations, preserving literal/boxing behavior. |
| `Verifier/BoogieGenerator.ExpressionWellformed.cs`; `Verifier/Statements/BoogieGenerator.TrForallStmt.cs` and other audited can-call callers | Original predicate/count checks; explicit scalar emission context; no unbound or exported facts; preserve can-call options. |
| `Verifier/BoogieGenerator.cs` | Keep existing native BV declarations, conversion axioms and rotation repair. Add only the required canonical-symbol plumbing/local formula builders; no unconditional width-family changes. |
| `DafnyPrelude.bpl` | No new trusted sequence theory and no unconditional extra declarations. Any eventual helper signature change must be gated and justified by actual implementation needs. |
| `Backends/SinglePassCodeGenerator/SinglePassCodeGenerator.Expression.cs` | Pass N past runtime-type simplification; emit direct integer operations, truncation, counts and single evaluation. Do not apply modular truncation to ordinary integer arithmetic. |
| `Backends/CSharp/CsharpCodeGenerator.cs`; `Backends/Java/JavaCodeGenerator.cs`; `Backends/GoLang/GoCodeGenerator.cs`; `Backends/Python/PythonCodeGenerator.cs`; `Backends/JavaScript/JavaScriptCodeGenerator.cs`; `Backends/Cplusplus/CppCodeGenerator.cs` | Each operation/width/carrier path in section 7, generated-code tests and runtime reference tests. |
| `Backends/Dafny/DafnyCodeGenerator.cs`, `Backends/Dafny/AST.dfy`, `Backends/Rust/Dafny-compiler-rust-definitions.dfy`, `Backends/Rust/Dafny-compiler-rust.dfy` | Width-bearing DAST operations, Rust native rotations/popcount, integer result adaptation and explicit unsupported cases. Regenerate checked-in generated artifacts through the normal pipeline. |
| `Source/DafnyRuntime/` C#, Java, Go/gomod, JS and other affected runtimes | Correct masks/count helpers and any popcount fallback; JavaScript wide integer repair or new correct helpers. |
| `DooFile.cs`; `Backends/Library/LibraryBackend.cs` | Persist source annotation and support dependency, rebuild descriptors, never accept a forged intrinsic identity. |
| `Source/DafnyLanguageServer.Test/Synchronization/IncrementalBitvectorMembersTest.cs` and related option invalidation tests | Rebuild/cancel cached member and type information when widths, constraints or options change. Test both resolver modes. |
| `docs/DafnyRef/`, `docs/dev/additional-axioms.md`, `docs/dev/news/193.feat`, `.github/review/regressions` | User semantics/options, every bridge model/control, release note and permanent regression registration. Product milestones update `.github/review/base` under the normal policy. |

Two traps deserve explicit code-review checks. First,
`CheckWellformedSpecialFunction` currently checks rotation arguments and count
but does not call `CheckResultToBeInType` for a constrained newtype result.
Prefer verifier lowering to the integer semantic result followed by the
original type's membership check, while retaining runtime operation identity.
Otherwise add that obligation explicitly. Second,
`Cloner.cs` currently clones a `SpecialFunction` through the ordinary Function
case. Lower executable special operations to descriptor-bearing operation nodes
and ghost views to ordinary calls, or explicitly preserve their private identity
through every export/refinement clone.

## 9. Measured cost and what the measurements establish

All reported measurements in this section come from public repository CI, using
the unchanged compiler at the source pin, reporting
`4.11.0+fcb2042d.review.2d6fd450`. The
[complete tables and qualifications](193-evidence/public-measurements.md),
machine-readable scope/VC records, source hashes, and
[reproduction instructions](193-evidence/README.md) are retained alongside this
document. The accompanying scripts and Dafny files are design probes, not a
production feature implementation.

### 9.1 Comparative native and bridge probes

[Run 42754841657](https://github.com/erniecohen/dafny/actions/runs/42754841657)
uses benchmark commit `79d20aa13e8584e1f4113d79a8d20c699e90e6ab`, Z3 5.1.0 and
4.12.1, both resolvers, 200,000 RU per batch, and a 15-second safety timeout.
All comparative modes use `--additional-axioms`, one prover core per invocation,
`/normalizeDeclarationOrder:0`, and `/restartProver`. The corpus covers 17
operations at widths 8, 16, 32 and 64. Every well-formedness scope passed.

Each solver/resolver configuration produced 32 files and 1,272 verification
scopes. All 128 invocations have complete results and matching input hashes.
For each solver the two resolvers have identical outcomes, resource counts and
per-VC results across all 1,272 scopes. There were no parser failures, missing
logs or wall-time timeouts.

| Mode | What is being measured | Correctness results, 5.1.0 / 4.12.1 |
|---|---|---|
| Reconstructed `assume false` baseline | Same per-operation contracts, discharged vacuously; the downstream original files were not supplied | 68/68 / 68/68 proved, vacuously |
| Current native attempt | Empty-body attempt at the same true contracts with the unmodified encoding | 0/68 / 0/68 proved |
| Separate BV-argument bridge contract | A deliberately trusted contract over BV arguments, then called with cast integers | 15/68 / 5/68 proved; remaining cases out of resource |
| Composed source-argument bridge contract | A deliberately trusted operation-and-round-trip contract at original integer operands | 68/68 / 68/68 proved |
| Pure native reduction identities | Actual proofs of the OR/XOR/ANDNOT reductions, with no new contract axiom | 12/12 / 12/12 proved |
| Integer reference wrappers | Actual definitional checks of reference-function APIs; not the sequence theorem | 68/68 / 68/68 proved |

The current native mode has 65 resource failures and 3 ordinary proof errors on
5.1.0; 63 resource failures and 5 errors on 4.12.1. These results show that this
empty-body attempt failed, not that the semantic equations are false or that no
assumption-free native proof is possible. The separate-contract results are
particularly poor: supplying an operation equation does not by itself solve
conversion proof search.

Here is the full requested width table for 5.1.0. Each cell is
**reconstructed assume-false baseline / composed-contract client**, in RU. Every
listed pair is Correct, including all associated well-formedness checks. The
parallel 4.12.1 table and all failed raw/native observations are in the linked
complete report, rather than being omitted or averaged together.

| Operation | 8 | 16 | 32 | 64 |
|---|---:|---:|---:|---:|
| AND | 7,826 / 6,773 | 9,497 / 7,779 | 12,866 / 9,797 | 19,613 / 13,835 |
| OR | 7,924 / 6,961 | 9,556 / 7,969 | 12,820 / 9,985 | 19,348 / 14,017 |
| XOR | 7,740 / 6,775 | 9,379 / 7,781 | 12,684 / 9,799 | 19,303 / 13,837 |
| ANDNOT | 8,064 / 7,019 | 9,719 / 8,025 | 13,056 / 10,043 | 19,739 / 14,081 |
| Complement | 4,143 / 3,744 | 4,990 / 4,270 | 6,711 / 5,328 | 10,153 / 7,444 |
| Left shift | 6,601 / 5,814 | 7,653 / 6,403 | 9,689 / 7,520 | 13,725 / 9,693 |
| Right shift | 6,524 / 5,766 | 7,576 / 6,355 | 9,612 / 7,472 | 13,648 / 9,645 |
| Addition | 5,256 / 4,436 | 6,968 / 5,444 | 10,392 / 7,460 | 17,240 / 11,492 |
| Subtraction | 5,376 / 4,460 | 7,152 / 5,468 | 10,704 / 7,484 | 17,808 / 11,516 |
| Multiplication | 5,767 / 4,578 | 9,062 / 5,592 | 19,519 / 7,626 | 55,802 / 11,696 |
| Unsigned division | 6,055 / 4,459 | 10,593 / 5,479 | 25,831 / 7,523 | 80,865 / 11,607 |
| Unsigned remainder | 6,117 / 4,459 | 10,701 / 5,475 | 26,013 / 7,507 | 81,213 / 11,571 |
| Single-bit access | 6,301 / 5,529 | 7,205 / 6,118 | 8,917 / 7,235 | 12,245 / 9,408 |
| Equality | 4,358 / 4,521 | 5,926 / 6,121 | 9,062 / 9,321 | 15,334 / 15,721 |
| Unsigned `<=` | 4,398 / 4,535 | 5,966 / 6,135 | 9,102 / 9,335 | 15,374 / 15,735 |
| Rotate left | 7,441 / 6,231 | 9,123 / 7,239 | 12,517 / 9,255 | 19,367 / 13,287 |
| Rotate right | 7,443 / 6,239 | 9,125 / 7,247 | 12,519 / 9,263 | 19,369 / 13,295 |

Across the 68 correctness scopes the baseline/composed totals are
919,685 / 551,032 RU on 5.1.0 and 949,863 / 551,516 RU on 4.12.1. This is not a
speedup claim over a sound baseline: `assume false` is vacuous, and the composed
client deliberately trusts a contract. The nonzero baseline work includes
checks that precede the false assumption. The useful finding is that the shape
of the local contract matters, and that a conversion-heavy API can be expensive
even after its meaning is granted.

The pure native helper proofs have identical outcomes/counts on both solvers
and resolvers:

| Actual proved native identity | 8 | 16 | 32 | 64 |
|---|---:|---:|---:|---:|
| OR by De Morgan | 1,950 | 1,950 | 1,950 | 1,950 |
| XOR by OR/AND/complement | 4,234 | 8,938 | 26,921 | 97,268 |
| ANDNOT by OR/XOR | 3,846 | 7,914 | 23,538 | 84,686 |

The integer reference wrapper checks cost 2,994–7,511 RU per correctness scope
on 5.1.0 and 3,002–7,519 on 4.12.1, independent of width in this corpus. These
are definitional checks of the proposed verification interface, not evidence
that all algebraic properties prove themselves automatically.

The composed-contract corpus trusts one direct fact per operation, including
derived operations. It is broader than the proposed ten-formula basis. Therefore
these costs are a **projection of client cost**, not measurements of the final
local emitter or the complete minimal-basis proof library. Actual native-adapter
and compiler integration costs remain a milestone gate. The main acceptance
list is measured above; popcount was not in that list and its implementation
and reference theorem remain a separate milestone with its own cost rows.

### 9.2 A substantive sequence theorem, proved without the new facts

The independent
[sequence proof run](https://github.com/erniecohen/dafny/actions/runs/42755593223)
uses benchmark commit `e046a64c1ce1f2c49127a2e281d3eda68c01b0d0` and
`--additional-axioms=false`. Its
[exact source](193-evidence/sequence-model.dfy) contains no assumptions, axiom
contracts or bodyless lemmas. It proves by induction on arbitrary width that
the value of the pointwise Boolean operation on little-endian sequences equals
the corresponding recursive integer reference, for AND, OR, XOR and ANDNOT.

All four solver/resolver configurations verify **17 scopes, zero errors**.
The two resolvers agree exactly on all outcomes and resource counts. A 2,000,000
RU cap is used for this generic library proof, separately from the 200,000 RU
client experiment.

| Correctness scope | Z3 5.1.0 | Z3 4.12.1 |
|---|---:|---:|
| Generic theorem, arbitrary width and operation code | 956,774 | 913,863 |
| Width 8 client | 32,721 | 32,497 |
| Width 16 client | 32,725 | 32,497 |
| Width 32 client | 32,725 | 32,497 |
| Width 64 client | 32,725 | 32,497 |
| Whole-file total, including well-formedness | 1,344,903 | 1,295,842 |

This is real evidence for putting the pointwise model in a verified library.
It does not yet prove the complete bijection, arithmetic, popcount or native
adapter library. The probe's OR/XOR/ANDNOT references use their direct recursive
Boolean definitions; the production library must additionally prove their
equivalence to the smaller AND/complement basis in section 2.

### 9.3 Vacuity results: the remaining gate is real

No `assert false` control verified. Some controls returned genuine proof errors;
others exhausted resources and are **inconclusive**, not satisfactory vacuity
controls:

| Controls, 68 per row | Rejected / inconclusive, 5.1.0 | Rejected / inconclusive, 4.12.1 |
|---|---:|---:|
| Separate BV-argument bridge | 52 / 16 | 50 / 18 |
| Composed source-argument bridge | 44 / 24 | 42 / 26 |

The AND/OR/XOR/ANDNOT controls are inconclusive at every measured width in both
forms. The complete report identifies the remaining arithmetic and rotation
cases individually. All recursive reference definitions remain in those
contexts; they were not hidden to turn a weaker query into a claimed success.

Complementary finite ground checks evaluate the recursive reference at concrete
arguments and assert the actual native operation equation. All 100 cases are
SAT in each solver/resolver configuration, including boundary counts and widths
1 and 3. They take 16–40 RU each. These are witnesses for those instances, not
consistency of the entire quantified context. The production axiom PR must
deliver effective full-context controls for every emitted basic fact and cast
composition; this design does not waive that rule.

### 9.4 Suite and standard-library evidence

The unchanged pin already has a successful
[full review run](https://github.com/erniecohen/dafny/actions/runs/42748364832).
Its retained `additional-axiom-resources` artifact reports the following pinned
OFF/ON observations. These are **the existing shared option**, not the cost of
the feature proposed here:

| Scope | Recorded entries | Pinned OFF RU total | Pinned ON RU total |
|---|---:|---:|---:|
| Suite, Z3 5.1.0 | 1,163 programs / 7,634 proof batches | 1,511,902,554 | 1,555,572,761 |
| Source library, Z3 4.12.1 | 2,190 declaration entries | 1,031,783,435 | 1,018,791,320 |
| Source library, Z3 5.1.0 | 2,190 declaration entries | 1,568,958,377 | 1,185,435,800 |

Totals sum the recorded resource counters, including capped/error outcomes, and
are not elapsed time or totals of only successful proofs. The suite inventory
also records 42 missing/empty logs per mode, reflecting entries without proof
batches; do not turn those into zero-cost verified programs. Outcome or resource
values differ in 618 suite rows and 465 library rows per solver. The source
artifact's generic caveat about old library ordering should be read with the
newer pinned `REVIEW.md`: the controlled source-library runner now uses
`/deterministicLiteralHashes:1` and `/restartProver`.

These totals are historical measurements of the stated pin, not measurements of
the newer publication revision. In particular, the later issue 97 change records
two different ON-mode standard-library outcomes under Z3 5.1.0; use the actual
implementation predecessor's vectors for its eventual comparison.

There is **no measured suite/standard-library delta for the proposed production
feature**, because no production implementation was requested or made. Loading
the canonical model and enabling new local facts can affect existing enabled
proofs, so the existing run cannot discharge that future gate. Every axiom
milestone must compare the real emitter against the pinned predecessor on the
complete suite and source library, with the standing OFF/ON verdict gates and
controlled resource vectors. The design-only PR changes no product code,
existing suite or standard-library proof source, verifier option, or shipped
standard-library binary.

### 9.5 Experiment provenance and reproducibility

The first probe run failed its infrastructure check because a shallow checkout
did not contain the source pin. No proof results from that failed setup are used.
The corrected run fetched the pin explicitly and checked product-source identity
before using its published compiler artifact. An early probe shape also cast
shift counts to BV explicitly, causing separate wide-count well-formedness
failures; the authoritative corpus uses the existing natural-count syntax.

The retained evidence includes the authoritative generator, runner, ground
checks, proof source, every client/control row, every scope/VC row, and receipts
with compiler/solver/source hashes. The public Actions artifacts additionally
contain full solver inputs and diagnostics. Re-running the runner exits zero
after recording expected diagnostic failures; acceptance is determined from
the result fields, not the workflow's green status. No failed or absent result
is relabeled a successful proof.

## 10. Alternatives and decisions

| Decision | Alternative and benefit | Cost or objection | Recommendation |
|---|---|---|---|
| Bit view representation | New `bit` type makes numeric digit notation natural | Adds type/boxing/conversion machinery with little benefit over Boolean values | `seq<bool>` plus a proved bool/digit adapter |
| Newtype verification | Lower every operation through `bvN` | Reuses existing machinery but imports mixed-theory conversion costs into every integer program | Direct integer reference semantics |
| Trusted theory | Add a complete sequence/bit axiom system | Concise specification; large consistency and trigger surface | Ordinary definitions and verified library; only native bridges are additional facts |
| Native bridge basis | One direct axiom per required operation | Simple clients and promising projected costs | Begin with the ten basic facts and prove derived operations; add a redundant accelerator only with evidence |
| Proof strategy | Ask the solver to bit-blast every client | No added bridge fact; poor observed mixed-theory scaling | Small pure-BV identity proofs once, then integer/library reasoning for clients |
| Trigger strategy | Global operation or selection quantifiers | Automatic coverage of deeply nested expressions | Finite local scalar instances; selection-trigger loops are a concrete counterexample |
| Width discovery | Use selected native carrier width | Little frontend work | Explicit semantic width, because carrier selection is backend dependent and can include extra constraints |
| Width syntax | Infer from any provable bounded predicate | Fewer annotations | Explicit attribute first; exact-interval inference later, never target-dependent inference |
| Operator API | Only library functions | Lowest frontend complexity and a useful first proof milestone | Library reference/proof foundation plus requested builtin operator syntax in staged PRs |
| Signed/unbounded integers | Infinite two's-complement semantics immediately | Natural for some bitwise laws and some host runtimes | Defer: width-dependent complement, shift interpretation and result domains need separate user decisions |
| Canonical support | Privileged user-spelled `andNat` or an axiom-marked special function | Easy wiring | Fixed embedded ordinary ghost source, independently verified and referenced by private provenance |
| Bit-view spelling | Synthetic `x.Bits()` only | Convenient and infers width | Generic `bitsNat(N,x as int)` first; `.Bits()` as optional checked sugar |
| Shared-option loading | Late demand-loaded model injection | Could reduce enabled-mode cost | Deterministic pre-resolution loading first, measured on the full suite; demand loading is a later optimization |
| Nonnative compilation | Reject every nonnative width | Small compiler patch | Integer runtime fallback where supported; honest C++/Rust target exclusions until their missing representations exist |

## 11. Permanent tests and acceptance gates

### 11.1 Mathematical and semantic tests

Create a regression driver under
`Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/` with separate
positive, negative, and vacuity inputs. Test both resolvers and both pinned Z3
versions. The model/proof files must contain no assumptions, axiom declarations,
bodyless lemmas or skipped verification; trusted *translator* instances must
be inventoried separately.

The positive matrix proves every operation's integer meaning at 8, 16, 32 and
64, including ANDNOT, single-bit access, equality and order. It also proves
fixed-length sequence round trips, leading-zero preservation, pointwise laws,
carry/borrow arithmetic, shifts, rotations and popcount. Exercise the generic
library at arbitrary natural width, then the concrete native adapters. Include
zero-width BV explicitly, plus widths 1, 3, 6, 7, 12, 31, 33, 63, 65 and 128
to distinguish mathematical width from a machine carrier and to retain #147's
non-power-of-two coverage.

For each basic fact and any composed instance, provide:

1. A positive equality that uses the actual emitted instance.
2. A live false assertion and an incorrect result in a satisfiable context
   containing that instance, both rejected for the intended obligation.
3. A concrete native/reference model witness, including boundary operands.
4. Negative guard cases: `-1`, `2^N`, zero divisor, count `-1` or `N+1`, and bit
   index N. Illegal source casts/counts must fail even when their parent bridge
   is eligible during well-formedness checking.

Do not count timeout, unknown, out of resource, parser error or an earlier failed
precondition as the intended vacuity rejection. If a control is difficult because
of recursive reference unfolding, repair the actual production proof interface
or use additional well-justified controls, and report the remaining cases.
Do not hide conflicting definitions and claim the original context was checked.

### 11.2 Frontend, binding, and compatibility tests

Cover full-range and stricter newtypes over both `nat` and `int`, malformed or
unprovable width bounds, different same-width newtypes, nested newtypes, aliases,
revealed/opaque exports, imported modules, refinements, and member-name collisions.
Specifically test that complement, AND, rotations and from-bits cannot fabricate
membership in a nonzero/even-only subtype. Exercise the synthetic-member path,
not only binary expressions.

Spoofing controls use fake canonical module/function names, a copied attribute,
an untrusted DOO claiming a private intrinsic, and a user operator from #194.
None may acquire native lowering or trusted reference meaning. Source/DOO
round trips test compatible and incompatible option combinations; an off-built
library with a previously ignored malformed `bitWidth` attribute must remain
unchanged when loaded by an enabled client. IDE tests change a width and a bound
and ensure cached member/type information is invalidated.

Exercise bridge operands containing nested functions, old states, statement
expressions, lambdas and comprehensions. The initial exclusions must be visible
in actual emitted output. Scalar lemmas invoked inside `forall` proof bodies
must work without accidentally exporting free variables or granting function
permissions. Pure BV programs with no integer observation must receive no new
mixed-theory local facts.

### 11.3 Compilation tests

For every supported target and carrier, inspect generated code to confirm that
the operation uses native integer operations or the documented integer runtime
helper, with no BV temporary or sequence allocation. Test both automatic native
selection and explicit wider/native-false representations without changing the
answer. Compare execution with an independent integer reference, exhaustively
for small widths and with a fixed large-width boundary corpus.

The boundary corpus includes zero, one, all ones, the highest bit, values around
bits 31/32/53/63, alternating masks, counts 0/1/N-1/N, and nested rotations in
both directions. Add Java narrow-carrier sign tests, JavaScript wide-helper
tests, Python 3.8-compatible popcount fallback tests, C++ undefined-behavior
sanitizer runs and Rust debug-mode full-width shifts. Verify operand evaluation
counts. Unsupported widths/targets require the specified diagnostic, not a
silently skipped test.

### 11.4 Option and cost gates

Run the four combinations of language option and shared axiom option. The
options-off path is compared with a build without the feature: same parsing,
resolution, diagnostics, generated Boogie/SMT, verdicts and controlled RU for
all pre-existing inputs. Unknown `bitWidth` attributes remain inert with the
language option off. Guard before constructing support AST nodes or collecting
local facts; suppressing them only after construction can still perturb IDs and
declaration order.

For existing `--additional-axioms=true` users, compare the real new model/facts
with the previous shared-option behavior. Run the entire suite and source
standard library, retaining matched and unmatched rows and every verdict change.
Use the pinned suite configuration and the current library controls
`/deterministicLiteralHashes:1` and `/restartProver`. Do not substitute an easier
order, larger limit, or isolated proof for a required full-run gate.

The initial scalar-client target is 200,000 RU per acceptance batch at all four
widths; the generic library proof has its separate measured budget. These are
new-feature targets, not changes to existing suite limits. Report setup-inclusive
counts as observed and classify outcomes from the solver/result field. Preserve
per-operation rows and distinguish once-only library proof cost from client cost.
Profile instantiations, nested depths and many distinct operations in one VC;
no new bridge quantifier should exist. Test zero/all-ones self-rotation equalities
as anti-loop cases even though the selected design avoids the dangerous patterns.

Any product PR follows the repository's usual separate verdict-update procedure.
A favorable average or improved unrelated proof does not excuse an unexplained
regression. An extra fact with a demonstrated problematic cost may receive its
own option only under the measured exception in `AGENTS.md`; do not introduce
per-operation switches preemptively.

## 12. Milestones small enough for separate PRs

| Milestone | Deliverable | Gate before moving on |
|---|---|---|
| M0 — design/evidence | This document, independent review and reproducible public probes | Owner/coordinator accepts the architecture and explicit unresolved release gates; no product change |
| M1 — reference model | Fixed ghost source, exact definitions, pure integer/sequence proofs, base-two adapter and independent proof runner | Both-off proof/audit; full bijections and core pointwise laws; no trusted BV bridge yet |
| M2 — support loading and ABI | Gated ordinary-source loading, private identity map, preverified-input policy, optional companion packaging and DOO dependency handling | Both-off identity; enabled full-suite cost; spoofing, clone/export, option and load-order tests |
| M3a — BV arithmetic observations | Local +/−/×/division/remainder/order facts, explicit scalar contexts, guarded composed casts | Each formula's model and effective controls; both resolvers; OFF/ON full-suite and source-library comparison |
| M3b — BV Boolean/shift observations | AND, complement and L/R; verified OR/XOR/ANDNOT/bit/rotation adapters | Same gates; actual minimal-basis acceptance table; preserve #147 and pure-BV queries |
| M4 — integer operator frontend/verifier | Width descriptor/validation, both resolvers, constants, `& \| ^ ! << >>`, direct integer reference lowering | Extra-predicate and opaque-export negatives; language-off identity; semantic source tests |
| M5a — first native compilation | Common generator plus C# and its arbitrary-integer fallback | Generated-code and exhaustive/boundary execution tests; same verified integer semantics |
| M5b — Java/Go | Their direct carriers, arbitrary integers and count rules | Target-specific generated/executed tests and runtime changes |
| M5c — Python/JavaScript | Compatible popcount fallback, correct wide JS helpers, direct integer paths | Reproduce and repair/bypass the observed JS defects; minimum-version tests |
| M5d — C++/Rust | Supported native carriers, descriptor propagation and explicit nonnative diagnostics | Sanitizer/debug boundary tests; generated artifact rebuild for the Dafny-written pipeline |
| M6a — rotation/bit queries | Descriptor-aware synthetic operations with final predicate checks | Both resolver member paths, clones, count endpoints, #147 nested controls |
| M6b — popcount | Reference theorem plus target intrinsics/fallbacks | No new axiom unless separately justified; proof-cost and runtime rows at all four widths |
| M7 — convenience and release | Optional `.Bits()` sugar, exact-interval width inference if desired, manual/examples, full acceptance report | All promised targets/operations complete, no open vacuity gate, full cost report and reviewed verdict changes |

M3a and M3b may run independently of frontend work after the support ABI is
stable. Backend work can proceed in parallel after M4's operation representation
is fixed. Split a listed milestone further if its target-specific work would
not fit a focused review. Each product PR carries its own regression driver,
release-note fragment and required `.github/review/base`/regression updates;
do not accumulate a large unreviewed axiom/backend change for the final PR.

## 13. Critical review, ranked by severity

**P0 means potential false verification or a wrong executable result for a
verified program. P1 blocks shipping or acceptance. P2 is an API/scope or
maintainability issue.** These are design/implementation obligations, not claims
that this documentation change introduces a product defect.

| Severity | Finding | Disposition |
|---|---|---|
| P0 | A user-spoofable reference function makes a bridge assert an arbitrary meaning | Fixed-source provenance and independent verification are mandatory; no name/attribute/DOO privilege |
| P0 | Omitting width, divisor, count or cast guards can state a false equation | Exact guards and width-zero branch specified; retain negative controls |
| P0 | An arbitrary newtype is not closed under the operations; current special-function WF omits its result check | Check the complete predicate for every result, including rotations and from-bits |
| P0 | A copied attribute on an opaque type could expose hidden representation facts | Require a revealed integer newtype; never recover capability from an abstract export's attribute |
| P0 | Lost binders, heap context or user-call permissions can turn a local fact into an invalid assumption | Audited scalar context, excluded operand subtrees, no new user-call permissions, no global can-call modifications |
| P1 | Triggering on bit selections can loop through equal operands/results and changing symbolic indices | Rejected; finite full-value instances and scalar library proofs selected |
| P1 | Current prototype vacuity controls have resource failures | **Open release gate.** No false assertion proved, but the inconclusive controls must be replaced or resolved in the actual implementation |
| P1 | Projected client costs do not measure the actual emitter, minimal-basis adapters or full-suite impact | **Open implementation gate.** Retain the projections as evidence and repeat with production code and full corpus |
| P1 | Native carrier width changes with backend and is sometimes zero in metadata | Separate semantic descriptor; never infer meaning from NativeType.Bitwidth |
| P1 | Existing JavaScript helpers produce wrong wide results | Reproduced publicly; repair or bypass before use by the feature |
| P1 | Host promotions, full-width shifts, Python minimum version, C++/Rust unsupported paths | Per-backend lowering contract, fallback/diagnostics, and execution gates specified |
| P1 | Executable injected model code would add unsupported BigInt/sequence compilation to native-only clients | Ghost model; dedicated runtime operation nodes; only verifier calls the reference |
| P1 | Cloning, DOO mode restoration, provenance and module order can lose or invent capability | Explicit loader/ABI milestone and source/DOO/incremental tests |
| P1 | Both-off AST construction or model injection can perturb default output and proof cost | Early gates plus byte/verdict/RU comparison; documentation-only change does not establish future identity |
| P2 | Direct facts for every derived operation enlarge the trusted review surface | Start with the practical basis; add redundant facts only with a derivation and actual cost evidence |
| P2 | Mandatory width annotation and scalar lemmas add some user work | Deliberate initial tradeoff; exact-interval inference and binder-aware automation may be added separately |
| P2 | A generic bit view does not by itself provide signed or infinite-two's-complement source semantics | Explicitly deferred; the total model is an extension point, not an implicit signed API commitment |

The strongest evidence for the recommendation is the combination of a genuine
generic sequence proof, cheap native reductions, the poor separate-conversion
projection, and the successful composed projection. The main residual risk is
not the bit-sequence mathematics; it is faithful integration with scope,
reference identity, result predicates, solver search and target integer rules.
Accepting this design should authorize the focused milestones, not waive any
axiom or compiler acceptance gate.

## 14. Required outward documentation

The Reference Manual and option help should state the following normative rules:

- `--integer-bitwise` enables bounded integer newtypes with a positive explicit
  `{:bitWidth N}` and established unsigned bounds. The width is independent of
  native representation. With the option off, the existing language behavior
  and ignored-attribute behavior remain.
- `& | ^ ! << >>` use the N-bit unsigned view; complement is within N bits.
  Counts satisfy `0<=k<=N`; shifts by N are zero and rotations by N are identity.
  Every result must satisfy the original newtype constraint.
- Integer-newtype arithmetic and equality keep their existing meanings. A
  signed carrier does not introduce signed source bit semantics.
- The canonical ghost view is `bitsNat(N,x as int)` and has exactly N entries,
  least significant first. Reverse casts require the value bound and any
  destination predicate. Document `.Bits()` only when its separate milestone ships.
- `--additional-axioms` adds native-BV interoperability facts without changing
  source conversion/count checks. Document scalar-local coverage and explicit
  lemma use under binders; do not claim complete automatic quantifier support.
- Describe every supported backend/fallback and the precise C++/Rust exclusions.
  “Native” means direct target integer operations where the representation
  supports them; it does not promise a hardware instruction on every backend.
- Document the optional proof companion, its model ABI/dependency and library
  option compatibility. Add examples with full-range, narrower-width carrier,
  additional-predicate, and `{:nativeType false}` newtypes.

No syntax, axiom, backend behavior or help text is changed by this design PR.
The document and evidence were prepared with OpenAI ChatGPT assistance, including
independent mathematical and compiler-integration review.
