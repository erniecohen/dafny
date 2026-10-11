# Full supported-line integration into dev

Source boundaries: supported line `1256f92b0b494e8f1bf52aaec145d36a8a862f33`,
dev `858e4bfbcf00fbf0146255c0a4b0efdfc4deb66f`, completed #169 port
`9029c0461d13aa73d7c40dddd260a26db65deaef`.

The [coverage map](supported-to-dev-coverage.json) records all 127 nonmerge
supported commits and 52 supported PRs from public Git and GitHub metadata.
The merge retains full supported history and reconciles source overlaps.
The [tree map](supported-to-dev-tree.json) records every path changed on either
line from the common base, with public blob identities and exact-copy checks.
It is structural evidence; overlapping source still requires semantic and
native review.
The integration remains under validation. Existing dev counterparts are listed
individually in the map, including the partial #136/#162 relationship; no
counterpart is treated as blanket source equality.

## Source adaptations

- Preserve dev floating-point types, conversions, constants, special fields,
  approximate expressions, refreshed resolver paths and existing type-system
  defaults. Extend cycle-safe numeric ancestry to floating-point carriers.
- Restore the fixed Boogie dependency and all prelude repairs. Retain upstream
  functions that depend on allocation while combining captured-value allocation
  and finite-support boundaries.
- Restore shared additional-axiom helpers, pure-arrow allocation facts and
  default two-state termination around #169's reviewed obligation lowering.
- Retain dev 4.11.1 identities and build with the supported .NET 10 migration.
  Runtime artifacts and compiler output remain branch-specific; dev uses its
  exact source hash, without a supported-line release tag or product hash.
- Combine regression registrations, including the dev output-filename alias
  control. Expected verdicts begin at dev's recorded baseline and must be
  updated only from individual demonstrated outcomes.
- Preserve dev Statistics, DateTime, ZonedDateTime and Base64 source additions;
  carry the supported quotient-proof/source follow-up alongside them.
- Restore strict original issue100, supporting definitions, Schorr–Waite and
  labeled/nested two-state checks. Earlier partial-port acceptance exceptions
  are removed; the combined tree requires its own native evidence.

Issues #170 and #171 remain separate. This integrates accepted supported-line
changes; the flag remains experimental and off by default.

## Validation boundaries

