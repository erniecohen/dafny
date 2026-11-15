# Enabled additional-axiom verdict baseline

The initial ON snapshots are captured from [the baseline probe](https://github.com/erniecohen/dafny/actions/runs/37137355281)
at `b227b40c`, whose product tree is unchanged from `d23ba465`. No OFF expected
file, proof source, axiom, solver option or resource limit is changed here.
The run used the checksum-pinned solvers already selected by `review.yml`:
Z3 5.1.0 for the suite and Z3 4.12.1/5.1.0 for the source standard library.

The probe completed every verifier execution and the ordinary regression and
resolver gates. Its two shard-1 resource-inventory steps failed because an
existing invalid CSV-logger test leaves an empty JSON log. This reporting error
is repaired by recording unavailable measurements explicitly. Those two
completed verdict tables were recovered from their job logs; the other six
are in their verdict artifacts. A subsequent ordinary strict run must match
all snapshots before acceptance. The probe's overall failed status is not a
claim of green CI.

## Suite

All 1,081 ON program verdicts equal the existing OFF expected file. All 1,081
fresh OFF verdicts also match. There are 7,351 logged proof batches per mode.

The historical #74 comparison measured 7,499 batches for the same 1,081 planned
programs because it removed bare output options left after expanding RUN-line
placeholders. Standing CI retains its existing command/diagnostic behavior.
Thus `Calculations`, `LetExpr`, `SmallTests`, `git-issue-2266` and `git-issue-5873`
retain their CLI-error verdicts instead of supplying those additional 148
batches. This change does not silently repair or enlarge that existing plan.
The separate regression harness continues to enforce the #33/#74 positive,
negative/vacuity and local-identity isolation tests in both resolver modes.

## Standard library

Each solver snapshot contains 2,190 declaration rows and seven part-summary
rows. Relative to the unchanged current OFF snapshots, the intended ON
improvements are:

| Solver | Declaration | OFF | ON |
|---|---|---|---|
| 4.12.1 | `DivMod.LemmaMultiplyDivideLt` correctness | Errors | Correct |
| 5.1.0 | `Power2.Lemma2To64` correctness | OutOfResource | Correct |
| 5.1.0 | `Base64.EncodeBVIsBase64` correctness | OutOfResource | Correct |

Each `run Std` row changes only to reflect those improvements: 4.12.1 has
7,612 verified and zero errors; 5.1.0 has 7,586 verified, two errors and 24
out-of-resource batches. Other part summaries and declaration outcomes are
unchanged. Existing failed proofs remain expected failures; ON does not mean
that the entire library proves under every solver.

These improvements agree with the retained [accepted #74 comparison](https://github.com/erniecohen/dafny/actions/runs/37104365416).
The older 5.1.0 ON snapshot additionally exhausted
`DivMod.LemmaIndistinguishableQuotients`; the already-merged proof repair
`5faaf26c2203d13a9aad37b0737112a2621f53d6` now proves it with both modes and
shifts later DivMod diagnostic lines by one. The current OFF snapshot already
records that repair. The fresh 4.12.1 ON outcomes match the historical ON
outcomes exactly. No new unexplained verdict is adopted.

Library resource counts retain the known default-order variation in #30.
The standing resource report records every paired observation but imposes no
cost threshold and makes no exact controlled-equality claim for library runs.
The accepted `MinimumWindowMax` cost outliers remain tracked in #78.
