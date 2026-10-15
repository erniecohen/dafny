# Maintained obligation proofs

Issue [#170](https://github.com/erniecohen/dafny/issues/170) repairs the eleven examples affected by experimental obligation checking. The source changes retain their logical contracts and resource ceilings. They change proof steps and boundaries; the verifier implementation is unchanged.

## Proof changes

- SiftDown and UnionFind join use method-scoped isolation. Append uses two local boundaries; the power induction uses three boundaries on existing assertions. The supported line also isolates RemoveFactor and the division-order induction at its existing ceiling.
- Schorr-Waite extends a concrete path in the initial heap before pointer reversal. Its independently checked helper supplies the witness for the existential reachability fact. The feature-dependent positive example, missing-edge control, and feasible-heap control remain separate tests.
- The iterator computes its next stack before updating its bookkeeping fields. The executable transition is retained.
- Modular subtraction uses concrete remainder and distributivity lemmas. The quotient/remainder converse uses commutation, the shifted-quotient identity, and the fundamental division identity.
- Base64 padding proofs use local prefix-length facts and existing encoding identities while keeping bitvector definitions hidden. The supported encoder uses independently checked string-composition lemmas. Its smaller existing ceiling also requires separate support/WF proof boundaries and the existing exact-length identity.

## Permanent coverage

The registered regression checks each target and supporting lemma independently, including their WF declarations. It covers the applicable resolver modes, both additional-axiom settings, and obligation checking ON, explicit OFF, and default. Default and OFF must have identical named batch outcomes and resource counts.

The complete-file phase resolves library sources through their ordinary project and selects every declaration in each affected file. It preserves the source limits and options. Each invocation records the actual exit, all batch outcomes, total RU, and maximum batch RU. A completed file may retain its documented negative controls or unrelated resource failures; timeout, signal, driver, and missing-log results fail the coverage check. The ordinary suite and full-library comparisons remain part of required CI.

## Public diagnostic receipts

- [Run 38714425139](https://github.com/erniecohen/dafny/actions/runs/38714425139) records 138 successful selected invocations and exact default/OFF equality.
- [Run 38717833869](https://github.com/erniecohen/dafny/actions/runs/38717833869) records 180 successful selected invocations, the retained Base64 support repairs, exact default/OFF equality, and standing suite/library verdict captures.

These wrappers record actual phase exits and return zero for diagnostics. The proposed head must also pass the real `review.yml` gates. The CI summary from the permanent regression reports the source/compiler/solver digests and numeric resources for every selected and complete-file invocation.

## Dev target costs

The following counts come from run 38717833869 with Z3 5.1.0, using each originally affected resolver/additional-axiom configuration. Each row includes every batch selected for that target.

| target | ON batches | ON total RU | ON maximum RU | OFF batches | OFF total RU | OFF maximum RU |
|---|---:|---:|---:|---:|---:|---:|
| PriorityQueue.SiftDown | 90 | 3,940,456 | 708,974 | 90 | 4,024,006 | 708,974 |
| M2.SchorrWaite | 58 | 4,743,255 | 835,685 | 63 | 8,891,922 | 4,966,200 |
| RemoveFactor | 13 | 172,721 | 39,290 | 13 | 172,272 | 39,290 |
| JoinMaintainsReaches1 | 98 | 9,138,563 | 6,378,062 | 98 | 7,621,010 | 4,878,660 |
| ExtensibleArray.Append | 6 | 8,623,377 | 7,006,646 | 6 | 11,470,834 | 9,876,460 |
| Iterator.MoveNext | 39 | 4,011,521 | 615,328 | 39 | 3,936,650 | 521,995 |
| DivByMultipleIsStronglyOrdered | 2 | 1,363,303 | 1,358,691 | 2 | 704,104 | 699,492 |
| PowStrictlyIncreases | 6 | 351,447 | 71,839 | 6 | 295,583 | 71,597 |
| DecodeValidEncode1Padding | 2 | 133,765 | 81,368 | 2 | 134,850 | 81,368 |
| ModNegNeg | 13 | 51,430 | 5,805 | 13 | 51,405 | 5,805 |
| EncodeBVIsBase64 | 1 | 256,457 | 256,457 | 1 | 258,327 | 258,327 |


## Dev complete-file costs

Compare the ordinary default-OFF suite commands in [baseline run 38173708929](https://github.com/erniecohen/dafny/actions/runs/38173708929) and repair run 38714425139, with additional axioms OFF. Counts include all declarations and retained failures in each file.

| file | baseline batches | repaired batches | baseline total RU | repaired total RU | baseline maximum RU | repaired maximum RU |
|---|---:|---:|---:|---:|---:|---:|
| dafny1/ExtensibleArray.dfy | 9 | 13 | 12,236,107 | 15,024,547 | 8,651,819 | 9,876,460 |
| dafny1/PriorityQueue.dfy | 22 | 110 | 13,150,654 | 6,826,440 | 10,315,363 | 1,538,811 |
| dafny1/SchorrWaite-stages.dfy | 230 | 264 | 76,118,719 | 45,935,183 | 16,032,136 | 16,023,935 |
| dafny2/SnapshotableTrees.dfy | 108 | 107 | 86,173,848 | 83,528,826 | 16,038,578 | 16,038,578 |
| dafny4/Primes.dfy | 27 | 27 | 1,540,218 | 1,540,218 | 512,648 | 512,648 |
| dafny4/UnionFind.dfy | 236 | 344 | 94,424,001 | 87,035,717 | 16,046,113 | 16,046,113 |

Append adds four complete-file batches and increases aggregate cost by 22.8%, from 12,236,107 to 15,024,547 RU. Its largest batch rises from 8,651,819 to 9,876,460 RU, below the unchanged 16,000,000 ceiling. The other changed complete files use less aggregate work in this comparison. More batches therefore do not by themselves establish a lower proof cost.

The remaining recorded suite outcomes are M0 Schorr-Waite and UnionFind Main resource failures, four SnapshotableTrees Insert resource failures, and its intentional concurrent-modification error. The repaired M2, join, and iterator targets pass within those complete-file runs. These outcomes remain explicit in the standing verdict tables.

The library capture changes only the `run Std` verified-batch total from 7838 to 7841 in the four active solver/axiom profiles: Power adds five batches and ModNegNeg removes two. Every declaration verdict stays unchanged. The unrelated JSON baseline resource failure observed in run 38173708929 is retained as separate baseline evidence; its expected declaration verdict is unchanged.
