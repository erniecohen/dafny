# Proposed Real SMT capture, diagnostic only

This is an English plan for review. No tracer, worker, solver, build or proof has
been run for this proposal. It does not authorize changes to the active Real
checkout or its current diagnostic input.

## Evidence and question

The origin-repaired diagnostic run `37232113837` has inner receipt SHA256
`dfa300517d75918a6d212f08a18996f7029fee9ef5c9d8178a0f26873c6e38cb`
and a fresh compiler identified by
`4.11.0+a4d3ec13a2929dd45f2f27770d458aa869f6667e`, the reused library SHA256
`9461fe3bb77dfabf981af767b586025e76bda574534f2829b59bf5c462955536`, worker
fingerprint `785fdda10ac925f7b14cc5556831843eb6d8a46f0f7b51054bbe84fc3f07d822`,
and exact Z3 digest
`b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23`.
The original conversion program hash is
`f4c160cb52184be2bb985e72c7979c22e4b97fb843d666d2a66e510b10d29abd`; irrational is
`a26457459fd322b3a72cd641cc22f48b389c3e660425ec9b35445edfe2f3a637`.
Their 200k/1M/10M requests have identical programs, obligations and configuration
apart from resourceLimit/requestId. Request/completion bindings and all four
serialized program hashes were checked independently. Eighty actual compiler
reference hashes match the captured identity inventory; observed Boogie references
match that inventory. These are immutable file/build observations, not signed origin
attestations or a full soundness theorem.

Both failing programs have no axioms or quantifiers. They do have ground heap
guard/anchor predicates and opaque frame-constructor assignments. The corresponding
pure irrational host control has none of that state. Thus quantified UF theory is
excluded for these packets; a ground-theory/incremental-context effect is a hypothesis.
The conversion's first check returned unknown/canceled; its next attempt got an
actual push-canceled command error. The irrational false goal returned unknown with
incomplete arithmetic, including both exploratory ceilings. No actual RU total or
SMT transcript is recorded, so canceled is not evidence distinguishing resource
exhaustion from timeout. Unknown is not Failed.

The original run `37230670642` token ledger was damaged by diagnostic printing.
The `setTokens:false` repair has now run: conversion origins are original Dafny
line 2, columns 23 and 3; irrational is line 4, column 3; universal origins are
lines 2, 3 and 6. All four program files were independently compared byte-for-byte
with the earlier run and are unchanged. Preserve the old receipt as historical
evidence, without accepting its source-origin ledger. The original universal frontend warning remains
an original failure even though both worker units verified. Current corpus is
44/46 strict and Java compilation passed.

## Immutable input boundary

Use only the origin-repaired diagnostic's frozen four original unit requests at
200000/20000ms, after root reviews that receipt. Bind each request's complete bytes,
programHash, unitId, ordered obligations, worker fingerprint and source fixture hash.
Do not edit even the requestId or solverExecutable in these original replays. Stage
the pinned package and solver at the request's existing absolute destination; if
that exact path cannot be established in a fresh dedicated CI job, stop incomplete.
There is no silent path substitution. Revalidate the package's complete file
inventory, source58/patch/three consumer pins, bootstrap560 receipt and solver digest.

A small separately reviewed replay host may read these exact JSON bytes and invoke
the existing WorkerProcessClient. It must forward the deserialized request unchanged,
require reserialization with the existing Protocol.JsonOptions to equal the complete
frozen request bytes before launch, then save the validated completion and lifecycle
evidence. The client adds its existing newline framing; this is not a request rewrite. It may not rebuild or
modify the verified B3 library, worker or solver, choose another solver executable,
enable logging flags in the worker, add solver commands, change seeds/options, or
replace the solver with a wrapper. Pin the replay host's source/build artifact too.

## Tracing route

Launch the disposable replay host under a resolved, hash-recorded strace from its
first exec; never attach late to an already running solver. Trace only descendants
of that launch, without external PID arguments, daemonization, seccomp filtering,
fault injection or syscall-limit detachment. Do not trace compiler/download stages
or credential environments. No sudo/cgroup delegation is needed; unavailable ptrace
permission or missing supported tracer is an incomplete diagnostic, not a fallback.

Candidate tracer shape, to be checked against the actual installed version before
implementation:

