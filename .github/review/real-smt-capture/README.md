# Real SMT capture diagnostic

This opt-in review utility replays the four immutable schema-2 packets captured
by public run 37232113837, then four separately labeled symbolic controls. It
reuses the exact previously verified 560-obligation library from run 37221479537;
it never builds the library or changes a solver command, seed or budget.

The original conversion expectations remain Verified for both the integer-cast
integrality check and the final round trip. The original irrational false goal
must be Failed over the complete Real carrier. Unknown and ToolError remain
strict mismatches. The original universal frontend warning is historical failed
CLI evidence; its two worker packets are still replayed unchanged. This utility
has no full-acceptance PASS result and zero exit only delivers diagnostic evidence.

In a fresh dedicated Linux x64 CI job with .NET8 and reviewed strace6.8 installed:

```
python3 -B .github/review/real-smt-capture/gate.py NEW_OUTPUT_DIRECTORY
```

The outer wrapper binds both public run/head/artifact metadata and exact ZIP
bytes before bounded extraction. It then preflights every archive member before
creating an extraction directory. The original archive admits exactly 677 members
under `out/b3-native-compile` and 335 under `Binaries/net8.0`; the prerequisite
admits exactly 1186, 344 and 9 under `out/b3-native-compile`, `Binaries/net8.0`
and `build/b3-host-tests`, respectively. Unknown roots, canonical duplicate paths,
file-as-ancestor collisions, unsafe names/modes, symlinks, encryption and all
inventory/path/file/aggregate bounds fail before extraction. Every extracted file
is hashed and inventoried; the additional roots are data only and are not loaded. GH_TOKEN/GITHUB_TOKEN are available only to gh
metadata/download stages. Their values never enter SDK, parser or trace scopes.
The inner coordinator can also be invoked with the hash-qualified extracted inputs:

```
python3 -B .github/review/real-smt-capture/coordinator.py \
  --captured-real-artifacts "$ORIGINAL_REAL_CAPTURE" \
  --prerequisite "$ORIGINAL_B3_PREREQUISITE" \
  --dotnet "$RESOLVED_DOTNET" --strace "$RESOLVED_STRACE" \
  --output "$NEW_OUTPUT"
```

All paths must be absolute. The frozen solver path in each request is preserved;
staging requires that exact public CI destination to be absent or already contain
the pinned bytes, with no symlink directory traversal. A different environment
that cannot establish the original path is incomplete, without path substitution.

The small replay host references the captured protocol assembly and does not load
Dafny/Boogie or normalize again. Original request serialization is byte-identical
and receives only the client's existing newline. Mixed controls change unit and
request identity only. Pure controls remove exactly the sealed packet's opaque
heap/frame and unused Boolean scaffold while retaining arithmetic assignments,
requires, both conversion checks and Learn flags. Full source-check coverage and
fixed eight-case denominator remain explicit; no generated source goal is discarded.

strace follows only the launched host and descendants. The coordinator records
validated pidfd/start-time process ownership, successful execs, a live solver image hash
and exact replay launch argv. Later successful execs and successful `execveat`
reject qualification. Strace reads dump bytes from tracee memory after syscalls;
the fixed runtime's serialized I/O buffers must stay stable through those reads.
This diagnostic does not claim atomic kernel payload attestation. It also checks
pipe creation and descriptor duplication. Full hex dumps are reconstructed
using successful return lengths, preserving partial and vector I/O. The traced
worker must consume the exact request plus newline; its startup PID and completion
must match the validated replay receipt. The solver's command/response pipes must
be distinct and created by that worker. Complete SMT framing is kept byte-exact.
A failed push can prevent a later check-sat: remaining original attempts must then
be explicit ToolError, without interpreting a missing query as a proof or countermodel.

A missed fast exec image, PID reuse, unsupported namespace/trace escape, incomplete
fork/FD topology, dump gap, truncated trace or unfinished worker/solver stream I/O makes
capture incomplete. Raw traces and immutable-path observations remain available as
weaker evidence; they do not qualify a live executable image. Completed I/O from
one reported TID between its successful exec line and a later exec/PID-reuse
boundary is also retained in explicitly unqualified observation files. Other
threads are not merged into those bytes, and no FD/process completeness is claimed. No reset, extra
query/statistics request, solver wrapper, logging option or rational-only carrier
is introduced. Original proof resource totals remain unavailable.

Every scope uses bounded owned-process cleanup. Only validated pidfds are signaled
by these coordinators; any required signal poisons its stage. Existing worker/client
cleanup code is preserved, and its group-kill calls are distinct observations. The
tracer's exit-kill option is an additional owned-tracee backstop. Parser cap overflow
or cleanup faults cannot establish capture completeness. The 64MiB retained-trace cap and
tracing overhead are diagnostic changes; observed outcomes and wall times cannot
establish original execution or cost parity.

Twenty-one source parser regressions cover partial writes, failed writes, vector
buffers, resumed I/O, dump gaps/missing bytes, orphan dumps, missing PID association,
truncation, SMT framing, absent live image, wrong fork parent, PID reuse and exact
executable epochs, including unsupported `execveat` boundaries. They do
not invoke a solver or worker. Six archive admission controls additionally cover
both pinned layouts, late unknown roots with no extraction output, unsafe paths and
modes, canonical duplicates, file ancestry and all admission bounds. The outer
wrapper now requires a fixed combined 37-control denominator. The six new sink
methods check fragmented order, exact cap, overflow, empty/non-EOF/deadline states,
read/write/identity faults and one fixed non-verifier Python writer's owned timeout.
That last negative control requires its stage to remain poisoned, its pidfd cleanup
to be recorded, and no residual children. The other five invoke no child process.
The new six controls have not been executed; historical run37242135132 passed the
previous27, then stopped before any worker/solver launched.

