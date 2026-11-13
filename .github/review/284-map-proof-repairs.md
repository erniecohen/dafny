# Canonical map payloads: proof and baseline repairs

## Stack on the character repair

This change is stacked on [the character repair](https://github.com/erniecohen/dafny/pull/290)
at `a220fed05038ff4c70ac325d29b7c0a5238ba0e7`, which includes the shipped
`587201ff` integration. Both character and map repairs remain enabled by default,
and each retains its independent hidden legacy comparator. The one-padding
Base64 prefix witness from that parent and the two-padding witness and direct
quotient proof from this change are all retained.

The map byte-identity check uses that stacked parent as its pre-map product.
This keeps the character representation and the other integrated changes equal
on both sides of the comparison. The review and obligation workflows also
accept pull requests targeting the character branch.

The failures in the [obligation run](https://github.com/erniecohen/dafny/actions/runs/43405321878)
and the [review run](https://github.com/erniecohen/dafny/actions/runs/43405321828)
are addressed with checked source proofs and an expected-statistics update.
The canonical-payload repair stays on by default, with its hidden
`--legacy-noncanonical-map-payloads` comparator. No encoding fact or resource
limit changes in this follow-up.

## Combined-tree producer proofs

Stacking the repairs changes the matching context of three producer proofs.
With additional axioms enabled, the canonical map row exhausts the resource
limit in `LimitedProducer.Invoke` and `ConcatenatedProducer.Invoke`; the same
combined sources verify with legacy map payloads. A separate compatibility
run reports a Z3 vector-expansion overflow in `FlattenedProducer.Invoke`;
the same unrepaired source verifies with legacy map payloads.
These are failures to establish valid state-preservation properties, not
examples of false source theorems.

`LimitedProducer` now splits at its existing checked `Valid()` assertion.
`ConcatenatedProducer` proves the one-result history extension explicitly and
uses the two component remaining counts: a produced result consumes one
remaining element; an absent result establishes that none remain.
`FlattenedProducer` checks and splits at `Valid()` after advancing the current
inner producer and rebuilding `Repr`. The following metric argument then uses
that checked representation fact. No contract, loop invariant, resource cap,
or encoding axiom is strengthened by these proof repairs.

The combined tree reruns the affected upstream harness and obligation parts
1, 2, 5 and 6. The two harness expectation updates contain only resource
statistics; the verified counts and expected negative diagnostics are retained.
The inventory capture matches all 290 reviewed producer groups byte for byte,
and its ordinary non-capture test passes.

## Obligation parts 1, 2, 5 and 6

All four failures select `M2.SchorrWaite` with consistent obligation checking
enabled. The selected batch exhausts at 16,038,332 RU; the complete-file cases
report that same failed batch. The new proof treats the newly marked node and
the previously marked nodes separately. The new node receives the extended
initial-heap path; every other node retains its earlier witness. This is a
checked preservation argument for the existing invariant. It supplies no
assumption about reachability.

## Upstream harness

- `SubsetTypes.dfy` retains 13 verified declarations and all 91 expected errors.
  Only its printed statistics move: total RU rounded to hundreds changes from
  765,400 to 820,400, and maximum batch RU from 150,000 to 153,900. Its expectation
  records the review run's output; stacking the character repair rerecords
  only these resource statistics, preserving the same verdict and diagnostics.
- The issue-82 nested-key map test reindexes its source witness from `i` to
  `value = n + i`. The range becomes `n <= value < n + 2`, and the stored value
  becomes `value - n`. This bijective change retains the two entries and all
  domain/value assertions while exposing the witness directly as the argument
  of the nested key function. It avoids searching for a witness through `n+i`.
- The issue-82 changed-heap test separates its reads-footprint assertions.
  Every original assertion remains checked under the existing per-batch cap;
  no footprint or allocation premise is assumed.

These are proof-search repairs for valid path, map and footprint properties.
They do not identify a false source theorem whose only justification was the
inconsistent map row. In particular, a failed search or resource exhaustion
alone would not establish that a source property is false.

## Standard library and comparison report

Both failures concern `Std.Arithmetic.DivMod.LemmaMultiplyDivideLe`. In the
review run, canonical payloads verify its correctness batch at 552,293 RU with
additional axioms off and 552,313 with them on. Legacy payloads exhaust at
1,031,153 and 1,031,157 RU respectively. Each lane therefore differs in exactly
the declaration row and the aggregate `run Std` row.

The arithmetic statement is true: monotonicity gives
`a / b <= (c * b) / b`, and cancellation gives `(c * b) / b == c` for positive
`b`. The replacement body calls those existing lemmas directly instead of
introducing a fresh higher-order induction predicate. Both payload modes can
use the same proof. The two Z3 5.1.0 baseline updates record `Correct` and the
corresponding aggregate count, retaining all other errors and exhausted proofs.
The canonical-map comparison continues to reject every verdict or declaration
outcome difference; no exception or blanket acceptance is added.

The comparison and obligation workflows retain the individual proof results,
batch counts and RU measurements for the repaired sources. A run with existing
documented errors or exhausted proofs is a verdict comparison, not a claim that
every standard-library or staged-example proof succeeds.

## Stacked baseline record

Both complete compatibility-solver profiles verify all 2,195 declarations. Their
`run Std` aggregate is rerecorded for the combined proof partitions; all
compatibility declaration verdicts remain unchanged. The checked source changes
add 243 batches relative to the character parent, including 188 from the three
new producer repairs.

The Z3 5.1.0 expectations start from the character parent's
[Linux Std artifacts](https://github.com/erniecohen/dafny/actions/runs/43427089589).
The division, bulk-reader and JSON-object proofs become `Correct` in both
additional-axiom profiles. The explicit remaining-count proof also makes
`ConcatenatedProducer.Invoke` correct with additional axioms off. These are the
only declaration changes relative to those artifacts. Existing errors and all
other exhausted declarations are retained; no declaration is added or removed.
The changed declaration outcomes and combined Linux aggregates are inferred
from those artifacts, the source proof repairs and the complete combined-tree
comparisons, pending this PR's Linux CI. They are not a claim of a combined
Linux execution.

The paired complete Z3 5.1.0 runs compare every one of the 2,202 rows in both
additional-axiom profiles. The strict CI comparison publishes individual proof
outcomes and RU movements for the combined sources. The compatibility and
primary results are verification evidence for their stated scopes, not a claim
that the recorded pre-existing errors or resource-bounded failures verify.
