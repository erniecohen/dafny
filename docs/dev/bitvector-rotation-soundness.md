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
| `github-issue-147-true.dfy`: both orders, interior counts, same-direction chains, counts 0 and `W`, a symbolic word at width 3, widths 0, 3, 6 and 12 | verifies |
| `github-issue-147-false.dfy`: the values the faulty rewrite gives, at widths 3, 6 and 12 | each postcondition fails |
| `github-issue-147-native-true.dfy`: the native widths 1, 32 and 64, at a resource limit of 2,000,000 | verifies, except one proof with Z3 4.12.1 (below) |
| `github-issue-147-native-false.dfy`: wrong values at widths 1, 32 and 64, at 2,000,000 | each postcondition fails |
| `github-issue-147-bounds.dfy`: counts -1 and `W + 1` | each count is reported |

`git-issues/github-issue-147-emission.dfy` checks what is sent to the solver.  In the BPL of a
program with rotations at widths 0, 1, 3, 6, 12, 32 and 65, widths 3, 6, 12 and 65 have the
definitions by shifts and no `bvbuiltin` rotation, widths 1 and 32 have the `bvbuiltin`
declarations and no inline one, and width 0 is unchanged; one check file per width, since the
order of the declarations is not fixed.  The SMT log of a program with rotations only at widths
0, 3, 6 and 12 has a `bvlshr` and a `check-sat`, so an empty log fails, and no `ext_rotate` and
no `(_ BitVec 0)`; that of a program with rotations only at widths 1, 32 and 64 has the extended
rotations.

`.github/review/regressions` names both drivers, and the existing tests with rotations:
`dafny0/Bitvectors.dfy`, `dafny0/BitvectorsMore.dfy`, `dafny0/GeneralNewtypeMemberVerify.dfy`,
`dafny0/GeneralNewtypeVerify.dfy`, `dafny4/git-issue57.dfy` and the two `proof-obligation-desc`
rotation-bound tests.

## Validation

Scratch probes built `review/4.11.0` (the base) and the fix (the candidate), and ran every
fixture with Z3 5.1.0 (archive sha256 `f47be8d2...92284a`) and Z3 4.12.1 (archive sha256
`e07d4193...cbf3b`, from dafny-lang/solver-builds), in both resolver modes; the four outputs
of each fixture were identical.  Exit codes and summaries, resource limit 200,000:

| fixture | base, Z3 5.1.0 | base, Z3 4.12.1 | candidate, either Z3 |
|---|---|---|---|
| original (`MixedBad`, false) | **0: 2 verified** | 4: 1 error | 4: 1 error |
| good (`MixedGood`, true) | **4: 1 error** | 0 | 0 |
| controls (4 `assert false`) | **4: 1 error**: the three in branches on the correct values are proved | 4: 4 errors | 4: 4 errors |
| true (12 methods, widths 0, 3, 6, 12) | **4: 8 errors**, every method at 3, 6 and 12 but the endpoints and the inverse | 0 | 0 |
| false (8 methods) | **0: all 8 false postconditions proved** | 4: 8 errors | 4: 8 errors |
| bounds | 4: 5 errors | 4: 5 errors | 4: 5 errors |

With Z3 5.1.0, the base proves the issue's false postcondition and seven more, and fails to prove
eight true ones; the candidate gives the right verdict on all of them, with either solver.  With
Z3 4.12.1 the base already gives the right verdicts on these fixtures: the composition rule
that 5.1.0 applies is not triggered there by these programs.

The definitions by shifts cost more than the native operators did, as expected.  With Z3 5.1.0
the candidate's proofs of the true facts at widths 3, 6 and 12 cost from 4,229 resource units
(width 3) to 27,587 (`Bv12Interior`); the largest is well under the issue's limit of 200,000.
A symbolic word costs more: one proof of `R(L(x, c), c) == x` at widths 3, 6 and 12 together,
with symbolic `x` and `c <= W`, cost 6,605 resource units on the base with Z3 5.1.0 (where the
native cancellation rule applies) and 185,966 on the candidate, 28 times as much; with Z3
4.12.1 it ran out of 200,000 on the base and cost 185,156 on the candidate.  That is the price
of soundness at these widths, and why the native operators stay where they are sound.  At
`MixedGood`'s size the costs are equal: its correctness proof cost 4,252 (base, where it
failed) and 4,234 (candidate) with Z3 5.1.0.

At the native widths 32 and 64 nothing changes.  With a symbolic word and a full-width count,
some proofs there (`Bv32FullThenZero`, `Bv64SameDirection`, and `Bv32Inverse` under 4.12.1)
do not finish within 200,000,000 resource units, on the base and the candidate alike; so
`github-issue-147-native-true.dfy` fixes the word, and the native fixtures run at 2,000,000.
With Z3 4.12.1 one of those proofs, `Bv64SameDirection`, still runs out of resources at
2,000,000, on the base and the candidate alike; its `.expect` records it.

### The suite and the standard library

`review.yml` ran on the candidate (scratch branch `scratch/issue147/ci-2`), and a scratch
comparison matched its artifacts, proof batch by proof batch, against those of the push run of
`review/4.11.0` at `0a14436`, the base.

**Suite** (1,161 programs, Z3 5.1.0, resource limit 16,000,000), with `--additional-axioms`
off and on alike: one verdict moved, that of `dafny0/GeneralNewtypeVerify.dfy` (below).  Of
7,605 proof batches, one changed outcome and four changed only their resource count, all in
programs with rotations; no batch changed in the programs without rotations.  The four:
`dafny0/Bitvectors.dfy` `Rotates` 18,746 to 22,395 (+3,649), and three of under +40
(`GeneralNewtypeMemberCompile`, `BitvectorsMore`, `GeneralNewtypeMemberVerify`).  With the
option on, the same batches moved by the same amounts, within 2 units.

**Standard library** (2,190 declarations; Z3 5.1.0 and 4.12.1; option off and on): no
outcome changed.  The library has no rotations; its total resource count moved by +0.0005%,
-0.0002%, -0.0002% and -0.0034% in the four configurations, within the run-to-run variation
that `.github/review/std-verdicts.py` documents for its declaration order.

**`dafny0/GeneralNewtypeVerify.dfy`**: its method `Bitvectors.Rotate` rotates a `bv5`
newtype, which the fix moves to the definition by shifts.  Its correctness proof has four
failing checks (lines 361, 364, 367, 371) in one verification condition.  With the fix that
condition ran out of resources: over 16,000,000 units under Z3 5.1.0, against 64,436 before,
and past the harness's 300 s under Z3 4.12.1.  Investigated by isolating the checks: each
alone fails as before, with the same message, and costs at most 51,099 units with the fix
against 39,563 before (5.1.0), or 12,206 against 11,193 (4.12.1); split into separate methods,
the outcomes and costs are those of the base (at most 11,048 against 11,235).  So no check
became hard; the single condition, in which the verifier goes on after each counterexample,
did.  No proof is gained or lost: the method failed before and fails after.  The test now puts
`{:isolate_assertions}` on `Rotate`, which keeps its four errors and every line number; its
summary counts the isolated batches.  The expected verdicts of the suite record the change,
with this reason, in a commit of their own.
