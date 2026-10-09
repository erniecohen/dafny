# Native investigation of the remaining exit regressions

This file retains investigations with their named source/compiler identities;
the first sections below concern historical product `fe6ddd1ba`. The current
single-check semantics are `991b60037`, followed by the command-neutral structural
observer `df4c7066f`. Its latest completed isolation, current-query replay and
[bounded seed classification](obligation-current-seed-classification.md) preserve
controls and explicitly distinguish diagnostic success from product acceptance.
Composite now meets review section 13's both-partial rule. RemoveFactor remains
open at the unchanged combined ceiling; no grouping policy or resource increase
is adopted. No whole-suite/library iteration or development port is run.

The historical experiments use the exact accepted macOS arm64 base and Z3 5.1.0,
original sources, ceilings and terminal reveal scopes. Scratch builds replace
only the core and verification frontend; other binary files remain exact.

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


## Composite and FormArmy: the body family is sufficient

A fresh 24-observation follow-up runs only Composite's failing seed 1 and
FormArmy's failing seed 0, in both axiom settings. The completed RemoveFactor
comparison is reused explicitly. All eight unchanged baseline/current controls
reproduce their outcomes, resource counts and complete SMT streams. All eight
false-entry controls contain genuine Invalid VCs and every reported independent
specification-WF check is Correct. The compiler and all other inputs are the same
as the preceding family experiment.

The older native campaigns did not save Composite seed-one SMT inputs. Four
additional reference captures use the exact accepted unmodified baseline and
current native bundles, reproduce the recorded outcomes/resource counts, and
supply the missing complete streams. An initial preparation stops before queue
submission on those missing files; it is retained and supplies no proof result.
The fresh follow-up requires the actual captured streams rather than weakening
its fidelity criterion.

Both axiom settings give the following results:

| Target and seed | All legacy | New exits only | New body only | All new |
| --- | --- | --- | --- | --- |
| Composite, 1 | Correct | Correct | OutOfResource | OutOfResource |
| FormArmy, 0 | Correct | Correct | OutOfResource | OutOfResource |

At these specific failing seeds, the body-side translation changes are sufficient
to reproduce resource exhaustion; the new exits alone verify. This differs from
RemoveFactor, whose isolated exit translation reproduces the complete failing
current query. The isolated Composite/FormArmy streams differ from the complete
current/baseline streams, so no claim of pairwise query identity is made there.

The saved native bodies show assertion-style method-call preparation before the
recursive Composite call and both FlyRobotArmy calls in FormArmy. That is a
concrete next boundary to investigate. The broad body family also includes
function-precondition, allocation and type checking, so this result alone does
not identify one particular call or support command. These are single-seed
causal boundaries, not multi-seed repairs, a semantic counterexample, or evidence
of uniform improvement over legacy. No product change follows from disabling
one family in this diagnostic.


## Fresh private argument aliases do not explain these failures

A separate completed 54-observation native diagnostic substitutes defined values
for fresh private function-WF arguments only inside a newly generated, accepted
pure ground preparation fragment. It rejects source-state writes, havoc,
forward/repeated bindings, binders, old expressions and unsupported forms.
For a fresh private `t`, `t := e; assume Q(t)` becomes `assume Q(e)`, in original
order, preserving facts over observable values, guards, function symbols,
boxing, heaps and fuel. An escape audit examines the containing implementation
and its corresponding procedure contract, including where clauses and attributes.
No alias may survive there. The original source and actual-check lowering are
unchanged; this is a scratch diagnostic, not a proposed product simplification.

All eighteen native controls reproduce current outcomes/resource counts, and
all six seed-zero complete streams through their last query match the accepted
native references. All eighteen false-entry controls have genuine Invalid VCs;
every reported independent specification-WF check is Correct. Each of the
36 candidate/negative comparisons has strictly identical actual assertion
expressions and attributes, including dependency IDs. Two argument bindings are
substituted in each target. Branch transfers and post-translation command
objects remain unchanged apart from the explicit negative entry check.

| Target | Current native | Private arguments substituted |
| --- | --- | --- |
| Composite | VRV | VRV |
| FormArmy | RRR | RRR |
| RemoveFactor | RRR | RRR |

The complete canonical SMT streams are identical between control and candidate
for every Composite and FormArmy seed in both axiom settings. Their resource
counts also match exactly. RemoveFactor's streams differ, but every tested seed
still exhausts the unchanged ceiling. This rejects private argument aliases as
an established resource repair for these cases. No product change follows.

