# Current enabled verdict-table changes

**Latest evidence:** product `fe6ddd1ba` has [68 focused suite/control observations](obligation-guarded-preparation-focused.md)
and [92 completed known-library/control observations](obligation-current-focused-library.md).
The library result explicitly combines 84 preserved observations from a harness-failed
attempt and eight controls from its completed follow-up. Remaining resource failures
and strict default-off cost acceptance stay open. No whole-suite/library run was
launched for this revision. Complete-gate results below belong to earlier pinned
revisions and cannot establish current acceptance.

This appendix records every enabled verdict-table movement in the completed
preceding `072175269` comparisons with baseline `ab210b78b` and Z3 5.1.0.
Both baseline and default-off exactly match their committed expected verdict
tables in all four complete gates. No table is rebased here. The [complete-gate
report](obligation-current-complete-gates.md) records scope, resource failures,
independent specification-WF evidence and rejected acceptance.

## Verifier suite

Each axiom setting has the same 60 changed source rows. Fifty-six remain
exit 4; three formerly exit-zero programs acquire resource failures; one
formerly resource-exhausted program becomes exit zero. Diagnostic row changes
include source locations moving from a procedure contract to the actual local
check. Changed verified/error counts are retained as row differences rather
than silently updating expectations. Only the priority-queue program becomes
fully accepted; its formerly failing batch was resource-exhausted, not invalid.
The separate Absy body improvement retains its failing independent SpecWF and
complete-program rejection.

| Program | Additional axioms off | Additional axioms on |
| --- | --- | --- |
| `dafny0/AutoContracts.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/BindingGuards.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/CanCall.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/ChainingDisjointTests.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/CoPrefix.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/ComputationsNeg.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/CustomErrorMesage.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/DTypes.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/Datatypes.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/DefaultParameters.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/Definedness.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/ForLoops.dfy` | summary/counts changed; source remains rejected | summary/counts changed; source remains rejected |
| `dafny0/GhostAutoInit.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/Includee.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/Inverses.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/Matrix-OOB.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/MultiSets.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/Predicates.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/PrefixTypeSubst.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/Refinement.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/ResultInTypeNewtype.dfy` | summary/counts changed; diagnostic lines changed; source remains rejected | summary/counts changed; diagnostic lines changed; source remains rejected |
| `dafny0/ResultInTypeSubsetType.dfy` | summary/counts changed; diagnostic lines changed; source remains rejected | summary/counts changed; diagnostic lines changed; source remains rejected |
| `dafny0/Skeletons.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/SubsetTypes.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/Twostate-Verification.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny0/TypeAntecedents.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny1/ExtensibleArray.dfy` | exit 0 to 4; summary/counts changed; diagnostic lines changed | exit 0 to 4; summary/counts changed; diagnostic lines changed |
| `dafny1/MoreInduction.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny2/SmallestMissingNumber-functional.dfy` | summary/counts changed; diagnostic lines changed; source remains rejected | summary/counts changed; diagnostic lines changed; source remains rejected |
| `dafny2/SnapshotableTrees.dfy` | summary/counts changed; diagnostic lines changed; source remains rejected | summary/counts changed; diagnostic lines changed; source remains rejected |
| `dafny2/pq-intrinsic-extrinsic.dfy` | exit 4 to 0; summary/counts changed; diagnostic lines changed | exit 4 to 0; summary/counts changed; diagnostic lines changed |
| `dafny3/Inc.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny3/WideTrees.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny3/Zip.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny4/Bug88.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny4/Circ.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny4/FlyingRobots.dfy` | exit 0 to 4; summary/counts changed; diagnostic lines changed | exit 0 to 4; summary/counts changed; diagnostic lines changed |
| `dafny4/Primes.dfy` | exit 0 to 4; summary/counts changed; diagnostic lines changed | exit 0 to 4; summary/counts changed; diagnostic lines changed |
| `dafny4/git-issue147.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `dafny4/regression-calc.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-1248.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-132-newtype-partial-total-universal-proof-nonvacuity.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-143-codatatype-abstemious-constructor-helper-positive.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-143-codatatype-destructive-guarded-observation-positive.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-145-nonvacuity.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-1989.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-19b.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-2211.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-2211a.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-2703.dfy` | summary/counts changed; source remains rejected | summary/counts changed; source remains rejected |
| `git-issues/git-issue-356-errors2.dfy` | summary/counts changed; source remains rejected | summary/counts changed; source remains rejected |
| `git-issues/git-issue-370.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-4035.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-4224.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-4787.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-600.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-6366.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-6531.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-6533.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |
| `git-issues/git-issue-6535.dfy` | diagnostic lines changed; source remains rejected | diagnostic lines changed; source remains rejected |

