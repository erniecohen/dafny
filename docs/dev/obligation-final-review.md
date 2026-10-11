# PR 168 final review

The [final review request](https://github.com/erniecohen/dafny/pull/168#issuecomment-6094358780) permits proceeding toward merge as an **experimental, default-off** feature, conditional on final soundness and correctness checks. The eleven unchanged performance regressions are no longer, by themselves, a merge blocker. The earlier [strict acceptance report](obligation-followup-acceptance.md) remains a historical result under its original criteria; it does not claim acceptance under this later decision.

The requested final work is:

1. Independently justify contract-WF preparation assumptions and preserve heap, guard and scope semantics; document the argument.
2. Register the Schorr-Waite sequential-postcondition example permanently, with false and feasible controls.
3. Retain the seven diagnostic isolation successes and four explicit source repairs, including their proof-maintenance costs. Repair patches remain review artifacts; original benchmark sources stay unchanged.
4. Complete final CI and required correctness checks. Another whole-suite performance comparison is unnecessary unless the implementation changes materially.
5. Keep the option disabled by default. Do not add speculative fuel, trigger, batching or normalization changes.

## Correctness finding during final review

Final review found that replay of a labeled two-state callee contract changed the heap used for the enclosing caller's decreases expression. Clause expressions use the callee's labeled previous heap, whereas the caller's decreases measure must retain the caller's original previous heap. These contexts can differ even for a nonmodifying lemma.

A complete-module control uses a caller with previous field value 2 and current value 0, and a labeled callee whose measure is the labeled value plus 1. The actual call comparison is therefore `0 + 1 < 2`. A false predicate in that callee's contract has measure 0. Incorrect replay compared its measure with the caller's measure evaluated at the label, assuming `0 < 0` before checking the false precondition. All independent contract-WF declarations and a concrete caller-state witness pass; this is a correctness concern, separate from bounded proof-search failures.

The correction retains the caller context separately throughout replay, including function WF, nested lambdas and statement expressions. The callee/clause heap, ordinary checked termination obligations, guards, publication order and existing fuel remain unchanged. Permanent controls inspect the individual declaration outcomes so an unrelated expected error cannot conceal acceptance of the false precondition.

Independently established callee specification WF licenses the function/helper measure below the callee measure, evaluated in the clause heap. The existing checked caller-to-callee termination obligation establishes the callee measure below the caller measure, evaluated in its original caller context. Transitivity licenses the replayed comparison. Rebasing only the callee side preserves this derivation; rebasing both sides does not.

The independent source and generated-Boogie review found no remaining concrete
correctness defect in the corrected replay paths. A final 48-invocation matrix
covers baseline/OFF/ON, both resolver modes and both additional-axiom settings.
It inspects 312 declaration outcomes, including 168 independently successful
WF declarations. Both false-precondition callers and separate false controls
fail genuinely; both feasible callers pass. All 16 baseline/OFF declaration and
batch resource vectors agree. The nested positive explicitly establishes its
helper precondition in an earlier callee clause; its first version omitted this
independent license and was corrected before accepting the matrix.

The corrected native build passes all 396 core tests and all 118 obligation
tests, including the ordinary independent inventory gate. The reviewed inventory
still has 287 groups with unchanged counts and classifications. The previous
automatic CI regression failure was the nested positive fixture omission; its
suite verdict and library jobs passed. That failed run remains historical evidence.

The [final focused correctness receipt](obligation-final-correctness.json) closes
all six actual test phases: 396 core tests, 118 obligation tests, one independent
inventory capture, one ordinary inventory test, the actual registered issue100
LitTest, and the editor option-invalidation test. The registered helper executes
all 386 verifier invocations. Its 32 retained replay JSONs contain 208 declaration
outcomes, including 112 independently successful WF declarations and 32 genuine
expected Invalid correctness declarations. All eleven direct Boogie publication
controls also have their exact expected verified/error counts. No empty selection,
skipped test, safety timeout or exhausted proof is accepted.

The native package comes from the [refreshed build](https://github.com/erniecohen/dafny/actions/runs/38031272435).
The frozen gate accounts for exactly one test-input bridge: the committed nested
positive precondition correction, source `4e1ec113527d6ce54522ab6b7cfd6fbcde34dea9`.
Its original and replacement hashes are recorded; every other archived test
input remains unchanged. All test load paths use the exact corrected native
product, the explicit official .NET10 runtime, and Z3 5.1.0. This focused closure
supplies the corrected registered regression, inventory and editor results that
the earlier automatic CI run could not complete. That older failed CI run is
not relabeled green; this receipt does not claim a new whole-suite comparison.

**The final source/BPL review and required focused correctness gates are complete.
The PR is ready for human review toward an experimental, default-off merge.**
This is not a general mechanized soundness theorem or a uniform proof-cost
improvement claim. No original benchmark or expected verdict was changed; the
four proof patches remain diagnostic review artifacts. Changing the default or
porting PR169 remains a separate owner decision.

## Regression evidence and maintenance cost

The [methodological investigation](obligation-methodological-investigation.md) records seven successes under diagnostic assertion isolation. Isolation separates proof searches while retaining the original per-batch resource ceiling; more batches may increase total proof cost. It is evidence of context sensitivity, not a product-level performance correction.

The [four proof repairs](obligation-remainder-proofs.md) record explicit initial-graph witnesses, Base64 composition lemmas, concrete modular arithmetic steps and moving a read-only iterator computation before state writes. Each adds a local mathematical proof or preserves a useful heap context. Added helpers introduce their own independently checked obligations; the iterator reordering preserves its executable transition. The patches retain contracts, assertions and reveals, and do not restore redundant assertion-before-check idioms.

The empty-body Schorr-Waite regression is intentionally distinct from its portable repair: enabled sequential checking supplies an established concrete path fact to the later existential postcondition, while baseline requires the explicit witness. Its supporting definitions and WF declarations are checked separately, with a missing-edge negative and an inhabited original-heap control.

The linked investigation and repair reports include measured target batch counts, aggregate and maximum Z3 resource units, and completed-proof ratios for all seven isolation examples and four repaired targets. Exhausted results are censored, and unmeasured configurations are explicitly identified. These historical measurements retain their exact frozen compiler, Z3 5.1.0, resource ceilings and configuration. The four repaired-target costs were reproduced by the corrected-compiler focused recheck; the seven isolation costs remain historical. No original benchmark, expected verdict table or ceiling is changed to improve a reported count.
