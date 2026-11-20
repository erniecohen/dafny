# Self-hosted Actions policy

Every executable job in `.github/workflows/` selects the literal labels
`[self-hosted, macOS, ARM64, fork-ci]`. There is no hosted fallback or dynamic
runner expression. `check-self-hosted-runners.py` rejects selectors outside this
policy and external reusable workflows; actionlint validates the workflow YAML.
The routing tests retain the same-repository event restrictions. Draft and fork
pull requests do not execute code on these runners.

Each workflow cancels superseded branch/PR runs. Tag and maintenance operations
retain their existing serialization rules. Matrix jobs admit one variation at a
time. A repository action pinned by commit additionally holds numbered OS advisory locks
across runner services. The repository variable `SELF_HOSTED_JOB_SLOTS` sets the
number of admitted jobs, defaulting to **2** when unset; excess waiters are not
discarded. Configure the same capacity in every participating repository. The keeper checks the job worker's process identity and releases
its slot if the worker disappears. Its post action cleans checkout outputs and
job temporary files before stopping the keeper. An unconditional shell cleanup
also removes temporary files if checkout or admission fails. The action is pinned
by commit so that a historical checkout cannot replace its post script. An action
update must also update the workflow references.

After admission and before the first checkout, the pinned action clears this job's
repository workspace, including all Git metadata and stale output. It verifies
that the workspace is an immediate child of `RUNNER_WORKSPACE`, and never follows
workspace symlinks into another directory. Cached actions, toolchains and job
scratch directories are outside this cleanup. The checkout starts from fresh
metadata even after a prior job failed during fetch.

All checkouts use the default one-commit depth or explicitly set depth 1. The shared
`fetch-refs` composite fetches only supplied commit IDs or fully qualified refs into
numbered private refs, with no tag following or configured remote refmap. The two
milestone jobs fetch complete ancestry of the PR base and head for `merge-base`;
`--unshallow` is used only while the repository is shallow, so a completed fetch
can be retried safely. Baseline build jobs fetch only their pinned commit at depth
1. No all-branch or all-tag fetch is needed; versioning retains its explicit
single-tag fetch. Case-colliding unrelated remote branches are never imported.

The job timeout includes admission waiting; shell commands retain the former job
execution timeout as a per-step safety cap. Worker niceness, .NET/Gradle daemon
settings and the cached .NET 10 SDK plus .NET 8 runtime follow the existing
self-hosted setup. Frozen comparison SDK/runtime versions stay pinned. Ordinary
verifier/corpus drivers use two workers, lit/xunit uses the existing two-thread
settings, and release/certified-library jobs retain their three-core cap.

Capacity planning retains a 12 GiB allowance per active build/verifier job and
2 GiB for runner services and waiting action processes. The default two slots
therefore allow **2 × 12 + 2 = 26 GiB** of concurrent Actions memory; a configured
capacity of N slots allows 12N + 2 GiB.
This is a conservative planning allowance, not an enforced RSS limit or a measured
peak. Jobs from older workflow revisions do not participate in this admission
protocol.

## Unavailable Linux coverage

- `review.yml/regressions-linux` is explicitly skipped. Its Linux resource-output
  expectations and native dependency-closure checks cannot be tested on macOS.
  The macOS regression job remains required. The unavailable job is excluded from
  the strict required-job list so that a macOS release is still possible.
- `review.yml/parity-linux` is explicitly skipped. The reusable parity workflow
  accepts only the macOS leg. The probe report still records missing Linux
  evidence; it cannot establish cross-platform parity.
- The release job keeps both `dafny-<version>-osx-arm64.zip` and
  `dafny-<version>-linux-x64.zip`. It checks the native product version, verification,
  persistent cache and checked-catalog positive/negative controls, then cross-publishes
  Linux x64 from the same commit with the same required checked catalog and assembly
  identity. The Linux zip is inspected for required self-contained runtime files,
  an ELF64 x86-64 apphost, complete embedded checked bundles and distribution hygiene.
  Both archives appear in `SHA256SUMS` and the release-notes sha256 block. Linux
  execution is qualified downstream, not here; no Linux executable is run on macOS.
- Former hosted fallback jobs are removed. Untrusted fork PRs have no runner
  fallback.

Solver versions, expected suite/library verdict files and the product version
base are unchanged. ARM64 solver archives retain their published checksum pins.
The module-window recorder uses `wait4` instead of GNU `time`; it preserves child
exit status and converts macOS RSS bytes to the existing KiB report format.
