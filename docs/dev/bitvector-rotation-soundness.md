# Bitvector rotations: soundness of the encoding

[Issue 147](https://github.com/erniecohen/dafny/issues/147) reports that Dafny 4.11.0 with
Z3 5.1.0 proves this false postcondition:

```dafny
method MixedBad(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 0
  ensures x.RotateRight(a).RotateLeft(b) == (4 as bv3)
{
}
```

Rotating a three-bit word by three places is the identity, and so is rotating it by zero, so
the value is 1.  This document says why the verifier proved 4, what the fix changes, and why
the result is sound.  The fix is in `Source/DafnyCore/Verifier/BoogieGenerator.cs`,
`AddBitvectorRotateFunction`.

## Cause

Dafny checks that the count `n` of `x.RotateLeft(n)` or `x.RotateRight(n)` on a `bvW` satisfies
`0 <= n <= W` (`CheckWellformedSpecialFunction`), converts it to a `bvW` with `nat_to_bvW`, and
calls `LeftRotate_bvW` or `RightRotate_bvW`.  Before the fix those were declared, for every
positive `W`, as Z3's `ext_rotate_left` and `ext_rotate_right`.

Z3's bitvector rewriter (in Z3 5.1.0,
[`src/ast/rewriter/bv_rewriter.cpp`](https://github.com/Z3Prover/z3/blob/0b6cdcdbc65da25ef0f73ac9da210574d0f66cf8/src/ast/rewriter/bv_rewriter.cpp#L2287-L2352))
rewrites a nested extended rotation into one rotation, whose count is the sum or the difference
of the two counts, computed with `bvadd` or `bvsub`: modulo `2^W`.  The rotation then reduces
its count modulo `W`.  The composition is right for every count only if

    (z mod 2^W) mod W == z mod W   for every integer z,

that is, if `W` divides `2^W`, that is, for positive `W`, if `W` is a power of two.  For
`W = 3`, `a = 3`, `b = 0`: `L(R(x, 3), 0)` becomes `L(x, (0 - 3) mod 8) = L(x, 5)`, which is a
left rotation by `5 mod 3 = 2`, and takes `001` to `100`, which is 4.

The defect is not confined to a zero or full-width count, nor to opposite directions:

| width 3, `x == 1` | value | value of the faulty rewrite |
|---|---:|---:|
| right by 3, then left by 0 | 1 | 4 |
| left by 3, then right by 0 | 1 | 2 |
| right by 2, then left by 1 | 4 | 2 |
| left by 3, three times | 1 | 2 |
| left by 2, four times | 4 | 1 |

The last line shows that reducing each count modulo `W` before the native operator would not
help: four counts of 2 sum to 8, which wraps to 0 modulo `2^3`.  Z3 rewrites rotations by
literal counts before it composes them, so the regression keeps the counts symbolic, as the
issue does.

## The encoding

| width | `LeftRotate_bvW`, `RightRotate_bvW` |
|---|---|
| `W == 0` | unchanged: an inline function whose value is the one value of `bv0` |
| positive power of two (1, 2, 4, 8, 16, 32, 64, 128, ...) | unchanged: `{:bvbuiltin "ext_rotate_left"}`, `{:bvbuiltin "ext_rotate_right"}` |
| any other positive `W` | an inline definition by shifts, below |

```boogie
function {:inline} LeftRotate_bvW(x: bvW, n: bvW) : bvW
{ or_bvW(LeftShift_bvW(x, n), RightShift_bvW(x, sub_bvW(Wbv W, n))) }
function {:inline} RightRotate_bvW(x: bvW, n: bvW) : bvW
{ or_bvW(RightShift_bvW(x, n), LeftShift_bvW(x, sub_bvW(Wbv W, n))) }
```

`LeftShift_bvW`, `RightShift_bvW`, `or_bvW` and `sub_bvW` are Z3's `bvshl`, `bvlshr`, `bvor` and
`bvsub`.  The call sites, the count checks, the names and the signatures do not change.

### Kind

This is a **reviewed replacement definition**.  It neither only weakens (it removes no axiom and
guards none) nor asserts a new fact: it replaces the meaning of two function symbols at the
affected widths with a definition, which a reviewer must check against the argument below.  It
needs no option: it is a soundness fix, which `AGENTS.md` allows to change the defaults, and it
adds no quantified axiom.

## Soundness argument

Write `L(x, n)` and `R(x, n)` for the rotations that Dafny's `RotateLeft` and `RotateRight`
denote: bit `j` of `L(x, n)` is bit `(j - n) mod W` of `x`, and bit `j` of `R(x, n)` is bit
`(j + n) mod W` of `x`.

1. **The counts are exact.**  A well-formed call has `0 <= n <= W`, checked before the call is
   used.  Since `W < 2^W` for every `W >= 1`, `nat_to_bvW(n)` is `n`, and `W - n`, computed by
   `bvsub`, lies in `0..W` and does not wrap.
2. **The bits are the rotation's.**  For `0 < n < W`: bit `j` of `x << n` is bit `j - n` of `x`
   for `j >= n`, and 0 for `j < n`; bit `j` of `x >>> (W - n)` is bit `j + W - n` of `x` for
   `j < n`, and 0 for `j >= n`.  The two are nonzero on disjoint bits, and on each bit the
   nonzero one is bit `(j - n) mod W` of `x`.  So their OR is `L(x, n)`.  `R` is symmetric.
3. **The endpoints.**  At `n = 0` and at `n = W`, one shift is by 0, which gives `x`, and the
   other by `W`, which in SMT-LIB gives 0.  So both give `x`, which is `L(x, 0) = L(x, W)`.
4. **Width 0** keeps its existing representation, so no SMT sort of width 0 appears.
5. **Composition.**  Each definition equals the rotation on every argument a well-formed program
   gives it, so nested calls equal nested rotations, by substitution.  No count arithmetic
   modulo `2^W` occurs: Z3 sees shifts and ORs, not extended rotations, at these widths.
6. **Outside `0..W`** the definition is a total function that differs from a rotation; no
   well-formed program reaches it there, and being a definition, it can introduce no
   inconsistency.
7. **The native path** is kept only where `W` divides `2^W`, where the rewriter's count
   arithmetic is exact modulo `W`.  The rewriter's other rules for extended rotations
   (rotation by a literal, and cancelling opposite rotations by the same count) are sound at
   every width, and Z3's bit-blaster reduces the count modulo `W`
   ([`bit_blaster_tpl_def.h`](https://github.com/Z3Prover/z3/blob/0b6cdcdbc65da25ef0f73ac9da210574d0f66cf8/src/ast/rewriter/bit_blaster/bit_blaster_tpl_def.h#L986-L1029)),
   as the review of the rewriter in
   [the plan for issue 147](https://github.com/erniecohen/dafny/issues/147#issuecomment-6741405156)
   found.

Points 2 and 3 were checked by enumeration: for every `W` from 1 to 12, every `x` and every
`n` in `0..W`, both definitions equal the bit permutation (196,608 checks, no mismatch).  And
`W` divides `2^W` for `W` in `1..70` exactly for 1, 2, 4, 8, 16, 32 and 64.

Why not use the definition by shifts at every width: it loses the cheap cancellation rule.  On
`R(L(x, a), a) == x` with `a <= W` and a resource limit of 200,000, Z3 5.1.0 proves the native
form at widths 3, 12, 31, 32, 64 and 65 in 47 resource units each, and the shift form in 1,709
at width 3 and 44,829 at width 12, and not at all at 31, 32, 64 or 65 (measured by direct SMT
queries for the plan in the issue).  Widths 8, 16, 32 and 64, the common ones, keep the native
operators.

## Regression tests

`Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/github-issue-147.dfy` runs each
fixture in `git-issues/Inputs/` with Z3 5.1.0 (`%review-z3`) and Z3 4.12.1
(`%review-z3-4.12.1`), in both resolver modes, with the issue's resource limit of 200,000:

| fixture | expected |
|---|---|
| `github-issue-147-original.dfy`, the issue's program, alone | its postcondition fails |
| `github-issue-147-good.dfy`, the same with `== 1` | verifies |
| `github-issue-147-controls.dfy`: `assert false` under the same preconditions, and in branches on the correct values, at widths 3 and 6 | each `assert false` fails |
| `github-issue-147-true.dfy`: both orders, interior counts, same-direction chains, counts 0 and `W`, widths 0, 1, 3, 6, 12, 32, 64 | verifies |
| `github-issue-147-false.dfy`: the values the faulty rewrite gives, and wrong values at 32 and 64 | each postcondition fails |
| `github-issue-147-bounds.dfy`: counts -1 and `W + 1` | each count is reported |

`git-issues/github-issue-147-emission.dfy` checks what is sent to the solver: for a program with
rotations only at widths 0, 3, 6 and 12, the SMT log is not empty, has a `check-sat`, and has
no `ext_rotate` and no `(_ BitVec 0)`; the BPL defines the rotations at those widths by shifts.
For a program with rotations only at widths 1, 32 and 64, the BPL keeps the `bvbuiltin`
declarations and the SMT log has the extended rotations.

## Validation

VALIDATION
