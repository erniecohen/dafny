# Current caller-publication native validation

Semantic/product `991b60037` separates the original inherited/filtered free-call
interface from ordinary calls whose preconditions have one local proof. Only
certified pure caller preparation is placed in the existing local proof branch;
statement-expression reveals and unsupported effects retain their outer scope.
The original contract supplies normal post-call publication. No background axiom,
global fuel policy, support omission, source proof hint or raised limit is used.

The [accepted compiler](https://github.com/erniecohen/dafny/actions/runs/37914777052)
passes all ten actual stages, 104 obligation tests in each mode, 382 core tests,
both native platform bundles/probes, the editor build and the unchanged reviewed
287-group producer inventory. The candidate-matched direct Boogie gate passes
all eleven contract/publication controls, including genuine assertion failures
for the false local proof branch, inherited/free-call exclusion and post-call
state invalidation. Compiler results and direct Boogie controls are distinct
from native Dafny acceptance.

## Retained native observations

The original 68 observations complete on the current accepted native bundle with
Z3 5.1.0, original sources, resource ceilings, options and per-process core count.
Both additional-axiom settings produce the same vectors below. Positions are
seeds 0, 1 and 7; V is Correct and R is OutOfResource.

| Target | Recorded baseline | Preceding product `fe6ddd1ba` | Current product `991b60037` |
| --- | --- | --- | --- |
| Power subtraction | VVV | VVV | VVV |
| AltPrimeDefinition | VVR | VRR | VVV |
| Composite | VVV | VRV | RRV |
| ExtensibleArray.Append | VVV | VVV | VVV |
| FormArmy | VRR | RRR | VVV |
| RemoveFactor | VRR | RRR | RRR |
| Iterator.MoveNext | VRR | RVV | RVR |

All sixteen positive controls verify, including the original issue and subset
reproducers. All four preceding negative controls contain genuine Invalid VCs;
all reported independent specification-WF results are Correct. Six default-off
Power observations reproduce the recorded baseline target outcomes and complete
resource vectors exactly. Baseline and preceding product evidence are explicitly
reused; they are not new executions or complete compatibility evidence.

The original full submission includes four additional direct calls to a method
with literal `requires false`, under both resolver and additional-axiom settings.
Its collection gate incorrectly requires a nonempty independent WF result for
that trivial specification. The nonzero gate result and its 68 completed
observations remain preserved. A fresh four-only gate records each process exit,
requires a genuine Invalid VC at the source call, and accepts an empty WF list
only for this literal fixture while requiring every emitted WF result Correct.
All four controls pass those expectations. No completed observation is rerun.

The current product therefore has 72 completed observations explicitly combining
68 retained observations and four successful follow-up controls. All sixteen
positive controls verify and all eight negative controls are genuinely Invalid.
This is not a single successful 72-observation gate or product acceptance.

## Printed-interface audit

A comparison of 142 original/checked-call interface pairs in 34 emitted programs
finds equal requirement-expression tokens after lexical value-binder renaming.
Free identifiers, types, full quantifier attributes and triggers, strings,
fuel/layer terms and every other expression token remain exact. The preceding
raw comparison is unequal solely because fresh lexical bound names differ.
Controls distinguish nested shadowing, capture, free-identifier changes, trigger
changes and fuel changes, and retain positive alpha equivalence. This is a
bounded printed-expression audit; leading requirement metadata is excluded,
and it is not a legacy-binary comparison or a universal fuel claim.

## Remaining work

FormArmy and AltPrimeDefinition now verify at every tested seed/configuration.
Composite and RemoveFactor remain unresolved resource regressions. Iterator is
partly successful in both baseline and current translation and is classified
under review section 13; its seed changes do not justify case-specific product
tuning. The candidate is not uniformly better than legacy and review acceptance
is not complete.

A [compiler-only exit-scope diagnostic](https://github.com/erniecohen/dafny/actions/runs/37918822079)
passes all six actual stages. Its 18-observation focused native gate also
completes: unchanged native, scoped exit proof and a false entry assertion, at
seeds 0/1/7 in both axiom settings. It retains the complete RemoveFactor exit
proof, including preparation, checks, split summaries, clause order, fuel
traversal, active reveal state and normal procedure contracts, inside the
existing proof branch. Preparation is certified pure. All six native controls
reproduce original outcomes and resource counts; both complete seed-zero solver
streams match. All twelve comparisons retain every actual check expression and
full attribute exactly, with the same exit-command counts. All reported WF
results are Correct and all six false-entry controls are genuinely Invalid.

The scoped positive remains RRR in both settings. Thus retaining support in a
local exit proof branch does not repair RemoveFactor at any tested seed. The
completed diagnostic gate validates the experiment and its controls; it does
not establish a positive proof result. This construction is rejected as a
resource repair and is not adopted in the product.

The complete current registered gate passes all 298 committed invocations with
the exact expected output: 73 positive observations have only Valid VCs, and all
225 expected negative observations contain genuine Invalid VCs. Eight expected
no-trigger warnings occur only in the negative-forall fixture. The helper
includes its original terminal-reveal control and new false-precondition call
outside the paired manifest. Invocation counting is checked before submission;
its committed body and expected output remain exact, with observational native
CSV/VC JSON logging and genuine Invalid checks.

The separate complete gate for all 172 paired/negative fixtures passes all
688 observations across both resolvers and additional-axiom settings. All 440
positive observations have only Valid VCs, and all 248 negative observations
contain genuine Invalid VCs. Source hashes match the committed fixtures; every
reported outcome is Valid or Invalid, without resource exhaustion, unknown or
timeout. Eight expected no-trigger warnings are confined to negative-empty-domain
and negative-forall. Both focused gates use the accepted native
`4.11.0+fcb2042d.review.c345966c` bundle for product `991b60037` and Z3 5.1.0,
with original sources, options, resource ceilings and per-process core count.

These are two independently completed gates, with distinct scopes and
expectations. They validate the current single-check, reveal-scope and call
publication fixtures. They do not rerun the whole verifier suite or standard
library, establish a repair for remaining resource regressions, or discharge
complete review acceptance.

Strict complete default-off library resource compatibility, ordinary required
CI and independent soundness review remain open. No whole-suite/library iteration or development-line port
is launched. Porting to PR #169 still requires explicit owner approval.

## Original contract reuse diagnostic: rejected resource repair

The product translates the checked-call publication interface separately from
the original interface. A
[scratch compiler](https://github.com/erniecohen/dafny/actions/runs/37924616057)
passes all six actual compilation/assembly stages for a focused native comparison
that instead copies the original translated contract. It retains expression
objects, formal/type bindings, frame expressions and descriptions, marks only
ordinary locally checked user requirements, and changes no actual caller/exit
check, preparation or fuel.

All 24 observations complete with Z3 5.1.0, original source inputs/options,
resource ceilings and core counts: Composite seeds 0/1, RemoveFactor seed 0 and
already repaired FormArmy seed 0, in both additional-axiom settings, each with
native/copy/false-entry variants. All eight native controls reproduce original
outcomes and resource vectors; all six seed-zero full solver streams match the
recorded native inputs. All sixteen actual-check/full-attribute comparisons
pass, all reported independent WF outcomes are Correct, and all eight entry
controls contain genuine Invalid assertions.

More strongly, every copied/native pair has the exact same complete solver
stream, single-batch resource vector and outcome, including Composite seed 1.
Composite remains OutOfResource at both seeds, RemoveFactor remains
OutOfResource, and FormArmy remains Correct in both settings. Specification
retranslation is therefore not a resource cause in this tested scope. Reject
this construction as a resource repair and do not adopt it in the product.
Successful diagnostic execution is distinct from positive proof acceptance.

Source inspection also finds a bookkeeping restriction in that first scratch
compiler: fresh dependency IDs reference the original dependency objects. The
coverage manager stores objects in a per-declaration set, so those entries merge,
unlike the distinct entries from separate translation. Fresh IDs alone do not
preserve that bookkeeping. That diagnostic cannot be adopted as-is.

Scratch revision `d58172ea8` captures factories at original dependency creation,
retaining the exact original source origin and Dafny expression. Each copied
contract receives a fresh dependency object and ID; runtime guards require one
new entry in the original per-declaration coverage set and matching dependency
type, range and description. Its
[compiler-only validation](https://github.com/erniecohen/dafny/actions/runs/37927075933)
passes all six actual compilation/assembly stages. Those native dependency
guards have not executed. Following the unchanged solver-stream refutation,
no native gate is launched for this metadata correction and neither scratch
construction is adopted. The original frozen evidence is retained.


The [ordinary-assertion comparison](obligation-native-exit-regressions.md#ordinary-assertion-oracle-and-terminal-scope-controls)
preserves 44 produced native observations and its rejected collection gate.
RemoveFactor's legacy source also exhausts resources after a final `assert true`,
which changes preceding terminal scopes. Composite's inserted own-precondition
assertion restores enabled seed zero but not seed one. An offline audit confirms
six faithful controls and eight genuine negatives without replacing the failed
native gate or claiming independent WF evidence. The whole-caller-clause
preparation comparison is scratch-only and pending; both resource regressions
remain unresolved.
