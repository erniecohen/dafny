# Decreases-order comparison and proof annotations

The corrected comparison is the default for the soundness repair in
https://github.com/erniecohen/dafny/issues/264. The hidden
`--legacy-decreases-order` switch supplies the old side of this comparison.
The [component argument](decreases-order.md) explains the changed formulas.

## Reproductions

The original product and candidate legacy mode accept the varying-arity real
reset (2 verified, 0 errors), the complete derivation of `false` (6 verified,
0 errors), the equal-rank sequence/datatype override (1 verified, 0 errors),
and the isolated M2 reset with `--modular-termination` (3 verified, 0 errors).
The corrected default rejects them with respectively 1, 5, 0, and 2 verified
and one error each. In the `false` example the recursive definition fails its
termination check; the program as a whole is rejected.

The M2 fixture repeats the trait precondition `0.0 < r <= 1.0` on the
overriding method, so its call is well formed and isolates the comparison
defect. The verbatim M2 sketch in the review omits that precondition. It already
fails before the repair: 2 verified, 3 errors (global-bound decrease, numeric
lower bound, and call precondition). Candidate legacy gives the same output;
the corrected default gives 2 verified, 2 errors (global bound and call
precondition). Thus the verbatim sketch is not an accepted pre-fix exploit.
The isolated fixture changes only this precondition and demonstrates the reset.

The checked-in [expected output](../../Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-264.dfy.expect)
records the corrected diagnostics and original/legacy summary lines for all
four isolated cases.

The two integration drivers exercise both resolvers, accepted fractional weak
progress with an unchanged suffix, suffix progress after a fractional prefix,
negative terminal values, early strict progress with an increasing suffix,
TOP padding, M3 forwarding/application, and a reachable `assert false` control.
The algebraic fixture verifies six declarations covering tuple absorption and
numeric ranking functions.

## Measurement scope

The comparison uses the suite's existing 1,164-program plan at its existing
resource limits, with 6,969 declaration outcomes in each mode. It also runs all
2,195 source/target-specific standard-library declaration outcomes, with the
library's own limits. The suite uses Z3 5.1.0; the library uses Z3 5.1.0 and the
branch's harness solver, Z3 4.12.1. Both settings of `--additional-axioms` are
measured separately. No resource limit or specification is weakened.

Suite declaration outcomes and resource totals come from the per-VC JSON logs.
The library uses its normal `/deterministicLiteralHashes:1 /restartProver`
configuration, with resource counts from its declaration logs. Suite processes
reuse a solver within a file as the standing gate does. Resource differences
are whole-scope sums, including unsuccessful proof attempts, not elapsed time.
A program summary's verified count also counts split batches; a change in that
count need not change a declaration outcome. Programs without a verification
log remain in the verdict inventory and contribute no recorded VC resource units.

## Annotation boundary

The standard-library annotations below record the affected proof obligations
without changing their statements, adding assumptions, or suppressing failures.
`OutOfResource` remains an incomplete proof; `Errors` records failed verification,
including solver-internal failures distinguished below.
Passing the expected-verdict gate means these outcomes match the reviewed
baseline, not that every standard-library theorem has been proved.

## CI comparison provenance

