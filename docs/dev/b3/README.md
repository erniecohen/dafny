# B3 backend implementation

Tracking issue: [#116](https://github.com/erniecohen/dafny/issues/116).

The implementation reuses the existing typed, pre-VC Dafny-to-Boogie AST translation. Boogie remains the default backend. B3 normalization, verification and result reporting must not invoke Boogie VC generation or proving. A separately pinned .NET worker will construct RawAst and run B3 resolution, type checking and static consistency checks before verification.

The branch currently contains baseline investigation records. It does not yet provide a B3 backend or claim language support.

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
- `support-matrix.json`: supported language/options, initially empty.
- `discrepancies.json`: outcome differences; currently no measurements.

Newest upstream work is inspected from the original repository pull-request refs as well as its main branch. PR #13 adds domains and contains acknowledged limitations and proof debt. PR #12 soundness work remains a separate unmerged change. These statuses must not be hidden by a dependency pin.
