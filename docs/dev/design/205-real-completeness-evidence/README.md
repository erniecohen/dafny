# Issue 205: design experiments and reproducible evidence

This directory contains **design evidence, not a product implementation**. The source pin is `62bae981952337d312cbb3d43c39f9813cca6084` on `review/4.11.0`. The proposed primitive is represented by exactly one explicitly marked `{:axiom}` lemma in the experiments. All analysis, least-upper-bound wrappers, and polynomial proofs around that placeholder are ordinary verified Dafny. The placeholder would be replaced by the sealed intrinsic described in the design before the acceptance tests could satisfy “no user axioms.”

The important results are:

- The full theorem for an arbitrary coefficient sequence verifies, with 90 assertion batches, under both supported solver versions and both resolvers.
- Explicit set-scoped membership and upper-bound triggers give the best reliable result of the measured inner-trigger alternatives.
- An empty-set and an unbounded-set false-proof control exercise the raw guarded primitive without a precondition masking the call. Deliberately deleting either guard makes the corresponding control prove `false`.
- Dormant global axioms can change resources even when their instantiation count is zero. This supports demand-local emission as the way to obtain the promised unused-feature identity.

The ordinary source tests run without live Z3 profiling. Profiling is enabled only when replaying the captured SMT directly in a fresh solver process. A preliminary run with live `smt.qi.profile` produced a Boogie “inconclusive” result before the affected VC had even been sent a `check-sat`; the unchanged proof passes in the clean protocol. Those preliminary runs are excluded.

## What is measured

| Dataset | Meaning | Existing `--additional-axioms` |
|---|---|---|
| `acceptance-source-runs` | Actual pinned Dafny verification of the complete symbolic polynomial proof and three false-proof controls | ON |
| `acceptance-triggers-*` | The same actual native real/ISet/Box VCs, changing only explicit pattern annotations | ON |
| `native-source-runs` | Small source-level LUB uses, live invalid-domain controls, and unconstrained-fallback controls | OFF and ON |
| `native-triggers-*` | Exact small native VCs with only inner patterns changed | ON |
| `native-stress-5.1.0` | A dense bounded interval, also with 64 real members of another set | OFF |
| `existing-real-*` | Five existing real-heavy test files, replayed with dormant abstract completeness candidates | OFF |
| `abstract-*` | Small abstract mathematical kernel comparing outer trigger/emission choices | Not a Dafny run |
| `guard-mutations-5.1.0` | Intentionally unsound mutations used to validate the negative controls | ON |

The existing shared option controls its already-shipped families. The experimental `complete` placeholder is active because it is explicitly axiomatic, independently of that option. No implementation of a new option-controlled intrinsic is claimed here.

The `baseline/` subdirectory has the separately documented full-suite and source-standard-library measurements. Its protocol and coverage must be read independently of the focused experiments below.

## Full acceptance proof and inner triggers

`sources/acceptance/real-completeness.dfy` proves the coefficient-sequence theorem by recursive Horner bounds and a least-upper-bound argument. Sequence length is a symbolic natural number; there is no fixed-degree bound. The single placeholder is:

```dafny
lemma {:axiom} complete(s: iset<real>, w: real, b: real) returns (u: real)
  ensures w in s && upper(s,b) ==> upper(s,u)
  ensures w in s && upper(s,b) ==>
    (forall y: real {:trigger upper(s,y)} :: upper(s,y) ==> u <= y)
```

`upper(s,b)` is the ordinary ghost predicate `forall x: real {:trigger x in s} :: x in s ==> x <= b`. There are no primitive preconditions and no global `Sup` function in this source experiment. The ordinary wrapper `lub` checks nonemptiness and an upper bound.

Each full-proof replay uses 90 fresh solver contexts, a resource limit of 2,000,000 per query, a 30-second safety timeout, arithmetic solver 2, and the exact other settings captured from Dafny. Resource totals include setup. “Instances” counts all quantified instances in the VCs, including the existing prelude; it is not a count solely of new completeness facts.

| Z3 | Inner pattern choice | RU | Quantifier instances | Proved batches |
|---|---|---:|---:|---:|
| 4.12.1 | Explicit set membership / `upper(s,b)` | **1,940,734** | **2,043** | **90 / 90** |
| 4.12.1 | Real-box trigger for pointwise upper bounds; scoped leastness | 3,653,293 | 2,540 | 90 / 90 |
| 4.12.1 | Automatic patterns for those upper/least quantifiers | 3,947,881 | 2,498 | 88 / 90 |
| 5.1.0 | Explicit set membership / `upper(s,b)` | **2,476,611** | **3,655** | **90 / 90** |
| 5.1.0 | Real-box trigger for pointwise upper bounds; scoped leastness | 3,891,059 | 4,729 | 90 / 90 |
| 5.1.0 | Automatic patterns for those upper/least quantifiers | 3,900,951 | 4,771 | 90 / 90 |

