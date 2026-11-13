# Model arguments and executable checks

Let `N > 0`, `M = 2^N`, and interpret a word as a Boolean vector indexed by
`0..N-1`. Its unsigned value is `U(x) = sum(2^i * x[i])`. Interpret `I(a)` as the
vector of the Euclidean residue `a % M`. Interpret the reference functions by
section 2's terminating recurrences. The paragraphs below apply at every positive
width, not only the widths tested by the executable model.

**A — conjunction.** Split each word into its low bit and the quotient of its
unsigned value by two. The low output bit is one exactly when both input
remainders modulo two are one; the remaining output bits are the conjunction
of the quotient vectors. Induction on `N`, with empty-vector value zero, is
exactly `andNat`'s recurrence, and therefore gives the A equation. This also
shows that the total integer reference ignores every bit above `N-1`, including
for negative integers under Euclidean division.

**C — complement.** Complement replaces every Boolean coefficient `b_i` in the
weighted sum by `1-b_i`. Summing the weights gives
`sum(2^i, i=0..N-1) = M-1`, so the unsigned complement has value `M-1-U(x)`.
No signed host-language complement is used without an explicit width mask.

**+ — addition.** The full-adder equations at each position preserve the weighted
sum of the input digits plus the incoming carry. Summing those equations cancels
the internal carries. The only discarded quantity is the final carry times `M`,
so the output is congruent to `U(x)+U(y)` modulo `M`. Since its unsigned value is
in `[0,M)`, it is the unique Euclidean residue stated in the schema.

**− — subtraction.** The fixed-width subtractor adds the complement of `y` and
an initial carry of one. C gives complement value `M-1-U(y)`, and the addition
argument gives the residue of `U(x)+M-U(y)`, which is `(U(x)-U(y)) % M`.
This uses Euclidean remainder, so underflow produces a nonnegative residue.

**× — multiplication.** Multiply by the Boolean digits of `y`, shifting the
partial result once at each step and adding either zero or `x`. The Horner
invariant says that the partial word is the product with the processed prefix,
modulo `M`. Induction over the `N` digits gives the product residue. Discarding
high bits at intermediate steps preserves this invariant because congruence
is preserved by addition and multiplication.

**/ — unsigned quotient.** For `U(y)>0`, restoring binary long division maintains
`processed_prefix = quotient*U(y)+remainder` with
`0 <= remainder < U(y)`. At completion this is the unique Euclidean quotient
and remainder of `U(x)` by `U(y)`. The quotient fits in the word because it is
at most `U(x)`. The schema deliberately excludes zero: native BV division is
total there, while source integer division requires a nonzero divisor.

**% — unsigned remainder.** The same long-division invariant gives the unique
remainder in `[0,U(y))`; this is precisely `U(x) % U(y)`. It fits in `[0,M)`.
At zero divisor native BV remainder returns the dividend; this does not make
the source expression `U(x) % 0` well formed, so the guard remains.

**L — left shift.** For `0 <= k <= N`, `k < 2^N`; consequently `I_N(k)` denotes
the original source count without wrapping. Moving each input bit up `k`
positions multiplies its weight by `2^k`; discarding positions at least `N`
removes multiples of `M`. The remaining word is `(U(x)*2^k) % M`. At `k=N`
all bits are discarded and both sides are zero.

**R — right shift.** Under the same count guard, deleting the low `k` digits
and moving the rest down divides each retained weight by `2^k`. The discarded
low digits are the remainder of `U(x)` modulo `2^k`, leaving the quotient
`U(x) / 2^k`. At `k=N`, `U(x)<2^N` makes the quotient zero. This is logical
unsigned right shift, not a signed arithmetic shift.

**≤ — unsigned comparison.** Compare the vectors from the greatest index
downward. If they first differ at index `j`, the vector having bit one there
has greater value: its weight `2^j` exceeds the sum `2^j-1` of all smaller
weights. Equal vectors have equal value. Thus native unsigned comparison is
exactly the comparison of `U(x)` and `U(y)`.

