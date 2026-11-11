# Module-resolution compatibility audit

`review.yml` builds the pre-feature control at
`b98637c6677560944b2498002ecd32ec987a90af` with the same SDK, runtime and
checksum-checked Boogie packages as the candidate. Its binary verifies the
candidate's public inputs. A change to this control requires a separately
reviewed default change; it must not silently follow the feature branch.

For each suite shard and for the standard library, the audit runs that control,
the candidate with the option omitted, and the candidate at N = 1, 2, 4, 8.
It uses the native verdict runners and their unmodified proof settings. Std
uses `/deterministicLiteralHashes:1 /restartProver`, exactly as its ordinary
verdict gate does. Target-specific parts retain their recorded library options.

An executable adapter adds `--bprint` before original arguments and records the
actual invocation and raw per-module byte digests. It does not normalize names,
paths or Boogie text. The adapter records process exits and safety timeouts; a
safety timeout or missing positive proof log makes the audit incomplete. Each
native invocation must have a matching Boogie observation, including an empty
module set for intentional pre-verification diagnostics. Raw Boogie is retained
for the Std and MinWindowMax cost investigations; other captured text is hashed
before removal. Original proof logs and commands remain in every artifact.

The comparison requires exact equality of:

- control vs option omitted: verdicts, per-VC outcomes and RU, and module bytes;
- N = 1 vs N = 2, 4, 8: the same three vectors;
- default vs enabled: proof and verdict populations (outcomes and RU may differ).

The enabled verdict vector is compared with its own reviewed files:
`expected-resolution-suite.tsv` and `expected-std-resolution-z3-5.1.0.tsv`.
The first capture fails until those files are reviewed and committed separately
from implementation changes, with every changed verdict explained. It writes
`expected-opt-in.tsv` for that review; it never overwrites an expected file.
Default verdict files are unchanged.

`report.json` lists every default-to-enabled verdict and per-VC RU movement,
full-scope total RU, and out-of-resource VC counts. For scopes with changed
outcomes, `seed-variance.json` records seeds 0, 1, 2 under both modes. Suite
experiments rerun the complete affected program with its native limits and
options. Std experiments filter one affected symbol at a time, retaining its
native limits, deterministic literal hashes and a fresh prover per batch.
The seed experiments are sensitivity measurements, not replacements for the
full-population seed-zero comparison or its reviewed baseline.

A Boogie digest difference alone does not identify a cause for a solver-cost
change. In particular, module-qualified names, background restriction and
normalization order can all affect the search. A claim that renaming caused a
movement needs a separate controlled name-mapping replay that reproduces the
default input/outcome. The audit does not make that claim automatically.
