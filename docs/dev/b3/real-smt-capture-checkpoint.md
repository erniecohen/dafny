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
fast or missed live-image capture and unsupported trace shapes stay incomplete.
A command error may prevent a later check-sat, whose source attempt must remain
explicit ToolError. No query, guard, solver option, seed or outcome is rewritten.

This is an unexecuted source checkpoint. Python AST/XML/source-seal checks are
static evidence only. Fifteen parser regressions, the C# replay build, trace
qualification and all eight worker cases require a future reviewed CI execution.
No workflow routing is enabled here. Tracing overhead and file caps are diagnostic
instrumentation and cannot establish original acceptance or proof cost parity.
The source seal is `98c9b875d0fb254cbc0c4137b142e88730f198a51b191b82a85f8441ff544289`
for 19 files; the reviewed Git/CI source identity binds that declaration without
claiming a signed origin attestation. The product ledger remains
`v4.11.0 0e36fadf2a702df121bc9c0fe0b11e43e475e207` because only review tooling and
documentation change.