**Composed casts — all schemata.** Substitute `I(a)` and, where applicable,
`I(b)` into the corresponding raw schema. Under `0<=a<M` and `0<=b<M`, their
unsigned values are `a` and `b`; the already approved bounded round-trip facts
state exactly these equalities. Substitute them in the reference arguments,
arithmetic expression and divisor guard, retaining the source count guard and
every relevant operand guard. The resulting equation is the composed fact;
it adds no new semantic assumption beyond the raw schema and bounded round
trips. This is not authority to accept an ill-fitting Dafny source cast.

## What the executable model checks

From the repository root, run:

```sh
python3 docs/dev/design/193-evidence/models/run_all.py
```

Only Python's standard library is used. `model.py` has two independently coded
sides. The sequence side extracts digits with Euclidean division, implements a
ripple-carry adder/subtractor, shift-and-add multiplication, restoring long
division, Boolean pointwise operations and index permutations. The native side
uses Python integer operators with explicit masks and native BV zero-divisor
and wrapped-count conventions. Recursive references are evaluated by finite
loops; OR/XOR/ANDNOT use the exact compositions in the design, not replacement
primitive equations.

For each width 1 through 8, every binary operand pair is checked. Every unary
operand and every word/count pair with count `0..N+1` is checked. Each schema
and each composed cast form is counted separately; results record how many
implications had true or false guards. False guards do not count as equation
checks. Zero-width singleton semantics are checked separately, with no
positive-width native declaration constructed.

At each width 16, 32 and 64, the default run uses **100,000 independently
sampled uniform operand pairs**, each with a uniform valid count. The first
operand is also tested at counts 0, N and N+1. Reproducible seeds are recorded.
Boundary operands, zero divisors, signed/out-of-range casts and count-guard
mutations supplement those samples. The minimum sample count is enforced by
the command-line parser. An assertion failure exits nonzero. No host names,
clock timings or machine-specific paths are retained.

`results.json` is a deterministic summary, not a proof certificate. These
checks can detect errors in the formulas and their finite implementations;
they cannot establish universal correctness at untested widths or inputs,
Boogie-emitter correctness, quantified-context satisfiability, or the absence
of a Z3 implementation defect. The model paragraphs and the live Dafny controls
supply different evidence and are not replaced by these tests.

## Guard counterexamples and their limits

The script executes and records every counterexample below, including the
exception rather than inventing a mathematical value for an undefined term.

| Guard | Executed counterexample | What it establishes |
|---|---|---|
| Divisor nonzero | Integer `0//0` raises; native `bv1(0)/0=1` and `bv2(0)/0=3` | An unguarded quotient family would equate the same integer `div(0,0)` with two distinct values, even if integer division were totalized. |
| Remainder divisor nonzero | Integer `1%0` raises; native `bv3(1)%0=1` | Source definedness requires the guard. It is not a claim that every possible backend totalization contradicts native remainder. |
| Count upper bound | `N=3,x=1,k=8`: `I_3(k)=0`, so native left/right shift gives 1; both arithmetic RHSs give 0 | Dropping the bound entirely permits a false equation after count wrapping. |
| Count lower bound | `k=-1` is outside `P`'s natural domain | Total reference functions must branch before evaluating the negative power. |
| Cast range | `N=3,a=8`: native complement gives 7, unguarded composed C gives −1; `a=-1` gives 0 versus 8 | Both lower and upper range bounds matter for composed C and for source cast validity. |
| Cast range/order | `N=3,a=8,b=1`: native order compares 0 with 1 and is true; `8<=1` is false | The uniform composed-cast range policy is necessary for the family. |
| Width zero | The raw rotation expression evaluates `k%0`; an SMT word constructor rejects width 0 | Use the singleton/empty-vector branch, not a positive-width SMT encoding. |

The guards are **sufficient interface conditions, not a claim of mathematical
minimality for every schema**. For example, L/R still agree for `N<k<2^N`;
AND and modular arithmetic are periodic in their integer operands, so their
composed equations can survive an out-of-range operand. All ten facts have a
consistent singleton extension at width zero; the counterexample there is to
an unguarded positive-width construction or rotation formula, not a fabricated
inequality between singleton values.
