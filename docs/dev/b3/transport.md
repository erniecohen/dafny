# Experimental B3 transport v1

This is a transport milestone of [P2](https://github.com/erniecohen/dafny/issues/119),
stacked on [PR 539](https://github.com/erniecohen/dafny/pull/539). It supplies no B3
source, solver integration or alternative Dafny backend. Ordinary Dafny execution
does not reference these assemblies. The executable must be invoked explicitly:

```sh
dotnet build Source/DafnyB3.Worker -p:NuGetAudit=false
dotnet Source/DafnyB3.Worker/bin/Debug/net10.0/DafnyB3.Worker.dll --protocol-v1
```

The worker reads one request and stdin EOF. It emits an `Unsupported` result for
each obligation, followed by an `Unsupported` completion, and exits 1. Invalid
arguments exit 2; malformed/missing/additional input or I/O failure exits 2 with
a fixed diagnostic. There is deliberately no successful proving implementation.
Neither parsing a request nor traversing its manifest is verification.

## Envelope and limits

`DafnyB3.Protocol` has only framework dependencies; it references neither Dafny,
Boogie nor B3. It defines manifest envelopes independently of a normalized IR.
The normalization schema and checked payload remain future work with P1/P3.
Protocol version 1 is an envelope contract, not a claim to freeze that future IR.

Each frame is a UTF-8 JSON object terminated by LF, at most 65,536 bytes excluding
LF. Invalid encoding, partial frames, duplicate/unknown properties, missing
constructor fields, unknown enum values and numeric enum encodings are errors.
Property names and string outcomes are case sensitive. EOF is legal only between
frames and never substitutes for completion.

The request has `Version` (1), `RequestId`, `UnitId` and `Obligations`. Identities
are nonempty ASCII letters, digits, `_`, `-`, `.` or `:`, at most 128 characters.
Exactly one unit and 1–256 unique obligations are permitted. The caller must
derive the manifest from checked preparation, rather than from worker output.

Every response has `Version`, `RequestId`, `UnitId`, `Sequence`, `Kind`,
`ObligationId`, `Outcome` and `Completed`. Sequences start at zero and increase
by one across all response frames. A `result` names exactly one still-expected
obligation and has `Completed: 0`. A `complete` has `ObligationId: null` and the
exact manifest count, after every expected result. Unknown kinds, foreign or
duplicate identities, missing results and trailing frames poison the ledger.

Outcomes are `ToolError`, `Proved`, `Failed`, `Unknown`, `Unsupported`, `Cancelled`,
`Timeout` and `ResourceLimit`. Only all-`Proved` results plus a `Proved` completion
can satisfy the ledger. Non-success outcomes never become successful through a
display label, an exit code, a partial result set or an empty manifest.

## Process boundary

`WorkerClient.CheckAsync` takes an executable and argument list, without shell
interpretation, a manifest and a mandatory deadline (positive, at most one hour).
It snapshots the manifest and owns one process for that request. Failed sessions
cannot be reused. A transport receipt is returned only after the ledger succeeds,
stdout ends cleanly, request writing and stderr reading succeed, and the process
exits zero. Any startup, pipe, decoding, accounting, exit, timeout or cancellation
failure raises an error. A receipt establishes transport consistency with the
selected worker; it is not an independently checked proof or worker attestation.

Request writing and response reading run concurrently. Stderr is continuously
drained, retaining at most 8,192 bytes and recording truncation. The request
deadline includes pipe draining and process exit. Cleanup cancels pending I/O,
kills a still-live worker's entire process tree and reaps it with a separate
bounded wait; pipe-task cleanup is also bounded. Process handles are disposed.
This relies on ordinary OS process-tree ownership: a future worker must itself
own and dispose its solver. It is not containment for a malicious executable that
detaches children or exits before its descendants can be enumerated.

## Acceptance boundary and remaining P2 work

The source baseline's original B3 CLI prints an assertion failure or a type error
without a failing exit status; its process shim passes EOF to string conversion.
The diagnostic controls distinguish true, false, ill-typed input and solver EOF.
These observations explain why wrapping that CLI and trusting exit zero is
insufficient. A diagnostic compilation is not a verified B3 bootstrap.

Tests use an explicitly separate adversarial peer to exercise complete transport
success and all failure paths, including repeated cancellation with a child,
bounded stderr flooding, write/read errors and a peer that never finishes. The
shipped scaffold has a separate negative control: it cannot produce proof success.

Still required before any Dafny-to-B3 proof result:

- Admit a repaired, pinned B3 implementation and bootstrap with no added trusted
  premises; maintain its provenance and patch list.
- Construct and check RawAst for exactly the selected unit; resolve, typecheck
  and check static consistency before traversal.
- Account for generated obligations and traversal independently of the response
  manifest, and connect this transport to P1's request ledger and consumers.
- Connect the worker to the solver shim described in [solver-shim.md](solver-shim.md),
  which acknowledges declarations, assertions and stack operations, parses bounded
  SMT responses and admits only a correlated exact `unsat` for the submitted
  negated goal. The scaffold does not call it yet.

No existing B3 source is vendored or patched here, so its original OSProcess and
solver defects remain open. No completion of P2, semantic lowering, source/model
correspondence or release acceptance is claimed.

Run the transport controls with:

```sh
dotnet test Source/DafnyB3.Test -p:NuGetAudit=false --settings .github/review/ci.runsettings
```
