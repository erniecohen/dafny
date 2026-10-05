# Registered issue-113 suite verdicts

These 133 new rows come from [the first full development gate](https://github.com/erniecohen/dafny/actions/runs/37243414478), source `500df6ecd0680991d4ce4732cfe305a6ba319dfb`, Z3 5.1.0. Each row is a newly registered input, rather than a changed old verdict. First-RUN options are preserved by the canonical suite planner. The feature-off/on output pairs have separate measured goldens; runtime execution and the complete strict gate remain separate acceptance requirements.

The old `BoundedPolymorphismCompilation.dfy` success row is preserved. Its prerequisite-composition crash and the changed standard-library rows are not accepted by this update.

| New input | Expected exit and summary | Reason for registration |
|---|---|---|
| `git-issue-113-dependency-newtype-finite-actual-positive-triggered.dfy` | 0 / 2 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-dependency-newtype-general-arrow-characteristic-negative.dfy` | 2 | New existing-language / feature-off control admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-dependency-newtype-hidden-characteristic-negative.dfy` | 2 | New existing-language / feature-off control admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-dependency-newtype-mixed-subset-cycle-negative.dfy` | 2 | New existing-language / feature-off control admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-dependency-newtype-partial-total-positive.dfy` | 0 / 3 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-dependency-newtype-partial-total-universal-proof-nonvacuity.dfy` | 4 / 1 verified, 3 errors | New final-false control and two deliberately false old-allocation postconditions; all three expected obligations fail. |
| `git-issue-113-dependency-newtype-partial-total-universal-proof.dfy` | 0 / 7 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-dependency-newtype-reference-characteristic-negative.dfy` | 2 | New existing-language / feature-off control admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-dependency-provided-characteristic-positive.dfy` | 0 / 0 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-dependency-provided-inhabited-false-negative.dfy` | 4 / 0 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 17. |
| `git-issue-113-dependency-provided-newtype-no-characteristic-negative.dfy` | 2 | New existing-language / feature-off control admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-dependency-provided-no-characteristic-negative.dfy` | 2 | New existing-language / feature-off control admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-dependency-revealed-control-positive.dfy` | 0 / 0 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-existing-arrow-raw-arrow-subset-negative.dfy` | 4 / 1 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 9. |
| `git-issue-113-existing-arrow-raw-arrow-subset-nonvacuity-negative.dfy` | 4 / 1 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 11. |
| `git-issue-113-existing-arrow-raw-arrow-subset-positive.dfy` | 0 / 4 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-existing-arrow-runtime-control.dfy` | 0 / 2 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-existing-arrow-variance-control.dfy` | 4 / 2 verified, 4 errors | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 8,12,15,18. |
| `git-issue-113-existing-codata-suspension-destructor-refinement-negative-raw.dfy` | 4 / 1 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 8. |
| `git-issue-113-existing-codata-suspension-generic-carrier-change-negative-raw.dfy` | 4 / 0 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 7. |
| `git-issue-113-existing-codata-suspension-helper-boundary-valid-rejected-raw.dfy` | 4 / 0 verified, 1 error | New conservative completeness-loss control for a semantically valid constructor helper; the repaired co-recursion boundary rejects it at lines 8. |
| `git-issue-113-existing-codata-suspension-inhabited-final-false-raw.dfy` | 4 / 0 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 12. |
| `git-issue-113-existing-codata-suspension-let-refinement-negative-raw.dfy` | 4 / 1 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 8. |
| `git-issue-113-existing-codata-suspension-match-refinement-negative-raw.dfy` | 4 / 1 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 8. |
| `git-issue-113-existing-codata-suspension-productive-universal-raw.dfy` | 0 / 6 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-existing-codata-suspension-strengthened-result-negative-raw.dfy` | 4 / 0 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 8. |
| `git-issue-113-existing-codata-suspension-unconditioned-predicate-negative-raw.dfy` | 4 / 0 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 9. |
| `git-issue-113-existing-codata-suspension-unguarded-negative-raw.dfy` | 4 / 0 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 7. |
| `git-issue-113-existing-codatatype-prefix-nequality-trigger-control.dfy` | 0 / 4 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-existing-codatatype-subset-control.dfy` | 4 / 1 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 8. |
| `git-issue-113-existing-codatatype-subset-field-negative.dfy` | 4 / 1 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 7. |
| `git-issue-113-existing-codatatype-subset-nonvacuity-negative.dfy` | 4 / 0 verified, 2 errors | New mixed control preserving both the unguarded recursive-call termination error and final false assertion. |
| `git-issue-113-existing-codatatype-subset-result-negative.dfy` | 4 / 1 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 8. |
| `git-issue-113-existing-datatype-base-trait-control.dfy` | 0 / 8 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-existing-sequence-control.dfy` | 0 / 2 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-existing-sequence-newtype-allocation-negative.dfy` | 4 / 2 verified, 1 error | New existing-language / feature-off control proof-boundary or nonvacuity negative; the intended obligation fails at lines 20. |
| `git-issue-113-existing-sequence-newtype-allocation-preallocated.dfy` | 0 / 3 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-existing-sequence-newtype-allocation-pure.dfy` | 0 / 3 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-arrow-allocation-negative.dfy` | 4 / 3 verified, 13 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 26,36,47,58,68,78,79,88,105,106,113,129,130. |
| `git-issue-113-extended-newtypes-arrow-allocation-positive.dfy` | 0 / 2 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-arrow-intermediate-negative.dfy` | 4 / 1 verified, 3 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 9,12,17. |
| `git-issue-113-extended-newtypes-arrow-intermediate-positive.dfy` | 0 / 3 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-arrow-membership-false-control.dfy` | 4 / 0 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 10. |
| `git-issue-113-extended-newtypes-arrow-membership-negative.dfy` | 4 / 1 verified, 5 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 10,13,16,19,22. |
| `git-issue-113-extended-newtypes-arrow-membership-positive.dfy` | 0 / 3 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-arrow-nominal-arrow-subset-negative.dfy` | 4 / 1 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 9. |
| `git-issue-113-extended-newtypes-arrow-nominal-arrow-subset-nonvacuity-negative.dfy` | 4 / 1 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 11. |
| `git-issue-113-extended-newtypes-arrow-nominal-arrow-subset-positive.dfy` | 0 / 3 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-arrow-positive.dfy` | 0 / 21 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-arrow-proof-negative.dfy` | 4 / 1 verified, 8 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 12,25,29,33,37,43,49,54. |
| `git-issue-113-extended-newtypes-arrow-resolver-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-arrow-runtime.dfy` | 0 / 2 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-arrow-variance-control.dfy` | 4 / 6 verified, 4 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 8,12,17,22. |
| `git-issue-113-extended-newtypes-codata-suspension-destructor-refinement-negative-nominal.dfy` | 4 / 1 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 7. |
| `git-issue-113-extended-newtypes-codata-suspension-generic-carrier-change-negative-nominal.dfy` | 4 / 0 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 7. |
| `git-issue-113-extended-newtypes-codata-suspension-helper-boundary-valid-rejected-nominal.dfy` | 4 / 1 verified, 1 error | New conservative completeness-loss control for a semantically valid constructor helper; the repaired co-recursion boundary rejects it at lines 8. |
| `git-issue-113-extended-newtypes-codata-suspension-inhabited-final-false-nominal.dfy` | 4 / 0 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 12. |
| `git-issue-113-extended-newtypes-codata-suspension-let-refinement-negative-nominal.dfy` | 4 / 1 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 7. |
| `git-issue-113-extended-newtypes-codata-suspension-match-refinement-negative-nominal.dfy` | 4 / 1 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 7. |
| `git-issue-113-extended-newtypes-codata-suspension-productive-universal-nominal.dfy` | 0 / 14 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-codata-suspension-strengthened-result-negative-nominal.dfy` | 4 / 0 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 8. |
| `git-issue-113-extended-newtypes-codata-suspension-unconditioned-predicate-negative-nominal.dfy` | 4 / 0 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 9. |
| `git-issue-113-extended-newtypes-codata-suspension-unguarded-negative-nominal.dfy` | 4 / 0 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 7. |
| `git-issue-113-extended-newtypes-codatatype-constrained-result-negative.dfy` | 4 / 0 verified, 4 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 8,14,19,23. |
| `git-issue-113-extended-newtypes-codatatype-cycle-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-codatatype-default-negative.dfy` | 4 / 0 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 5. |
| `git-issue-113-extended-newtypes-codatatype-field-constraint-negative.dfy` | 4 / 1 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 12. |
| `git-issue-113-extended-newtypes-codatatype-field-refinement-proof-negative.dfy` | 4 / 1 verified, 4 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 11,14,17,25. |
| `git-issue-113-extended-newtypes-codatatype-floating-dev-positive.dfy` | 0 / 1 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-codatatype-hidden-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-codatatype-positive.dfy` | 0 / 21 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-codatatype-proof-negative.dfy` | 4 / 1 verified, 7 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 8,12,16,23,29,35,41. |
| `git-issue-113-extended-newtypes-codatatype-resolver-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-codatatype-runtime.dfy` | 0 / 5 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-datatype-base-trait-nominality-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-datatype-base-trait-positive.dfy` | 0 / 8 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-datatype-cycle.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-datatype-descriptor-runtime.dfy` | 0 / 2 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-datatype-floating-negative.dfy` | 4 / 0 verified, 6 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 13,14,15,16,17,18. |
| `git-issue-113-extended-newtypes-datatype-floating-positive.dfy` | 0 / 4 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-datatype-generic-witness-runtime.dfy` | 0 / 3 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-datatype-partial-equality.dfy` | 0 / 3 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-datatype-phantom-positive.dfy` | 0 / 4 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-datatype-positive.dfy` | 0 / 18 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-datatype-proof-negative.dfy` | 4 / 2 verified, 6 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 11,15,19,23,29,35. |
| `git-issue-113-extended-newtypes-datatype-resolver-cycle-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-datatype-resolver-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-datatype-runtime.dfy` | 0 / 12 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-datatype-trait-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-datatype-type-test-resolver-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-datatype-type-test-runtime.dfy` | 0 / 3 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-direct-reference-resolver-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-finite-actual-characteristic-nested-argument-positive-proof.dfy` | 0 / 5 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-finite-actual-characteristic-positive-proof.dfy` | 0 / 5 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-finite-actual-characteristic-proof-nonvacuity.dfy` | 4 / 2 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 16. |
| `git-issue-113-extended-newtypes-finite-codatatype-actual-positive.dfy` | 0 / 3 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-generic-companion-runtime.dfy` | 0 / 3 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-generic-conversion-runtime.dfy` | 0 / 7 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-hidden-base-cast.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-hidden-base-introduction.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-hidden-base-member.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-hidden-base-order.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-hidden-base-plus.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-hidden-base-typearguments.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-hidden-cast.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-hidden-member.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-hidden-ordinal.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-hidden-positive.dfy` | 0 / 10 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-inhabited-false.dfy` | 4 / 1 verified, 1 error | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 9. |
| `git-issue-113-extended-newtypes-native-runtime.dfy` | 0 / 2 verified, 0 errors | New existing-language / feature-off control positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-numeric-view-errors.dfy` | 4 / 2 verified, 2 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 9,14. |
| `git-issue-113-extended-newtypes-numeric-views.dfy` | 0 / 4 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-option-off.dfy` | 2 | New existing-language / feature-off control admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-ordinal-bv-resolve.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-ordinal-errors.dfy` | 4 / 4 verified, 9 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 12,18,23,29,35,41,47,54,61. |
| `git-issue-113-extended-newtypes-ordinal-generic.dfy` | 4 / 4 verified, 2 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 23,29. |
| `git-issue-113-extended-newtypes-ordinal-native-resolve.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-ordinal-ranges.dfy` | 4 / 3 verified, 3 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 10,15,22. |
| `git-issue-113-extended-newtypes-ordinal-resolve.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-ordinal-runtime.dfy` | 0 / 4 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-ordinal-unicode-errors.dfy` | 4 / 2 verified, 3 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 9,14,21. |
| `git-issue-113-extended-newtypes-ordinal-unicode.dfy` | 0 / 3 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-ordinal.dfy` | 0 / 13 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-substitution.dfy` | 4 / 1 verified, 2 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 17,25. |
| `git-issue-113-extended-newtypes-type-test-actuals-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-type-test-downcast-runtime.dfy` | 0 / 6 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-type-test-ghost-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-type-test-ghost-positive.dfy` | 0 / 2 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
| `git-issue-113-extended-newtypes-type-test-hidden-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-type-test-nested-ghost-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-type-test-phantom-negative.dfy` | 2 | New enabled extended base admission or nominality negative; the intended declaration is rejected. |
| `git-issue-113-extended-newtypes-witness-negative.dfy` | 4 / 0 verified, 3 errors | New enabled extended base proof-boundary or nonvacuity negative; the intended obligation fails at lines 6,7,8. |
| `git-issue-113.dfy` | 0 / 21 verified, 0 errors | New enabled extended base positive contract; canonical verification succeeds. |
