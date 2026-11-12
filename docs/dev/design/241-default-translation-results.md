# Default translation qualification before proof repair

This cohort compares shipped commit https://github.com/erniecohen/dafny/commit/7dc1d91b1159bafb2d75dfb092930112528e6c18 with default producer https://github.com/erniecohen/dafny/commit/1dbf44071689b525046d97ceba9ccd5f8344e396. Subsequent moved-proof repairs require new library qualification; these observations retain their original producer identity.

The population contains 1,172 suite programs and seven standard-library parts in each additional-axiom profile: 2,358 cases and 31,564 task observations per mode. Both profiles retain their native resource limits and options. There are no abnormal process exits or task-population changes. The standing suite plan has twelve CLI-rejected invocations per profile, including missing print arguments and unsupported shell-redirection tokens. Those 24 observations cover CLI diagnostics and contribute no proof tasks; they are not verification evidence. Corrected native verification supplements are pending.

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

Default expected verdicts must be refreshed only from failed current gating artifacts in a separate explained commit. This report changes no expected verdict file and does not claim current-head CI acceptance.

## Current Std gating artifacts after the two proof repairs

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

The complete declaration aggregates and batch counts are in [the current Std CI cost ledger](241-default-translation-std-ci-costs.tsv). Isolation changes batch shape for the two repaired proofs, so these aggregates do not pair their old and new VC numbers. These artifacts establish current CI verdict and cost changes; they do not capture native SMT byte hashes. Complete transport identity between the prerequisite and translation-table modes remains pending.


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

The merged operator `revealed` witness is a separate unresolved front-end regression: shipped `8deca8ad` resolves it, but the integrated default and table builds reject the export view. Ownership is coordinated on https://github.com/erniecohen/dafny/pull/263#issuecomment-6836472064. No resolver-site fix is implemented here. Complete native-stream qualification retains its exact producer/input identities; the source repairs above require renewed qualification of their affected proof inputs.
