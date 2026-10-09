# Native investigation of the remaining exit regressions

This is diagnosis of the current semantic/product `fe6ddd1ba`, not a completed
repair or acceptance result. Both experiments use the exact accepted macOS arm64
base and Z3 5.1.0, original proof sources, resource ceilings, options and retained
terminal reveal scopes. Only the core and verification frontend are instrumented
in scratch builds; all other binary files remain byte-identical to the accepted
base. No whole-suite or whole-library gate is run.

## Whole source-clause preparation

The current local exit loop prepares top-level conjuncts individually before
canonical splitting. An immediate assertion prepares its complete source clause.
A native diagnostic tests the original-clause boundary for only Composite and
FormArmy, with both axiom settings and seeds 0, 1 and 7. RemoveFactor has one
unconjoined clause, so this hypothesis does not justify repeating it.

All 36 observations complete. Twelve unchanged native controls reproduce current
outcomes and resource counts; all four seed-zero complete SMT streams through
their last query match, including FormArmy's separate WF and body queries.
All twelve false-entry controls have genuine Invalid VCs, and every reported
independent specification-WF check is Correct.

The strict typed lexical-binder fingerprints of every existing actual assertion
are identical: 33 checks for Composite and 15 for FormArmy. Ground identifiers,
triggers, operators, guards and fuel layers are retained. The full assertion
attributes differ, and the audit identifies the differences solely as generated
proof-dependency `id` values; subsumption and other assertion attributes agree.
Those differences are recorded rather than hidden by the fingerprint comparison.
Branch-transfer and post-translation command identities are audited before and
after adding each negative control. Preparation and normal clause publication
are the intended changes.

`V` means Correct and `R` means OutOfResource; positions are seeds 0, 1 and 7.
Both axiom settings agree.

| Target | Current native | Whole source clause |
| --- | --- | --- |
| `Composite` | VRV | VVR |
| `FormArmy` | RRR | RRR |

Whole-clause preparation trades Composite's successful and failing seeds rather
than eliminating the regression. FormArmy continues to exhaust resources at
every tested seed. It is rejected as a demonstrated resource repair; no product
change is made from this experiment. Equal checked formulas and additional
term availability do not guarantee monotone solver resource use.

The compiler-only [source-clause build](https://github.com/erniecohen/dafny/actions/runs/37871109480)
passes all six actual stages. The first two-process proof submission is cancelled
while still queued, before any proof runs. A fresh immutable copy serializes the
same 36 observations for lower peak memory; every individual verifier retains
its original one-core setting. No completed or running proof is cancelled.

## Isolating RemoveFactor's exit translation

A separate native comparison isolates five method/iterator exit gates from the
other opt-in translation gates. Declaration and implementation choices remain
paired: legacy exit mode retains checked procedure ensures, and local exit mode
marks them nonchecking only when the mandatory local checks exist. Legacy call
mode retains checked callee requires when local call checks are disabled.
The other family also governs implicit preparation checks within an exit replay;
that interaction is retained explicitly in the interpretation.

The first completed scope is twelve observations for only RemoveFactor at seed
zero: both axiom settings, four translations, and four false-entry controls.
All four native baseline/current controls reproduce outcomes, resource counts
and complete SMT streams through their last query. Every negative control is
genuinely Invalid and every reported independent WF check is Correct.

| Body translation | Exit translation | Seed-zero result, both axiom settings |
| --- | --- | --- |
| Legacy | Legacy | Correct |
| New | Legacy | Correct |
| Legacy | New | OutOfResource |
| New | New | OutOfResource |

The complete canonical SMT streams also match within both pairs: new-body/
legacy-exit equals all-legacy, and legacy-body/new-exit equals current all-new.
The resource counts match within those pairs as well. This localizes the
observed seed-zero regression to the exit translation family, rather than the
changed recursive-call precondition check. It does not yet distinguish local
exit preparation, publication, and the relocation of the check itself.

The saved generated Boogie shows the same two `product` fuel layers in the
legacy checked ensures and the current actual exit assertion. The current exit
adds private argument/allocatedness/can-call preparation and normal post-check
publication. It has no reduced fuel in that checked formula. The earlier
duplicate-removal experiment does not repair it, so duplicate can-call support
alone is not an established explanation.

The compiler-only [family-isolation build](https://github.com/erniecohen/dafny/actions/runs/37872189665)
passes all six actual stages. Selected names bound only the audited proof scope;
family gating applies consistently across the translated module and supplies
no proposed product policy. This single-seed finding is causal evidence within
the recorded scope, not a multi-seed repair or a compatibility waiver.
