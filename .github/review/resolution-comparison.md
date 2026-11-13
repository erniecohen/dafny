# Module-resolution compatibility audit

`review.yml` builds the pre-feature control at
`0025e3f001351ee0f7c801c052bdd7d992c0de6e` with the same SDK, runtime and
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

The control advanced to `0025e3f001351ee0f7c801c052bdd7d992c0de6e`
after the independent operator design addendum. Its product source is unchanged.

Parallel resolution requires `--type-system-refresh`. For each suite shard and
the standard library, the audit keeps two native default controls (`baseline`,
`off`) with the original inference settings. It additionally runs an explicit
refreshed-engine profile on both builds (`baseline-refreshed`, `off-refreshed`)
and on the candidate at N = 1, 2, 4, 8. That profile appends
`--type-system-refresh:true` after the original arguments, including for fixtures
whose native RUN line selects legacy inference. It is a distinct comparison
profile; the native default observations and shipped verdict gates remain intact.
The option-validation regression separately requires rejection of the original
incompatible flags with exit 2, without parsing or resolving source.

All profiles use the native verdict runners and retain their resource, solver
and assertion-batch settings. Std
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
- refreshed control vs refreshed option omitted: the same three vectors;
- N = 1 vs N = 2, 4, 8: the same three vectors;
- refreshed option omitted vs enabled: proof and verdict populations (outcomes
  and RU may differ).

The enabled verdict vector is compared with its own reviewed files:
`expected-resolution-suite.tsv` and `expected-std-resolution-z3-5.1.0.tsv`.
Generate and review those files locally before pushing, separately from the
implementation changes, with every changed verdict explained. A local capture
writes `expected-opt-in.tsv` for that review; it never overwrites an expected file.
Default verdict files are unchanged.

`report.json` separately lists native-default to refreshed, refreshed to enabled,
and native-default to enabled verdict and per-VC RU movements,
full-scope total RU, and out-of-resource VC counts. For scopes with changed
outcomes between refreshed serial and enabled, `seed-variance.json` records
seeds 0, 1, 2 under both modes. Suite
experiments rerun the complete affected program with its native limits and
options with the explicit refreshed-engine profile. Std experiments rerun the complete Std invocation, retaining its native
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
