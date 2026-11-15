# Default-off solver query context and resource diagnostic

Public solver-session diagnostic [37250405693](https://github.com/erniecohen/dafny/actions/runs/37250405693), source 79f9eeec206080c1a1b4812d74c002af699e7857, completed all 15 pairs / 30 invocations. All invocations exited 0 with 132 verified, 0 errors, no warnings and no timeout. The nine canonical cores4 comparisons still have exact RU differences and raw solver-session differences. This is a completed diagnostic, not a passing cost-compatibility gate. No tolerance or replacement oracle was applied.

Repaired compiler b615831aaa26fea72364627d0fe6040f10e49eee was compared with feature compiler a5304cd9aeb5e4fd12700aa2e61125a904d58996 on the same 4608-file original-input receipt ff5985528bb5a0e23f6ef6a5b2321791ac4ebbdd6de4281e611ad846743b1b03. Recorded hashes for all 13 Boogie DLLs match exactly between arms. The runner class was Linux x64 with four CPUs. Paired invocations retain the same canonical settings; the separately labeled cores1 controls change only the core count. Raw JSON vcResults rows omit randomSeed in all 3960 batches. The diagnostic collector fills that missing field with "0" using batch.get("randomSeed", "0"); this is a collector fallback, not recorded per-batch seed provenance. No invocation argument or set-option command in the 84 retained SMT logs explicitly sets a seed. These artifacts therefore do not establish an actual per-batch seed value. The explicit extended-newtype option is absent from these canonical commands.

Canonical cores4 samples remain in recorded sample order:

| Solver / case | Sample | Repaired RU | Feature RU | Difference | Changed single VCs | Exact / reordered / other query contexts |
|---|---:|---:|---:|---:|---:|---:|
| 5.1.0 / cs | 0 | 6,553,608 | 6,553,603 | -5 | 3 | 66 / 61 / 5 |
| 5.1.0 / cs | 1 | 6,553,669 | 6,553,603 | -66 | 58 | 58 / 73 / 1 |
| 5.1.0 / cs | 2 | 6,553,609 | 6,553,626 | +17 | 4 | 57 / 68 / 7 |
| 5.1.0 / notarget | 0 | 6,553,603 | 6,553,607 | +4 | 4 | 57 / 72 / 3 |
| 5.1.0 / notarget | 1 | 6,553,604 | 6,553,603 | -1 | 1 | 57 / 71 / 4 |
| 5.1.0 / notarget | 2 | 6,553,676 | 6,553,727 | +51 | 70 | 57 / 65 / 10 |
| 4.16.0 / notarget | 0 | 6,599,362 | 6,599,367 | +5 | 3 | 120 / 0 / 12 |
| 4.16.0 / notarget | 1 | 6,599,362 | 6,599,293 | -69 | 69 | 58 / 74 / 0 |
| 4.16.0 / notarget | 2 | 6,599,033 | 6,599,293 | +260 | 2 | 57 / 65 / 10 |

Each invocation contains 127 declarations and 132 VCs. Raw BPL file names and bytes are identical across each pair and every same-arm repeat: 14 files for cs, 13 for notarget. The raw SMT logs contain no boogie-vc-id markers. Their actual named assertion IDs map uniquely to the BPL verboseName declaration, allowing 126 single-batch queries per invocation to join to their actual name and vcNum=1. The six other queries all belong to Std.FileIO.WriteUTF8ToFile (correctness); their query-to-vcNum correspondence is deliberately unassigned. Actual JSON vcNum rows remain available, all six costs equal in every comparison, aggregate 546455 RU.

The context counts compare exact reset-to-check command sequences, not normalized formulas. “Reordered” means every literal command has a one-to-one occurrence correspondence but occurs in a different position; both original positions are retained. “Other” includes generated-symbol changes and remains unnormalized. Named assertion ID order is identical for all 132 queries of all pairs.

Within-arm canonical repeats also move in RU:

| Solver / case | Arm | Samples 0, 1, 2: total RU | VCs varying across repeats |
|---|---|---|---:|
| 5.1.0 / cs | Repaired | 6,553,608, 6,553,669, 6,553,609 | 62 |
| 5.1.0 / cs | Feature | 6,553,603, 6,553,603, 6,553,626 | 1 |
| 5.1.0 / notarget | Repaired | 6,553,603, 6,553,604, 6,553,676 | 70 |
| 5.1.0 / notarget | Feature | 6,553,607, 6,553,603, 6,553,727 | 5 |
| 4.16.0 / notarget | Repaired | 6,599,362, 6,599,362, 6,599,033 | 69 |
| 4.16.0 / notarget | Feature | 6,599,367, 6,599,293, 6,599,293 | 72 |

Across both arms and all five observations per arm, the 126 single-batch query identities produce 430 repeated exact identity/context groups. In 424 groups, the complete prior-session raw prefix differs. No group has different RU for the same ordered reset-to-check context. Thus these observations support a context-construction explanation for the cost movements; they do not exhibit a pure prior-session-history effect after identical contexts. All 3876 queries after the first query of a session have a reset before their context; the 84 initial queries begin fresh recorded sessions.

One concrete correspondence is the 4.16.0 canonical sample1 pair: 58 contexts match exactly and 74 contain only literal command moves. Its 69 changed single-batch costs all decrease by 1 RU. A separate 5.1.0 notarget cores1 sample1 example for MAX_YEAR moves the exact assertion (= $generated@@29 ($generated@@19 2147483648)) later in the context. It comes from the unchanged BPL bounded-integer axiom TWO__TO__THE__31 == LitInt(2147483648). Other examples move TWO__TO__THE__32 == LitInt(4294967296) and also show changed generated-symbol numbering. The retained diffs and byte-offset ledger preserve all original commands and semantic paths.

The largest canonical per-VC movement is IsValidDateTime under 4.16.0 sample2: 18769 to 18975 RU (+206); the repaired arm itself ranges from 18769 to 18976 across canonical repeats. Under 5.1.0 notarget sample2, FromEpochTimeMillisecondsFunc changes from 36219 to 36342 (+123), while the feature arm itself ranges from 36218 to 36342. These are recorded movements, not accepted tolerances.

Boogie v3.5.5 is the exact package version declared by this diagnostic source. Its Checker.Target fully resets and rebuilds a context from the split declarations; Setup orders declarations using ContentHash when normalization is enabled; SetupAxioms serializes the axiom conjunction in traversal order, and NormalizeNamer supplies generated names using the encounter counter. This is a source mechanism consistent with the observed order and numbering movement. The retained experiment does not isolate which preparation/concurrency step selects an order, or prove that the feature cannot influence scheduling. There is no identified difference in Dafny-to-BPL formulas, and no observed extra RU effect when the final ordered SMT context is identical.

The six separately labeled cores1 comparisons are diagnostic controls and do not replace the canonical denominator:

| Solver / case | Sample | Repaired RU | Feature RU | Difference | Changed single VCs |
|---|---:|---:|---:|---:|---:|
| 5.1.0 / cs | 0 | 6,553,603 | 6,553,605 | +2 | 2 |
| 5.1.0 / cs | 1 | 6,553,608 | 6,553,604 | -4 | 4 |
| 5.1.0 / notarget | 0 | 6,553,605 | 6,553,614 | +9 | 13 |
| 5.1.0 / notarget | 1 | 6,553,608 | 6,553,672 | +64 | 72 |
| 4.16.0 / notarget | 0 | 6,599,299 | 6,598,962 | -337 | 4 |
| 4.16.0 / notarget | 1 | 6,599,293 | 6,599,299 | +6 | 2 |

Metadata-only analysis read and hash-checked the actual raw files and command ranges. It did not execute Dafny, Boogie, Z3, or replay any solver input. Machine evidence: query-identity-ledger.json, paired-context-comparisons.json, ordered-command-correspondences.json, within-arm-resources.json, within-arm-query-comparisons.json, identity-context-resource-observations.json, multi-batch-cost-boundary.json, seed-provenance-inspection.json, and paired-measurements.tsv.
