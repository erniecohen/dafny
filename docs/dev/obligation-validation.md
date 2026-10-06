# Obligation lowering validation status

The change for [issue 100](https://github.com/erniecohen/dafny/issues/100)
is experimental and default off. It has not been merged or released.
The local arguments and producer classifications are in
[obligation-lowering.md](obligation-lowering.md).

## Exit-publication preservation: focused evidence only

The fuel/polarity rewrite in the previously tested candidate was withdrawn. The
preservation source restores the original expression and splitter fuel rules,
retains original checked cast and allocation formulas, and adds assertion-style
checking without replacing those formulas.

The added method-exit checks now retain the ordinary publication policy of
checked procedure ensures. Copying the split explicit-assert emitter had
suppressed earlier proved pieces in the continuation. The correction retains
every checked formula, fuel term, summary and inherited guard; explicit split
assertions keep their original check-and-forget behavior. Structural regressions
compare both the checked expressions and their command publication policies.

The [supported build and normal inventory gate](https://github.com/erniecohen/dafny/actions/runs/37427679277)
built both native platforms and the editor assembly, and passed all 39 focused
structural checks. The captured producer inventory matches the reviewed registry.
This build probe did not execute a solver.

Fresh focused native verification confirms the unchanged original issue #100,
AllLiteralsAxiom and GHC-MergeSort with the option enabled, at the unchanged
resource ceiling, default resolver, additional axioms off and seed zero. Both
former postcondition regressions are repaired in this scope. The generic solver
reason `incomplete quantifiers` did not identify their cause; the controlled
Boogie comparison isolated the command publication difference.

All 36 positive, negative and non-vacuity controls have their expected outcomes
across both resolvers, with additional axioms off and seed zero. The three focused
default-off programs retain their operational Boogie and complete named batch
outcome/resource multisets. This is focused evidence, not suite or library
acceptance, and does not establish uniform superiority of the enabled path.

The complete registered issue #100 gate also passed all 198 expected native
observations. This covers both resolver modes and both additional-axiom settings,
ordinary and isolated negative controls, and project/CLI option precedence. The
private launcher adapter records native resource logs and permits only the
registered negative-forall fixture's expected missing-trigger warning; its
proof inputs and expected results are unchanged. This is registered-regression
evidence, not full repository acceptance.

The current supported product is `0dff159b59c5980f6a32a2a60a256226dd04cb88`;
the build source is `b428bb7d34fdd7c57c5b2f1f1818a73ca90ffd51`. It reports
`4.11.0+fcb2042d.review.32a7e4ae`. The existing version calculation hashes
modified upstream files, so this change to files added by the feature retains
the prior suffix. Product/build commits and binary hashes identify the revision;
the version string alone is insufficient.

The development draft remains at the preceding preservation revision; its
[build](https://github.com/erniecohen/dafny/actions/runs/37394258403) and
[normal inventory gate](https://github.com/erniecohen/dafny/actions/runs/37395195252)
passed all 33 focused checks. The method-exit follow-up still needs development
porting and CI. The evidence below describes the superseded candidate and does
not validate the current source. Complete suite/library comparisons,
end-to-end invariance and independent soundness review remain pending.

## Source boundaries

* Development starts at `858e4bfbcf00fbf0146255c0a4b0efdfc4deb66f`.
* The supported port includes `review/4.11.0` through
  `ab210b78b50adb5a192542897f0a00cdb40f3c35`, including the intervening
  allocation and forall-substitution repairs.
* The tested supported candidate reports `4.11.0+fcb2042d.review.d160d1e3`.
  Its verifier sources last changed in `c086d89a76c70d4a1f176f5d62fa72b8e48a0754`.
  Subsequent changes corrected structural and generated fixtures, documented
  boundaries, and removed scratch-only workflows.

## Public build evidence

The [supported build and normal inventory gate](https://github.com/erniecohen/dafny/actions/runs/37360060667)
passed all 32 obligation structural checks. The
[Linux baseline and candidate builds](https://github.com/erniecohen/dafny/actions/runs/37362360235)
also completed, with all 32 candidate structural checks passing.
These runs built the editor regression assembly; they did not run a solver.
A separate current translation-only capture of the eight regression/cost
sources gives identical default-off operational Boogie in all eight cases,
retaining identifiers, attributes, guards, heaps, layers and representations;
only complete comment lines are excluded. The intentionally abstract programs
that cannot compile to a target language are recorded with that compilation
status. Capturing their Boogie does not count as solver acceptance.
Their successful diagnostic wrappers do not establish verifier or library acceptance.

The deterministic inventory gate runs without capture mode in normal CI.
The registered regression checks the unchanged original #100 source at its
16,000,000 resource ceiling, both resolver and additional-axiom settings,
project/CLI precedence, visible subset introductions, false controls and
normal/isolated negative checks. The larger diagnostic collection contains
154 cases; fixed-seed and isolation observations are recorded separately.

The [development ARM build probe](https://github.com/erniecohen/dafny/actions/runs/37371355240)
completed its build, all 30 branch-specific structural checks and the editor
regression assembly build. The preceding x64 attempts did not acquire a hosted
runner and executed no build step. Supported-port solver results do not
establish development-line verifier acceptance.

## Outstanding acceptance

The superseded full suite contains 1,161 verifier programs. Both additional-axiom
settings are complete: default-off matches all program verdicts and recorded
batch resource counts. The enabled outcome and diagnostic changes are listed
in [obligation-suite-comparison.md](obligation-suite-comparison.md).
The standard-library comparison is incomplete. The completed default-off
comparison with additional axioms disabled has no verdict change and identical
operational Boogie for all emitted programs. Resource counts vary in that run;
the remaining default-off setting and both enabled settings still need completion.
No expected-verdict table has been changed for this work. The new registered
program still needs its expected row captured through the ordinary CI artifact
procedure; the exact-baseline comparison does not replace that integration step.

The superseded enabled candidate had completeness regressions in
`AllLiteralsAxiom.calc_trick` and `GHC-MergeSort.sorted_sequences`; the current
method-exit follow-up repairs those two in focused verification. Its earlier
resource regressions in SchorrWaite, MinWindowMax, Regression16, Primes and
FlyingRobots still require fresh measurement.
Additional successful proofs cross the declared cost-review threshold.
These are opt-in regressions to investigate and report, not intentional
specification changes or reasons to replace expected verdicts.
The standard-library runner's declaration-order variability is documented in
`std-verdicts.py`; equality of off-mode verdicts is not a claim of exact resource
neutrality. Complete operational input comparisons accompany that audit.

The ordinary required CI and independent human soundness review remain merge
requirements. No issue closure, repository-green claim or full matching-closure
claim follows from the focused checks alone. AI assisted the implementation and
validation.
