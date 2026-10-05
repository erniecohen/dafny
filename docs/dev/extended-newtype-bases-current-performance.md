# Issue 113: current public performance observations

The current public benchmark supports representation preservation in its bounded executable corpus. The C# `Work` body is byte-identical between the direct-base and newtype arms in all 15 executable workloads. In all 14 workloads with complete allocation profiles, their managed allocations also equal the optimized-wrapper arm. This does not establish a universal speedup, complete depth support, or whole-feature acceptance.

The measured [public run 37357169410](https://github.com/erniecohen/dafny/actions/runs/37357169410) used harness commit `ec224fdbc8c5bcba488ba92f1a387f737b555d42`, product `4e50e86cde6ab2ef6cab0762ffc4df59d646dc15`, Source tree `9c2bb48a778ee2abd6ef10f8a0d72f7843a16af5`, and compiler `4.11.0+fcb2042d.review.c9c7622d`. The separate issue 165 callback proposal is absent. All 149 fixture/helper inputs are unchanged from the reviewed benchmark corpus; the depth-1000 direct-base source has 1,000 aliases. An earlier copied 100-alias input is not used as its denominator.

The [complete comparison table](extended-newtype-bases-current-performance-data/workload-comparison.csv) contains 167 rows, one per workload/backend/solver/feature setting, with B, N, W-erased and W-materialized columns. The existing constrained B/W variants preserve their matching refinement predicates and introduction obligations. N is requested only with the feature enabled. Each row links its raw public artifact. The [receipt](extended-newtype-bases-current-performance-data/receipt.json) binds the 37 public artifact digests and derived files; [generated-code comparisons](extended-newtype-bases-current-performance-data/csharp-work-body-comparison.json) retain all 15 original B/N bodies and source hashes.

## Measurement boundaries

The feature axis is `--extended-newtype-bases`, with `--general-newtypes=true --type-system-refresh=true`; it is not the `--additional-axioms` axis. AdditionalAxioms is omitted and follows the compiler default. Z3 5.1.0 and 4.12.1 are separate public diagnostic axes. RU is compared only within one solver/build/settings combination. The commands request `smt.random_seed=0`, declaration order 0, one core, a 16,000,000 per-VC resource limit, 60-second VC safety cap and 90-second whole-command cap. No alternate seed was measured.

All runs use public Linux x86_64 Ubuntu 24.04 Actions runners and the recorded target toolchains. The fixtures consume their results. Arm order is shuffled with seed 113132. Resolve/translate/verify and ordinary runtime have three separate command/process samples; allocation profiles have five; each backend build has one sample. The table reports medians and median absolute deviations (MAD). A one-sample build MAD of zero does not estimate build variability. Proof figures repeated on backend rows reuse the same verifier observations; they are not extra proof runs.

Resolve timings and peak RSS cover process startup, parsing and resolution together. Translation uses the existing `/noVerify` route; its BPL sizes and syntactic axiom/function/procedure/quantifier/assert counts are **completed translation output**, not verify output or actual VC/quantifier-instantiation counts. No quantifier-instantiation counters are available in the existing output. Ordinary post-warmup timing includes final output and process teardown; the C# profile measures `Work` separately and counts current-thread managed allocated bytes. Object counts, other-thread/native allocations and a universal runtime-memory peak are not inferred from these bytes.

The original diagnostic exits remain explicit:

| Stage | Requested records | Observed result |
| --- | ---: | --- |
| Resolve | 978 | 978 exit 0 |
| Translate | 978 | 954 exit 0; 24 whole-command caps |
| Verify | 978 | 951 exit 0; 27 whole-command caps |
| Ordinary backend build | 300 | 300 exit 0 |
| Ordinary runtime | 900 | 900 actual processes, expected checksums and exit 0 |
| C# profile build | 60 | 60 exit 0; four codata rebuilds emit CS8618 |
| Requested C# profile sample | 300 | 280 actual samples; 20 warning-rejected admissions, no runtime |

Actions success records diagnostic orchestration. It does not waive warnings, caps, unavailable metrics or failed prerequisite admissions. Ordinary C# backend output hides successful native-build diagnostics, so a quiet ordinary CLI log does not establish absence of the underlying codata warning.

## Allocation and emitted representation

These are median managed bytes inside one warmed C# `Work` call. All completed allocation samples have MAD zero. Other backends have successful checksum executions but no allocation instrumentation in this run.

| Workload | B | N | W-erased | W-materialized |
| --- | ---: | ---: | ---: | ---: |
| arrow-adapter | 3,520,112 | 3,520,112 | 3,520,112 | 4,000,112 |
| arrow-coercion | 1,760,112 | 1,760,112 | 1,760,112 | 2,240,112 |
| datatype-ghost-update | 960,048 | 960,048 | 960,048 | 1,280,096 |
| datatype-nested-update | 960,048 | 960,048 | 960,048 | 5,280,096 |
| datatype-root-update | 960,048 | 960,048 | 960,048 | 1,280,096 |
| function-creation | 1,920,000 | 1,920,000 | 1,920,000 | 2,400,024 |
| function-identity | 0 | 0 | 0 | 480,024 |
| generic-boundary | 8,960,048 | 8,960,048 | 8,960,048 | 9,440,072 |
| ordinal-finite | 0 | 0 | 0 | 640,032 |
| ordinal-finite-refinement | 0 | 0 | 0 | 640,032 |
| refinement-local | 0 | 0 | 0 | 640,032 |
| refinement-quantified | 800,368 | 800,368 | 800,368 | 1,280,392 |
| refinement-trivial | 0 | 0 | 0 | 640,032 |
| tree-observation | 35,200,000 | 35,200,000 | 35,200,000 | 35,680,000 |

The matching B/N/W-erased allocations are observations for these exact contracts and iterations. The materialized wrapper costs depend on this backend and workload; they are not a general bytes-per-wrapper theorem. No completed C# profile has an N median `Work` slowdown above 10% against B or W-erased in this run. These short single-run comparisons do not establish a calibrated performance guarantee.

For the function-identity workload, both B and N compile their Encode/Decode helpers to returns of the original `Func`:

```csharp
public static Func<BigInteger, BigInteger> Encode(Func<BigInteger, BigInteger> b) {
  return b;
}
public static Func<BigInteger, BigInteger> Decode(Func<BigInteger, BigInteger> v) {
  return v;
}
```

Both original `Work` bodies contain the same identity-wrap loop:

```csharp
while ((_2_i) < (n)) {
  _0_value = __default.Encode(__default.Decode(_0_value));
  checksum = (checksum) + (Dafny.Helpers.Id<Func<BigInteger, BigInteger>>(_0_value)(_2_i));
  _2_i = (_2_i) + (BigInteger.One);
}
```

For codatatype-lazy, both B/N Encode and Decode likewise return the original `_IStream`. Their byte-identical `Work` body obtains a base view, reads `dtor_head`, and encodes `dtor_tail`; wrapping adds no traversal in this excerpt. Ordinary codata execution has 20 builds and 60 actual runtime processes across four arms/five backends. Each process calls `Work` twice, including warmup. The four C# profile builds emitted runnable DLLs, but CS8618 on the existing lazy-cache field `d` rejected all 20 profile admissions before launch. Codata allocation remains unavailable. This is not an observed missing entry point or runtime crash. The warning gate is unchanged.

Code equality here covers the named method and the quoted helpers. Entire generated files contain different declaration metadata; neither full-file equality nor universal runtime equivalence is claimed.

## Proof cost and depth findings

The ordinary families do not show a uniform proof-cost advantage. The table retains different declaration counts rather than dividing away newtype witness/constraint obligations. At depth 100, N verifies 102 declarations while B and W verify two. With Z3 5.1.0, N costs 268,355 RU versus B 9,604 and W-erased 166,822; at depth 10 N costs 36,485 versus W-erased 25,514. These exceed the provisional 20% investigation trigger. The fixtures declare different nominal chains and corresponding cast/constructor proofs; the extra obligations explain why total costs are not equal, but do not establish a causal account of every RU difference or excuse nonlinear preparation.

The severe scaling boundary remains open:

| Exact enabled depth case | Completed translate median | Translation BPL bytes | Verification |
| --- | ---: | ---: | --- |
| N100, Z3 5.1.0 | 1.62 s | 300,679 | 102 verified; 268,355 RU |
| N1000, Z3 5.1.0 | 85.27 s | 2,093,692 | Three 90-second caps; no RU result |
| N1000, Z3 4.12.1 | 68.90 s | 2,093,692 | 1,002 verified; repeated 2,593,169 RU |
| W100, either axis | See full table | 4,792,164 / 4,792,170 | Completed, priced separately by solver |
| W1000, both variants/feature settings/axes | 24 translation caps | Unavailable | 24 verification caps; no RU result |

N1000 pinned verify emits a 2,241,812-byte BPL but zero-byte CSVs; it cannot inherit an older source's proof result. All W1000 `/noVerify` translations cap too. Standalone resolution completes, but these logs do not locate the remaining delay within translation, Boogie preparation, printing or solver execution. A cap is not an observed solver resource-limit exhaustion. Reference-axis completion does not replace the incomplete pinned axis. Nonlinear N preparation and missing W1000 results require focused attribution; a different safety cap would be a separately labeled observation, not a repair of this frozen campaign.

## Retention and remaining work

All 37 API-digest ZIPs, 168 stage reports, 4,494 actual/admission rows, before/after compiler bindings and every raw sample were independently rehashed during review. The retained ZIPs contain 36,458 unique logical members. All reported uploaded generated members match their hashes except 120 hidden `.NETCoreApp,Version=v8.0.AssemblyAttributes.cs` files absent from the original upload selection. Their in-run hashes remain recorded; their bytes are unavailable here. Complete retained generated-tree closure is therefore unestablished, even though the principal emitted sources and runtime binaries are retained.

The original 90-second campaign stays incomplete. Remaining work includes focused depth attribution, a warning-preserving codata-profile diagnostic and separately reviewed compiler-warning repair if allocation profiling is needed, retained hidden outputs in a future run, alternate-seed stability, and the final integration-tree gates. Default-off compatibility is a separate corpus. This report preserves observed results, source-supported explanations and unresolved hypotheses separately; it does not authorize source, oracle, budget or warning waivers.
