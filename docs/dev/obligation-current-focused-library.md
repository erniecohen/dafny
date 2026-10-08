# Current focused standard-library regressions

Semantic/product `fe6ddd1ba` uses the [accepted native compiler build](https://github.com/erniecohen/dafny/actions/runs/37808373912).
This record covers only the previously identified declarations and relevant
controls: 92 observations on macOS arm64 with Z3 5.1.0. Original source,
project options, per-declaration resource overrides, two-core setting and
Boogie's default declaration normalization are retained. No whole-library or
whole-suite gate was launched.

## Completed regression observations

Each original is checked with both additional-axiom settings and seeds 0, 1
and 7. The vectors below agree between the two axiom settings. `V` means
Correct and `R` means OutOfResource.

| Previously identified case | Current enabled vector |
| --- | --- |
| `LemmaDivByMultiple` | VVV |
| `BatchArrayWriter` constructor | VVV |
| `LemmaMapDistributesOverConcat` | VVV |
| `LemmaMapPartialFunctionDistributesOverConcat` | VVV |
| `LemmaFilterDistributesOverConcat` | VVV |
| `WillSplitOnDelim` | VVV |
| `EncodeDecodeRecursively` | VVV |
| `LemmaRemainder` | RRR |
| `LemmaMaxOfConcat` | VVV |
| `MultisetOrdinalDecreasesToSubMultiset` | VRV |

All four earlier functional failures verify in the current focused scope.
Their 24 existing false controls contain genuine Invalid VCs. All three earlier
reveal-scope failures verify at every selected seed. All reported independent
specification-WF results are Correct. The remainder proof still exhausts its
limit at all selected seeds; the multiset proof exhausts its limit at one seed.
Those are resource failures, not Invalid proof results.

The earlier full/library matrices used different product revisions and, in
some cases, different platforms. These current successful cases cannot be
attributed solely to the latest support-order change across that boundary.
The [current suite regression record](obligation-guarded-preparation-focused.md)
also retains unresolved resource failures. Neither this record nor those
focused successes establish uniform improvement or product acceptance.

## Completion and harness boundary

The first frozen job completed 84 observations, then its result checker
incorrectly expected a body-correctness declaration for the `MembersSpec`
function, whose verification result is well-formedness only. Its outer gate
failed; that attempt and its completed observations are preserved. A fresh
job corrects that expected declaration kind and executes only the eight
remaining comparison controls. The original 84 observations are explicitly
reused, and the interrupted partial control is excluded. The combined record
therefore contains 92 completed observations from two immutable artifacts,
not a claim that the original job succeeded.

## Existing default-off cost sample

The eight `MembersSpec` controls use the unchanged baseline binary and the
current binary with obligation preservation disabled, both axiom settings
and two repeats. Every control verifies. The entire printed module is byte
identical across baseline and candidate repeats within each axiom setting.
Actual native solver streams differ. Resource counts also differ between
repeated runs of the unchanged baseline binary under each axiom setting.
Only one of the four baseline/candidate paired resource comparisons agrees
exactly.

This confirms that native cost repeatability is already absent in this sample
under the original library configuration. The fork documents that limitation
in [REVIEW.md](../../REVIEW.md#expected-standard-library-verdicts) and the
[library gate](../../.github/review/std-verdicts.py). The original complete
library gate uses that documented default order. Its configuration is not
changed here. The earlier [captured-query diagnosis](obligation-default-off-query-diagnosis.md)
causally isolates ordering and generated names for its own sampled streams;
these fresh controls do not extend that replay to every current stream.

Strict complete-library resource compatibility remains unaccepted. Baseline
nonrepeatability must be distinguished from a product-caused cost movement;
it is not a waiver or evidence that every remaining movement has that cause.
The remaining known regressions are the next diagnostic scope. Another whole
suite is not an iteration step, and the development port still requires owner
approval.
