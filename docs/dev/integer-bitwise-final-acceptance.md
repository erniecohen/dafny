# Integer-newtype bitwise final acceptance plan

This is a plan, not an integrated acceptance result. Execute it only after the
issue [193](https://github.com/erniecohen/dafny/issues/193) reference, checked
bridges, source APIs and runtime stack have merged into `review/4.11.0`.
The runtime series includes [C#](https://github.com/erniecohen/dafny/pull/621),
[source rotations and queries](https://github.com/erniecohen/dafny/pull/623),
[population count](https://github.com/erniecohen/dafny/pull/624),
[Python](https://github.com/erniecohen/dafny/pull/625),
[Java](https://github.com/erniecohen/dafny/pull/626),
[Go](https://github.com/erniecohen/dafny/pull/627),
[C++](https://github.com/erniecohen/dafny/pull/628) and
[JavaScript](https://github.com/erniecohen/dafny/pull/629). Rust completes the
backend matrix. The checked native bridge gates include
[shifts and addition](https://github.com/erniecohen/dafny/pull/610),
[subtraction](https://github.com/erniecohen/dafny/pull/615) and
[rotations](https://github.com/erniecohen/dafny/pull/622). Each intermediate PR retains its focused qualification evidence;
none substitutes for checking the final merged revision.

## Freeze the inputs

Record the final merge commit, every optional proof module's blob, the compiler
and runtime source identities, the pinned Boogie packages and Z3 version. Use the
fork's supported solver configuration; do not enable `smt.bv.solver=2`.
Record the exact command lines and options with the retained acceptance results.
Confirm all dependencies have merged and every runtime fixture is registered in
`regressions.d`, with both expected-verdict tables covering verify-first tests.
Do not update expected verdicts to conceal a regression.

## Proof and control matrix

Run each reference, arithmetic/Boolean/shift/rotation bridge, source API and
runtime script gate in both resolver modes and with additional axioms off/on.
The theorem checklist includes the sequence/value bijections, Boolean operations
and ANDNOT, shifts, rotations, bit access, population count, equality and order,
modular addition/subtraction/multiplication, unsigned division/remainder, and
signed observations. Collect per-VC resource counts for each native acceptance
theorem at widths 8, 16, 32 and 64, separating checked bridge costs from client
costs. Assumed bridge projections are not proof results.
Require every positive obligation to prove, and every designated wrong-result,
invalid-count, strict-predicate and triggered `assert false` control to fail at
its exact expected source location. Resource exhaustion, timeout, warnings and
inconclusive results do not count as positive or negative controls.
Inspect the assumption-audit reports for the complete optional-module dependency
closures; require no unchecked proof dependencies. Check the compiler-owned
identity and shadowing controls, `.doo` enabled/disabled boundaries, opacity and
source evaluation/aliasing controls. Retain the already-qualified serial mutation
results; rerun any mutation whose site changes during integration, with a clean
baseline and bounded timeout that terminates only its own process group.

## Runtime matrix

Run the full arithmetic-digit oracle checks for C#, Python, Java, Go, C++,
JavaScript and Rust. Require all ten operations, native and nonnative storage
where supported, semantic width distinct from carrier width, signed/unsigned
carrier boundaries, zero/full-width counts, strict predicates, and output aliases.
Include JavaScript's Number/BigInt precision boundary and Rust's native unsigned
128-bit boundary. Run Rust with its existing `--enforce-determinism` requirement.
For C++, run its documented native-width subset and require clean diagnostics for
unsupported width/storage/count combinations; do not count rejected cases as
runtime coverage. Exercise the JavaScript launcher backpressure control as well.

## Identity and persistent-cache acceptance

Compare the final build's option-off behavior to a source-pinned, change-isolated
baseline on the existing suite and standard library. Keep unrelated merged fixes
and dependencies in that baseline; account separately for unrelated input and
expected-verdict changes. Historical pre-feature results alone do not establish
final identity. Check verdicts and each VC's
resource count, with byte-identical normalized Boogie where source identity
permits it. Compare generated code for existing bitvector clients on all seven
backends. Separate intentional opt-in source additions from default behavior.

For representative Boolean/shift, rotation/bit-query and population-count
consumers, compare fresh, cold, warm and `--resolution-cache-check` runs, with
additional axioms both off and on. Require identical obligations, per-VC verdicts
and resource counts, generated code and runtime output. Inspect pipeline profiles
to prove that the consumer actually hits the warm cache without resolving and
that the audit path checks it against a fresh resolution. Include stale/corrupt
snapshot fallback controls from the cache regression suite. An unnoticed miss
does not satisfy warm acceptance.

## Integration and closure

Build with the repository's pinned dependencies and run the registered issue-193
lit tests in both ordinary subprocess and in-process harness environments, then
the final merged revision's normal regression, verdict and standard-library CI
jobs. Run the bookkeeping unit tests and version check. Keep concurrency within
the agreed limit and run memory-heavy steps under resource admission, including
analysis scripts. Shut down build servers inside their admitted step.

Close issue 193 only after the full matrix has recorded exact source/toolchain
identities, passed controls, conclusive verdict/RU comparisons and green merged
CI. Link the public acceptance summary from the issue and list the remaining
intentional backend restrictions. Performance measurements and local execution
details belong in private evidence, not this document or GitHub text.
