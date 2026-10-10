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

**Final soundness signoff and final-build correctness validation are pending. Keep the PR draft until those conditions close.**

## Regression evidence and maintenance cost

The [methodological investigation](obligation-methodological-investigation.md) records seven successes under diagnostic assertion isolation. Isolation separates proof searches while retaining the original per-batch resource ceiling; more batches may increase total proof cost. It is evidence of context sensitivity, not a product-level performance correction.

The [four proof repairs](obligation-remainder-proofs.md) record explicit initial-graph witnesses, Base64 composition lemmas, concrete modular arithmetic steps and moving a read-only iterator computation before state writes. Each adds a local mathematical proof or preserves a useful heap context. Added helpers introduce their own independently checked obligations; the iterator reordering preserves its executable transition. The patches retain contracts, assertions and reveals, and do not restore redundant assertion-before-check idioms.

The empty-body Schorr-Waite regression is intentionally distinct from its portable repair: enabled sequential checking supplies an established concrete path fact to the later existential postcondition, while baseline requires the explicit witness. Its supporting definitions and WF declarations are checked separately, with a missing-edge negative and an inhabited original-heap control.

The linked investigation and repair reports include measured target batch counts, aggregate and maximum Z3 resource units, and completed-proof ratios for all seven isolation examples and four repaired targets. Exhausted results are censored, and unmeasured configurations are explicitly identified. These historical measurements retain their exact frozen compiler, Z3 5.1.0, resource ceilings and configuration. They do not claim corrected-compiler performance. No original benchmark, expected verdict table or ceiling is changed to improve a reported count.
