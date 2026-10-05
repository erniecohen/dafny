# Obligation lowering validation status

The change for [issue 100](https://github.com/erniecohen/dafny/issues/100)
is experimental and default off. It has not been merged or released.
The local arguments and producer classifications are in
[obligation-lowering.md](obligation-lowering.md).

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

The current full suite contains 1,161 verifier programs. Both additional-axiom
settings are complete: default-off matches all program verdicts and recorded
batch resource counts. The enabled outcome and diagnostic changes are listed
in [obligation-suite-comparison.md](obligation-suite-comparison.md). The complete
standard-library comparison is still in progress.
No expected-verdict table has been changed for this work. The new registered
program still needs its expected row captured through the ordinary CI artifact
procedure; the exact-baseline comparison does not replace that integration step.

Focused enabled checks have identified completeness regressions in
`AllLiteralsAxiom.calc_trick` and `GHC-MergeSort.sorted_sequences`, and resource
regressions in SchorrWaite, MinWindowMax, Regression16, Primes and FlyingRobots.
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
