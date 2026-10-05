# Issue 82 verdict and resource review

The candidate source is `3880947769b51baa37cb8bd4b4daf1db7576d62e`, product `cf69e0e254be770a6cc833f0434ecbe932d7eec1`, version `4.11.0+fcb2042d.review.235e4590`, measured by the [public candidate capture](https://github.com/erniecohen/dafny/actions/runs/37236271404). It is compared with baseline `e3d66f3fbab77116e5f1a232c0af939647981a65`, [baseline review](https://github.com/erniecohen/dafny/actions/runs/37203483220). These records describe that exact completed proof run. Its six expected-verdict comparisons failed; the expectation-only artifact closure is reported separately from these proof outcomes.

All 20 recorded jobs identify the same captured compiler source. Every recorded build, corpus-runner, harness, resolver and audit step completes successfully; the only failed steps are the six reviewed expected-verdict comparisons. One conditional resolver artifact upload is intentionally skipped. Individual proof Errors and resource exhaustion remain recorded below. A successful scratch capture job alone is not evidence that its expected-verdict comparison passed. The registered lit manifest contains 95 cases, and the log records exactly those 95 passing cases. The translation audit detects all 105 mutations and checks 19 actual SMT files containing 23 solver queries. These checks cover the finite-support translation fragment; they do not establish consistency of the whole prelude.

## Verifier suite

Both option lanes retain all 1,081 existing programs and all 7,351 proof batches. Every baseline proof batch has a matched candidate batch; there are no added or removed batches. In each lane, 42 programs emit no proof JSON in both builds and retain their existing program verdicts: 41 have diagnostic exits 1 or 2, and `github-issue-2563.dfy` has an inherited rejected `/verificationLogger:csv` argument and a 0 verified / 0 errors summary. These provide no proof-batch evidence. The paired totals below cover all emitted proof batches, including intended verification failures and resource exhaustion, not just successful obligations.

| Additional axioms | Baseline RU | Candidate RU | Change |
|---|---:|---:|---:|
| OFF | 1,490,451,710 | 1,499,134,750 | +0.58% |
| ON | 1,533,614,197 | 1,542,793,092 | +0.60% |

Exactly one existing program verdict changes in each lane: `dafny4/gcd.dfy` has one additional out-of-resource proof at line 165. `GcdSubtract` batch 5 moves from Valid at 12,643,055 RU to OutOfResource at 16,010,180 RU under the unchanged 16,000,000 limit. Its source and contract are unchanged. The proof chooses a,b with p*a=x and p*b=y, and uses p*(b-a)=y-x. Since p>0 and x<y, the positive factor witness follows mathematically; this run does not verify it within the original budget. Any expectation update must record this cost regression explicitly. No resource cap, arithmetic axiom, specification or proof source is adjusted to make it pass.

The following are every matched batch above 50,000 RU that rises by more than twofold. They are reviewed separately from the aggregate totals.

| Program / declaration | OFF baseline / candidate RU | ON baseline / candidate RU | Outcome |
|---|---:|---:|---|
| `Inverses`, `RotateA` correctness | 455,386 / 3,608,514 | 455,386 / 3,608,514 | Expected verification failure preserved. The source intentionally exposes an inverse-trigger limitation. |
| `QuantificationNewSyntax`, `NewSyntax.M` correctness | 115,496 / 259,393 | 115,496 / 259,393 | Expected invalid obligations preserved: division by zero, unsatisfied function precondition and subset conversion. |
| `Lucas-up`, `Lucas_Theorem` correctness | 128,649 / 455,980 | 185,243 / 412,388 | Valid in both builds and lanes, with the same universal bit-set/binomial parity contract. |

These are observed search-cost changes after the default soundness repair. The individual causal solver mechanisms are not established by this comparison. Intended failures remain rejected; their increased search costs are not interpreted as semantic regressions or completed proofs. No matched suite batch moves from Invalid to Valid. Comparing all 2,162 captured program outputs finds no newly added warning or error about a trigger or pattern; that diagnostic comparison does not audit every standard-library log or prove all SMT patterns valid.

`SubsetTypes.dfy` retains its exact 13 verified / 91 errors and diagnostics. The Z3 5.1.0 suite capture reports total 725,000 / maximum 82,500 in the baseline and 710,700 / 75,400 in the candidate. Removing only those two resource lines leaves identical captured diagnostics and summaries in each option mode. Those primary-solver measurements do not define the literal test expectation. Separately, the Z3 4.12.1 literal test harness uses the committed expectation 690,900 / 71,900, replacing 697,000 / 73,800; the candidate passes that harness. The expectation edit changes only its two resource-output lines.


## Source-library declarations

All 85 tracked library source and project files are byte-identical to the baseline. All four solver/option lanes contain exactly 2,197 rows: 2,190 declarations and seven run summaries covering Std and six target-specific parts. Every declaration and summary key matches the baseline; no row is added or removed. The library source contracts, proof bodies, project configuration and declaration order are unchanged. The standard project retains its 1,000,000 resource-unit limit and every existing declaration override. The aggregate number of proof batches in each matched declaration is unchanged.

`AboutMap_Bind_` and `AboutConcatBindSucceeds` verify in both solvers and option modes under their original budgets. Their universal contracts preserve failure results and, on success, the mapped or paired value and final remainder. The repair exposes existing arrow-result allocation instances within lambda facts while retaining their good-heap, arrow allocation, argument allocation and precondition premises.

| Solver / additional axioms | Baseline main-library summary | Candidate main-library summary | Moved declarations / changed summary rows |
|---|---|---|---:|
| 4.12.1 / OFF | 7611 verified, 1 error | 7610 verified, 0 errors, 2 out of resource | 3 / 1 |
| 4.12.1 / ON | 7612 verified, 0 errors | 7610 verified, 0 errors, 2 out of resource | 2 / 1 |
| 5.1.0 / OFF | 7588 verified, 2 errors, 22 out of resource | 7587 verified, 2 errors, 23 out of resource | 8 / 1 |
| 5.1.0 / ON | 7590 verified, 2 errors, 20 out of resource | 7588 verified, 2 errors, 22 out of resource | 9 / 1 |

There is no new Errors declaration. The inherited Z3 5.1.0 errors remain `LittleEndianNat.ToNatLeft` well-formedness and `Mul.LemmaMulOrderingAuto` correctness in both modes. Their contracts are mathematically valid: sums of natural digits times positive base powers are nonnegative; and nonzero integers with nonnegative product have the same sign. In the positive case each integer is at least one, so the product is at least each factor; in the negative case the nonnegative product is greater than each negative factor. These arguments do not convert the recorded failures into verified proofs. The Z3 4.12.1 OFF error for `LemmaMultiplyDivideLt` becomes Correct; its ON lane was already Correct.

The table lists every moved declaration (12 unique declarations, 22 lane cells). RU is a declaration aggregate, summed across proof batches, rather than a per-obligation limit or measurement. OutOfResource is an unverified proof under its original cap, even when the source contract has the mathematical justification below. Any expectation change must retain that distinction and its specific reason. Source paths are relative to `Source/DafnyStandardLibraries/src/Std`.

| Declaration / source | Observed lane and move | Candidate RU / aggregate proof batches | Source-contract review and disposition |
|---|---|---|---|
| `DivMod.LemmaModNegNeg`, `Arithmetic/DivMod.dfy:1320` | 4.12.1 OFF: Correct → OutOfResource; 4.12.1 ON: Correct → OutOfResource | 4.12.1 OFF: 1,582,839 / 13; 4.12.1 ON: 1,583,507 / 13 | For positive d, x*(1-d) differs from x by the multiple -x*d, so their Euclidean remainders agree. The unchanged integer-induction proof exceeds an original obligation limit. |
| `DivMod.LemmaMultiplyDivideLt`, `Arithmetic/DivMod.dfy:899` | 4.12.1 OFF: Errors → Correct | 4.12.1 OFF: 360,516 / 1 | For b>0 and a<b*c, a<=b*c-1, so Euclidean a/b<c, including negative a or c. The original induction proof now verifies in this lane. |
| `DivMod.LemmaMultiplyDivideLe`, `Arithmetic/DivMod.dfy:877` | 5.1.0 OFF: Correct → OutOfResource; 5.1.0 ON: Correct → OutOfResource | 5.1.0 OFF: 1,029,583 / 1; 5.1.0 ON: 1,029,587 / 1 | For b>0 and a<=b*c, Euclidean a/b<=c. The original composition of division induction and division of multiples exceeds its cap. |
| `DivMod.LemmaDivByMultipleIsStronglyOrdered`, `Arithmetic/DivMod.dfy:854` | 5.1.0 OFF: OutOfResource → Correct; 5.1.0 ON: OutOfResource → Correct | 5.1.0 OFF: 777,482 / 1; 5.1.0 ON: 777,506 / 1 | From x<y=m*z and z>0, x<=m*z-1, hence x/z<m=y/z. The original induction proof now verifies within its cap. |
| `DivMod.LemmaRoundDown`, `Arithmetic/DivMod.dfy:728` | 5.1.0 OFF: OutOfResource → Correct; 5.1.0 ON: OutOfResource → Correct | 5.1.0 OFF: 526,111 / 1; 5.1.0 ON: 526,135 / 1 | a%d=0 gives a=k*d; adding 0<=r<d preserves quotient k. The original division-induction proof now verifies. |
| `Power.LemmaPowIncreases`, `Arithmetic/Power.dfy:396` | 5.1.0 OFF: OutOfResource → Correct; 5.1.0 ON: OutOfResource → Correct | 5.1.0 OFF: 129,105 / 1; 5.1.0 ON: 81,354 / 1 | A positive natural base is at least one, so increasing a natural exponent multiplies by a factor at least one. Existing positivity, PowAdds and induction now verify. |
| `Base64.EncodeRecursively`, `Base64.dfy:310` | 4.12.1 OFF: Correct → OutOfResource; 4.12.1 ON: Correct → OutOfResource | 4.12.1 OFF: 5,586,316 / 76; 4.12.1 ON: 5,586,316 / 76 | The suffix invariant identifies the completed suffix with recursive encoding, while 4*i=3*j aligns the slices. Prepending the same encoded block preserves it. An original loop proof remains out of resource. |
| `Base64.EncodeBVIsBase64`, `Base64.dfy:907` | 5.1.0 ON: Correct → OutOfResource | 5.1.0 ON: 1,060,319 / 1 | The length-modulo-three cases produce full unpadded groups, one byte with two padding characters, or two bytes with one padding character. Existing validity lemmas compose the source contract; this lane exceeds its cap. |
| `BulkActions.BatchArrayWriter.Invoke`, `Actions/BulkActions.dfy:434` | 5.1.0 OFF: OutOfResource → Correct; 5.1.0 ON: OutOfResource → Correct | 5.1.0 OFF: 17,930,650 / 107; 5.1.0 ON: 17,930,650 / 107 | Full storage rejects without consumption. Otherwise the matched input increments exactly one of size or otherInputs, records consumption, and preserves capacity and partitioned history. All unchanged isolated obligations now verify. |
| `JSON.Sequences.Elements well-formedness`, `JSON/ZeroCopy/Deserializer.dfy:319` | 5.1.0 OFF: Correct → OutOfResource; 5.1.0 ON: Correct → OutOfResource | 5.1.0 OFF: 71,163,185 / 257; 5.1.0 ON: 71,167,300 / 257 | Strict split/suffix contracts justify the next parse. Singleton separator checks refine its type; nonempty suffix facts justify appending, and strict suffix supplies recursive decrease. The original append-precondition proof at line387 exceeds its cap. |
| `JSON.Arrays.BracketedToArray`, `JSON/ZeroCopy/Deserializer.dfy:874` | 5.1.0 OFF: Correct → OutOfResource; 5.1.0 ON: Correct → OutOfResource | 5.1.0 OFF: 3,166,300 / 14; 5.1.0 ON: 3,166,300 / 14 | SuffixedElementSpec(d) unfolds to Value(d.t)+CommaSuffix(d.suffix)=Item(d). For every d<arr the restricted callback precondition holds and the serializers agree pointwise. Bracketed_Morphism preserves the concatenation and bracket bytes, giving Spec.Array. The original Morphism_Requires assertion at line879 is out of resource. |
| `JSON.Objects.BracketedToObject`, `JSON/ZeroCopy/Deserializer.dfy:992` | 5.1.0 OFF: Correct → OutOfResource; 5.1.0 ON: Correct → OutOfResource | 5.1.0 OFF: 3,610,112 / 17; 5.1.0 ON: 3,610,112 / 17 | SuffixedElementSpec(d) unfolds to KeyValue(d.t)+CommaSuffix(d.suffix)=Member(d). For every d<obj the restricted callback precondition holds and the serializers agree pointwise. Bracketed_Morphism preserves the concatenation and bracket bytes, giving Spec.Object. The original Morphism_Requires assertion at line998 is out of resource. |

The mathematical arguments for non-arithmetic rows compose the unchanged component contracts. No semantic counterexample was found in this source review. The listed budget failures remain verification regressions.

Cost totals include only the same declarations that are Correct in both builds. Each lane has an explicit denominator; improvements from OutOfResource to Correct and losses from Correct to OutOfResource are excluded from these sums and recorded above.

| Solver / option | Correct → Correct declarations / aggregate batches | Baseline RU | Candidate RU | Change |
|---|---:|---:|---:|---:|
| 4.12.1 / OFF | 2,187 / 7,634 | 1,002,544,624 | 1,008,139,493 | +0.56% |
| 4.12.1 / ON | 2,188 / 7,635 | 988,657,113 | 993,755,664 | +0.52% |
| 5.1.0 / OFF | 2,162 / 6,832 | 871,077,943 | 902,574,329 | +3.62% |
| 5.1.0 / ON | 2,163 / 6,833 | 768,811,769 | 808,580,995 | +5.17% |

`JSON.Core.Structural` well-formedness remains Correct in all four lanes. Its constant-empty lambda footprint definition is emitted once while the same child, range, type, allocation and heap guards remain. These declaration totals cover six assertion batches; the TSV does not locate cost inside an individual batch.

| Solver / option | Baseline Structural RU | Candidate Structural RU | Aggregate batches |
|---|---:|---:|---:|
| 4.12.1 / OFF | 4,691,497 | 3,351,842 | 6 |
| 4.12.1 / ON | 4,691,497 | 3,351,842 | 6 |
| 5.1.0 / OFF | 18,327,510 | 2,740,180 | 6 |
| 5.1.0 / ON | 18,327,510 | 2,740,180 | 6 |

`TryStructural` well-formedness is Correct in all four lanes under its unchanged `@ResourceLimit("100e6")`: 3,676,326 RU in each primary lane and 1,366,143 RU in each compatibility lane. This is the recorded well-formedness declaration, not an inferred separate body-correctness verdict. Its universal contract composes the leading WS split, at-most-one-byte SkipByte split, and trailing WS split. The byte equations compose to before+value+after+remaining and the cursor preserves the suffix. At EOF the middle view is empty and satisfies jopt; SplitFrom does not require strict progress. Typed valid view components make the Spec.Structural callback defined.

The following are every matched declaration whose aggregate RU rises by more than twofold and reaches at least 50,000 RU (15 lane cells). The artifact does not contain per-batch RU; this list cannot locate a costly assertion inside an isolated declaration. In particular, `FlattenedProducer.Invoke` rises from 27.85 million to 66.35 million RU in both primary lanes while remaining Correct under its original 100 million override. That material individual regression must be reviewed separately from the smaller aggregate percentages.

| Declaration | Lane | Baseline / candidate aggregate RU | Outcome / reason |
|---|---|---:|---|
| `Arithmetic.DivMod.LemmaModNegNeg (correctness)` | 4.12.1 OFF | 677,252 / 1,582,839 | Correct → OutOfResource; disposition above. |
| `Consumers.ConsumedOfAllAccepted (correctness)` | 4.12.1 OFF | 152,078 / 454,896 | Correct in both. Filtering out any false flag would make the consumed sequence shorter. Equality of consumed/history lengths therefore forces every acceptance flag true; the original induction proves the repeated-true output. |
| `Base64.DecodeValidEncode (correctness)` | 4.12.1 OFF | 117,393 / 294,944 | Correct in both. The unchanged proof composes inverse lemmas for empty, unpadded, one-padding and two-padding cases. |
| `Parsers.StringParsers.String (well-formedness)` | 4.12.1 OFF | 69,348 / 164,753 | Correct in both. The prefix-length guard establishes slice/drop bounds; success returns the expected prefix and remainder, while failure is recoverable. The captured-string callback retains its checked domain. |
| `Arithmetic.DivMod.LemmaModNegNeg (correctness)` | 4.12.1 ON | 662,219 / 1,583,507 | Correct → OutOfResource; disposition above. |
| `Consumers.ConsumedOfAllAccepted (correctness)` | 4.12.1 ON | 152,078 / 454,896 | Correct in both. Filtering out any false flag would make the consumed sequence shorter. Equality of consumed/history lengths therefore forces every acceptance flag true; the original induction proves the repeated-true output. |
| `Parsers.StringParsers.String (well-formedness)` | 4.12.1 ON | 69,437 / 163,727 | Correct in both. The prefix-length guard establishes slice/drop bounds; success returns the expected prefix and remainder, while failure is recoverable. The captured-string callback retains its checked domain. |
| `Producers.FlattenedProducer.Invoke (correctness)` | 5.1.0 OFF | 27,851,594 / 66,352,148 | Correct in both at its unchanged @ResourceLimit("1e8"). Component producer contracts, disjoint/fresh representations, history invariants and the lexicographic decrease justify fetching/skipping inner producers, emitting one value, and reporting exhaustion. This is a material cost increase despite a successful proof. |
| `JSON.ZeroCopy.Deserializer.Numbers.Frac (well-formedness)` | 5.1.0 OFF | 289,025 / 748,885 | Correct in both. Missing period returns Empty; a present period requires a nonempty digit parse. Existing cursor-split contracts justify the Maybe/Frac serialization callback and result. |
| `Parsers.StringParsers.String (well-formedness)` | 5.1.0 OFF | 69,546 / 163,907 | Correct in both. The prefix-length guard establishes slice/drop bounds; success returns the expected prefix and remainder, while failure is recoverable. The captured-string callback retains its checked domain. |
| `Producers.FlattenedProducer.Invoke (correctness)` | 5.1.0 ON | 27,851,594 / 66,352,148 | Correct in both at its unchanged @ResourceLimit("1e8"). Component producer contracts, disjoint/fresh representations, history invariants and the lexicographic decrease justify fetching/skipping inner producers, emitting one value, and reporting exhaustion. This is a material cost increase despite a successful proof. |
| `Base64.DecodeValidEncode1Padding (correctness)` | 5.1.0 ON | 1,132,666 / 5,229,930 | Correct in both at its unchanged 12,000,000 override. The unpadded prefix and final two bytes are inverted separately and concatenated. |
| `Base64.EncodeBVIsBase64 (correctness)` | 5.1.0 ON | 459,840 / 1,060,319 | Correct → OutOfResource; disposition above. |
| `JSON.ZeroCopy.Deserializer.Numbers.Frac (well-formedness)` | 5.1.0 ON | 289,025 / 748,885 | Correct in both. Missing period returns Empty; a present period requires a nonempty digit parse. Existing cursor-split contracts justify the Maybe/Frac serialization callback and result. |
| `Parsers.StringParsers.String (well-formedness)` | 5.1.0 ON | 69,630 / 165,094 | Correct in both. The prefix-length guard establishes slice/drop bounds; success returns the expected prefix and remainder, while failure is recoverable. The captured-string callback retains its checked domain. |

These are observed search costs under Boogie's default declaration order. The library runner documents that resource totals can vary between runs of the same build; they are not an expected-verdict oracle or a cross-run guarantee. Verdict changes are separate from cost-only changes. This comparison does not establish an individual causal solver mechanism for every moved or expensive row.
