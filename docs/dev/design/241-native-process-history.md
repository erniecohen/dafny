# Native solver history and task resource counts

The unrelated-module growth difference is caused by native solver process history.
It is not a translation-text difference. This is a causal reproduction of the
20,558 to 20,534 RU change, rather than an inference from coincident solver counts.
The separate lifecycle investigation is
https://github.com/erniecohen/dafny/issues/287.

## Fixed task and controlled histories

The declaration is Std.Arithmetic.MulInternalsNonlinear.LemmaMulIsAssociative
(correctness), native problem
Impl$$Std_mArithmetic_mMulInternalsNonlinear.__default.LemmaMulIsAssociative_split0.
Its exact 8,083-byte command sequence has SHA-256
ab5b290de0cf4027ec3845d3adcef8590098dcb200613a3a2e25f8b91ed0cb75.
The original one-function and 64-function unrelated-module captures have identical
Boogie and native task bytes. Both use three physical solvers, but the target
arrives after different histories: 1,660 tasks on solver 3 versus 1,918 on solver 2.

A new capture of one fixed 64-function source supplies both histories. The longer
history begins at Impl$$Std_mProducers.ConcatenatedProducer.Invoke_split277.
Cutting it at CheckWellFormed$$Std_mConsumers.IConsumer.Accept_split0 reconstructs
the shorter history: every problem name, task hash and task length in the
1,661-task suffix matches the original one-function physical stream. The target
bytes are unchanged. Direct native replay uses only Z3 -smt2 -in; it runs no
Dafny, Boogie, cache or task scheduler.

| Native history before the identical target | Fresh processes | Target RU, median [range] |
|---|---:|---:|
| 1,660 previous tasks | 5 | 20,558 [20,558, 20,558] |
| 1,918 previous tasks | 5 | 20,534 [20,534, 20,534] |
| None | 5 | 20,536 [20,536, 20,536] |

All 15 native invocations exit zero without SMT protocol errors and return unsat
for the target. Captured earlier
tasks emit existing pattern warnings; repeated stdout and stderr hashes agree
within each history. Fresh target-only replay emits no warnings. The
[machine-readable record](241-native-process-history.json) retains input/output
hashes, task counts, lengths and individual observations.

The logical reset boundary does not imply fresh native process state. Boogie's
100 ms idle handshake can retire a healthy process depending on timing, selecting
which prior tasks precede this target. Fixing the complete prior command history
reproduces the old resource counts exactly, even with one unchanged source. Thus
source growth and scheduling are not needed once that history is fixed. Task-local
options, prelude and assertion commands are included in the equal task hash.

## Secondary parameter diagnostic

Additional diagnostic get-option queries find smt.arith.nl true in a fresh process
and false after either historical prefix, surviving reset. The other twenty
queried option values agree, including seeds, timeout and resource limit. These
extra queries perturb solver state and are not part of the exact-byte cohort.
Explicitly restoring smt.arith.nl to true before the target still gives 20,558 and
20,534 after the two histories. The conditional false-only option emission in AddImplementationWithAttributes
[predates this stack](https://github.com/erniecohen/dafny/blob/fcb2042d6d043a2634f0854338c08feeaaaf4ae2/Source/DafnyCore/Verifier/BoogieGenerator.cs#L1460).
Parameter carry-over alone does not explain the measured difference;
no particular internal native cache or allocator mechanism is claimed.

## Qualification boundary

Task command equality proves input invariance, not isolation from a pooled native
process's prior state. The complete OFF/cold/warm cohort separately passes exact
task bytes, outcomes/RU, reports, stdout/stderr and exit, with physical counts
reported separately. The original growth observation has one classified RU
change and no verdict change. It must not be represented as pooled growth-RU
equality.

The diagnostic capture/replay tool is published under .github/measure/NativeSolverWire
in https://github.com/erniecohen/dafny/pull/262. Optional full-wire capture inserts
no solver commands. Replay can send either the exact target alone or its recorded
prior history, with five independent native processes. An explicit /restartProver
is used only as a fresh-process qualification control; this stack does not change
the ordinary solver lifecycle or enable that setting by default. The standalone
result cache already uses fresh solving for reusable tasks; this observation
explains why logical reset plus equal task text is insufficient for that policy.
