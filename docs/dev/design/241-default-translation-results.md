# Default translation qualification before proof repair

This cohort compares shipped commit https://github.com/erniecohen/dafny/commit/7dc1d91b1159bafb2d75dfb092930112528e6c18 with default producer https://github.com/erniecohen/dafny/commit/1dbf44071689b525046d97ceba9ccd5f8344e396. Subsequent moved-proof repairs require new library qualification; these observations retain their original producer identity.

The population contains 1,172 suite programs and seven standard-library parts in each additional-axiom profile: 2,358 cases and 31,564 task observations per mode. Both profiles retain their native resource limits and options. There are no abnormal process exits or task-population changes. The standing suite plan has twelve CLI-rejected invocations per profile, including missing print arguments and unsupported shell-redirection tokens. Those 24 observations cover CLI diagnostics and contribute no proof tasks; they are not verification evidence. Corrected native verification supplements subsequently pass on the integrated ownership producer, as recorded below; the original CLI observations remain diagnostic coverage only.

The default changes cost in 198 cases and 5,601 task observations. Every changed-cost task has both a changed containing Boogie module and a changed captured prepared-command prefix. The complete per-task old/new RU, delta and input-change witnesses are in [the cost ledger](241-default-translation-costs.tsv). Total RU is 5,579,024,819 before and 5,533,278,471 after (delta -45,746,348). Invalid observations remain 3,486; Valid changes from 28,017 to 28,015 and OutOfResource from 61 to 63. These are observations across the two profiles, not unique declarations.

The changes include module-qualified local names, dependency-local intrinsic catalogs, dependency-depth heights and stable declaration/conjunct order. This is not an isolated alpha-renaming experiment. The Boogie witnesses establish that no cost/outcome move occurs with unchanged translated text; they do not isolate the contribution of each text transformation.

The captured prefix descriptors include prover-interface calls and problem names; their hashes are not complete native-wire hashes. In 1,262 changed-cost task observations, the implementation/problem name also changes under the module-qualified Base64 name. The ledger labels these witnesses as prepared-command prefixes. Complete native input-writer equality remains a separate gate below.

Every outcome change in this cohort is listed below. VC numbers are one-based report numbers; solver split indices are zero-based.

| Profile / input | Declaration | VC | Shipped outcome / RU | Default outcome / RU |
|---|---|---:|---|---|
| `std-0/Std` | `Std.Base64.DecodeValidPartialsFrom2PaddedSeq (well-formedness)` | 1 | OutOfResource / 1058478 | Valid / 814545 |
| `std-0/Std` | `Std.BulkActions.BatchReader.Read (correctness)` | 41 | Valid / 817799 | OutOfResource / 1118886 |
| `std-0/Std` | `Std.JSON.ZeroCopy.Deserializer.Core.TryStructural (well-formedness)` | 1 | Valid / 62855306 | OutOfResource / 100105061 |
| `std-0/Std` | `Std.Producers.ConcatenatedProducer.Invoke (correctness)` | 273 | OutOfResource / 100186692 | Valid / 74995309 |
| `std-1/Std` | `Std.BulkActions.BatchReader.Read (correctness)` | 41 | Valid / 837048 | OutOfResource / 1119336 |
| `std-1/Std` | `Std.JSON.ZeroCopy.Deserializer.Core.TryStructural (well-formedness)` | 1 | Valid / 62855306 | OutOfResource / 100105061 |
| `std-1/Std` | `Std.JSON.ZeroCopy.Deserializer.Sequences.Elements (well-formedness)` | 150 | OutOfResource / 1250541 | Valid / 1172918 |
| `suite-1/dafny2/MinWindowMax.dfy` | `MinimumWindowMax (correctness)` | 122 | Valid / 34262063 | OutOfResource / 100020029 |

The same cohort also compares the prerequisite with translation-table OFF, OFF with enabled-cold, and enabled-cold with enabled-warm. All 2,358 cases match in Boogie, prepared solver-command prefixes, complete untimed reports, outcomes/RU, stdout/stderr and exit. This does not yet establish complete raw-transport identity: close-only recording omits the last idle physical solver in some runs, including two cold/warm observations. The separate exact native input-writer audit, including every physical solver and its terminal handshake, remains required.

