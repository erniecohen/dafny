# Passive native solver-input observation

This qualification tool observes the pinned Boogie transport used by the cache
comparisons in https://github.com/erniecohen/dafny/pull/262. Build it against a
matching Dafny binary directory:

```sh
dotnet build .github/measure/NativeSolverWire/NativeSolverWire.csproj -c Release \
  -p:DafnyDirectory=DAFNY_BINARY_DIRECTORY -o AUDIT_BINARY_DIRECTORY
```

Copy the matching Dafny binary directory's dependencies into the audit directory
before building. Run the resulting `NativeSolverWire.dll` with a JSON request:

```json
{
  "Cwd": "SOURCE_DIRECTORY",
  "Arguments": ["verify", "input.dfy", "--solver-path", "Z3_BINARY", "--cores", "1", "--verification-time-limit", "0"],
  "Destination": "OBSERVATION_DIRECTORY",
  "LiveDirectory": "TEMPORARY_OUTPUT_DIRECTORY",
  "Modes": ["off", "cold", "warm"]
}
```

Use only `off` when observing a build without the translation table. `cold`
clears the process-local table; `warm` retains it. Every mode receives identical
source and observation paths. The supplied live directory is deleted before each
mode; use a disposable directory. The observer redirects Boogie and JSON reports
to this directory and moves completed outputs under the mode's directory. It
preserves original argument errors by placing its observation options first.

The supported transport is Boogie `v3.5.5+review.c5a9f2ef` with native SMT-Lib
Z3 and ordinary verification. Reflection obtains the private native input writer;
a different ABI fails explicitly. The wrapper writes through unchanged, hashing
the exact encoded characters, lengths and original newlines. It inserts no SMT
commands, changes no command bytes/order and does not normalize the wire.

Each `solver-*.jsonl` contains two independent native-byte observations:

- `task-wire` hashes the exact encoded sequence after a task's empty solver
  checkpoint through its completed proof, including setup options, the prelude,
  assertion input, native request pings and adaptive model/error queries. In this
  pinned ABI each `Checker.BeginCheck` resets its prover; the `(reset)` operation
  establishes the boundary, and bytes after it belong to the task. A replacement
  process starts at its fresh empty checkpoint. The observer attaches the problem
  name at `NewProblem`, which occurs after setup. It ends the task immediately
  before `GoBackToIdle` starts the idle handshake, or at native close. An
  overlapping task or missing checkpoint fails explicitly. Every `NewProblem`
  increments `meta.json`'s `taskStarts`; acceptance requires exactly that many
  completed task records, preserving problem names and multiplicity.
- `wire-final` hashes the entire physical process history, including checkpoint
  operations, previous tasks and idle handshakes. `meta.json` counts every physical
  solver created through the factory; require one final observation for each.

Strict raw-SMT equality compares `task-wire` SHA-256 and byte lengths by problem
name, preserving multiplicity. Physical solver identity, boundaries, counts and
idle recycling are reported separately and are excluded from that equality.
[The measured idle-recycling nondeterminism](https://github.com/erniecohen/dafny/issues/287)
is present with the translation table OFF. A difference in any task's own bytes
still fails. Prepared-prefix hashes are retained separately; they include
prover-interface calls/problem names and must not be called raw SMT hashes.

The observer awaits Boogie's existing terminal `GoBackToIdle` handshakes, including
its asynchronous/inlined call shapes, and retains only weak references to finished
solver wrappers. A missing cleanup handshake fails rather than accepting a
truncated stream. The pinned native transport constructor writes no input before
the hook is attached, and its close operation ends input without emitting another
SMT command; see [the pinned transport source](https://github.com/erniecohen/boogie/blob/v3.5.5%2Breview.c5a9f2ef/Source/Provers/SMTLib/SMTLibProcess.cs).

Run `python3 .github/measure/NativeSolverWire/compare.py LEFT_MODE_DIRECTORY
RIGHT_MODE_DIRECTORY` to check coverage and equality; it exits nonzero for any
task-byte or report difference. Physical carrier differences are printed without
failing that check. It compares `boogie.json`, stdout, stderr, exit, task wire hashes/lengths and complete
untimed report rows. Split rows may finish asynchronously; pair them by VC number
and random seed while preserving all fields and multiplicities. Record exact
source/binary hashes and every argument per cohort. Distinguish ordinary solver
pooling from explicit `/restartProver` and `/deterministicLiteralHashes:1`
profiles. Version probes outside the verification-task solver factory are not
included in the physical proof-solver counter. Timing values describe the current
observation; they do not establish equality or a proof-cost change.
