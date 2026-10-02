# Resolver matrix

`resolver-matrix.py` runs each case in `resolver-cases.json` twice: once with the
legacy resolver and once with the refreshed resolver. The manifest supplies
source paths, options, and per-process deadlines explicitly. `refresh_options`
adds options that require the refreshed resolver without preventing legacy
resolution. It does not parse,
replace, or claim to execute Lit `RUN` lines.

The matrix complements two existing gates:

- `lit-verdicts.py` and its expected verdict files remain unchanged.
- The regression job uses the upstream Lit harness and executes all `RUN` lines
  in every registered test. In particular, the issue 38 and 39 negative controls
  still run verification with a real solver and check their diagnostics.

The initial manifest covers resolver-only source programs in `dafny0` through
`dafny4` and `git-issues`, fixed resolver crash and diagnostic regressions, and
additional option variants from later `RUN` lines. Print/serialization pipelines,
compiler actions, and tests of command-line argument precedence remain the Lit
harness's responsibility. This is an explicit, bounded selection, not a claim
that every resolver path or every upstream `RUN` line is covered.

These raw `resolve` commands intentionally use ordinary language defaults unless
an option is explicit in the manifest. They do not inherit `%resolve`'s
`--general-traits=datatype` and `--general-newtypes` defaults: the latter requires
the refreshed resolver. The issue 46 case enables it only in refreshed mode;
the legacy row checks the diagnostic for unsupported bitvector newtypes. The
plain bitvector-literal range control needs neither feature flag. Warning cases
explicitly allow warnings, and both modes retain their complete diagnostics.

## Results

Every case has a stable ID, and each execution records its resolver mode, exit
code, full stdout and stderr, and one outcome: `accepted`, `rejected`, `crash`,
`timeout`, or `unexpected-exit`. Only exit 2 with an ordinary parse/resolution
diagnostic counts as rejection. An internal exception never counts as an
ordinary rejection. Full diagnostic text is compared, including source lines,
with only CRLF and the absolute checkout prefix normalized.

Each process has a wall-clock safety deadline (60 seconds by default, maximum
300). On timeout, the runner kills its process group, including descendants,
and retains partial output. The matrix does no verification and does not use
wall-clock timing as a proof-cost measurement. Each run uploads the execution
commands, raw output, normalized results, differences, and summary.

The strict job fails on any changed/missing/added result and always fails on a
crash, timeout, or unexpected exit, even if such a result is in the baseline.
There is no automatically accepted failure list.

## Establishing or changing the baseline

1. Dispatch `review.yml` on a `scratch/` branch with `resolver_probe=true`.
   The resolver step records all outcomes and differences and exits zero. Its
   summary explicitly says that it is a probe, not a passing gate. The other
   workflow gates remain strict.
2. Inspect every changed result in the `resolver-results` artifact and record
   why it is expected. Do not adopt unexplained changes or crashes/timeouts.
3. Commit `resolver-results/actual.json` as
   `.github/review/expected-resolver-results.json` in a separate commit whose
   message records the reason for the new or changed outcomes.
4. Dispatch with the default `resolver_probe=false` and require the strict run
   to pass before updating the PR.

`resolver-probes.json` contains the already reported inferred-newtype cycle from
issue 44. It is intentionally separate from the strict manifest until repaired;
probe dispatches capture both resolver modes with a 15-second deadline. Its
record cannot turn a timeout into a passing strict result. Move the case into
the strict manifest with a reviewed diagnostic expectation when fixed.

Infrastructure unit tests run stand-in child processes to check output-pipe
handling, process-tree deadlines, outcome classification, baseline comparison,
mode coverage, and probe/strict behavior. They do not build or run Dafny.
