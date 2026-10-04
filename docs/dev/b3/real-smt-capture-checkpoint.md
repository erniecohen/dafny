# Real solver-stream diagnostic source checkpoint

Issue #124 now has a separately sealed review utility in
[real-smt-capture](../../../.github/review/real-smt-capture/README.md).
It replays four immutable baseline worker packets from public
[run 37232113837](https://github.com/erniecohen/dafny/actions/runs/37232113837)
and four separate symbolic pure/mixed controls. Their original 200k resource
ceiling and 20-second whole-worker deadline are preserved. The verified library
from [run 37221479537](https://github.com/erniecohen/dafny/actions/runs/37221479537)
is reused without a new library proof or logging rebuild.

The repaired capture restores original Dafny origins and preserves all four
normalized program files byte-for-byte. Conversion remains Inconclusive followed
by ToolError; irrational false remains Inconclusive, and the current corpus is
44/46 strict. The original universal frontend warning remains historical failed
CLI evidence even though both corresponding worker units verified. No expectation
is waived by this diagnostic.

The new wrapper pins both public archives and uses bounded owned-process scopes.
SDK and tracing descendants receive no download token environment. The parser
checks successful exec identity, live solver-image digest, source-bound worker
request/newline bytes, native startup/completion records and exact pipe/FD
lineage. Trace completeness and mathematical matching have separate receipts;
fast or missed process/live-image capture and unsupported trace shapes stay incomplete.
Exact replay argv and a single successful exec epoch per admitted process prevent
later executable transitions from borrowing an earlier image pin; successful
`execveat` remains unsupported. The exact strace 6.8 version line and unchanged
solver path with no directory symlink traversal are required.
Bounded completed-I/O observations may be retained under explicitly unqualified
exec-line/TID labels, without a process, FD or stream-completeness claim.
Strace reads tracee memory after each syscall; reconstructed I/O observations
require the fixed runtime's stable serialized buffers, without atomic kernel
payload attestation. A command error may prevent a later check-sat, whose source attempt must remain
explicit ToolError. No query, guard, solver option, seed or outcome is rewritten.

The current FIFO sink delta is an unexecuted source checkpoint. AST/XML/seal checks
are static only. It preserves the27 prior methods and adds six sink methods for a
projected33. Source-only sink controls and all eight qualified worker cases require
future reviewed CI execution. Historical27-control and SDK evidence below belongs
to the prior archive-repair source, not this new sink.
The enclosing scratch-only workflow routes the diagnostic. Tracing overhead and file caps are diagnostic
instrumentation and cannot establish original acceptance or proof cost parity.
The source seal is `fadf15e7a1680e197892223fd8a68467092a17a023e49ebc88d2d9c96d6b0018`
for20files; the reviewed Git/CI source identity binds that declaration without
claiming a signed origin attestation. The product ledger remains
`v4.11.0 0e36fadf2a702df121bc9c0fe0b11e43e475e207` because only review tooling and
documentation change.


Public run [37240724688](https://github.com/erniecohen/dafny/actions/runs/37240724688)
on source `f6ed9e2c6471db389f34c4fd6b0fccab16ee3d29` completed only diagnostic
preflight. Its exact artifact is 392634483 bytes, SHA256
`d40abddfcecc4870eab62e74c59417e2121d6271237b733a7ac68bfed68cb8a6`.
Routing receipt SHA256 is
`4a18c7b5a5079cd16af93022164fccb8fe53ac58d1a334c5e6c9a5cddf1a4450`;
inner receipt SHA256 is
`17ca82130551d0f0608a9334c2f059dbf994b4c420b7df7a10a9c5cd29f33771`.
Exact strace setup passed, but archive extraction rejected the original artifact's
legitimate `Binaries/net8.0` root with
`ValueError: Unsafe/unexpected public archive path`. Three metadata/download stages
had zero signals and no residual children. Zero parser controls, replay builds,
workers and queries ran; workflow success supplies no semantic evidence.

The source-only repair pins the two archives' complete member-root counts separately
and performs every safety/inventory/bound check before extraction. All admitted
members are hashed; the additional Binaries and host-test roots are never loaded.
Six archive controls join the unchanged 21 parser controls, making a fixed combined
denominator of 27. Original requests, worker/library, primitive formulas, projections,
solver flags, budgets, tracer and ownership checks are unchanged. The former f6
receipt/seal remains historical; this new seal requires a reviewed fresh run.


Public diagnostic [37242135132](https://github.com/erniecohen/dafny/actions/runs/37242135132)
passed exact archive extraction and27/27 controls, SDK/strace checks, replay build
and control preparation. Artifact11317412968 is709614311bytes, SHA256
`f9c5463f786ac8310d5d9f446c968d0618dae10149b7330f6f92aaeae388e13d`.
Outer receipt is`c9e10805f14a4cec08586f31c79708227d86e704c126e18727cb6c30ddc542d4`,
archive-gate receipt`40cd8c113c55e694cfdb8cb67be81de9ccd81ae8d7dc15ed1c8e76f5a7635094`,
capture receipt`71e7ac44d9f8588666ffb73a55f8b7c00826b46da6be6ca698cadf62bf4d6a93`.
The first original traced replay exited SIGXFSZ during .NET8.0.31 startup. Its trace
is39348bytes/SHA256`8480d3068d938e07f3dc56f2105f389d57357aaec742acbda060fb4bb36b34e8`.
No worker/solver exec, result or query was produced; the remaining seven cases did
not run. All supervisor stages used zero signals and left no residual children;
the traced stage remained poisoned. This is incomplete diagnostic evidence and
changes none of the original strict mathematical expectations.

The narrow source repair uses a bounded owned FIFO sink and no resource-limit
mutation. It keeps Program.cs, baseline packets, worker/library/solver, options and
budgets unchanged. It records inherited FSIZE, rejects mismatch, distinguishes
prefix/complete hashes and requires EOF after natural owned exit/reaping inside the
original40s stage deadline. It introduces no axioms, query or acceptance claim.
