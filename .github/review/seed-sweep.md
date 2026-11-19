# Comparing proof cost over naming seeds

[`seed-sweep.py`](seed-sweep.py) compares two Dafny executables (or
`DafnyDriver.dll` files) on a file, project, or member selection. It uses only
Python's standard library and runs on POSIX systems. It is an opt-in review
tool; it changes no Dafny options, encoding, or CI gate.

The motivation is [issue 438](https://github.com/erniecohen/dafny/issues/438):
an encoding change can renumber later solver symbols, and the cost of one
naming can be very different from the cost of another. A nonzero Boogie
`/randomSeed:N` changes both Boogie's names and Z3's random seeds. Equal seed
numbers in two encodings **do not form paired observations**. This runner
compares the two unpaired distributions. It does not implement normalized-name
counter offsets or claim to isolate renaming from Z3 randomization.

## Stable CLI

From the repository root, with both products already built:

```sh
python3 .github/review/seed-sweep.py \
  --base base/DafnyDriver.dll --candidate candidate/DafnyDriver.dll \
  --solver z3/bin/z3 --target program.dfy --filter-symbol MyLemma \
  --seeds 48 --resource-limit 16000000 --out sweep
```

`--base` and `--candidate` accept executables or DLLs; `--dotnet` selects the
DLL host. Binary paths are resolved relative to the invocation directory.
`--target` is repeatable, and each input can be a `.dfy` file or a
`dfyconfig.toml` project. Targets are passed unchanged to `dafny verify`.
`--filter-symbol` uses Dafny's own member-filter semantics, including the
well-formedness/correctness scopes that selection produces.

For proof changes, set `--base-cwd` and `--candidate-cwd` to separate source
trees and use the same relative target in each. For encoding changes, leave
both working directories at the same source tree. Members are matched by
their full logged names, including their check kind. A renamed/added/deleted
member invalidates the comparison; rerun on an explicitly comparable scope.

Other controls:

- `--seeds N` defaults to 48 consecutive seeds starting at `--start-seed 1`.
  Zero seeds are excluded. Fewer than 48 samples per lane is exploratory:
  statistics are reported, but `better` and `worse` are disabled.
- `--cores` is 1 by default, with choices 1–3. A run is one verifier process;
  processes run sequentially, alternating base/candidate order at each seed.
  Keep core count, limits, batch structure, and shared options fixed when
  comparing encodings. Independent proof edits may change the batch structure;
  the unit being compared is the whole logged member, not matched VC numbers.
- `--resource-limit` is required, in resource units, per assertion batch.
  Dafny's member attributes and project settings retain their normal semantics.
  The runner does not infer or normalize effective member limits. Ensure they
  match across lanes; reducing a limit is not a proof-cost improvement.
- Verification wall-time limits are disabled. `--timeout` is a process safety
  cap (default 600 seconds). A cap hit kills the owned process group and
  invalidates the comparison, rather than recording a resource failure.
- `--arg=TOKEN` adds a shared Dafny argument; `--base-arg=TOKEN` and
  `--candidate-arg=TOKEN` add variant-specific arguments. Repeat once per token,
  including option values. Controls owned by the sweep (seed, solver, limits,
  cores, filter, log, cache) cannot be overridden this way. Use variant-specific
  options deliberately and inspect the manifest before interpreting results.
- `--threshold RU` is repeatable; defaults are 5,000,000 and 20,000,000.
  Counts use strictly greater than the threshold. `--alpha` defaults to 0.05.
- `--out` must name a new directory. Existing output is never overwritten.

Use a fixed Z3 build for both products; the fork's proof-cost comparator is
Z3 5.1.0. The script records the version and file hash, but intentionally does
not download or select a solver for the user. Disable external caches and
complexity iterations in project configuration. Replayed JSON results are
rejected. Do not use solver wrappers whose configuration changes during a run.

## Report and verdict

For each logged member and lane, `summary.json` and `summary.md` report sample
count, successes, exhausted runs, batch outcome counts, and successful member
RU Q25/median/Q75/Q90. Quantiles use linear interpolation at `(n-1)*q` (type 7).
A successful member's cost is the sum of its batches' resource counts for
that one run. There are no cross-seed sums or geometric means.

Resource-exhausted runs are right-censored: their measured consumed RU is
retained in the raw sample but excluded from completed-cost quantiles and
threshold counts. Exhaustions are listed separately, even if a threshold is
above the exhausted run's limit. This prevents a fast failure from looking
like a cheap successful proof. In the rank test, every exhaustion ranks after
every success, and all exhaustions tie. The endpoint is therefore
**within-budget verification performance**, ordered first by completion and
then by completed member RU; it is not an estimate of uncapped proof cost.

The rank test is the unpaired, two-sided Mann–Whitney U test with average
ranks for ties, the standard tie-corrected normal approximation, and a 0.5
continuity correction. All-equal samples have p=1. The reported effect is
`P(candidate worse) + 0.5*P(tie)` over all cross-lane pairs. Above 0.5 favors
base; below 0.5 favors candidate. A Holm correction controls the family of
tests across **all** selected members. A significant result at `--alpha`
produces `better` or `worse` according to that effect. Otherwise the verdict
is `indistinguishable`, meaning no detected shift, **not proven equivalence**.
The overall verdict is `worse` if any member is worse, else `better` if any
is better, else `indistinguishable`. Improvements never cancel regressions.

The test is an empirical screening method, not a soundness check or proof of
causality. Reusing seeds across lanes does not create a paired test, but the
normal approximation still assumes representative, sufficiently independent
samples. Near a decision boundary, collect more seeds and inspect success
counts, quartiles, and tails. Rank superiority can coexist with a worse tail
or a lower success fraction; the full report, not the verdict alone, must
support an engineering decision. Fix the sample count and scope before running;
repeatedly extending until significance is not covered by the reported p-value.

Exit statuses are 0 for a complete better/indistinguishable comparison,
1 for a complete worse comparison, and 2 for an invalid comparison or bad CLI.
An invalid comparison has no directional verdict. Missing/empty/malformed
logs, duplicate members/VC numbers, changing member populations, unexpected
exit codes, counterexamples, solver timeouts/memory failures/unknowns, cache
replays, and changed captured inputs invalidate it. Expected resource failures
are data only when the process exits 4 and the log agrees.

## Evidence and rerunning

`manifest.json` records options, working directories, binary/solver versions,
and hashes. Each `base/SEED` or `candidate/SEED` directory retains `command.json`,
`output.txt`, `exit.json`, Dafny's `results.json`, and the parsed `sample.json`.
`summary.json` includes every parsed run, allowing an independent recomputation.
No partial population is silently intersected or dropped. On a bad run the
sweep stops; earlier samples remain available without a valid verdict.

Managed product identity includes the entry point and adjacent DLL, JSON, BPL,
and DOO files. Source identity includes Git-tracked and nonignored untracked
Dafny/project/DOO inputs (or all such files below a non-Git working directory),
explicit targets, and their relative Dafny includes. These are hashed before
and after execution. Run in a clean source tree: ignored project
glob inputs in Git trees, external libraries passed as arguments, wrapper dependencies,
the .NET runtime, and transient edits restored between the two captures are
outside that identity boundary. Record those dependencies separately if used.
Archive complete product distributions and source revisions with the results.
Reports contain local commands and paths; review them before publication.

## The issue-425 / PR-430 example

[`fixtures/seed-sweep/issue-425.dfy`](fixtures/seed-sweep/issue-425.dfy) is the
public [issue-425 reproducer](https://github.com/erniecohen/dafny/issues/425#issuecomment-6915818313).
To compare canonical and legacy map payloads using one product:

```sh
python3 .github/review/seed-sweep.py \
  --base binary/DafnyDriver.dll --candidate binary/DafnyDriver.dll \
  --solver z3/bin/z3 --target .github/review/fixtures/seed-sweep/issue-425.dfy \
  --filter-symbol NoWidePreserved --seeds 48 --cores 2 \
  --resource-limit 64000000 --arg=--boogie \
  --arg='/normalizeDeclarationOrder:0 /structuralLiterals' \
  --arg=--heap-free-lambda-facts --arg=--general-traits --arg=datatype \
  --arg=--enforce-determinism \
  --candidate-arg=--legacy-noncanonical-map-payloads --out map-sweep
```

For [PR 430](https://github.com/erniecohen/dafny/pull/430), instead use products
with the original Boogie `3.5.5-review.dcd726f6` and the proposed
`3.5.5-review.5d9897bc`, omitting the legacy argument. That PR was closed and its
Boogie change reverted: neither is a recommended product pin. The subsequent
[findings](https://github.com/erniecohen/dafny/issues/425#issuecomment-6918718703)
explain why favorable single seeds and total RU did not establish a cost win.
The sweep is meant to expose that distinction, not to reproduce an exact
historical resource count on a different product or solver build.

For [issue-437 hardening](https://github.com/erniecohen/dafny/issues/437), select
the same standard-library project and member in two source working directories.
Preserve the project's intended per-member limits and other proof options.
Inspect the success fractions as well as the rank verdict: `indistinguishable`
when both variants often exhaust is not evidence that a proof is robust.

Run the tool's controls with:

```sh
cd .github/review
python3 -m unittest test_seed_sweep
python3 -m unittest discover -p 'test_*.py'
```
