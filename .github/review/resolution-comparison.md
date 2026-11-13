# Module-resolution compatibility audit

`review.yml` builds the pre-feature control at
`8deca8ad4e8b49988f683855fe78a6917338511c` with the same SDK, runtime and
checksum-checked Boogie packages as the candidate. Its binary verifies the
candidate's public inputs. The control must remain a separately reviewed shipped commit without the
resolution option; it must not silently follow the feature branch.

The control advanced from `b98637c6677560944b2498002ecd32ec987a90af` after
https://github.com/erniecohen/dafny/pull/279 independently changed the shipped
default to fix duplicate opaque-reference type constructors. That crash fix can
legitimately change Boogie bytes, so it belongs in both builds. The original
control/candidate audit remains at
https://github.com/erniecohen/dafny/actions/runs/43134871839. The control remains
pinned to a shipped commit without this feature; no verdict baseline is changed
by this update.

The control advanced again to `c02d46e8c7faad8490b66238d1a3378267985728`
after https://github.com/erniecohen/dafny/pull/276 and
https://github.com/erniecohen/dafny/pull/275 landed independently. The latter
fixes the default decreases-tuple relation and deliberately changes shipped
suite/library verdicts. Both builds must include that fix; its legacy opt-out
is not enabled for this audit. The prior complete audit remains at
https://github.com/erniecohen/dafny/actions/runs/43151173385.

The control advanced to `7dc1d91b1159bafb2d75dfb092930112528e6c18`
after https://github.com/erniecohen/dafny/pull/270 independently changed the
shipped map-glue guards for matching and refactored datatype equality. Its
prelude bytes belong in both builds; the feature must not undo that shipped
change. The completed preceding-base captures remain at
https://github.com/erniecohen/dafny/actions/runs/43214507192.

The control advanced to `50624274c63bed6d3bff8c16dc8f64cfb686ef20`
after https://github.com/erniecohen/dafny/pull/277 shipped the independent,
opt-in source-operator feature. Both builds include that current parser and
resolver; this integration does not enable source operators implicitly. Its
shipped default verdict files are unchanged. The five completed paired
captures and six full-Std seed observations on the preceding shipped base are at
https://github.com/erniecohen/dafny/actions/runs/43252258965.

The control advanced to `8deca8ad4e8b49988f683855fe78a6917338511c`
after https://github.com/erniecohen/dafny/pull/286 added CI-only .NET download
retries. Its product source is identical to the preceding pinned control. Both
builds now use the shipped retry policy.

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
