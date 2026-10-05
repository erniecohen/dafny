# Extended newtype bases: public performance measurements

The completed artifacts of [run 37241854757](https://github.com/erniecohen/dafny/actions/runs/37241854757) cover 13 extended families and all five fixture cohorts: function identity, function creation, generic tuple boundaries, generic nesting at levels 1/8/32, and nominal depth at 1/10/100/1000. Of 978 verifier samples, 954 pass and 24 wrapper samples remain unproved after the outer process cap.

Compiler source: be0c7c4da87db52cfe76cc8422001a1e9228fb60; last product: 95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6. Dafny is 4.11.0, executing on github-ubuntu-24.04. The primary solver is Z3 5.1.0; 4.12.1 is a separate comparison. Both use seed 0, one core, 16,000,000 RU per verification batch, a 60-second per-batch verifier limit and a 90-second outer process cap, and declaration-order normalization disabled.

All 810 non-depth verifier samples exit zero, all CSV batches Passed, with no verifier warnings: 558 extended and 252 from the other fixture cohorts across both solvers. Depth adds 144 verified samples and 24 capped samples. The extended total includes twelve extra N-off samples from trivial/local numeric refinements. The 318 completed arm/solver identities each have three repetitions with identical RU totals. All 69 selected source files are byte-identical to prior package b573bf6efadf0bdad83cb26188206e9daabd2771.

Primary-solver proof cost is the sum of each program’s batch RU, including well-formedness and correctness. Full per-family results for both solvers are in [the proof-cost table](extended-newtype-bases-performance/proof-cost-table.tsv). B is the base/subset carrier, N the newtype, and W the datatype wrapper. Wrapper erasure is a compiler setting, so both W proof totals agree.

| Family | B RU | N RU | W RU |
|---|---:|---:|---:|
| datatype-ghost-update | 61,900 | 68,292 | 71,871 |
| datatype-nested-update | 70,602 | 77,387 | 82,790 |
| datatype-root-update | 69,697 | 76,705 | 82,505 |
| generic-boundary | 109,761 | 119,548 | 116,392 |
| visibility-session | 778,650 | 1,168,550 | 1,001,650 |

The datatype updates have seven VC batches for N versus six for B/W; visibility has 250 versus 150. These are family-specific proof costs. The only B feature-on/off difference is ordinal-finite-refinement: 87,864→87,212 RU under 5.1.0 and 87,657→87,019 under 4.12.1. All other B totals agree.


The complete depth cohort resolves 168/168 samples. Translation and verification each have 144 passes and 24 outer caps. All capped identities are depth-1000 W-erased/W-materialized, with the feature both off and on, three repetitions and both solvers. No capped run records Boogie or nonempty proof CSV rows, so its RU remains unavailable. Exit 124 is assigned by the runner after killing the process group at 90 seconds; it is not a measured RU exhaustion. There are no missing-trigger warnings in the completed depth proofs.

| Depth, primary solver | B RU | N RU | W RU |
|---|---:|---:|---:|
| 1 | 9,604 | 12,680 | 11,445 |
| 10 | 9,604 | 36,485 | 25,514 |
| 100 | 9,604 | 268,355 | 166,822 |
| 1000 | 9,604 | 2,593,055 | unavailable: outer cap |

N at depth 1000 has 1,002 VC batches and identical costs across its three samples. The separate 4.12.1 result is 2,593,169 RU. Both W settings have equal proof costs at depths 1/10/100 and both cap at 1000. A capped wrapper result supplies no N/W cost ratio at that depth.

The 15 runtime families build all four arms on C#, JavaScript, Python, Go and Java: 300/300 ordinary builds and 900/900 runtime samples exit zero, with exact checksums. These ordinary C# builds include the runtime; the separate external-runtime configuration is outside this matrix. Runtime median/MAD is recorded after warmup output through final output/process teardown; proof cost uses RU.

C# profiles count current-thread managed bytes inside the second Work call, with 2,000 warmup and 20,000 measured iterations in five independent processes. All 14 available families have equal B/N/W-erased bytes in every sample. The emitted C# B/N Work bodies are byte-identical in all 15 runtime families, and instrumentation preserves each body verbatim. Other backends expose no allocation counter.

| Workload | B/N/W-erased bytes | W-materialized bytes |
|---|---:|---:|
| arrow-adapter | 3,520,112 | 4,000,112 |
| datatype-ghost-update | 960,048 | 1,280,096 |
| datatype-nested-update | 960,048 | 5,280,096 |
| datatype-root-update | 960,048 | 1,280,096 |
| function-creation | 1,920,000 | 2,400,024 |
| generic-boundary | 8,960,048 | 9,440,072 |

The datatype programs retain universal checksum postconditions. Ghost update preserves tag=ghost stamp while changing the stamp. Generic-boundary carries Pair=(int,int) through a sequence, map and generic Identity function. Its tuple-boundary proof and measured runtime are included; separate ghost-tuple functional coverage is outside this benchmark denominator.

Codatatype allocation remains unavailable: the four instrumented C# rebuilds exit zero with CS8618 for field d, and the draft runner skips their entry points. Its 20 profile records have exit 125 and zero usable allocation samples. Ordinary codatatype proofs, builds and checksums pass.

Compiler build exits zero with warning output, including nullable C# warnings. All 202 archived DLLs are covered by the 208-component manifest and hash-match actual bytes. Archive SHA-256: 8f4055f4cde6a2792db1819f6cfe82e898ec3033e4ec8a8e456474fe8c1959df. All 360 native source copies match the verified inputs. Independent read-only auditing checks 4,474 command records (4,426 have completed raw process exits; 48 are synthetic caps), 978 CSV records (24 empty after caps), 1,824 Boogie hashes and 9,262 available generated-file hashes. A further 120 listed hidden .NETCoreApp assembly-attribute files are absent from the stable download; complete generated-file inventory verification is bounded by that absence. No available hash mismatch was found.

Depth-1000 wrapper attempts are unproved; all other selected positive proof contracts complete. The public workflow’s successful verdict does not erase its recorded caps. These are positive contracts and bounded runtime measurements; the separate negative/control corpus and full canonical gate remain outside this report.

The [depth results](extended-newtype-bases-performance/depth-proof-cost-table.tsv), [stage denominators](extended-newtype-bases-performance/stage-denominators.tsv), [C# allocations](extended-newtype-bases-performance/csharp-allocation-table.tsv), and [runtime median/MAD table](extended-newtype-bases-performance/runtime-timing-table.tsv) retain each measured arm and available outcome. The [public generators, contracts and recording scripts](https://github.com/erniecohen/dafny/tree/be0c7c4da87db52cfe76cc8422001a1e9228fb60/.github/review/issue113-benchmarks) reproduce the workload definitions; raw commands, outputs and measurements are attached to the linked public run.

## Source contracts and generated representation

The following excerpts are from compiler source be0c7c4da87db52cfe76cc8422001a1e9228fb60. The full files and contracts are byte-identical to prior benchmark source b573bf6efadf0bdad83cb26188206e9daabd2771. B denotes the base/subset carrier, N the nominal newtype, and W a datatype wrapper.

The ghost-update carriers preserve the same logical predicate and inhabited witness:

```dafny
datatype Leaf = Leaf(value: int, ghost stamp: int)
datatype Envelope = Envelope(leaf: Leaf, tag: int)
// B:
type Value = x: Envelope | x.tag == x.leaf.stamp
  witness Envelope(Leaf(0, 0), 0)
// N:
newtype Value = x: Envelope | x.tag == x.leaf.stamp
  witness Envelope(Leaf(0, 0), 0)
// W:
datatype Wrapped = Wrap(payload: Envelope)
type Value = w: Wrapped | w.payload.tag == w.payload.leaf.stamp
  witness Wrap(Envelope(Leaf(0, 0), 0))
```

Every arm retains the universal Work postcondition:

```dafny
method Work(n: nat) returns (checksum: int)
  ensures 2 * checksum == n * (n + 1)
```

B and N use the same update and explicit reintroduction:

```dafny
var v := value;
var u := v.(tag := i + 1, leaf := v.leaf.(stamp := i + 1));
value := u as Value;
```

W updates the payload and reintroduces its wrapper:

```dafny
var v := value;
var u := v.payload.(tag := i + 1, leaf := v.payload.leaf.(stamp := i + 1));
value := Wrap(u) as Value;
```

At 20,000 measured iterations, C# B/N/W-erased all allocate 960,048 current-thread managed bytes in each of five independent samples; W-materialized allocates 1,280,096. The compiled C# B/N Work bodies are byte-identical. Ordinary builds and checksum runs pass on C#, JavaScript, Python, Go and Java for both wrapper compiler settings. Allocation counters are available only for C#.

The closure-creation cohort changes Value's representation while preserving its round-trip contract and universal checksum postcondition:

```dafny
type Callable = int -> int
// B: type Value = Callable
// N: newtype Value = Callable
// W: datatype Wrapper = Wrap(value: Callable); type Value = Wrapper
lemma RoundTrip(b: Callable) ensures Decode(Encode(b)) == b {}
method Work(n: nat) returns (checksum: int)
  ensures 2 * checksum == n * (n + 1)
```

Its B/N loop creates and calls the same captured closure on each iteration:

```dafny
var captured := i;
value := Encode((x: int) => x + captured);
checksum := checksum + (value)(1);
```

W applies value.value. C# B/N/W-erased each allocate 1,920,000 measured bytes; W-materialized allocates 2,400,024. This is equality of the bounded measured allocations; closure creation still allocates. B/N emitted Work bodies are byte-identical, and all five ordinary backend outputs match.

The generic-boundary cohort carries Pair=(int,int) through seq<Value>, map<int,Value>, and Identity<Value>. It retains the universal postcondition 2*checksum=n*(n+1)+14*n. This is actual tuple/generic-boundary performance coverage. Separate ghost-tuple functional tests are outside the performance denominator.

The full report preserves the depth-1000 wrapper translation/proof caps, unavailable codatatype profiles, native compiler warnings, and absent hidden artifact metadata. These excerpts do not broaden that outcome boundary.

The [performance-trigger investigation](extended-newtype-bases-performance-investigation.md) attributes the additional nominal proof batches and reports all six runtime median comparisons above the provisional ten-percent trigger, including raw sample ranges and generated-code equality.
