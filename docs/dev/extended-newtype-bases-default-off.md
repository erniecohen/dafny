# Default-off observations for product4e50e86c

[Run 37361031421](https://github.com/erniecohen/dafny/actions/runs/37361031421) has complete physical capture for the frozen two-arm scope: 38 paired jobs, 2,283 pairs and 4,566 compiler calls. This independently verified receipt covers 39 immutable artifacts (the plan plus every paired job), 59,940 unique logical files and all 20 casefold-colliding BPL members through exact ZIP names and the lossless path mapping. The aggregate artifact is reconciled in the separately frozen addendum below. No engine, replay or source edit was performed during this review.

The comparison reuses the feature-free repaired compiler bbf90260694e2e3004030c35b57f012a98b7c65d and current compiler product 4e50e86cde6ab2ef6cab0762ffc4df59d646dc15. The latter archive records its actual build commit 31f49205d82e73a8db944ef0b9c2be063736d72c and Source tree 9c2bb48a778ee2abd6ef10f8a0d72f7843a16af5, equal to the current product's Source tree. It is not an e050/#165 compiler. Actual binary/component/library and solver hashes, original 4b2742 input hashes, all pre/post guards and retained raw parse results were checked without mismatches. The harness commit is 56e2d6141bcb8c1d73622df13145294b178f17a3.

| Captured comparison | Pairs | Verdict differences | Warning differences | Resource/batch differences | Normalized BPL differences |
| --- | ---: | ---: | ---: | ---: | ---: |
| Canonical, actual AX-off | 1,081 | 0 | 0 | 0 | 0 |
| Canonical, actual AX-on | 1,081 | 0 | 0 | 0 | 0 |
| Seven Std projects, two solvers, two AX settings | 28 | 0 | 0 | 4 | 0 |
| Separate original #82 literal routes | 93 | 0 | 0 | 0 | 0 |
| Total | 2,283 | 0 | 0 | 4 | 0 |

These are paired observation outcomes, not an overall-green result. The original sources, literal commands and expected-output bytes were retained. Every original #82 verification route was observed. The reads-positive source still has 13 verified/2 errors at lines 209 and 233 on all four AX/resolver routes in both arms. Its four original expected-zero exit checks and four OutputChecks fail per arm. Intermediate-callback negatives reject at lines 17 and 18 instead of only 18; fresh-reference negatives reject at lines 21 and 35 instead of only 35. These contribute another eight failed original OutputChecks per arm. Their intended negative assertions still fail, while the extra valid-prefix failures remain conservative completeness losses. Shard0 reports observations_complete=true and complete=false; shard1 reports both true. No original oracle was waived.

Whole Std itself remains unproved in both arms: 5.1 AX-off gives 7,582 verified/4 errors/26 resource caps; 5.1 AX-on gives 7,585/4/23; reference AX-off gives 7,610/2; reference AX-on gives 7,609/2/1. All four whole-Std commands exit4. All six TargetSpecific projects pass without warnings in every paired solver/AX setting. Matched caps and failed proofs do not become accepted by comparison.

| Whole Std profile | Baseline RU | Current RU | Delta | Changed declarations / VCs |
| --- | ---: | ---: | ---: | ---: |
| Z3 5.1, AX-on | 1,340,603,210 | 1,340,788,961 | +185,751 | 251 / 1,871 |
| Z3 5.1, AX-off | 1,429,584,800 | 1,429,584,810 | +10 | 1 / 1 |
| Z3 4.12.1 reference, AX-on | 1,019,702,262 | 1,019,703,097 | +835 | 57 / 674 |
| Z3 4.12.1 reference, AX-off | 1,026,263,901 | 1,026,388,511 | +124,610 | 82 / 1,270 |

All 85 raw BPL files are byte-identical in each of these four pairs, before normalization. Every exact declaration name, VC number, assertion sequence and outcome matches. The raw JSON declaration array positions differ at 171/158/192/292 positions respectively, and within-declaration VC logging order differs in 112/112/114/121 declarations. Those raw orders are retained in [four-Std-movements.json](extended-newtype-bases-default-off-data/four-Std-movements.json). Matching is by the unique exact declaration name and VC number; logger array order does not establish solver submission or session order.

The 5.1 AX-off movement is only MulInternalsNonlinear.LemmaMulIsAssociative correctness VC1: 20,554 to 20,564 RU, at MulInternalsNonlinear.dfy:26:25. The largest 5.1 AX-on movement is JSON.ZeroCopy.Serializer.NumberHelper2 correctness VC88: 187,368 to 337,472 (+150,104), at Serializer.dfy:191:64. The largest reference AX-off movement is its VC143: 160,789 to 382,585 (+221,796), at Serializer.dfy:179:64, partly offset by its VC104 decrease of 70,472. The reference AX-on movement includes 670 VCs increasing by one RU, plus four other changed VCs; its largest change is Deserializer.Core.WS well-formedness VC8, +155.

The actual paired command suffixes are identical: verify src/Std/dfyconfig.toml, the pinned solver, cores4, verification-time-limit0, the unchanged AX setting and observer CSV/JSON/BPL destinations. Every pair ran baseline then current. The original project still supplies resource-limit1,000,000 and its original refresh/traits/general-newtypes settings; the CLI time-limit0 overrides the project's 300-second limit. No seed option was added. Actual JSON VC rows omit randomSeed, CSV rows contain 0, and the artifacts contain no SMT session logs or explicit BPL seed setting. Therefore CSV0 is not proof of actual solver seed provenance, and byte-identical BPL is not proof of identical ordered SMT queries or prior solver context.

All four resource movements remain unexplained default-off compatibility blockers. No tolerance, waiver or nondeterminism conclusion is applied. The smallest initial follow-up is to repeat the exact whole-Std baseline/current pair once for each of these four solver/AX profiles (eight additional physical calls), retaining the same corpus/project, parameters, baseline-current order and fresh process boundaries. A transparent solver-protocol observer must retain complete ordered commands, source VC identities where available, explicit seed settings, and each session's complete prior prefix while forwarding original arguments and bytes to the exact pinned binary. This is proposed only, not executed or approved acceptance. Same-arm variation with the same complete ordered context would establish observed nonrepeatability; changed context would instead identify a context/order explanation requiring further investigation. Neither result alone waives the gate.

The collector's 32 raw incomplete pairs are preserved: 15 declaration-only/include-only or empty selected-root inputs per AX setting, plus the intended malformed-logger CLI rejection per AX setting. They represent 16 unique original sources. Included proof bodies were not requested by the root-only commands. The comment-only C++ array driver and external-C# driver do not establish their separate original compilation contracts. The include48 missing-trigger warning remains expected compatibility evidence outside warning-free proof acceptance. Missing BPL is never treated as solver-query equality.

SchorrWaite's literal --bprint destination is now actually observed: both AX settings and both arms have 272 verified/0 errors and the same fresh 418,440-byte raw BPL hash 26e6597f60935b0dbdcdd7d9c4553f0e407308af34a4db2a99fe9aeba0ae6b21. The historical missing-Schorr observation is not retained as an incomplete current pair; no proof flag, budget or oracle changed. SchorrWaite-stages still has the matched resource-cap outcome and remains unproved.

Machine evidence is split into the complete physical and paired ledgers, exact casefold mapping ledger, four-Std-movements.json and four-Std-batch-vectors.tsv, incomplete-source-scope-supplement.json, and literal-original-oracle-boundaries.json. The separately frozen [primary receipt](extended-newtype-bases-default-off-data/frozen-primary-39-receipt.json) binds its audit-file hashes and all 39 exact archive receipts. The separately reviewed aggregate addendum preserves those raw observations and boundaries. Audit ledgers outside the linked publication subset are identified by hash in that receipt; their bytes are not included here.

## Aggregate addendum

Aggregate artifact 11368665245 (full-default-off-report) is independently bound to run 37361031421 and head 56e2d614. Its API digest and size, all eight exact ZIP members, lossless physical mapping and raw step exits were checked. All 2,190 canonical/Std pairs reconcile case by case with the frozen primary 39-artifact receipt. The separate 93 literal pairs and all 38 job denominators give 2,283 pairs and 4,566 calls.

The raw aggregate complete_observations=false and closure raw_observation_closure_complete=false remain unchanged. Eight canonical shards are listed as incomplete because they retain 32 incomplete selected-root/intentional-CLI pairs. The aggregate also retains separate #82 literal reads-oracle failures. These fields do not indicate an absent paired job: all physical calls are present. They still prevent a green or accepted observation-closure claim under the collector's original policy. Zero diagnostic step exits and successful Actions orchestration do not accept the proofs or four unexplained Std resource movements. The primary 39-artifact receipt and its raw flags are not rewritten.

## Published evidence

- [frozen-primary-39-receipt.json](extended-newtype-bases-default-off-data/frozen-primary-39-receipt.json): `c4aa419f3dbc51648777153355b623a69f2ea4bdc7c485d29335d1ab7e2970cf`.
- [four-Std-movements.json](extended-newtype-bases-default-off-data/four-Std-movements.json): `a3dd40c418bae775b6f556c3097dbc013635572960da0add5ab36dffb9ff184a`.
- [four-Std-batch-vectors.tsv](extended-newtype-bases-default-off-data/four-Std-batch-vectors.tsv): `f2dc7fd29c2360d80185e4cced8f97460b51cf55f5796c95fdcad0f58ad73ed4`.
- [incomplete-source-scope-supplement.json](extended-newtype-bases-default-off-data/incomplete-source-scope-supplement.json): `9922ae6cd8b57629a3c9ec0e718e8e153eaa78f2f234440a0c8119a955e0e861`.
- [literal-original-oracle-boundaries.json](extended-newtype-bases-default-off-data/literal-original-oracle-boundaries.json): `a0e44efe900b81131ea7113a664007b6fc9b004d340124c53e00bd8b2d49e922`.
- [aggregate-addendum.json](extended-newtype-bases-default-off-data/aggregate-addendum.json): `ea59a707ba91c2f59026e2b9c47043b93f9368c85c96eac884514f3316558c3e`.

The original raw ZIPs are the artifacts of the linked public run. [Publication receipt](extended-newtype-bases-default-off-data/publication-receipt.json) binds the files above. This report describes the frozen4e50/compiler-c9 comparison; it does not validate a later shipping source, the separate callback proposal, or the cast experiment.
