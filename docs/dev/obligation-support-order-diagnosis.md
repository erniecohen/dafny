# Native assertion-local support ordering

A completed 36-observation native diagnostic uses the current `a709df226`
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

## General local correction, pending compilation and native validation

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
