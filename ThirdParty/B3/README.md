# B3: checked P0 source snapshot

This directory vendors [dafny-lang/b3](https://github.com/dafny-lang/b3) at
`b24a2b2d6101df900e67d3bb151c4e85af4cd16d`, the P0 pin recorded in
[`docs/dev/b3/dependencies.json`](../../docs/dev/b3/dependencies.json).
The upstream MIT license is retained in `LICENSE`; `README.upstream.md` is the
unchanged upstream README. `UPSTREAM.json` records every copied file's upstream
and patched SHA-256, and the complete patch list.

The snapshot includes all 36 Dafny source files, the C# native I/O adapter,
and all 42 upstream examples with their expected output. Other native targets,
upstream build scripts and reference documentation are omitted. No compiler,
build target, CI workflow, default option or backend registration consumes this
directory. The commands below are explicit developer checks. This is the proof
repair milestone for [issue 119](https://github.com/erniecohen/dafny/issues/119),
dependent on [PR 539](https://github.com/erniecohen/dafny/pull/539).

## Repair and acceptance boundary

The P0 source had eight `assume {:axiom}` statements. Each is removed:

1. `Incarnations.Expect` accepted any Boolean and ensured that Boolean without a
   precondition. `Expect(false, "witness")` therefore let an unconditional lemma
   prove `false`. Its compiled `expect` halts for that argument, which cannot
   justify a total logical function. Partial lookups and expression substitution
   are now methods with executable guards. The unelaborated closure case halts.
   No logical function obtains facts from a possible runtime failure.
2. The two automatic-invariant assumptions in `TypeChecker` become checked
   `Fail` returns before calling the expression checker.
3. The three automatic-invariant assumptions and the procedure-header assumption
   in `Verifier` become executable guards before the dependent operation.
4. The solver-response loop no longer invents a finite response length. It checks
   a decreasing line budget and returns `Failure` on exhaustion. The default is
   1,000,000 lines; developer controls can request a smaller bound.

Both `termination false` traits are ordinary checked traits. The closed
`OptionInfo` datatype uses the fork's checked `termination_closed` facility so
that the CLI override satisfies its cross-module termination contract. Recursive
substitution has explicit structural decreases; sequence/pattern traversal uses
finite loops. Driver and standard-input wrappers no longer use `decreases *`.

The native process adapter no longer supplies an uninterpreted ghost validity
predicate, a pure external function, or axiomatized freshness/validity contracts.
Native operations return arbitrary input values without logical postconditions.
A Dafny wrapper allocates its own fresh object; its representation validity is
`true`, proved from its definition. The four `@Axiom` markers are removed.

This establishes the vendored Dafny program's checked contracts and termination
obligations under this fork and its existing standard library. External I/O can
still block or fail: its C# implementation is a runtime boundary, not a proof of
OS or solver termination. The existing packaged standard library has its own
external assumptions, visible in `dafny audit`; none is introduced here.

The runtime guards retain successful behavior when their predicates hold. The
upstream corpus preserves all results and diagnostics: one debug output requires
comparison modulo map-entry order. Responses exceeding the explicit line budget
now fail. These checks do not prove the B3 verifier's encoding semantically sound,
and the existing transport's EOF and parenthesis parsing behavior is retained.

## Explicit developer checks

Build this fork as described in the repository's `AGENTS.md`. With its assembly
and Z3 5.1.0 available, run from the repository root (replace angle-bracket
arguments with your chosen files/directories):

```sh
python3 ThirdParty/B3/controls/provenance.py --upstream <exact-P0-checkout>
python3 ThirdParty/B3/controls/check.py --dafny-dll <Dafny.dll> \
  --solver <z3> --work work/b3-check --upstream <exact-P0-checkout>
python3 ThirdParty/B3/controls/mutations.py --dafny-dll <Dafny.dll> \
  --solver <z3> --work work/b3-mutations
```

`check.py` requires a nonempty, entirely passed verification CSV, checks the
control program, requires three deliberately false postconditions to fail, then
compiles and runs positive and negative controls. The optional upstream checkout
is compiled only for runtime comparison; allowing its existing warnings during
that compilation is not proof acceptance. `--skip-proof` is explicitly diagnostic
and cannot establish bootstrap acceptance. C# build commands disable NuGet audit.

`mutations.py` changes one repair site at a time. Removing a logical guard must
fail verification; the closure-halt mutation must change its runtime negative
control. Reintroducing unchecked termination must fail the strict diagnostic
check. Source-policy controls also reject reintroduced axiom markers and
unbounded executable-method declarations, which Dafny otherwise permits.
Build/solver failures never count as killed mutations.

The `test/` files retain upstream lit-style comments as corpus data. They are not
registered as new fork integration tests; the controls are standalone and no
expected-verdict inventory or regression manifest is changed.

## Remaining issue 119 work

This snapshot is not yet an opt-in backend. A subsequent PR must implement and
validate the structured worker, fail-closed transport and result decoding,
process lifetime/cancellation, source/obligation identity, and differential
Boogie/B3 coverage on the P0/P1 census. No existing Dafny program uses B3 here.
