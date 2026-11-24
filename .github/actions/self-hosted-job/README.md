# Self-hosted job admission

The action holds a numbered advisory lock for the entire job, including post
cleanup. It checks the owning job's process identity while waiting and holding.
Admitted work keeps its reservation until completion or cancellation.

`slots` caps concurrent jobs (default 2). `min-free-percent` and `max-pressure`
provide optional startup checks in slots-only mode. `kind` is an exact registered
key: workflows pass `ci:${{ github.workflow }}`. An absent or unknown kind uses
the project's largest declared peak, including for unregistered workflow names.
The registered workflow names are `review`, `obligation-proofs`, and
`Certified standard-library core` (`issue211-std.yml`).

## Host-level admission configuration

Configure these environment variables on the runner service, rather than in
workflow variables or repository files:

- `ADMISSION_RUNNERS_JSON`: path to a JSON object with positive integer fields
  `studio_ci_budget_mb` (shared declared-memory budget in MB) and
  `admission_slots` (maximum eligible numbered locks). Optional `admission_memory`
  contains `min_free_percent` (integer 0–100, default 40) and `max_pressure`
  (`normal`, `warn`, or `critical`, default `normal`). These current host settings
  take precedence over the action's memory inputs in budget mode.
- `ADMISSION_PROJECTS_DIR`: directory of JSON registrations. Each has a nonempty
  `name`, a `repos` list of exact repository names such as `example/repository`,
  and `peak_mb`. The peak is a positive integer for all kinds, or a map from
  exact kind keys to positive integers. Underscore-prefixed map entries are
  annotations. The map must contain at least one other entry. Exactly one
  registration must contain `GITHUB_REPOSITORY`. Other sizing fields are ignored.
- `ADMISSION_LOCK_DIR`: optional shared lock directory. By default the action
  keeps using its existing user-cache admission directory. All cooperating
  producers must use the same directory. An empty value is an error.

Both configuration paths must be set to enable budget mode. If neither is set,
the action logs one line and retains slots-only admission. A partial, empty,
unreadable, or invalid configuration fails the step. Configuration, registration,
and lock-directory settings are read on every attempt. The effective slot count
is the smaller of the action input and the current host cap. A request larger
than the full budget fails immediately.

## File protocol and rollout

Budget-aware producers exclusively lock `budget.lock` nonblocking, then one
`active.lock.N`. They count every occupied numbered lock, including indices above
the current cap, and `agent-reserve.lock`. Free locks' metadata is ignored.
An occupied lock with missing, malformed, or dead-PID metadata refuses admission;
it is never assigned an estimated size. Live declarations plus the new request
must fit the shared budget (equality is allowed). Unknown or failed bounded
memory probes refuse admission too.

Before releasing the mutex, admission atomically replaces `<holder-lock>.json`
from a same-directory temporary file. Its object has `pid` (the keeper's positive
PID), `project`, `kind` (or `unknown`), `declared_mb` (positive integer), and
`started` (positive Unix timestamp in seconds). Only after publication can the
ready marker appear. The keeper retains the holder lock until the job ends,
unlinks its sidecar before closing the holder, and never deletes a lock file.
Neither lock descriptor survives an exec. Refused attempts release both locks
before waiting. Slot/mutex waits poll each second; budget/memory waits back off
through 5, 10, 20, 30, and 60 seconds, checking job cancellation each second.

Before enabling budget mode, drain and stop all old holders **and waiters** in
every producer, deploy compatible implementations, and pin workflows to the
reviewed action revision. Old waiters ignore the transaction mutex; the protocol
cannot safely enable itself during a rolling upgrade. Disabling budget mode or
changing the shared lock directory likewise requires a coordinated drain.
Do not change runner counts, slot caps, memory floors, or live service settings
as part of this code handoff. Reservations schedule declared peaks; they do not
limit RSS. Observe actual usage and pressure after deployment and revise sizing
through the owning host configuration process.

Tests run with the normal CI bookkeeping command:
`python3 -m unittest discover -s .github/review -p 'test_*.py'`.