```
strace --kill-on-exit -I 1 -f -ttt -yy -xx -s 256 \
  -e trace=%process,read,readv,write,writev,pipe,pipe2,dup,dup2,dup3,fcntl,close,close_range \
  -e read=0 -e write=1 -o NEW_TRACE_FILE -- PINNED_DOTNET REPLAY_HOST --request FROZEN_REQUEST
```

The official strace manual describes following children, descriptor decoding and
full read/write hexadecimal dumps. Those dumps, rather than shortened syscall
strings, supply the Z3 fd0/fd1 bytes. Explicit interruptibility and exit-kill are
needed for bounded failure cleanup. The candidate deliberately uses only options
present in the reviewed v6.8 source; every syscall/dump must have an unambiguous
tracee PID association, including root records. Missing association fails capture. [strace project](https://strace.io/),
[manual](https://man7.org/linux/man-pages/man1/strace.1.html),
[v6.8 option implementation](https://github.com/strace/strace/blob/v6.8/src/strace.c).

Keep one trace file per original request rather than unbounded per-thread files.
Apply a 64MiB hard regular-file size cap to this diagnostic process scope and a
bounded trace-growth monitor; retain an explicit truncation/cap failure. Also cap
parsed syscall count, thread/process identities, reconstructed streams (1MiB each),
receipt size and total request count before allocation. No silently truncated trace
can qualify capture. These are instrumentation-only limits. They do not replace or
increase the existing worker's 20-second whole-request deadline or 200k RU ceiling.
The outer safety deadline permits only bounded cleanup after that worker deadline.
The regular-file cap is inherited diagnostic instrumentation and must be recorded
as such; it provides no equivalence claim about the original execution environment.
Tracing changes scheduling and adds overhead: instrumented outcomes and elapsed time
cannot establish original cost parity or repair original acceptance.

## Ownership, exec identity and FD direction

The coordinator must be an exclusive single-launch Linux subreaper with no initial
children. Capture the strace root through the actual Popen identity and pidfd.
Maintain an independently bounded owned-process ledger, with before/after parent and
start-time validation and pidfds; process-group membership or a printed numeric PID
alone is insufficient. Record threads separately by TID-to-TGID mapping, since a
thread identifier is not a process pidfd on the target kernel. Successful clone/fork
events and successful exec transitions must agree with the owned lineage. Reject
untraced clone flags, unknown PID namespaces, missed ownership, unexpected executables,
or reuse/mapping ambiguity as capture-incomplete. Do not classify an unknown PID
using a later same-number process.

The implementation binds the traced replay to the exact launch argv and permits
only its frozen worker exec plus worker-child solver `-version` and `-in -smt2`
execs. Each admitted process group has one successful exec epoch. Any later
successful exec, unexpected executable or successful `execveat` rejects qualified
capture. Unqualified same-TID observations also stop at successful `execveat`.

Identify every interactive Z3 exec by its successful exec transition, exact pathname
and exact argv [-in,-smt2], not comm/filename alone. The separate -version probe is a
different process and must not supply proof bytes. While each interactive solver is
alive, bind /proc executable image device/inode and image hash to its captured
start-time/pidfd, with identity checks surrounding the read. Compare to the sealed
solver image and recheck immutable staged package/solver files afterward. If this
live image binding is missed, preserve raw observations but do not claim qualified
exec-image identity. A successful exec pathname plus before/after file hashes alone
is weaker evidence under an explicit exclusive-immutability assumption, not atomic
kernel image attestation.

Use pipe inode annotations plus pipe/dup/close events to establish that Z3 fd0 is
the frozen worker's command pipe and fd1 is its response pipe. FD integers alone
can be recycled. For the actual solver process, reconstruct successful reads on
fd0 and writes on fd1 from full dump blocks and syscall return lengths. Handle
partial writes, readv/writev, unfinished/resumed calls, EOF and restart markers;
unrecognized forms fail capture. Zero/error returns contribute no delivered bytes;
never append an attempted full buffer after a partial return. Save delivered input
bytes and produced output bytes separately. Do not claim the worker consumed every
produced byte without matching read evidence. If supported syscalls or descriptor
transfers escape this reviewed mapping, mark the stream incomplete.

Strace 6.8 obtains full dumps by reading tracee memory after the syscall and emits
the adjacent completed-syscall/dump records. Treating those observations as the
runtime's bytes requires stable I/O buffers while strace reads them. The fixed
worker writes records serially, and its solver sends wait before subsequent sends;
this is an explicit source-runtime premise, not atomic kernel payload attestation.
An implementation that reuses or concurrently mutates these buffers would need a
different capture argument. The parser's positive-return slicing alone does not
prove buffer stability.

Parse SMT command/response framing only after byte reconstruction. Preserve raw
streams, exact order, EOF/exit markers and hashes. Match labeled source obligation
IDs against the frozen program and attempt sequence. Check that the original stream
really contains no quantified helper assumptions, and record the actual sequence of
push/assert/check-sat/reason/pop and the subsequent canceled push. Do not insert
statistics, reset, simplify, extra checks or recovery commands. Absence of a statistics
command leaves proof resources unavailable.

All normal exits require root reaping, exited owned pidfds, zero surviving/adopted
children and no coordinator cleanup signals. Record existing WorkerProcessClient
cleanup calls separately: its unconditional isolated-group cleanup remains byte-for-
byte unchanged, and tracing those calls does not establish that they signaled a live
member. The coordinator cannot suppress or alter client cleanup for this diagnostic. Failure/cancellation/cap overflow poisons capture
and drains only kernel-pinned owned processes within a fixed bound. Exit-kill is a
backstop for this tracer's own kernel-traced descendants, not an external PID policy;
do not assume killing the tracer alone accounts for every descendant. Any forced
cleanup or residual child remains failure, even if the JSON completion looked valid.

## Separate symbolic controls

Keep four extra requests in a separately hashed exploratory manifest, each in its
own fresh worker/solver scope, at the same 200k/20s/arith2/exact flags and pins:

1. Pure symbolic integer round trip: arbitrary Int i, the same saved Real assignment
   chain, integrality check and final floor(embed(i)) equality. Expected Verified for
   both original roles, not one folded or deleted check.
2. The same integer round trip with the exact ground opaque heap/frame scaffold from
   the original normalized module. Expected Verified for both roles.
3. Pure Real x with x*x=2 and a false check. Expected Failed.
4. The same full Real predicate/false check with that exact opaque scaffold.
   Expected Failed.

Construct the mixed scaffold from exact typed normalized records, not guessed symbol
names. The originals remain separate unchanged replays. Require exact attempt IDs,
coverage, outcomes, completed traversal and error absence for each control's intended
mathematical verdict. Unknown/ToolError remains an unmatched control. Record every
case even if an earlier one fails; trace qualification and mathematical matching
are distinct booleans. These controls are universal symbolic statements rather than
chosen input examples. For every integer i, embedding(i) is already integral, hence
floor(embedding(i))=i and embedding(floor(embedding(i)))=embedding(i). The nonlinear
false control is invalid over all mathematical reals: the real-closed carrier has
a square root of positive 2, so its premise has a model. No rational carrier or
floating approximation may replace that argument.

## Decision boundary after evidence

If pure and mixed cases differ, that identifies a concrete context-sensitive
operational boundary; it does not prove which solver mechanism caused it. If both
fail, investigate the exact primitive/incremental stream first. Canceled is not
permission to reset a session with a new full budget. Any future recovery must retain
all stable UF/SSA/source premises and learned facts, replay exactly, and share the
original deadline plus a defensible remaining resource ceiling. Without such
accounting, stop inconclusive rather than grant another 200k.

A future conversion repair could be a separately reviewed exact typed normal form:
floor(embedding(e))=e for Int e, together with capture-free substitution of genuine
fresh SSA defining equalities. The reverse embedding(floor(r))=r is not valid for
arbitrary Real r and must remain a checked integrality obligation. No current formula
or guard is rewritten in this diagnostic. A future nonlinear repair needs a sound
full-Real decision route or a checked algebraic model/certificate; neither unknown
classification nor a rational-only interpretation provides one. Solver configuration
changes or decomposition of independent theories need their own English model and
resource argument before implementation. This source checkpoint implements only the observation/replay route above. The
original strict corpus remains failed
until fresh uninstrumented acceptance passes its unchanged mathematical expectations.

## Source review anchors

The replay request serialization and complete-unit cancellation boundary are in
`Source/DafnyB3Protocol/WorkerProcessClient.cs` (`RunAsync`, lines 10-84).
The current frozen request/ledger writer is
`.github/review/real-triage/Program.cs` (typed emission lines 90 and 116; request
construction and dispatch lines 136-143). Its `Save` method uses the same
`Protocol.JsonOptions` as the client.

The unchanged native SMT preamble is in
`ThirdParty/B3/src/Solver/ExternalSolvers.dfy` lines 29-51.
`ThirdParty/B3/src/Solver/Solvers.dfy` `Prove`, lines 177-205, records check-sat
unknown plus reason as Inconclusive and then pops.
`ThirdParty/B3/src/Solver/RSolver.dfy` lines 408-422 and 441-499 surround
checks with context pushes/pops; `DeclareNewSymbols`, lines 501-598, follows
demanded symbols and their owned axioms. `SmtEngines.dfy` `SendCmd`, lines
84-119, latches command failure. Thus the later push-canceled response is a
concrete protocol error, not a second mathematical counterexample. The trace must
resolve exactly which commands/responses preceded it without changing that schedule.

The pure irrational symbolic host control is
`Source/DafnyB3Host.Test/HostIntegrationTests.cs`,
`AnIrrationalRealWitnessCannotMakeFalseVacuouslyTrue` (line 349). Its historical
1M/10s result is separate evidence; the new exploratory pure/mixed pair uses the
unchanged baseline 200k/20s and cannot borrow that older verdict.

## Implemented admission and artifact boundary

The source implementation adds a sealed outer `gate.py` wrapper. It validates
public run 37232113837 / head a4d3ec13, artifact 11314207981, 201508453 bytes,
SHA256 37233c026b7744d1c66d20169a81a698cb70ca21dbe68dbc6403d19a4d1013f3;
and public run 37221479537 / head ee32faed, artifact 11311016429, 145457636 bytes,
SHA256 76be6dadc245884a15c52b0b94d6eef8f16c96caed2d12ed23b3956c91e617fe.
Those are public artifact observations supplied independently and checked against
the captured original archive, not private executor observations. The wrapper
bounds member count, names, modes, uncompressed sizes and aggregate extraction,
captures a complete file inventory, and rechecks it after all scopes. Its token
environment is limited to metadata/download subprocesses. The isolated SDK builds
only the replay host referencing the captured protocol DLL. Original library and
worker files are copied as exact verified package bytes.

The parser supports reviewed strace6.8 only, rejecting other installed versions.
Full dump offsets and iovec markers come from upstream syscall.c/util.c; a write
buffer dump records attempted length and must be sliced by the actual return.
The worker stdin is independently reconstructed and required to equal the frozen
request plus existing newline. Its two stdout records bind actual worker PID,
request identity and validated completion. Original linear check IDs match the
exact attempt list. A complete command failure trace may contain fewer check-sat
queries than source checks: every unqueried remaining check must be explicit
ToolError. The session's first error response must be its final emitted command
response, as the pinned failure latch forbids further sends. This distinction
preserves goal completeness without inventing missing mathematical queries.

This first source admission is deliberately strict about unfinished worker or solver stdin
or stdout operations. A native cleanup kill may leave an unfinished read in the
trace; that yields capture-incomplete with preserved raw trace, rather than a
claimed complete stream. Any future narrower handling must justify the missing
return boundary before qualification. Trace/PID/FD/image qualification and strict
mathematical matching are separate booleans. Source-only validation is not a
claim that either has passed.

Live-image hashing can temporarily slow the bounded ownership scan. A short
version probe or other fast process may vanish before pidfd capture; each such
transient observation stays explicit, and a traced process lacking a live pin
blocks qualification. Completed read/write dumps may nevertheless be retained as
weaker, separately named observations scoped to a reported TID and successful exec
line. Such bytes are not joined across a subsequent exec/PID-reuse boundary and
other threads are not included. They assert no live-image, ownership, FD topology
or complete-process stream claim. Raw traces remain authoritative observations.
The request bytes, worker manifest, all invoked package files and utility seal are
rechecked after each replay; the seal is also checked around the SDK build.
