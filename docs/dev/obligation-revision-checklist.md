# Issue 100 revision checklist

**Caller-publication candidate `991b60037` has 68 retained native observations; four additional controls remain pending.**
It preserves the inherited/free-call interface, publishes original contracts for
ordinary locally checked calls, and scopes only certified pure preparation.
Statement-expression reveals and unsupported effects keep their prior outer
scope. The completed scratch diagnostics below motivate this candidate. Its
[normal compiler build](https://github.com/erniecohen/dafny/actions/runs/37914777052)
passes all ten actual stages: both platform/Boogie-probe builds, 104 obligation
tests in each mode, 382 core tests, the unchanged 287-group inventory and the
editor build. The preceding build passes all structural controls but fails only
the old registry comparison; that failed build is retained. The refresh changes
only audited source-layout records, not counts or classifications. The [current native record](obligation-caller-publication-native.md) preserves
68 completed observations and the collection failure requiring only four fresh
controls. FormArmy and AltPrimeDefinition are VVV in both axiom settings;
Composite and RemoveFactor remain unresolved. Review acceptance is incomplete.

Current [native exit-regression diagnosis](obligation-native-exit-regressions.md) completes 36 whole-clause observations and 36 family-isolation observations, explicitly combining twelve RemoveFactor observations with a 24-observation follow-up for the other two cases. Whole-clause preparation is rejected as a resource repair despite identical actual checks. RemoveFactor seed-zero exhaustion is localized to exit translation, while Composite seed1 and FormArmy seed0 exhaustion is reproduced by the body family, with faithful native controls; no product repair or acceptance is claimed. A further 54-observation private-argument substitution diagnostic preserves every actual check and does not repair any target; Composite/FormArmy solver streams remain identical to their controls at every tested seed. A twelve-observation certified caller-order follow-up also leaves the two selected failing seeds unchanged, with faithful controls and strictly retained checks.


**Preceding evidence:** product `fe6ddd1ba` has [68 focused suite/control observations](obligation-guarded-preparation-focused.md)
and [92 completed known-library/control observations](obligation-current-focused-library.md).
The library result explicitly combines 84 preserved observations from a harness-failed
attempt and eight controls from its completed follow-up. Remaining resource failures
and strict default-off cost acceptance stay open. No whole-suite/library run was
launched for this revision. Complete-gate results below belong to earlier pinned
revisions and cannot establish current acceptance.

**Preceding focused result:** semantic/product `fe6ddd1ba`, with the
[accepted compiler build](https://github.com/erniecohen/dafny/actions/runs/37808373912),
completes 68 native observations on macOS arm64 with Z3 5.1.0, both additional-axiom
settings and original resource ceilings. All reported independent specification-WF
results are Correct; all sixteen positive controls verify, all four negative
controls contain genuine Invalid VCs, and six default-off Power vectors exactly
match the recorded baseline. Baseline and preceding `a709df226` results are
explicitly reused. Composite gains one successful seed; Power remains green.
Five suite targets still exhaust resources at selected seeds, so focused enabled
acceptance is rejected. No whole-suite/library job or development port is run.

**Preceding correction:** semantic/product pin `a709df226` replaces empty
certified-WF branches by `G ==> G`, retaining the guard terms without an empty
control-flow split. Its [build](https://github.com/erniecohen/dafny/actions/runs/37781426875)
passes all ten actual stages, both platforms and probes, the editor build,
92 structural observations in each mode, the exact 287-group inventory and all
370 core unit tests. Its [focused native result](obligation-guarded-preparation-focused.md)
completes 68 observations: Power verifies all three seeds in both axiom settings,
with its two actual exit checks byte unchanged. All observed specification-WF
checks, sixteen positive controls and four genuine invalid controls behave as
required. Five suite targets still have resource failures at selected seeds;
targeted enabled acceptance remains false. The earlier baseline evidence is
explicitly reused; only six default-off Power observations are newly executed
and match that baseline exactly. No whole-suite or whole-library run was launched.

**Preceding measured candidate:** semantic correction `338b82396`, product pin
`69dc750b5`, build `861472a9d` passes all ten build stages, 92 structural
observations in both modes, the independent 287-group inventory and all 370 core
unit tests. Its [focused known-regression diagnostic](obligation-guarded-preparation-focused.md)
completes 146 observations: selected default-off vectors agree exactly, but five
of six suite targets still fail at some seeds and Power fails at every seed.
Original issue/subset positives and genuine invalid negative controls retain their
expected results. No new whole-suite or whole-library run was launched.
The complete gates below describe the preceding product `072175269`; they cannot
validate this new candidate. Keep both PRs as drafts and retain the owner's
approval requirement before porting to the development line.

The [requested revisions](obligation-review-change-request.md) are the governing
requirements for the next implementation of PRs [#168](https://github.com/erniecohen/dafny/pull/168)
and [#169](https://github.com/erniecohen/dafny/pull/169). They supersede the additive
check-package design and exclude the later body/context-collection and
definition-support experiments. Preserve the request in full; use this file to
record implementation and evidence against each requirement as revisions land.

**Historical complete-gate status (superseded product):** the declared-contract preparation correction was implemented as
candidate `98b0dd509`, compiled product pin `072175269`. Its accepted build passes
all ten stages, 85 structural observations in capture and normal modes, the
independent 287-group inventory and all 363 core unit tests. Its complete native
registered/paired gates pass 274/676 observations, including 244 genuine invalid
negative observations. Its focused 72-observation library matrix still rejects
enabled Power subtraction at every seed. Its 216-observation matrix and 112-observation actual-source diagnostic finish;
the four targeted original library failures are repaired. Power's stable solver
failure is isolated to the second clause's materialized WF path in a separate
48-observation replay; omissions remain diagnostic. A 24-observation exact-query
replay explains one sampled default-off ordering/cost movement. The [historical complete suite/library gates](obligation-current-complete-gates.md)
finish and reject enabled acceptance. Exact default-off suite compatibility passes;
strict default-off library resource equality remains rejected. Normal registered
and editor integration tests pass with recorded compiler/harness provenance.
All four affected-declaration seed matrices finish with 648 observations and recorded scope/platform limits; independent soundness review remains open. The preceding `58d8ac112` full gates rejected
enabled acceptance and strict default-off library resource equality. Historical
results cannot discharge the current revision's acceptance. Keep both PRs as drafts.
Settle the supported-line design first. The owner requires an explicit approval
before any implementation is ported to #169; keep that port pending until approval.
The development line then requires independent evidence. The [current preparation diagnosis](obligation-local-preparation-diagnosis.md) records the local repair and rejected preceding native evidence.

The target implementation must satisfy:

> Dafny now translates an implicit proof obligation using the same relevant local
> pre-check support as an immediate explicit assertion of that obligation, while
> checking the obligation only once and without consulting unrelated body terms.

## Supported-line implementation: PR #168

Do not mark an item complete from source inspection alone when native verification
is required. Record the implementing commit, exact baseline/build/solver identity,
test scope, completed verdict and remaining limits in the evidence column or a
linked validation report.

| ID / request section | Required change and evidence | Status / evidence |
| --- | --- | --- |
| S1 / 1 | Replace additive packages with one strengthened actual implicit check. Reuse relevant assertion-style preparation and preserve the original kind's publication. Remove duplicate proofs of the same proposition. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S2 / 2 | Check each postcondition locally at every return/fallthrough in the active body reveal scope. Retain a caller-visible contract without a second implementation proof. Preserve inherited `$_reverifyPost`, clause order, earlier-clause facts, diagnostics and dependencies. Verify free/nonchecking Boogie `ensures` semantics with a small direct Boogie test before relying on it. Justify any split-summary publication. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S3 / 3 | Check each instantiated method-call precondition once at the pre-call heap. Preserve callee assumptions without duplicate call checks; verify free/nonchecking `requires` semantics. Preserve receivers, generics, boxing, old arguments, frame/termination checks and call order. Retain only normal call publication. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S4 / 4 | Apply canonical preparation to the existing ordinary and higher-order function-precondition assertions; add no parallel proof package. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S5 / 5 | For visible subset/newtype constraints, check required base membership and the guarded defining constraint with assertion-equivalent support, then derive the symbolic membership fact using its existing introduction rule. Keep hidden constraints abstract. Cover results, assignments, constructor arguments, conversions, defaults/constants, collections and lambdas. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S6 / 6 | Replace duplicate conversion assertions with one guarded constraint proof, followed only by required symbolic/type publication. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S7 / 7 | Use one assertion-equivalent allocation check with existing representation bridges, then derive the downstream representation. Preserve current/old/labeled-old heaps; old-allocation negatives must fail. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S8 / 8 | Audit every source-expressible implicit obligation against the producer inventory, including bounds, nullness, domains, destructors, division/modulo, invariants and iterator/yield contracts. Document specialized internal checks outside the source-proposition criterion. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S9 / 9 | Keep the withdrawn global occurrence/polarity/value fuel rewrite withdrawn. Use the existing explicit-assertion fuel/layer machinery locally; introduce no global expression policy. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S10 / 10 | Ensure support depends only on the proposition and legitimate local verifier state. Reject body/prefix/invariant/context term collection and all later collection/definition-support experiments as product changes to either PR. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S11 / 11 | Fix `ObligationFingerprint` using lexical binder identities/stacks. Distinguish the specified shadowing counterexamples and retain a positive alpha-equivalence control. | Implemented in `426a8d413`; the opcode controls are added in `8ddf1458b`; the binder and opcode controls pass in the [current normal build](https://github.com/erniecohen/dafny/actions/runs/37808373912), with 95 obligation tests in each mode and 373 core tests. |
| S12 / 12 | Add quantified and non-quantified paired assertion-invariance tests for exits, subsets, method/function preconditions, bounds and old allocation. Add no-body-leakage, active scoped-reveal and no-duplicate-contract-proof structural controls. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S13 / 13 | Rerun stable exit-package, exit-summary and call-package regressions on the single-check implementation before solver-specific repairs. Classify partially successful baseline/candidate seed movements as solver/resource variance. Keep source hints and resource ceilings unchanged. | Complete matrices of both preceding revisions and causal replay are documented in [the diagnosis](obligation-local-preparation-diagnosis.md); current complete focused gates and causal/query replays finish; preceding complete suite/library gates finish and remain rejected; all four affected-declaration seed matrices finish and are classified in the [current seed record](obligation-current-seed-classification.md), with platform/scope limits and raw gate rejections retained. |
| S14 / 14 | Rewrite the PR description around single-check assertion-equivalent lowering. Complete every supported-line acceptance item below before requesting approval. | Description rewritten; supported-line acceptance remains pending |

## Supported-line acceptance

The checked registered/paired items below record the completed `072175269`
comparison. They do not discharge current `fe6ddd1ba` acceptance. The newest
68-observation native result above supplies current focused issue/subset,
preparation and Power evidence. Current full registered/paired evidence and
strict default-off library resource compatibility remain open; no full-suite
rerun is authorized as an iteration loop for the existing regressions.

- [x] Original issue 100 source verifies without its redundant final assertion at the existing ceiling (preceding registered/paired gates pass; current focused original/subset controls also pass).
- [x] The subset reproducer verifies without its redundant assertion (preceding registered/paired gates pass; current focused original/subset controls also pass).
- [x] Representative method and function precondition examples verify without redundant assertions (preceding paired gate passes; full current paired acceptance remains open).
- [x] All registered/paired negative controls remain negative (all 244 expected-negative observations in the preceding complete paired gate have invalid VCs; the full-suite ill-formed-specification body movement is documented with its still-failing independent specification-WF).
- [x] Scoped-reveal examples retain their previous proofs at the actual check point (preceding registered/paired controls and current structural controls pass; the current focused library scope cases all pass).
- [ ] Default-off translation, verdicts and resource behavior remain compatible with the supported baseline under the fork's required gates (preceding full-suite verdicts and resource vectors match exactly in both settings; preceding library verdicts and operational Boogie match, but strict library resource equality rejects both settings; current focused cost controls confirm baseline nonrepeatability).
- [ ] Enabled mode has no unexplained stable correctness regression.
- [x] Remaining resource movements are classified with multiple seeds using S13's policy (648 observations; the recovered library failure matrix has an explicit platform boundary and four full-control movements; classification does not imply acceptance).
- [ ] No body/context scanning, new background axiom or global fuel policy is introduced.
- [ ] Producer inventory, normal regression integration, documentation, required suite/library gates and independent soundness review are complete.

## Development port: PR #169

| ID | Required change and evidence | Status / evidence |
| --- | --- | --- |
| D1 | Wait for S1-S14's semantic design to settle and obtain explicit owner approval before porting; port that same single-check invariant, without an independently evolved solution or later scratch experiments. Adapt to the current development verifier architecture. | Pending |
| D2 | Port the lexical `ObligationFingerprint` fix and all relevant controls. | Pending |
| D3 | Run the paired assertion-invariance tests, original issue 100, non-quantified subset/call cases, reveal-scope controls and negatives natively on the development build. Supported-line results cannot substitute. | Pending |
| D4 | Run the development verifier suite and standard library under the fork's required configurations. Compare default-off behavior with the actual development baseline and classify multi-seed movements using S13. | Pending |
| D5 | Confirm the development-specific producer inventory; document branch-specific lowering and soundness differences, exact evidence scope and remaining limits. Request approval only after development-specific acceptance. | Pending |

## Updating this record

Keep the full request unchanged. Update statuses and evidence here as commits land,
and link current validation from both PR descriptions. A successful focused case,
public build or solver replay does not complete a whole-suite/library requirement.
Do not close an item merely because a superseded implementation passed its tests.


The [native solver-profile diagnosis](obligation-native-exit-regressions.md#faithful-solver-profiles-distinguish-the-remaining-searches)
adds completed faithful twelve- and six-observation captures at existing failing
seeds. They distinguish active quantifiers without establishing a resource
repair. Paired user-defined method-call isolation now completes twelve observations and
localizes both selected body failures to that producer. Its six-observation preparation-emission
follow-up completes with strictly identical actual checks: Composite improves
only after that diagnostic support omission, while FormArmy still exhausts
resources. The omission is excluded as a product fix. The three-observation split caller-publication follow-up also completes with
strictly identical actual checks, all pre-check support retained and faithful
native/invalid controls. FormArmy seed 0 still exhausts resources with the two
extra summaries omitted; that hypothesis is rejected as a repair. Remaining
resource acceptance and the other outstanding review requirements stay open.


The [support-preserving caller placement comparison](obligation-native-exit-regressions.md#existing-background-assertions-and-the-sole-legacy-caller-proof)
completes four observations with faithful baseline/current controls and an
Invalid negative. All six substituted procedure preconditions match the generated
local formulas. FormArmy seed 0 still exhausts resources when the full new
preparation precedes a sole legacy call proof; relocation is unnecessary to
reproduce that selected failure. Active body background assertions also match
baseline exactly in all three selected cases. The completed three-observation private-argument allocatedness
isolation restores the selected hybrid proof after omitting only two allocation
assumptions. All six check formulas/full generated attributes remain identical,
all other preparation is retained, and the native/Invalid controls are faithful.
This is a conditional diagnosis; the full current path previously still failed
with all caller WF omitted. Removing legitimate support is excluded as a product
fix. The PR description is rewritten around the requested design;
S14 acceptance and the remaining review requirements remain open.


The [combined caller-emission diagnosis](obligation-native-exit-regressions.md#combined-caller-emissions-explain-the-remaining-selected-search)
completes five full-current FormArmy observations with two faithful complete-SMT
controls, a genuine Invalid control and all fifteen actual checks/full attributes
strictly unchanged. Omitting WF or split summaries alone still exhausts resources;
omitting both restores the selected proof. Leading can-call omission is not
needed for that restoration. This explains a selected bounded search without
authorizing product support deletion or establishing multi-seed acceptance.
Remaining review acceptance stays open.


The [support-preserving caller scope experiment](obligation-native-exit-regressions.md#support-retained-in-local-caller-proof-scopes)
preserves every original preparation and sole check. Four individual original/
scoped observations complete with faithful native controls and unchanged actual
formulas/full attributes. FormArmy seed zero verifies when scoped; Composite seed
one still exhausts resources. Both new scoped false controls exhaust resources
and are inconclusive, so their complete diagnostic gates reject acceptance.
No product repair, uniform improvement or current review acceptance is claimed.
General call publication and negative validation remain required before adoption.


The [FormArmy scope seed follow-up](obligation-native-exit-regressions.md#formarmy-scope-follow-up-at-all-existing-seeds)
executes all eighteen observations with six faithful native controls, twelve
strict actual-check/attribute comparisons and exact reproduction of the earlier
scoped seed-zero positive. Scoped FormArmy is VVV in both axiom settings, against
current RRR, without removing its own check preparation or changing fuel/limits.
The distinct before-check negative is IRR in both settings; its two Invalid
results identify the inserted false assertion, while four OOR results remain
inconclusive. Validation is rejected. This does not settle general publication,
remaining regressions, negative validation or current review acceptance; product
adoption remains pending.


The [normal call-contract publication follow-up](obligation-native-exit-regressions.md#original-call-contract-publication-with-a-local-proof-scope)
retains eighteen completed FormArmy observations and adds only the four unfinished
direct false-precondition controls after a scratch count-audit correction.
FormArmy is VVV in both axiom settings with original call-contract publication;
all ten negative observations are genuinely Invalid. Six native full-stream/cost
controls and fourteen strict actual-check/attribute comparisons pass. This is
twenty-two observations across two frozen gates, not a successful single run.
General adoption must preserve inherited/filtered free-call publication and
outer reveal scope. Remaining regressions and review acceptance stay open.


The new direct Boogie publication gate completes all eleven observations with
expected successful/error summaries, including the false local proof branch,
inherited/free-call exclusion and post-call state-change negatives. It uses the
exact pinned package bytes matched to the accepted compiler and Z3 5.1.0, at
unchanged limits. This validates the contract-publication mechanism, not the
pending seventy-two-observation native Dafny regression gate or overall review
acceptance.
