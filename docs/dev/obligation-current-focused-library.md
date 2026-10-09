# Current focused standard-library regressions

**Current candidate:** [canonical caller construction native evidence](obligation-canonical-caller-native.md) records the normal `b3888f933` compiler, complete 310 registered observations, successful Remainder seed vectors, 96 known-library observations and the explicitly combined 72 known-suite/control observations. Remaining resource and review acceptance requirements stay open; preceding results below retain their original compiler identities.


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


## Standard construction order preserves support but does not repair Remainder

The [standard-construction compiler](https://github.com/erniecohen/dafny/actions/runs/37993156829)
passes all eight actual build stages. The complete thirty-six-observation native
comparison covers original and named-lambda sources, seeds 0/1/7, both added-axiom
settings, unchanged controls, selected construction and entry-false controls.
The selected path omits the extra early can-call construction and uses the
complete can-call support already generated by standard statement-WF traversal
after WF. Fresh-state normalization and the certified local proof scope remain
as in the preceding diagnostic.

All twelve native controls retain outcomes and resource counts; all four
seed-zero module token streams match their original controls. All twenty-four
selected/control comparisons retain the eight actual check formulas and full
attributes, complete unique can-call formulas and attributes, and raw preparation
under a captured typed fresh-declaration role frame. The independent audit
retains every original guarded fact, binding and havoc. All twelve false-entry
controls have genuine Invalid VCs; independent specification WF remains Correct.

| Source form | Native, either axiom setting | Selected, axioms off | Selected, axioms on |
| --- | --- | --- | --- |
| Original | RRR | RRR | VVR |
| Named lambda | RRR | RRR | RVV |

Only four of twelve selected positive observations verify. Construction order
therefore changes search behavior but does not close the governing requirement.
All support-preservation controls pass; neither missing complete can-call facts
nor reduced actual-check fuel is established by this comparison. No diagnostic
lowering is adopted into product `991b60037`.

## The individual Remainder obligations verify

A separate complete eighteen-observation native diagnosis uses the normal
`df4c7066f` compiler at seed zero. It covers original, named-lambda and immediate
explicit-precondition source forms in both axiom settings, with unchanged,
assertion-isolated and isolated entry-false variants. Original source bytes,
project, warning policy, core count, declaration order and per-batch ceiling
remain fixed. Isolation changes VC context, batching and total available budget;
it is a diagnosis, not a product repair or acceptance at the original combined
ceiling.

All six native controls retain their full module streams, outcomes and resource
counts. Every individual check in all six isolated positive observations verifies:
eight batches for original/named-lambda callers and thirteen for the explicit
source assertions. All six false-entry controls have a genuine Invalid VC at
the exact inserted assertion, and independent specification WF remains Correct.
The original/named-lambda combined VCs still exhaust resources; the combined
explicit-assertion VCs verify. Thus no individual obligation is unproved in this
bounded diagnostic. The remaining failure occurs in their combined solver
context, rather than establishing an absent fact or less fuel at one check.

The three emitted precondition expressions also match their immediate explicit
assertion counterparts after accounting for existing frozen argument equalities
and bound-variable names. Their attributes expose a separate difference:
explicit split assertions use `subsumption 0`, while implicit caller pieces keep
ordinary publication. Both later publish the complete proposition. Making prior
pieces available immediately can change matching in the combined VC; formula
equality alone does not prove that this publication difference causes exhaustion.

The [scratch publication comparison](https://github.com/erniecohen/dafny/actions/runs/37994951374)
now completes all twelve native observations. Both original and named-lambda
sources still exhaust resources at seed zero in both axiom settings. All four
native full-module/outcome/resource controls, eight actual-expression/full-original-
metadata and command-retention comparisons, four genuine Invalid controls and
independent WF controls pass. Thus changing only the three split-publication
attributes does not repair Remainder. Reject it; product publication stays intact.

## One large source assertion is sufficient

The preceding successful source comparison inserted both `assert d > 0` and an
assertion of the larger induction precondition. A complete twelve-observation
native comparison now separates them at seed zero, in both axiom settings, with
the normal compiler, original combined ceiling and no assertion isolation.

| Named-lambda source form | Axioms off | Axioms on |
| --- | --- | --- |
| No redundant assertion | R | R |
| Both immediate precondition assertions | V | V |
| Only `assert d > 0` | R | R |
| Only the larger precondition assertion | V | V |

All four unchanged controls retain the complete module streams, outcomes and
resource counts. All four new false-entry controls fail genuinely at their exact
inserted assertion, and independent specification WF remains Correct. The larger
proposition alone supplies the essential help in this paired source comparison;
the extra positivity assertion is not responsible. These source assertions remain
diagnostics, not proof hints to be adopted into the product.

A [captured-argument compiler](https://github.com/erniecohen/dafny/actions/runs/37997471051)
passes all eight actual compiler stages. Its first native control stops before
proof because the scratch audit requires an RHS inferred type before normal
Boogie resolution. There are zero complete observations: this failed execution
is retained and is not proof evidence or a conclusion about substitutions. A
[corrected audit build](https://github.com/erniecohen/dafny/actions/runs/37999108823)
is in progress. It checks the original assignment target against its declared
type, rejects a differing known RHS type, records unset inferred types explicitly,
and retains normal Boogie resolution/typechecking.

The same narrow substitution comparison captures only the existing
call-argument freeze assignments and the same actual caller checks. Selected
checks use the already computed argument value expressions through those exact
typed binding equalities, with no reevaluation. All binding, preparation, support,
complete summary, call-interface and metadata commands remain. Value stability,
binder capture and full original/projected formula controls are mandatory; native
projection observes existing ASTs without constructing new ones. This tests
local substitutions, not arbitrary surrounding body terms. No new axiom, fuel or
publication policy, extra proof, raised ceiling, batching change or product
adoption is proposed. No whole-suite/library iteration or development port is
involved.

## Captured argument values: complete, no repair

The [corrected audit compiler](https://github.com/erniecohen/dafny/actions/runs/37999108823)
passes all eight actual stages. The complete twelve-observation native gate covers
original and named-lambda source forms at seed zero in both axiom settings. All
four native full-module/outcome/resource controls match. All eight original and
projected actual-formula/full-metadata comparisons, typed binding and value-stability
controls, retained preparation/support/summary/command-order controls and normal
Boogie typechecking pass. All four entry-false controls remain genuinely Invalid
and independent specification WF is Correct. Every selected positive still
exhausts resources. Reject argument substitution as the repair.

The complete correctness query is identical to native for the named-lambda form
in both settings; the original form's query changes but still exhausts resources.
The initial runner's optional query hash selected the independent WF log. Offline
collection corrects this using the separate correctness log, validates the two
declarations and single-VC log shape and outcomes, and requires every false-entry
query to differ. Raw logs and the original hash fields are retained. No result
is inferred from the WF-only hash. The earlier pre-inference audit failure also
remains separately retained, with zero complete proof observations.

The earlier construction comparison also selected fresh normalization and scope
extensions. It therefore does not isolate standard construction alone. A
[construction-only compiler](https://github.com/erniecohen/dafny/actions/runs/37999913438)
is building the next narrow comparison: original normalization and scope policy
in every variant, standard WF complete can-call support retained before the same
actual checks, and only the extra early caller support construction omitted.
Complete support formulas/attributes, raw and normalized typed fresh-state
preparation, actual check formulas/metadata, negative and independent WF controls
are mandatory. No unique support, reveal effect, fuel or publication policy is
removed. No diagnostic has been adopted into the product.

## Standard construction alone: all selected seeds verify

The [construction-only compiler](https://github.com/erniecohen/dafny/actions/runs/37999913438)
passes all eight actual build stages. Its complete seed-zero comparison contains
twelve observations; all four selected positives verify and all preservation,
false-entry and independent WF controls pass. The separate complete thirty-six-
observation follow-up keeps the same compiler and tests seeds 0, 1 and 7 in both
axiom settings, for original and named-lambda source forms.

| Source form | Native, either setting | Standard construction, axioms off | Standard construction, axioms on |
| --- | --- | --- | --- |
| Original | RRR | VVV | VVV |
| Named lambda | RRR | VVV | VVV |

All twelve native outcome/resource controls and four complete seed-zero module
controls match. All twenty-four comparisons retain the eight actual check
formulas and full metadata, both raw and normalized preparation through typed
fresh-declaration roles, original normalization/scope policy, and the complete
unique can-call support formulas/attributes before the check. All twelve
entry-false controls remain genuinely Invalid; independent specification WF is
Correct. The original combined ceiling, warning policy, source and project
options, declaration order and core count remain fixed. No assertion isolation,
positive source hint, extra proof, fuel/publication rewrite or fresh-scope
extension is selected.

This isolates a successful canonical construction path, rather than the earlier
compound normalization/scope experiment. The general product candidate
`b3888f933` removes the extra early caller can-call construction and uses the
complete support already emitted by the standard statement-WF path after WF,
before the same sole actual check. It adds a structural witness/support-order
control and registered quantified implicit/explicit and false-source fixtures.
Normal compiler and relevant native gates are pending. Scratch evidence is not
relabeled as product evidence, closure of the other regressions, complete
default-off compatibility, or independent soundness review.
