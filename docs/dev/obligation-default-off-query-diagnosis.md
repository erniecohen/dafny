# Default-off solver query ordering diagnosis

**Current evidence:** eight focused native `MembersSpec` comparisons use source
`df4c7066f`, accepted compiler source `63fb750a9`, the original supported baseline
`ab210b78b` and Z3 5.1.0. The complete targeted gate finishes; all eight WF checks
verify. Exact default-off resource equality remains rejected. This is not a
whole-library acceptance result. The earlier evidence below is historical.

This is limited causal evidence for the `MembersSpec` standard-library sample,
not acceptance of the complete strict resource comparison. It uses the historical
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


## Current compiler repeats preserve Boogie, not exact cost

The fresh scope is baseline/default-off, both additional-axiom settings, with
two repeats of each. Sources, project configuration, normalization, solver,
platform, cores and original ceiling are fixed. The candidate uses the
[accepted structural compiler](https://github.com/erniecohen/dafny/actions/runs/37955030278).
No prior proof results are reused in these eight observations.

Every emitted Boogie module is byte-identical between baseline and current
within its axiom setting, including both repeats. Resource counts differ in
all four baseline/current paired comparisons; even the two unchanged baseline
runs differ in the axiom-off setting. Every observation verifies, and the scope
contains no negative controls. A successful diagnostic wrapper is not acceptance
of strict resource equality.

A fresh read-only query analysis establishes an explicit bijection over generated
symbols, including all binder occurrences. After that rename, all eight full
dynamic suffixes match and the static command multisets match. Removing only
the declaration of one integer constant and the two ground literal equalities
for 4294967295 and 4294967296 leaves the entire command sequence identical.
Thus the observed input differences are confined to those command positions
and their resulting generated names. No checked formula, fact, trigger or fuel
term changes. This extends the historical structural diagnosis to the current
compiler; the earlier direct-solver replay remains historical and is not newly
executed here. No full-library equality waiver follows. The current controlled replay below
was subsequently completed; keep the earlier replay as separate historical evidence.


## Current exact-query replay confirms the sampled ordering cause

A fresh sixteen-observation direct Z3 5.1.0 replay uses all eight complete solver
streams from the current compiler comparison above. Each raw stream verifies and
reproduces its own native resource count exactly. Thus the baseline repeat
variation and every sampled baseline/current cost difference are reproduced by
the captured inputs themselves.

For each stream, an explicit bijective generated-symbol rename preserves all
uses and lexical binder occurrences. The entire dynamic query suffix is exact,
and the static command multiset is exact. Restoring one integer declaration's
position and the order of the two existing ground literal equalities makes all
eight diagnostic inputs identical. All eight normalized replays verify with
exactly the same resource count. No fact, checked formula, trigger, fuel layer,
solver option or resource ceiling is changed.

This extends the bounded causal result to the current accepted structural
compiler, both additional-axiom settings and both repeats. Generated Boogie is
byte-identical within each setting; the sampled proof-cost variation comes from
solver-input ordering and generated names. It does not establish what internal
allocation or collection behavior selected those orders, nor does it explain
unsampled library cost movements.

The normalized queries are diagnostic copies, not a product translation change,
new native compiler gate or complete-library cost waiver. This replay contains
no new negative controls; the preceding eight native observations supply their
reported independent WF results. Strict default-off library resource acceptance
remains open, including the distinction between unchanged generated Boogie and
exact native query bytes. No broader verification suite is launched.
