# Local preparation diagnosis for the single-check replacement

This analysis belongs to PR #168 and the [requested revisions](obligation-review-change-request.md), especially S1, S2, S5, S9, S10 and S13. It concerns semantic revision `2ad5d25a5`, built from `cbe74d128` in the [63-control build](https://github.com/erniecohen/dafny/actions/runs/37713397830). That build passed compilation, the structural controls and the normal reviewed producer-inventory gate. Native source acceptance nevertheless failed. The source preparation repair is `f6c230fb0`, its existential-state handoff correction is `f5c5abf18`, and the current reviewed source/inventory pin is `a08a9485c`. The first repair build compiled both platforms but rejected a structural fixture syntax error and outdated inventory; both have been corrected. The [current build](https://github.com/erniecohen/dafny/actions/runs/37717887176) now passes all 71 structural controls and the normal reviewed inventory. Its complete registered native gate passes all 242 observations. Complete paired, suite/library and independent soundness acceptance remain pending. No implementation has been ported to PR #169.

## Original exit obligation

The unchanged original issue 100 still exhausted its original resource ceiling. The otherwise corresponding explicit-assertion and final-`assert true` controls succeeded. The sole implicit postcondition had the assertion splitter's formula and layer policy, but merely granting the declared clause's can-call permission omitted the immediate assertion's local well-formedness preparation. Quantifier preparation includes fresh local binder setup and source-local can-call checks/terms, so matching the final formula alone is insufficient.

The generated Boogie also differed in preceding scope commands: adding a final statement makes earlier statements nonterminal. A controlled replay separated this difference from local preparation. At seeds 0, 1 and 7, adding only the immediate assertion's well-formedness preparation made the original exit proof succeed while keeping its original scope commands. Changing only the preceding scope commands did not. The version with the same local preparation and a false postcondition failed with an invalid verification condition.

The repair calls the existing assertion well-formedness path on the actual clause, then lowers its sole postcondition check using the same fresh translator and resulting local fuel state, without resetting an existential adjustment consumed by preparation. A nested-existential structural control compares this state with the immediate assertion under both resolvers. It changes no earlier scopes and searches no body terms. Inherited source clauses retain their reverification guard around local preparation; inherited iterator clauses retain their exclusion. Clause order, declared permission and post-check publication remain as described in [the construction](obligation-lowering.md).

## Quantified initializer type obligation

The `array-initializer` positive failed its subset constraint under both resolvers and both additional-axiom settings. The defining constraint used a wrapper around the already translated initializer result. That wrapper correctly freezes evaluation but no longer exposes the application domain that source `CanCall(C(init(indices)))` includes. The constructor's separate domain assertion uses check-and-forget, so its `RequiresN` term cannot be relied upon as a subsequent published fact.

The replay restored the frozen application's domain in the local constraint guard, under the existing index range. This made the positive succeed at all three seeds. The corresponding guarded false constraint still failed with an invalid verification condition. Publishing the earlier domain assertion also made the positive succeed in a diagnostic variant, but would change the original continuation policy and is excluded from the product repair.

The repair checks the guarded constraint once. The independent existing range-bound domain check remains mandatory and retains check-and-forget. Base membership and any derived symbolic representation carry the same domain guard; no unconditional domain/can-call assumption is introduced. The initializer handle is captured at its original evaluation point and is not translated again.

## Iterator preparation context

The first native paired trial of the local preparation revision (`a08a9485c`)
accepted its first 476 observations, then rejected a yield negative control with
an undeclared `$_ReadsFrame`. Its fresh two-state yield translator had a default
method reads-frame name, while iterator bodies intentionally disable method
reads checks: an iterator's reads clause denotes locations retained across yield,
not a method's readable frame. The new WF replay made that incorrect context
observable. An internal translation error is not accepted negative evidence.

The repair in `126dfd96b` preserves the body's existing translator/frame policy
and replaces only its old heap with the exact saved iteration heap for the yield
clause. Inherited yield clauses keep their exclusion from preparation/checks.
Body-specific structural controls resolve and typecheck the generated Boogie,
retain the saved old heap and reject an undeclared method-frame reference. The
current reviewed source pin is `8e7f54505`; its own build and native acceptance
are pending. Previously prepared full comparisons of the earlier revision will
not be submitted as acceptance of this correction.

The private paired adapter also now treats a missing/empty verification log as
rejected evidence and continues the remaining observations. It must never count
an internal error as a successful negative control. This changes evidence
collection only; all source, solver options, resource limits and expectations
remain fixed.

## Evidence boundaries

The controlled generated-Boogie experiment completed all 42 observations, covering original/explicit controls, isolated preparation/scope/literal/domain changes, three seeds and invalid false controls. Literal-anchor and published-domain variants are diagnosis only and are excluded from the source repair. These replays establish causal evidence for these examples, not source-level acceptance, universal assertion invariance or independent soundness review.

The complete paired source run finished all 656 observations before the repair. The original exit and initializer positives were rejected on all four resolver/axiom combinations. Every one of the 232 negative observations contained an invalid verification condition. Four intentional empty-domain negatives also carried a missing-trigger warning outside the private adapter's narrow warning allowance; their invalid outcomes were retained, and the next adapter permits only that fixture's existing warning. No fixture source, solver hint, resource ceiling or batch policy is changed.

The registered gate stopped at its original issue 100 failure; it is incomplete. None of these gates establishes repository acceptance. The correction needs a fresh normal inventory gate, complete registered/paired gates and suite/standard-library comparisons.

## Seed classification before the repair

The complete six-file diagnostic matrix and original/explicit/final-true controls used baseline `ab210b78b`, candidate default-off and candidate enabled modes, both additional-axiom settings and seeds 0, 1 and 7. The complete standard-library diagnostic matrix used the same configurations. Default-off retained every compared baseline batch outcome/resource vector in these focused scopes. This is not full-suite default-off acceptance.

The table records enabled target outcomes at seeds **0 / 1 / 7**. `V` means verified; `R` means resource exhaustion. Baseline and default-off agree in every row.

| Example | Baseline/off | Enabled, axioms off | Enabled, axioms on | Classification |
| --- | --- | --- | --- | --- |
| NoTypeArgs | V / R / V | R / V / V | R / V / V | Both modes partly successful: solver/resource variance. |
| ExtensibleArray | V / V / V | R / V / R | R / V / R | Enabled result is seed dependent; resource regression, not evidence of a false source contract. |
| SchorrWaite | V / R / V | R / R / R | R / R / R | Enabled resource regression persists across these seeds; acceptance remains open. |
| FlyingRobots | V / R / R | R / R / R | R / R / R | Enabled resource regression persists across these seeds; acceptance remains open. |
| MinWindowMax | V / V / R | R / R / R | V / R / V | Additional-axiom and seed dependent resource movement; acceptance remains open. |
| Primes | V / R / V | R / V / V | R / V / V | Both modes partly successful: solver/resource variance. |

The original exit exhausted resources in every matrix configuration before this repair; explicit and final-true controls verified in each. Filter concatenation, delimiter splitting and Base64 round-trip verified enabled at every seed. The power subtraction target had an enabled invalid result at seed 0 but verified at seeds 1 and 7, with either additional-axiom setting. It is a seed-dependent solver result under S13, and receives no case-specific source hint or product special case.

The focused matrices are diagnostic evidence. The new preparation revision must be measured separately, and full-suite/default-off compatibility, ordinary CI and independent soundness review remain required.
