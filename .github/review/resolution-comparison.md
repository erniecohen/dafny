# Module-resolution compatibility audit

`review.yml` builds the pre-feature control at
`c02d46e8c7faad8490b66238d1a3378267985728` with the same SDK, runtime and
checksum-checked Boogie packages as the candidate. Its binary verifies the
candidate's public inputs. A change to this control requires a separately
reviewed default change; it must not silently follow the feature branch.

The control advanced from `b98637c6677560944b2498002ecd32ec987a90af` after
https://github.com/erniecohen/dafny/pull/279 independently changed the shipped
default to fix duplicate opaque-reference type constructors. That crash fix can
legitimately change Boogie bytes, so it belongs in both builds. The original
control/candidate audit remains at
https://github.com/erniecohen/dafny/actions/runs/43134871839. The control remains
pinned to a shipped commit without this feature; no verdict baseline is changed
by this update.

The control subsequently advanced to the shipped decreases-order product in
https://github.com/erniecohen/dafny/pull/275, which independently changed the
default expected rows. Both candidate branches merge that shipped product;
this control update does not adopt cache-enabled behavior as the default.

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
options. Std experiments rerun the complete Std invocation, retaining its native
limits, deterministic literal hashes, printing and a fresh prover per batch.
Filtering to an affected symbol could skip earlier module translations and change
the default translator's name history. Seed zero must reproduce the original
full per-VC outcome/RU vector before the seed spread is accepted as evidence.
The six Std mode/seed pairs run in independent CI jobs using the captured producer
and small input-vector artifact. Each pins the same solver bytes and rebinds the
runner-local solver path. Artifacts `resolution-std-seed-<mode>-<seed>` retain the
full invocation, proof log, Boogie and affected-scope summary.
The seed experiments are sensitivity measurements, not replacements for the
full-population seed-zero comparison or its reviewed baseline.

A Boogie digest difference alone does not identify a cause for a solver-cost
change. In particular, module-qualified names, background restriction and
normalization order can all affect the search. A claim that renaming caused a
movement needs a separate controlled name-mapping replay that reproduces the
default input/outcome. The audit does not make that claim automatically.


## Enabled suite expectations after the shipped decreases-order repair

The four gating shards at
https://github.com/erniecohen/dafny/actions/runs/43219720405
failed only their stale enabled expectation comparison. Each passed exact
control-versus-option-omitted equality and N = 1 versus N = 2, 4, 8 equality
for Boogie bytes, verdicts and per-task RU. All four report no default-to-N = 1
verdict movements. The 1,165-row replacement is assembled from their unmodified
`expected-opt-in.tsv` artifacts:
[part 0](https://github.com/erniecohen/dafny/actions/runs/43219720405/artifacts/13946616791),
[part 1](https://github.com/erniecohen/dafny/actions/runs/43219720405/artifacts/13947450603),
[part 2](https://github.com/erniecohen/dafny/actions/runs/43219720405/artifacts/13946622992),
[part 3](https://github.com/erniecohen/dafny/actions/runs/43219720405/artifacts/13946418909).

Every changed row below equals the corresponding shipped default row. The
reasons follow the independently shipped repair and its proof changes in
https://github.com/erniecohen/dafny/pull/275 and
[`decreases-order-results.md`](../../docs/dev/decreases-order-results.md).
The default expectation files are unchanged. The two existing valid-print
observer profiles remain as previously recorded in the enabled vector.

| Program | Reason for replacing the enabled row |
| --- | --- |
| `dafny0/DecreasesTo0.dfy` | Restore positive `GoodInstances` proofs and add three intentional negative-old-value errors; inserted methods move diagnostic lines. |
| `dafny0/DecreasesTo1.dfy` | Add three intentional negative-old-value errors; inserted methods move diagnostic lines. |
| `dafny0/FunctionSpecifications.dfy` | `GoodPost` well-formedness now finishes after redundant lower-bound batches are removed. |
| `dafny0/NoTypeArgs.dfy` | Repair the recursive lemma and prove two concatenation helpers, adding two successful batches. |
| `dafny0/Termination.dfy` | Remove the redundant lower-bound failure at line 126. |
| `dafny0/ForLoops.dfy` | The decreases-order source changes move two diagnostic lines, with unchanged outcomes and counts. |
| `dafny1/SchorrWaite.dfy` | Remove three redundant successful lower-bound batches. |
| `dafny1/PriorityQueue.dfy` | Remove one redundant successful lower-bound batch. |
| `dafny2/SnapshotableTrees.dfy` | Repair `Iterator.Push`; its explicit proof facts change successful split accounting, preserving the existing error and four resource failures. |
| `dafny2/MinWindowMax.dfy` | The corrected decreases-order translation removes redundant batches and lets the previously resource-limited enabled-resolution proof finish. |
| `dafny2/SmallestMissingNumber-functional.dfy` | Remove two redundant successful lower-bound batches, preserving seven resource failures. |
| `dafny3/Inc.dfy` | Remove a redundant lower-bound diagnostic at an already reported location. |
| `dafny4/ExpandedGuardednessNeg.dfy` | Remove two redundant lower-bound diagnostics at already reported locations. |
| `dafny4/Primes.dfy` | Remove one redundant successful lower-bound batch. |
| `git-issues/git-issue-4939a.dfy` | Remove two redundant lower-bound diagnostics at already reported locations. |
| `git-issues/git-issue-193-reference-laws.dfy` | Add the six successful reference-model laws shipped by https://github.com/erniecohen/dafny/pull/276. |
