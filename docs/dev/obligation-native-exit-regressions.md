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
