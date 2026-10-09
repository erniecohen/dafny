# Canonical caller construction: current native evidence

Candidate `b3888f933` removes the extra early construction of caller can-call
support. The ordinary assertion-style well-formedness preparation already emits
the complete support after its witnesses and before the single actual check.
The change preserves the original normalization, scope, check formulas and full
metadata, fuel, publication and reveal behavior. It introduces no background
axiom, second proof, body-term collection or resource-ceiling change.
The [licensing argument](obligation-certified-preparation.md) and
[construction diagnosis](obligation-current-focused-library.md) describe the
semantic boundary and the preceding faithful scratch comparisons.

## Normal compiler and registered regressions

The [normal compiler build](https://github.com/erniecohen/dafny/actions/runs/38001880501),
source `8259b4208`, reports `4.11.0+fcb2042d.review.5841c0e6`. All ten actual
stages pass: both native compilers, both direct Boogie probes, 108 obligation
tests in each mode, all 386 core tests, the reviewed 287-group producer inventory,
and the editor build. The [first build](https://github.com/erniecohen/dafny/actions/runs/38001097227)
passes compilation and the new support-order controls but fails the old inventory
comparison. The correction refreshes the changed translator file hashes and
records removal of exactly one redundant early can-call construction; all other
producer groups, counts and expression hashes are unchanged. That initial failure
remains part of the validation history.

The complete current registered issue-100 helper passes all 310 native
observations using this normal compiler and Z3 5.1.0: 81 positive observations
and 229 genuine Invalid negative observations, with exact committed expected
output and no resource or timeout outcomes. Both resolver modes and both
additional-axiom settings are exercised where the helper specifies them. This
includes the new paired quantified-caller preparation and negative controls,
original issue 100, visible constraints, call preparation, project-option
precedence, and legacy terminal reveal scopes. It is focused registered-test
acceptance, not the complete paired manifest, verifier suite or library gate.

## Known library regressions

The unchanged known-library gate completes all 96 observations at original
project settings and ceilings, seeds 0/1/7 and both axiom settings. Remainder now
verifies at all three seeds with axioms off and on, without a redundant source
assertion. Its six fresh baseline comparisons also verify. The earlier eight
functional/reveal cases remain successful at every selected seed. All thirty
negative controls contain genuine Invalid VCs, and all reported independent
specification well-formedness checks are Correct.

Multiset ordinal decrease remains resource-exhausted at seed zero and verifies
at seeds one and seven under both axiom settings. Thus 64 of the 66 positive
observations verify; the other two are resource failures. The gate's normal
completion establishes a complete diagnostic record, not that every original
positive verifies. This is neither full-library acceptance nor evidence that
the enabled implementation is uniformly cheaper or better than the baseline.

## Known suite regressions and retained harness failure

The original 68 known-suite/control observations are completed and retained.
The subsequent aggregate fails because the newly added direct false-precondition
control has no separate specification-WF declaration, while the harness requires
one. Its four raw solver results are genuinely Invalid at the call site. A
separate complete four-observation gate reruns only those unchanged controls
with the correct declaration boundary and succeeds. The 72-observation family
explicitly combines those 68 preserved observations and four fresh controls;
the original aggregate's failure is not relabeled as a successful gate.

Original issue 100, subset and guarded-argument controls still verify. Power,
AltPrimeDefinition, ExtensibleArray.Append and FormArmy verify at seeds 0/1/7
under both axiom settings. RemoveFactor and Composite exhaust resources at
those selected seeds; the iterator verifies at zero and one but exhausts at
seven. The earlier full 0-7 classification of Composite and the iterator remains
historical evidence, not a new 0-7 run of this compiler. Section 13's restriction
on tuning both-partial cases is unchanged. All actual negative controls are
Invalid. The six selected default-off Power vectors exactly match the recorded
baseline outcomes and resource counts; this sample does not establish complete
default-off compatibility.

## Remaining review work

The stable RemoveFactor and multiset resource movements, complete default-off
compatibility/cost-policy acceptance and independent soundness review remain
open. The successful caller correction is general, not a quantifier-only repair.
A scratch-only standard unconditional exit-construction comparison is under
validation; it preserves the original normalization/scope/check/fuel/publication
policies and retains leading support for inherited or statement-expression
clauses. It has no product adoption or acceptance claim yet.

No further whole-suite or whole-library iteration was launched for this caller
revision. PR #168 remains draft and its reviewed code head has not yet been
advanced to this candidate. PR #169 is unchanged; a development-line port still
requires explicit owner approval. The durable [review request](obligation-review-change-request.md)
and [checklist](obligation-revision-checklist.md) continue to govern completion.
