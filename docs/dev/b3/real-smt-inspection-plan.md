# Fixed artifact-inspection checkpoint and control inventory

Issue124 source-only child of6839c1c0d0964674b095dae0dd612654e47ef672.
The eight new methods below fix a projected45-control denominator. Preserve all37
existing method bodies byte-for-byte. None of these new controls has been executed.
The existing intentional non-verifier timeout control retains its explicit expected
failure/validated-cleanup label; parser controls must not be described as worker proof.

1. InterruptedTraceControls.test_restart_observation_has_identity_reason_and_no_result_or_bytes
2. InterruptedTraceControls.test_terminal_resume_preserves_initial_identity_and_no_result_or_bytes
3. InterruptedTraceControls.test_malformed_unknown_identity_or_attached_dump_fail_closed
4. InterruptedTraceControls.test_unresolved_selected_streams_still_reject_full_qualification
5. AttemptScheduleControls.test_frozen_eight_schedules_are_exact_and_choice_multiplicity_is_preserved
6. AttemptScheduleControls.test_missing_extra_reordered_wrong_sequence_or_breadcrumb_attempts_fail_closed
7. ArtifactInspectionControls.test_exact_archive_preflight_rejects_noncanonical_collisions_roots_modes_and_bounds_before_output
8. ArtifactInspectionControls.test_inspection_is_hash_bound_and_cannot_promote_unresolved_streams_or_old_coverage_flags

The new scratch-only real-smt-inspection mode reads exactly the existing
run37244861245 artifact and emits additional source-bound inspection evidence.
It performs no SDK, Replay, worker, solver, proof, normalization or new query stage.
Historical host coverage/math fields and receipt bytes remain unchanged. Full
selected-stream qualification still rejects unresolved stdin/stdout observations;
no stronger qualified prefix is part of this checkpoint. Unknown non-result
identity and malformed syntax fail closed. The current capture mode's request,
worker/runtime and solver behavior does not change. All ordinary workflow defaults
and job gates must remain unchanged.

## Reviewed English correspondence

This is an English review proposal, not implemented source or an authorization to
run another worker. The frozen source remains6839c1c0d0964674b095dae0dd612654e47ef672.
All original request/program bytes, four exploratory packets, worker/library,
solver, argv, environment,200k/20s configuration and runtime disposal stay fixed.
The prior diagnostic fields and failures remain immutable.

Actual evidence:37 controls passed. All20 operational stages passed with zero
supervisor signals/residual children; the intentional non-verifier timeout control
separately used three validated signals and retained a poisoned, empty scope.
All8 traces are nonempty, within64MiB, complete in the FIFO transport sense, with
EOF after owned exit/reaping and unchanged inherited FSIZE[-1,-1]. Complete
transport does not establish complete stream qualification: qualified captures and
weak observations are0/8 due the current parser grammar. Actual original worker
outcomes are unchanged. Conversion-pure also ends with canceled then push canceled;
irrational-pure returns Failed while original and mixed return Inconclusive.
No raw resource-unit measurement or original acceptance follows.

The first grammar rejection in universal0 is trace line2475, solver PID2894:
`<... read resumed>0x55fe12fd4750, 4096) = ? ERESTARTSYS (To be restarted if SA_RESTART is set)`.
The joined original record identifies stdin and its exact hex-annotated pipe.
Immediately before it, worker thread2884 calls `kill(2894, SIGSTOP)`. This is an
actual worker-side disposal observation; it is distinct from the outer ownership
supervisor's zero signals. The pinned native wrapper OSProcess.cs Close calls
Process.Kill(entireProcessTree:true) before disposing streams. Later terminal
resumed reads include `<unfinished ...>) = ?` (universal0 lines3537/3584) on
runtime fd64. Every case contains the same two kinds of non-result observation.
Their presence does not justify assigning a successful result or delivered bytes.

The source rejection is capture.py157-162: ordinary calls require an integer
result; only exit/exit_group currently admit `= ?`. Lines352-353 separately deny
unresolved identified worker/solver stdin/stdout I/O. Keep that latter rejection,
including non-success terminal records that were removed from the pending table
when a resume line was encountered. A grammar repair must not hide their identity.

First proposed checkpoint: represent completed integer-result calls separately
from exact interrupted/terminal non-result observations. Preserve PID/TID, original
begin/end lines, raw bounded argument text, FD token where identifiable, and exact
reason spelling. Restrict the first new grammar to observed read/readv/write/writev
ERESTARTSYS and observed terminal resumed non-result shapes; other unknown forms
remain rejected. Require an existing matching unfinished record before joining a
resume, matching syscall name, all existing nesting/line/call/PID bounds and no
orphan byte dump. Keep interrupted/terminal observations as explicit unresolved
stream evidence with no integer result and no bytes field. They must never enter
positive-return slicing, EOF, FD mutation, fork, exec, syscall-success or
command/response facts. A future completed restart cannot silently erase an earlier
unresolved stream observation under this conservative first slice.