The same explicit source proves all 90 batches under the refreshed resolver, with exactly the same resource totals on each solver. Full source-run results are in `data/acceptance-source-runs/results.json`; the compact comparison is `data/acceptance-summary.csv`.

The two automatic-pattern failures on 4.12.1 are `polynomialIvt_split31` and `polynomialIvt_split46`. Both report `(incomplete (theory arithmetic))`. The first uses 17,598 setup plus 35,571 query units. The second uses 16,932 setup plus exactly 2,000,000 query units and therefore reaches the configured query cap. No timeout was observed. See `data/failure-reasons.json` and the diagnostic replays retained in the raw archive.

The existential LUB acceptance lemma alone costs 7,791 RU / 29 instances on 4.12.1 and 7,635 RU / 21 instances on 5.1.0. The IVT body has 52 batches: 1,409,775 RU / 1,141 instances and 1,705,659 RU / 2,582 instances respectively. The remainder is the ordinary arithmetic, polynomial, and wrapper proof support.

### Why the exact triggers matter

A pointwise bound with fixed `s,u` uses `{s[Box(x)]}`. Its instances contribute an inequality and do not introduce new membership arguments. Leastness uses `{upper(s,b)}`; its instance contributes `u <= b` and does not create a new upper-bound request. The definition of `upper` can introduce membership witnesses, but scoped leastness does not turn every such witness into another upper-bound request. This is an argument about the new family, not a guarantee that arbitrary user quantifiers cannot generate an unbounded search.

Replacing leastness by a real-box trigger is especially poor. In the exact small native kernel on 5.1.0:

| Positive obligation | Scoped RU / instances | Broad leastness RU / instances | Broad result |
|---|---:|---:|---|
| Checked LUB wrapper | 7,319 / 6 | 71,003 / 612 | Incomplete quantifiers |
| Member upper bound | 7,651 / 18 | 70,101 / 623 | Incomplete quantifiers |
| Leastness use | 7,005 / 5 | 43,923 / 361 | Incomplete quantifiers |
| Existential LUB | 7,635 / 21 | 2,004,850 / 2,227 | Query cap reached |

The first three failures are below their caps. A target `upper(s,b)` need not contain a boxed `b`, so a broad boxing pattern can miss the needed target while instantiating irrelevant ones. The existential case consumes exactly 2,000,000 query units plus 4,850 setup units and reports `canceled`. The corresponding 4.12.1 results have the same classifications.

The dense-interval stress case with 64 unrelated members costs 41,739 RU / 758 instances with scoped patterns, versus 60,177 / 1,818 with the broad pointwise pattern, and 141,146 / 3,690 when both pointwise and leastness patterns are broadened. This is why membership of an unrelated set should not drive bound instances for the requested set.

## Vacuity, missing primitive, and ordinary supremum wrappers

The three full-proof negative controls call the raw guard-total placeholder on a bounded singleton, the empty set, and an unbounded set. Each fails only at its final `assert false`, in both resolvers and both solver versions. Their totals are 40,644 RU on 4.12.1 and 42,049 RU on 5.1.0.

The empty control also supplies `upper(s,u-1.0)`, and the unbounded control supplies `u+1.0 in s`. These terms make the controls sensitive to a missing guard. With the nonempty guard deliberately deleted, `emptyVacuity` proves `false`; with the boundedness guard deleted, `unboundedVacuity` proves `false`. Each mutation leaves the other two controls failing. These are intentional unsound comparison files, not proposed axioms.

The separate small native kernel has four positive methods and five expected negative assertions, including wrong assertions that the invalid-domain result equals zero. All eight solver/resolver/shared-option combinations have exactly those outcomes: 81,222 total RU on 4.12.1 and 81,807 on 5.1.0. The raw primitive's invalid-domain result is deliberately unconstrained; a zero fallback is an ordinary library-wrapper decision.

`sources/acceptance/sup-wrapper.dfy` implements existence, uniqueness, a total ghost `sup` with zero fallback, and a root-returning IVT wrapper as ordinary verified code. Its four checks all pass in both resolvers: 55,141 RU with 4.12.1 and 56,084 with 5.1.0. These wrapper runs use the solver-reuse protocol, without `/restartProver`; do not combine their counts with the fresh-process totals above. `data/wrappers-and-no-primitive/` retains the source hashes, exact flag descriptions, and reports.

The no-primitive control replaces `complete` by an ordinary zero-returning implementation with no postconditions or axiom. Verification reports two errors, both LUB-wrapper postconditions. Other method contracts can still be used modularly, so their individual successes do not rescue the failed program. The full program does not verify without the primitive.

`dafny audit` on the positive prototype reports exactly one finding: the explicitly marked `complete` axiom. Audit performs no verification. The stored audit record documents this trust boundary.