`EncodeBVIsBase64` also exhausts its bound in the obligation gate's filtered invocation, which is a different preparation context from this complete-library cohort. The assertion-isolation repair preserves its contract and proof body; the subsequent library cohort must account for its changed assertion-batch population.

The owner's current push policy requires expected verdict files to be generated locally with the pinned standing gate, before a single validated push. Historical artifact-derived cohorts below retain their original provenance; current local refreshes are committed separately with their reasons.

## Historical Std gating artifacts after the first two proof repairs

This separate cohort compares shipped producer https://github.com/erniecohen/dafny/commit/7dc1d91b1159bafb2d75dfb092930112528e6c18 from https://github.com/erniecohen/dafny/actions/runs/43233144466 with default producer https://github.com/erniecohen/dafny/commit/181707046beef94b7d7367a0cf5822ed65c2fa84 from https://github.com/erniecohen/dafny/actions/runs/43267649021. These are matching standing CI options, including four verification cores; they do not replace the earlier single-core cohort. Expected rows were taken from the failed current artifacts in a separate explained commit.

Both retained proof repairs pass in all four profiles. Assertion isolation adds 32 batches to `EncodeBVIsBase64` and three to `TryStructural`, preserving their contracts, bodies and bounds. Every declaration outcome change in this current cohort is listed below; RU here is the declaration aggregate, not a per-batch bound.

| Solver / axioms | Declaration | Shipped outcome / RU | Default outcome / RU |
|---|---|---|---|
| 5.1.0 / off | `Std.BulkActions.BatchReader.Read (correctness)` | Correct / 11800387 | OutOfResource / 12094105 |
| 5.1.0 / on | `Std.BulkActions.BatchReader.Read (correctness)` | Correct / 11852466 | OutOfResource / 12130234 |
| 5.1.0 / on | `Std.JSON.ZeroCopy.Deserializer.Sequences.Elements (well-formedness)` | OutOfResource / 77407063 | Correct / 77320476 |
| 4.12.1 / off | `Std.Base64.Encode2PaddingIs2Padding (correctness)` | Correct / 637472 | OutOfResource / 1025435 |

There are no changed declaration keys or logical-error outcomes. The `Read` failing batch uses 1118886 RU with axioms off and 1119336 with axioms on; both retain their existing limit. The `Encode2PaddingIs2Padding` proof retains its existing 1000000 RU limit. Z3 4.12.1 with axioms on has no declaration outcome changes. Std summary changes also count the 35 additional isolation batches. Target-specific declaration verdicts are unchanged.

| Solver / axioms | Declarations with changed cost | Shipped total RU | Default total RU | Delta RU |
|---|---:|---:|---:|---:|
| 5.1.0 / off | 368 | 1537571323 | 1351249929 | -186321394 |
| 5.1.0 / on | 368 | 1198380562 | 1131968325 | -66412237 |
| 4.12.1 / off | 377 | 1078590955 | 1052685034 | -25905921 |
| 4.12.1 / on | 363 | 1026098165 | 1027584028 | +1485863 |

The complete declaration aggregates and batch counts are in [the current Std CI cost ledger](241-default-translation-std-ci-costs.tsv). Isolation changes batch shape for the two repaired proofs, so these aggregates do not pair their old and new VC numbers. These artifacts establish verdict and cost changes for their stated producer; they do not capture native SMT byte hashes. Complete transport identity between the prerequisite and translation-table modes remains pending.


### Post-integration focused proof and display repairs

The regression failure at https://github.com/erniecohen/dafny/actions/runs/43302337128/job/131476681653 reproduces locally against the integrated default compiler. With consistent-obligation checking enabled, `issue100-original.wfi` moves from Correct at 41,618 RU in shipped `8deca8ad` to OutOfResource at 16,033,149 RU in all four legacy/refreshed and additional-axiom combinations. The existing 16-million limit is unchanged. A CLI assertion-isolation probe verifies its five correctness batches at 153,835 total RU. The retained repair annotates only `wfi`, preserving its body and contract; the complete registered assertion-invariance shard then passes, including the original/explicit/final-true variants, old-heap and guarded-argument checks, sequential path and false controls, and project-option precedence. Probe cost values describe the explicit probe; the full registered-shard result is separate evidence.

