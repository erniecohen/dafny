# Default-off solver query ordering diagnosis

**Latest evidence:** product `fe6ddd1ba` has [68 focused suite/control observations](obligation-guarded-preparation-focused.md)
and [92 completed known-library/control observations](obligation-current-focused-library.md).
The library result explicitly combines 84 preserved observations from a harness-failed
attempt and eight controls from its completed follow-up. Remaining resource failures
and strict default-off cost acceptance stay open. No whole-suite/library run was
launched for this revision. Complete-gate results below belong to earlier pinned
revisions and cannot establish current acceptance.

This is limited causal evidence for the `MembersSpec` standard-library sample,
not acceptance of the complete strict resource comparison. It uses the current
compiled product `072175269`, build source `8d23f3fc8`, baseline `ab210b78b`
and Z3 5.1.0. Neither product translation nor project gates were changed for it.

## Native observation

Two runs of the same baseline binary and two runs of the same candidate binary
with obligation preservation disabled all verify on the same executor. The
entire emitted Boogie module is byte-identical in all four runs. Their resource
counts differ, including between the two unchanged baseline runs.

Actual `--solver-log` files are present in this diagnostic. Their streams are
not byte-identical. Two ground literal equalities involving 4294967295 and
4294967296 occur at different positions in the static background. In one
stream an integer constant declaration appears earlier, correspondingly changing
generated symbol names.

An explicit bijective rename preserves every symbol's declaration sort and every
use, including binder occurrences. After that rename, the complete dynamic
suffix from `push` through the proof query is identical. The static prefix
contains exactly the same command multiset. Restoring the one declaration's
position and placing only the two literal equalities at a fixed valid position
makes all four entire diagnostic streams byte-identical. No fact, trigger,
checked formula, fuel layer, option or resource ceiling is added or removed.

## Controlled replay

A complete 24-observation direct solver replay checks each original stream three
times and each corresponding reordered stream three times. Each original stream
reproduces its own native resource count exactly on every repeat. All reordered
streams verify and have the same resource count. This isolates the sampled cost
movement to the emitted stream's ordering and generated names; it is not a
resource counter randomly changing for an identical captured stream.

The reordered streams are diagnostic inputs only. Their success does not count
as a native project gate, does not establish exact native query identity, and
does not relax the fork's strict default-off resource requirement. This sample
also cannot explain every movement in the complete library. The initial replay
attempt placed the literal equalities before their declarations and was rejected
as invalid input; the corrected fresh complete replay requires error-free solver
execution and preserves that rejected attempt separately.

## Fresh additional-axiom observation

The completed cost matrix supplies another same-executor MembersSpec pair with
additional axioms enabled. Its baseline/default-off Boogie is again
byte-identical while resource counts differ. A fresh read-only analysis finds an
explicit bijective generated-symbol rename after which the entire dynamic
suffix and static-prefix command multiset are identical. The non-literal
background assertion sequence is also identical. Moving a literal declaration
changes declaration positions and generated names; the same two ground literal
equalities appear at different static positions.

This extends the structural ordering observation to the other axiom setting.
It is not another direct solver replay or full-library acceptance. The complete
24-observation replay above is the executed causal evidence; neither analysis
asserts equality of the native query bytes or a waiver of strict resource equality.
