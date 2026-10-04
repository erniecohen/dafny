# Fixed qualified shared native smoke coordinator

This is an unpublished source-only routing checkpoint for issue126, based on
the independently reviewed706c9542 harness/coordinator source. No new harness
build, native proof, solver or control has run. The distinct shared-native-smoke
workflow choice selects only this fixed coordinator. Every existing focus and
default behavior remains unchanged: native-proof-smoke still selects its prior
coordinator, and unavailable-metadata still selects the prior nonproof coordinator.

The new focus explicitly skips all ordinary build/verifier/standard-library/
regression/binary/release jobs and the previous b3-native-compile job, even if
compile_only is false. Its dedicated job records NOT GREEN diagnostics with
exit0 unless compile_only=true, full=false and every other boolean is false.
Only passing routing/source pins enable setup-dotnet and the coordinator. There
is no Java setup, Dafny/Boogie product build, B3 library build or full gate here.

Before setup, routing checks the exact44 source manifest and declared bytes,
the five-file external inventory and coordinator29524byte SHA
3e4e8712a080e7deb457d61a488384a63c814a192c9cd455e5271e3165fb0657.
Invocation then captures and checks that same coordinator body again and
compile/executes those checked bytes, with no cache or forwarded arguments.
Both workflow Python entries use isolated -I mode. Expected routing/coordinator
failures preserve a failed summary while allowing always-on diagnostic archival;
job exit0 remains delivery only. The sole artifact is out/b3-native-compile,
under its existing b3-native-compile name.

Source44 remains exactly
b2e0e676f8c279e25358330ac3a91e740a12ddce319fd9aaedc5f1bd508dbc90.
Its route-unregistered metadata documents the immutable706c source checkpoint;
actual routing state now belongs to this external five-file manifest and workflow
revision. The original19/other36 payloads, coordinator body, owned supervisor,
normal Program and product/base8333 bytes remain unchanged.

The new file b3-shared-native-proof-smoke.py accepts no command-line arguments.
It downloads only the exact qualification artifact11316129057 from public
run37238572256, checks its80709394byte ZIP SHA
4f4b07ddac0ca8d5d8876a349ce6871d12d0dcf774c757971dabe29bf8ce7a29,
then extracts the exact1192 regular entries under out/b3-native-compile.
Run/artifact provenance is fixed historical reviewed data. The coordinator fetches
the ZIP endpoint only; it does not claim a fresh run/artifact metadata fetch.
No alternate archive layout, artifact, candidate build or package supplement is
accepted. The original qualified summary and bundle remain distinct from the new
smoke summary and shared-harness output.

It replays the unchanged strict schema2 inspector against all three historical
nonproof receipts, exact source38/coordinator5/compiled pins and complete archived
package catalogs. It validates original b07c baseline archive and archived a613
candidate input, all19 prior lifecycle source pins, the inspected eleven-control
lifecycle receipt and the exact solver image. Pinned public Z3 shell source bytes
are fetched and hashed as technical evidence for the fixed -version/-smt2/-in
origin-resource boundary. This is not a signed compiler-origin statement.

The coordinator source manifest pins every recursive harness source/fixture file
except itself. A separate five-file manifest pins this document, the unchanged
owned-process supervisor, this coordinator, the two-token review base and unchanged
workflow. Declared source and coordinator bytes are checked before/after the SDK
build, before/after the fixed proof host and finally. The dotnet executable and new
DLL/deps/runtimeconfig/compiled-manifest bytes are rechecked at those boundaries.
Current C# session boundaries also rehash that same complete new bundle.

Reviewed Python imports compile exactly captured, hash-checked source bytes in
fresh namespaces; cached Python bodies are not read. Bytecode writes are disabled.
Initial exact source inventory is separate from declared-file post-build checks,
so legitimate SDK obj/bin outputs do not change the frozen source scope.

The SDK builds only this dependency-free net8.0 harness, using the fixed
NativeProofSmokeProgram StartupObject override,Release,m1 and shared compilation
disabled. No Dafny product is rebuilt. A failed build keeps its primary process
failure; absent compiled-manifest output is recorded distinctly, while original
source/coordinator/dotnet checks still run in the finally path. A successful build
must produce an exact four-file bundle and a DLL distinct from the historical
qualified nonproof assembly.

The fixed proof host receives one bounded JSON inputs file, no native argument
array. It selects original product packages, fixed fixtures, exact solver,
qualification ZIP and lifecycle/origin evidence. The actual MainWithWriters argv
is generated internally: verify,cores1,time20,resource200000,configured wrapper,
native JSON and CSV logs. The ordinary native parser, defaults, translation,
solver arguments, query bytes, raw replies and seeds are not rewritten.
A600second outer host safety deadline bounds all six20second smoke verification
calls plus pin checks/cleanup; it is not a cost metric or increased proof budget.

Process stages use the byte-identical exclusive subreaper/pidfd supervisor. Every
direct root is pinned before waiting; observed descendants use exact parent
identities or exclusive subreaper adoption. Failure signals target captured owned
pidfds only; they preserve NOT GREEN. The narrow build/download disappearing-
descendant observation records remain visible. No numeric-PID or global process
kill fallback is introduced.

Linuxx64 and a private delegated cgroup-v2 parent with pids.max300 are required.
Only the fixed C# host runs through sudo with resolved dotnet and an explicit
env-i whitelist; no GH/GITHUB token, LD_PRELOAD,LD_LIBRARY_PATH or LD_AUDIT enters
that privileged host. Effective UID0 is recorded. This is explicitly privileged
CI, not an unprivileged deployment result.

The coordinator retains the private parent's device/inode and directory fd.
Its finally path independently checks owned membership/population. Any inspection
fault or surviving leaf/member triggers the exclusive fd-anchored cgroup.kill
drain, bounded population wait and separate procs/events empty checks; intervention
keeps the gate failed. Unexpected subgroup names prevent deletion/acceptance after
drain. No process outside the owned parent is signaled. Empty pinned-parent removal
runs as a separately owned bounded sudo stage. Only the newly created private
proof evidence subtree is made readable after host termination and parent cleanup.

Acceptance requires exact six-case ordering and verdicts, actual nonzero native
batch/assertion/source logs, all28 assembly-object/context audits, zero unavailable
demands/poison, actual private collection after scheduler disposal, all native
owned pidfds/leaf cleanup, strict raw SMT per-VC final replies matching untouched
native JSON/CSV resource counts, full process evidence, no coordinator cleanup
intervention, no remaining children and all six integrity boundaries. A job exit0
only delivers a summary; expected or unexpected failures remain summary NOT GREEN.

Any six-call PASS would qualify only this archived a613 loader/smoke experiment.
It would not establish fresh Boogie state, order independence,174check/fullsuite/
stdlib/final integrated default parity or later B3/Real/BV/G3 candidate qualification.
Those remain separately required. No public run is authorized by this source
checkpoint, and ordinary Program still has no proof route.
