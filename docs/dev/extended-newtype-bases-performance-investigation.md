# Extended newtype bases: investigation of performance triggers

The provisional proof-cost triggers have concrete batch-level explanations. They do not support a uniform speed claim. This investigation reads the completed [the completed public matrix](https://github.com/erniecohen/dafny/actions/runs/37241854757) on source `be0c7c4da87db52cfe76cc8422001a1e9228fb60`, product `95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6`. No new execution or source change was made. The recorded three RU samples are identical in each completed comparison arm on both solvers.

The depth fixtures retain the same universal RoundTrip(b: Pair) identity statement, and a separate concrete non-vacuity call. B has transparent aliases; N declares a chain of unconstrained nominal newtypes and explicitly introduces each layer; W constructs/destructs the matching singleton-datatype chain. The whole-program denominator includes declaration well-formedness, rather than discarding those costs.

| Depth, Z3 5.1.0 | B total RU / VCs | N declaration WF | N RoundTrip | N non-vacuity | N total RU / VCs | W total RU / VCs | N vs W total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 9604 / 2 | 1959 | 7648 | 3073 | 12680 / 3 | 11445 / 2 | +10.791% |
| 10 | 9604 / 2 | 19590 | 13822 | 3073 | 36485 / 12 | 25514 / 2 | +43.000% |
| 100 | 9604 / 2 | 195900 | 69382 | 3073 | 268355 / 102 | 166822 / 2 | +60.863% |
| 1000 | 9604 / 2 | 1959000 | 630982 | 3073 | 2593055 / 1002 | unavailable: outer process cap | unavailable |

Depth1 is below the provisional20% N-versus-W trigger, although it is +32.028% against raw B. Depth10 and100 exceed20% against W. Each added nominal declaration has a distinct Layer (well-formedness) batch costing1959RU. The actual BPL shows a trivial constraint branch and an implicit-witness branch whose only assertion is true. W declarations add datatype theory but no corresponding checked implementation/batch. W RoundTrip costs8372/22441/163749RU at depths1/10/100; N RoundTrip itself is cheaper at each of these depths. The whole-program difference is therefore extra declaration-WF cost, offset by a cheaper universal identity proof. Against B there is also the cost of nominal introduction/projection terms and membership theory in the identity lemma.

The N generated BPL at depth10 has12 implementations,13 syntactic assert tokens and126274 bytes; W has2 implementations,2 asserts and154470 bytes. At depth100 the respective sizes are315956 and4792839 bytes. These are syntax/translation metrics, not solver quantifier-instantiation counts. Such counters are unavailable in the retained logs. The W proof arms have identical verification semantics and RU across the erased/materialized labels; erasure concerns target code generation. At depth1000 all W translate and verify invocations in both modes/solvers hit the existing90-second outer process cap. No CSV RU or nonempty BPL survived those caps. They cannot be called16M-RU exhaustion or assigned a numeric cost. N1000 completed with2.593M totalRU.

Z3 4.12.1 shows the same extra1959RU per declaration and the sameVC counts. Whole N totals are12794/36599/268469/2593169; W totals11445/25514/166822/unavailable. N-versus-W at1/10/100 is +11.787%/+43.447%/+60.931%. This supports the declaration explanation across the two measured solvers without claiming a universal complexity bound.

The visibility source repeats fifty independently named provider/client export sessions. Each has an opaque API call justified by exported RoundTrip and a revealed API call with the same universal identity contract. N adds a nominal Value declaration and identity casts for Encode/Decode; W adds a singleton constructor/destructor. The source keeps the hidden boundary intact.

| Batch category, both solvers | Count | B RU | N RU | W RU |
| --- | ---: | ---: | ---: | ---: |
| Provider.Value well-formedness | N only50 | 0 | 97950 | 0 |
| Provider.Decode well-formedness | N only50 | 0 | 167650 | 0 |
| Provider.RoundTrip correctness | 50 | 268950 | 310400 | 347250 |
| Client.VisibleRoundTrip correctness | 50 | 268950 | 310400 | 347250 |
| Client.HiddenRoundTrip correctness | 50 | 240750 | 282150 | 307150 |
| Whole source | B/W150 VCs; N250 | 778650 | 1168550 | 1001650 |

The N Value declaration contributes50*1959RU from the same trivial declaration-WF batch. Each Decode conversion contributes3353RU: its actual BPL asserts $Is(v, Tclass.Provider.Box()) while taking v in the nominal Value type. The visible newtype membership axiom connects Value and Box. The equivalent W destructor and B alias produce no separate checked Decode body. These100 added batches total265600RU. N's remaining universal-lemma batches total902950RU, below W's1001650. Thus N-versus-W's166900RU (+16.663%) is exactly extraWF265600 minus cheaper-lemma98700. N-versus-B's389900RU (+50.074%) is extraWF265600 plus the124300RU nominal-lemma delta. The fifty sessions multiply the same per-session categories, rather than exposing a rising solver-instantiation effect. Optimizing would require separately reviewing these declaration/conversion checks; omitting them from a benchmark would change its denominator.

Runtime medians have six >10% comparisons across150 N/reference pairs (15 runtime families, five targets, B/W-erased references). The table records the actual post-warmup boundary through final output/process teardown, not isolated Work cost.

| Family / target / reference | N vs reference median | N samples ms | Reference samples ms |
| --- | ---: | --- | --- |
| arrow-adapter / Java / W-erased | +11.0% | 29.181,40.234,38.775 | 34.930,41.968,29.802 |
| arrow-coercion / Java / B | +27.7% | 28.090,28.955,19.326 | 17.144,21.994,30.220 |
| arrow-coercion / Java / W-erased | +26.5% | 28.090,28.955,19.326 | 30.538,22.210,19.225 |
| function-creation / Java / B | +78.3% | 24.266,25.428,26.659 | 22.141,13.482,14.259 |
| refinement-quantified / Java / W-erased | +54.2% | 19.129,12.263,21.009 | 11.974,12.407,21.304 |
| refinement-quantified / Python / B | +15.1% | 12.913,12.420,11.303 | 10.950,10.650,10.787 |

Four pairs have overlapping sample ranges. Java function-creation and Python refinement-quantified have a consistent positive separation in this capture, so their observed slower post-warmup times should remain visible. Their smallest cross-sample slowdown is9.60% and3.22%, respectively; none of the six pairs has every N sample more than10% above every reference sample. Three samples in one job do not establish a backend-wide speed guarantee or erase these measured median triggers.

Static emission review finds no extra hot-path code for the two separated pairs: function-creation B/N __default.java is byte-identical (SHA2569f45c36b68b4d1d45ea77395a3f2c48d0f8cc96edfbb9b3212bd43aee9b6cf66); its actual __default.class bytes in the retained jars are also identical (SHA256b3618e1e6c6be6933707de5e8ebfc6e0b0a8a4f3a6486ac95e0134bd5aa05739). The Java entrypoint source differs by a blank line, and its debug-containing class bytes differ. Python quantified-refinement B/N whole module_.py is byte-identical. These facts rule out an additional emitted Work conversion or wrapper in those comparisons. They do not prove which environment/JIT/teardown effect produced the observed times.

The separate C# profiler isolates Work from output/teardown for five samples per arm. All28 valid N-versus-B/W-erased comparisons across14 available families have median Work differences at or below10%, and all have identical managed allocation bytes. Codatatype profiling remains unavailable (native nullable-warning build boundary); its successful ordinary runtime checks do not supply allocation evidence. No inference is made for unprofiled Java/Python allocations.

The investigation supports a precise limitation: nominal declarations and conversion well-formedness add proof work, while the actual identity lemmas can be cheaper than wrappers; runtime representation/Work allocation parity is supported where measured, and small native timings retain noise and the six median triggers. No source optimization is proposed here, no historical cap is reclassified, and no uniform performance promise is made.

[Per-batch attribution](extended-newtype-bases-performance/proof-batch-attribution.tsv) and [runtime median triggers](extended-newtype-bases-performance/runtime-median-triggers.tsv) retain the measured rows behind this explanation. Raw BPL, source and solver logs remain in the linked public matrix artifacts.
