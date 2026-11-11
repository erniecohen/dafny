# Bounded integer bitwise operations and the bitvector/sequence bridge

Design and critical review for [issue 193](https://github.com/erniecohen/dafny/issues/193),
revised for the [round-2 request](https://github.com/erniecohen/dafny/issues/193#issuecomment-6806298004).
This is a design deliverable and executable evidence, not a production implementation.

**Source reviewed:** `review/4.11.0` at
[`428e348b0b125cb67505f03a0026b3f497f98e48`](https://github.com/erniecohen/dafny/tree/428e348b0b125cb67505f03a0026b3f497f98e48).
Paths and symbols below refer to that immutable tree. The binding policies are
`AGENTS.md`, `REVIEW.md`, and `docs/dev/additional-axioms.md` there. The preceding
design is [PR 247 at a61b06c7](https://github.com/erniecohen/dafny/tree/a61b06c71d742ff19b280592d26d1a0e2ce9c87c).

**Evidence pins are distinct.** The original public measurements in sections 9.1,
9.2 and 9.4 use `62bae981952337d312cbb3d43c39f9813cca6084`; they are historical
calibration, not measurements of the new pin or the ten-fact implementation.
The source delta adds #97's guarded lambda-precondition equivalence at
[`5f8855a4`](https://github.com/erniecohen/dafny/commit/5f8855a4d9314e37f87d9900682ad71cc8434a55)
and repins Boogie to `3.5.5-review.c5a9f2ef` at `428e348b`. No native BV wrapper or
prelude change occurs in this interval. Round-2 controls explicitly exercise
#97; an enabled flag alone would not instantiate a missing lambda family.

## 1. Recommendation and scope

Give bounded integer newtypes **integer semantics directly**. Verify their bitwise
expressions against fixed compiler-owned integer reference functions, prove
their properties in an ordinary Dafny companion, and compile
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
| Rotations | Proved native shift/sum decomposition, L/R/+, power-product and quotient/remainder lemmas, with the source count bounds |
| Popcount | Verified fold over the reference bits; compiler optimization is a separate refinement obligation |
| Sequence bijections and pointwise equations | Ordinary Dafny induction, no new trusted sequence fact |

The native identities in this table are actual proof obligations, not new
axioms. At any concrete width a user can prove them in a width-specialized
scalar lemma. The compiler may mechanically generate adapters for widths used
by a program, but it must run their proof or use a separately reviewed native
identity, not mark generated adapters verified by fiat.

A direct OR/XOR/rotation value fact is a redundant accelerator, not part of the
initial trusted basis. The historical measurements include broader direct-contract
projections; section 9 distinguishes those from the round-2 checked derivations.
A later PR may add an accelerator only with its derivation from this basis,
actual integration measurements, and its own live controls. Do not silently
promote benchmark contracts to axioms.

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

### 3.4 Exact derived adapters from this basis

The [Boolean proof sources](193-evidence/derived-boolean/) contain the exact
OR, XOR and ANDNOT lemmas at widths 8, 16, 32 and 64. The deterministic
[bit/rotation generator](193-evidence/derived-rotation/generate.py) emits the
corresponding exact Dafny lemmas, native helper proofs, clients and controls at
all four widths; its [pure mathematics](193-evidence/derived-rotation/pure-math.dfy)
is independently verified with additional axioms OFF. Run the common generator
in section 9.5 to materialize every file. Generation is textual specialization,
not an assumption that generated code is correct: every generated lemma body
is a separate verification obligation, including the native helpers.

| Derived adapter | Exact basis instances after expanding dependencies | Independently proved support |
|---|---|---|
| OR | 3 C + 1 A | Native `x|y = !(!x & !y)`; reference normalization and range |
| XOR | 4 C + 3 A, including the OR adapter | Native `x^y = (x|y) & !(x&y)`; exact reference expansion |
| ANDNOT | 1 C + 1 A | Existing bounded round trip for its left operand; reference expansion |
| Single-bit value | 1 R + 1 A | `andNat(N,z,1)=z%2` for N>0 |
| Rotate left | 1 L + 1 R + 1 modular + | Native rotation equals the sum of its two disjoint shifts; power product and no-overflow bound |
| Rotate right | 1 R + 1 L + 1 modular + | The analogous native decomposition and arithmetic bound |

All reference equations match section 2. All explicit `assume {:axiom}` lines
are identified local projections of these basic facts; no derived-operation
contract, native identity, reference range or arithmetic theorem is assumed.
They stand in for a future emitter and are the only new trusted hypotheses in
the derivations. The production emitter has not been implemented or tested.

For rotation, put `a=U(x)`, `p=P(k)`, `q=P(N-k)`, so `M=p*q`. Euclidean division
writes `a=q*h+r`, with `r<q` and `h<p`. Therefore `(a*p)%M=p*r`, and
`p*r+h<M`; the modular-addition bridge loses its final modulus. This is the
left reference formula. Swap p and q for right rotation. The checked
`legalCount` lemma handles `k=N` separately from `k<N`, so both endpoints agree
with the reference's integer `k%N` normalization. There is no wrapped native
addition of rotation counts in this derivation.

The native rotation helper uses a finite balanced case split on the legal
count, with a genuine native proof for each literal count 0..N. This is a
measured proof construction choice, not a new rotation axiom. Its size and
one-time verification cost grow with N; section 9 reports that cost separately
from subsequent scalar lemma calls. Helpers using assertion isolation retain
all verification conditions; a scope's aggregate RU can exceed its largest VC
substantially. Included helper bodies must also be verified as standalone inputs.

The bridge controls and derived-summary controls serve different purposes.
The former retain the basic local hypotheses themselves. The latter call the
checked adapter and a checked witness certificate before rejecting false,
thereby checking that the exported summary remains live. Neither turns a
solver `unknown` into a SAT certificate (section 9.3).

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
| Fixed integer reference definitions | Opaque private UFs plus explicit finite definition-unfold calls (section 6.4); no new global recurrence trigger. The pure Dafny model used to validate the equations has ordinary fuel/reveal translation. |
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
introduces additional matching/model-search problems. Use fixed opaque
reference symbols with explicitly requested local definition steps, validate
those equations against the pure Dafny model, and prove useful scalar facts
once. A fixed prelude is not permission to add an unbounded recurrence axiom.

## 5. Soundness argument and solver boundary

Fix a positive width N and M=2^N. Interpret a word as a Boolean vector indexed
by `[0,N)`, U as its little-endian weighted sum, and I(a) as the vector of the
Euclidean residue `a % M`. Interpret the integer references by section 2's
terminating recurrences. Interpret native operations by the
[SMT-LIB fixed-size bitvector semantics](https://smt-lib.org/theories-FixedSizeBitVectors.shtml).
This supplies one simultaneous interpretation, not an unrelated model per fact.

**A (AND).** Write each unsigned operand as its parity plus twice its integer quotient by two. Native AND's low output bit is the conjunction of the two parities, and its remaining bits are AND of the remaining operand bits. The specified `andNat` recurrence says exactly this; induction on N, with the empty-width result zero, gives the equality. The recurrence also shows that `andNat(N,a,b)` depends only on a and b modulo M, including negative integers under Euclidean division.

**C (complement).** Each input bit coefficient is either zero or one. Complement replaces it by one minus that coefficient, so the weighted sum becomes the sum of all weights minus U(x), namely M−1−U(x).

**Addition.** Native addition discards carries beyond bit N−1. The unsigned result is therefore the unique residue in [0,M) of U(x)+U(y), equal to Euclidean `(U(x)+U(y)) % M`.

**Subtraction.** Native subtraction is addition of the two's-complement negation and discards the final borrow. Its unique unsigned residue is `(U(x)−U(y)) % M`; Euclidean remainder is essential when the integer difference is negative.

**Multiplication.** Native multiplication keeps exactly the low N bits of the product. The unsigned result is `(U(x)*U(y)) % M`. The model uses mathematical integers, not a host multiplication that could overflow before the remainder.

**Division.** When U(y)>0, the unique quotient q in U(x)=q*U(y)+r with 0<=r<U(y) is nonnegative and less than M. Native unsigned division returns precisely q, the integer Euclidean quotient. Native division by zero has a separate total-theory meaning and is outside this argument.

**Remainder.** Under the same nonzero-divisor guard, native unsigned remainder is the unique r from the Euclidean quotient/remainder decomposition; r is also in [0,M). The zero-divisor native totalization must not be identified with an unguarded source integer remainder.

**L (left shift).** For 0<=k<=N, the count conversion is exact because N<2^N for every positive N (including N=1). Shifting moves each surviving weight from 2^i to 2^(i+k); weights at or above M are discarded. The result is `(U(x)*2^k) % M`. At k=N every weight is discarded, giving zero.

**R (logical right shift).** The exact nonnegative count drops the k least significant coefficients. Their removed value is U(x)%2^k, leaving integer quotient U(x)/2^k. At k=N, U(x)<2^N makes this quotient zero.

**Unsigned <=.** Both native operands are interpreted in [0,M). Native unsigned order is the usual order on those weighted sums. No signed comparison or two's-complement ordering is involved.

**Composed-cast form.** For every source operand satisfying 0<=a<M, native conversion I(a) has U(I(a))=a. Substitute these equalities into the relevant native schema, preserving the divisor and source-count guards and exact static widths. This proves the specified composed formula. It justifies a local shortcut at those original operands; it does not turn a source cast into a wrapping cast or authorize discovering a source argument by textual matching.

### What the model claim means

With **complete mathematical reference definitions**, each bridge is a theorem:
adding it leaves the mathematical model set unchanged. Do not make that claim
about every model of the actual opaque or fuel-limited backend encoding.
Hidden reference applications can be underconstrained there, and a bridge can
exclude some of those spurious interpretations. That is useful strengthening,
not evidence of inconsistency.

The required relative argument is this: take an intended model of the existing
source/native semantics and enabled shared-option families. Extend it with the
exact total integer references, and interpret every fuel layer by that same
mathematical value. Each permitted definition instance, bridge instance and
proved library theorem then holds. Existing integers, words, heaps, allocations,
function permissions and old symbols retain their interpretation. Fresh reference
symbols admit this extension of the old, reference-free signature. The argument
is not a proof of the consistency of the entire Dafny prelude or the correctness
of Z3's implementation.

At the new pin, the shared-family inventory is: bounded round trips, closed
integer literal identities, reflexive heap-succession instances, result-allocation
application instances, override call-layer instances, assign-such-that call
permissions, and **#97's guarded source-lambda precondition equivalence**.
The latter has a compatible source-closure interpretation: a handle contains its
body, precondition and reads maps; under its type, allocation, capture, family
and heap guards, `RequiresN` agrees with the declared range. The bridge changes
none of those maps or guards. Test their actual interaction, including the
separate `--heap-free-lambda-facts` option, as well as retaining #97's own
negative/capture/old-heap fixtures. A bit-only test with the shared switch on
cannot demonstrate an interaction with an uninstantiated lambda fact.

### Executable checks and guard counterexamples

Run all standard-library-only Python models from the repository root:

```sh
python3 docs/dev/design/193-evidence/models/run_all.py
```

[Model arguments and guard details](193-evidence/models/model-arguments.md),
[executable definitions](193-evidence/models/model.py), and
[retained results](193-evidence/models/results.json) are part of this revision.
The Boolean-digit side independently uses ripple-carry addition/subtraction,
shift-and-add multiplication, restoring long division, index shifts and weighted
sums. The native side uses explicitly masked Python operators. Reference AND
uses quotient/remainder, and the derived references use the exact compositions
in section 2; the comparison is not two aliases of one implementation.

The run exhausts **87,380 binary pairs** at widths 1–8 and **4,606 word/count
combinations** with counts 0..N+1. It also checks 100,000 uniformly sampled pairs
at **each** of widths 16, 32 and 64, with a sampled legal count plus 0, N and N+1
for each word. Binary schemata have no count argument; shift schemata have no
second word operand, so irrelevant variables are factored out. With boundary
cases, the totals are 387,455 pairs and 1,184,145 word/count cases, 4,778,877 active
raw-schema and 4,785,849 active composed-schema equation checks, and 37,931,258
additional independent/derived equalities. False-guard implications are counted
separately. These finite checks detect mistakes; the mathematical arguments
above justify arbitrary widths.

The request's phrase “each guard is necessary” needs precision. Source legality
can be stricter than the weakest mathematical premise of a particular formula.
The executable counterexamples deliberately distinguish those cases:

| Guard or boundary | Concrete example | What it establishes |
|---|---|---|
| Nonzero divisor | Native `bv1(0)/0=1` and `bv2(0)/0=3` | Dropping the quotient guard at both widths would equate the same integer `div(0,0)` to 1 and 3. Source integer division is undefined there. |
| Nonzero remainder divisor | Python `1 % 0` raises `ZeroDivisionError`; native `bv3(1)%0=1` | A definedness requirement. Do not invent a numeric integer remainder at zero; some backend totalizations could match the native one. |
| Count bound | N=3, x=1, k=8: I(k)=0, native L/R return 1, arithmetic L/R return 0 | Some bound preventing wrapped counts is essential. The equations actually hold for all 0<=k<2^N; k<=N is the retained source rule, not their minimal mathematical bound. |
| Count lower bound | P(-1) is outside P's natural domain | The total references branch before P(k); the scalar L/R formulas require nonnegative k. |
| Cast upper bound | N=3,a=8: native C is 7, `M-1-a` is -1; native `8<=1` after casts is true, integer order is false | Original-value complement/order cannot ignore wrapping. |
| Cast lower bound | N=3,a=-1: native C is 0, `M-1-a` is 8 | Negative source values cannot be substituted for their unsigned observations. |
| Width zero | `k % 0` raises `ZeroDivisionError`; no SMT `BitVec 0` sort exists | Use the separate singleton before count normalization/native-sort creation. A correctly defined singleton satisfies the extended scalar formulas; it is not a counterexample to those formulas. |

All source fit checks remain mandatory. Mathematically, composed A, modular
+/−/× and L's value operand are periodic and do not individually need cast-range
premises; C written without normalization, R, division/remainder and unsigned
order generally do. A range-check optimization must preserve the source rule
regardless of that distinction. Never infer an arbitrary newtype predicate from
a masked value: every result still has to satisfy the original predicate.

### 5.1 Issue 147: exact repair and remaining native reachability

The fix is commit
[`05a067c860ced91e2e0086ddf180c1ccdf544cff`](https://github.com/erniecohen/dafny/commit/05a067c860ced91e2e0086ddf180c1ccdf544cff)
(PR 218), already present at the reviewed pin. In
[`AddBitvectorRotateFunction`](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L1153),
positive non-power-of-two widths use explicit shift/OR definitions; positive
power-of-two widths retain Z3 `ext_rotate_left/right`. Width zero follows the
existing singleton path and never constructs an SMT `BitVec 0`.

The pinned regressions are
[`github-issue-147.dfy`](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/github-issue-147.dfy),
which runs original, true, false, inhabited-control and count-bound fixtures on
both solvers/resolvers, and
[`github-issue-147-emission.dfy`](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/github-issue-147-emission.dfy),
which checks the actual Boogie/SMT paths at widths 0, 1, 3, 6, 12, 32 and 65.
The exact false controls are in
[`Inputs/github-issue-147-controls.dfy`](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-147-controls.dfy).

**Can the proposed adapters reach the affected rewrite on Z3 5.1.0? Yes.**
The BV-facing adapters at 8/16/32/64 use the retained native extended rotations.
A nested caller can therefore reach the same native composition rewrite. Its
known defect, combining counts modulo `2^N` before modulo N, cannot change the
answer at these widths: N divides `2^N`, so reducing modulo `2^N` preserves the
residue modulo N. Positive non-power-of-two adapters go through the repaired
shift/OR wrappers and do not emit that native rotation operator. Integer-only
reference rotations never call it. Do not claim that the native rewrite is
unreachable; distinguish reachability from the removed unsound case.

The repair is not a promise of cheap composition proofs. The pinned driver
explicitly records `Bv64SameDirection` as out of resource on Z3 4.12.1 at
2,000,000 RU. That inherited observation is not a passing vacuity control and
is not silently replaced by the cheaper scalar-summary controls in this design.
Preserve #147's value, negative, bound and emitted-operator regressions in every
rotation milestone.

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

**Recommendation: fixed compiler-owned reference symbols, with explicit local
definition unfolding, and an ordinary proved Dafny companion.** Replace the
privileged embedded source-module loader. This removes the need to authenticate
a user-spelled reference function as the meaning of a language operator. It
moves the defining equations into the compiler's trusted semantic surface; it
does not make those equations untrusted or automatically reduce every part of
the trusted computing base.

Two facts at the pin constrain the implementation. The
[BoogieGenerator constructor](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L109)
marks every loaded prelude function `AlwaysRevealed`. Meanwhile
[OpaqueMemberRewriter](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Rewriters/OpaqueMemberRewriter.cs#L79)
skips functions whose body is null. Merely placing recursive bodies in the
prelude and calling a bodyless facade “opaque” would not implement this plan.

**Concrete encoding.** Keep one canonical definition description for section 2's integer-valued
operator references (powers, normalization, Boolean operations, shifts, rotations
and population count). The sequence view `bitsNat`, its inverse `value`, and
indexed view `bit` remain ordinary Dafny companion definitions with checked
bodies; there is no new trusted sequence theory. Generate a conditionally loaded
prelude fragment of
private uninterpreted function declarations, such as `IB#And(n,a,b):int`.
Do not install recursive BPL bodies or globally quantified, unfuelled recurrence
axioms. Construct public ghost signatures from installed compiler data, with
private intrinsic IDs and completely resolved types/contracts. `_System`
members are not a place to insert arbitrary unresolved recursive source.
User names, attributes, source paths, export clones and `.doo` metadata cannot
create that intrinsic identity.

Add a narrowly specified **definition-unfold intrinsic**, illustrated by these
compiler-produced signatures (not trusted user declarations):

```text
andNat(n:nat,a:int,b:int):int
unfoldAnd(n:nat,a:int,b:int)
  ensures andNat(n,a,b) ==
    (if n==0 then 0 else
       (if a%2==1 && b%2==1 then 1 else 0)
         + 2*andNat(n-1,a/2,b/2))
```

A resolved `unfoldAnd(n,a,b)` call emits exactly this one local instance after
ordinary argument well-formedness checks. It does not recursively unfold newly
introduced applications or grant permissions to user functions in its arguments. The recursive application is guarded
by the nonzero branch. Corresponding unfold operations expose the other exact
definitions, including the zero-width and negative-count branches before any
partial source expression. This is a **new trusted definitional rule**, justified
by section 5's model, not an existing `reveal` facility and not a new BV bridge.
No globally quantified recurrence is needed. Ordinary library lemmas call these
operations explicitly and prove induction steps; no library lemma is accepted
merely because it has a desired contract or a verification-skipping attribute.

The raw compiler-owned integer-reference signatures return integers without using the range theorem
being proved. Establish power positivity before unfolding definitions that
display division or remainder by a power; preserve their source-definedness
obligations rather than silently assuming the candidate positivity contract. Prove positivity, result range and useful algebra in the companion
from the unfold equations. If consumer signatures later expose proved range
contracts to make full-interval result checks automatic, their proof target
must omit those same contracts. Keep this bootstrap dependency explicit; a
function's assumed range contract cannot serve as its own proof. Other algebra
remains ordinary scalar lemma calls. This finite definition mechanism needs its
own emission tests, witness controls and cost measurements in M2.

| Concern | Embedded ordinary Dafny reference source | Fixed prelude plus local unfold rule |
|---|---|---|
| Semantic trust | Normal function translation plus independently checked source bodies/contracts | Reviewed fixed defining equations, intrinsic-call translation, and independently checked companion theorems |
| Canonical identity | Privileged source loader, declaration provenance map, injection order and bundle integrity | Private compiler IDs and reserved backend symbols; no source body supplies operator meaning |
| Proof interface | Existing fuel/reveal machinery | Explicit one-step unfold calls; opaque UFs otherwise |
| Separate compilation | Injected module dependency and canonical bundle reconstruction | Compiler-owned signatures reconstructed by the installed version; ordinary proof dependencies |
| `.doo` | Must not accept source/header metadata as authority for canonical functions | Same rule; no library can manufacture an intrinsic identity |
| Opaque newtype export | Requires a separate visibility/capability check | Still requires exactly that check |
| Both options OFF | Must avoid loading or registering injected support | Must avoid constructing signatures, loading the fragment or changing declaration order |

The export P0 is **independent** of reference ownership: copying `bitWidth`
metadata onto an opaque exported type can disclose or recreate representation
capability even with perfectly fixed prelude functions. Eligibility must still
require the revealed integer-newtype definition and a descriptor validated in
that declaration's recorded language mode. Retain the section 6.1/6.3 export,
refinement, cloning and language-server tests. A fixed prelude solves the
reference-body substitution problem, not this frontend boundary.

**Options and identity.** The public ghost-reference API and proof companion
require `--integer-bitwise`; native bridge use under `--additional-axioms` alone
can use private reference symbols without adding a public semantic dependency.
Register IntegerBitwise as a Global option with
`OptionLibraryImpliesLocalError`. An enabled library requires an enabled client;
an old/off-built library loaded into an enabled client keeps its formerly inert
attributes inert. Recognize capability using module/declaration options, not
only the root invocation.

At the pin, [AdditionalAxioms is a CLI option](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Options/CommonOptionBag.cs#L725),
while [DooFile](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/DooFile.cs)
serializes/checks Global and Module options and already requires exact
`DafnyVersion`. Preserve this distinction. DOO stores pre-resolution source;
recompute width descriptors and canonical bindings on load. Because these fixed
definitions are product code, the existing exact version check suffices as the
initial reference ABI. Add a separate ABI/hash only if proof packages or model
semantics can vary within a product version. Neither a version string nor
“already verified” status is authority to create compiler identities.

Load the compiler-owned fragment independently of any custom base `--prelude`,
rejecting duplicate private declarations rather than adopting user-provided
ones. A custom prelude remains an existing explicit trust boundary and is not
covered by the default-prelude model/control claim. With both options OFF,
retain the old parsing, resolution and Boogie-output path byte for byte on old
inputs; do not unconditionally append even unused declarations to the prelude.

**Proof and runtime separation.** M1 checks the pure ordinary Dafny model with
both options OFF. After the fixed mechanism exists, check the canonical companion
with integer-bitwise ON and additional axioms OFF; check native adapters
separately with the bridge option ON. Keep candidate derived/range contracts unavailable
while proving them; the exact defining unfold equations remain available. Ghost references never become runtime calls: executable
operators/rotate/popcount retain dedicated resolved identities and semantic N,
and backends implement their mathematical meanings directly. Ghost views may
be ordinary source calls.

If the local unfold mechanism is deferred, the coherent fallback is real opaque
Dafny bodies with the complete existing fuel/reveal/can-call translation. Do not
alias a bodyless source name to an unfuelled private function and claim ordinary
Dafny unfolding still works.


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
| `AST/SystemModuleManager.cs`, compilation/option setup and `Verifier/BoogieGenerator.cs` | Construct resolved compiler-owned ghost signatures only in language-enabled mode; load the private reference fragment when needed by either option; preserve the both-off path exactly. |
| Canonical reference-definition description and ordinary proof companion | Generate fixed UF signatures/local unfold equations and a pure model from the reviewed definitions; verify typing/range without assuming the target contracts; prove bool/digit correspondence and scalar adapters. |
| `AST/TypeDeclarations/NewtypeDecl.cs`, `AST/Cloner.cs`, `AST/Modules/ScopeCloner.cs` | Width descriptor, unresolved/resolved clone policy, provenance preservation, and opaque-export behavior. `ScopeCloner` copies attributes while hiding a newtype; do not recover capability from that attribute alone. |
| `Resolver/NativeTypeAnalysis.cs`, `Resolver/ModuleResolver.cs` | Extract backend-independent interval validation; establish metadata before operator inference; keep native selection separate and later. |
| Legacy and refreshed resolver files listed in section 6.3 | New gated bit-capability constraints and same-type rules, diagnostics, result type and count handling. Do not globally broaden old constraints. |
| `AST/Expressions/Operators/UnaryOpExpr.cs`, `BinaryExpr.cs`; `Resolver/ConstantFolder.cs` | Distinct resolved integer operations and constant folding with exact N-bit masks, leaving ordinary arithmetic folding unchanged. |
| `AST/SystemModuleManager.cs` and `Resolver/ModuleResolver.cs` BV member registration | Both the legacy generic `_bv` member path and refreshed width-specific path need any new ghost `.Bits()` member. Newtype rotate/popcount members use descriptor identity and collision checks. |
| `Verifier/Expressions/TranslateBinaryExpr.cs`; `Verifier/BoogieGenerator.ExpressionTranslator.cs` | Direct integer reference calls for new operations; canonical resolved identities; local BV bridge collection at integer observations, preserving literal/boxing behavior. |
| `Verifier/BoogieGenerator.ExpressionWellformed.cs`; `Verifier/Statements/BoogieGenerator.TrForallStmt.cs` and other audited can-call callers | Original predicate/count checks; explicit scalar emission context; no unbound or exported facts; preserve can-call options. |
| `Verifier/BoogieGenerator.cs` | Keep existing native BV declarations, conversion axioms and rotation repair. Add only the required canonical-symbol plumbing/local formula builders; no unconditional width-family changes. |
| `DafnyPrelude.bpl` or a separately loaded fixed fragment | Conditionally add private UF signatures; no recursive BPL bodies or global unfuelled recurrence. Audit explicit local unfold translation and custom-prelude collisions. |
| `Backends/SinglePassCodeGenerator/SinglePassCodeGenerator.Expression.cs` | Pass N past runtime-type simplification; emit direct integer operations, truncation, counts and single evaluation. Do not apply modular truncation to ordinary integer arithmetic. |
| `Backends/CSharp/CsharpCodeGenerator.cs`; `Backends/Java/JavaCodeGenerator.cs`; `Backends/GoLang/GoCodeGenerator.cs`; `Backends/Python/PythonCodeGenerator.cs`; `Backends/JavaScript/JavaScriptCodeGenerator.cs`; `Backends/Cplusplus/CppCodeGenerator.cs` | Each operation/width/carrier path in section 7, generated-code tests and runtime reference tests. |
| `Backends/Dafny/DafnyCodeGenerator.cs`, `Backends/Dafny/AST.dfy`, `Backends/Rust/Dafny-compiler-rust-definitions.dfy`, `Backends/Rust/Dafny-compiler-rust.dfy` | Width-bearing DAST operations, Rust native rotations/popcount, integer result adaptation and explicit unsupported cases. Regenerate checked-in generated artifacts through the normal pipeline. |
| `Source/DafnyRuntime/` C#, Java, Go/gomod, JS and other affected runtimes | Correct masks/count helpers and any popcount fallback; JavaScript wide integer repair or new correct helpers. |
| `DooFile.cs`; `Backends/Library/LibraryBackend.cs` | Persist source annotation and language mode, rebuild descriptors/canonical bindings, retain exact-version compatibility; never accept a forged intrinsic identity. |
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

Reported RU in this document comes from public repository CI. Keep the pins
separate: historical calibration uses `62bae981` / version `.2d6fd450`;
round-2 controls and checked derivations use the requested `428e348b` / version
`.8e189a3e`. The old [complete measurements](193-evidence/public-measurements.md)
remain available. Round-2 sources, outcomes, costs, hashes and commands are in
[the new report](193-evidence/round2/public-results.md). These are design probes,
not a production emitter or integer-bitwise implementation.

### 9.1 Historical calibration: broader contracts, not the ten-fact basis

[Run 42754841657](https://github.com/erniecohen/dafny/actions/runs/42754841657)
uses benchmark commit `79d20aa13e8584e1f4113d79a8d20c699e90e6ab`, both solvers
and resolvers, a 200,000-RU per-batch cap, and the old compiler pin. It covers
17 operations at widths 8, 16, 32 and 64. Its complete per-width tables remain
in the historical report; repeating them here would obscure the distinction
between trusted derived contracts and checked derivations.

| Mode | Correctness results, Z3 5.1.0 / 4.12.1 | Meaning |
|---|---|---|
| Reconstructed `assume false` baseline | 68/68 / 68/68 | Vacuous; not a sound cost baseline |
| Empty-body native attempt | 0/68 / 0/68 | This proof attempt fails, not a falsification of the equations |
| Trusted separate BV-argument contract | 15/68 / 5/68 | Conversion proof search still dominates many clients |
| Trusted composed integer-argument contract | 68/68 / 68/68 | Broad direct contracts, including derived operations |
| Actual native Boolean reduction identities | 12/12 / 12/12 | Genuine native proofs |
| Integer reference wrappers | 68/68 / 68/68 | Definitional interface checks, not all useful algebraic laws |

The broad composed-contract clients cost 3,744–15,735 RU on 5.1.0; the total
across 68 correctness scopes is 551,032/551,516 RU on the two solvers. Every
well-formedness scope passed, and both resolvers produced identical outcomes
and RU. There were no missing logs or wall-time failures. These observations
motivated composing the source casts locally. They do **not** measure deriving
OR/XOR/bit/rotation from the ten basic facts, and do not justify treating those
broader contracts as additional axioms. Section 9.6 supplies the checked
minimal-basis costs, including support construction and well-formedness.

### 9.2 A substantive sequence theorem, proved without the new facts

The historical, independently checked
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

### 9.3 Effective vacuity controls and their exact scope

The old 68-case rows rejected 52/50 false controls in separate form and 44/42 in
composed form on Z3 5.1.0/4.12.1. The remainder exhausted resources. In particular,
all AND/OR/XOR/ANDNOT controls were inconclusive, already at width 8. Those are
failed controls, not passes and not evidence of inconsistency.

**Diagnosis by ablation and SMT trace.** Remove every BV operation and leave
only a call of transparent recursive `andNat` followed by `assert false`: the
failure still occurs at widths 8 and 64. Making the reference opaque changes
that resource failure to Dafny `Errors`/Boogie `Invalid`. Width-64 bit-blasting
therefore is not required to reproduce the failure. The captured queries set `smt.mbqi=false`:
MBQI is not the explanation either. Quantifier profiles show deeper and more
numerous instantiations in the transparent recursive/fuel/literal/arithmetic
encoding. Symbolic opaque width-64 mixed BV/integer contexts can still exhaust,
so mixed-theory search is a second cost. The trace supports this localization;
it does not prove that a single trigger is solely responsible.

**A necessary qualification:** a Dafny `Errors`/Boogie `Invalid` result is not
necessarily a solver `sat` result. The captured opaque A64 query returns
`unknown` with reason `incomplete quantifiers`, after which Boogie reports an
ordinary assertion rejection using a candidate model. It terminates without
resource exhaustion, but that candidate is not a checked model of the whole
prelude. Report these three outcomes separately: operational rejection of
`assert false`; actual SAT for the finite new-fact slice; and the mathematical
model-extension argument. None may be relabelled as either of the others.

**Method selected for production regression design:**

1. Keep reference functions opaque in the control context. Independently verify
   their exact witness values with the bridge disabled. For example,
   `and53(n)` proves `andNat(n,5,3)==1` for every n>=3 by induction through the
   original recurrence, and `powers()` proves P(0), P(1) and P(64).
2. In an inhabited witness context, retain every basic and composed local
   bridge equation the emitter would supply for the observed terms. Preserve
   source guards, native operations, conversions, existing shared-option machinery
   and full result/type checks; explicitly activate #97 in the combined fixture. Do not replace the actual bridge result by
   a numeral or assume the reference certificate.
3. Assert false. Require `Errors` with an `Invalid` VC at that assertion, with no
   failed precondition, certificate, positive check, timeout or exhausted VC.
   A diagnostic exit code or green workflow alone is insufficient.
4. Include a source lambda and a requires observation for #97's necessary
   direction. The combined control first proves that direction for symbolic t,
   then takes the inhabited t=0 branch and retains all ten raw/composed bridge
   families. A split immediately at the final false assertion separates the
   preceding positive checks without removing bridge premises.
5. Retain a sensitivity mutation: changing both A right-hand sides by +1 must
   make false provable at the certified witness. This checks that the reference
   value and bridge equation can actually conflict in the test.

The [standalone control file](193-evidence/round2/controls64.dfy) includes all ten
schemata; [the combined file](193-evidence/round2/lambda-controls64.dfy) exercises
#97 and the separate heap-free option; [the pure certificates](193-evidence/round2/pure-certificates64.dfy)
verify with additional axioms OFF and contain no `assume` or axiom-marked lemma.
In these design-only projections, each `assume {:axiom}` below is exactly an
identified prospective local emitter equation. It is not a proof of that equation.
Production tests replace these harness assumptions with the actual emitter and
retain structural checks that every expected guarded equation is present.

The [first round-2 public run](https://github.com/erniecohen/dafny/actions/runs/43100427111)
uses the exact requested compiler and both solvers/resolvers. Every listed
control rejects false with no other failure; each number below is the complete
control correctness-scope RU, including both batches in the combined case.
Both resolvers give identical numbers. Heap-free ON/OFF is the separate existing
lambda option; additional axioms are ON throughout this table.

| Control | Z3 5.1.0 | Z3 4.12.1 |
|---|---:|---:|
| A | 13,347 | 12,522 |
| C | 4,673 | 4,659 |
| L, including count N | 17,300 | 17,305 |
| R, including count N | 15,190 | 15,225 |
| + | 8,151 | 8,670 |
| − | 8,129 | 8,726 |
| × | 8,834 | 9,243 |
| / | 8,410 | 8,497 |
| % | 8,323 | 8,424 |
| ≤ | 6,469 | 6,569 |
| All ten plus #97, heap-free OFF | 136,296 | 175,461 |
| All ten plus #97, heap-free ON | 124,948 | 163,279 |

Here is the exact A/C/L/R text at width 64. The functions and checked certificates
are those in the linked standalone file; L/R exercise both count 1 and count N:

```dafny
lemma {:induction false} control_A64()
{
  and53(64);
  // A, composed at checked integer literals (its range guard is true).
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 3 < 18446744073709551616
    ==> (((5 as bv64) & (3 as bv64)) as int) == andNat(64,5,3);
  // The raw A instance is retained as well.
  assume {:axiom} (((5 as bv64) & (3 as bv64)) as int) ==
    andNat(64,(5 as bv64) as int,(3 as bv64) as int);
  assert false;
}

lemma {:induction false} control_C64()
{
  assume {:axiom} 0 <= 5 < 18446744073709551616
    ==> ((!(5 as bv64)) as int) == 18446744073709551615-5;
  assume {:axiom} ((!(5 as bv64)) as int) ==
    18446744073709551615-((5 as bv64) as int);
  assert false;
}

lemma {:induction false} control_L64()
{
  powers();
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 1 <= 64
    ==> (((5 as bv64)<<1) as int) == (5*p(1))%18446744073709551616;
  assume {:axiom} 0 <= 1 <= 64
    ==> (((5 as bv64)<<1) as int) ==
    (((5 as bv64) as int)*p(1))%18446744073709551616;
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 64 <= 64
    ==> (((5 as bv64)<<64) as int) == (5*p(64))%18446744073709551616;
  assume {:axiom} 0 <= 64 <= 64
    ==> (((5 as bv64)<<64) as int) ==
    (((5 as bv64) as int)*p(64))%18446744073709551616;
  assert false;
}

lemma {:induction false} control_R64()
{
  powers();
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 1 <= 64
    ==> (((5 as bv64)>>1) as int) == 5/p(1);
  assume {:axiom} 0 <= 1 <= 64
    ==> (((5 as bv64)>>1) as int) == ((5 as bv64) as int)/p(1);
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 64 <= 64
    ==> (((5 as bv64)>>64) as int) == 5/p(64);
  assume {:axiom} 0 <= 64 <= 64
    ==> (((5 as bv64)>>64) as int) == ((5 as bv64) as int)/p(64);
  assert false;
}
```

**What a successful control establishes.** The Dafny fixture shows that the
verifier rejects `assert false` in the exposed, inhabited context, with the
bridge equations live and reference witness values certified independently.
It rules out an immediately exploitable contradiction found by that run. It
does **not**, by itself, prove a countermodel: the underlying result can be
`unknown (incomplete quantifiers)`. The separate quantifier-free consistency
witness keeps every raw/composed local equation and guard at the same ground
inputs, uses the actual native operators, and supplies the reference values
from the mathematical model. A real `sat` answer for that slice establishes
joint satisfiability of those new facts; the A+1 mutation must be `unsat`.

The [ground-witness generator](193-evidence/round2/ground_witness.py) reads the
actual `assume {:axiom}` clauses from both control sources and checks that the
combined fixture retains the exact ordered union: **24 raw/composed instances**,
including both counts in L and R. It preserves guards and emits native SMT BV
operators/conversions. It does not compute away their results in Python. The
only supplied reference values are the independently checked `and53` and power
certificates. Their source hashes must match the verified Dafny artifacts;
the small source-expression parser is not a proof checker. The corruption test
changes both A forms by +1; its unsatisfiability must involve the correct
reference certificate, not merely disagreement between the two bridge forms.

An explicit extension of those ground reference observations is
`P(n)=2^n` and `andNat(n,a,b)=sum(2^i*d_i(a)*d_i(b), i=0..n-1)`, where
`d_i(a)=(a / 2^i) % 2` uses Euclidean division. Splitting off digit zero proves
the original recurrence even for negative integer a,b; the empty sum is zero.
At the witness, AND is 1, complement is M−6, addition is 8, subtraction 5−9 is
M−4, product is 15, quotient/remainder are 1/2, shifts at count 1 are 10/2,
and both shifts at count 64 are zero. All guards are true. Thus satisfiability
cannot be obtained by making a guard false, and the finite reference values
extend to the intended total functions rather than arbitrary UF values.

The [query-profile script](193-evidence/round2/profile_queries.py) captures the
actual Dafny query and replays its intact first-check prefix, adding only
observational quantifier profiling and requests for reason/statistics. It also
attributes shared source-location quantifiers by their existing skolemid and
checks that this qid relabelling preserves the result and summary statistics.
It never removes a prelude axiom, bridge premise, trigger or fuel layer.

Neither test proves all schema instances, arbitrary transparent recursive
contexts, the future emitter, or the complete existing Dafny prelude consistent.
Opacity suppresses recursive-definition search. Section 5's model extension
and separately checked reference proofs justify those hidden definitions; the
finite tests supplement that argument. #97 is exercised operationally in the
full Dafny fixture and included in the general model argument, not silently
replaced by a quantifier-free lambda abstraction and called fully checked.
A production change still must replace every harness assumption by an actual
emitted fact and rerun these controls; no production fact is approved here.

The round-2 public report records exact pin, source hashes, per-VC outcomes and
RU, both resolvers and both solvers. Keep 200,000 RU as the acceptance ceiling
for each of these concrete controls. Resource exhaustion remains a failure of
the gate, even when the mathematical model is known. The old broad-contract
projection's inconclusive controls are superseded by these precise finite
contexts; they are not retroactively marked passing.

### 9.4 Suite and standard-library evidence

The historical pin `62bae981952337d312cbb3d43c39f9813cca6084` has a successful
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

These totals measure `62bae981`, not the round-2 comparison pin `428e348b`.
In particular, the intervening issue 97 change records
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

The [first round-2 attempt](https://github.com/erniecohen/dafny/actions/runs/43099852372)
failed during solver setup: a variable written for the next Actions step was
used in the current step. It produced no proof evidence. The
[corrected controls/models run](https://github.com/erniecohen/dafny/actions/runs/43100427111)
completed all four solver/resolver configurations with zero unexpected
acceptance outcomes. The [complete round-2 run](https://github.com/erniecohen/dafny/actions/runs/43103815404)
also checks every derived helper, caller and control, the finite SAT witnesses,
and actual-query profiles. Its immutable input commit is
`d81f1379d5ae346cdcaad051519feb50c482dffb`.

The workflow consumes the public compiler artifact built from `428e348b`, checks
its version and unchanged product sources, and downloads checksum-pinned solver
releases. The report retains compiler/solver hashes, generated-source hashes,
per-scope and per-VC distinctions, and actual outcomes. Generated Dafny and full
SMT inputs/logs are in the public CI artifacts. No private-machine RU is used in
this document. Historical infrastructure failures and superseded probe shapes
remain described in the original report, with their original pins.

From the repository root, using the requested compiler and either solver:

```sh
python3 docs/dev/design/193-evidence/models/run_all.py
python3 docs/dev/design/193-evidence/round2/generate.py cases
python3 docs/dev/design/193-evidence/round2/run.py \
  --dafny "$DAFNY" --z3 "$Z3" --sources cases --output results \
  --source-commit 428e348b0b125cb67505f03a0026b3f497f98e48 \
  --resource-limit 200000 --wall-timeout 600
python3 docs/dev/design/193-evidence/round2/ground_witness.py \
  --controls cases/controls64.dfy --lambda-controls cases/lambda-controls64.dfy \
  --z3 "$Z3" --out ground
python3 docs/dev/design/193-evidence/round2/profile_queries.py \
  --dafny "$DAFNY" --z3 "$Z3" --controls cases/controls64.dfy --out profiles
```

For a DLL, add `--dotnet "$DOTNET"` to the Dafny runner and profile commands.
Repeat the runner with `--refresh`, and with each solver. The runner itself
checks both settings of the separate heap-free option on lambda fixtures. Pure
support files are explicit inputs verified with additional axioms OFF; including
them in a caller is not sufficient. Clients and controls keep 200,000 RU;
visibly annotated library helpers have their separate larger budgets.

The diagnostic runner exits zero after recording failures. Read its unexpected
scope, setup/log and unmapped-failure counts, not merely workflow status.
Ablation files preserve resource failures as observations and never count as
verified adapters. The ground checker requires real SAT/UNSAT answers. The
profile result records actual `unknown` reasons independently of Dafny's verdict.

### 9.6 Minimal-basis cost and accelerator policy

The exact conditional derivations are in section 3.4. Their source-level
assumptions are precisely the proposed basic instances, so these measurements
replace the old broader-contract projection for this question. They remain
predictions for a future production emitter and the selected fixed-UF/unfold
mechanism: the current fixtures use ordinary Dafny reference definitions.
Changes in surrounding translation can change solver cost even when the
mathematical fact is identical.

The report separates three costs: proving reference/native support once,
constructing the checked adapter once, and a subsequent caller including its
well-formedness scopes. Assertion isolation preserves every obligation; report
both summed RU and the largest VC. The integer-input bit/rotation callers use
that annotation explicitly, and unsplit counterparts remain as diagnostics.

The [full public run](https://github.com/erniecohen/dafny/actions/runs/43103815404) verifies every selected helper body and caller in
all four solver/resolver configurations. The following measured values include
**well-formedness plus correctness**. Each cell is **Z3 5.1.0 / 4.12.1**; both
resolvers give identical client counts. All inputs are integers in range; bit
and rotation clients use explicit assertion isolation.

| Integer-input client | 8 | 16 | 32 | 64 |
|---|---:|---:|---:|---:|
| OR | 19,705 / 19,693 | 20,713 / 20,701 | 22,729 / 22,717 | 26,761 / 26,749 |
| XOR | 21,671 / 21,659 | 22,677 / 22,665 | 24,695 / 24,683 | 28,733 / 28,721 |
| ANDNOT | 19,367 / 19,355 | 20,373 / 20,361 | 22,391 / 22,379 | 26,429 / 26,417 |
| Bit value | 39,593 / 39,472 | 43,618 / 43,305 | 50,587 / 49,890 | 66,537 / 65,216 |
| Rotate left | 27,770 / 27,786 | 29,958 / 29,990 | 34,342 / 34,406 | 43,110 / 43,238 |
| Rotate right | 27,770 / 27,786 | 29,958 / 29,990 | 34,342 / 34,406 | 43,110 / 43,238 |

The twelve corresponding unsplit bit/rotation callers exhaust in both solvers
at all four widths; they remain visible ablations. Ordinary native-word bit
callers cost 14,996–37,720 RU and native-word rotation callers 13,509–17,321 RU.
Thus a cheap checked contract does not imply that every unshaped mixed-theory
call is cheap.

Construction is separate: at width 64, OR/XOR/ANDNOT adapter totals are
779,276/775,993, 716,094/716,153 and 575,306/575,738 RU, respectively. Each native
variable-count rotation helper costs 16,503,535/16,733,258 RU across isolated
VCs, plus its checked literal-count dependencies. The 64-bit rotate-left adapter
itself costs 1,323,785/2,759,806 RU; rotate-right costs 1,307,541/1,951,203.
Every obligation verifies within its explicit helper cap. The report gives all
widths, support totals, largest VCs, and the sole pure-certificate cost variation
between resolver runs. These are real once-only costs, not hidden axioms or
costs silently charged to every caller.

**Proposed accelerator threshold.** First supply the checked scalar adapter.
Consider a redundant direct fact if a representative client still exhausts or
exceeds 200,000 RU after those calls, and a paired candidate at least halves
its cost and brings it below 100,000 RU on both solvers. Alternatively, require
at least 5% reduction in total verification RU on representative complete client
projects. In either case require the model/derivation, the operational and finite-slice
controls of section 9.3, actual-emitter correspondence, both-off identity, and full suite/Std comparisons;
report regressions as well as gains. These are engineering decision thresholds,
not a theorem or an already adopted fork policy. A large once-only helper proof
by itself is not grounds to enlarge the trusted basis.

## 10. Alternatives and decisions

| Decision | Alternative and benefit | Cost or objection | Recommendation |
|---|---|---|---|
| Bit view representation | New `bit` type makes numeric digit notation natural | Adds type/boxing/conversion machinery with little benefit over Boolean values | `seq<bool>` plus a proved bool/digit adapter |
| Newtype verification | Lower every operation through `bvN` | Reuses existing machinery but imports mixed-theory conversion costs into every integer program | Direct integer reference semantics |
| Trusted theory | Add a complete sequence/bit axiom system | Concise specification; large consistency and trigger surface | Fixed integer definitions with explicit unfold rules, ordinary sequence definitions and proved laws; native interoperability facts use `--additional-axioms` |
| Native bridge basis | One direct axiom per required operation | Simple clients and promising projected costs | Begin with the ten basic facts and prove derived operations; add a redundant accelerator only with evidence |
| Proof strategy | Ask the solver to bit-blast every client | No added bridge fact; poor observed mixed-theory scaling | Small pure-BV identity proofs once, then integer/library reasoning for clients |
| Trigger strategy | Global operation or selection quantifiers | Automatic coverage of deeply nested expressions | Finite local scalar instances; selection-trigger loops are a concrete counterexample |
| Width discovery | Use selected native carrier width | Little frontend work | Explicit semantic width, because carrier selection is backend dependent and can include extra constraints |
| Width syntax | Infer from any provable bounded predicate | Fewer annotations | Explicit attribute first; exact-interval inference later, never target-dependent inference |
| Operator API | Only library functions | Lowest frontend complexity and a useful first proof milestone | Library reference/proof foundation plus requested builtin operator syntax in staged PRs |
| Signed/unbounded integers | Infinite two's-complement semantics immediately | Natural for some bitwise laws and some host runtimes | Defer: width-dependent complement, shift interpretation and result domains need separate user decisions |
| Canonical support | Embedded trusted ordinary Dafny source | Reuses existing fuel/reveal but requires privileged source provenance and injection | Fixed compiler-owned UFs plus explicit local unfold rules and an ordinary proved companion |
| Bit-view spelling | Synthetic `x.Bits()` only | Convenient and infers width | Generic `bitsNat(N,x as int)` first; `.Bits()` as optional checked sugar |
| Shared-option loading | Unconditional reference prelude declarations | Easy wiring but can alter old output and RU | Conditional fixed fragment; public ghost signatures only in language-enabled mode; measure all four option combinations |
| Nonnative compilation | Reject every nonnative width | Small compiler patch | Integer runtime fallback where supported; honest C++/Rust target exclusions until their missing representations exist |

### 10.1 Client annotation burden

These are **interface predictions**, not verification results for an implemented
integer-bitwise translator. Assume a fixed positive N, `M=2^N`, full-interval
unsigned newtypes (`0<=x,y,a,b,m<M`), legal shift/rotation counts `0<=k<=N`, and
bit indices `0<=i<N`. The recommended references are opaque to ordinary clients,
with proved range facts and a scalar proof library. Every lemma name below is a
proposed library obligation, not a claim that such an API already exists.

“Call” means one scalar lemma call after its preconditions are available.
“Manual” means a client needs an explicit mixed-theory proof if that adapter has
not been supplied; it does not assert that no small empty-body example could
happen to verify. The alternative keeps the same integer-valued newtypes and
integer comparisons but lowers bitwise operations through BV casts, with the
existing round-trip facts. Pure word identities do not automatically establish
the corresponding mixed integer statement.

| Client fact | Integer references and proved library | Internal BV and round trips |
|---|---|---|
| `x & (2^k-1) == x % 2^k` | Call `AndMaskLow(N,x,k)` | Call proved `BvAndMaskLow_N`; round trips alone leave a manual mask/division proof |
| `x & y <= x` | Call `AndBound(N,x,y)` | Call `BvAndBound_N`; the native unsigned word inequality is easy, but transferring it to the stated integer comparison still needs a justified adapter |
| `x | y >= x` | Call `OrBound(N,x,y)` | Call `BvOrBound_N`; same native-order/integer-order distinction |
| `(x >> k) << k == x - x % 2^k` | Call `ShiftRoundDown(N,x,k)` | Call `BvShiftRoundDown_N`; round trips alone supply neither shift/division equation |
| `x ^ x == 0` | Call `XorSelf(N,x)` | Expected automatic: native self-XOR becomes zero and the literal observation becomes integer zero |
| Bit test via `(x >> i) & 1` | Call `BitTest(N,x,i)` to connect the expression with `bitsNat(N,x)[i]` | Call `BvBitTest_N`; the R/A derivation supplies the arithmetic part, then the same proved view theorem |
| Mask merge `(a & m) | (b & !m)` | Its result fits automatically; its pointwise meaning needs `MaskMergeBit(N,a,b,m,i)` | Result fits automatically; pointwise meaning needs `BvMaskMergeBit_N` or a manual selection/distribution proof |
| Popcount of `2^i` | Call `PopPower(N,i)`, with `i<N` | Call `BvPopPower_N`; round trips alone say nothing about popcount or a bit-sequence fold |
| `x << k < 2^N`, given `x < 2^(N-k)` | Automatic from the full-interval result type once ordinary well-formedness is checked | Automatic from the unsigned result observation and range |
| Rotate by 0 and by N | Call `RotateEndpoints(N,x)` for left/right endpoints | Expected automatic after native constant-count simplification and the input round trip |

For mask merge the actual proposition is
`bit(N,result,i) == (if bit(N,m,i) then bit(N,a,i) else bit(N,b,i))`; merely writing the
expression is not a theorem. The left-shift row as requested is only a range
claim. Its stronger, often intended claim `x << k == x*2^k` uses the stated
no-overflow premise and needs `ShiftFits(N,x,k)` or `BvShiftFits_N`.
At k=N that premise forces x=0. Powers in this table are mathematical powers
`P(k)`; Dafny's `^` is XOR. Construct `P(k)-1` in integer arithmetic and cast
the complete mask: `P(N)` itself does not fit in the N-bit newtype. A whole
sequence equality for mask merge additionally needs extensionality unless the
library exports that complete theorem. Extra newtype predicates are outside this table:
masking into `[0,M)` does not establish an arbitrary additional predicate.

With equally equipped proved libraries this conservative table gives nine
one-call rows and one automatic row for opaque integer references, versus
seven one-call rows and three expected-automatic rows for internal BV lowering.
Some one-call BV adapters require proof shaping: the measured composed bit and
rotation clients use assertion isolation. With **round trips only**, the BV
alternative has substantial manual mixed-theory work in the seven nontrivial
rows. Providing the missing adapters honestly converts that into library work,
not unavoidable repeated client work.

Keep the accepted integer-reference architecture, but do not justify it by a
claim of uniformly fewer annotations. The table gives internal BV a small
convenience advantage for self-XOR and constant rotations. Integer references
make the seven arithmetic/view-oriented obligations ordinary integer/sequence
library theorems and keep mixed BV/integer construction confined to explicit
interoperability. The measured adapter-construction costs make that separation
valuable. Supply the named scalar lemmas in M1 and expose the two simple
identity lemmas conveniently; do not add trusted automatic facts merely to
save those calls. Actual newtype-client measurements remain an implementation
gate, and could refine these automatic classifications.


## 11. Permanent tests and acceptance gates

### 11.1 Mathematical and semantic tests

Create a regression driver under
`Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/` with separate
positive, negative, and vacuity inputs. Test both resolvers and both pinned Z3
versions. Pure reference and native-helper proof bodies contain no assumptions, axiom
declarations, bodyless unproved lemmas or skipped verification. Design-only
adapter projections may contain only the separately inventoried basic-fact
stand-ins. Production adapters must use the actual compiler mechanism; fixed
definition rules and native bridge emissions remain inventoried trusted
components. Boolean native identities and pure mathematics are independently
checked with additional axioms OFF. Rotation native helpers are checked in the
enabled generated files; their bodies contain no projected assumptions, but
may use the already existing enabled verifier theory.

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

Do not count timeout, out of resource, parser error or an earlier failed
precondition as the intended vacuity rejection. Record the underlying solver
status as well: `unknown (incomplete quantifiers)` can lead to Dafny
`Errors`/Boogie `Invalid`, but is not a SAT result. Require the two distinct
checks of section 9.3: an operational live false rejection and genuine SAT for
the complete finite emitted-fact slice, with independently checked certificate
values and the mathematical extension argument. A resource-exhausted control
still fails; the ground witness never retroactively passes the old context.
If the acceptance requirement is SAT for the entire unsliced Dafny VC, that
stronger requirement remains unestablished by this evidence. Do not silently
claim it was met. Actual-emitter controls and source-to-emission correspondence
are mandatory before any production fact can ship.

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

The fixture names below are proposed permanent regression identities. Put source
regression drivers under the normal `git-issues/` test directory, with separate
positive, negative and control inputs. No production milestone is completed by
this design PR.

**B0 (both options OFF identity):** compare the pre-existing input corpus against
a build without the feature: byte-identical Boogie, unchanged verdicts and
controlled RU, and unchanged relevant parsing/resolution diagnostics. Construct
no new reference declarations, descriptors or facts on that path. New optional
proof files have their own rows; do not mix their construction cost into the
old-corpus identity claim.

**S4 (suite and source-library comparison):** compare the full regression suite
and source Std library in all four language/axiom flag combinations, on both
pinned solvers and both resolver paths where applicable. Use the standing limits
and ordering controls (`normalizeDeclarationOrder:0` for the suite;
`deterministicLiteralHashes:1` and `restartProver` for source Std). Retain
matched/unmatched rows, verdict changes and RU, including resource failures.
M0/M1 cannot run language-enabled modes before the option exists: record those
modes as unavailable, and require M2 to close the complete S4 matrix.

**V2–V4** below means the named live definition, bridge and source-operator
controls introduced in M2–M4, with their facts actually active. They include
wrong-result as well as false assertions, real #97 lambda/Requires observations,
and the section 9.3 finite-slice SAT/mutation checks when trusted facts are added.
An exhausted control fails. Operational Dafny rejection and raw solver SAT remain
separate reported outcomes. Runtime comparisons supplement these logical controls.

| Milestone and named regression | Vacuity/control requirement | Both-OFF identity | Suite/Std comparison |
|---|---|---|---|
| **M0: design/evidence.** `193-design-evidence`: reproduce the models, exact proof inventory, query profiles and ground witnesses; review the fixed definitions and ten-fact inventory. | Current concrete controls, all-ten/#97 context and A+1 sensitivity; preserve historical failures and unknown reasons. Production-emitter correspondence remains pending. | Capture B0 baseline; documentation-only product-tree identity. | Capture available S4 baselines; no claim of an implemented-feature delta. |
| **M1: pure mathematical library.** `193-reference-laws`: ordinary definitions, full sequence/value bijections, pointwise laws, base-two adapter, and the ten client lemmas in section 10.1. Verify every body independently. | `193-reference-live-false`: inhabited calls to proved laws followed by false and wrong-value assertions; include zero width and leading zeros. No new axioms or skipped proofs. | B0 on old inputs; new pure proofs run with both features OFF. | S4 for available modes; record unavailable language modes for M2, and report new library construction costs separately. |
| **M2: fixed references/unfolding.** `193-reference-emission` and `193-bootstrap`: conditional UF signatures, exact finite unfold rules, proper builtin construction, raw-signature range proofs, then consumer contracts; options and DOO reload. | `193-unfold-live-false` plus actual definition-slice SAT and mutations. Test source/DOO spoofing, duplicate custom-prelude symbols, branch guards and noncircular range proofs. | B0 including no new declarations/bindings on the old path. | Complete S4 before accepting this product change; measure reference availability and unfolding separately from BV bridges. |
| **M3a: arithmetic observation emitter.** `193-arithmetic-emission`: +/−/×/division/remainder/order, raw/composed forms, guards, scalar context and excluded operands. | `193-arithmetic-live-false`: each actual emitted fact, cumulative conjunction, wrong results, nonzero guards, complete emitted-slice SAT and mutations; real #97 interaction. | B0; retain pure-BV emission identity where no integer observation is eligible. | S4 against the predecessor with all older shared families active; list every moved row. |
| **M3b: Boolean/shift emitter and adapters.** `193-boolean-shift-emission`: A/C/L/R and checked OR/XOR/ANDNOT/bit/rotation adapters; every pure/native helper is an explicit proof input. | `193-boolean-shift-live-false`: raw/composed and derived summaries, all-ten/#97 conjunction, actual emitted-slice SAT; counts 0/N and invalid counts. Retain #147 value, bound and emission controls. | B0; pure native helpers receive no unsolicited mixed facts. | S4; redo the complete minimal-basis client/support table with actual emission and cumulative M3a facts. |
| **M4: integer frontend/verifier.** `193-newtype-operators`: semantic width, both resolvers, constant folding and direct reference lowering; full/strict predicates, aliases, exports, refinements, caches and recorded library modes. | `193-source-live-false`: actual source operators and unfold/library facts active; reject false subtype membership, bad casts/counts and wrong values. Retain V2–V3. | B0 including formerly inert attributes, diagnostics and incremental invalidation. | S4 including the newtype client corpus; no reference-only proxy for actual source syntax. |
| **M5a: common generator/C#.** `193-runtime-csharp`: native and BigInteger execution, small-width exhaustive and boundary inputs, exact semantic masks, count endpoints and single evaluation; inspect generated code. | Retain V2–V4; execute an independent reference oracle. No runtime BV/sequence detour. | B0 for old generated code and verifier output. | S4; separate verifier cost from target build/execution results. |
| **M5b: Java.** `193-runtime-java`: promoted byte/short signs, logical right shift, host count masking, native and BigInteger paths. | Retain V2–V4; generated and executed reference comparisons, especially counts 0/N. | B0, including old Java output. | S4 plus Java target comparisons. |
| **M5c: Go.** `193-runtime-go`: native `&^`, semantic masks/counts, arbitrary integers and both maintained runtime copies. | Retain V2–V4; independent executed reference oracle and single-evaluation cases. | B0, including old Go output. | S4 plus Go target comparisons. |
| **M5d: Python.** `193-runtime-python`: arbitrary integers, complement/shift masks, endpoints and the supported minimum version. | Retain V2–V4; independent reference execution and compatible helper/fallback tests. | B0, including old Python output. | S4 plus Python minimum-version and target comparisons. |
| **M5e: JavaScript.** `193-runtime-javascript`: repair or bypass #246; bits 31/32/53/63, widths 33–53 and wider values, unsigned normalization and high limbs. | Retain V2–V4; keep #246 reproductions and execute compiled source against the reference, not only direct runtime calls. | B0, including old JS output; separately review any unavoidable bug-fix delta. | S4 plus JS runtime/target comparisons; no unrepaired helper in a promised path. |
| **M5f: C++.** `193-runtime-cpp`: unsigned carriers, semantic masks, full-width shifts, C++17 constraints and explicit unsupported nonnative diagnostics. | Retain V2–V4; reference execution and sanitizer/debug boundary cases. | B0, including old C++ output. | S4 plus supported/unsupported target rows. |
| **M5g: Rust.** `193-runtime-rust`: semantic width through DAST, supported carriers, generated-artifact rebuild and explicit nonnative exclusions. | Retain V2–V4; reference execution, debug-mode count boundaries and semantic/carrier-width mismatches. | B0, including old DAST/Rust output. | S4 plus Rust target and regenerated-artifact comparisons. |
| **M6a: rotation/bit surface.** `193-rotation-bit-source`: both member-resolution paths, clones, final predicates, legal indices, nested rotations, non-power-of-two widths and #147. | `193-rotation-bit-live-false`: actual source operations/adapters, wrong results and invalid membership; retain V2–V4 and #147. Add end-to-end cases to every supported backend fixture. | B0 including member resolution and old BV wrappers. | S4 plus all backend rotation/bit execution rows and current client RU. |
| **M6b: population count.** `193-popcount-source`: generic reference theorem, zero/all ones/high bit/power-of-two laws, result bounds and target intrinsics/fallbacks. | `193-popcount-live-false`: active correspondence with false and wrong-count assertions; retain V2–V4. Independent execution on every supported target. | B0 including minimum-version/fallback behavior. | S4 plus popcount proof/runtime rows; any proposed accelerator needs its own model/control gate. |
| **M7: convenience/release.** `193-convenience`: optional `.Bits()`, exact-interval inference, manual/examples and cache behavior. | Retain the complete live-false, emitted-slice SAT/mutation and runtime inventories; inference cannot bypass visibility, predicates or module mode. | Final B0; no unresolved required identity check. | Final S4 with every supported operation/target accounted for and exclusions tested. |

M3a/M3b may proceed independently after M2's identity and definition interface is
stable, but each must rerun the cumulative shared-option controls. Backend PRs
may proceed in parallel after M4 fixes the resolved operation representation.
Testing a runtime rotate/popcount helper in M5 does not count as end-to-end
source-feature acceptance before M6 supplies the source operation.

Every product PR carries its regression driver, release-note fragment and
required `.github/review/base`/regression updates. Any intended verdict change
follows the existing policy: first expose it in CI, then update the expected
artifact in a separate explained commit. Do not defer these gates to M7.


### 12.1 Next question, answered: may a fitting carrier supply rotation semantics?

**Question:** if the operands and the verified result fit the selected native
carrier, may the backend use that carrier's rotate operation directly?

**Answer: no, unless its rotation width matches the semantic width or the
backend separately establishes an equivalent lowering.** Consider semantic
N=12 and a stricter unsigned newtype whose values are below 256, implemented
in an 8-bit carrier. The legal source input x=16, k=8 has
`rolNat(12,16,8)=1`: bit 4 moves to bit 0 modulo 12. An 8-bit carrier rotation
by 8 instead returns 16. Both input and both possible outputs fit the carrier
and the newtype, so result-membership checking alone cannot catch the wrong
lowering. This is a counterexample to a proposed optimization, not a claim of
an already implemented integer-newtype bug.

The per-backend refinement obligation is
`decode(C_N,W(encode(a),k)) == rolNat(N,a,k)` for every source-legal argument
whose result satisfies the original newtype predicate. The implementation must
retain N even when W is smaller or larger. It may use a native rotate directly
when N=W; otherwise use correctly masked/widened shifts or another proved
implementation at N. Counts 0/N and intermediate truncation require explicit
attention. Add `(N,W,x,k)=(12,8,16,8)` to `193-rotation-bit-source` and each
supported backend's execution fixture, alongside the full-width count cases.
The same obligation explains why complement masks at N and why a host's
modulo-carrier-width shift rule cannot silently replace the source count rule.

## 13. Critical review, ranked by severity

P0 denotes potential false verification or wrong executable results. P1 blocks
implementation acceptance or shipping; P2 concerns cost, usability or scope.
These findings review a proposal, not an implemented language feature.

| Severity | Finding | Disposition |
|---|---|---|
| P0 | A reference body chosen by user name/attribute could give an arbitrary meaning to a trusted bridge. | Replace source authority with fixed compiler identities and exact local unfold rules. The new rule is itself a trusted definitional mechanism, with M2 proof/emission/control gates. |
| P0 | A range proof can be circular if its own consumer contract is already assumed. | Raw integer signatures for foundation checking; candidate derived/range contracts and equivalent automatic facts excluded until independently proved. |
| P0 | An opaque export's copied width attribute can recreate hidden representation capability. | Independent of prelude ownership. Require revealed integer-newtype structure and a descriptor validated in the declaration's recorded mode; test both resolvers, clones and reloads. |
| P0 | Incorrect divisor/count/cast handling, binder escape, heap mismatch or invented call permission can make a local fact false. | Exact schemata, guarded composition, explicit scalar context and actual-emission correspondence. The Python model documents which guards concern truth, source definedness or nonminimal policy. |
| P0 | Full-interval closure does not imply an arbitrary newtype predicate, and carrier fit does not establish rotation meaning. | Preserve every result-membership obligation, including special operations. Section 12.1 gives a fitting-input/fitting-result counterexample to rotating at the carrier width. |
| P1 | The old reference-transparent false controls exhaust; ordinary Errors/Invalid can still conceal raw solver unknown. | Diagnosis and the two-part control method are concrete: operational rejection plus genuine finite-slice SAT and mathematical extension. Full unsliced-prelude SAT is not established. Production controls must use actual emitted instances. |
| P1 | Minimal-basis library construction is much dearer than calling its checked contracts. | Measured, not hidden: integer-input clients cost at most 66,537 RU, but native variable-count rotation support exceeds 16 million RU per width-64 helper. All selected bodies verify; unsplit caller failures remain explicit. |
| P1 | The new fixed-reference mechanism and the real collector have not been implemented or measured. | Current projections establish conditional derivations and costs. M2/M3 still need their own live controls, identity and complete S4 comparisons. No prototype result waives these gates. |
| P1 | Existing wide JavaScript helpers have publicly reproduced wrong results (#246). | Repair or bypass before using them; test compiled source as well as direct helpers. Other backends retain promotion, count, minimum-version and unsupported-target gates. |
| P1 | Conditional declarations, public intrinsic bindings or metadata reconstruction can alter the default path. | Every milestone names B0/S4; no new reference construction on the both-OFF path. Exact product-version compatibility is an ABI check, not authority supplied by a library. |
| P2 | Native simplification can save two client annotations compared with strictly opaque integer references. | State the advantage honestly; provide the nine scalar integer lemmas and keep mixed conversion construction in the interoperability library. Actual source-language automation remains to be measured. |
| P2 | Direct facts for derived operations enlarge the trusted interface. | No demonstrated accelerator need in the shaped clients. Apply the explicit client/project thresholds and all soundness/compatibility gates before adding one. |
| P2 | Signed/unbounded semantics, arbitrary width inference and automatic binder harvesting would expand the design substantially. | Deferred; total reference functions and explicit scalar lemmas do not silently enable those language features. |

The definitions, models, exact conditional derivations and reference-ownership
decision are settled enough to start M1. That frees pure mathematical library
work from frontend/backend implementation and from adding solver facts. The
remaining release risks are concrete integration obligations: definition
unfolding, scope and visibility, emitted facts, proof costs and target arithmetic.
The accepted architecture permits the focused milestones; it does not certify
unimplemented gates or turn historical exhausted controls into passes.

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
