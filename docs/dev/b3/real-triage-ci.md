# Focused Real diagnostic CI

The `real-triage` choice in `review.yml` is available only with the scratch
compilation job. Its outer coordinator refuses a non-scratch ref, a non-dispatch
event, or a simultaneous full bootstrap request. Existing workflow job guards
and the default `none` focus remain unchanged. A zero job exit delivers a
diagnostic artifact, never a full acceptance result.

The reviewed inner source seal is
`941ef16e0f1e9d4d79120f8ff77b45c42a5475365823dbe63f16b09bfb02feb3`.
All eight sealed source files and their byte lengths must match before the
coordinator imports its helpers. They are checked again after the build and
diagnostic stages. The source seal's `csharpCompiled=false` and
`proofOrWorkerExecuted=false` describe that source checkpoint; later runtime
evidence belongs to the separate execution receipt.

The prerequisite is the fixed public
[run 37221479537](https://github.com/erniecohen/dafny/actions/runs/37221479537),
source `ee32faedf6968ffef9baf5e3ab18caa91180a670`, artifact
`b3-native-compile` ID `11311016429`. The coordinator captures the current public
run and artifact metadata, requires those exact associations, downloads the
fixed artifact endpoint to a new archive, and checks the archive SHA-256
`76be6dadc245884a15c52b0b94d6eef8f16c96caed2d12ed23b3956c91e617fe`
and 145,457,636-byte size before extraction. Extraction is bounded, rejects
duplicate or escaping paths and links, and captures only the needed prerequisite
compiler, worker, library, solver and proof-receipt files in a fresh directory.
The receipt records the selected inventory's actual hashes and byte lengths.

The original solver is extracted from that hash-checked public artifact, with
file SHA-256
`b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23`.
The inner coordinator checks its exact Z3 5.1.0 version. It validates the fixed
prior summary, bootstrap log, 560 Passed CSV rows and library bytes; all 58
current vendor files, the patch, upstream revision and three source consumers
must still match the a6 source fingerprint. These independently passed library
obligations are reused, without repeating their proof. The original full gate
remains `passed=false`.

The outer coordinator records the actual fresh checkout HEAD, expected compiler
version `4.11.0+HEAD`, its own/workflow source hashes, the product ledger and the
resolved .NET executable hash. The inner coordinator builds the current compiler
without overriding SourceRevisionId, checks the actual CLI version and loaded
Core version/bytes, emits actual typed BPL/normalized requests/obligation ledgers,
and runs the unchanged 200,000-resource/20-second original fixtures. The two
additional limits are exactly 1,000,000 and 10,000,000, labeled exploratory;
no other source, condition, option, check order or query change is made. The
unchanged current 46-control corpus and Java compilation are separate focused
stages. Their receipts cannot waive any original warning or required Failed
negative outcome.

Both coordinators require an initially empty dedicated Linux child-subreaper
scope and use the reviewed pidfd adopted-child cleanup helper. Every stage has
an output byte bound, wall-clock safety deadline and empty-child boundary. The
outer coordinator also has a 40-minute safety deadline below the unchanged
45-minute workflow job cap. A timeout, forced cleanup or leftover child keeps
that stage failed; a failed drain prevents later stages. The wrapper removes the
GitHub token environment variables before invoking the inner build/proof
coordinator. These are process-tree ownership controls; no cgroup containment,
escaped-runtime or signed provenance attestation is claimed.

Every outer command, actual process exit, cleanup result and log/captured-output
hash is recorded, with the inner strict receipt embedded and hash-bound. The
artifact upload retains its existing `if: always()` guard. The outer `passed`
field deliberately remains false: inspect the original strict booleans and
separately labeled exploratory results. Current-corpus or Java success is
reported only for those focused stages. This source checkpoint has not been
built or executed.
