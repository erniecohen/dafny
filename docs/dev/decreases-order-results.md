# Decreases-order comparison and proof repairs

The corrected comparison for https://github.com/erniecohen/dafny/issues/264 is
on by default. Hidden `--legacy-decreases-order` retains the former translation.
`--modular-termination` remains opt-in. The [soundness argument](decreases-order.md)
covers the component relations, TOP padding, tuple lift, loop summaries and
induction hypotheses. No primitive axiom is added.

## Completed proof repairs

All five [coordinator-requested items](https://github.com/erniecohen/dafny/pull/275#issuecomment-6813065817)
are repaired without raising a resource limit or adding an assumption:

- `NoTypeArgs.Lemma` uses explicit recursion and named concatenation identity
  and associativity lemmas, preserving the type-inference test.
- `SnapTree.Iterator.Push` establishes the left subtree's prefix and the
  residual stack relation before recursion.
- `Std.Arithmetic.Power.LemmaPowIncreases` uses direct induction on `e2 - e1`
  and existing multiplication lemmas. Its statement is unchanged.
- `Std.Producers.FlattenedProducer.Invoke` establishes the new last output
  immediately after the producer call and splits that history fact from the
  remaining loop update. Moving the assertion alone avoided the solver overflow
  but exhausted the existing budget; adding the local split finishes. Its
  statement is unchanged.
- `DecreasesTo0/1.GoodInstances` require nonnegative inputs. Each file has a
  separate `BadNegativeInstances` method with three marked strict/weak failures
  for negative old values. `DecreasesTo0.Tuples` bounds its positive integer
  case. The reference manual and release note explain the behavior change.

The full CI artifacts confirm the repaired declarations are `Correct` in both
axiom configurations; the two library repairs are also `Correct` with both
Z3 versions. The new negative tests intentionally remain `Errors`.
**No requested proof regression remains unrepaired.** Existing unrelated
incomplete proofs remain visible in the inventories: agreement with a baseline
is not a claim that every library theorem is proved.

The branch incorporates the independently shipped opaque-trait constructor
repair from https://github.com/erniecohen/dafny/pull/279.

The later [CI platform run](https://github.com/erniecohen/dafny/actions/runs/43152109330)
exposed one additional proof-cost regression: `Lucas-down.Lucas_Binary''` is
`Correct` on Linux but exhausts the same 16,000,000 RU budget on macOS, in both
axiom modes. The lemma already contains its two recursive induction calls.
Disabling redundant automatic induction preserves its statement and twelve
verified declarations, without raising the limit. Its existing lit regression
covers the unchanged expected output; final PR CI checks the suite budget.
This repair needs no expected-verdict change: the baseline already requires
`Correct`.

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

## Measurement scope and provenance

Both CI cohorts use the suite's existing 1,164-program plan and resource limits.
The suite uses Z3 5.1.0; all 2,195 source/target-specific library declarations
are measured with Z3 5.1.0 and the harness solver, Z3 4.12.1. Both settings of
`--additional-axioms` are measured separately. Resource totals include failed
and incomplete attempts. Programs without a verification log stay in the
verdict inventory and contribute no recorded VC resource units.

Suite declaration outcomes and resources come from per-VC JSON logs. The
library uses its normal `/deterministicLiteralHashes:1 /restartProver` setting
and declaration logs. A program's verified count includes split batches;
a change in that count need not change a declaration's verdict.

The **initial comparison**, before the proof repairs above, isolates the
comparison change. Corrected source `dd59f50d0b52360b4e9eafa47e915bfc0a625a35`
was measured by [initial PR CI](https://github.com/erniecohen/dafny/actions/runs/43116688442).
The pre-fix control is `428e348b0b125cb67505f03a0026b3f497f98e48`, measured by
[shipped CI](https://github.com/erniecohen/dafny/actions/runs/43088663501).
[Candidate-legacy CI](https://github.com/erniecohen/dafny/actions/runs/43099027773)
reproduces the complete Z3 5.1.0 control vectors exactly: all 7,941 suite
batches and all 2,195 library declarations have identical outcomes and resource
counts, in both axiom modes. Z3 4.12.1 controls use the pre-fix translation,
which the opt-out retains.

The byte-identity control in the completed build job of the
[comparison run](https://github.com/erniecohen/dafny/actions/runs/43103517370)
reports exact stdout, stderr, Boogie and SMT bytes for seven fixture families.
Its later measurement jobs were cancelled and supply none of the totals here.
A focused repeat after integrating shipped source-contract changes through
`908d5b8b8` also preserves all seven families, including M2 and callbacks.
Subsequent comparison-source changes only attach diagnostic origins in the
corrected mode; the legacy path is unchanged.

The **cohort after the five requested repairs** is `ecd7d6b17b18cf494d277ad21d627641fbd1268a`,
measured by [post-repair PR CI](https://github.com/erniecohen/dafny/actions/runs/43146753087).
It includes the proof edits and merged shipped changes. Its resource difference
from the initial corrected source therefore includes those changes; it is not
an isolated measurement of the comparison switch on identical proof source.
All six replacement baselines are copied from this run's artifacts.

The later CI-only merge from https://github.com/erniecohen/dafny/pull/274 moves
the suite and Z3 4.12.1 gates to macOS; the Z3 5.1.0 library gates stay on
Linux. The measurements here remain the identified Linux cohort. Cross-platform
resource counts are not asserted equal. These recorded totals also precede
the later Lucas induction repair; final PR CI enforces the same expected
verdicts and retains the later resource observations.

## Initial comparison, before proof repairs

Differences are corrected default minus legacy; these are historical results,
not the repaired declarations' final verdicts.

| Scope | Additional axioms | Changed declarations | Legacy RU | Initial default RU | Change |
|---|---|---:|---:|---:|---:|
| Suite, Z3 5.1.0 | off | 5 | 1,484,006,833 | 1,509,768,804 | +25,761,971 |
| Suite, Z3 5.1.0 | on | 5 | 1,515,651,096 | 1,477,526,219 | -38,124,877 |
| Standard library, Z3 5.1.0 | off | 2 | 1,545,253,347 | 1,534,281,963 | -10,971,384 |
| Standard library, Z3 5.1.0 | on | 2 | 1,165,086,904 | 1,217,282,554 | +52,195,650 |
| Standard library, Z3 4.12.1 | off | 1 | 1,031,445,898 | 1,112,946,242 | +81,500,344 |
| Standard library, Z3 4.12.1 | on | 0 | 1,020,750,464 | 1,027,160,658 | +6,410,194 |

Every initially changed declaration, and its repaired-source outcome:

| Configuration | Declaration | Legacy | Initial default | Repaired default |
|---|---|---|---|---|
| suite-off | `dafny0/DecreasesTo0.dfy: GoodInstances (correctness)` | Correct | Errors | Correct |
| suite-off | `dafny0/DecreasesTo1.dfy: GoodInstances (correctness)` | Correct | Errors | Correct |
| suite-off | `dafny0/FunctionSpecifications.dfy: GoodPost (well-formedness)` | OutOfResource | Correct | Correct |
| suite-off | `dafny0/NoTypeArgs.dfy: Lemma (correctness)` | Correct | OutOfResource | Correct |
| suite-off | `dafny2/SnapshotableTrees.dfy: SnapTree.Iterator.Push (correctness)` | Correct | OutOfResource | Correct |
| suite-on | `dafny0/DecreasesTo0.dfy: GoodInstances (correctness)` | Correct | Errors | Correct |
| suite-on | `dafny0/DecreasesTo1.dfy: GoodInstances (correctness)` | Correct | Errors | Correct |
| suite-on | `dafny0/FunctionSpecifications.dfy: GoodPost (well-formedness)` | OutOfResource | Correct | Correct |
| suite-on | `dafny0/NoTypeArgs.dfy: Lemma (correctness)` | Correct | OutOfResource | Correct |
| suite-on | `dafny2/SnapshotableTrees.dfy: SnapTree.Iterator.Push (correctness)` | Correct | OutOfResource | Correct |
| std-off-5.1.0 | `Std Std.Arithmetic.Power.LemmaPowIncreases (correctness)` | OutOfResource | Correct | Correct |
| std-off-5.1.0 | `Std Std.Collections.Seq.MergeSortedWith (well-formedness)` | OutOfResource | Correct | Correct |
| std-on-5.1.0 | `Std Std.Arithmetic.Power.LemmaPowIncreases (correctness)` | Correct | OutOfResource | Correct |
| std-on-5.1.0 | `Std Std.Collections.Seq.MergeSortedWith (well-formedness)` | OutOfResource | Correct | Correct |
| std-off-4.12.1 | `Std Std.Producers.FlattenedProducer.Invoke (correctness)` | Correct | Errors | Correct |

## Outcomes and resources after the five requested repairs

Only newly finishing proofs differ among the common declaration names relative
to the original legacy control. The added declarations are
`NoTypeArgs.ConcatAssociative` and `NoTypeArgs.ConcatRightIdentity` (`Correct`),
and `DecreasesTo0.BadNegativeInstances` and `DecreasesTo1.BadNegativeInstances`
(`Errors`, intentionally failing tests).
The repaired suite has 6,973 declarations; the original control had 6,969.

### suite-off

- `dafny0/FunctionSpecifications.dfy: GoodPost (well-formedness)`: OutOfResource → Correct.

### suite-on

- `dafny0/FunctionSpecifications.dfy: GoodPost (well-formedness)`: OutOfResource → Correct.

### std-off-5.1.0

- `Std Std.Arithmetic.Power.LemmaPowIncreases (correctness)`: OutOfResource → Correct.
- `Std Std.Collections.Seq.MergeSortedWith (well-formedness)`: OutOfResource → Correct.

### std-on-5.1.0

- `Std Std.Collections.Seq.MergeSortedWith (well-formedness)`: OutOfResource → Correct.

### std-off-4.12.1

No common declaration changes verdict.

### std-on-4.12.1

No common declaration changes verdict.

| Scope | Additional axioms | Repaired default RU | Change from initial default |
|---|---|---:|---:|
| Suite, Z3 5.1.0 | off | 1,478,712,808 | -31,055,996 |
| Suite, Z3 5.1.0 | on | 1,446,470,223 | -31,055,996 |
| Standard library, Z3 5.1.0 | off | 1,537,573,552 | +3,291,589 |
| Standard library, Z3 5.1.0 | on | 1,198,382,791 | -18,899,763 |
| Standard library, Z3 4.12.1 | off | 1,078,953,003 | -33,993,239 |
| Standard library, Z3 4.12.1 | on | 1,026,169,522 | -991,136 |

## Reasons for replacement baseline rows

The six files come directly from the post-repair CI artifacts, in one separate
baseline commit. Compared with the previous corrected-default baselines:

- Both suite configurations: `DecreasesTo0/1` restore their positive
  `GoodInstances` proofs and add three intentional negative-old-value failures
  each. Source-line sets change as the negative methods are inserted.
- Both suite configurations: `NoTypeArgs` restores `Lemma` and adds two proved
  concatenation helpers, updating its verified count.
- Both suite configurations: `SnapshotableTrees` restores `Iterator.Push`;
  its explicit proof facts also change split-batch accounting.
- Library: the exponent induction restores `LemmaPowIncreases` with Z3 5.1.0
  and additional axioms on. The history split restores
  `FlattenedProducer.Invoke` with Z3 4.12.1 and axioms off. Successful split
  counts change in the library run summaries for the other configurations.

The original baseline changes from the comparison itself remain: the now
well-founded `decreases to` expressions include numeric lower bounds;
`FunctionSpecifications.GoodPost` and `Seq.MergeSortedWith` newly finish;
separate lower-bound batch removal changes successful counts or redundant
diagnostics in `ForLoops`, `Termination`, `PriorityQueue`, `SchorrWaite`,
`MinWindowMax`, `SmallestMissingNumber-functional`, `Inc`,
`ExpandedGuardednessNeg`, `Primes`, and `git-issue-4939a`. None of those batch
or diagnostic changes introduces another failed declaration.
