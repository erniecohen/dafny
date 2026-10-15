# Maintained obligation proofs

[#170](https://github.com/erniecohen/dafny/issues/170) stabilizes the eleven documented proof cases for experimental obligation checking. The changes retain their mathematical contracts, assertions, reveals, existing resource attributes, and intentional controls. The verifier and axiom implementation are unchanged.

## Proof changes

- SiftDown and UnionFind join use method-scoped isolation. Append uses two local boundaries, and the power induction marks three existing assertions. The supported line also isolates RemoveFactor and division-order induction.
- Schorr-Waite constructs a concrete initial-heap path before pointer reversal and keeps the child/path witnesses explicit across the update. Its helper is checked independently.
- The iterator computes the read-only next stack before assigning its bookkeeping fields. The original increment, assertions, reveals, and executable transition are retained.
- ModNeg keeps both original assertions and proves the integer-multiple fact pointwise with concrete remainder lemmas. The quotient/remainder converse uses commutation, shifted quotient, and fundamental division identities.
- Base64 padding proofs hide bitvector definitions while using local prefix facts and existing encoding identities. The supported encoder adds two proved string-composition lemmas. Its smaller default library limit also needs scoped support/WF checks and the existing exact-length identity; its original bounds calls remain.

## Coverage and evidence

Six registered parts check every selected target/helper and WF declaration, the applicable resolvers, both additional-axiom settings, and checking ON/OFF/default. Each default/OFF pair stays in one part and must have identical named batch outcomes and RU vectors. Each matrix includes 60 complete-file scopes; library files resolve through their ordinary project. Full unfiltered library runs provide additional closure evidence.

| line | completed scopes | selected proofs/WF | default/OFF | public matrix |
|---|---:|---|---|---|
| Dev | 288 | all Correct | exact | [run 38731950683](https://github.com/erniecohen/dafny/actions/runs/38731950683) |
| Supported | 300 | all Correct | exact | [run 38732284284](https://github.com/erniecohen/dafny/actions/runs/38732284284) |

The parts have a twelve-minute safety deadline with process-group cleanup, earlier than the existing harness's fifteen-minute wait. They discard stale receipts and require the entire planned partition to finish. [#176](https://github.com/erniecohen/dafny/issues/176) reports the unchecked harness wait separately; the earlier single-test pass is insufficient completion evidence. The partition/deadline guards were checked independently. Required exact-head fork CI must complete every part.

The supported in-tree driver omits assembly-version generation and reports `0.0.0.0`; its source and compiler-core digest identify that diagnostic build. The release-wrapper build checks its proper Dafny version in required CI. All table measurements below come from the linked public CI with Z3 5.1.0. Each count includes every selected batch, including WF checks. `legacy`/`refresh` names the resolver; the final OFF/ON names additional axioms. Original project and source-specific ceilings remain in force. Some unchanged declarations have larger or unlimited per-declaration budgets; file maxima include them.

## Dev target costs

| target / configuration | ON batches | ON total RU | ON maximum | OFF batches | OFF total RU | OFF maximum |
|---|---:|---:|---:|---:|---:|---:|
| SiftDown / legacy/OFF | 90 | 3,940,456 | 708,974 | 90 | 4,024,006 | 708,974 |
| M2.SchorrWaite / legacy/OFF | 69 | 4,921,676 | 557,811 | 74 | 5,261,365 | 762,388 |
| RemoveFactor / legacy/OFF | 13 | 172,721 | 39,290 | 13 | 172,272 | 39,290 |
| JoinMaintainsReaches1 / legacy/OFF | 98 | 9,138,563 | 6,378,062 | 98 | 7,621,010 | 4,878,660 |
| Append / refresh/OFF | 6 | 8,623,377 | 7,006,646 | 6 | 11,470,834 | 9,876,460 |
| MoveNext / refresh/OFF | 39 | 4,011,521 | 615,328 | 39 | 3,936,650 | 521,995 |
| DivByMultipleIsStronglyOrdered / refresh/OFF | 2 | 1,363,303 | 1,358,691 | 2 | 704,104 | 699,492 |
| PowStrictlyIncreases / refresh/OFF | 6 | 351,447 | 71,839 | 6 | 295,583 | 71,597 |
| DecodeValidEncode1Padding / refresh/OFF | 2 | 133,765 | 81,368 | 2 | 134,850 | 81,368 |
| ModNegNeg / refresh/ON | 15 | 286,450 | 41,745 | 15 | 286,438 | 41,745 |
| EncodeBVIsBase64 / refresh/ON | 1 | 256,457 | 256,457 | 1 | 258,327 | 258,327 |

## Dev complete-file costs

These representative file/configuration rows match the originally affected cases. The public artifact records every complete-file configuration and all outcomes. Exit 4 can include the retained negative control or named non-target resource failures listed below; selected #170 targets/helpers and every recorded WF declaration are Correct.

| file / configuration | exits ON/OFF | ON batches | ON total RU | ON maximum | OFF batches | OFF total RU | OFF maximum |
|---|---|---:|---:|---:|---:|---:|---:|
| PriorityQueue.dfy / legacy/OFF | 0/0 | 110 | 7,308,597 | 2,033,963 | 110 | 6,826,440 | 1,538,811 |
| SchorrWaite-stages.dfy / legacy/OFF | 0/4 | 260 | 32,131,319 | 12,025,559 | 270 | 37,125,024 | 16,023,931 |
| Primes.dfy / legacy/OFF | 0/0 | 27 | 22,316,986 | 14,608,072 | 27 | 1,540,218 | 512,648 |
| UnionFind.dfy / legacy/OFF | 4/4 | 325 | 108,698,094 | 16,048,019 | 325 | 105,761,715 | 16,046,113 |
| ExtensibleArray.dfy / refresh/OFF | 0/0 | 13 | 12,810,243 | 7,006,646 | 13 | 15,024,547 | 9,876,460 |
| SnapshotableTrees.dfy / refresh/OFF | 4/4 | 107 | 80,470,551 | 16,038,800 | 107 | 83,528,826 | 16,038,578 |
| DivMod.dfy / refresh/OFF | 0/0 | 297 | 13,790,011 | 3,289,811 | 297 | 9,490,688 | 1,949,021 |
| DivMod.dfy / refresh/ON | 0/0 | 297 | 13,017,001 | 2,045,839 | 297 | 9,480,369 | 1,949,045 |
| Power.dfy / refresh/OFF | 0/0 | 68 | 1,123,268 | 71,839 | 68 | 1,016,931 | 71,597 |
| Base64.dfy / refresh/OFF | 0/0 | 479 | 136,730,127 | 85,885,985 | 479 | 134,231,351 | 85,885,942 |
| Base64.dfy / refresh/ON | 0/0 | 479 | 47,476,169 | 15,219,088 | 479 | 38,161,917 | 5,884,002 |

## Supported target costs

| target / configuration | ON batches | ON total RU | ON maximum | OFF batches | OFF total RU | OFF maximum |
|---|---:|---:|---:|---:|---:|---:|
| SiftDown / legacy/OFF | 90 | 3,940,456 | 708,974 | 90 | 4,024,006 | 708,974 |
| M2.SchorrWaite / legacy/OFF | 1 | 7,204,327 | 7,204,327 | 1 | 2,043,835 | 2,043,835 |
| RemoveFactor / legacy/OFF | 13 | 172,721 | 39,290 | 13 | 172,272 | 39,290 |
| JoinMaintainsReaches1 / legacy/OFF | 98 | 9,138,563 | 6,378,062 | 98 | 7,621,010 | 4,878,660 |
| Append / refresh/OFF | 6 | 8,623,377 | 7,006,646 | 6 | 11,470,834 | 9,876,460 |
| MoveNext / refresh/OFF | 39 | 4,011,521 | 615,328 | 39 | 3,936,650 | 521,995 |
| DivByMultipleIsStronglyOrdered / refresh/OFF | 11 | 644,744 | 403,676 | 11 | 461,749 | 276,138 |
| PowStrictlyIncreases / refresh/OFF | 6 | 351,447 | 71,839 | 6 | 295,583 | 71,597 |
| DecodeValidEncode1Padding / refresh/OFF | 2 | 133,742 | 81,368 | 2 | 135,586 | 81,368 |
| ModNegNeg / refresh/ON | 15 | 286,450 | 41,745 | 15 | 286,438 | 41,745 |
| EncodeBVIsBase64 / refresh/ON | 1 | 46,895 | 46,895 | 1 | 45,108 | 45,108 |

## Supported complete-file costs

These representative file/configuration rows match the originally affected cases. The public artifact records every complete-file configuration and all outcomes. Exit 4 can include the retained negative control or named non-target resource failures listed below; selected #170 targets/helpers and every recorded WF declaration are Correct.

| file / configuration | exits ON/OFF | ON batches | ON total RU | ON maximum | OFF batches | OFF total RU | OFF maximum |
|---|---|---:|---:|---:|---:|---:|---:|
| PriorityQueue.dfy / legacy/OFF | 0/0 | 110 | 7,308,597 | 2,033,963 | 110 | 6,826,440 | 1,538,811 |
| SchorrWaite-stages.dfy / legacy/OFF | 4/4 | 9 | 28,347,320 | 16,035,619 | 9 | 22,662,887 | 16,035,093 |
| Primes.dfy / legacy/OFF | 0/0 | 27 | 22,316,986 | 14,608,072 | 27 | 1,540,218 | 512,648 |
| UnionFind.dfy / legacy/OFF | 4/4 | 142 | 64,302,398 | 16,048,151 | 142 | 59,282,612 | 16,046,427 |
| ExtensibleArray.dfy / refresh/OFF | 0/0 | 13 | 12,810,243 | 7,006,646 | 13 | 15,024,547 | 9,876,460 |
| SnapshotableTrees.dfy / refresh/OFF | 4/4 | 107 | 80,470,551 | 16,038,800 | 107 | 83,528,826 | 16,038,578 |
| DivMod.dfy / refresh/OFF | 4/4 | 306 | 10,278,625 | 1,032,365 | 306 | 8,800,238 | 1,031,247 |
| DivMod.dfy / refresh/ON | 4/4 | 306 | 11,322,707 | 1,032,379 | 306 | 8,787,432 | 1,031,251 |
| Power.dfy / refresh/OFF | 4/4 | 68 | 2,136,683 | 1,023,859 | 68 | 2,030,432 | 1,023,021 |
| Base64.dfy / refresh/OFF | 0/0 | 594 | 120,587,216 | 85,885,985 | 594 | 130,300,389 | 85,885,942 |
| Base64.dfy / refresh/ON | 0/0 | 594 | 34,058,214 | 1,699,027 | 594 | 33,970,956 | 1,698,961 |

## Cost changes and ordinary baselines

The [dev baseline](https://github.com/erniecohen/dafny/actions/runs/38173708929) and [first repair capture](https://github.com/erniecohen/dafny/actions/runs/38714425139) record the ordinary default-OFF suite comparison. Append grows from 9 to 13 batches and 12,236,107 to 15,024,547 total RU, a 22.8% increase. Its maximum grows from 8,651,819 to 9,876,460 RU under the unchanged 16,000,000 ceiling. More batches alone do not establish lower aggregate cost.

Keeping both original ModNeg assertions increases affected-configuration OFF work from the first portable calculation's 51,405 RU to 286,438 RU, with 15 batches and a 41,745 maximum. [Run 38730717076](https://github.com/erniecohen/dafny/actions/runs/38730717076) proves twelve selected and eight complete-DivMod profiles across both solvers. All four ordinary dev default-OFF library profiles finish with 7,843 verified and zero errors; no declaration verdict changes.

For the supported line, [run 38732344105](https://github.com/erniecohen/dafny/actions/runs/38732344105) records eight complete reference-solver Schorr-Waite profiles with 9 verified/zero errors, the twenty ModNeg/complete-DivMod profiles, and the four ordinary library captures. Default-OFF Z3 4.12.1 has 7,746 verified/zero errors in both axiom modes. Z3 5.1.0 has 7,728 verified/2 errors/16 resource failures without additional axioms and 7,730/2/14 with them. All remaining declaration outcomes are unchanged from the [published baseline](https://github.com/erniecohen/dafny/actions/runs/38047092869). The 134 additional batches are DivMod +9, Power +5, and Base64 +120. Each expectation commit explains every changed row.

## Remaining configuration limits

- Both suite lines retain the M0 Schorr-Waite and UnionFind Main resource failures. The supported suite also retains its higher-level UnionFind Join resource failure. SnapshotableTrees retains its intentional concurrent-modification error and four Insert resource failures. The repaired M2, join-helper, and iterator proofs are Correct in those completed files.
- At the supported 5.1.0 default library ceiling, the unchanged MultiplyDivideLe/Lt/Hoist bodies retain resource failures, and PowIncreases does so without additional axioms. Their outcomes remain explicit; they are separate from the repaired targets.
- The unfiltered ON captures are [dev](https://github.com/erniecohen/dafny/actions/runs/38734539649) and [supported](https://github.com/erniecohen/dafny/actions/runs/38734601782). Every #170 library target/helper is Correct in all four profiles per line. Dev Std has 7,874 verified plus Producer.Invoke resource exhaustion with 5.1.0, and 7,875 verified/zero errors with 4.16.0. Two dev target-specific parts have 132 verified/zero proof errors but exit 2 on unnecessary-requires warnings; the warning policy is retained.
- Additional supported 4.12.1/ON checks report an unproved MultiplyDivideLe postcondition, plus other non-target resource failures. The original-source ON outcome for that reference solver has not been measured here, so no baseline attribution is claimed. The unfiltered capture lists every remaining error/resource outcome.
- The unrelated dev default-OFF JSON baseline resource failure observed in run 38173708929 is retained as baseline evidence; its expected declaration verdict is unchanged.
- [Original-source ordinary profiles](https://github.com/erniecohen/dafny/actions/runs/38741741226) reproduce dev UnionFind Main resource exhaustion at the existing 50-million reference-solver ceiling in both resolvers, plus Snapshot Main's 300-second timeout and four Insert resource failures. The [current ordinary test capture](https://github.com/erniecohen/dafny/actions/runs/38737223047) preserves those outcomes and removes the old iterator precondition failure. The pre-existing dev UnionFind runtime test remains outside the registered gate; its full-file/target coverage is explicit in the six-part regression.
- The [supported ordinary capture](https://github.com/erniecohen/dafny/actions/runs/38739670603) genuinely records all six test cases. Its only failures are the four obsolete batch-count goldens, which are updated separately; UnionFind and Snapshot's existing exit-4 test pass. The disabled Snapshot golden comparison is preserved on both lines.

The feature-dependent Schorr-Waite positive fixture, independent definitions/WF checks, missing-edge control, and feasible-original-heap control remain unchanged and separate from the full-algorithm witness repair. No new axiom, unchecked premise, contract weakening, solver fallback, or resource-limit increase is included.
