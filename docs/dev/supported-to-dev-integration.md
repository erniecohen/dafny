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
creation of the draft PR. Additional 4.16 submissions and PR updates that would
retrigger it are held pending the owner's narrow solver authorization. Existing
runs are preserved. Reading their completed logs is not a new solver run.

Both existing 4.16 library profiles (additional axioms OFF/ON) report 7617
verified, one error and five resource failures. The error is
`DivMod.LemmaMultiplyDivideLt`, assertion `(b * c) % b == 0` at 917. Resource
failures are two obligations in `ConcatenatedProducer.Invoke`,
`Base64.EncodeBVLengthCongruentToZeroMod4`, `Base64.EncodeBVIsBase64`, and
`Power.LemmaPowIncreases`. All six target-library profiles have no semantic
errors (132, 132, 15, 35, 16 and 16 verified). Solver authorization alone would
not make verified archive regeneration pass: these actual Std failures need
separate source/reference diagnosis and proof/resource resolution. They have
not been accepted by changing ceilings or hiding verification.

All eight library archives require verified regeneration from the combined
source and exact compiler; existing dev archives are historical inputs, not
combined-source certification. The prepared recipe requires
`hidden-no-verify=false`, complete nonempty declaration/VC evidence and explicit
archive/source/compiler/solver hashes. Arithmetic has an explicit 5,000,000 RU
ceiling. No archive is regenerated by relabeling its old manifest, and no
unverified replacement is proposed.

The draft integration is not ready to merge. Remaining gates are explained
normal regression and verdict baselines, complete fresh normal exact-head CI,
and exact-source verified library archives. Issues #170 proof-source repairs and
#171 default-on review remain separate. After a new owner review, use a GitHub
merge commit for this full-history integration so both supported and #169
ancestry remain in dev. Do not squash away that ancestry or push dev directly.
