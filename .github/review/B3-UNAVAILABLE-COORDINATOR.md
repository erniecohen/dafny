# Fixed disposable unavailable-metadata controls

The `unavailable-metadata` scratch workflow choice runs only the three reviewed
nonproof controls. It never builds a new candidate product, invokes a solver,
enables a native proof entrypoint, or establishes query/resource parity. Expected
failures are recorded with job exit zero; acceptance requires `summary.json`, all
three exact control receipts and all process-stage ownership receipts.

The imported harness source is exactly the accepted 38-file manifest
`2fc847ab499ae08e398cbd748426d476589e1c74365348f7c6eb252e5c23ecfb`.
All nineteen previously qualified lifecycle source files are unchanged. StartupObject
is overridden at build time only to `B3AlcGate.UnavailableMetadataProgram`; the
project default and normal Program remain unchanged. Both native proof APIs in
this source are explicitly disabled. The output source manifest must match the
input manifest byte-for-byte; the built DLL, deps and runtimeconfig get distinct
actual hashes in the new receipt. These are CI build declarations, not signed
compiler-origin attestations. A build requires an absent fresh output directory;
there is no prior compiled manifest to trust before that build. The exact compiled
manifest is checked in the build's finally path, including failed SDK stages.
Only a successful build can declare the complete new DLL/deps/runtimeconfig bundle.

The baseline archive remains public run 37182760834, SHA256 `679b3569...`, original
b07c source and Core `9e4eaf6a...`. The candidate remains the archived package from
public run 37221204349, source `a6132c6758c863ec4a897e4ac1534dc727f0b006`, full package
manifest `3d592286...` and Core `92824cdc...`. Every candidate file is compared with
that exact inventory before and after each control. The C# host rechecks both packages and
baseline archive. The candidate's earlier native smoke remains explicitly NOT GREEN;
its successful compilation is only package provenance. The downloaded artifact also
preserves the original eleven-control lifecycle receipts and their source pins,
without rerunning them or executing the archived solver.

A separate coordinator manifest pins this document, the process supervisor, the
coordinator, the corrected two-token review base and workflow routing. Assertions
must be enabled. The original source manifest is pinned by its accepted literal
hash; the separate coordinator manifest hash is captured at initial preflight and
must remain exact. Platform checks and exact recursive source-directory inventory
are an initial prerequisite. Subsequent checks reread only the declared 38 source
files and five coordinator files, so legitimate SDK obj/bin output does not expand
or weaken the frozen source scope.

`immutableInputChecks` must contain exactly ten passing boundary records: before
build, after-build-sources, after-build, then before/after each fixed control, and
final. Every record checks the original 38 source pins, the unchanged five-file
coordinator manifest and file pins, and the resolved dotnet bytes. After the fresh
build, every control boundary checks the complete DLL/deps/runtimeconfig/compiled
manifest bundle by original hash and size. Post-boundary checks run in finally
paths even after a failed or cancelled control's owned cleanup. An input fault
preserves NOT GREEN; a passing earlier stage cannot erase it. The baseline archive
and complete candidate inventory are also rechecked at these boundaries.

Reviewed Python helper and validator imports compile/execute a captured,
hash-checked source-byte buffer in a fresh module namespace with the correct
file/spec metadata. They never call SourceFileLoader.exec_module or consume a
cached Python body. Python bytecode writes are disabled before these imports.
The two imported captures, byte counts and exact hashes are recorded independently
of later on-disk pin checks; the accepted validator source is unchanged. No harness
or reviewed helper body is executed during a source-only check.

The process supervisor establishes an exclusive Linux subreaper,
with default SIGCHLD disposition, no initial children and no concurrent subprocess
launcher. Every stage creates a new session and captures the actual direct child's
PID/start-time/parent identity and pidfd before waiting. The control command is
exactly the resolved hash-recorded dotnet host, this compiled harness and one input
JSON path. Each of the three controls runs in a fresh host; framework hash seeds
are not asserted equal between these disposable nonproof processes.

Direct-child lists from every owned process thread are scanned repeatedly. A
validated owned-parent edge, or new direct-child adoption by this exclusive
subreaper, establishes descendant ownership; no process outside that lineage is
signaled. Captured pidfds remain authoritative across reparenting and PID reuse.
A recycled PID receives a new captured identity and cannot inherit an old root
classification or pidfd. The recorded descendant condition is observed ancestry,
not a claim that polling detects every process that could be born and reaped
between samples. The fixed nonproof source invokes no process creation API.

Accepted control stages require exit zero, exactly the one recorded root, no
observed descendants, no poison or signal, all owned pidfds exited, root reaped,
and zero direct children. The strict imported inspector then validates every
metadata, missing-demand, assembly-object, Default/private/context, collection,
source and package fact in the C# receipt. Output parsing never substitutes for
process exit or ownership evidence. Read-only artifact download/build stages use
the same bounded supervisor but can observe naturally exiting SDK descendants.

Failure, timeout or cancellation poisons the stage and performs bounded cleanup
through verified owned pidfds only. Root/known descendants are stopped before new
inspection attempts, and registry faults still perform a direct-child drain with
fresh parent/start-time/pidfd validation. Any cleanup signal preserves failure even
if all processes subsequently exit. No numeric-PID or process-group signal fallback
is used. A 512-entry metadata bound and larger direct-list bound cannot turn a
cleanup error into acceptance; remaining children or failed capture remain NOT GREEN.
This helper is source-reviewed here but has not yet been runtime-qualified. It is
not a general hostile fork-bomb containment claim.

The coordinator handles SIGINT/SIGTERM as cancellation, including signals received
inside cleanup. Repeated cancellation cannot interrupt an already bounded cleanup.
A control's own 45-second watchdog remains in force; its outer stage deadline is
50 seconds with up to ten seconds of failed cleanup. SDK build has a separate
600-second safety cap. No clock measurement is treated as proof cost.

Only public artifact downloads receive the workflow read token. Build and control
environments use an explicit small whitelist and never preserve GH_TOKEN or other
credential variables. No sudo, cgroup delegation or solver execution is used in
this new mode. The older qualified privileged lifecycle boundary is recorded as
historical evidence only, not generalized to these controls or deployment.

Linux process-lifetime behavior is based on
[pidfd_open](https://man7.org/linux/man-pages/man2/pidfd_open.2.html),
[PR_SET_CHILD_SUBREAPER](https://man7.org/linux/man-pages/man2/PR_SET_CHILD_SUBREAPER.2const.html)
and the [Python pidfd signal API](https://docs.python.org/3.12/library/signal.html#signal.pidfd_send_signal).
The native shared-library proof path, optional-reference reachability beyond the
fixed controls, fresh Boogie state, solver origin qualification and full default
cost parity remain separate work. No unavailable assembly is treated as resolved.
