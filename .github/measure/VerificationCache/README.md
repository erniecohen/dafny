# Persistent verification-task cache measurements

Build the CLI, select the pinned Z3 executable, and run:

```sh
python3 .github/measure/VerificationCache/run.py \
  --dafny Binaries/Dafny.dll --solver "$Z3" \
  --library Source/DafnyStandardLibraries --output "$ARTIFACTS" \
  --runs 5 --jobs 5
```

The output directory must be new. Its manifests, copied sources, full native CLI
commands, stdout/stderr, JSON reports, per-task key profiles and measurements are
private artifacts; do not commit them. Use immutable built binaries throughout.
Limit `--jobs` together with other local verification processes to the current
compute budget. Five fresh processes execute each scenario, with a barrier
between scenarios. The default is one concurrent process.

Each sample owns a fresh source copy and cache directory. Cold starts with an
empty cache; warm launches a new process with the populated cache; edit launches
another new process after swapping the equality operands in the body of
`Std.Functions.Commutative`. No contract or other source body changes. The native
Std project supplies its verification profile, including its RU budget. The
script uses one verification core, no elapsed-time limit, deterministic literal
hashes and independent prover histories. Target-specific variants are excluded
by that project. An expected negative outcome is recorded rather than discarded.

Wall time includes CLI startup, image hashing, resolution, translation, preparation,
cache I/O and verification. POSIX `wait4` records user plus system CPU for the
child process tree. `solverStarts` counts physical proof-solver processes;
`solverVersionProbes` separately counts version probes. RU sums every selected
task, including replayed tasks; it is not the RU of work actually sent to a solver.

The script checks a nonempty complete task inventory, exact cold/warm stdout,
stderr and exit status, and identical complete JSON rows except timing/provenance.
Split rows are matched by VC number/seed, retaining every field and duplicate;
the logger's existing asynchronous split completion order can vary between runs.
Every warm task must replay with zero proof or version-probe processes. After the
edit, the set of recomputed keys must equal the new complete task keys absent
from the cold run. Complete input includes ordered UsedTypes and execution
parameters, not merely printed pruned declarations. The focused regression also
uses an independent edited cold run as its changed-key oracle and checks
corruption, divergence/quarantine, counterexamples and effective limit overrides.
