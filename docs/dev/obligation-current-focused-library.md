# Current focused standard-library regressions

## Current normal-compiler refresh

The focused refresh completes **96 native observations** on normal structural
compiler `df4c7066f`, build `63fb750a9`, and Z3 5.1.0. It reruns only the ten
known functional, scope and resource cases below and their existing negatives;
six fresh unchanged-baseline Remainder comparisons and six entry-false Remainder
controls complete the scope. The original entire project/source, options,
resource ceilings, two-core setting and default declaration normalization remain.
No whole-library or whole-suite iteration is run.

The vectors are seeds 0/1/7 and agree between the two axiom settings:

| Known case | Current enabled | Fresh baseline in this scope |
| --- | --- | --- |
| `LemmaDivByMultiple` | VVV | Not rerun |
| `BatchArrayWriter` constructor | VVV | Not rerun |
| `LemmaMapDistributesOverConcat` | VVV | Not rerun |
| `LemmaMapPartialFunctionDistributesOverConcat` | VVV | Not rerun |
| `LemmaFilterDistributesOverConcat` | VVV | Not rerun |
| `WillSplitOnDelim` | VVV | Not rerun |
| `EncodeDecodeRecursively` | VVV | Not rerun |
| `LemmaRemainder` | RRR | VVV |
| `LemmaMaxOfConcat` | VVV | Not rerun |
| `MultisetOrdinalDecreasesToSubMultiset` | RVV | Not rerun |

V means Correct and R OutOfResource. All thirty negatives contain genuine Invalid
VCs; Remainder's six new controls identify the inserted source false assertion.
All reported independent specification-WF checks are Correct. The diagnostic gate
completes successfully, but its positive resource failures remain and product
acceptance is false. Remainder is a current stable sampled resource regression,
with a successful fresh baseline under both axiom settings; it is not section
13's both-partial variance. Multiset's current seed-zero failure is retained;
its historical baseline has a separate compiler/platform boundary.

### Quantified preparation remains in the outer continuation

Remainder's current generated body has eight actual assertions: four caller
precondition pieces, two postcondition pieces and two lambda-definedness checks.
The legacy VC has the corresponding checked requires/ensures and the same two
lambda checks. Counts alone do not establish formula or input equivalence.

The current local preparation for `LemmaDivInductionAuto` contains conditional
havocs of fresh quantified-WF binder locals, followed by their can-call support.
The existing normalization rejects conditional havocs; the scope certificate
also rejects all havocs. Consequently the entire caller proof remains in the
outer continuation, rather than the existing locally terminating proof branch.
This is a concrete source/Boogie difference, not yet a causal resource diagnosis.

