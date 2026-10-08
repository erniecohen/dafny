# Obligation lowering validation status

**Current candidate under validation:** semantic/product `fe6ddd1ba` keeps the
existing leading can-call command and places it after accepted certified pure
preparation only when it reads no assigned or havoced variable. Unsupported
fragments retain the previous order. Actual checks, expression/fuel state,
certified facts, source state and reveal scopes are retained. The
[native support-order diagnosis](obligation-support-order-diagnosis.md) completes
36 observations for the preceding `a709df226` product with faithful native
controls, but does not validate this new compiler candidate. The fresh [normal build](https://github.com/erniecohen/dafny/actions/runs/37808373912)
passes all ten actual stages: 95 obligation tests in each mode, 373 core tests,
the exact 287-group inventory, both platform bundles/probes and editor
compilation. Focused current-candidate native verification remains pending. No whole-suite
iteration or development port is being performed.

**Newest correction:** semantic/product pin `a709df226` replaces empty
certified-WF branches by `G ==> G`, retaining the guard terms without an empty
control-flow split. Its [build](https://github.com/erniecohen/dafny/actions/runs/37781426875)
passes all ten actual stages, both platforms and probes, the editor build,
92 structural observations in each mode, the exact 287-group inventory and all
370 core unit tests. Its [focused native result](obligation-guarded-preparation-focused.md)
completes 68 observations: Power verifies all three seeds in both axiom settings,
with its two actual exit checks byte unchanged. All observed specification-WF
checks, sixteen positive controls and four genuine invalid controls behave as
required. Five suite targets still have resource failures at selected seeds;
targeted enabled acceptance remains false. The earlier baseline evidence is
explicitly reused; only six default-off Power observations are newly executed
and match that baseline exactly. No whole-suite or whole-library run was launched.

**Preceding measured candidate:** semantic correction `338b82396`, product pin
`69dc750b5`, build `861472a9d` passes all ten build stages, 92 structural
observations in both modes, the independent 287-group inventory and all 370 core
unit tests. Its [focused known-regression diagnostic](obligation-guarded-preparation-focused.md)
completes 146 observations: selected default-off vectors agree exactly, but five
of six suite targets still fail at some seeds and Power fails at every seed.
Original issue/subset positives and genuine invalid negative controls retain their
expected results. No new whole-suite or whole-library run was launched.
The complete gates below describe the preceding product `072175269`; they cannot
validate this new candidate. Keep both PRs as drafts and retain the owner's
approval requirement before porting to the development line.

The [requested review revisions](obligation-review-change-request.md) govern the
single-check replacement in PR #168. The preceding semantic correction is `98b0dd509`, compiled product `072175269`,
built from `8d23f3fc8`. Its structural, registered/paired and normal registered/editor
integration gates pass. Its [complete current suite/library gates](obligation-current-complete-gates.md)
finish under both axiom settings and reject enabled acceptance. Exact default-off
suite verdict/resource compatibility passes; strict default-off library resource
equality remains rejected despite unchanged verdicts and operational Boogie.
The [current causal analysis](obligation-local-preparation-diagnosis.md) and
[query ordering diagnosis](obligation-default-off-query-diagnosis.md) distinguish
actual semantic preparation repairs, explained stable solver failure and limited
ordering evidence. The older revisions below remain historical evidence only.

The [preceding 63-control build](https://github.com/erniecohen/dafny/actions/runs/37713397830)
compiled both native platforms and the editor, passed all structural controls and
the normal reviewed 284-group producer-inventory gate. Native gates of its
semantic revision `2ad5d25a5` still rejected the original issue 100 and quantified
initializer positives. All paired negative observations retained invalid VCs.
Focused default-off comparisons matched their baseline outcome/resource vectors;
complete suite and standard-library acceptance remain pending.

The [preceding preparation build](https://github.com/erniecohen/dafny/actions/runs/37717887176)
compiled both native platforms and the editor. All 71 structural controls and
the normal reviewed 287-group inventory gate passed. These include local
quantifier WF, one postcondition proof, inherited preparation guarding,
initializer domain preservation and nested-existential fuel identity with an
immediate assertion under both resolvers. Native version:
`4.11.0+fcb2042d.review.7ebd2a20`; build source: `ece2a91fb`.

The complete registered native gate now passes all 242 observations using that
build and Z3 5.1.0, including the unchanged original issue 100, subset and
range-bound collection positives, reveal-scope controls, negative controls and
project/CLI precedence. This is registered acceptance, not suite/library or
independent soundness acceptance. The complete paired trial then rejected an iterator-yield translation error:
local preparation referenced an undeclared method reads frame. Its first 476
observations passed, but the full gate was incomplete. The frame/old-heap repair
in `126dfd96b` and its body-specific resolution/typechecking controls require a
fresh complete native gates; the corrected build is recorded below. Completed
earlier multi-seed/library inputs remain diagnostic evidence of their recorded revision. Required complete suite
and standard-library gates remain pending.

The [preceding corrected iterator build](https://github.com/erniecohen/dafny/actions/runs/37719808505)
passes both native builds, both Boogie probes, the editor build and all 73
structural controls in capture and normal inventory modes. The reviewed
287-group inventory matches. Native version:
`4.11.0+fcb2042d.review.6c3dfb23`; build source: `2165f4084`.
Its complete registered gate passes all 242 observations. Its complete paired
gate passes all 656 observations with both resolvers and both axiom settings;
all 232 expected-negative observations contain genuine invalid verification
conditions. These include the repaired iterator context and initializer domain,
original and subset positives, method/function preconditions, old allocation
and reveal controls. This is focused acceptance, not repository acceptance.
Complete baseline/default-off/enabled comparisons of that build remain
diagnostic evidence of the iterator revision. They cannot validate the later
opaque existential correction. Ordinary CI and independent soundness
review are still pending.

The preceding preparation matrices are now complete (216 suite observations
and 72 library observations). Their focused baseline/default-off vectors match
exactly, and the unchanged original succeeds enabled across three seeds. The
Power target fails enabled at every seed in that revision. Its higher checked
fuel layer and body result are retained; the [analysis](obligation-local-preparation-diagnosis.md)
records the stable error separately from partially successful seed movements.

The current opaque correction preserves the consumed existential fuel state
through guarded lowering. Recursive-predicate equality controls cover method
and opaque contracts under both resolvers. Its [77-control build](https://github.com/erniecohen/dafny/actions/runs/37724174182)
passes all nine recorded stages, including both native platforms, both probes,
the editor and both capture/normal structural runs. The normal 287-group
inventory matches. Build source: `d3dc53c8e`; native version:
`4.11.0+fcb2042d.review.6c3dfb23`. Exact artifact identities distinguish this
build from the preceding build with that same reported version. Fresh native
gates are required; the [diagnosis](obligation-local-preparation-diagnosis.md) records the
actual one-layer mismatch and the separate Power causal replay.

## Complete library evidence of the preceding iterator revision

The preceding iterator revision `8e7f54505`, build `2165f4084`, completes the
entire seven-part standard-library gate in baseline, default-off and enabled
modes under both additional-axiom settings, using Z3 5.1.0. Each mode contains
2,190 declaration rows and seven run rows. Baseline/default-off have 7,724
recorded batches, and enabled mode has 8,994. No run is truncated or accepted
from a missing log.

Every default-off verdict and every emitted operational Boogie program matches
its corresponding baseline in both settings. Named batch resource counts still
differ. The strict default-off compatibility gate therefore rejects both runs;
Boogie-text identity is not proof of identical solver queries or proof cost.
The focused exact comparisons recorded above cannot substitute for this full
scope. The source retains its legacy explicit-assertion lowering branch when
the feature is off. The cause of the full-scope resource differences remains
under investigation; no tolerance or baseline expectation is changed.

Enabled mode has 14 newly failing declarations with additional axioms off and
13 with them on, plus cost movements requiring review in each setting. No
baseline `Errors` declaration becomes `Correct`. The five new `Errors`
declarations shared by both settings are:

- `DivMod.LemmaDivByMultiple`: the existing callee result does not establish
  the final division postcondition in the new exit VC.
- `Power.LemmaPowSubtractsAuto`: the second quantified postcondition remains
  invalid in the controlled replay described in the diagnosis.
- `BulkActions.BatchArrayWriter._ctor`: local postcondition preparation reports
  an insufficient reads clause for `Valid()`.
- `Collections.Seq.LemmaMapDistributesOverConcat` and
  `LemmaMapPartialFunctionDistributesOverConcat`: local postcondition
  preparation reports function preconditions that depend on labeled entry
  requirements. Their expression-local reveals do not remain globally active.

The remaining newly failing declarations exhaust their original resource
ceilings. These include division/modulo lemmas, Base64 controls, JSON sequence
well-formedness and a producer invocation. Both complete runs reject enabled
acceptance. Current source-level controls will compare these errors with actual
immediate assertions, without changing library proofs or adding hints.

The full-suite attempts of this preceding revision were interrupted before all
three modes completed; they are incomplete execution evidence. Fresh complete
suite and standard-library gates for `58d8ac112` are queued separately and do
not inherit either acceptance or rejection from the preceding native build.

## Current native focused acceptance

The current fuel revision `58d8ac112`, built from `d3dc53c8e`, completes the
registered gate's 242 observations and the paired gate's 656 observations with
Z3 5.1.0. Both resolver modes and both additional-axiom settings are covered;
all 232 expected-negative observations have genuine invalid verification
conditions. Original, subset, method/function precondition, allocation,
initializer, iterator and retained-reveal controls pass at their original
ceilings. These are focused acceptance results, not complete repository
acceptance.

A separate four-observation inspection of the current native emitted Boogie
confirms the recursive opaque equality's checked formula matches the immediate
assertion under both resolvers, including fuel layers and quantifier triggers.
The comparison normalizes fresh binder names, whitespace and the leading
command's subsumption attribute only. It establishes translation identity for
this control, not verification of the otherwise unproved recursive proposition.

Fresh complete suite and library comparisons retain the unchanged baseline
proof files, gate runners, expected tables and limits. Those results and the
current resource/error seed classifications remain pending. The preceding
library rejection above is not treated as current acceptance.

The [first repair build](https://github.com/erniecohen/dafny/actions/runs/37717196930)
compiled both platforms and the editor but rejected the inherited fixture's
missing semicolon and outdated registry. Both have been corrected; its partial
structural result is superseded by the current normal gate.

The [preceding diagnostic build](https://github.com/erniecohen/dafny/actions/runs/37711275949)
passed all 61 earlier structural controls, including exact old-array allocation
identity under both resolvers. Its normal registry was also outdated. The cast
control confirms one enabled target check versus two legacy checks. The structural
fingerprint retains lexical binder and binary/unary opcode identities, with
negative controls for shadowing, implication/conjunction and addition/subtraction.
These builds execute no native proofs.

The [foundation build](https://github.com/erniecohen/dafny/actions/runs/37672843215)
completed both native builds and all 46 structural/inventory controls. This includes
the lexical binder correction and its positive/negative shadowing controls. The
seven direct fixed-Boogie free-contract/negative controls have their expected
verifier summary counts under Z3 5.1.0. These establish interface encoding only;
they do not establish Dafny source, suite or library acceptance.

The reports below describe the preceding additive implementation and remain
historical evidence for triage. They do not validate the single-check replacement.
The [revision checklist](obligation-revision-checklist.md) tracks current evidence.
Porting implementation to PR #169 requires explicit owner approval first.

## Certified contract preparation candidate

The new candidate `98b0dd509` implements the
[certified-preparation argument](obligation-certified-preparation.md). Its [build](https://github.com/erniecohen/dafny/actions/runs/37731304101)
passes all ten recorded stages, including both native platforms, both probes,
the editor and the complete 363-test core unit suite. All 85 structural
observations pass in capture and normal modes; the reviewed 287-group inventory
matches independently. Compiled product pin: `072175269`; build source:
`8d23f3fc8`; native version: `4.11.0+fcb2042d.review.77d7a235`.
The complete registered native gate passes all 274 observations. The complete
paired gate passes all 676 observations; every one of the 244 expected-negative
observations contains a genuine invalid VC. The conditional-reads caller control
independently confirms that the callee's specification-WF passes while the
caller's false requirement fails in all four registered modes. These are focused
acceptance of this exact candidate, not full-suite acceptance.

The fresh 72-observation library matrix completes with exact focused default-off
outcome/resource equality in both axiom settings and all three seeds. Filter
concatenation, delimiter splitting and encode/decode pass enabled; Power
subtraction remains an enabled error at every seed. Its checked formula retains
the higher assertion fuel layer. Generated division Boogie uses the same
arithmetic wrappers in the legacy procedure postcondition and the new local
check; a wrapper change does not explain that preceding failure. The fresh
216-observation suite/original matrix completes with exact focused baseline/off
batch vectors. The original issue succeeds enabled across all seeds, resolvers
and axiom settings. NoTypeArgs and ExtensibleArray succeed enabled at all tested
seeds; SchorrWaite, Primes and MinWindowMax have partly successful vectors in both
baseline and enabled modes and are classified as solver/resource variance under
S13. FlyingRobots retains resource exhaustion at all enabled seeds while baseline
is partly successful, so that resource movement remains open.

The complete 112-observation actual-source diagnostic verifies all four untouched
originals (division, writer constructor and both map concatenations) enabled
across three seeds. All 36 false controls retain invalid VCs. Immediate body
assertions still fail, reflecting their additional body WF obligations. The
current certified support repairs the original contracts without duplicating
those obligations or assuming the actual contract's truth. Actual solver-query
files are present for four same-host MembersSpec repeats. The [query ordering
diagnosis](obligation-default-off-query-diagnosis.md) establishes a limited cause
for that sample's resource differences, including an unchanged-baseline repeat;
strict complete-library resource acceptance is still pending.

A complete 48-observation current generated-Boogie Power replay retains both
mandatory exit checks. The enabled control fails at every seed in both axiom
settings. Removing the second clause's WF materialization, or placing WF in an
isolated diagnostic branch, makes all seeds verify. Removing only the first
clause's WF or only the certified divisor assumptions does not. False exit
controls remain invalid. Omitting relevant preparation is excluded from the
product; this is an explained solver-search effect, not a repair or an
acceptance claim. The fresh [complete suite and standard-library comparisons](obligation-current-complete-gates.md)
now finish with unchanged proof bytes, hints, ceilings, batching and expected tables.
Both suite runs preserve every default-off verdict/resource vector. Both library
runs preserve default-off verdicts and operational Boogie, but strict resource
comparison rejects them. Enabled suite mode has six resource failures in each
setting; the complete source remains negative for the ill-formed specification
whose body VC becomes valid. Enabled library mode has eleven/ten newly failing
declarations, including stable Power subtraction. All four affected-declaration
failure/cost seed matrices finish, covering 648 observations. The [seed record](obligation-current-seed-classification.md)
preserves the recovered library matrix's platform boundary, four full-control
movements, candidate-only failures and stable resource regressions. These
diagnostics do not relax any raw full-gate rejection. The subsequent statement-expression
reads hypothesis is rejected by its independent specification-WF checks; no
compiler change follows from that incomplete diagnostic.

The actual normal registered issue-100 test and editor option-invalidation test
also pass, each with exactly one executed/passed test. The linked report records
both the hosted harness and exact native compiler/server assembly provenance.
Independent soundness review and overall acceptance remain open.

Eight additional structural observations cover
independent specification-domain proofs, a mandatory false first postcondition,
conditional-reads anti-vacuity and recursive fuel identity after reads traversal.
Five new native fixtures extend the complete registered scope to 274 observations
and paired scope to 676, including 244 expected-negative observations.
The preceding evidence below cannot validate this candidate.

## Completed native evidence of the preceding fuel revision

Product `58d8ac112`, build `d3dc53c8e`, completes all three modes of both
additional-axiom settings with Z3 5.1.0. Each full-suite mode contains 1,161
program rows and 6,909 declarations. Baseline/default-off have 7,605 batches;
enabled has 7,695. Default-off verdicts and every recorded resource vector
match exactly in both settings. Expected parser/resolver and command-line
negatives remain in the denominator. Enabled mode has 14 newly failing
declarations with additional axioms off and 15 with them on. No baseline
`Errors` declaration becomes `Correct`. Both enabled gates reject acceptance.
The new correctness-declaration errors include `ReadsOnMethods.OnlySpecReads`
and `InForall` in `github-issue-39.dfy`. Both already have failed specification-WF
checks in baseline/default-off; the complete source programs are expected
negatives in all modes. These declaration movements are not newly rejected
valid source programs. The other new failures exhaust existing resource ceilings.
All affected examples remain in the complete comparison evidence.

The completed suite failure inventory is:

| Source / declaration | Enabled result | Axiom settings |
| --- | --- | --- |
| `dafny0/ReadsOnMethods.dfy` / `OnlySpecReads` | `Errors` | both |
| `dafny1/ExtensibleArrayAuto.dfy` / `ExtensibleArray.Set` | `OutOfResource` | both |
| `dafny2/MinWindowMax.dfy` / `MinimumWindowMax` | `OutOfResource` | on only |
| `dafny2/SnapshotableTrees.dfy` / `SnapTree.Iterator.MoveNext` | `OutOfResource` | both |
| `dafny2/SnapshotableTrees.dfy` / `SnapTree.Iterator.Push` | `OutOfResource` | both |
| `dafny4/FlyingRobots.dfy` / `FormArmy` | `OutOfResource` | both |
| `dafny4/NumberRepresentations.dfy` / `dec` | `OutOfResource` | both |
| `dafny4/Primes.dfy` / `AltPrimeDefinition` | `OutOfResource` | both |
| `dafny4/Primes.dfy` / `Composite` | `OutOfResource` | both |
| `dafny4/Primes.dfy` / `RemoveFactor` | `OutOfResource` | both |
| `dafny4/UnionFind.dfy` / `M3.UnionFind.JoinMaintainsReaches1` | `OutOfResource` | both |
| `git-issues/github-issue-2174.dfy` / `CommLShiftUpCast` | `OutOfResource` | both |
| `git-issues/github-issue-2174.dfy` / `MaskedCommLShiftUpCast` | `OutOfResource` | both |
| `git-issues/github-issue-2174.dfy` / `PushUpCastIntoLShift` | `OutOfResource` | both |
| `git-issues/github-issue-39.dfy` / `InForall` | `Errors` | both |


Both preceding library gates also finish all seven parts and 2,197 rows per mode.
Default-off preserves every verdict and operational Boogie program, but fails
strict resource equality. Enabled mode retains the five errors listed above
and has 14 new failures with additional axioms off and 13 with them on. Cost
movements require review. No expected verdict, hint or ceiling has changed.
An unchanged-baseline repeat itself has resource movements; a separate pair
of same-binary default-off repeats also moves resources. This establishes
variance but does not prove that baseline/candidate solver queries are identical
or discharge the fork's strict library cost gate. The requested solver-query
capture produced no query files, so it supplies no query-identity evidence.

The preceding 216-observation suite/original matrix and 72-observation library
matrix complete. Their focused default-off outcome/resource comparisons match.
The original issue succeeds enabled across three seeds under both resolvers
and axiom settings. The three repaired library targets pass across all three
seeds; Power subtraction retains a stable enabled error. Partially successful
seed vectors remain solver/resource variance under S13, rather than stable
correctness regressions.

A complete 112-observation actual-source diagnostic covers division, the writer
constructor and both map-concatenation failures. Untouched originals pass in
baseline/default-off and fail enabled at all three seeds. Adding an immediate
assertion also fails at all three seeds in all three modes. False controls
retain invalid VCs. In particular, the constructor's body assertion adds a
reads-frame requirement absent from specification postcondition WF. Replaying
those WF assertions added obligations, beyond the intended local support.
The [declared-contract preparation argument](obligation-certified-preparation.md)
records the correction and required anti-vacuity controls before
implementation. The new candidate is not yet validated or accepted.

The [expanded compiler-unit probe](https://github.com/erniecohen/dafny/actions/runs/37727158566)
retains the nine passing structural/build stages. Its additional complete core
unit stage reports 354 passed and one failed of 355: `RoundTripCurrentVersion`
cannot find Z3 in the hosted test environment and its missing-solver diagnostic
throws. The wrapper's success does not make this stage pass. This source-only
probe performs no verification and does not replace native proof gates.

## Historical additive implementation

The change for [issue 100](https://github.com/erniecohen/dafny/issues/100)
is experimental and default off. It has not been merged or released. Complete
native suite and standard-library comparisons of the current reveal-scope
correction reject enabled completeness/performance and controlled default-off
library cost acceptance. The local arguments and producer classifications are in
[obligation-lowering.md](obligation-lowering.md); the complete qualitative verdict
review is in [obligation-suite-comparison.md](obligation-suite-comparison.md).

## Current construction

The fuel/polarity rewrite in the preceding candidate was withdrawn. The current
source restores the original expression and splitter fuel rules, retains original
checked cast and allocation formulas, and adds assertion-style checking without
replacing those formulas.

The added method-exit checks retain the ordinary publication policy of checked
procedure ensures. Copying the split explicit-assert emitter had suppressed
earlier proved pieces in the continuation. The correction retains every checked
formula, fuel term, summary and inherited guard; explicit split assertions keep
their original check-and-forget behavior. Structural regressions compare both
the checked expressions and their command publication policies.

Method and forall proof bodies now retain the legacy `ReturnPosition` context.
The opt-in path no longer inserts scope pops after terminal reveal hints.
Previously retained reveals stay available to existing and appended checks;
ordinary nonterminal scope boundaries are retained. No original check, summary,
fuel term, library proof or resource ceiling was removed or weakened.

The current supported product is `c515251c9a5f06490591defa3df3ef86e1b8d175`;
the build source is `1cb6a03bb14d2d96d57a1e6ac9f1c5f8f27935d2`, reporting
`4.11.0+fcb2042d.review.c38abced`. The supported baseline remains
`ab210b78b50adb5a192542897f0a00cdb40f3c35`. Later registered-test/report edits
change no product source. The preceding complete comparison measured product
`0dff159b59c5980f6a32a2a60a256226dd04cb88`, build
`b428bb7d34fdd7c57c5b2f1f1818a73ca90ffd51`, version
`4.11.0+fcb2042d.review.32a7e4ae`; it does not validate the current product.

## Build and registered evidence

The [supported build and normal inventory gate](https://github.com/erniecohen/dafny/actions/runs/37494376061)
built both native platforms and the editor assembly, and passed all 43 focused
structural checks. These compare terminal/nested calculation reveal scope,
forall proof scope and ordinary nonterminal boundaries under both resolvers.
The independently captured producer inventory matches the reviewed registry.
This build probe did not execute a solver.

Focused native verification confirms the unchanged original issue 100,
AllLiteralsAxiom and GHC-MergeSort with the option enabled, at the unchanged
resource ceiling, default resolver, additional axioms off and seed zero. Both
former postcondition regressions are repaired in this scope. The generic solver
reason `incomplete quantifiers` did not identify their cause; the controlled
Boogie comparison isolated the command publication difference.

All 36 positive, negative and non-vacuity controls have their expected outcomes
across both resolvers, with additional axioms off and seed zero. The three focused
default-off programs retain operational Boogie and complete named batch
outcome/resource multisets.

The complete registered issue 100 gate passed all 222 expected native
observations. This covers both resolver modes and both additional-axiom settings,
ordinary and isolated negative controls, and project/CLI option precedence. The
native capture adapter permits only the registered negative-forall fixture's
expected missing-trigger warning. The added terminal-reveal fixture verifies
under both resolvers, both additional-axiom settings and both option settings.
Existing reveal-isolation and hide-after-reveal negatives also run with ordinary
and isolated assertions. Their expected failures are retained.
These controls and structural checks do not replace independent soundness review.

The focused native library gate completes all 24 observations: the unchanged
Filter, Split and Base64 declarations verify in baseline, default-off and enabled
modes under both additional-axiom settings; the separate power control verifies
in baseline/default-off and retains its enabled failure. All eight focused
baseline/default-off batch/resource comparisons match. Actual native pruned
declarations and solver inputs are retained with these results. These are
complete focused checks, not full-suite acceptance.

## Complete native suite and standard-library comparisons

Both additional-axiom settings finish all three modes: baseline, candidate with
the feature off, and candidate with it on. Complete existing runners retain
source RUN options, project files, warning policy, resource ceilings, batching
and declaration order. No global fuel/resource increase, new seed or batching
option is used. Each comparison gate completes normally and rejects enabled
product acceptance.

With the feature off, all 1,161 suite programs match the baseline and committed
expected verdicts in both settings. All 6,909 named declarations and complete
batch outcome/resource entries match. Expected parser/resolver and command-line
negatives remain in the denominator. Enabled mode introduces six resource
failures with additional axioms off and five with them on. AllLiteralsAxiom and
GHC-MergeSort pass; some other formerly resource-limited declarations now pass.
Still-successful declarations also require cost review. No baseline Errors
declaration becomes Correct.

The library comparisons retain all 2,190 declaration rows and seven project/
target-specific run rows per mode. The unchanged `EncodeDecodeRecursively`,
`LemmaFilterDistributesOverConcat` and `WillSplitOnDelim` verify enabled in both
settings. The inverse `DecodeEncodeRecursively` also verifies, from baseline
resource exhaustion. `LemmaPowSubtractsAuto` still reports Errors. Other
formerly successful declarations exhaust their original ceilings, and some
successful declarations require cost review.

Every candidate default-off library verdict matches the committed expectation.
The initial additional-axioms-on baseline proves `Objects.BracketedToObject`,
while default-off exhausts its ceiling. The required unchanged complete repeat
returns that baseline declaration to the expected OutOfResource result and all
baseline/default-off verdicts match. Complete operational Boogie matches
between baseline/off and across the original and repeated runs, excluding only
complete comments. This confirms baseline variation without a source or binary
change. Resource entries still vary under the runner's existing declaration
order; controlled default-off library cost remains unaccepted. The initial
enabled library comparison has seven fresh-baseline failure movements in this
setting, including that varying declaration; the repeat has six. With additional
axioms off, the library has five fresh-baseline failures.

[Failure examples and native diagnosis](obligation-failure-examples.md) retain
the causal visibility evidence and the surviving quantified power interaction.
Every changed program/declaration verdict is reviewed in
[the complete comparison](obligation-suite-comparison.md). No expected verdict
was replaced. These results do not establish uniform superiority, complete
checking-environment preservation or end-to-end matching invariance.

## Focused diagnosis after the complete comparisons

The [six-case native causal investigation](obligation-failure-examples.md#focused-native-diagnosis-of-the-six-suite-regressions)
uses unchanged complete-file captures, matched baseline/current seed runs and
controlled native omissions under both additional-axiom settings. Current
product versus scratch instrumentation with no omissions matches operational
Boogie and target assertion-batch outcome/resource vectors. The study localizes
new exit-package, exit-summary and call-site-package contributions to the six
remaining suite regressions. No original assertion, contract, resource ceiling,
fuel setting or reveal was changed in the product. Native omissions remain
scratch diagnostics; unchecked-summary variants are excluded as proof evidence.
Precise solver instantiation chains and a product correction retaining the
intended additional checking power remain open. This is focused diagnosis,
not another full-suite gate or completed acceptance.

## Outstanding acceptance

Enabled completeness/performance, controlled default-off library cost,
end-to-end invariance and independent human soundness review remain unaccepted
or pending. Ordinary required CI and complete regression integration, including
the new registered program's expected row through the ordinary CI artifact
procedure, remain merge requirements. The exact-baseline comparisons do not
replace that integration step. The pre-existing false self-postcondition is
tracked separately in issue 166.

The development draft now ports both method-exit publication and reveal-scope
corrections. Its [build and normal inventory gate](https://github.com/erniecohen/dafny/actions/runs/37503850171)
passed all 41 structural checks. The development product is
`48a6f13236a32bb4f7c4b1543c7f69b161d0b104`; its build source is
`6270d888a60cd7a6714f8cc99885487cc6047f84`. This probe executed no solver.
Supported-port native results do not validate development-line verification.

This remains a draft. No issue closure, repository-green claim, full
matching-closure claim, merge or release follows from these results. AI assisted
the implementation and validation.
