# Obligation lowering validation status

The change for [issue 100](https://github.com/erniecohen/dafny/issues/100)
is experimental and default off. It has not been merged or released. Complete
native full-suite and standard-library comparisons of the preceding
exit-publication correction failed enabled completeness/performance and controlled
library cost acceptance. Fresh complete comparisons of the reveal-scope
correction are in progress. The local arguments and producer classifications are in
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

## Previous complete native comparisons

The following results describe the preceding exit-publication product. Fresh
complete suite and library gates for the current reveal-scope correction are
in progress, retaining the complete existing scope and both axiom settings.

Both additional-axiom settings are complete for each of baseline, candidate with
the option off, and candidate with it on. The complete existing runners retain
source RUN options, committed project settings, warning policy, resource ceilings,
batching and declaration-order behavior. No global fuel/resource increase, new
solver seed or batching option is used to obtain these results.

With the option off, all 1,161 suite programs match the baseline and committed
expected verdicts. Complete named declaration and batch outcome/resource entries
match in both settings. Expected parser/resolver and command-line negatives stay
in the program denominator. No successfully verified proof lacks its resource log.

The standard-library comparisons cover the main Std project and all six
target-specific parts. All 2,190 declaration verdicts and seven run verdicts match
the baseline and committed expectations with the option off. All emitted
operational Boogie matches after excluding complete comment lines. The baseline
itself has expected errors/resource exhaustion; matching it does not mean every
library lemma verifies.

Default-off library resources vary under the runner's existing declaration-order
behavior documented in `std-verdicts.py`. Complete operational-input and command
comparisons accompany the audit, but exact controlled cost compatibility remains
unaccepted. A late capture assertion incorrectly expected one Boogie file per
part; the completed native evidence was audited for all emitted modules instead.
The original capture failures are retained separately. No completed proofs were
rerun to correct that postprocessing check.

AllLiteralsAxiom and GHC-MergeSort pass in both complete enabled suite settings.
The enabled suite nevertheless introduces resource exhaustion in other formerly
successful declarations, and some still-successful cases require cost review.
The enabled library introduces both resource exhaustion and proof errors. The
four formerly Correct declarations now reporting Errors are
`Std.Arithmetic.Power.LemmaPowSubtractsAuto`, `Std.Base64.EncodeDecodeRecursively`,
`Std.Collections.Seq.LemmaFilterDistributesOverConcat` and
`Std.Collections.Seq.WillSplitOnDelim`. Native diagnostics identify a failed
postcondition, calculation steps and a function precondition.
[Failure examples and controlled diagnosis](obligation-failure-examples.md)
identify a definition-visibility regression in the three hide/reveal cases.
The preceding opt-in method-body `ReturnPosition` override added pops after reveal hints
and before existing checks. Native pruned declarations confirm the defining
Filter axiom disappears from both failed calculation checks; retaining the
relevant reveals in the outer proof scope restores all three native declarations.
Those witnesses were diagnostic library edits. The current product instead preserves legacy scope, and all three unmodified declarations verify in focused native validation. Complete acceptance remains pending.
The quantified power failure is separately isolated to the added second-clause
exit package; its exact solver instantiation mechanism remains unresolved.
Complete checking-environment preservation remains unaccepted. Other
resource-limited library declarations become successful.
No baseline Errors declaration becomes Correct in these comparisons.

Every changed suite program and library declaration verdict has been reviewed
in [obligation-suite-comparison.md](obligation-suite-comparison.md). Expected
verdict tables have not been changed. These regressions are not specification
changes or grounds for broadly replacing expectations. Complete comparisons
do not establish uniform superiority of the enabled path.

## Outstanding acceptance

Enabled completeness/performance, controlled default-off library cost,
end-to-end invariance and independent human soundness review remain unaccepted
or pending. Ordinary required CI and complete regression integration, including
the new registered program's expected row through the ordinary CI artifact
procedure, remain merge requirements. The exact-baseline comparisons do not
replace that integration step. The pre-existing false self-postcondition is
tracked separately in issue 166.

The development draft remains at the preceding preservation revision; its
[build](https://github.com/erniecohen/dafny/actions/runs/37394258403) and
[normal inventory gate](https://github.com/erniecohen/dafny/actions/runs/37395195252)
passed all 33 focused checks. The current method-exit and reveal-scope corrections still need
development porting and CI. Supported-port native results do not validate the
development line.

This remains a draft. No issue closure, repository-green claim, full
matching-closure claim, merge or release follows from these results. AI assisted
the implementation and validation.
