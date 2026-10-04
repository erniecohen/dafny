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
or cleanup faults cannot establish capture completeness. The 64MiB file cap and
tracing overhead are diagnostic changes; observed outcomes and wall times cannot
establish original execution or cost parity.

Twenty-one source parser regressions cover partial writes, failed writes, vector
buffers, resumed I/O, dump gaps/missing bytes, orphan dumps, missing PID association,
truncation, SMT framing, absent live image, wrong fork parent, PID reuse and exact
executable epochs, including unsupported `execveat` boundaries. They do
not invoke a solver or worker. Six archive admission controls additionally cover
both pinned layouts, late unknown roots with no extraction output, unsafe paths and
modes, canonical duplicates, file ancestry and all admission bounds. The outer
wrapper runs the fixed combined 27-control denominator before any traced replay.
No control has been executed at this repaired source checkpoint.

`DESIGN.md` gives the English mathematical and observational argument. The source
seal declares all 19 utility/input files; the enclosing reviewed Git/CI receipt
binds that declaration. It is no signed build or runtime attestation. Workflow
routing is enabled by the enclosing scratch-only review workflow. The first
execution, run 37240724688, stopped at the original archive's legitimate
`Binaries/net8.0` root: the former validator admitted only `out/b3-native-compile`.
It ran zero parser controls, workers or queries. Its immutable failure receipt
remains historical evidence; this layout repair requires a new reviewed execution.