The full qualifier must still reject every unresolved identified solver or worker
fd0/fd1 read/write observation, whether pending, interrupted or terminal. If a
non-result observation's FD/process identity cannot be established, fail closed.
Noncritical runtime fd64 observations can be retained without establishing a stream
fact; they do not excuse an unresolved selected fd. Exec epochs, pidfd/start-time
and parent checks, exact image SHA, pipe creation/owner/inheritance/dup tables,
request consumption, worker completion and all existing stream bounds remain.
Therefore this grammar checkpoint will likely expose the intended unfinished-I/O
rejection for these8 actual traces. It must not promise8 complete captures.

Existing weak observations may retain an explicitly observation-only prefix of
completed positive read0/write1 dump records within one exec epoch, stopping at
the first selected non-result/unresolved event and reporting that boundary. These
remain unqualified: no live-image/FD/ownership or complete-stream claim. The stable
serialized tracee-memory buffer premise still applies. Never label an incomplete
prefix a full capture, and never synthesize a zero-byte successful call.

A stronger selected-stream prefix would require a separate review. The possible
argument would cover only bytes reconstructed from completed positive syscalls
before the first selected ambiguity, under the same live-image, original FD/owner
and immutable-buffer premises. It could establish that particular observed
check-sat/response frames appeared before that boundary; it could not establish
EOF, future absence, full request/response coverage or original acceptance. Do not
implement this stronger prefix qualifier in the first grammar checkpoint.

Second proposed checkpoint: correct only postprocessing attempt-schedule evidence,
leaving Program.cs and every packet untouched. Program.cs31 currently compares a
sorted dynamic attempt multiset to a sorted static obligation inventory. For
universal1 the static inventory has one sOassert1, while the native worker correctly
executes it twice. The entire current completion equals the frozen372321 original
completion object (including sequence numbers, IDs, descriptions, breadcrumbs,
results and fingerprints). Original completion file SHA is
b1c0cab2b13bbc6e43bcaa04db295bd2d5e51f26b549ab3e78860beba7a05293.
The old current replay receipt has exactCheckCoverage=false and
mathematicalMatched=false; retain those historical fields rather than overwrite.

The source schedule is independently justified. Frozen universal1 IR contains a
labeled Conditional(true,then,else) before the single checked quantifier.
RawAstBuilder.cs54 translates it to RawAst.If. Pinned StmtResolver.dfy191-197
prepends cond/negated-cond assumptions and creates Choose([branch0,branch1]).
Verifier.dfy302-309 records `choose alternative i` and processes each branch with
the continuation. The source assertion in that continuation therefore has two
attempts, including the false-assumption branch. Do not substitute a deduplicated
ID set, discard that branch or infer that only one attempt is legitimate.

Use an explicit immutable per-case expected ordered schedule, bound to exact
request/program hashes and reviewed original IR, with each row (sequence,ID,
breadcrumbs). Expected schedules are:

* universal0: (0,sOassert1,[]), (1,sOassert2,[]).
* universal1: (0,sOassert1,[choose alternative 0]),
  (1,sOassert1,[choose alternative 1]).
* original/pure/mixed conversion: (0,sOassert1,[]), (1,sOassert2,[]).
* original/pure/mixed irrational: (0,sOassert1,[]).

Bind the original schedules to the frozen372321 completion/IR; bind exploratory
schedules to the unchanged generated arithmetic statements and their exact packet
hashes. This is a fixture-specific schedule, not a general assertion that each
static goal executes once or that unreachable checks must execute. Require exact
order, multiplicity, sequence and breadcrumbs; every static goal remains present.
Extra, missing, repeated beyond expectation, reordered or wrong-breadcrumb attempts
fail. Record fresh fields such as reviewedAttemptScheduleMatched and
mathematicalOutcomeAndScheduleMatched, separate from the old host fields.
Mathematical matching still requires traversal completed, no worker error, exact
schedule and the original Verified/Failed result/attempt rules. Unknown and ToolError
remain mismatches. No case expectation is waived.

If/when a separately reviewed physical stream qualifier can apply, label queries
by the full expected attempt row rather than unique static ID. Match the ordered
check-sat response sequence to that exact attempt prefix. An original unqueried
remaining attempt is acceptable evidence only as the unchanged explicit ToolError
suffix following the captured fatal command response, never as a missing success.
The conversion push-canceled fatal response must remain ordered and explicit.
This changes evidence association, not worker behavior or check goals.

A suitable first execution after source approval is a bounded read-only artifact
inspection mode: fetch and validate only immutable run37244861245 artifact11319066007,
710066155 bytes, SHA858af57479406a051568c82f5b322a99b21d6860923d34521005d518f4e98714;
verify source/receipt/request/package/trace hashes, then parse its8 frozen traces
and postprocess its8 existing completions. It runs no SDK, Replay, worker, solver,
proof, new query or normalization. Preserve the prior actual receipts and attach
new source-bound inspection fields. Parser controls remain separate from worker
execution; any fixed subprocess control retains its explicit non-verifier label.

Suggested focused controls, to be fixed and counted before implementation, preserve
all37 existing method bodies: interrupted observation retains identity/no result/no
bytes; terminal observation does likewise; selected unresolved I/O still rejects
full qualification; unknown/malformed non-result and attached dump fail; exact
universal1 two-branch schedule passes while all order/multiplicity/breadcrumb
mutations fail; fatal conversion suffix stays explicit and no Unknown becomes
Failed. The fixed inventory above supersedes the suggested grouping; no execution or dispatch is authorized by this source checkpoint.