The combined compiler source is `eac45d90832014b377a68e49444057d230123e58`,
reporting `4.11.1+eac45d90`, built with .NET 10 and
Boogie `3.5.5-review.37e4435d`. The [immutable native build](https://github.com/erniecohen/dafny/actions/runs/38139291400)
records the compiler and dependency assembly hashes. Native proof receipts use
Z3 5.1.0, binary SHA256
`b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23`.
Later metadata, fixture and documentation commits do not change this compiler
source. Each receipt names its compiler source and verification input source.

### Passing focused gates

- The [strict original issue100 receipt](https://github.com/erniecohen/dafny/actions/runs/38139429937)
  passes all six calls at the original ceilings with both resolvers, without the
  earlier partial-port acceptance exception. The earlier complete
  [independent interpreter](https://github.com/erniecohen/dafny/actions/runs/38136538378)
  records 386 verifier invocations with zero failed checks, including supporting
  definitions, independently licensed contract preparation, Schorr–Waite
  sequential-postcondition positives, missing-edge negatives, feasible-state
  controls and labeled/nested two-state cases. Those receipts retain their
  explicit source boundaries; they are not final normal CI.
- The [fresh native float controls](https://github.com/erniecohen/dafny/actions/runs/38139429937)
  pass all eight profiles: two resolvers, lowering OFF/ON, positive/negative.
  Every positive has ten Correct declarations. Every negative has four Correct
  and six Errors declarations, each expected negative VC is Invalid, and no
  profile has an empty VC set or a resource failure. These include inhabited
  constrained carriers, NaN rejection, and literal/variable IEEE signed zero.
- The [editor repair receipt](https://github.com/erniecohen/dafny/actions/runs/38139091828)
  passes 9/9 tests: six repeated bitvector/cache cases, two floating member/cache
  cases and the OFF/ON/OFF project-option invalidation case. The earlier timeout
  was a real initialization null dereference: upstream dev registers value-type
  members before the supported cache is initialized. Initialization-local member
  tables are now merged before publishing the cache; a mere null guard would
  have lost those members on the next cache hit.
- The [current 5.1 metadata/structural repair receipt](https://github.com/erniecohen/dafny/actions/runs/38139988740)
  records actual exits zero for all five phases. Core tests pass 398/398 with no
  skips. The independent capture matches all 288 registered producer groups.
  Finite-support raw/lifted/SMT captures, boundary tests and structural checks
  pass; all four feasible false controls retain their sole intended errors.
  The strict resolver baseline matches all 96 executions: 45 accepted, 51
  rejected, no crashes and no timeouts. The input source is `b1ee24a09f464fecc45fc067eea0811a470d93b5`;
  the immutable compiler is the eac45 source above.

The [explicit native allocation/read interaction receipt](https://github.com/erniecohen/dafny/actions/runs/38141013445)
uses immutable eac45 and input source `ba41dd89c1c94d8d6942ab1364274a3beea3b682`
with Z3 5.1.0. All 32 profiles have their expected actual exits. Both resolvers
and lowering OFF/ON preserve the exact upstream diagnostic-location sequences
for Reads (20 verified, 17 errors), ReadsOnMethods (68 verified, 35 errors), and
Naked (19 resolver errors, zero verification declarations). Naked is resolution
coverage, not a positive proof. The remaining 28 profiles have nonempty
verification declarations and VC batches, containing only Valid/Invalid
outcomes without resource failures. The paired controls retain 19 Correct
capture-positive declarations, 1 Correct/11 Errors capture-negative,
2 Correct/2 Errors pure-callback, 14 Correct finite-support, and
1 Correct/1 Error lambda-allocation false control per profile. Summary errors
count failed assertions; they need not equal the number of Errors declarations.
The receipt is focused native evidence, separate from normal Lit acceptance.

The [producer change map](supported-to-dev-producer-changes.json) accounts for
280 to 288 groups: 39 additions, 31 removals and seven token changes, each with
its semantic role and reason. Permission-query replacements retain the default
formula while routing enabled queries through the supported helper. Added
families cover lambda handles, pure-arrow allocation, datatype/default-value and
element initialization. The current captured verifier file hashes match the
reviewed inventory.

### Complete comparison and proof cost

The [five-mode diagnostic comparison](https://github.com/erniecohen/dafny/actions/runs/38137406071)
uses the same combined input source `be862609d9147fb80dcea8df96eceddc722bb1ee`
for original-dev compiler `858e4bfbcf00fbf0146255c0a4b0efdfc4deb66f` and the
integrated compiler at be862. It distinguishes default OFF, lowering ON,
additional axioms OFF/ON, and both enabled. Later eac45 native controls provide
separate evidence for the float and editor repairs. Original-dev option
rejection on new supported features is explicitly recorded, not treated as a
failed proof of their semantics.

Each suite mode has 1183 rows. Original dev has 1126 nonempty JSON results and
8152 VC batches; each integrated mode has 1145 results and 8208 batches.
Unavailable parser/resolver cases (57 baseline, 38 integrated) remain explicit.
There are 39 changed default-OFF rows, 89 baseline-to-ON rows and 58 OFF-to-ON
rows. All 39 default-OFF changes have individual before/after values,
explanations and artifact links in the
[verdict change catalog](supported-to-dev-verdict-changes.json). This catalog
supports review; it does not automatically update an expected-verdict table.

| Mode | Suite RU | Std verified / errors / OOR | Std RU |
| --- | ---: | --- | ---: |
| Original dev, combined inputs | 1,407,402,974 | 7615 / 3 / 5 | 1,346,894,118 |
| Integrated OFF | 1,414,310,309 | 7615 / 2 / 6 | 1,527,125,784 |
| Lowering ON | 1,405,071,172 | 7646 / 2 / 7 | 1,517,864,809 |
| Additional axioms only | 1,453,891,224 | 7617 / 1 / 5 | 1,425,261,364 |
| Both enabled | 1,481,272,357 | 7648 / 2 / 5 | 1,377,617,064 |

Std has 2439 rows per mode: 2432 declarations and seven execution phases.
These complete diagnostic denominators do not turn failing proofs into passing
library gates. TargetSpecific ON profiles also report two unnecessary-requires
warnings in Duration (196,13 and 196,26), so those two phases exit 2 despite 132
verified declarations and no semantic errors. Warning-fatal acceptance remains
in force; no warning suppression or proof-source cleanup is included here.

The [fresh three-compiler source comparisons](https://github.com/erniecohen/dafny/actions/runs/38139429937)
show exact supported/integrated declaration outcomes and per-VC resource counts
for SchorrWaite, stages and SmallestMissingNumber in both resolvers. The whole
refreshed SchorrWaite resource failure and the added stages/SmallestMissing
limits are carried supported-line costs. They remain visible, with unchanged
ceilings and source inputs. The sequential-postcondition positive is separate
from those whole-file limits. GoodPost's added limit is also reproduced by both
supported and integrated compilers in all four switch profiles. Power and Base64
limits in default OFF are reproduced by the supported reference. The DivMod
MultiplyDivideLe error already occurs with original dev on combined sources.

### Normal CI and remaining work

The [normal PR172 CI](https://github.com/erniecohen/dafny/actions/runs/38139347333)
failed. Its eight diagnostic suite shards and build pass; registered regressions
pass 138/192 and fail 54. The finite-support and resolver harness failures were
repaired and independently passed in the 5.1 receipt above. The complete
registered gate still requires a fresh run after the
[recorded harness adaptations](supported-to-dev-harness-adaptations.json):
explicit legacy general-newtypes flags, .NET 10 fixture assembly paths, and
observed dev diagnostic wording preserving error locations, counts and exits.
Golden resource counts and semantic verdict baselines are handled separately;
the initial failed receipts remain available.

Existing normal CI uses Z3 4.16 for upstream test/library profiles, while native
trusted proof controls here use 5.1. Normal CI was triggered automatically by
creation of the draft PR. The owner has explicitly authorized additional 4.16
runs for dev CI and library repairs and the existing 4.12.1 historical reference
checks. Native correctness controls retain pinned 5.1. All prior failed receipts
remain available; these solver approvals do not change proof acceptance.

The initial library failures are historical evidence, retained below. The
[complete focused repair](https://github.com/erniecohen/dafny/actions/runs/38152168499)
passes all 16 selected client/helper profiles on both 5.1 and 4.16: 41 Correct
declarations and 819 Valid VC batches per solver. The final producer contract
has 670 Valid batches; its largest is 12,397,485 RU on 5.1 and 36,603,519 RU on
4.16, below the unchanged 100,000,000 limit. The arithmetic repair invokes
existing multiplication commutativity in the remainder assertions. Power uses
existing base-one and strict-monotonicity lemmas. Encoding and producer proofs
expose the exact prefix, padding, output, history and state-transition facts.
Every supporting helper body was verified; no theorem contract or resource
ceiling was weakened. The inherited `Encode1PaddingIs1Padding` helper has
`resource_limit 0`; this is disclosed rather than described as a bounded proof.

[All eight archives](https://github.com/erniecohen/dafny/actions/runs/38153632234)
were independently built with verification enabled at source `6e28abce2`, using
the immutable integrated compiler and approved 4.16 solver. The
[archive receipt](supported-to-dev-verified-archives.json) records 134 source
hashes, complete native declaration/VC results, archive/program digests and
manifests with both no-verify flags false. Std has 2,313 Correct declarations
and 8,144 Valid batches; Arithmetic has 646 Correct declarations and 1,219 Valid
batches at its explicit 5,000,000 ceiling. These native archive denominators
differ from normal Std verification's split-obligation summaries. The initial
Arithmetic missing-import failure is retained; its standalone dependency
closure was repaired explicitly. Later proof-source changes require fresh
archive regeneration: those historical bytes do not certify changed inputs.

The [complete four-profile native library capture](https://github.com/erniecohen/dafny/actions/runs/38153950653)
at `468c8dfd2` executes all seven original library phases, with 2,432 native
declaration rows per profile. Both 4.16 profiles pass: Std reports 7,838
verified and zero errors; target phases report 132, 132, 15, 35, 16 and 16.
5.1 OFF reports 7,836 verified, one error in `Power2.Lemma2To64` and one resource
failure in `DecodeValidUnpaddedPartialFrom1PaddedSeq` WF. 5.1 ON reports 7,836
verified, no errors and two resource failures: the same WF and
`DecodeValidEncode1Padding`. All target phases pass. The post-capture wrapper
then fails because `std.expected` received a list instead of a filename; actual
complete TSVs and outputs remain available. This is a failed diagnostic run,
not complete acceptance or a final identity receipt. The harness call is fixed.

The new proof bridges explicitly calculate `Pow2(64)` through the certified
power-product identity and prepare prefix/decoded-length facts in the checked
postcondition statement expression before slicing. The padded decoder client
uses the existing combined-partials lemma. Their
[fresh before/after and helper checks](https://github.com/erniecohen/dafny/actions/runs/38154816517)
pass all 11 client/helper profiles in all four combinations: 26 Correct
declarations and 83 Valid VC batches per profile set. The original 5.1
failures reproduce in the before capture. Complete closure and regenerated
archives for these final proof inputs remain required.

The [individual historical ON catalog](supported-to-dev-normal-on-baseline-changes.json)
accounts for 80 rows: 37 same-source language-default effects, 19 preserved
upstream source/coverage effects, 23 additions and one renamed fixture.
[Explicit paired language profiles](https://github.com/erniecohen/dafny/actions/runs/38153732399)
reproduce the combined default row for all 37 unchanged sources. Resolution-only
and zero-VC profiles are distinguished from proof evidence. IEEE negative
fixture diagnostic counts differ from their grouped Invalid VC batch counts;
both denominators are recorded. The initial counterpart merge omitted two
accepted #141 mode pairs. They are restored byte-for-byte, and their final
error lines return to 11 and 19. Historical ON/OFF rows from the omitted pairs
cannot be installed as final oracle rows. Their [four fresh native profiles](https://github.com/erniecohen/dafny/actions/runs/38154959043)
all pass strict expected-failure checks: actual exit4, one Valid and one Invalid
VC each, with the sole intended error at11 or19. Complete registered acceptance
with the restored RUN pairs is still required.

The draft integration is not ready to merge. Remaining gates are final
explained normal regression/verdict baselines, complete fresh normal exact-head
CI, full library closure and exact-source verified archives after the last
proof edit. Issues #170 proof-source repairs and #171 default-on review remain
separate. After a new owner review, use a GitHub merge commit so both supported
and #169 ancestry remain in dev. Do not squash that ancestry or push dev directly.

### Contained library repair evidence

The [three-compiler, two-solver diagnostic](https://github.com/erniecohen/dafny/actions/runs/38151543462)
uses the exact same unmodified combined `eac45d908` Std sources for each compiler.
With 4.16, original dev passes all six selected blocker profiles. Supported and
integrated compilers have identical outcomes and resource counts for all six:
one invalid DivMod assertion and four resource-failing profiles (two VCs in the
producer). With 5.1, original dev also has arithmetic and resource failures;
supported and integrated results again match. These comparisons attribute the
combined defaults to the carried supported implementation; they do not certify
the complete library.

The [focused repair diagnostic](https://github.com/erniecohen/dafny/actions/runs/38151778627)
at `877bdf9f0` verifies 11 selected client/helper profiles, 32 declarations and
640 VCs per solver, with the immutable integrated `eac45d908` compiler. Its
wrapper records failures and exits zero; its workflow status is diagnostic.

| Proof | 5.1 actual result | 4.16 actual result | Repair |
| --- | --- | --- | --- |
| MultiplyDivideLe/Lt | Correct | Correct | Existing commutativity helper inside remainder assertions |
| PowIncreases | Correct, 9,520 RU | Correct, 9,520 RU | Equal exponents, base one, or existing strict monotonicity lemma |
| EncodeBVLengthCongruentToZeroMod4 | Correct, 6,140,037 RU across isolated VCs | Correct, 2,454,312 RU | Existing exact-length lemma |
| ConcatenatedProducer.Invoke | 606 Valid VCs, maximum 29,408,706 RU | 605 Valid, final Ensures assertion OOR at 100,188,197 RU | Explicit singleton output and remaining-count transition |
| EncodeBVIsBase64 | OOR at 5,061,218 RU | OOR at 5,061,218 RU | Original validity proof, before prefix/padding repair |

Commutativity, modulo multiples, base-one power, strict power increase and exact
encoded-length helpers pass on both solvers. Later candidates expose the encoded
prefix/padding slices and the producer's full validity/freshness/history contract.
Their final focused success and historical archive closure are recorded above;
new proof inputs still require fresh complete closure. No theorem contract, resource ceiling, nonlinear arithmetic setting or
expected semantic verdict was weakened by these proof edits.
