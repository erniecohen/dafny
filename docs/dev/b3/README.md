# B3 backend implementation

Tracking issue: [#116](https://github.com/erniecohen/dafny/issues/116).

The implementation reuses the existing typed, pre-VC Dafny-to-Boogie AST translation. Boogie remains the default backend. B3 normalization, verification and result reporting must not invoke Boogie VC generation or proving. A separately pinned .NET worker will construct RawAst and run B3 resolution, type checking and static consistency checks before verification.

The branch implements an experimental bool/int slice with a supervised worker. It retains goals, state transitions, modular contracts and inductive loop checks, and rejects unhandled constructs. Source axioms, ordinary nonidentity definitions, distinctness constraints and lambda equations remain omitted. Exact typed integer division/modulo bodies and active always-revealed universal definitions now substitute native operations under the [arithmetic correspondence argument](arithmetic-correspondence.md); all 36 real-input controls passed in public run 37207690631, including divisor signs and zero-divisor failures. Direct closed monomorphic reads of owned stores now use the reviewed read-over-write ITE identity, with zero global map axioms. Exact complete-tuple observation equality uses an uninterpreted predicate of the two map values; other map operations, polymorphic maps and collection definitions remain conservative abstractions. All 15 strict map controls passed in public run 37207690631. The [map correspondence argument](map-helper-correspondence.md) and 15 strict worker verdict controls record the exact boundary. This is incomplete support, with real/bitvector and visibility coverage tracked below.

The documented subset and clean-install gates passed in public [run 37209385158](https://github.com/erniecohen/dafny/actions/runs/37209385158), on exact source `f927757337371038a61406a3b107543ca2b72f77`. The receipts record 26 neutral Core controls, 137 normalizer controls, 41 protocol controls, 24 host controls, 40 IDE controls, four integration regressions, all 15 strict map cases and all 36 real Dafny corpus cases. The unchanged pinned B3 library has [557/557 verified bootstrap batches](https://github.com/erniecohen/dafny/actions/runs/37201990430); its complete source inventory and binary hashes were checked before reuse. Two [experimental package](experimental-package.md) builds from identical binary inputs produced the same archive hash. Clean-install version, completed valid B3 verification and actual assertion-failure controls passed with no residual child processes or dependency downloads. Scratch job success alone is not acceptance; these are the inspected stage receipts.

Focused default-Boogie checking confirms the translation-error repair, while exact full-suite resource-count parity remains unresolved. Release validation and broader real/bitvector and visibility coverage remain pending. Repairs and conservative approximations are recorded in `discrepancies.json`.

## Building and selecting the worker

`ThirdParty/B3/source-manifest.json` pins the newest upstream contribution and the structured-library patch. Supply the exact bootstrap compiler archive recorded in `dependencies.json`, plus Z3 5.1.0, to:

```sh
Scripts/build-b3-worker.sh compiler.tar.gz /path/to/z3
```

The script verifies the B3 library with the pinned bootstrap compiler before publishing the worker package. Keep the archive with the build inputs; the linked CI artifact has a retention limit. A future release package must make these inputs persistently available.

Select B3 using the modern CLI:

```sh
dafny verify example.dfy --verification-backend b3 \
  --b3-worker build/b3-worker/package/DafnyB3Host.dll \
  --solver-path /path/to/z3
```

For an editor project, place `verification-backend = "b3"`, `b3-worker = "/path/to/DafnyB3Host.dll"`, and `solver-path = "/path/to/z3"` in `[options]`. Command-line values take precedence. The default worker location is `b3/DafnyB3Host.dll` beside Dafny. Worker binaries must stay beside their build manifest and library dependencies; changed or missing files fail validation. Boogie remains the default when the option is absent.

The experimental B3 backend supports the `verify` command and editor verification. Selecting B3 for `build`, `run`, `test`, or `translate` fails before compilation or worker lookup, including with `--no-verify`. Those commands still use the Boogie verification pipeline, so their B3 selection is rejected until they can use the backend-neutral pipeline.

Editor counterexample requests require a backend that provides models. B3 requests return an explicit JSON-RPC invalid-request error before verification starts. Cancelling a counterexample request stops its pending wait.

`measure-complexity`, `generate-tests`, and `find-dead-code` require metrics or counterexample generation that this B3 slice does not provide. They explicitly reject B3 selection from the CLI or project file before preparation or generation. Test generation and dead-code analysis accept `--verification-backend boogie` to override a project's B3 setting.

B3 uses a fresh isolated process for each generated checking unit. The time limit covers the unit, including worker startup and IO. It requires a positive time limit and currently supports Unix process groups, arithmetic solver 2, and ordinary Z3 invocation. Solver unknown, invalid packages, incomplete output, cancellation and unsupported input produce non-success outcomes. Models, proof dependencies and resource counts are unavailable in this slice; options requiring those capabilities fail explicitly.

Editor verification caching is unsupported. Keep `cache-verification = 0` (the default) in project options, or `--cache-verification 0` when starting `server`. Positive levels fail configuration preflight even for a source without verification units.

## Running the backend gate

After the pinned verifying bootstrap, run the repository entrypoint:

```sh
make test-b3 B3_LIBRARY=/path/to/B3Library.dll Z3_PATH=/path/to/z3
```

It builds the CLI, checks the neutral contracts and backend selection, runs normalizer and worker controls, requires all 15 typed map verdict controls, and executes the real Dafny corpus. The final corpus rejects unsupported features and includes negative assertion/contract controls. Protocol and normalizer test projects also participate in the normal solution test entrypoint. The host stays separate because its verified library artifact is an explicit build input.

## Work packages

- [P0 #117](https://github.com/erniecohen/dafny/issues/117): baseline, bootstrap and emitted-IR census.
- [P1 #118](https://github.com/erniecohen/dafny/issues/118): backend-neutral task/result boundary and Boogie adapter.
- [P2 #119](https://github.com/erniecohen/dafny/issues/119): pinned B3 worker, structured results and fail-closed transport.
- [P3 #120](https://github.com/erniecohen/dafny/issues/120): first real Dafny-to-B3 vertical slice.
- [P4 #121](https://github.com/erniecohen/dafny/issues/121): representation specialization, expressions, maps and lambdas.
- [P5 #122](https://github.com/erniecohen/dafny/issues/122): explicit state, contracts, old and where-clause semantics.
- [P6 #123](https://github.com/erniecohen/dafny/issues/123): loops, labels and structured control flow.
- [P7 #124](https://github.com/erniecohen/dafny/issues/124): typed native real and bitvector theories.
- [P8 #125](https://github.com/erniecohen/dafny/issues/125): definition visibility and isolated axiom contexts.
- [P9 #126](https://github.com/erniecohen/dafny/issues/126): CLI, project, IDE, options and capability completion.
- [P10 #127](https://github.com/erniecohen/dafny/issues/127): differential corpus, packaging and release acceptance.

[Native rotation soundness #147](https://github.com/erniecohen/dafny/issues/147) tracks a separately reproduced default-verifier discrepancy. Portable B3 rotation support remains part of P7; the native false postcondition is never an accepted differential expectation.

P0 establishes the source/toolchain and emitted-IR boundary. P1/P2 establish the task/result and worker protocol contracts. P3 delivers a true/false/unsupported slice using real generated Dafny checking units. P4-P8 complete semantics, P9 completes consumers and option handling, and P10 gates packaging and release.

## Trust and review conditions

- Preserve the existing prelude, guarded boxing/collection axioms and cardinality validation.
- Account for every generated implementation and check; failure during preparation cannot become empty success.
- Only a complete successful traversal with matching structured results can yield verified status. Solver unknown, unsupported input, cancellation and transport failure are distinct non-success outcomes.
- Independently review model interpretation for specialization/maps/lambdas, contract/where/old/loop correspondence, scoped definition availability, and solver/result protocol health.
- Track advertised support and differential anomalies explicitly. Agreement with Boogie is evidence of agreement, not an end-to-end soundness proof.

## Records

- `dependencies.json`: exact upstream source/package and bootstrap identities.
- `work-items.json`: issue/branch mapping.
- `support-matrix.json`: validated language/options and explicit exclusions.
- `discrepancies.json`: reproducible outcome differences and conservative approximations.

Newest upstream work is inspected from the original repository pull-request refs as well as its main branch. PR #13 adds domains and contains acknowledged limitations and proof debt. PR #12 soundness work remains a separate unmerged change. These statuses must not be hidden by a dependency pin.
