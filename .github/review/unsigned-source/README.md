# Unsigned wrapper typed-source diagnostic (issue 124)

This source-only diagnostic reuses the compiler built at public source
`7b60f93c2cf81486178eb7fcd6fdc35f7e868c71` in
[the compiler-only run](https://github.com/erniecohen/dafny/actions/runs/37236538953).
That run's compiler and contracts passed, but its normalization gate failed
435/436. Its original failed summary remains byte pinned and is copied intact.
Reusing this archive establishes compiled source provenance only. No library,
worker, solver, verification result, backend acceptance or default parity is reused.

`fixture.dfy` contains exactly the 201-byte value of the raw string in
`B3UnsignedWrapperTests.ActualDafnyDirectRangeRoundTripAndFalseChecksRemainPresent`:
no final newline, URI `file:///B3VisibilityTests.dfy`, default options and the
captured prelude. `Program.cs` repeats the original helper's scope reset,
parse/resolve/translate/Boogie-resolve/typecheck sequence. It enumerates every
translated module and all three original implementations, without selection.
Every result, including Unsupported and all diagnostic tokens, remains present.
Normalization outcomes are observations rather than expected successful verdicts.

The inventory assigns deterministic IDs by actual reference identity and reports
all raw commands, StateCmd containment, transfers, procedure predicates, where
predicates and structured If/While guards. Each guard includes independently
matched raw positive and negative assumptions, the original source child paths,
resolved function/declaration identities and exact partition attributes. These
flattened reference/complement matches are observations, not owner-specific
producer-branch or G3 availability certificates. Names are descriptive; they
grant no semantic recognition. Complement matching is a
read-only copy of the current CFG validator's finite shapes and creates no
Boogie expression. The inventory is compared before/after normalization.
The original source is emitted with the exact printer overload setting
`allowPrintDesugaring=false` and `setTokens=false`; no desugaring, passification,
engine instance, verification tasks, pruning or prover is invoked.

`inputs.json` seals the original public archive and all 89 flat runtime inputs:
all DLLs (including 13 Boogie DLLs), dependency/runtimeconfig files and prelude.
The source-sealed coordinator loads Python modules from captured source bytes,
checks public metadata/archive bytes, and checks all captured inputs before and
after the SDK build and runner. Only the tiny diagnostic is built against those
captured assemblies; the Dafny compiler is not rebuilt. The generated runner
DLL, dependency file and runtime configuration are bound together by actual
bytes and hashes before execution and rechecked afterward. Recorded `dotnet`
and `gh` image bytes/hashes are checked before and after every stage. Restore
uses an empty package-source list. Authentication variables exist only in
download stages;
they are excluded from SDK and runner descendants.

Each stage uses the unchanged reviewed Linux owned-process helper: dedicated
child-subreaper, validated pidfds, empty initial/final owned child scopes and
bounded cleanup. Any cleanup signal or failed drain prevents later stages.
SDK/runner stages have 120-second safety caps and 32 MiB log caps. Reference/node
inventories admit at most 100,000 nodes, depth below 128 and 256 raw blocks per
unit; the aggregate typed output is bounded by 8 MiB. These are diagnostic
resource limits, not logical assumptions or altered verification settings.

Select `b3_focus_gate=unsigned-source`, `b3_compile_only=true`,
`b3_full_gate=false` on a scratch branch. Other modes retain their existing gates.
The diagnostic mode records failures and always uploads its receipt with exit
zero. A zero exit is delivery only: `diagnosticReceiptProduced` requires a
complete three-unit receipt; `acceptanceClaimed`, `verificationAttempted`,
`completeLibraryVerified` and `libraryBinaryProduced` remain false.
No implementation or execution acceptance is claimed by this source checkpoint.