Corrected source tree: `dd59f50d0b52360b4e9eafa47e915bfc0a625a35`; combined product: `cb5a313beb4039a0089c8f195de25f6ff5a8fab8`. [PR CI](https://github.com/erniecohen/dafny/actions/runs/43116688442) retains the raw suite proof logs and the six expected-verdict artifacts. The product also incorporates the independently shipped JavaScript runtime repair; its verifier sources match `74964118e20d455dd307819d9abaad817683d9b7`.

The pre-fix control is source `428e348b0b125cb67505f03a0026b3f497f98e48`, from the [shipped CI run](https://github.com/erniecohen/dafny/actions/runs/43088663501). The [initial candidate-legacy CI run](https://github.com/erniecohen/dafny/actions/runs/43099027773) reproduces its complete Z3 5.1.0 vectors exactly: all 7,941 suite batches and all 2,195 library declarations have identical proof outcomes and resource counts, in both axiom modes. The suite legacy cohorts below use those identical vectors; library controls use the pre-fix run. Z3 4.12.1 controls are the pre-fix translation, which the opt-out retains. These are public CI measurements; supplemental Z3 4.16.0 data from the initial run is not the Z3 4.12.1 gate.

The byte-identity control in the completed build job of the [comparison run](https://github.com/erniecohen/dafny/actions/runs/43103517370), at source `59addceb`, reports exact stdout, stderr, Boogie and SMT bytes for all seven fixture families under pre-fix default versus candidate legacy. Its later measurement jobs were cancelled and supply none of the resource totals below. Subsequent verifier changes only attach diagnostic origins in the corrected mode; the legacy path is unchanged.

The later shipped source-contract changes through `908d5b8b8` are integrated in
`0fc877a47043b74c7451ea549a3149cd2a46b8cb`. They change modular source-contract reachability while retaining
the corrected component relations and tuple lift. The measurements above identify
their original source precisely; final validation of the combined branch is linked
from the PR.

All resource differences below are corrected default minus legacy.

| Scope | Additional axioms | Changed declarations | Legacy RU | Default RU | Change |
|---|---|---:|---:|---:|---:|
| Suite, Z3 5.1.0 | off | 5 | 1,484,006,833 | 1,509,768,804 | +25,761,971 |
| Suite, Z3 5.1.0 | on | 5 | 1,515,651,096 | 1,477,526,219 | -38,124,877 |
| Standard library, Z3 5.1.0 | off | 2 | 1,545,253,347 | 1,534,281,963 | -10,971,384 |
| Standard library, Z3 5.1.0 | on | 2 | 1,165,086,904 | 1,217,282,554 | +52,195,650 |
| Standard library, Z3 4.12.1 | off | 1 | 1,031,445,898 | 1,112,946,242 | +81,500,344 |
| Standard library, Z3 4.12.1 | on | 0 | 1,020,750,464 | 1,027,160,658 | +6,410,194 |

## Changed proof outcomes

Each name below is a declaration outcome, rather than an individual split batch.

### suite-off

- `dafny0/DecreasesTo0.dfy: GoodInstances (correctness)`: Correct → Errors.
- `dafny0/DecreasesTo1.dfy: GoodInstances (correctness)`: Correct → Errors.
- `dafny0/FunctionSpecifications.dfy: GoodPost (well-formedness)`: OutOfResource → Correct.
- `dafny0/NoTypeArgs.dfy: Lemma (correctness)`: Correct → OutOfResource.
- `dafny2/SnapshotableTrees.dfy: SnapTree.Iterator.Push (correctness)`: Correct → OutOfResource.

### suite-on

- `dafny0/DecreasesTo0.dfy: GoodInstances (correctness)`: Correct → Errors.
- `dafny0/DecreasesTo1.dfy: GoodInstances (correctness)`: Correct → Errors.
- `dafny0/FunctionSpecifications.dfy: GoodPost (well-formedness)`: OutOfResource → Correct.
- `dafny0/NoTypeArgs.dfy: Lemma (correctness)`: Correct → OutOfResource.
- `dafny2/SnapshotableTrees.dfy: SnapTree.Iterator.Push (correctness)`: Correct → OutOfResource.

### std-off-5.1.0

- `Std Std.Arithmetic.Power.LemmaPowIncreases (correctness)`: OutOfResource → Correct.
- `Std Std.Collections.Seq.MergeSortedWith (well-formedness)`: OutOfResource → Correct.

### std-on-5.1.0

- `Std Std.Arithmetic.Power.LemmaPowIncreases (correctness)`: Correct → OutOfResource.
- `Std Std.Collections.Seq.MergeSortedWith (well-formedness)`: OutOfResource → Correct.

### std-off-4.12.1

- `Std Std.Producers.FlattenedProducer.Invoke (correctness)`: Correct → Errors.

### std-on-4.12.1

No declaration outcome changes.

## Standard-library proof annotations

The changed VCs follow from the new termination formulas, which also affect
induction hypotheses and loop summaries. A resource-budget crossing is a proof-search
result; it does not establish that the theorem became false or that its former
proof relied on the reset defect. These annotations retain every failed or
incomplete outcome explicitly.

- `Std Std.Arithmetic.Power.LemmaPowIncreases (correctness)` ([Power.dfy](../../Source/DafnyStandardLibraries/src/Std/Arithmetic/Power.dfy)): The arithmetic/induction proof crosses its existing resource budget in opposite directions between the two axiom configurations under Z3 5.1.0. The additional-axioms-enabled exhaustion is retained as an open proof obligation.
- `Std Std.Collections.Seq.MergeSortedWith (well-formedness)` ([Seq.dfy](../../Source/DafnyStandardLibraries/src/Std/Collections/Seq.dfy)): The recursive merge well-formedness proof finishes within its existing resource budget under Z3 5.1.0 after the termination VCs change.
- `Std Std.Producers.FlattenedProducer.Invoke (correctness)` ([Producers.dfy](../../Source/DafnyStandardLibraries/src/Std/Actions/Producers.dfy#L2297)): With Z3 4.12.1 and additional axioms off, the solver returns `unknown` with `Overflow encountered when expanding vector` while checking `currentInner == Seq.Last(original.Outputs())` after `original.Invoke(())`. The driver reports `Errors` and an assertion diagnostic. This is a solver-internal failure and an incomplete proof, not a counterexample or evidence that this equality is false. The same declaration remains `Correct` with additional axioms on. The loop also carries frame, history and validity obligations; no statement or limit is weakened.

## Every changed suite baseline row

The same program rows change with additional axioms off and on. The files
retain exit codes, summary counts, and error-line sets from the PR CI artifacts.

- `dafny0/DecreasesTo0.dfy`: Unbounded integer `x` and `y` may be negative. Further numeric descent from a negative old component is now rejected, including the corresponding tuple assertions.
- `dafny0/DecreasesTo1.dfy`: The same negative-old-value obligations as `DecreasesTo0`, with proof-obligation expressions enabled.
- `dafny0/ForLoops.dfy`: Two lower-bound failures now belong to the composite loop termination check, moving their source locations from the decreases expression to the loop. Error count and declaration outcomes are unchanged.
- `dafny0/FunctionSpecifications.dfy`: The changed termination formulas let `GoodPost` well-formedness finish within the existing resource limit; its previous resource diagnostic disappears.
- `dafny0/NoTypeArgs.dfy`: `Lemma` correctness now exhausts the existing resource limit after the termination formulas change. This is an annotated proof-cost regression, not an accepted proof.
- `dafny0/Termination.dfy`: One redundant separate lower-bound diagnostic disappears because the bound is part of the tuple relation. Declaration outcomes are unchanged.
- `dafny1/PriorityQueue.dfy`: Removing separate lower-bound obligations changes the split-batch verified count; declaration outcomes stay the same.
- `dafny1/SchorrWaite.dfy`: Removing separate lower-bound obligations changes the split-batch verified count; declaration outcomes stay the same.
- `dafny2/MinWindowMax.dfy`: Removing separate lower-bound obligations changes the split-batch verified count; declaration outcomes stay the same.
- `dafny2/SmallestMissingNumber-functional.dfy`: Removing separate lower-bound obligations changes the split-batch verified count; the same declarations remain out of resource.
- `dafny2/SnapshotableTrees.dfy`: `SnapTree.Iterator.Push` correctness now exhausts the existing resource limit. Separate lower-bound batch removal also changes the verified count. The new exhaustion remains an incomplete proof.
- `dafny3/Inc.dfy`: One redundant lower-bound diagnostic disappears; declaration outcomes and the set of error lines stay the same.
- `dafny4/ExpandedGuardednessNeg.dfy`: Two redundant lower-bound diagnostics disappear; the two failing declaration outcomes and their locations stay the same.
- `dafny4/Primes.dfy`: Removing a separate lower-bound obligation changes the split-batch verified count; declaration outcomes stay the same.
- `git-issues/git-issue-4939a.dfy`: Two redundant separate lower-bound diagnostics disappear; declaration outcomes and the set of error lines stay the same.

For each changed standard-library `run Std` summary row, the named outcome
changes above explain the errors/resource diagnostics that appear or disappear.
The lower verified-batch counts also reflect the removal of separate numeric
lower-bound obligations. All target-specific declaration outcomes remain
`Correct`. Source proof failures already present in legacy mode are retained
in the baselines.
