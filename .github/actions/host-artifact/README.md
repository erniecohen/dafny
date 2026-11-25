# Host artifacts

`host-artifact` transfers a single file or directory between jobs in the same
workflow run. Use it after checkout, with `mode: put` or `mode: get`, `name`, and
`path`. For a put, the path must exist; patterns and multiple paths are not
supported. A directory contributes its contents; a file contributes its basename.
For a get, the path is the destination directory (default `.`).

Set **`HOST_ARTIFACT_DIR`** in the runner service environment to the same absolute,
persistent, writable directory for every runner participating in these handoffs.
The runners must share a filesystem with working POSIX advisory locks and atomic
rename. Python 3 and `tar` must be available. Set `HOST_ARTIFACT_DIR` on every
runner in the same apply. A mixed pool fails a consumer with "missing" when its
producer used GitHub and its reader expects host storage; there is no fallback
for a missing host artifact. Drain participating jobs before the coordinated apply.
This action does not change runner configuration.

The archive key is
`HOST_ARTIFACT_DIR/<owner>/<repository>/<run_id>/<run_attempt>/<name>.tar`.
Names contain letters, digits, underscores, dots and hyphens, beginning with a
letter, digit or underscore. A get searches the current attempt and then earlier
attempts, newest first, independently for each name. It never reads another run
or a future attempt. Missing names fail with an explicit error.

Puts pack to a temporary file alongside the destination and publish by atomic
rename with archive permissions `0644`; an unsuccessful pack leaves any previous
archive intact. Each repository has a permanent `.lock` file. Gets hold its shared
advisory lock throughout extraction. Packing runs without a lock; put publication,
retention recording and pruning take its exclusive lock. Each put records the
largest requested `retention-days` for the run in `.retention-days`, across names
and attempts, and refreshes the run directory's modification time. Host retention
defaults to three days when the input is omitted. A put deletes other numeric run
directories only after their own recorded retention has elapsed since their last
put (three days for older unrecorded runs). It leaves the current run and other repositories
alone. Concurrent readers cannot observe a partially deleted run. A reader that
starts after an expired run has been removed fails normally. Long-running or old
reruns may therefore require rebuilding their expired inputs.
Pruning failures log a warning after publication and do not fail the successful put.

With `HOST_ARTIFACT_DIR` unset or empty, the shell entry point selects the GitHub
backend and the composite uses `actions/upload-artifact@v4` (missing input is an
error) or `actions/download-artifact@v4`. Its optional `retention-days` is forwarded
unchanged; omitting it uses the repository default on GitHub. `include-hidden-files`
defaults to false and is forwarded to both backends, matching the previous v4
upload steps; explicit true includes hidden files. The log identifies
the backend. Local archives preserve executable modes; GitHub's fallback has the
standard artifact permission behavior. Keep permission-sensitive builds in their
existing tar bundles. Host storage is a trusted runner cache, not an isolation
boundary between untrusted repository code.

Only internal build handoffs use this action. GitHub still retains reports, logs,
verdicts, release binaries, cross-workflow build bundles and tree-reuse markers.
The main build and the issue-74 build publish both their existing GitHub artifact
and a host copy when configured, because other workflows consume those builds.
Their backend selector runs in a shell so it sees inherited runner environment
variables as well as workflow-defined variables.

Run the shell entry point's local controls without Actions:

```sh
python3 .github/review/test_host_artifact.py
python3 .github/review/mutate-host-artifact.py
```

The fallback controls execute backend selection and check the composite's exact
upload/download guards and input forwarding. They do not contact the Actions
artifact service. The race control uses two processes with explicit rendezvous
points around extraction and lock acquisition.