The [scratch fresh-local scope comparison](https://github.com/erniecohen/dafny/actions/runs/37981555012)
tests a stricter declaration-based certificate without dropping or rewriting
preparation, fuel, actual formulas or full attributes. A havoc is eligible only
if its identifier resolves to a local declared during that same preparation;
existing-variable and unresolved havocs are rejected. Writes still require the
recorded fresh argument bindings. Existing guard structure and command positions
remain. Calls, real assertions, labels, unsupported branches and statement
expressions retain their previous scope policy. No source reveal is moved.
The comparison completes all eighteen native observations on compiler
`ed934ccf2`. The six native controls retain the prior outcomes and resource
counts, and the two seed-zero emitted modules retain their complete noncomment
Boogie token streams. All twelve selected/false variants retain all eight actual
check expressions, full attributes and the entire preparation fingerprint. All
six false-entry controls contain genuine Invalid VCs; independent reported
specification-WF checks remain Correct.

| Preparation policy | Axioms off, seeds 0/1/7 | Axioms on, seeds 0/1/7 |
| --- | --- | --- |
| Unchanged native control | RRR | RRR |
| Existing proof branch with fresh-local eligibility | VVR | VRR |

This is partial benefit, not a complete repair or a product change. Scoping alone
leaves three of the six failures. It preserves conditional havocs and empty
branches; it does not normalize or remove their guarded support.

### Guarded normalization of fresh binders: complete, mixed benefit

The [scratch follow-up compiler](https://github.com/erniecohen/dafny/actions/runs/37987506010)
extends the existing guarded normalization only to resolved locals created by
that same preparation. Every write must occur once. Havoc variables must have
no where clause and be absent from ancestor guards; argument bindings must be
absent from every guard. Existing-state writes and unsupported commands retain
the original policy. Each original fact retains its ancestor guards, expression,
full attributes and token; each original binding and havoc remains once in order.
Empty branches retain their guard terms in tautologies. An independent audit
inspects the normalized implication spine against original expression and
attribute identities. Actual checks, fuel and source reveal visibility remain.
Compiler `4844204ef` passes all eight actual build stages. The complete
comparison contains eighteen native observations at the original options and
limits. All six controls reproduce the prior native outcomes and resource
counts; both seed-zero emitted modules retain their complete noncomment Boogie
token streams. All twelve selected/false variants retain the original raw
preparation and all eight actual check expressions/full attributes, and pass the
independent guarded-fact/binding/havoc retention audit. All six false-entry
controls contain genuine Invalid VCs; reported independent WF checks are Correct.

| Preparation policy | Axioms off, seeds 0/1/7 | Axioms on, seeds 0/1/7 |
| --- | --- | --- |
| Unchanged native control | RRR | RRR |
| Fresh-local scope alone, preceding comparison | VVR | VRR |
| Guarded fresh-binder normalization plus scope | RVR | VVV |

The normalization improves the sampled axioms-on path, but seed zero with axioms
off exhausts again where scope alone succeeded. Thus it is not uniformly better
than the preceding scope experiment and does not repair both supported settings.
No normalization or scope change is adopted. These results distinguish intact
checks/support from proof-search success; they do not identify a missing fact,
justify an axiom-specific product workaround, or close regression acceptance.

## Direct caller assertion comparison: remaining invariance failure

The current normal compiler `df4c7066f` is checked on twenty-four original,
frozen-lambda, immediate-precondition-assertion and source-false observations,
at seeds 0/1/7 in both axiom settings. The entire original project, options,
two-core setting, default ordering, warning policy and ceilings remain. Naming
the existing lambda supplies a separate control for that source change. The
immediate assertions are exactly the callee's two preconditions with actual
`d`/`f` substitutions and the original explicit triggers. They are paired
review diagnostics, never source hints retained as the product repair.

| Source form | Axioms off, seeds 0/1/7 | Axioms on, seeds 0/1/7 |
| --- | --- | --- |
| Original caller | RRR | RRR |
| Same lambda named, no redundant assertion | RRR | RRR |
| Named lambda and immediate precondition assertions | VVV | VVV |

All six original controls reproduce the prior outcomes and costs; both seed-zero
emitted modules retain the complete noncomment Boogie token streams. Six genuine
source-false controls fail at the inserted assertion, and reported independent
WF checks are Correct. The earlier unparenthesized source attempt stopped on the
unchanged warning policy; only this fresh complete warning-clean comparison is
acceptance evidence for the diagnostic. It still does not establish product
acceptance: the redundant assertion supplies help the current implicit path
fails to obtain within the same ceiling. The lambda-binding change alone does
not explain the success. The generated frozen body has eight actual assertions;
the asserted body has thirteen, including the added three split proposition
checks and their legitimate explicit WF/numeric checks. Neither body's caller
preparation contains a `Reads1` term or body reads-frame assertion, so the
omitted reads sink is not established as the cause in this example.

## Certified caller support order: complete, mixed benefit

The [audited compiler](https://github.com/erniecohen/dafny/actions/runs/37989882373),
source `492309dd0`, passes all eight actual stages and completes eighteen native
observations at the original source/project/limits. It constructs the existing
caller can-call expression at its original point, then passes that same command
to the existing certified support-order helper. Movement requires normalized
preparation and independence from every written/havocked private local. All
selected original support commands remain once at the end before the actual
clause check; unsupported fragments retain the original policy.

| Preparation policy | Axioms off, seeds 0/1/7 | Axioms on, seeds 0/1/7 |
| --- | --- | --- |
| Unchanged native control | RRR | RRR |
| Fresh normalization/scope, preceding comparison | RVR | VVV |
| Same preparation plus existing certified caller support order | VVV | RVR |

All six controls retain the prior outcomes/costs and both seed-zero module token
streams. All twelve selected/false variants retain the original raw preparation,
all eight actual check expressions/full attributes, the independent guarded-
fact/binding/havoc audit and both original caller support formulas/full attributes.
Each original support command object remains exactly once. All six genuine false
controls remain Invalid and reported independent WF checks remain Correct. This
is complementary partial benefit, not a uniform repair: the axioms-on zero/seven
successes disappear. No axiom-dependent choice or product policy is adopted.

The source audit also distinguishes command placement from expression creation:
the caller constructs its extra leading can-call expression before the assertion-
WF traversal. That assigns quantified binder names before fresh WF locals, while
an explicit assertion first runs WF and then constructs its can-call support.
The frozen native body materializes locals numbered 3/4/5; the explicit source
preparation materializes 0/2/4. Names alone are not missing semantic facts and do
not prove a fuel difference, but moving an already constructed command does not
replay that construction order. A subsequent local construction-order comparison
must retain actual checks and the complete support formula set, compare fresh
locals by typed declaration roles, and retain genuine negatives before adoption.

## Fresh multiset baseline comparison

A separate eighteen-observation comparison retains the exact original project,
source, limits, two-core setting, default ordering, normal compiler `df4c7066f`
and unchanged baseline `ab210`, with pinned Z3 5.1.0. Seeds 0/1/7 yield baseline
VVV and enabled RVV under both axiom settings. The six fresh enabled observations
reproduce the preceding current outcomes and costs. Six additional entry-false
controls at the actual lemma entry contain genuine Invalid VCs at that line;
all reported independent specification-WF checks are Correct.

This fills the compiler/platform baseline gap. It does not establish section
13's both-partial condition: the baseline succeeds at every selected seed,
while enabled mode exhausts seed zero. The current resource movement remains
open; its successful seeds must not be hidden by reporting only seed zero.
No source proof hint, ceiling increase, broader library/suite run or product
change is made. This is complete diagnostic evidence, not positive acceptance.

## Preceding compiler record

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