`DESIGN.md` gives the English mathematical and observational argument. The source
seal declares all 20 utility/input files; the enclosing reviewed Git/CI receipt
binds that declaration. It is no signed build or runtime attestation. Workflow
routing is enabled by the enclosing scratch-only review workflow. The first
execution, run 37240724688, stopped at the original archive's legitimate
`Binaries/net8.0` root: the former validator admitted only `out/b3-native-compile`.
It ran zero parser controls, workers or queries. Its immutable failure receipt
remains historical evidence; this layout repair requires a new reviewed execution.


The new sink uses one fresh mode0600 FIFO for strace's output and one retained
regular trace, with no extra executable or inherited descriptor. A bounded pump
reads at most256KiB per call in the same coordinator thread. It retains at most
64MiB and reads at most one extra sentinel byte; overflow and I/O faults poison the
owned stage and stop later replays. A prefix hash/length is distinct from a complete
trace hash. Natural completeness requires nonempty exact bytes, EOF after every
owned identity exits/is reaped, a matching final file hash and completion inside the
existing40s stage deadline. Backpressure can change instrumented timing; no cost or
acceptance parity follows. No resource limit is set: inherited FSIZE is recorded
before/after, and a mismatch fails admission rather than changing runtime options.
The pinned tracer closes its shared log before replay exec. Incomplete prefixes
never enter complete FD/I/O analysis.


The subsequent parser checkpoint recognizes the actual strace6.8 all-hexadecimal
FD annotation labels observed in run37242135132. Each label is decoded only when
all bytes use exact lowercase `\xHH`; mixed/truncated/suffixed tokens fail. A
complete decoded pipe label must be `pipe:[positive-decimal-inode]`, bounded to a
64-bit inode and signed32-bit FD. A pipe creation has exactly two full, distinct
FD tokens with the same inode and representation. All existing pipe-owner,
inheritance, duplication, exec/image and worker/solver stream guards remain.
Exact plain pipe tokens are retained for the unchanged legacy synthetic fixtures;
there is no mixed or permissive escape route. Four additional source-only methods
cover observed syntax, malformed/bounded forms, exact ends, and a full synthetic
FD route with wrong-inode/wrong-parent negatives. The27 historical methods and
six sink methods keep their bodies unchanged, for37 projected controls. None of
the new ten controls has run; no Real mathematical expectation is waived.


The artifact-only inspection checkpoint adds `real-smt-inspection`, a scratch-only
workflow focus with `b3_compile_only=true` and `b3_full_gate=false`. It skips SDK,
Java and tracer setup. Its only external commands download the exact historical
public artifact and run the fixed parser/ownership controls; it never executes
Replay, a worker, a solver, normalization, or a new query. The local entry is:

```sh
python3 -B .github/review/real-smt-capture/inspection.py NEW_OUTPUT_DIRECTORY
```

The input is exactly run37244861245/head6839c1c0, artifact11319066007,
710066155 bytes, SHA256
`858af57479406a051568c82f5b322a99b21d6860923d34521005d518f4e98714`.
All2618 canonical exported regular files, modes, lengths and SHA256 values are
checked before/while extraction and rechecked after inspection. The declared
layout is fixed to2611 files below `out/b3-native-compile` and7 below
`out/b3-real-smt-setup`. Metadata/download processes alone receive the token
variables; bounded owned controls receive the filtered environment. Python and
`gh` executable hashes, stage ownership/drain receipts and fresh source seals are
recorded. This is observed provenance, with no signed attestation claim.

Actual historical run37244861245 passed37 controls and completed8 trace sinks,
with unchanged FSIZE and zero supervisor signals/residual children. Parsed tracee
signal calls remain separate observation-only rows, with no ownership claim. Its parser
rejected all8 streams before qualification. This checkpoint preserves those old
receipts. Exact interrupted/terminal read/readv/write/writev records now become
immutable non-result observations, with original TID/FD/text/begin/end/reason and
no integer result or bytes. Any unresolved selected worker/solver fd0/fd1 stream
still rejects full qualification. Completed tracee-memory observations can remain
an explicitly unqualified prefix, stopped before the first selected unresolved
observation. Neither such a prefix nor FIFO EOF is a complete stream claim.

The new schedule fields compare sequence, obligation ID and breadcrumbs in order,
including both universal1 Choice attempts. They do not overwrite the historical
host's false `exactCheckCoverage`/`mathematicalMatched` fields, deduplicate goals,
or change Program.cs or packets. The mathematical match still requires the
original exact outcome, traversal and attempt rules; Unknown/ToolError do not pass.
A fully qualified physical query, if any, is labeled by its exact attempt row;
unqueried remaining attempts must stay explicit ToolError. Resource counts remain
unavailable. No stronger prefix or Real acceptance is claimed.

The new seal declares24 utility/input files. There are45 projected control methods:
all37 historical bodies are unchanged, plus the eight names fixed in
[the inspection plan](../../../docs/dev/b3/real-smt-inspection-plan.md).
The intentional non-verifier timeout control retains its expected failure and
validated poisoned cleanup; it is not proof execution. These eight new methods
and the inspection route are source-only and have not run. Diagnostic zero exit
only preserves the receipt, including setup, parser, ownership or capture failures.