The filtered Base64 scope in https://github.com/erniecohen/dafny/actions/runs/43302341109/job/131476524557 also exposes three moves, all Correct in the corresponding shipped artifact at https://github.com/erniecohen/dafny/actions/runs/43233144216:

| Declaration | Obligation checking | Integrated outcome / RU |
|---|---|---|
| `DecodeValidPartialsFrom1PaddedSeq (well-formedness)` | off and on | OutOfResource / 1055894 |
| `EncodeBVLengthExact (correctness)` | off | OutOfResource / 1054802 |
| `EncodeDecodeValid (correctness)` | on | OutOfResource / 1023215 |

The retained `DecodeValidPartialsFrom1PaddedSeq` repair adds only `{:isolate_assertions}`. In the complete filtered Base64 scope, both obligation-checking modes verify all 17 well-formedness batches (1,316,029 total RU, maximum 503,870) and seven correctness batches (350,014 RU off; 358,359 on). The one-million per-task limit is unchanged. A separate reachable `assert false` produces Errors in both modes while all 17 well-formedness batches remain Valid. This changes two batches to 24, adding 22 batches. The other two moved correctness verdicts remain recorded; an isolation probe for `EncodeBVLengthExact` still exhausts a batch and was discarded. These filtered-scope observations do not replace the standing whole-Std four-core cohort above.

`BitvectorsMore` retains its 9 verified / 41 error verdict and diagnostics in both resolver modes. Its resolved-program display now sorts the canonical width inventory and includes the generated helper width `bv4`; the refreshed global system display no longer duplicates the module-owned rotation helper declarations. Both snapshots are refreshed from the local native outputs. The labeled-refinement trigger check still requires `$Heap_at_0`, the current heap, receiver and assign-such-that binder in order, while accepting the module-qualified binder name and resulting line wrapping.

The earlier operator export-view regression is closed by the worker-owned builtin-declaration identity repair from https://github.com/erniecohen/dafny/pull/263, integrated at worker head `32d19ce06dcfb296d0c3c32eb3ae2ac3c388b372`. Both integrated binaries verify the revealed witness and preserve the intended hidden-member/opaque-rank diagnostics at thread settings 0/1/2/4/8. No duplicate resolver-site fix is implemented here. Eleven rejected legacy-Std observations reproduce the shipped 19 ordinary diagnostics and exit 2, without an abort. Twelve editor-cache tests preserve unchanged-dependency reuse.


## Integrated ownership qualification and numeric normalization

The integrated table producer `3f70580309ccc64562816973a4e456164b6f99a4` passes the full-library growth control: all 93 unaffected module texts are byte-identical after an unrelated module grows from one to 64 functions and translation order reverses. Checksums and model-view are enabled, with zero solver calls or version probes. Its corrected CLI supplement covers 24 native verifier invocations, 24 replay comparisons and the original 24 malformed CLI observations, with zero unexplained differences. Compiler and stdin-I/O semantics are outside this supplement.

Two earlier complete full-Std native-stream comparisons also pass under their recorded explicit `/deterministicLiteralHashes:1`, `/restartProver`, one-core profiles, each accounting for 7,751 physical solvers in all six compared modes. These retain their earlier pre-Decode1-isolation producer; they are not acceptance evidence for the subsequent source changes. Suite invocations in that cohort retain their per-case arguments, rather than receiving these Std-only overrides.

An ordinary shipped-against-itself negative control on the newer 7,773-task Std population exposes pre-existing process-random numeric declaration order. Identical printed Boogie produces 247 task outcome/RU differences between two independent shipped executions, including these outcome moves:

| Declaration / VC | First outcome / RU | Second outcome / RU |
|---|---|---|
| `Std.JSON.ZeroCopy.Deserializer.Arrays.BracketedToArray (correctness)` / 8 | Valid / 1084177 | OutOfResource / 1156506 |
| `Std.JSON.ZeroCopy.Deserializer.Objects.BracketedToObject (correctness)` / 6 | Valid / 507173 | OutOfResource / 1156506 |

