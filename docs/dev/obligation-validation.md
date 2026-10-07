# Obligation lowering validation status

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