Two individual suite declarations improve from resource exhaustion to
`Correct`: `SMN_Correct` in `SmallestMissingNumber-functional.dfy` and
`PriorityQueue_extrinsic.AboutInsert` in `pq-intrinsic-extrinsic.dfy`.
The first program still contains another failing check; only the second
changes its complete-program exit to zero. No wholly accepted suite source
contains a formerly invalid batch that has newly become valid.

## Standard library

All changed declaration verdicts are listed below. Additional run-summary rows
reflect the same declaration changes. The two existing library `Errors`
declarations remain `Errors`. No baseline invalid library batch becomes valid.
These improvements are fixed-run outcomes, not promises across solver seeds.

| Declaration | Additional axioms off | Additional axioms on |
| --- | --- | --- |
| `Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered (correctness)` | Correct to OutOfResource | Correct to OutOfResource |
| `Std.Arithmetic.DivMod.LemmaModMultiplesVanish (correctness)` | Correct to OutOfResource | Correct to OutOfResource |
| `Std.Arithmetic.DivMod.LemmaMulModNoopLeft (correctness)` | Correct to OutOfResource | Correct to OutOfResource |
| `Std.Arithmetic.DivMod.LemmaRemainder (correctness)` | Correct to OutOfResource | Correct to OutOfResource |
| `Std.Arithmetic.DivMod.LemmaRoundDown (correctness)` | Correct to OutOfResource | Correct to OutOfResource |
| `Std.Arithmetic.Power.LemmaPowIncreases (correctness)` | OutOfResource to Correct | Unchanged outcome |
| `Std.Arithmetic.Power.LemmaPowStrictlyIncreases (correctness)` | Correct to OutOfResource | Unchanged outcome |
| `Std.Arithmetic.Power.LemmaPowSubtractsAuto (correctness)` | Correct to Errors | Correct to Errors |
| `Std.Base64.DecodeEncodeRecursively (correctness)` | OutOfResource to Correct | OutOfResource to Correct |
| `Std.Base64.DecodeValidEncode1Padding (correctness)` | Correct to OutOfResource | OutOfResource to Correct |
| `Std.Base64.DecodeValidEncode2Padding (correctness)` | OutOfResource to Correct | Unchanged outcome |
| `Std.Base64.EncodeBVIsBase64 (correctness)` | Unchanged outcome | Correct to OutOfResource |
| `Std.Base64.EncodeDecodeValid (correctness)` | OutOfResource to Correct | OutOfResource to Correct |
| `Std.BulkActions.BatchReader.Read (correctness)` | OutOfResource to Correct | OutOfResource to Correct |
| `Std.Collections.Seq.LemmaMaxOfConcat (correctness)` | Correct to OutOfResource | Correct to OutOfResource |
| `Std.JSON.ZeroCopy.Deserializer.Sequences.Elements (well-formedness)` | Correct to OutOfResource | Correct to OutOfResource |
| `Std.Termination.TerminationMetric.MultisetOrdinalDecreasesToSubMultiset (correctness)` | Correct to OutOfResource | Correct to OutOfResource |
| `Std.Unicode.UnicodeEncodingForm.PartitionCodeUnitSequenceChecked (correctness)` | OutOfResource to Correct | OutOfResource to Correct |
