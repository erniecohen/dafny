# Repeated default comparison

The manual `review.yml` input `default_comparison=true` runs one diagnostic
comparison job on a `scratch/` branch. Only that combination skips the ordinary
build, regressions and resolver-test root jobs and their dependent jobs. The
input cannot skip any of those jobs on `review/**`, `dev`, tags or pull requests.
An ordinary review run remains necessary for the final test/harness revision.

The job builds two frozen products once each:

- A: `4e98f3f592310854ca1448d517b82dcbcc0f51f4`, the original development baseline.
- B: `b85e4b6f3af7eedd39bf355394a3f50c1b348646`, the batch-verification product.

The optional `default_comparison_mode=baseline-control` keeps A fixed and binds
B to the same unmodified development revision as A. Its default, `candidate`,
retains the A/B pairing above. The mode alone cannot enable the comparison or
skip any ordinary gate: manual scratch dispatch and `default_comparison=true`
are still required.

The workflow checkout supplies the harness and records its own revision. Its
production reference remains `b85e4b6f`, separately checked in both modes. In a
baseline control the harness differs from the measured development products;
this difference is recorded explicitly. Test/harness repairs never replace a
measured product. Both products build with the same explicit version stamp,
exact SDK/runtime and checked dependencies. The same two built outputs are reused
for A1, B1, B2, A2.
Build files, restore inputs, library/suite inputs, solver digests, environment and
reported versions are retained. These are development comparison artifacts,
separate from ordinary version-stamped release binaries.

The same isolated installation contains only SDK 10.0.402 and the required
.NETCore runtimes 10.0.13 and 8.0.32. The source generator requires .NET 8. Its
runtime archive is checksum-pinned and extracted without replacing the .NET 10
host. SDK/runtime versions, locations and every installed file hash are captured
before either build and checked again before verification. No external runtime
or different product binary supplies the missing dependency.

Each of three OFF scopes runs the full four-pass sequence:

- The original 1,183-program suite with Z3 5.1.0, jobs 4, one complete shard.
- The standard library's seven native parts with Z3 5.1.0.
- The same seven parts with the tree-test solver Z3 4.16.0.

The unchanged native runners preserve program/library options, declaration order,
resource limits and safety caps. Suite proof options remain cores 1 and the
runner's existing resource ceiling unless the original program supplies them.
Source-library parts keep cores 4 and their native project limits. No seed,
ordering flag, proof limit or expected verdict is adjusted for this comparison.
Only separately recorded executable/solver/observation locations are abstracted
in command comparisons. Actual command arrays and working directories survive.

`default-comparison-suite-inventory.json` binds the original public OFF artifacts,
ordered plan, exact scope/VC/assertion identities and the intended diagnostic or
zero-proof cases. There are 785 proof-bearing programs, 398 explicit nonproof
cases, 7,166 declarations and 8,208 VCs. Diagnostic cases retain exact exit,
summary, error lines and diagnostic-line digests, including intentional usage
errors. An empty log from an intended positive program is incomplete.

`default-comparison-inventory.json` binds the original source-library artifacts'
exact seven parts, declaration names and per-declaration VC counts. Those old
artifacts did not retain individual VC logs. The complete controlled A1 therefore
also fixes actual VC numbers, seeds and assertion identities for all later
source-library passes. Missing or substituted identities are incomplete; no
intersection of the observed populations supplies an intended inventory.

Every pass keeps process output/exits, invocation metadata and raw JSON logs.
Any missing program, wrong preprocessing exit, duplicate identity, truncated
inventory or inconsistent summary makes that pass incomplete. Later passes are
still attempted and failures survive. A scope with an incomplete pass has an
`incomplete-<scope>.json`, not an ordinary comparison report. If any scope is
incomplete there is no top-level `summary.json`; inspect `incomplete.json` and
`comparison-status.json`. A complete report lists same-binary repetitions before
all cross-build comparisons, with exact denominators and every changed verdict
and resource entry. `raw-manifest.json` binds retained files.

The wrapper uploads evidence and records the probe exit even for expected proof
failures, incomplete measurements and vector differences. A successful workflow
status is not a preservation verdict. Differences never update expected files,
waive the default verdict/resource contract or authorize integration or release.
Inspect all three scopes' actual populations and comparisons before drawing a
conclusion.

The mocked tests (`test_default*comparison.py`) check bookkeeping and negative
controls without invoking a verifier. They do not qualify any product build.
