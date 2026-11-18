# Conditional bit and rotation derivations

Run `python generate.py` in this directory. The deterministic generator writes
exact Dafny inputs for widths 8, 16, 32, and 64:

- `derived-N.dfy`: native adapters, checked bridge derivations, and clients.
- `control_derived-N.dfy`: three grounded false controls using those derivations.
- `derived-composed-N.dfy`: integer-input clients with explicit assertion isolation.
- `control_derived-composed-N.dfy`: three grounded false controls for those clients.
- `ablation-composed-N.dfy`: diagnostic clients whose operands start as integers.

The only trusted statements in the generated proofs are the explicitly marked
local basic bridge instances: two per single-bit derivation (R, A), and three per
rotation derivation (L, R, +). No bit or rotation postcondition is assumed. The
native decomposition, power arithmetic, low-bit recurrence, and normalized
integer rotation identities all have checked bodies. `pure-math.dfy` contains no
trusted statement and is verified separately with additional axioms disabled.

## Derivation

For a native word `x`, write `a = x as int`, `M = p(N)`, and `p(k) = 2^k`.
The single-bit proof first uses R on `y = x >> k`, then A on `y & 1`.
The checked recurrence lemma `lowBit` proves
`andNat(N, y as int, 1) == (y as int) % 2` for every positive N; its tail is
zero by induction. Consequently the observed bit is `(a / p(k)) % 2`.

For a left rotation with `0 <= k <= N`, the native adapter proves
`x.RotateLeft(k) == (x << k) + (x >> (N-k))`. The right-rotation adapter proves
the corresponding reversed formula. Addition is valid because the shifted bit
fields do not overlap. These identities are verified from native bit-vector
semantics, with one literal-count lemma per legal count and a complete balanced
case split. Counts 0 and N are included. No disjointness or native decomposition lemma is assumed.

For left rotation set `p = p(k)`, `q = p(N-k)`, so `M = p*q`, and split
`a = q*d+r`, `0 <= r < q`, `0 <= d < p`. The checked integer lemmas prove
`(a*p) % M = p*r` and `p*r+d <= p*(q-1)+(p-1) = M-1`.
The + bridge therefore introduces a redundant remainder that the proved bound
removes. Right rotation uses the same argument with the powers exchanged.
`rotateLeftMeaning` and `rotateRightMeaning` prove that these legal-count formulas
match the canonical definitions using `k % N`, including `k == N`.
The integer reference functions do not call native rotations.

## Proof cost and controls

Run each generated file with the runner's ordinary 200,000 resource limit. An
explicit `resource_limit` attribute identifies any library obligation given a
larger per-VC budget. Record both the resource sum of every verification scope
and its largest VC: an isolated proof may have a small largest VC and still
consume a substantial total. In particular the finite native adapter is a
library cost paid before using the cheap clients; it is not free background
knowledge. The public reproduction retains the exact generated sources and all
scope outcomes.

Each positive client calls a proved conditional derivation. The three negative
controls use the concrete word 5 and count 1, materialize the needed power
constants, call the same derivation, and assert false. They must report Dafny
Errors / Boogie Invalid at that assertion, without resource failure; that outcome
does not necessarily report SMT SAT. These
controls check the derived summaries; the separate Q1 fixtures check the full
raw bridge conjunction and reference equations.

The composed-input files make no new assumptions. They call the checked cast
round-trip lemma and then the same derivation for `(a as bvN)`. The positive
version uses `isolate_assertions`; the ablation has the same two calls without
that annotation. Measure both the full scope cost and the largest VC. Resource
exhaustion in an ablation is a diagnostic observation, not a successful proof
or consistency result. Keeping both variants separate makes this proof-shaping
dependency explicit.
