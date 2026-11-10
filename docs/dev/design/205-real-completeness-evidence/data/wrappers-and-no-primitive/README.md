# Wrapper and primitive-removal evidence

The four wrapper declarations verify under both solvers and both resolver modes. These are solver-reuse runs, with no `/restartProver` and no live profiling. Their totals are 55,141 RU on Z3 4.12.1 and 56,084 RU on Z3 5.1.0.

`real-completeness-no-primitive.dfy` replaces the marked primitive by an ordinary zero-returning lemma with no axiomatic attribute or postconditions. The complete run has two errors, both the checked LUB wrapper's postconditions. The remaining declarations can still use method contracts modularly; their successes do not make the failed program verify.

Exact flags, source hashes, declaration counts and outcomes are in `results.json`. Original text reports are retained alongside it. The source files are in `../../sources/acceptance/`.
