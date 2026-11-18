# Public measurements for issue 193

Source: [public probe run](https://github.com/erniecohen/dafny/actions/runs/42754841657), benchmark commit `79d20aa`, compiler source `62bae981952337d312cbb3d43c39f9813cca6084`, reporting version `4.11.0+fcb2042d.review.2d6fd450`.

The run uses 200,000 RU per batch, 15 seconds as a safety timeout, `--additional-axioms`, one prover core per invocation, `/normalizeDeclarationOrder:0`, and `/restartProver`. It measures existing syntax and explicitly trusted bridge API projections on an unmodified compiler. It does not measure the proposed frontend, local axiom emitter, or complete minimal-kernel library.

**Completeness checks:** each of the four solver/resolver configurations contains 32 generated files and 1,272 verification scopes. Every source hash matches its manifest. All 128 invocations produced complete results. Every well-formedness scope is `Correct`; failures in the tables below are correctness/control outcomes. Legacy and refreshed resolvers have exactly equal scope sets, outcomes, resource counts, and per-VC results for all 1,272 scopes on each solver. No zero-RU observation occurs in these data; omitted trivial scopes have not been invented as zero-cost measurements.

## Outcomes by mode

Counts below are per resolver; the second resolver is an exact repeat. Each triple is **Correct / Errors / OutOfResource**, including helper and well-formedness scopes. No timeout, parse failure, missing log, or other outcome occurred.

| Mode | Z3 5.1.0 all scopes | Z3 4.12.1 all scopes | Client/control correctness interpretation |
| --- | ---: | ---: | --- |
| `assume_false` | 156 / 0 / 0 | 156 / 0 / 0 | 68 proved, 0 errors, 0 resource limits (5.1.0); 68 proved, 0 errors, 0 resource limits (4.12.1) |
| `native` | 88 / 3 / 65 | 88 / 5 / 63 | 0 proved, 3 errors, 65 resource limits (5.1.0); 0 proved, 5 errors, 63 resource limits (4.12.1) |
| `candidate` | 159 / 0 / 53 | 149 / 0 / 63 | 15 proved, 0 errors, 53 resource limits (5.1.0); 5 proved, 0 errors, 63 resource limits (4.12.1) |
| `vacuity` | 76 / 52 / 16 | 76 / 50 / 18 | 0 proved, 52 errors, 16 resource limits (5.1.0); 0 proved, 50 errors, 18 resource limits (4.12.1) |
| `fused` | 224 / 0 / 0 | 224 / 0 / 0 | 68 proved, 0 errors, 0 resource limits (5.1.0); 68 proved, 0 errors, 0 resource limits (4.12.1) |
| `fused_vacuity` | 88 / 44 / 24 | 88 / 42 / 26 | 0 proved, 44 errors, 24 resource limits (5.1.0); 0 proved, 42 errors, 26 resource limits (4.12.1) |
| `native_helpers` | 12 / 0 / 0 | 12 / 0 / 0 | 12 proved, 0 errors, 0 resource limits (5.1.0); 12 proved, 0 errors, 0 resource limits (4.12.1) |
| `int_reference` | 212 / 0 / 0 | 212 / 0 / 0 | 68 proved, 0 errors, 0 resource limits (5.1.0); 68 proved, 0 errors, 0 resource limits (4.12.1) |

A proved `assume_false` client is vacuous. A proved `candidate` or `fused` client depends on a trusted contract per operation; it is not a proof of that contract. `int_reference` is a definitional API check, not the sequence-theorem proof. Native proof failures show that this empty-body proof attempt did not establish the true semantic target, not that the target is false.

## Paired baseline and fused client costs

Each cell is **assume-false baseline / fused client**, in resource units. All cells in this table are `Correct`, and their corresponding whole files have exit 0. The baseline is reconstructed from the issue description; it is not the unavailable downstream library's original program. The fused contract combines the operation meaning and bounded cast round trips at the original integer arguments. These are source-API projection costs, not implementation costs.

| Operation | 5.1.0: 8 | 5.1.0: 16 | 5.1.0: 32 | 5.1.0: 64 | 4.12.1: 8 | 4.12.1: 16 | 4.12.1: 32 | 4.12.1: 64 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| AND | 7,826 / 6,773 | 9,497 / 7,779 | 12,866 / 9,797 | 19,613 / 13,835 | 7,826 / 6,773 | 9,497 / 7,779 | 12,866 / 9,797 | 19,613 / 13,835 |
| OR | 7,924 / 6,961 | 9,556 / 7,969 | 12,820 / 9,985 | 19,348 / 14,017 | 7,924 / 6,961 | 9,556 / 7,969 | 12,820 / 9,985 | 19,348 / 14,017 |
| XOR | 7,740 / 6,775 | 9,379 / 7,781 | 12,684 / 9,799 | 19,303 / 13,837 | 7,740 / 6,775 | 9,379 / 7,781 | 12,684 / 9,799 | 19,303 / 13,837 |
| andNot | 8,064 / 7,019 | 9,719 / 8,025 | 13,056 / 10,043 | 19,739 / 14,081 | 8,064 / 7,019 | 9,719 / 8,025 | 13,056 / 10,043 | 19,739 / 14,081 |
| NOT | 4,143 / 3,744 | 4,990 / 4,270 | 6,711 / 5,328 | 10,153 / 7,444 | 4,143 / 3,744 | 4,990 / 4,270 | 6,711 / 5,328 | 10,153 / 7,444 |
| left shift | 6,601 / 5,814 | 7,653 / 6,403 | 9,689 / 7,520 | 13,725 / 9,693 | 6,601 / 5,814 | 7,653 / 6,403 | 9,689 / 7,520 | 13,725 / 9,693 |
| right shift | 6,524 / 5,766 | 7,576 / 6,355 | 9,612 / 7,472 | 13,648 / 9,645 | 6,524 / 5,766 | 7,576 / 6,355 | 9,612 / 7,472 | 13,648 / 9,645 |
| addition | 5,256 / 4,436 | 6,968 / 5,444 | 10,392 / 7,460 | 17,240 / 11,492 | 5,256 / 4,436 | 6,968 / 5,444 | 10,392 / 7,460 | 17,240 / 11,492 |
| subtraction | 5,376 / 4,460 | 7,152 / 5,468 | 10,704 / 7,484 | 17,808 / 11,516 | 5,376 / 4,460 | 7,152 / 5,468 | 10,704 / 7,484 | 17,808 / 11,516 |
| multiplication | 5,767 / 4,578 | 9,062 / 5,592 | 19,519 / 7,626 | 55,802 / 11,696 | 5,817 / 4,578 | 9,400 / 5,592 | 21,201 / 7,626 | 63,244 / 11,696 |
| unsigned division | 6,055 / 4,459 | 10,593 / 5,479 | 25,831 / 7,523 | 80,865 / 11,607 | 6,077 / 4,459 | 10,656 / 5,481 | 25,949 / 7,523 | 81,120 / 11,609 |
| unsigned remainder | 6,117 / 4,459 | 10,701 / 5,475 | 26,013 / 7,507 | 81,213 / 11,571 | 6,139 / 4,459 | 10,755 / 5,475 | 26,131 / 7,507 | 81,459 / 11,571 |
| bit access | 6,301 / 5,529 | 7,205 / 6,118 | 8,917 / 7,235 | 12,245 / 9,408 | 6,301 / 5,529 | 7,205 / 6,118 | 8,917 / 7,235 | 12,245 / 9,408 |
| equality | 4,358 / 4,521 | 5,926 / 6,121 | 9,062 / 9,321 | 15,334 / 15,721 | 4,358 / 4,521 | 5,926 / 6,121 | 9,062 / 9,321 | 15,334 / 15,721 |
| unsigned <= | 4,398 / 4,535 | 5,966 / 6,135 | 9,102 / 9,335 | 15,374 / 15,735 | 4,398 / 4,535 | 5,966 / 6,135 | 9,102 / 9,335 | 15,374 / 15,735 |
| rotate left | 7,441 / 6,231 | 9,123 / 7,239 | 12,517 / 9,255 | 19,367 / 13,287 | 7,541 / 6,247 | 9,545 / 7,271 | 14,317 / 9,319 | 26,929 / 13,415 |
| rotate right | 7,443 / 6,239 | 9,125 / 7,247 | 12,519 / 9,263 | 19,369 / 13,295 | 7,543 / 6,255 | 9,547 / 7,279 | 14,319 / 9,327 | 26,931 / 13,423 |

Across 68 correctness scopes, the baseline/fused totals are 919,685 / 551,032 RU on 5.1.0 and 949,863 / 551,516 RU on 4.12.1. The fused ranges by width are 3,744–7,019, 4,270–8,025, 5,328–10,043, and 7,444–15,735 RU on both solvers. These comparisons do not validate an assumed baseline; substantial baseline work remains in precondition/cast checks before `assume false`.

## Current native attempt and unfused bridge API

Each cell is **native / raw bridge**. `R` means `OutOfResource` at the 200,000-RU cap; the exact count (including setup) is retained in the CSV. `F` followed by a count means `Errors`, without resource exhaustion. A bare count means `Correct`. No native empty-body client proved its target. Raw bridge clients proved 15/68 targets on 5.1.0 and 5/68 on 4.12.1.

| Operation | 5.1.0: 8 | 5.1.0: 16 | 5.1.0: 32 | 5.1.0: 64 | 4.12.1: 8 | 4.12.1: 16 | 4.12.1: 32 | 4.12.1: 64 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| AND | R / 42,819 | R / 81,871 | R / R | R / R | R / R | R / R | R / R | R / R |
| OR | R / 78,002 | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| XOR | R / 124,195 | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| andNot | R / 34,602 | R / 63,654 | R / 182,911 | R / R | R / R | R / R | R / R | R / R |
| NOT | R / 12,699 | R / 23,969 | R / 57,836 | R / 173,699 | R / 48,934 | R / R | R / R | R / R |
| left shift | F25,959 / R | F39,745 / R | F121,307 / R | R / R | F26,153 / R | F39,606 / R | F59,786 / R | F147,162 / R |
| right shift | R / R | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| addition | R / R | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| subtraction | R / R | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| multiplication | R / R | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| unsigned division | R / R | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| unsigned remainder | R / R | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| bit access | R / R | R / R | R / R | R / R | F62,781 / R | R / R | R / R | R / R |
| equality | R / 5,551 | R / 7,215 | R / 10,543 | R / 17,199 | R / 5,541 | R / 7,205 | R / 10,533 | R / 17,189 |
| unsigned <= | R / R | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| rotate left | R / R | R / R | R / R | R / R | R / R | R / R | R / R | R / R |
| rotate right | R / R | R / R | R / R | R / R | R / R | R / R | R / R | R / R |

This is evidence against relying on the raw conversion-heavy API. It supports testing local facts that preserve original source expressions. A frontend implementation and its complete proof obligations are still needed to establish that the proposed emitter achieves the fused projection's costs.

## Pure native helper proofs and integer reference checks

The following native-bitvector helper correctness proofs use no axiom contracts or assumptions. Their outcomes and counts are identical across both solvers and both resolvers.

| Proved identity | 8 | 16 | 32 | 64 |
| --- | ---: | ---: | ---: | ---: |
| `(a \| b) == !(!a & !b)` | 1,950 | 1,950 | 1,950 | 1,950 |
| `(a ^ b) == ((a \| b) & !(a & b))` | 4,234 | 8,938 | 26,921 | 97,268 |
| `(a & !b) == ((a \| b) ^ b)` | 3,846 | 7,914 | 23,538 | 84,686 |

Each integer-reference mode verifies 17 client correctness scopes per width. Their costs are independent of width: 2,994–7,511 RU, totaling 82,796 per width on 5.1.0; 3,002–7,519 RU, totaling 82,920 per width on 4.12.1. All 212 scopes per solver/resolver, including reference-function and client well-formedness, are `Correct`. The arithmetic reference wrappers in this corpus express **bitvector** modulo arithmetic, not a proposal to change ordinary integer-newtype `+`, `-`, or `*`.

## Vacuity controls: exact unresolved cases

All controls call their bridge with concrete arguments and then assert false, leaving the transparent recursive reference definitions present. No false assertion was proved. `Errors` is the intended rejected false assertion; `OutOfResource` is inconclusive and does not pass the vacuity gate.

**Raw bridge:** on 5.1.0, the AND/OR/XOR/andNot control exhausts resources at every width (16 inconclusive, 52 rejected). On 4.12.1 the same 16 controls plus rotate-left and rotate-right at width 64 exhaust resources (18 inconclusive, 50 rejected). All other raw controls are rejected.

**Fused bridge:** on 5.1.0, inconclusive controls are AND/OR/XOR/andNot at every width; remainder at widths 16/32/64; division and multiplication at widths 32/64; subtraction at width 64 (24 inconclusive, 44 rejected). On 4.12.1, add subtraction at width 32 and rotate-right at width 64 (26 inconclusive, 42 rejected). All other fused controls are rejected.

The public finite ground witness run contains 100 SAT cases in each configuration, for 400 observations. Its exact recursive reference values are evaluated at the chosen concrete inputs, and native rotations are expressed by shifts/OR. Counts range from 16 to 40 RU and total 2,416 per configuration. This covers nonzero operands and endpoint shifts/rotations at widths 1 and 3 as well as machine widths. It is a complementary model witness, not a substitute for the inconclusive full-context controls. The production vacuity gate remains open.

The outcome field is authoritative: resource counts may include work beyond the nominal cap, so a count above 200,000 must not automatically be relabeled `OutOfResource`.

## Retained machine data

- `public-client-batches.csv`: every client/control correctness and well-formedness scope, including resource failures.
- `public-all-batches.csv`: every scope and VC, including bridge declarations and reference definitions.
- `public-files.csv`: all invocation exits and generated-source hashes.
- `public-ground-controls.csv`: all 400 observed ground-witness results.
- `public-receipts.json`: source/compiler/solver identities and options.
- `public-resolver-comparison.json`: complete resolver equality check; no differences.

No suite or standard-library performance delta for the proposed production feature is claimed by these data.

## Verified generic sequence correspondence (additional axioms OFF)

A separate [public proof run](https://github.com/erniecohen/dafny/actions/runs/42755593223), benchmark commit `e046a64`, verifies `sequence-model.dfy` on the same pinned compiler with **`--additional-axioms=false`**. All four solver/resolver configurations exit 0 with **17 verified, 0 errors**. Every verification scope is `Correct` and every VC is `Valid`; both resolvers have exactly equal scope/VC outcomes and resource counts on each solver. The source SHA-256 is `e5eeaa8c83e55b65de9374a2ca9990d3faa48ad5b1793f31df8f081a4b950f43`.

This file has no `assume` statement, axiom contract, bodyless declaration, or disabled verification. It defines little-endian `bits(w,a): seq<bool>`, the weighted recursive `value(s)`, and pointwise Boolean operations. Its induction lemma proves, for **arbitrary natural width** and natural operands,

```dafny
value(zip(op, bits(w,a), bits(w,b))) == reference(w,op,a,b)
```

where operation codes 0–3 select AND, OR, XOR, and andNot and `reference` selects the corresponding recursive natural function. The width-specific clients call that proved generic lemma. Thus these costs measure an actual verified sequence/reference correspondence, independently of the trusted native bridge projections above.

The proof uses a 2,000,000-RU cap and 15-second safety timeout, with one prover core, source declaration order, fresh prover processes, and the shared axiom option explicitly OFF. The following costs are correctness scopes; the generic theorem is proved once, not once per width.

| Scope | Z3 5.1.0 RU | Z3 4.12.1 RU |
| --- | ---: | ---: |
| Generic theorem, arbitrary width | 956,774 | 913,863 |
| Width 8 client | 32,721 | 32,497 |
| Width 16 client | 32,725 | 32,497 |
| Width 32 client | 32,725 | 32,497 |
| Width 64 client | 32,725 | 32,497 |

Whole-file totals, including all well-formedness and helper scopes, are **1,344,903 RU** on 5.1.0 and **1,295,842 RU** on 4.12.1. All 68 observed scope/VC results across the four configurations are retained in `sequence-batches.csv`; `sequence-receipts.json` retains exit codes, compiler/solver/source hashes, options and resolver equality.

This establishes the four-operation sequence/reference correspondence. It does not yet prove the full bits/value bijection, arithmetic and rotation library, native bridge, or production option behavior. The raw/fused prototype vacuity limitations reported above remain unchanged.