## Unused-feature cost experiment

The five-file experiment uses the existing shared option OFF. The “global” candidates add dormant abstract set/real completeness declarations to otherwise unchanged captured VCs. **All new completeness instance counts are zero**, and all baseline solver-result sequences are preserved. These tests intentionally contain negative obligations, so “same outcome” does not mean every assertion is proved.

| Existing test, Z3 5.1.0 | Baseline RU | Dormant broad axiom | Dormant nested supremum | Dormant request axiom | Local/no-use |
|---|---:|---:|---:|---:|---:|
| `dafny0/RealTypes.dfy` | 22,142 | 26,012 | 26,302 | 26,102 | **22,142** |
| `dafny0/RealCompare.dfy` | 98,309 | 109,145 | 109,957 | 109,397 | **98,309** |
| `dafny0/TypeConversions.dfy` | 446,417 | 238,564 | 239,144 | 238,744 | **446,417** |
| `dafny0/TypeMembers.dfy` | 164,023 | 187,365 | 189,408 | 187,904 | **164,023** |
| `dafny4/Regression3.dfy` | 16,427 | 18,174 | 18,348 | 18,228 | **16,427** |

The local/no-use candidate adds zero bytes because these files contain no primitive request. Its SMT hashes and resources equal baseline by construction. This is modeling evidence for the proposed emission discipline, **not a completed implementation identity gate**. Both solver versions' complete measurements are retained. Production must still test that the option gate and intrinsic lowering actually preserve this property.

## Abstract outer-trigger comparison

The `abstract-*` datasets retain the early outer-trigger comparisons: broad membership/boundedness multipattern, supremum-plus-witness multipattern, nested supremum trigger, private request trigger, and local ground instance. They use an abstract immutable-set sort, not Dafny's boxing and function-value encoding. Their IVT case is only a final sign contradiction conditional on continuity consequences. Use the full native proof above for acceptance evidence.

Those experiments explain the architecture choice: on 5.1.0, 20 bounded sets with no request cause 40 completeness instances and 10,615 RU under the broad pattern, while local emission stays at the no-completeness baseline of 4,294 RU. With 64 member terms for one requested set, the broad and supremum-plus-witness patterns produce 130 outer completeness instances; a private request produces one and a local instance has no outer quantifier.

## Reproduction and provenance

The scripts use only Python's standard library. Set `ISSUE205_DOTNET`, `ISSUE205_DAFNY`, `ISSUE205_Z3_4121`, and `ISSUE205_Z3_510` to the chosen executables. Build the source pin following `AGENTS.md`, including the pinned Boogie packages. The measured .NET SDK is 10.0.100. Solver executables are from the PyPI `z3-solver` 4.12.1.0 and 5.1.0.0 wheels; they are **not** the distinct GitHub-release binaries used by the fork's CI. Version strings and byte hashes are in `manifest.json`.

From this directory:

```sh
python scripts/run_acceptance.py \
  --dotnet "$ISSUE205_DOTNET" --dafny "$ISSUE205_DAFNY" \
  --source_dir sources/acceptance \
  --z3_4 "$ISSUE205_Z3_4121" --z3_5 "$ISSUE205_Z3_510" \
  --output reproduced/acceptance

python scripts/replay_patterns.py \
  --smt reproduced/acceptance/positive-z3-5.1.0-refresh-0/input.smt2 \
  --z3 "$ISSUE205_Z3_510" --output reproduced/patterns-5.1.0 \
  --qid_prefix realcompletenessdfy --upper_lines 199 --least_lines 206,212,220

python scripts/run_kernel.py \
  --z3 "$ISSUE205_Z3_510" --output reproduced/abstract-5.1.0
```

`run_dafny_corpus.py` additionally takes `--source` for the pinned repository. `run_source_kernel.py` takes `--source sources/native-kernel/source-kernel.dfy`. `run_guard_mutations.py` creates explicitly named unsound variants in its output directory. All scripts expose their arguments with `--help`.

Each data directory has a deterministic `raw.tar.gz`, exact member hashes, and reviewable JSON/CSV summaries. The archives preserve the SMT and proof-source bytes. Only `Results File:` destination paths in human diagnostic output have been reduced to basenames; the original and shipped diagnostic hashes are recorded. Some replay-manifest input hashes describe Python's newline-normalized logical text; the exact original-byte hashes remain in `archive-members.json`. Per-VC formula hashes establish equality after removing only pattern annotations.

The local Git attributes preserve CSV/TSV/archive bytes across checkouts, including meaningful empty trailing TSV columns. `SHA256SUMS` covers these experiment files; the independently prepared `baseline/` directory has its own checksum file. No local absolute paths, host identifiers, vendor binaries, or preliminary failed-instrumentation runs are included.