The [scoped diagnostic compiler](https://github.com/erniecohen/dafny/actions/runs/37877352182)
passes all six actual stages. Two preceding attempts are preserved and excluded:
the first syntax audit follows an unresolved procedure link and stops before
verification; the second completes one native control before a spelling-based
all-program escape audit conflates distinct local scopes. Generated Boogie
shows the same private spelling in Composite and ProductPlusOneIsPrime. The
fresh completed scope corrects only the audit, retains all original proof inputs
and does not treat either aborted attempt as hypothesis evidence.


## Certified caller support order does not repair the failing seeds

The current exit path passes leading can-call support into the common certified
preparation helper; method-call checks still emit that support directly before
preparation. A further native diagnostic uses the same helper for only caller
support. It moves the existing assumption object after setup only when the
complete-fragment certificate proves noninterference, and retains its original
placement otherwise. Every caller support object is audited as present exactly
once. No preparation facts, actual checks, fuel, guards or publication change.

The completed scope has twelve observations: Composite's failing seed 1 and
FormArmy's failing seed 0, both axiom settings, unchanged/caller-order/false-entry
variants. All four controls match current outcomes, resources and complete SMT
streams through their last query. All four negative controls have genuine
Invalid VCs and every reported independent specification-WF check is Correct.
All eight candidate/negative comparisons have identical actual assertion
expressions and attributes, including dependency IDs. Two caller supports move
in Composite and four in FormArmy; their objects remain present exactly once.

Both targets still exhaust the unchanged ceiling in both settings, with resource
counts identical to their controls. Candidate solver streams differ, so no
pairwise stream identity is claimed. This rejects caller support order as a
repair of these specific failing seeds. A larger seed sweep is not justified
by this result. The [compiler-only diagnostic](https://github.com/erniecohen/dafny/actions/runs/37877821143)
passes all six actual stages. No product change or whole-suite/library run follows.

For RemoveFactor's preceding native family comparison, all 33 background SMT
assertions are identical between legacy and current in each axiom setting; the
checked body VC differs and current has two additional constant declarations.
This confirms that the localized exit failure does not arise from replacing
background axioms in that comparison. It does not by itself identify which body
VC difference causes resource exhaustion. The checked product fuel remains the
same, and neither alias elimination nor duplicate-support removal repairs it.


## Faithful solver profiles distinguish the remaining searches

A completed twelve-observation comparison profiles the unchanged native current
and baseline translations at Composite seed 1, RemoveFactor seed 0 and FormArmy
seed 0, in both additional-axiom settings. All twelve prior outcomes, resource
counts and complete SMT streams through the last query agree exactly. Every
reported independent specification-WF check is Correct. The same pinned Z3
5.1.0 receives the native input directly with quantifier reporting enabled;
no compiler instrumentation, source hint, proof option or ceiling changes.

A subsequent six-observation capture, at those same seeds with additional axioms
off, adds diagnostic verbosity and an unchanged byte-for-byte input tee. All six
reference outcomes, resource counts and complete streams again match exactly.
The preceding profiles agree across the two axiom settings; the second capture
therefore narrows the inspection without repeating both settings.

| Native comparison | Profile finding | Limit of the inference |
| --- | --- | --- |
| FormArmy | The largest reported counters belong to existing pairwise integer-index quantifiers over sequences; related heap-update quantifiers also grow. | This identifies active quantifiers, not the support command that causes the changed search. |
| Composite | Current reported instantiation counters are lower than the successful baseline, including arithmetic-wrapper quantifiers. | Resource exhaustion is not explained by a universal increase in instantiation counts. |
| RemoveFactor | Current reported counters are also lower; set insertion/difference axioms are prominent in both versions. | The previously localized exit change still needs a causal explanation within its body VC. |

These are per-quantifier reporting comparisons, not summed solver totals. Z3's
[quantifier reporting implementation](https://github.com/Z3Prover/z3/blob/z3-5.1.0/src/smt/smt_quantifier.cpp)
prints cumulative counters, while
[the instantiation queue](https://github.com/Z3Prover/z3/blob/z3-5.1.0/src/smt/qi_queue.cpp)
can print intermediate reports. Repeated rows must not be added. Transformed
quantifier clones can share an identifier. The
[SMT parser](https://github.com/Z3Prover/z3/blob/z3-5.1.0/src/parsers/smt2/smt2parser.cpp)
assigns a default identifier from the input scanner's closing line, so mapping
uses captured native input rather than line numbers from Boogie's solver log.
All quantifiers at a matching line are retained when the mapping is ambiguous.

The source-level assertion implementation also confirms that `AssertMode.Check`
retains ordinary assertion publication. It does not select `AssertAndForget`
or add `subsumption 0` to these caller checks. This rules out that proposed
explanation; it does not establish equivalence of the complete search contexts.

The next causal boundary is only user-defined method-call preconditions and
their matching procedure requirements. A scratch compiler keeps the single
mandatory proof on either the local or legacy procedure side and isolates this
producer from other opt-in translation changes. Original input, resource policy
and reveal scope remain unchanged. The completed comparison is recorded below. No profile result
is promoted to a product repair, full-suite/library acceptance or completed review.


## User-defined method-call checks are sufficient at the selected seeds

A completed twelve-observation native comparison separates exactly two paired
translation decisions: the sole user-defined precondition proof in `TrCall` and
its nonchecking procedure `requires` copy. Legacy mode retains the checked
procedure requirement; local mode retains the mandatory local proof. Two-state
old allocation, function preconditions, membership, frame, termination, exits
and other translation decisions remain in the other family. Preparation choices
within the new caller replay are an explicit interaction with that family.

Only Composite seed 1 and FormArmy seed 0 are executed, with additional axioms
off, original source/options/ceilings and one verifier core. The earlier
both-setting native profiles and family comparisons are retained. All four
unchanged current/legacy controls match complete native solver streams through
the last query, outcomes and resources. All four false-entry controls have
genuine Invalid VCs, and every reported independent specification-WF check is
Correct. No actual obligation is dropped in either isolated translation.

| Target and seed | All legacy | New method calls only | Current except method calls | All current |
| --- | --- | --- | --- | --- |
| Composite, 1 | Correct | OutOfResource | Correct | OutOfResource |
| FormArmy, 0 | Correct | OutOfResource | Correct | OutOfResource |

The user-defined method-call producer is sufficient to reproduce both selected
failures; the remaining current translation changes verify when those calls use
legacy requirements. Composite's calls-only complete stream equals the preceding
other/body-only stream. FormArmy's streams differ despite matching resource
counts, so stream identity is not claimed there. These results narrow the prior
body-family finding to a particular producer; they do not distinguish its WF
support, local proof lowering/placement and normal publication.

The [paired caller compiler](https://github.com/erniecohen/dafny/actions/runs/37884342637)
passes all six actual stages. No product repair or broader seed acceptance follows
from disabling the new producer. The completed six-observation follow-up below traverses the
same complete caller WF path and consume its original translator/fuel state,
while diagnostically omitting only certified pure preparation-command emission.
It retains leading can-call support and the actual check/publication and requires
strict assertion-expression/attribute equality. Unsupported fragments, havoc,
non-private writes, real assertions and scope changes cannot be omitted.
This deliberate support omission is causal diagnosis, not a proposed product
fix: the intended product must retain assertion-equivalent proving support.


## Caller WF emission separates Composite from FormArmy

The follow-up completes six native observations at the same two seeds, with
additional axioms off. It traverses the complete current caller WF path and
consumes the same translator/fuel state, then omits only accepted pure preparation
commands for the selected caller. Leading can-call support, every actual check,
normal publication and the call remain. Unsupported fragments, havoc, source
writes, real assertions and scope changes are rejected.

Both unchanged controls reproduce complete native streams, outcomes and resource
counts. Both negatives have genuine Invalid VCs, every reported independent WF
check is Correct, and all four comparisons have strictly identical actual
assertion expressions and attributes, including dependency IDs. Composite retains
33 actual checks and FormArmy retains 15. Two prepared fragments are omitted in
Composite and four in FormArmy. The
[diagnostic compiler](https://github.com/erniecohen/dafny/actions/runs/37886097322)
passes all six actual stages.

| Target and seed | Current native | Caller WF commands omitted |
| --- | --- | --- |
| Composite, 1 | OutOfResource | Correct |
| FormArmy, 0 | OutOfResource | OutOfResource |

Composite's selected resource failure depends on these additional caller WF
commands in this comparison. FormArmy's failure survives their omission; it
requires a different explanation within the caller producer. This deliberate
support omission is not a product repair. The intended product must retain
assertion-equivalent pre-check support, regardless of the diagnostic outcome.

The next source-level difference is split caller publication. Current local
checks publish their ordinary checked pieces and then separately assume the
whole source precondition. Legacy `GetRequires(Call)` skips free-only splitter
pieces, and Boogie checked requirements publish their checked pieces without
that extra whole-P summary. A narrow FormArmy comparison keeps all preparation,
actual checks, fuel and checked-piece publication and isolates only this extra
post-check summary. It generates the original summary/dependency ID before
omitting its emission so actual attributes can be compared strictly. All three observations now complete. The unchanged native control reproduces
its complete SMT input, outcome and resource count; the false-entry control is
genuinely Invalid, and all reported independent WF checks are Correct. Both
comparisons retain the same 15 actual assertion expressions and all attributes,
including dependency IDs. Two whole-precondition summaries are omitted, with all
pre-check preparation and ordinary checked-piece publication retained. FormArmy
seed 0 still exhausts its original resource limit. The
[publication diagnostic compiler](https://github.com/erniecohen/dafny/actions/runs/37886833332)
passes all six actual stages. Removing the extra summary is rejected as a
resource repair; no product change or acceptance follows from this comparison.


## Existing background assertions and the sole legacy caller proof

The faithful raw solver captures also permit comparing the assertions active at
the body query. Interpreting each push/pop excludes earlier, popped independent
WF queries. All active background assertions are byte-identical between baseline
and current translation for Composite, FormArmy and RemoveFactor. Their body VCs
are different. This provides no evidence that an added or removed background
axiom explains these selected failures; local preparation and VC encoding remain
the relevant boundaries.

A further four-observation FormArmy seed-zero comparison keeps the complete new
caller preparation but restores the sole checked procedure requirements. All
other opt-in translation families remain legacy in this hybrid. It traverses the
original caller lowering/fuel state and generates each local check for audit,
but emits no duplicate local caller proof and no extra post-check summary. The
actual call retains its checked requirements, original actuals and pre-call heap.

The audit compares all six generated local precondition expressions with the
corresponding checked procedure requirements under formal-to-actual substitution.
Every typed lexical fingerprint agrees, including guards, operators, trigger
patterns, coercion destinations and fuel. The generated formulas also agree
with the unchanged current control. Their full attributes are compared and
differ solely in generated proof-dependency IDs; this is recorded explicitly.
The clone-only comparison supports unresolved identifiers at this stage,
requires frozen typed identifier actuals, rejects binder capture and unsupported
expressions, and leaves the native verifier program untouched. The earlier
post-resolution substitution adapter is rejected before producing a hybrid
proof result.

Both unchanged baseline/current controls reproduce their full native SMT inputs,
outcomes and resources. The false-entry hybrid is genuinely Invalid, every
reported independent WF check is Correct, and the
[corrected diagnostic compiler](https://github.com/erniecohen/dafny/actions/runs/37890532431)
passes all six actual stages.

| FormArmy seed 0, additional axioms off | Result |
| --- | --- |
| Unchanged baseline | Correct |
| Unchanged current | OutOfResource |
| Full new caller preparation, sole legacy caller proof | OutOfResource |
| False-entry hybrid | Invalid |

Preparation/leading-support in the legacy checking encoding is sufficient to
reproduce this selected failure. Relocating the actual precondition proof is
therefore unnecessary to reproduce it. Together with the earlier omission
experiments, this establishes more than one search-sensitive caller boundary;
it does not establish a product repair or a universal account of solver cost.
The allocatedness-only follow-up below now identifies a concrete
search-sensitive fact inside this hybrid. Its deliberate omission remains
diagnosis only; the product retains legitimate assertion-equivalent support.


## Private-argument allocatedness tips the legacy-check hybrid over its ceiling

Only FormArmy seed 0 with additional axioms off is tested. The three-observation
follow-up retains the complete original caller WF/fuel traversal, every other
WF/can-call command, all private assignments and the same six sole checked
procedure requirements. It omits only two assumptions of this form, one before
each `FlyRobotArmy` call:

```boogie
assume $IsAlloc(t, TSeq(Tclass._module.Bot()), $Heap);
```

Here `t` is that function application's private frozen sequence argument. The
audit requires a certified pure fragment, a matching private argument and the
current pre-call heap. Unsupported commands, havoc, source writes, a different
heap, repeated omissions or a change to any retained command object are rejected.
No fuel traversal, actual precondition, trigger pattern, call position, procedure
contract or other preparation fact is changed.

All three observations complete. The unchanged hybrid reproduces its complete
native SMT input, outcome and resources. The omitted-fact variant verifies at
the original resource ceiling; its false-entry control is genuinely Invalid.
Every reported independent WF check is Correct. All six generated formulas
strictly match their formal-to-actual substituted procedure requirements, and
both comparisons preserve all generated expression fingerprints and full
attributes, including dependency IDs. The
[allocatedness diagnostic compiler](https://github.com/erniecohen/dafny/actions/runs/37891920242)
passes all six actual stages.

| FormArmy seed 0, additional axioms off | Result |
| --- | --- |
| Full preparation, sole legacy caller proof | OutOfResource |
| Only the two argument allocatedness assumptions omitted | Correct |
| False-entry control with those assumptions omitted | Invalid |

These extra allocatedness facts are sufficient contributors to resource
exhaustion in this selected legacy-check hybrid. This directly demonstrates a
search cost from additional pre-check support while the checked propositions
stay the same; it is not evidence of reduced fuel or a weaker actual check.
The earlier faithful profiles identify active sequence-index and heap-related
quantifiers, but no new profile of this variant is captured, so specific counter
changes or a particular instantiation chain are not asserted here.

The scope matters: the full current path still exhausts resources in the earlier
experiment that omits the complete caller WF fragment. Removing these two facts
therefore does not fully explain or repair that current path. More than one
caller translation difference can affect this resource-limited search. Removing
legitimate assertion-style support is excluded as a product fix, and this
single-seed causal result does not establish uniform improvement, multi-seed
acceptance or a whole-suite/library result. No product change follows from it.


## Combined caller emissions explain the remaining selected search

The preceding full-current FormArmy experiment still exhausted resources when
either all caller WF emissions or only the two whole-precondition summaries were
omitted. A five-observation follow-up tests their combination, then the remaining
leading clause can-call emissions. It changes emissions only after constructing
every original object and dependency identifier. Complete original WF traversal
and fuel consumption remain in effect. All fifteen actual assertion formulas
and their full attributes match strictly across all five observations.

The unchanged current control and unchanged WF-omission control reproduce their
previous complete SMT streams, outcomes and resource counts. All reported
independent specification-WF checks are Correct. The false-entry control contains
a genuine Invalid VC. The
[diagnostic compiler build](https://github.com/erniecohen/dafny/actions/runs/37894540510)
passes all six actual stages and preserves the accepted native bundle except for
the two explicitly instrumented assemblies. Product `fe6ddd1ba` and Z3 5.1.0,
original source, seed zero, additional axioms off and the original ceiling remain
fixed.

| Full-current FormArmy variant | Result |
| --- | --- |
| Unchanged current | OutOfResource |
| Caller WF emission omitted | OutOfResource |
| Caller WF and the two split summaries omitted | Correct |
| Those emissions and the four leading can-call assumptions omitted | Correct |
| False entry assertion in the final diagnostic | Invalid |

Together with the earlier summary-only result, this shows that either retained
emission family can keep this selected search over its existing ceiling even
when the other is removed. Omitting both restores the proof; removing leading
can-call emissions is unnecessary for that restoration. The actual checks retain
their original terms, guards and fuel. This is a bounded solver-search effect of
additional emitted facts, rather than evidence that the proposition became false
or that its check received fewer triggering subterms or less fuel.

This deliberately weakened diagnostic is not a product repair. Assertion-style
preparation must remain available for the mandatory check, and the original
kind's normal publication must be preserved. The result is confined to the
selected seed/configuration and does not establish a particular instantiation
chain, multi-seed stability or supported-line acceptance. No new whole-suite or
whole-library run, solver fallback, raised ceiling, source hint or development
port is included.


## Support retained in local caller proof scopes

A scratch comparison keeps every original leading can-call, WF preparation,
mandatory check and split summary, in its original order, inside the existing
`PathAsideBlock` mechanism. The path cutoff follows every mandatory check. The
continuation assumes the exact checked precondition pieces and performs the
original call. Each original check and preparation object occurs exactly once;
full expression traversal, fuel, heap, visibility and actual assertion attributes
remain unchanged. No supporting fact is removed from its own check.

The experiment covers only Composite seed one and FormArmy seed zero, with
additional axioms off and unchanged sources, options and resource ceilings.
Each unchanged native control reproduces its complete previous solver stream,
outcome and resource count. All 33 Composite and 15 FormArmy actual checks and
full attributes match strictly under source-keyed ordering. The
[Composite compiler build](https://github.com/erniecohen/dafny/actions/runs/37896736816)
and corrected
[FormArmy compiler build](https://github.com/erniecohen/dafny/actions/runs/37902649612)
pass all six actual stages; their source difference corrects a diagnostic call
count only. Earlier audit-count failures are preserved and excluded from proof
results.

| Target | Unchanged current | Caller preparation scoped locally | False assertion after first scoped check |
| --- | --- | --- | --- |
| Composite, seed 1 | OutOfResource | OutOfResource | OutOfResource |
| FormArmy, seed 0 | OutOfResource | Correct | OutOfResource |

Four individual original/scoped observations completed. Both attempted negatives
exhaust resources and contain no genuine Invalid VC. Both full diagnostic gates
therefore reject acceptance; those negatives are inconclusive, not passed
controls. The FormArmy result is a promising support-preserving construction,
not an accepted product repair or a multi-seed result. Composite is not repaired.

A general implementation must publish the original call contract facts, not
blindly publish internal assertion-split pieces when induction or splitting
changes their representation. The selected FormArmy pieces had already been
strictly matched to the substituted legacy requirements; Composite has the two
plain recursive-call conditions. Existing normal can-call requirements and
previously retained outer reveal scope must remain. Product `fe6ddd1ba` is
unchanged. No full suite/library iteration, resource increase, source proof hint,
support deletion or development port follows from this experiment.


### FormArmy scope follow-up at all existing seeds

A subsequent FormArmy-only matrix executes all eighteen observations at seeds
0, 1 and 7, in both additional-axiom settings: unchanged current, the same caller
scope construction, and a distinct false assertion before the first mandatory
caller check. The latter is inserted after that call's preparation. The prior
false assertions after the first check are preserved and remain inconclusive;
the new control does not replace or reinterpret them.

The [diagnostic compiler](https://github.com/erniecohen/dafny/actions/runs/37903599649)
passes all six actual stages. Its positive translation is unchanged. All six
native controls reproduce the complete reference solver streams, outcomes and
resource counts. The scoped seed-zero positive also reproduces the previous
complete scoped stream and cost. All twelve candidate/control comparisons retain
all fifteen actual assertion expressions and full attributes strictly. Each
check retains its own original preparation and fuel; every preparation/caller
object remains exactly once, and reported independent WF results are Correct.

Both axiom settings give these vectors. Positions are seeds 0, 1 and 7;
`V` means Correct, `R` OutOfResource and `I` a genuine Invalid VC with the added
false assertion's own diagnostic confirmed.

| FormArmy variant | Results |
| --- | --- |
| Unchanged current | RRR |
| Caller preparation scoped locally | VVV |
| New false assertion before the first check | IRR |

Thus all six real-program scoped observations verify at their original ceiling.
This establishes the improvement across the tested seeds/configurations for this
construction. It does not establish universal improvement or product acceptance.
Only two of six new negative controls are genuinely Invalid; four exhaust
resources. The completed eighteen-observation execution therefore has a rejected
validation gate. The earlier after-check negatives remain rejected as well.
General call publication, representative source cases and negative validation
still need to be established before adopting this experiment in product code.
Composite remains unresolved by the scoped construction; RemoveFactor has not
been tested with it. Product `fe6ddd1ba` remains unchanged, with no whole-suite/
library iteration, raised ceiling, source hint or development port.


### Original call-contract publication with a local proof scope

The next diagnostic keeps every original caller preparation, actual check and
split summary in its local proof branch, but publishes no copied internal check
pieces to the continuation. Instead, it marks the original nonchecking user
requirements with Boogie's existing `always_assume` attribute. The ordinary
Boogie call supplies the original contract conditions with its own formal/actual
substitution, boxing and pre-call heap handling. Every condition object is
retained by identity; no condition is retranslated, no additional proposition
check is introduced, and each check retains its original preparation and fuel.

The [first diagnostic compiler](https://github.com/erniecohen/dafny/actions/runs/37909310841)
passes all six actual stages. Eighteen FormArmy observations complete, with six
unchanged controls reproducing the full solver streams, outcomes and resource
counts, and twelve strict comparisons of all fifteen original assertion
expressions and full attributes. In both axiom settings, positions at seeds
0, 1 and 7 are:

| FormArmy variant | Results |
| --- | --- |
| Unchanged current | RRR |
| Local proof scope, original call-contract publication | VVV |
| Same translation with an added false entry assertion | III |

All six positive observations verify at their original resource ceiling, and
all six false-entry observations are genuine Invalid VCs. The direct source
false-precondition fixture initially stops before verification because its
scratch audit incorrectly assumes literal preparation contains zero commands.
The completed eighteen observations and the unsuccessful original gate are
preserved; that gate is not reclassified as successful.

A [follow-up compiler](https://github.com/erniecohen/dafny/actions/runs/37912361023)
changes only that diagnostic count check. A fresh gate runs only the four
unfinished observations: a direct call to a method with `requires false`,
unchanged and scoped, in both axiom settings. All four are genuinely Invalid,
with the failing precondition identified at the source call. The two strict
actual-check/attribute comparisons pass, and observed preparation counts match.
The four-observation diagnostic gate succeeds; it explicitly reuses the retained
FormArmy results from the earlier compiler. This provides twenty-two completed
observations across the two frozen gates and ten genuine negative results.
It does not provide product acceptance or a single twenty-two-observation run.

A source audit also identifies a restriction on general adoption. For inherited
or filtered `free` calls, the original Boogie translation skips ordinary checked
requirements. Globally marking their contract copies `always_assume` would newly
publish those propositions on the free-call paths. A general correction must
preserve that distinction, alongside original outer reveals and unsupported
preparation effects; the scratch marker must not be adopted indiscriminately.
Composite is not repaired by the earlier caller-scope construction, and
RemoveFactor has not been tested with it. Product `fe6ddd1ba` remains unchanged.
No source hint, raised limit, new background axiom, global fuel rewrite,
whole-suite/library iteration or development port is used.


### Current product and retained-support exit follow-up

The [current native record](obligation-caller-publication-native.md) belongs to
product `991b60037`. Its 72 observations explicitly combine 68 preserved results
from a collection-failed gate with four successful fresh false-call controls.
FormArmy and AltPrimeDefinition are VVV in both axiom settings. Composite is RRV
and RemoveFactor RRR; neither is claimed repaired. All sixteen positive controls
verify and all eight negative controls are genuinely Invalid. The prior failed
gate is not reclassified as successful and no completed scope is discarded.

A complete eighteen-observation native RemoveFactor exit-scope diagnostic retains
every original exit preparation/check/summary, current reveal context and fuel
traversal, and normal procedure contracts. At seeds 0/1/7 in both axiom settings,
native and scoped positives both remain RRR. All six native outcome/resource
controls and both seed-zero complete-query controls are exact; twelve actual
check/full-attribute comparisons and exit-command counts are exact. Independent
WF remains Correct and all six false-entry controls are genuine Invalid VCs.
The diagnostic's successful execution validates that controlled negative result,
not a repair or product acceptance. The scope construction is not adopted.
There is no support deletion, raised ceiling, source hint, whole-suite/library
iteration or development port.


## Ordinary assertion oracle and terminal-scope controls

On current product `991b60037`, a source-level diagnostic records all 44 native
observations for RemoveFactor seed 0 and Composite seeds 0/1, in both option and
additional-axiom settings. Each uses the original source, an inserted assertion
of its own obligation, or an `assert true` at the same position. Eight separate
false-entry controls contain genuine Invalid VCs at the inserted assertions.
The six unchanged enabled controls reproduce prior outcomes and resource vectors;
all four seed-zero complete solver streams match exactly. Original ceilings,
options, batches, per-process cores and the accepted compiler/Z3 5.1.0 remain.
The inserted assertions are disposable diagnostics, not product proof hints.

The native collection gate is rejected, and that result remains preserved. Its
collector incorrectly requires nonempty independent specification-WF JSON and
rejects any warning despite retaining the original `--allow-warnings` policy.
Both selected methods emit no independent WF result or WF implementation here.
Each observation has the same existing bodyless-method warning, shifted by the
one inserted source line where applicable. A separate offline audit confirms
all 44 source hashes/results, that exact warning policy, six faithful controls
and eight genuine negatives. This audit does not turn the rejected gate into a
successful native gate, establish warning-free whole-project verification, or
supply independent specification-WF proof evidence.

Both additional-axiom settings have the following results. V is Correct and R
is OutOfResource. The three columns are different source variants, not seeds.

| Target / seed / option | Original source | Own-proposition assertion | Same-position `assert true` |
| --- | --- | --- | --- |
| RemoveFactor / 0 / off | V | R | R |
| RemoveFactor / 0 / on | R | R | R |
| Composite / 0 / off | V | V | V |
| Composite / 0 / on | R | V | R |
| Composite / 1 / off | V | V | V |
| Composite / 1 / on | R | R | R |

RemoveFactor's inserted assertion appears after the original terminal conditional.
The generated Boogie adds eleven push/pop pairs throughout that preceding
conditional/calculation when either final statement is inserted; the original
and current implicit-check variants have zero such pairs. Thus the
ordinary source-assertion comparison has a terminal-scope confound; even the
true assertion exhausts the legacy proof. The explicit assertion
and current implicit exit equality have exact expression tokens in both axiom
settings, including both product fuel layers. This bounded comparison excludes
the different dependency IDs and the explicit split assertion's subsumption
attribute; it does not claim full command/metadata equivalence. Matching
resource counts for the explicit and implicit failures do not imply identical
solver streams; those streams differ. This is neither a repair nor evidence
that the current implicit check loses fuel.

Composite's assertion is placed immediately before its existing recursive call:
`assert 2 <= a && !IsPrime(a);`. It restores the enabled seed-zero proof, but
seed one still exhausts resources; the true assertion restores neither seed.
All these Composite variants have the same three push/pop pairs, so that
particular terminal-scope confound does not explain its seed-zero improvement.
Whole-method success alone does not identify the failing local obligation or
prove that its support was missing. The explicit assertion publishes preparation
and its established proposition in the surrounding continuation, while the
current ordinary caller uses a certified local proof branch and original normal
call-contract publication. Those contexts and the number of checks differ.

There is also a concrete preparation boundary to test: the explicit assertion
prepares the whole conjunction with its short-circuit guard, whereas the current
caller prepares its top-level conjuncts separately, using the preceding checked
conjunct as a premise. These guards are logically equivalent at their checks,
but their generated control flow differs. A
[scratch compiler comparison](https://github.com/erniecohen/dafny/actions/runs/37933841924)
changes only that caller source-clause boundary, retaining the existing complete
preparation, splitting, sole checks, pure-scope policy and original contracts.
The experimental policy applies generally to ordinary checked calls; selected
names bound the observation and false-entry control, not a product rule.
All six actual compilation/assembly stages pass. The corresponding macOS
24-observation comparison completes with its frozen inputs unchanged.
A [second scratch build](https://github.com/erniecohen/dafny/actions/runs/37935823909)
compiles the same diagnostic for both native platforms; all eight actual stages
pass. Each platform retains the exact accepted compiler bundle except its own
new Core and LanguageServer assemblies. No product adoption, support deletion,
global fuel change, new axiom or raised ceiling follows from compilation or the
source-oracle observations.

### Whole caller source-clause result: partial macOS improvement, rejected adoption

Both native comparisons complete all 24 observations with Z3 5.1.0: Composite
seeds 0/1, repaired FormArmy seed 0 and unchanged-exit RemoveFactor seed 0, in
both axiom settings, each with native, whole-clause and false-entry variants.
On each platform, all eight native controls reproduce prior outcomes and resource
vectors, and all six seed-zero complete solver streams match exactly. All eight
false-entry controls per platform are genuinely Invalid, and all reported
independent WF results are Correct. The identical controls establish native
fidelity only for this measured scope.

On each platform, every one of the sixteen actual-expression comparisons is
exact, with unchanged check counts, source origins, triggers and fuel terms.
Both strict gates nevertheless reject twelve comparisons because dependency ID
attributes change; only FormArmy retains every full attribute. An offline
inspection finds only ID-string changes in the compared attributes, with a
one-to-one mapping; it does not amend either strict gate or validate dependency
objects, coverage entries or descriptions. Both rejected gates and their complete
evidence remain preserved.

| Target / tested seed | Native control, both platforms | Whole clause, macOS | Whole clause, Linux |
| --- | --- | --- | --- |
| Composite / 0 | OutOfResource | Correct | OutOfResource |
| Composite / 1 | OutOfResource | OutOfResource | OutOfResource |
| FormArmy / 0 | Correct | Correct | Correct |
| RemoveFactor / 0 | OutOfResource | OutOfResource | OutOfResource |

Both axiom settings give the same results. FormArmy and RemoveFactor also retain
identical complete native/whole-clause solver streams and resource vectors on
each platform. Composite's streams change within each platform. A cross-platform
comparison finds all 24 complete canonical solver inputs through the final
`check-sat` exactly equal, including command order, options and quantifier IDs.
All actual-check expression/origin/full-attribute fingerprints also match across
platforms. Only whole-clause Composite at seed zero has a different verdict and
resource vector. Both solvers report version 5.1.0, but their recorded native
binary hashes differ.

The seed-zero result therefore records proof-search behavior that differs between
the native solver builds on identical complete inputs. It does not identify CPU,
OS, compiler, floating-point behavior, quantifier instantiation or missing facts
as the cause. The partial macOS improvement does not settle Composite seed one or
RemoveFactor, and the strict metadata restriction remains. Adopt no product
change or general resource-repair claim from this construction.

### Two exit argument-allocation facts do not repair RemoveFactor

The saved current Boogie identifies two additional private set-argument allocation
assumptions immediately before RemoveFactor's sole fallthrough postcondition
check. A [scratch compiler](https://github.com/erniecohen/dafny/actions/runs/37940625142)
passes all eight actual compilation/assembly stages and tests only that boundary.
The diagnostic traverses the original complete preparation and fuel path, then
omits exactly those two `$IsAlloc(t, TSet(TInt), $Heap)` assumptions. It requires
the frozen private argument, integer-set type and current heap, and rejects
unsupported commands or source writes. All private assignments, other WF/can-call
support, the actual check, normal summary, procedure contracts and branch
transfers remain. This deliberate support omission is causal diagnosis and is
excluded as a product fix.

All six native observations complete at seed zero with Z3 5.1.0 and the original
sources, ceiling, core count, warning policy and batches: native, two-fact omission
and false-entry variants, in both axiom settings. Both native controls reproduce
prior outcomes, resource vectors and complete solver streams. All four strict
actual-expression/full-attribute comparisons pass, including dependency IDs;
every variant retains the same thirteen actual checks. Both false-entry controls
are genuinely Invalid, and every reported specification-WF result is Correct.
The complete diagnostic gate passes; this does not make its positive proof
observations successful.

| RemoveFactor / seed zero, both axiom settings | Result |
| --- | --- |
| Unchanged current | OutOfResource |
| Only the two private exit allocation facts omitted | OutOfResource |
| False-entry control with the same omission | Invalid |

Omitting this two-fact subset is insufficient to repair the selected regression.
The changed resource count does not establish zero search influence, and no new
quantifier profile or causal instantiation chain is inferred. Retain the result
and reject adoption; RemoveFactor remains unresolved. No whole-suite/library
rerun or development-line port follows from this diagnostic.

## Quantified fuel identity from the new issue comment

The [reported identity example](https://github.com/erniecohen/dafny/issues/100#issuecomment-6073435411)
is related to the assertion/implicit-check mismatch and is already repaired by
current product `991b60037` in this measured scope. The
[unchanged reproducer](examples/obligation-fuel-identity.dfy) uses a recursive
`End` function under a nested universal/existential formula. Its lemma has the
same formula as both precondition and postcondition and an empty body. A
nonrecursive predicate wrapper is the reported workaround.

A complete focused comparison uses the accepted native compiler and Z3 5.1.0,
default resolver/seed, normal batches and warning policy, with additional axioms
off/on and consistent obligation checks off/on. Sixteen complete-file invocations
produce 28 targeted correctness observations: original raw and wrapped identities,
the immediate own-proposition assertion variant, false-entry variants of both
identities, and the symbolic witness `End(s, [v]) == v`. Supporting function
checks and every reported specification-WF check are Correct, without warnings.
All twelve wrapped/witness positive controls have only Valid VCs, and all eight
false-entry controls contain genuine Invalid VCs. The complete diagnostic gate
passes; the retained default-off proof failures are expected observations.

| Case, both additional-axiom settings | Default off | Current opt-in |
| --- | --- | --- |
| Raw `requires P; ensures P; {}` | Postcondition fails | Verifies |
| Same raw identity with immediate `assert P` | Assertion proves; postcondition fails | Verifies |
| Predicate-wrapped identity | Verifies | Verifies |
| Symbolic `End(s, [v]) == v` witness | Verifies | Verifies |
| False assertion in either identity | Invalid | Invalid |

The generated Boogie explains the important distinction. The default-off
precondition states and triggers the existential's recursive application at
`$LS($LZ)`, while its checked procedure postcondition uses
`$LS($LS($LZ))`. The explicit assertion uses `$LS($LZ)` and succeeds, but leaves
the old checked postcondition at the mismatched layer. In current opt-in mode,
the sole body-exit check also uses `$LS($LZ)`, including the existential trigger.
Its printed expression is token-identical to the precondition and explicit
assertion under capture-free renaming of the two distinct quantified binders,
including types, operators, free identifiers and fuel terms. The remaining
fuel-two procedure postcondition is free/nonchecking; it is not a second
implementation obligation.

This uses the existing explicit assertion policy that decreases fuel locally
when translating an existential assertion. No global expression/fuel policy is
rewritten, and no new product change, background axiom, redundant proof or source
hint is needed for the original example. The failure does not refute the source
identity; additionally, the verified symbolic witness shows how `End` can reach
any natural value. This is evidence for the exact example and settings, not a
universal guarantee for every quantified identity or a repair of Composite and
RemoveFactor. No whole-suite/library run or development-line port is involved.

## Pure exit preparation does not explain RemoveFactor exhaustion

A completed six-observation comparison retains the sole exit check and normal
publication while omitting all seven certified pure exit-WF command emissions
for RemoveFactor seed zero, in both additional-axiom settings. The complete
original preparation and translator/fuel traversal still run. Leading can-call
support, actual formulas, every assertion attribute, procedure contracts and
branch transfers remain unchanged. Guards reject escaped private arguments and
any uncertified preparation. The
[scratch compiler](https://github.com/erniecohen/dafny/actions/runs/37949568995)
passes all eight actual compilation/assembly stages.

Both untouched controls reproduce their prior complete solver input, outcome
and resource count exactly. All four actual-check/full-attribute comparisons
pass, both false-entry controls have genuine Invalid VCs, and every reported
independent WF result is Correct. The complete diagnostic gate passes. Both
positive preparation-omission observations still exhaust the original resource
ceiling; this remains a rejected repair, not positive verification acceptance.
No support omission is adopted.

The separate unused-declaration hypothesis has no supporting input evidence:
the selected original and private-argument-substitution solver inputs contain
no unused private constants, and have the same three unused background function
declarations. No additional proof run is needed for that hypothesis.

The support-preserving placement comparison below moves the unchanged fragment
to terminal paths and audits exactly one mandatory check per reachable exit.
It rejects that alternative as a repair. The original motivation incorrectly
assumed that legacy procedure postconditions remained separate per terminal
path throughout Boogie. The pinned Boogie implementation also unifies returns,
as confirmed below. Composite, RemoveFactor and complete review acceptance
remain open.

## Terminal-path exit placement is also a rejected repair

A support-preserving scratch comparison moves the unchanged certified exit
fragment from the common exit block to its two terminal predecessor paths. It
keeps every preparation command, leading can-call support, actual check, normal
summary and procedure contract. All other body command objects and transfers
remain unchanged. A control-flow audit requires exactly one mandatory check on
each of the two reachable terminating paths. The additional static exit check
represents the second path; no path checks its clause twice.

The first trial is retained as a failed diagnostic: copying statement dependency
IDs violates Boogie's uniqueness rule, so the relocated variant stops at
resolution before producing a proof result. Its native control completes. The
[corrected compiler](https://github.com/erniecohen/dafny/actions/runs/37952630209)
passes all eight actual stages and preserves the first exit's original entries.
The second receives fresh IDs and distinct dependency objects with equal source
ranges, descriptions and policies. Runtime guards require additional entries in
the original coverage set and restoration of the manager's declaration context.

All six corrected native observations complete at RemoveFactor seed zero, for
both additional-axiom settings. Both native controls reproduce complete solver
inputs, outcomes and resource counts exactly. Four comparisons preserve every
unique check expression, origin and non-ID assertion attribute; the necessary
additional dependency IDs are validated separately rather than described as
literal full-attribute equality. Both false-entry controls contain genuine
Invalid VCs, and every reported independent WF result is Correct.

Both relocated positive observations still exhaust their original resource
ceiling. The complete diagnostic gate passes, but terminal-path placement is
rejected as a repair within the explicitly recorded fresh-metadata boundary.
Neither scratch exit construction is adopted. This result does not establish
that all placement choices have no search effect or settle the remaining
Composite/RemoveFactor regressions.


Source inspection of the exact pinned Boogie release corrects the initial
placement hypothesis: [GenerateUnifiedExit](https://github.com/erniecohen/boogie/blob/v3.5.5%2Breview.37e4435d/Source/VCGeneration/Transformations/DesugarReturns.cs#L11)
joins multiple returns before [InjectPostConditions](https://github.com/erniecohen/boogie/blob/v3.5.5%2Breview.37e4435d/Source/VCGeneration/Transformations/DesugarReturns.cs#L63)
adds checked procedure postconditions. Thus the legacy pipeline also has a
common postcondition check. The completed terminal-path diagnostic is a
support-preserving alternative, not a faithful reconstruction of the old CFG,
and its rejection does not establish that the current implementation newly
introduced a join. Both control-flow audits and the native results remain valid
within their stated scope.

## Exact native unified-exit construction produces the same solver input

The separate-terminal-path experiment above does not reconstruct legacy behavior.
The exact pinned Boogie pass constructs a common `GeneratedUnifiedExit` when
there are multiple terminal returns. The [focused scratch compiler](https://github.com/erniecohen/dafny/actions/runs/37957404907)
passes all eight actual stages, using accepted structural revision `df4c7066f`
and the corrected normal compiler bundle. Only the diagnostic core and language
server components are rebuilt; every other native binary remains byte exact.

The completed native comparison restores the two terminal returns in RemoveFactor
and invokes that public pass, then appends the original certified preparation,
check and publication fragment unchanged. It retains every original command once,
the sole static postcondition check, all full dependency attributes, original fuel
traversal and one mandatory check on each reachable exit path. All six observations
complete at seed zero, both axiom settings and the original ceiling. Both native
controls reproduce the preceding recorded outcome, resource vector and complete
solver stream; four strict actual-check/full-attribute comparisons pass, both
false-entry controls have genuine Invalid VCs, and reported independent WF results
remain Correct.

The native and unified variants have **identical complete solver inputs through
the last check-sat**, in both axiom settings. Both exhaust the original resource
ceiling with identical resource vectors. This rules out this terminal-construction
change as a repair more directly than outcome equality alone. No construction is
adopted. The new compiler's faithful native controls confirm only this bounded
reference scope; they do not reidentify the earlier complete native fixture gates.

## Current-candidate family diagnosis

The preceding family attribution belongs to product `fe6ddd1ba`, before the
caller-publication correction. The [current-candidate compiler](https://github.com/erniecohen/dafny/actions/runs/37959795786)
passes all eight actual stages, using accepted structural revision `df4c7066f`
and the normal compiler bundle. A first attempt compiled successfully but its
scratch audit accessed an implementation's unresolved procedure link and aborted
before verification. That failed attempt contributes zero proof observations.
The corrected audit finds the procedure directly by declaration name, changing
no translated proof commands.

The corrected native gate completes all 36 observations at original sources,
ceilings, options and one core. It selects only Composite seeds zero/one and
RemoveFactor seed zero, both axiom settings. Twelve current/legacy controls
reproduce exact prior complete solver streams, resource vectors and outcomes.
Twelve false-entry controls contain genuine Invalid VCs; reported independent
WF results remain Correct. Each hybrid retains its required proofs: legacy exit
translation keeps checked procedure ensures, while canonical exit translation
checks locally and has nonchecking procedure copies. Legacy call translation
retains its checked requirements. The remaining family also applies inside exit
WF preparation; “body-only” is shorthand, not an assertion of zero interaction.

| Target / seed | Legacy | Current full | Exit family only | Remaining family only |
| --- | --- | --- | --- | --- |
| Composite / 0 | Correct | OutOfResource | Correct | OutOfResource |
| Composite / 1 | Correct | OutOfResource | Correct | Correct |
| RemoveFactor / 0 | Correct | OutOfResource | OutOfResource | Correct |

Both axiom settings give the same table. RemoveFactor's exit attribution is
reproduced on the current candidate. Composite seed zero's remaining-family
failure is also reproduced, but seed one's full-mode failure is an **interaction**:
both isolated families verify, unlike the earlier candidate's body attribution.
This is causal localization, not a product repair. It does not justify disabling
one family for named cases or omitting any obligation/support.

The next focused exit comparison changes placement before native Boogie names
structured blocks and assigns successors. The prior terminal-path relocation
happened after that construction and cannot settle this boundary. Its selected
source ends with a conditional and two simple fallthrough tails. The same certified
exit commands move into those tails before CFG resolution, one mandatory check
on each path, unchanged fuel/preparation/publication and validated fresh source
dependencies. Compiler and native validation remain pending; no construction is
adopted or positive proof result claimed.


## Pre-CFG terminal placement reproduces the same solver input

The [corrected scratch compiler](https://github.com/erniecohen/dafny/actions/runs/37964711809)
passes all eight actual stages using the accepted structural compiler as its base.
The first attempt stopped before verification because its source-command audit
rejected a normal nested else-if; that failure remains preserved with zero proof
observations. The correction traverses that structure for retention auditing only.

All six corrected native observations complete at the original source and ceiling,
with two faithful complete-input/outcome/resource controls, four strict unique
actual-check/origin/non-ID-attribute comparisons, Correct reported WF results and
two genuine Invalid entries. The full exit fragment is prepared at the original
local point and placed in the two structured terminal branches before native
block naming and successor construction. Original source commands remain once;
one branch retains the original fragment and the other has validated distinct
source dependency/coverage entries. There is one mandatory postcondition check
on each actual exit path. Fuel, preparation and normal publication remain.

RemoveFactor seed zero still exhausts resources in both axiom settings. More
strongly, all six complete solver streams through the final check-sat are identical
to their corresponding earlier post-CFG terminal-placement streams. Thus moving
this same terminal-copy construction earlier does not expose a different search.
Reject it as a repair; neither scratch construction is adopted.

A remaining bounded exit-skeleton comparison withholds the already generated
fragment before root statement collection, lets native Boogie construct the two
terminal returns naturally, then uses the exact pinned unified-exit pass and
appends the unchanged fragment once. This differs from reconstructing returns
after CFG generation and from copying the check into two branches. Its completed compiler/native result follows; it is not a product policy.


## Naturally constructed exits still produce the native query

The [source-skeleton scratch compiler](https://github.com/erniecohen/dafny/actions/runs/37966133403)
passes all eight actual stages against the accepted structural base. All six
native observations complete, with two exact complete-input/outcome/resource
controls, four strict actual-check/full-attribute comparisons, Correct reported
WF results and two genuine Invalid false entries.

The already prepared pure exit fragment is withheld before native statement
collection, anonymous naming and successor construction. The resulting two
native terminal returns go directly through the pinned `GenerateUnifiedExit`
pass; no return or block is reconstructed afterward. Its new shared exit then
receives the entire original fragment once and in order. All original source
and native body commands, fuel traversal, preparation, publication, actual
formulas and full dependency attributes remain. Both actual exit paths have
one mandatory check, with thirteen static assertions and no dependency copies.

Native and transformed complete solver streams through the last check-sat are
exactly identical in both axiom settings. RemoveFactor seed zero still exhausts
resources with the same count. This rejects the remaining source-skeleton
construction as a resource repair, beyond the earlier post-CFG unification and
pre-CFG terminal-copy results. None is adopted. Current semantic product
`991b60037` is unchanged; Composite/RemoveFactor resource acceptance remains
open, with no proof hint, support omission, raised ceiling, full-suite iteration
or development port.


## Individual checks verify when isolated

A fresh complete thirty-observation diagnostic uses the accepted structural
compiler and Z3 5.1.0, at the existing failing Composite seeds zero/one and
RemoveFactor seed zero, in both axiom settings. Each has ordinary enabled/off
controls, corresponding `--isolate-assertions` observations and a source false-entry
control with isolation enabled. All twelve ordinary controls reproduce their
recorded complete solver streams, outcomes and resource vectors exactly.
All six negatives contain genuine Invalid VCs at the exact inserted assertions;
all reported independent WF results remain Correct.

Every positive isolated declaration verifies: Composite has twenty-two individual
batches and RemoveFactor thirteen, in both enabled and legacy paths. There is
no individually unproved obligation in this bounded scope. The ordinary enabled
combined VC still exhausts resources in every selected case. This points to
interaction in the combined verification condition rather than a missing local
proof check; it does not identify a universal quantifier or arithmetic cause.

Isolation changes batching and aggregate available budget. These successes are
not proofs under the original single combined ceiling, product repairs or a
request to globally isolate assertions. Original sources and per-batch limits
remain fixed apart from the explicit negative-control insertion. A narrower
scratch comparison marks only freshly generated actual caller/exit contract
checks, retaining every formula, existing full attribute/dependency ID, support,
fuel, publication, transfer and static check count. Other source assertions are
unmarked. The completed contract-only comparison follows; no grouping policy is
adopted.


## Contract checks verify; the remaining body group still exhausts resources

The [contract-only scratch compiler](https://github.com/erniecohen/dafny/actions/runs/37969057895)
passes all eight actual stages against the accepted structural compiler. All
eighteen native observations complete with Z3 5.1.0, unchanged sources and
per-batch ceilings: Composite seeds zero/one and RemoveFactor seed zero, both
additional-axiom settings, each with native, contract-isolated and false-entry
variants. All six native controls reproduce complete solver inputs, outcomes
and resource vectors; all twelve comparisons preserve actual check formulas,
original full attributes and dependency IDs except the added isolation marker.
All six false controls contain genuine Invalid VCs and every reported independent
WF result is Correct.

Only the actual locally generated caller and exit contract checks are marked.
No ordinary source assertion is marked, and no preparation command, fuel choice,
publication, transfer or original static check is changed. Composite verifies
at both selected seeds in both axiom settings, with eight isolated contract
checks and one remaining body batch.

RemoveFactor remains OutOfResource in both settings. Its isolated postcondition
and recursive-call precondition are Valid. The failing third batch contains the
eleven original ordinary body checks: PickLargest definedness, calculation and
explicit assertion steps, and recursive termination obligations. Every one of
these checks verifies separately in the preceding global-isolation diagnostic.
This establishes the location of the remaining grouped failure; it does not
identify a particular false or individually unprovable body obligation.

The pinned Boogie split implementation retains the preceding commands for an
isolated assertion and prunes future or unrelated blocks. In the remainder,
isolated assertions become assumptions under their existing subsumption policy;
blocks no longer leading to a checked assertion are pruned. Thus local preparation
and fuel remain, but complete-query trigger-visible context changes. Isolation
also changes the total available budget. Neither successful isolation result is
an unchanged combined-query acceptance result or evidence of uniform improvement.
The contract-only construction does not repair both regressions and is rejected
as a product remedy. Do not add automatic isolation to the single-check option.

The current semantic implementation remains unchanged. Original combined-query
Composite and RemoveFactor resource acceptance stays open. No source proof hint,
resource increase, broader suite iteration or development-line port is adopted.


## One duplicate local can-call assumption: validation pending

Source inspection finds a difference from immediate assertion preparation:
`TrStmt_CheckWellformed` already ends with the whole proposition's can-call
support, while declared-exit lowering additionally emits its earlier-created
leading support. For pure normalized preparation the leading command is already
commuted to follow that same WF fragment. The two selected RemoveFactor formulas
are expected to be structurally identical; native validation must establish this,
not printed text or logical-equivalence guessing.

A [focused scratch compiler](https://github.com/erniecohen/dafny/actions/runs/37973692714)
is being built to test omission of only this extra command. It requires native
structural expression equality and a strict lexical fingerprint, absent attributes
on both assumptions, and retention of every original preparation command once.
Expression construction and translator traversal still occur at their original
points. All distinct support terms, fuel, actual check formulas/full attributes,
publication and transfers remain. It scans only this proposition's newly generated
preparation, with no body/context term collection or new supporting instance.

All six native observations complete. The assumptions are structurally identical
and have identical strict fingerprints; both native controls reproduce complete
solver input, outcome and resource count, all four actual-check/full-attribute
comparisons pass, all reported WF results are Correct and both false entries
contain genuine Invalid VCs. Every original WF preparation command remains once.
RemoveFactor still exhausts resources in both axiom settings. Removing this
strictly duplicate command is therefore rejected as a resource repair and no
product change is adopted.
The separate assertion-command-type audit finds a distinct postcondition error
wrapper and optional expansion metadata, but no direct solver-generation subtype
branch for this selected case. The subsequent wrapper-only comparison tests
possible visitor effects explicitly; no product check-type change follows from
the source audit.


## Original postcondition wrapper: validation pending

The [wrapper-only scratch compiler](https://github.com/erniecohen/dafny/actions/runs/37976081754)
compares the original Boogie `AssertEnsuresCmd` shape with the current local
assertion. The wrapper contains the exact actual checked expression as command
metadata; it is not inserted into a procedure contract. The procedure's original
free contract remains, with no second checked ensures. All other commands retain
their object identities and original positions; preparation, fuel, publication,
transfers, thirteen static checks and every actual expression/full attribute
remain. Description and error-mining metadata also retain their references.

The source audit above finds no direct subtype switch in solver generation for
this selected case, but standard visitors additionally visit the wrapper's
ensures condition. That audit cannot establish native input identity or rule out
all traversal effects. Require unchanged full-input/outcome/resource controls,
strict actual-check comparisons and genuine Invalid entry controls before drawing
any conclusion. The native comparison remains pending; no check-type change is
adopted as a product correction.
