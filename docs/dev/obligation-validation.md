# Obligation lowering validation status

The [requested review revisions](obligation-review-change-request.md) govern the
single-check replacement in PR #168. The supported semantic revision is now
`8e7f54505`, restoring source-local assertion well-formedness preparation and the
frozen initializer application's domain guard. It also preserves iterator yield frames and old heaps. Its focused native gates pass; complete acceptance remains pending.
The [current causal analysis](obligation-local-preparation-diagnosis.md) records
the rejected preceding source gates, controlled generated-Boogie experiments,
negative outcomes and multi-seed classification.

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

The [corrected iterator build](https://github.com/erniecohen/dafny/actions/runs/37719808505)
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
Complete baseline/default-off/enabled suite and standard-library comparisons
use this build and are in progress. Ordinary CI and independent soundness
review are still pending.

The preceding preparation matrices are now complete (216 suite observations
and 72 library observations). Their focused baseline/default-off vectors match
exactly, and the unchanged original succeeds enabled across three seeds. The
Power target fails enabled at every seed in that revision. Its higher checked
fuel layer and body result are retained; the [analysis](obligation-local-preparation-diagnosis.md)
records the stable error separately from partially successful seed movements.

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
