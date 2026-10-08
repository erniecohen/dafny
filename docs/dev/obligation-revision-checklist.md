# Issue 100 revision checklist

**Current candidate:** semantic correction `338b82396`, product pin
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

**Status:** the declared-contract preparation correction is implemented as
candidate `98b0dd509`, compiled product pin `072175269`. Its accepted build passes
all ten stages, 85 structural observations in capture and normal modes, the
independent 287-group inventory and all 363 core unit tests. Its complete native
registered/paired gates pass 274/676 observations, including 244 genuine invalid
negative observations. Its focused 72-observation library matrix still rejects
enabled Power subtraction at every seed. Its 216-observation matrix and 112-observation actual-source diagnostic finish;
the four targeted original library failures are repaired. Power's stable solver
failure is isolated to the second clause's materialized WF path in a separate
48-observation replay; omissions remain diagnostic. A 24-observation exact-query
replay explains one sampled default-off ordering/cost movement. The [complete current suite/library gates](obligation-current-complete-gates.md)
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
| S11 / 11 | Fix `ObligationFingerprint` using lexical binder identities/stacks. Distinguish the specified shadowing counterexamples and retain a positive alpha-equivalence control. | Implemented in `426a8d413`; the opcode controls are added in `8ddf1458b`; the binder and opcode controls pass in the [current 85-observation normal build](https://github.com/erniecohen/dafny/actions/runs/37731304101). |
| S12 / 12 | Add quantified and non-quantified paired assertion-invariance tests for exits, subsets, method/function preconditions, bounds and old allocation. Add no-body-leakage, active scoped-reveal and no-duplicate-contract-proof structural controls. | Implemented across `d93ff6137`, `874f87b64`, `8ddf1458b`, `64d02eb77` and `2ad5d25a5`; native acceptance pending |
| S13 / 13 | Rerun stable exit-package, exit-summary and call-package regressions on the single-check implementation before solver-specific repairs. Classify partially successful baseline/candidate seed movements as solver/resource variance. Keep source hints and resource ceilings unchanged. | Complete matrices of both preceding revisions and causal replay are documented in [the diagnosis](obligation-local-preparation-diagnosis.md); current complete focused gates and causal/query replays finish; current complete suite/library gates finish and remain rejected; all four affected-declaration seed matrices finish and are classified in the [current seed record](obligation-current-seed-classification.md), with platform/scope limits and raw gate rejections retained. |
| S14 / 14 | Rewrite the PR description around single-check assertion-equivalent lowering. Complete every supported-line acceptance item below before requesting approval. | Pending |

## Supported-line acceptance

- [x] Original issue 100 source verifies without its redundant final assertion at the existing ceiling (current complete registered/paired gates pass).
- [x] The subset reproducer verifies without its redundant assertion (current complete registered/paired gates pass).
- [x] Representative method and function precondition examples verify without redundant assertions (current paired gate passes).
- [x] All registered/paired negative controls remain negative (all 244 expected-negative observations in the current complete paired gate have invalid VCs; the full-suite ill-formed-specification body movement is documented with its still-failing independent specification-WF).
- [x] Scoped-reveal examples retain their previous proofs at the actual check point (current registered/paired and structural controls pass).
- [ ] Default-off translation, verdicts and resource behavior remain compatible with the supported baseline under the fork's required gates (current full-suite verdicts and resource vectors match exactly in both settings; current library verdicts and operational Boogie match, but strict library resource equality rejects both settings).
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
