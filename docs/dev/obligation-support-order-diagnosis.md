# Native assertion-local support ordering

A completed 36-observation native diagnostic uses the preceding `a709df226`
verifier, Z3 5.1.0, macOS arm64, both additional-axiom settings and seeds 0, 1
and 7. It changes only the placement of one existing leading can-call assumption
within the generated body of `Composite` or `RemoveFactor`. All original command,
actual check and branch-transfer objects are retained. Fuel, facts, source state
and reveal scopes remain intact.

The [scratch build](https://github.com/erniecohen/dafny/actions/runs/37802569505)
records six successful actual stages. The diagnostic verification frontend is
assembled onto the preceding public build; every other binary, including the
verifier core and original driver, is byte exact. Applied source hashes and
assembly provenance are recorded. The twelve unchanged controls reproduce all
preceding native outcome/resource vectors; the four available seed-zero queries
also match exactly. All twelve false-entry controls contain actual Invalid VCs,
and every reported independent specification-WF result is Correct.

Both axiom settings give the following vectors. `V` means Correct and `R` means
OutOfResource; positions are seeds 0, 1 and 7.

| Target | Current native control | Existing support moved after pure WF |
| --- | --- | --- |
| `Composite` | VRR | VRV |
| `RemoveFactor` | RRR | RRR |

The movement improves Composite at seed 7 and reduces its proof cost at seed 0;
seed 1 still exhausts the original ceiling. RemoveFactor is unaffected. This
identifies a native query-order effect, rather than missing fuel or check terms.
It does not establish a complete repair or product acceptance.

Earlier printed-Boogie replays are not promoted to native evidence: Composite's
unchanged replay did not reproduce native verification. A first native hook was
attached to the unused legacy translation entry and produced no audit. A later
attempt completed Composite but rejected RemoveFactor because the diagnostic
locator missed its conjunction of can-call facts. All incomplete attempts remain
separate from this completed 36-observation result.

## General local correction

The product candidate creates the existing leading support expression at its
original point, preserving its expression, origin and attributes. It emits that
same command after certified WF preparation only when normalization accepts the
whole fragment and the support expression reads none of its assigned or havoced
variables. Unsupported fragments keep the original order. The decision examines
only the freshly generated local fragment and actual marked private arguments;
it collects no terms from surrounding body commands.

An assume changes no state. In an eligible fragment, only unique private argument
bindings and fresh WF havoc choices can change variables; no source/heap write,
call, real check, scope change or transfer is permitted. The support expression
is independent of every such write. If it is false, both orders block the path;
if true, both admit the same choices and retain the same guarded supporting
facts. Thus the same states, facts, terms and fuel reach the sole mandatory P
check. Normal clause order, inherited guards and publication remain unchanged.

Unit controls reject movement across a private write or havoc that the support
reads, and accept independent support across pure private preparation. This
ordering argument does not promise monotone solver cost. Fresh compilation and
focused current-candidate evidence are required before crediting a product
improvement. No source proof hint, ceiling, expected verdict, background axiom,
global fuel policy, default-off branch or whole-suite iteration is changed.

The first current-product build attempt
([run 37805731745](https://github.com/erniecohen/dafny/actions/runs/37805731745))
recorded compilation errors at the existing method-call and yield helper uses.
The recording workflow itself succeeded, but its eight build/test stages failed;
only package retrieval and version generation succeeded. No verification result
is credited to that attempt. The compatibility repair preserves the original
optional preparation-guard position and makes the new postcondition support
parameter optional. Calls and yields therefore retain their preceding behavior.
Fresh compilation and focused proof validation are still required.

The compatibility-repaired product
([run 37807569735](https://github.com/erniecohen/dafny/actions/runs/37807569735))
compiled both platform bundles and contract probes, built the editor tests, and
passed 95 obligation unit tests. Its normal inventory assertion and complete
core-test invocation rejected an outdated reviewed source inventory. The
recording workflow's success did not override those failures. The captured
287 groups were audited: changes were confined to the modified helper file's
hash, the helper's parameter arity, and the exit call's invocation hash; no
producer or assertion count changed. The reviewed inventory is updated to those
source bytes. No proof verdict table is changed. A fresh normal inventory/core
check and focused native verification remain required.

The fresh [normal validation](https://github.com/erniecohen/dafny/actions/runs/37808373912)
for product `fe6ddd1ba` passes all ten actual stages: both platform bundles and
contract probes, editor compilation, 95 obligation tests in capture and normal
modes, 373 core tests, and the byte-exact reviewed 287-group inventory. Focused
native verification is running for the known targets and relevant controls at
the original ceilings; it is still pending. No whole-suite/library iteration
or development-line port has been launched.

## Current native product result

Product `fe6ddd1ba` completes a focused 68-observation gate using the accepted
normal build above. The [current target table](obligation-guarded-preparation-focused.md)
records both axiom settings and seeds 0, 1 and 7. Composite reproduces the native
diagnostic's VRV vector; Power and ExtensibleArray verify every selected seed.
FormArmy and RemoveFactor remain RRR. Iterator and AltPrimeDefinition retain
their preceding partly successful vectors and section-13 classification.

All sixteen positive controls verify, all four negative controls have genuine
Invalid VCs, and every reported independent specification-WF result is Correct.
Six newly executed default-off Power vectors match the recorded baseline exactly.
Earlier baseline and a709 results are reused explicitly. All available
seed-zero body assertion texts are unchanged from a709, including their
expressions and attributes after removing comments/dependency ids. This is a
textual check of actual formulas, not native AST serialization equivalence.

The durable gate completes its diagnostic denominator but rejects targeted
enabled acceptance because resource failures remain. Neither whole-suite nor
whole-library acceptance is claimed. This correction implements the justified
local support-order change; it does not fix all known regressions or make the
opt-in path uniformly better than legacy. No source hints, limits, solver
options, retained reveals or expected proof verdicts are changed.


## Native duplicate-support hypothesis

The current `fe6ddd1ba` preparation can finish with consecutive identical,
unattributed can-call assumptions. A scratch-only native diagnostic tests
omitting one duplicate at that fresh certified preparation boundary. The same
support command remains before the actual check, with every fact, triggering
subterm, heap and fuel layer intact. There is no body-term collection, new
proposition proof or product policy based on declaration names.

The diagnostic completes 54 observations for only Composite, FormArmy and
RemoveFactor, both axiom settings and seeds 0, 1 and 7. Eighteen unchanged
native controls reproduce the current outcome/resource vectors; all six
seed-zero complete input streams through their last proof query reproduce
canonically, including both FormArmy WF and body queries. All eighteen
false-entry controls contain genuine Invalid VCs, and all reported independent
specification-WF checks are Correct. Every actual native assertion and
branch-transfer object remains intact. Identical local support is explicitly
witnessed after each omitted duplicate.

| Target | Current native control | Duplicate omitted |
| --- | --- | --- |
| `Composite` | VRV | VRV |
| `FormArmy` | RRR | RRR |
| `RemoveFactor` | RRR | RRR |

Both axiom settings agree. Duplicate removal repairs none of these resource
outcomes; the successful Composite seeds cost more. This hypothesis is rejected
as a repair, and no corresponding product change is made. Stronger logical
availability of facts does not imply monotone solver resource use.

The first scratch attempt preserves a complete 36-observation Composite/FormArmy
subset but rejects the RemoveFactor scope: its conservative expression matcher
does not recognize an identical typed coercion around the empty-set expression.
The matcher is extended to compare that exact cast target and operand, using
the pinned Boogie operator equality. A fresh 18-observation follow-up executes
only RemoveFactor and its controls; the completed first subset is reused
explicitly. No Composite/FormArmy proofs are repeated. The original failed
outer gate and both immutable artifacts remain recorded; neither scratch build
establishes product acceptance.

The compiler-only scratch [first build](https://github.com/erniecohen/dafny/actions/runs/37815916741)
and [typed-coercion follow-up](https://github.com/erniecohen/dafny/actions/runs/37817411795)
each pass all six actual stages. Only the instrumented core and verification
frontend are assembled onto the exact accepted current native base; all other
binary files are byte-identical. These are diagnostic builds, not accepted
normal product builds. Original proof sources, ceilings, solver options and
reveal scopes are unchanged. No whole-suite/library gate or development port
is performed.
