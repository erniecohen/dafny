# Canonical map payloads: proof and baseline repairs

The failures in the [obligation run](https://github.com/erniecohen/dafny/actions/runs/43405321878)
and the [review run](https://github.com/erniecohen/dafny/actions/runs/43405321828)
are addressed with checked source proofs and an expected-statistics update.
The canonical-payload repair stays on by default, with its hidden
`--legacy-noncanonical-map-payloads` comparator. No encoding fact or resource
limit changes in this follow-up.

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
  is updated to the review run's output.
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
