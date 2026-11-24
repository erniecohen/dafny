# Self-hosted job admission

The action holds a numbered advisory lock for the entire job, including post
cleanup. It checks the owning job's process identity while waiting and holding.
Admitted work keeps its reservation until completion or cancellation.

## Required policy

Every workflow passes these inputs from the corresponding repository variables,
without expression defaults. Missing or invalid policy fails at startup.
All memory quantities are MiB (1024 squared bytes).

| Input | Repository variable |
| --- | --- |
| `slots` | `SELF_HOSTED_JOB_SLOTS` |
| `reserve-mb` | `SELF_HOSTED_JOB_RESERVE_MB` |
| `default-peak-mb` | `SELF_HOSTED_JOB_DEFAULT_PEAK_MB` |
| `peak-min-samples` | `SELF_HOSTED_JOB_PEAK_MIN_SAMPLES` |
| `peak-history-days` | `SELF_HOSTED_JOB_PEAK_HISTORY_DAYS` |
| `probe-timeout-s` | `SELF_HOSTED_JOB_PROBE_TIMEOUT_S` |
| `slot-poll-s` | `SELF_HOSTED_JOB_SLOT_POLL_S` |
| `memory-backoff-s` | `SELF_HOSTED_JOB_MEMORY_BACKOFF_S` |

Sizes and times must be positive finite numbers. Slots and the minimum sample
count must be positive integers. Backoff is a nonempty JSON array of positive
finite seconds. `expected-peak-mb` is an optional positive finite caller peak.
`kind` is an exact registered key; workflows pass `ci:${{ github.workflow }}`,
matching the completed-history sampler's workflow convention. For workflows
with substantially different job sizes, declare the heavier job's expected peak
or coordinate a matching sampler kind change rather than silently pooling kinds.

## Peak selection and headroom

Configure these environment variables on the runner service:

- `ADMISSION_RSS_HISTORY`: full path to the local `rss.jsonl` file. Daily rotations
  alongside it, named `rss.jsonl.YYYYMMDD`, are also read. An unset, missing or
  unreadable history uses the required `default-peak-mb`; no home path is assumed.
- `ADMISSION_PROJECT`: registered project name for history matching. If absent,
  use the budget registration's project name, or the repository name when there
  is no host budget configuration.

A caller peak overrides history. Otherwise use completed jobs in the declared
lookback window, deduplicated by job key, with positive finite sampled peaks:
first matching project and kind, then matching kind across projects. Each group
needs `peak-min-samples` completed jobs. A known kind never falls back to the
project's mixed-kind distribution. With no kind, project-only history is eligible.
The p95 is the sorted value at index `round(0.95 * (n - 1))`, using Python's
round-to-even rule. Insufficient history uses `default-peak-mb`. The selected
peak, source, sample count, project and kind are logged. Sampled peaks are lower
bounds and p95 excludes the upper tail; neither is an upper-bound guarantee.

After acquiring a slot, bounded local probes read `memory_pressure -Q`,
`sysctl -n hw.memsize`, and `sysctl -n kern.memorystatus_vm_pressure_level`.
Admission requires known normal or warn pressure (1 or 2), and:

```
available_mb = free_percent / 100 * physical_bytes / 1048576
outstanding_j = max(0, max(declared_mb_j, expected_mb_j) - current_tree_rss_mb_j)
available_mb - expected_peak_mb - sum(outstanding_j) >= reserve_mb
```

Equality admits; critical (4), unknown pressure and unreadable probes refuse.
Free memory already accounts for current RSS. Charging only outstanding growth
avoids counting that RSS twice, while preventing several newly admitted jobs
from spending the same apparent headroom before they allocate their peaks.
The holder PID identifies its process tree; for CI, its `Runner.Worker` ancestor
is the root, since job steps can be siblings of the keeper. An unreadable tree
charges its full peak. Holders without `expected_mb` use their declaration.
This rule does not limit RSS or guarantee against unsampled spikes, reparented
processes, unmanaged work or background system growth.

## Host-level declared budget

These existing runner environment variables retain the shared budget protocol:

- `ADMISSION_RUNNERS_JSON`: path to a JSON object with positive integer fields
  `studio_ci_budget_mb` (shared declared-memory budget) and `admission_slots`
  (maximum eligible numbered locks). Memory policy comes from the required
  action inputs; the old percentage-floor configuration is no longer used.
- `ADMISSION_PROJECTS_DIR`: directory of JSON registrations, each with a nonempty
  `name`, exact repository membership in `repos`, and `peak_mb`. Peaks are a
  positive integer or a nonempty map of exact kind keys to positive integers;
  underscore-prefixed entries are annotations. Exactly one registration must
  match `GITHUB_REPOSITORY`. Unknown kinds use the project's largest declaration.
- `ADMISSION_LOCK_DIR`: optional shared lock directory, retaining the existing
  user-cache default. All cooperating producers must use the same directory.
  An empty value is an error.

Both budget configuration paths must be set together. Partial, empty, unreadable
or invalid configuration fails. With neither present, headroom and growth still
apply, with the expected peak recorded as the declaration rounded up to MiB.
There is no slots-only fallback. Configuration and registrations are read on
every attempt; slots are clamped to the current host cap. Requests larger than
the entire declared budget fail immediately. Live declarations plus the incoming
registered declaration must fit the budget independently of the headroom rule.

## File protocol and rollout

Every attempt exclusively locks `budget.lock` nonblocking, then one
`active.lock.N`. CI never takes `agent-reserve.lock`. Accounting includes every
occupied numbered lock, including old high indices, and the agent reserve.
Free locks' sidecars are ignored. Occupied locks with missing, malformed or
dead-PID metadata refuse admission; their size is never guessed.

Before releasing the mutex, admission atomically replaces `<holder-lock>.json`
from a same-directory temporary file. Its object contains `pid` (the keeper's
positive PID), `project`, `kind`, `declared_mb` (positive integer), `expected_mb`
(positive finite number), and `started` (positive Unix timestamp in seconds).
Only after publication may the ready marker appear. The holder lock remains
held until job completion; metadata is unlinked before unlocking. Lock files
are never removed, and neither lock descriptor survives exec.

Refused attempts release both locks before waiting. Slot/mutex waits use
`slot-poll-s` and reset memory backoff. Budget, memory and unaccounted-holder waits
use `memory-backoff-s`, capped at its last element. Waits check cancellation
and owner identity at least each second and remain within the job timeout.
Reasons are logged when they change; memory diagnostics include available,
expected, outstanding, reserve and pressure. Admitted jobs are never stopped
because a later sample or configuration changes.

This replaces `min-free-percent` / `max-pressure` and the corresponding
`SELF_HOSTED_JOB_MIN_FREE_PERCENT` / `SELF_HOSTED_JOB_MAX_PRESSURE` variables.
Coordinate rollout: drain and stop old holders **and waiters** across producers,
set all required repository variables and runner environment paths, deploy
compatible producers, and pin workflows to the reviewed action commit. Old
queued runs retain old pinned actions; new variables alone cannot upgrade them.
Changing the shared lock directory also needs a coordinated drain. This change
does not apply settings, alter slot capacity, restart services or deploy policy.

Offline tests use the normal bookkeeping command:
`python3 -m unittest discover -s .github/review -p 'test_*.py'`.
Serial negative controls run with `python3 .github/review/admission-mutations.py`.
