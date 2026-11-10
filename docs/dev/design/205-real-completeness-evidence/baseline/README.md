# Pristine baseline evidence for #205

These measurements use product source at
`62bae981952337d312cbb3d43c39f9813cca6084`, with no implementation of the proposed
completeness primitive. They establish the unchanged corpus and its costs. They
do **not** validate candidate registration, demand detection, packaging, or
OFF/ON identity. Those remain product implementation gates.

## Results

| Run | Program/verdict rows | Declarations | VC batches | Total reported RU | Expected-verdict differences |
| --- | ---: | ---: | ---: | ---: | ---: |
| Existing ON-suite harness, Z3 5.1.0 | 1,163 programs | 6,922 | 7,634 | 1,555,572,761 | 0 / 1,163 |
| Full Std ON, Z3 4.12.1 | 2,197 rows | 2,190 | 7,724 | 1,018,791,320 | 0 / 2,197 |
| Full Std ON, Z3 5.1.0 | 2,197 rows | 2,190 | 7,724 | 1,185,435,799 | 0 / 2,197 |

The suite contains negative tests. Its declaration outcomes are 5,173 `Correct`,
1,731 `Errors`, and 18 `OutOfResource`; its batch outcomes are 5,878 `Valid`,
1,734 `Invalid`, and 22 `OutOfResource`. Its program exit codes are 546 success,
338 verification failure, 263 parser/resolver failure, and 16 CLI failure.

Std under Z3 4.12.1 has 2,190 `Correct` declarations and 7,724 `Valid` batches.
Under Z3 5.1.0 it has 2,171 `Correct`, 17 `OutOfResource`, and 2 `Errors`
declarations; 7,705 `Valid`, 17 `OutOfResource`, and 2 `Invalid` batches. These
failures are present in the pinned expected verdicts. All six target-specific
parts completed under both solvers, and all their batches were valid. No safety
timeout occurred in any run.

The sum of declaration RU equals the sum of batch RU separately in all three
runs. RU means the resource count reported by the solver, not elapsed time or
quantifier-instantiation count. No machine timing is included in this evidence.

## Coverage and modes

The existing suite harness retains each test's original first `RUN` line. Its
ON flag precedes those options. Eighteen tests explicitly specify
`--additional-axioms=false`; they are listed in `suite-off-overrides.tsv` and
were retained without alteration. Original resource, time, and Boogie options
also retain their existing precedence. “ON suite” describes that exact CI
harness, not a forced option override of every test.

All 1,163 program verdicts are retained. There are 1,121 readable JSON logs,
41 absent logs from early diagnostics, and one empty log. The empty log belongs
to the existing `git-issues/github-issue-2563.dfy` invalid-logger-option fixture,
which reports zero verified obligations. There are no nonempty malformed logs.
The complete list and categories are in `suite-unavailable-logs.tsv`.
Unavailable per-program RU is blank in `suite-programs.csv`, not silently
counted as a measured zero. Every available declaration and VC result is
included, including errors and resource exhaustion.

Both standard-library runs have seven readable JSON logs: the main project and
six target-specific parts. The main part uses
`/deterministicLiteralHashes:1 /restartProver`. Target-specific parts retain the
committed binary's compatible options, as required by the existing harness.

The source census scans all 2,539 `.dfy` files in the full LitTest subtree and
115 `.dfy` files in the standard-library subtree. It finds no
`RealCompleteness` or `real.Sup` occurrence. The suite schedules 1,163 roots from
that larger subtree. This lexical census confirms that the pinned corpus does
not use the proposed API; it is not an execution test of unwritten demand
analysis. Inventory hashes and exact scope appear in
`source-demand-census.json`.

## Files and schemas

`provenance.json` associates all data with the source commit, measured build,
solver binary hashes, flags, harness paths, and portable command templates.
`files.json` gives each data file's size and SHA-256. `summary.json` contains the
aggregate outcomes, resource counts, run summaries, and comparison results.

The three verdict TSVs retain the harness's verdict columns. Suite wall time
and standard-library run wall time have been removed. Standard-library
declaration rows retain RU and batch counts. Existing comparison scripts read
the retained columns without requiring timing fields.

The declaration CSVs contain `program` or `part`, declaration name, outcome,
resource count, and number of batches. The VC CSVs contain `program` or `part`,
declaration name, VC number, outcome, and resource count. These are direct
projections of every `verificationResults` and `vcResults` entry in the original
JSON logs. `suite-programs.csv` also preserves per-program JSON coverage and
logged totals. No source obligations, failed batches, or resource-exhausted
batches were filtered out. Raw JSON timing, assertion payloads, and machine
paths are excluded from the public projection.

## Reproduction

Use a clean checkout at the commit above and build the pinned product with the
repository's build prerequisites. The observed build used .NET SDK 10.0.100,
Release, `-m:1`, `-p:UseSharedCompilation=false`,
`-p:GeneratePackageOnBuild=false`, and
`-p:SourceRevisionId=issue205-design-62bae981`. Its managed binary hashes are
identifiers for this observation, not a requirement that an independent build
reproduce build-path-dependent bytes. No product source was patched.

Supply absolute paths in `ISSUE205_DAFNY`, `ISSUE205_Z3_4121`, and
`ISSUE205_Z3_510`, with the solver versions and SHA-256 values in
`provenance.json`. Set `DOTNET_ROOT` if the chosen Dafny apphost needs it. From
the repository root, use a fresh output directory:

```sh
ISSUE205_BASELINE_OUT="$(pwd)/results/issue205-baseline"
mkdir -p "$ISSUE205_BASELINE_OUT"
python3 .github/review/lit-verdicts.py plan \
  Source/IntegrationTests/TestFiles/LitTests/LitTest \
  "$ISSUE205_BASELINE_OUT/suite-plan.tsv"
python3 .github/review/lit-verdicts.py run \
  "$ISSUE205_BASELINE_OUT/suite-plan.tsv" \
  Source/IntegrationTests/TestFiles/LitTests/LitTest \
  "$ISSUE205_DAFNY" "$ISSUE205_Z3_510" \
  "$ISSUE205_BASELINE_OUT/suite-on.tsv" \
  --jobs 8 --additional-axioms true \
  --measurements "$ISSUE205_BASELINE_OUT/suite-on"
python3 .github/review/std-verdicts.py run \
  Source/DafnyStandardLibraries "$ISSUE205_DAFNY" "$ISSUE205_Z3_4121" \
  "$ISSUE205_BASELINE_OUT/std-4.12.1-on.tsv" --cores 4 \
  --boogie '/deterministicLiteralHashes:1 /restartProver' \
  --additional-axioms true \
  --measurements "$ISSUE205_BASELINE_OUT/std-4.12.1-on"
python3 .github/review/std-verdicts.py run \
  Source/DafnyStandardLibraries "$ISSUE205_DAFNY" "$ISSUE205_Z3_510" \
  "$ISSUE205_BASELINE_OUT/std-5.1.0-on.tsv" --cores 4 \
  --boogie '/deterministicLiteralHashes:1 /restartProver' \
  --additional-axioms true \
  --measurements "$ISSUE205_BASELINE_OUT/std-5.1.0-on"
```

Run `lit-verdicts.py compare` against
`.github/review/expected-verdicts-additional-axioms.tsv`, and
`std-verdicts.py compare` against each corresponding
`.github/review/expected-std-verdicts-z3-<version>-additional-axioms.tsv`.
The expected files were not changed. The supplied comparison reports record
zero differences for each complete run.