The passive native writer records the first differing byte at offset 167,273,645, in generated numeric declarations. Pinned Boogie's `Checker.GetReorderedDeclarations` selects `ContentHash` by default; its large numeric literals use CLR `BigInteger.GetHashCode`, which has a process-random seed. Boogie's existing `DeterministicLiteralHashes` path uses content-derived numeric hashes instead. The runtime implementation is visible in [BigInteger.GetHashCode](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Runtime.Numerics/src/System/Numerics/BigInteger.cs#L999) and the [random HashCode seed](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/HashCode.cs#L54). The prerequisite now enables that path by default, without saving/restoring any counter state or patching Boogie. The standalone result-cache branch applies the same numeric policy only inside enabled cache sessions, leaving its option-off setting unchanged.

Frozen producers after this change are default `62c7976f8104901326fd8bd20dce33145eab2d88` (product `05276f470205c09b39c616fee0e7a7f2bdd17e56`) and table `28cb1c2e3edf3e71d7a6c0b7cfa0f2e9c7eb0b2a` (product `af0f8caad62f86cce82a3887c2bb2fce9431b9ba`). Both Release CLI builds pass; all 448 prerequisite and 467 table Core tests pass. The current ordinary-pool additional-axiom Std comparison passes all prerequisite/table-off/cold/warm pairs, including complete native SMT, all 7,773 task outcomes/RU and reports/diagnostics; each mode uses one physical solver. The no-axioms Std profile also preserves all 7,773 task outcomes/RU, Boogie, untimed reports, stdout/stderr and exit. It exposes an ordinary-pool raw-stream mismatch described below; the remaining suite population continues. Producer-specific frontend controls complete as recorded below. Their completion and current-head CI are required before readiness; the historical tables above are not relabelled as current acceptance.


## Current shipped-base CI comparison

All four current Std jobs pass at https://github.com/erniecohen/dafny/actions/runs/43328497848, producer `62c7976f8104901326fd8bd20dce33145eab2d88`. The matching shipped artifact is https://github.com/erniecohen/dafny/actions/runs/43293654613 at `8deca8ad4e8b49988f683855fe78a6917338511c`. These standing CI invocations explicitly select four verification cores, `/deterministicLiteralHashes:1` and `/restartProver`; they do not replace the ordinary pooled-solver local raw-stream comparison. Each profile has the same 2,195 declaration keys. The [current declaration ledger](241-default-translation-std-ci-8de-costs.tsv) records all 1,592 changed-cost rows, including batch-shape changes from the retained isolation annotations.

| Solver / axioms | Changed-cost declarations | Shipped RU | Default RU | Delta RU |
|---|---:|---:|---:|---:|
| 5.1.0 / off | 393 | 1537571323 | 1352578805 | -184992518 |
| 5.1.0 / on | 393 | 1198380562 | 1133217427 | -65163135 |
| 4.12.1 / off | 410 | 1078590955 | 1053760381 | -24830574 |
| 4.12.1 / on | 396 | 1026098165 | 1028482520 | 2384355 |

Every declaration outcome change in this current cohort is listed below; no logical-error outcomes or target-specific verdicts change. Z3 4.12.1 with additional axioms on has no outcome changes.

| Solver / axioms | Declaration | Shipped outcome / RU | Default outcome / RU |
|---|---|---|---|
| 5.1.0 / off | `Std.BulkActions.BatchReader.Read (correctness)` | Correct / 11800387 | OutOfResource / 12094105 |
| 5.1.0 / on | `Std.BulkActions.BatchReader.Read (correctness)` | Correct / 11852466 | OutOfResource / 12130234 |
| 5.1.0 / on | `Std.JSON.ZeroCopy.Deserializer.Sequences.Elements (well-formedness)` | OutOfResource / 77407063 | Correct / 77320476 |
| 4.12.1 / off | `Std.Base64.Encode2PaddingIs2Padding (correctness)` | Correct / 637472 | OutOfResource / 1025435 |

The failing tasks retain their existing resource bounds. These four declaration outcome moves are the already-recorded naming/order changes; no additional expectation update is needed for this numeric-policy revision. The Decode1 isolation adds 22 further batches and changes aggregate cost, while its proof remains Correct in all four profiles. Other gating jobs and the complete current local suite/Std transport cohort are still required before readiness.


### Current frontend measurements

Current table source `28cb1c2e3edf3e71d7a6c0b7cfa0f2e9c7eb0b2a`, product `af0f8caad62f86cce82a3887c2bb2fce9431b9ba`, completes five fresh-process observations per scenario. Warm/edit processes populate their own process-local table before the measured interval; the edit changes one function in an unrelated module. No solver or version probe is requested; RU is not measured. Median [minimum, maximum], seconds:

| Input / scenario | Wall | CPU | Translations / hits | Solver calls |
|---|---|---|---:|---:|
| Std cold | 56.303 [55.193, 62.376] | 58.284 [57.434, 61.408] | 94 / 0 | 0 |
| Std warm | 16.334 [15.837, 16.564] | 18.267 [17.558, 18.830] | 0 / 94 | 0 |
| Std unrelated edit | 16.314 [15.474, 16.939] | 18.465 [17.433, 19.028] | 1 / 93 | 0 |
| Three-module cold | 0.562 [0.509, 0.581] | 0.551 [0.500, 0.568] | 3 / 0 | 0 |
| Three-module warm | 0.092 [0.084, 0.099] | 0.096 [0.089, 0.101] | 0 / 3 | 0 |
| Three-module unrelated edit | 0.118 [0.111, 0.128] | 0.123 [0.116, 0.130] | 1 / 2 | 0 |

Std warm frontend speedup is 3.45x. Every cold observation translates 94 modules; every warm observation hits all 94; every edit observation retranslates only the unrelated module and hits the other 93. Both inputs complete all 30 expected observations with zero bypasses. These measurements describe the frontend interval, not end-to-end verification speed.


The same current producer also repeats the complete growth control: 94 modules, all 93 unaffected texts byte-identical after unrelated growth from one to 64 functions and reversed translation order, with checksums/model-view enabled and zero solver calls/probes. Eleven current numeric-policy rejected-Std observations match shipped diagnostics and exit exactly.


### Numeric-policy harness repairs

The upstream-harness failure at https://github.com/erniecohen/dafny/actions/runs/43328497848/job/131567077034 contains two RU-only snapshots and one moved witness proof. Local reproductions use the same snapshot solver, Z3 4.12.1, for `SubsetTypes` and `SchorrWaite`; this is upstream-harness compatibility evidence, separate from the Z3 5.1.0 cache qualification. Their diagnostics and verdicts are unchanged. The retained snapshot values are:

| Program / resolver | Verified / errors | Old total / maximum RU | New total / maximum RU |
|---|---|---|---|
| `SubsetTypes` / legacy | 13 / 91 | 765400 / 150000 | 765700 / 150700 |
| `SchorrWaite` / legacy | 269 / 0 | 31938598 / 1863972 | 31955613 / 2720100 |
| `SchorrWaite` / refreshed | 273 / 0 | 32101527 / 1948443 | 32255101 / 2034361 |

`NestedKeyWitness82` in the map positive fixture moves from Correct to Errors when stable numeric hashes are enabled. A four-way diagnostic reproduces this on both shipped `8deca8ad` and the prerequisite: explicit hash mode 0 is Correct at 87,465 correctness RU; mode 1 reports the two unproved postconditions at 97,941 RU. Thus this is an exposed search-order sensitivity in an existing proof, not an unexplained resolver change. The repair adds only `{:isolate_assertions}`, preserving the contract, body, nested source comprehensions and two-million RU bound.

The complete registered map lit test passes all four resolver/domain-trigger positive combinations and all ten designated negative invocations. The repaired witness has four Valid correctness batches: 175,733 total RU, maximum 77,691 with domain triggers off; 175,850 total, maximum 77,736 with them on. Both resolver modes agree. Four separate reachable-false controls fail with Errors, without resource exhaustion or timeouts; the audit report contains no findings. All four positive combinations also pass prerequisite/table-off/cold/warm equality in complete native SMT, Boogie, reports, outcomes/RU, stdout/stderr and exit. The focused harness passes four selected tests, including both `SchorrWaite` resolver snapshots and `SubsetTypes`; this does not claim the full harness has passed on the repaired head.

### Task-scoped native equality and physical solver counts

The owner defines strict raw-SMT equality per proof task, from that task's own empty solver checkpoint. Compare the exact encoded native command sequence, including setup/prelude/options and adaptive error/model queries, independent of which physical process carries it. A reset establishes the empty checkpoint; bytes after it belong to the task. A replacement process starts at its fresh checkpoint. End the task before the idle cleanup handshake. Previous-task history, physical process boundaries and idle-handshake recycling are excluded from task equality and reported separately. Any difference in a task's own sequence, outcomes/RU, stdout/stderr or exit remains a blocker.

The original no-axioms full-Std observation preserves all 7,773 task outcomes/RU, Boogie, complete untimed reports, diagnostics and exit, while prerequisite/table-OFF/cold/warm use 2/3/2/2 physical solvers. Cold/warm also agree in whole-process bytes. Pinned Boogie's `Checker.GoBackToIdle` discards a checker if its native idle ping exceeds a hard-coded 100 ms wall-clock wait. The extra OFF boundary follows `Std.Producers.FunctionalIProducer.Decreases`; every mode also recycles after `Std.Producers.ConcatenatedProducer.Invoke`. All physical solvers are observed, and the final common stream is byte-identical. The additional-axiom observation uses one solver per mode and agrees in whole-process bytes too.

This pre-existing pool nondeterminism is tracked at https://github.com/erniecohen/dafny/issues/287. The passive close-stack diagnostic confirms the timeout path. It does not justify changing the task's command sequence or ignoring a task mismatch. The prerequisite retains ordinary pooled solving: it neither enables `/restartProver` by default nor changes Boogie's lifecycle. Earlier explicit fresh-solver profiles remain separate producer-specific evidence. The renewed task-native observer retains physical hashes/counts and requires complete per-task and physical-solver coverage.

### Completed transport cohort and retained-maxima proof repair

The frozen numeric-policy cohort completes all 2,362 cases and 31,686 task observations per mode: 1,172 suite programs in each axiom profile, seven Std parts in each profile, and four explicit history-free controls. There are no harness errors or unclassified differences. All cold/warm fields match. Every mode has identical Boogie, outcomes/RU, complete untimed reports, stderr and exit. All 2,344 suite cases pass native-wire equality; twelve prerequisite/table-OFF stdout differences are exactly the newly advertised option line in CLI-rejected invocations with no proof tasks. The corrected verifier supplements remain separate evidence. OFF/cold fails only the no-axioms full-Std prepared-prefix and physical-stream fields described above.

A separate completed passive diagnostic records `Checker.GoBackToIdle` → `Checker.Close` from a timer continuation at the common `ConcatenatedProducer.Invoke` recycle boundary. It appends a close stack only after native `Close`; it changes no solver request or lifecycle policy. This confirms the timing-driven recycling mechanism; task-scoped equality above supersedes the former whole-process acceptance criterion.

The renewed suite also reproduces the previously listed `MinimumWindowMax` additional-axiom OOR at its outer-loop retained-maxima invariant: VC 122, 100,020,029 RU under the existing 100-million bound. The retained repair proves that invariant explicitly for one arbitrary retained entry at the end of the loop. It adds a checked `forall` proof statement; the executable algorithm, contract and RU ceiling are unchanged. A trial inner-loop invariant still exhausted its bound and was discarded.

The complete repaired program verifies all 232 batches in both additional-axiom profiles, compared with the previous 222. The ten added batches check the explicit proof. With axioms off, correctness uses 66,726,084 aggregate RU and the maximum task is 10,945,376; with axioms on, correctness uses 80,486,571 RU and the maximum task is 38,066,580. Well-formedness remains 69,095 RU across nine batches. These are focused producer-specific observations, not a new full-suite performance benchmark. Two complete prerequisite/table-OFF/cold/warm native-writer supplements pass every compared field on the repaired source. Four reachable-false controls (both inside the new `forall` body and at method exit, in each axiom profile) fail with Invalid, without OOR/timeouts; audit has no findings. Both legacy/refreshed upstream-harness compatibility profiles also verify 232 batches with the harness's pinned Z3 4.12.1. Main cache proof qualification uses Z3 5.1.0.

That earlier frozen cohort predates the integration-fixture repair. The completed task-native cohort below includes it. Under the owner's revised push policy, both expected suite files are generated locally from its prerequisite stdout/stderr/exit using the standing verdict extractor and expected-file exporter. All 1,172 program keys per profile match; the only changed row is `dafny2/MinWindowMax.dfy`, from 222 to 232 verified batches, because the retained explicit proof adds ten checked batches. No CI artifact is used for this refresh.


### Completed task-native qualification

The task-scoped native-writer cohort completes 2,362 cases and 31,706 task observations per mode, using the frozen product binaries identified above and integration/Std inputs from `e7e18c65ec22fd5780adfe05aacd5c9e5a65b4a8`. This input includes the retained-maxima repair. All prerequisite/table-OFF, OFF/cold and cold/warm pairs have identical per-task native bytes, printed Boogie, outcomes/RU, untimed report fields, stderr and exit, with complete task/physical coverage. Cold/warm stdout also matches throughout. The twelve prerequisite/table-OFF stdout differences are exactly the added option-help line in CLI-rejected invocations with zero proof tasks; they remain classified diagnostic differences. No unclassified difference or observer error occurs.

All 24 corrected native verifier companions pass the same comparisons, adding 352 task observations per mode: 2,386 cases and 32,058 task observations combined. These companions cover verification, not compiler execution or stdin/redirection behavior. The main cohort contains 28,156 Valid, 3,490 Invalid and 60 OutOfResource observations per mode; these include deliberate negative tests and known resource-limited library outcomes, not a claim that all programs verify.

Physical solver counts remain separate:

| Scope | Prerequisite | Table OFF | Cold | Warm |
|---|---:|---:|---:|---:|
| Main cohort | 1578 | 1578 | 1577 | 1577 |
| Corrected verifier companions | 20 | 20 | 20 | 20 |
| Full Std, axioms off (included above) | 2 | 2 | 2 | 2 |
| Full Std, axioms on (included above) | 1 | 2 | 1 | 1 |
| `MinimumWindowMax`, axioms on (included above) | 2 | 1 | 1 | 1 |

The last two rows are the only main-cohort cases with different physical counts. Their complete task-byte sequences and all proof/report fields agree. These observations confirm the owner-selected boundary without changing the default solver lifecycle or adding `/restartProver`. The [machine-readable qualification summary](241-task-native-qualification.json) records producer identities, counts and every classified diagnostic difference. The passive observer and strict comparator are published in https://github.com/erniecohen/dafny/pull/262. Corruption controls reject an altered task hash and a dropped task record.


### Native Std growth control

A separate prerequisite capture grows an unrelated non-dependency module from one to 64 functions. In each additional-axiom profile, all 86 printed original units and all 7,773 native Std task command sequences remain byte-identical; the unrelated unit's own text changes. This complements the 93-module frontend graph control above: printed CLI programs and frontend module graphs have different counts. Complete task and physical coverage, outcomes, exit and stderr match. Physical solver counts are 3/3 with axioms off and 1/1 with axioms on.

One no-axioms report row changes RU despite identical native commands: `Std.Arithmetic.MulInternalsNonlinear.LemmaMulIsAssociative (correctness)` remains Valid, 20,558 to 20,534 RU. The cause is now reproduced by native process history alone: five direct replays after 1,660 preceding tasks each give 20,558 RU, five after 1,918 give 20,534, and five fresh target-only processes give 20,536. Both histories are reconstructed from one fixed-source capture; no translator, Boogie, cache or scheduler runs during replay. All other report fields and all additional-axiom report rows match. The [causal investigation](241-native-process-history.md) records exact hashes and controls. This is a classified pooled-history effect, not pooled growth-RU equality. The complete OFF/cold/warm equality cohort separately has no difference. The product solver lifecycle is unchanged.

The owner now restricts both public cache options to `--type-system-refresh`. The frozen full cohort predates that admission restriction; legacy internal-API translation observations remain implementation evidence, not a claim that legacy CLI cache invocations are accepted. Std's project already selects the refreshed type system. New CLI admission tests reject legacy cache requests before pipeline startup with exit 2.
