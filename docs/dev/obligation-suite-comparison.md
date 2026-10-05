# Full-suite obligation comparison

Status: both additional-axiom settings complete; standard-library and ordinary required CI acceptance remain pending.

The exact supported baseline and candidate identified in [obligation-validation.md](obligation-validation.md) were compared with Z3 5.1.0 and the unchanged suite runner. All 1,161 planned programs completed in each of the baseline, default-off and enabled runs for both additional-axiom settings. Default-off has no program verdict difference and no recorded batch-resource difference in either setting. Parser/resolver-negative programs without solver logs remain in the program denominator; they do not contribute solver resource records.

Enabled mode changes diagnostic locations, checked-obligation counts and some outcomes. The tables below record those changes without updating expected verdicts. Batch indices can shift when checks are added, so declaration identity is used for outcome and aggregate-cost review.

## Declaration outcome changes

| Additional axioms | Source | Declaration | Baseline | Enabled |
|---|---|---|---|---|
| off | `dafny0/AllLiteralsAxiom.dfy` | `NeedsAllLiteralsAxiom.calc_trick (correctness)` | Valid | Invalid |
| off | `dafny0/MultiSets.dfy` | `test7 (correctness)` | OutOfResource | Valid |
| off | `dafny1/SchorrWaite.dfy` | `SchorrWaite (correctness)` | Valid | OutOfResource |
| off | `dafny2/MinWindowMax.dfy` | `MinimumWindowMax (correctness)` | Valid | OutOfResource |
| off | `dafny2/SmallestMissingNumber-functional.dfy` | `SMN_Correct (correctness)` | OutOfResource | Valid |
| off | `dafny2/pq-intrinsic-extrinsic.dfy` | `PriorityQueue_extrinsic.AboutInsert (correctness)` | OutOfResource | Valid |
| off | `dafny4/FlyingRobots.dfy` | `FormArmy (correctness)` | Valid | OutOfResource |
| off | `dafny4/GHC-MergeSort.dfy` | `sorted_sequences (correctness)` | Valid | Invalid |
| off | `dafny4/Primes.dfy` | `RemoveFactor (correctness)` | Valid | OutOfResource |
| off | `dafny4/Regression16.dfy` | `InsertBalanced_A (correctness)` | Valid | OutOfResource |
| off | `dafny4/Regression16.dfy` | `InsertBalanced_B (correctness)` | Valid | OutOfResource |
| on | `dafny0/AllLiteralsAxiom.dfy` | `NeedsAllLiteralsAxiom.calc_trick (correctness)` | Valid | Invalid |
| on | `dafny0/MultiSets.dfy` | `test7 (correctness)` | OutOfResource | Valid |
| on | `dafny1/SchorrWaite.dfy` | `SchorrWaite (correctness)` | Valid | OutOfResource |
| on | `dafny2/SmallestMissingNumber-functional.dfy` | `SMN_Correct (correctness)` | OutOfResource | Valid |
| on | `dafny2/pq-intrinsic-extrinsic.dfy` | `PriorityQueue_extrinsic.AboutInsert (correctness)` | OutOfResource | Valid |
| on | `dafny4/FlyingRobots.dfy` | `FormArmy (correctness)` | Valid | OutOfResource |
| on | `dafny4/GHC-MergeSort.dfy` | `sorted_sequences (correctness)` | Valid | Invalid |
| on | `dafny4/Primes.dfy` | `RemoveFactor (correctness)` | Valid | OutOfResource |
| on | `dafny4/Regression16.dfy` | `InsertBalanced_A (correctness)` | Valid | OutOfResource |
| on | `dafny4/Regression16.dfy` | `InsertBalanced_B (correctness)` | Valid | OutOfResource |

There are eight new failing declarations with additional axioms off and seven with them on. Three declarations succeed in each setting after previously exhausting resources. No declaration classified Invalid in the baseline becomes Valid. This bounded observation is not a soundness proof.

AllLiteralsAxiom has the same final procedure goal in the captured enabled and legacy programs; enabled mode additionally checks the local postcondition and publishes its permitted summary. GHC-MergeSort reports failure of the inlined `AllSorted` postcondition. Their source properties were not changed; preserving the checks does not remove these matching regressions. Resource-limited declarations retain their original ceilings.

## Existing successful declarations flagged for cost review

These declarations cross the review threshold: more than twice the baseline resource use and at least 100,000 additional resource units. Complete performance acceptance remains pending.

| Additional axioms | Source | Declaration |
|---|---|---|
| off | `dafny4/Primes.dfy` | `Composite (correctness)` |
| off | `dafny4/NumberRepresentations.dfy` | `dec (correctness)` |
| off | `dafny2/COST-verif-comp-2011-4-FloydCycleDetect.dfy` | `Node.AnalyzeList (correctness)` |
| off | `dafny4/NumberRepresentations.dfy` | `inc (correctness)` |
| off | `dafny4/GHC-MergeSort.dfy` | `perm_sequences (correctness)` |
| off | `dafny0/Array.dfy` | `Fill_True (correctness)` |
| off | `dafny0/Array.dfy` | `Fill_All (correctness)` |
| off | `dafny0/Termination.dfy` | `ExtEvensSumToEven (correctness)` |
| off | `dafny4/GHC-MergeSort.dfy` | `perm_reverse (correctness)` |
| off | `dafny0/InductivePredicates.dfy` | `Alt.MyLemma_Nicer# (correctness)` |
| off | `dafny0/InductivePredicates.dfy` | `Alt.MyLemma_NotSoNice# (correctness)` |
| off | `dafny4/ACL2-extractor.dfy` | `NthAppendB (correctness)` |
| on | `dafny4/Primes.dfy` | `Composite (correctness)` |
| on | `dafny4/NumberRepresentations.dfy` | `dec (correctness)` |
| on | `dafny2/COST-verif-comp-2011-4-FloydCycleDetect.dfy` | `Node.AnalyzeList (correctness)` |
| on | `dafny4/NumberRepresentations.dfy` | `inc (correctness)` |
| on | `dafny4/GHC-MergeSort.dfy` | `perm_sequences (correctness)` |
| on | `dafny0/Array.dfy` | `Fill_All (correctness)` |
| on | `dafny0/Array.dfy` | `Fill_True (correctness)` |
| on | `dafny0/Termination.dfy` | `ExtEvensSumToEven (correctness)` |
| on | `dafny4/GHC-MergeSort.dfy` | `perm_reverse (correctness)` |
| on | `dafny4/ACL2-extractor.dfy` | `NthAppendB (correctness)` |
| on | `dafny2/MajorityVote.dfy` | `FindWinner (correctness)` |

## Program-level changes

| Additional axioms | Source | Baseline summary | Enabled summary | Classification |
|---|---|---|---|---|
| off | `dafny0/AllLiteralsAxiom.dfy` | 4 verified, 0 errors | 3 verified, 1 error | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny0/AutoContracts.dfy` | 36 verified, 9 errors | 36 verified, 9 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/BindingGuards.dfy` | 10 verified, 6 errors | 10 verified, 6 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/CanCall.dfy` | 34 verified, 3 errors | 34 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/ChainingDisjointTests.dfy` | 4 verified, 4 errors | 4 verified, 4 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/CoPrefix.dfy` | 13 verified, 11 errors | 13 verified, 11 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/ComputationsNeg.dfy` | 5 verified, 5 errors | 5 verified, 5 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/CustomErrorMesage.dfy` | 1 verified, 9 errors | 1 verified, 9 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/DTypes.dfy` | 20 verified, 7 errors | 20 verified, 7 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/Datatypes.dfy` | 29 verified, 15 errors | 29 verified, 15 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/DefaultParameters.dfy` | 72 verified, 74 errors | 72 verified, 74 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/Definedness.dfy` | 9 verified, 37 errors | 9 verified, 37 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/ForLoops.dfy` | 23 verified, 25 errors | 23 verified, 28 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny0/Fuel.dfy` | 31 verified, 39 errors | 31 verified, 23 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny0/GhostAutoInit.dfy` | 7 verified, 52 errors | 7 verified, 52 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/Includee.dfy` | 1 verified, 3 errors | 1 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/Inverses.dfy` | 31 verified, 3 errors | 31 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/Matrix-OOB.dfy` | 0 verified, 3 errors | 0 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/MultiSets.dfy` | 33 verified, 5 errors, 1 out of resource; 2 out of resource | 34 verified, 5 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny0/Predicates.dfy` | 16 verified, 3 errors | 16 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/PrefixTypeSubst.dfy` | 12 verified, 5 errors | 12 verified, 5 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/Refinement.dfy` | 28 verified, 13 errors | 28 verified, 13 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/ResultInTypeNewtype.dfy` | 3 verified, 87 errors | 3 verified, 92 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny0/ResultInTypeSubsetType.dfy` | 3 verified, 87 errors | 3 verified, 92 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny0/Skeletons.dfy` | 4 verified, 1 error | 4 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/SubsetTypes.dfy` | 13 verified, 91 errors | 13 verified, 91 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/Termination.dfy` | 106 verified, 27 errors | 112 verified, 27 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny0/Twostate-Verification.dfy` | 65 verified, 42 errors | 65 verified, 42 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny0/TypeAntecedents.dfy` | 5 verified, 3 errors | 5 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny1/MoreInduction.dfy` | 26 verified, 4 errors | 36 verified, 4 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny1/SchorrWaite.dfy` | 272 verified, 0 errors | 282 verified, 0 errors, 1 out of resource; 2 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny2/MinWindowMax.dfy` | 228 verified, 0 errors | 249 verified, 0 errors, 1 out of resource; 2 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny2/SmallestMissingNumber-functional.dfy` | 53 verified, 0 errors, 7 out of resource; 8 out of resource | 85 verified, 0 errors, 6 out of resource; 7 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny2/SnapshotableTrees.dfy` | 103 verified, 1 error, 4 out of resource; 5 out of resource | 126 verified, 1 error, 4 out of resource; 5 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny2/pq-intrinsic-extrinsic.dfy` | 31 verified, 0 errors, 1 out of resource; 2 out of resource | 32 verified, 0 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny3/Inc.dfy` | 8 verified, 12 errors | 8 verified, 12 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny3/WideTrees.dfy` | 5 verified, 1 error | 5 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny3/Zip.dfy` | 3 verified, 5 errors | 3 verified, 5 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny4/ACL2-extractor.dfy` | 32 verified, 0 errors | 40 verified, 0 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny4/Bug88.dfy` | 0 verified, 2 errors | 0 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny4/Circ.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny4/FlyingRobots.dfy` | 31 verified, 0 errors | 30 verified, 0 errors, 1 out of resource; 2 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny4/GHC-MergeSort.dfy` | 55 verified, 0 errors | 54 verified, 1 error | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny4/Primes.dfy` | 15 verified, 0 errors | 14 verified, 0 errors, 1 out of resource; 2 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny4/Regression16.dfy` | 5 verified, 0 errors | 3 verified, 0 errors, 2 out of resource; 3 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny4/SoftwareFoundations-Basics.dfy` | 53 verified, 1 error | 54 verified, 1 error | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny4/gcd.dfy` | 95 verified, 0 errors, 2 out of resource; 3 out of resource | 105 verified, 0 errors, 2 out of resource; 3 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `dafny4/git-issue147.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `dafny4/regression-calc.dfy` | 0 verified, 2 errors | 0 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-1248.dfy` | 0 verified, 2 errors | 0 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-132-newtype-partial-total-universal-proof-nonvacuity.dfy` | 1 verified, 3 errors | 1 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-143-codatatype-abstemious-constructor-helper-positive.dfy` | 1 verified, 2 errors | 1 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-143-codatatype-destructive-guarded-observation-positive.dfy` | 1 verified, 2 errors | 1 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-145-nonvacuity.dfy` | 2 verified, 2 errors | 2 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-1989.dfy` | 17 verified, 7 errors | 17 verified, 7 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-19b.dfy` | 19 verified, 9 errors | 19 verified, 9 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-2211.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-2211a.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-2703.dfy` | 0 verified, 4 errors | 0 verified, 4 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-356-errors2.dfy` | 2 verified, 7 errors | 2 verified, 9 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| off | `git-issues/git-issue-370.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-4035.dfy` | 2 verified, 4 errors | 2 verified, 4 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-4224.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-4787.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-600.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-6366.dfy` | 4 verified, 2 errors | 4 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-6531.dfy` | 4 verified, 1 error | 4 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-6533.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| off | `git-issues/git-issue-6535.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/AllLiteralsAxiom.dfy` | 4 verified, 0 errors | 3 verified, 1 error | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny0/AutoContracts.dfy` | 36 verified, 9 errors | 36 verified, 9 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/BindingGuards.dfy` | 10 verified, 6 errors | 10 verified, 6 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/CanCall.dfy` | 34 verified, 3 errors | 34 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/ChainingDisjointTests.dfy` | 4 verified, 4 errors | 4 verified, 4 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/CoPrefix.dfy` | 13 verified, 11 errors | 13 verified, 11 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/ComputationsNeg.dfy` | 5 verified, 5 errors | 5 verified, 5 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/CustomErrorMesage.dfy` | 1 verified, 9 errors | 1 verified, 9 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/DTypes.dfy` | 20 verified, 7 errors | 20 verified, 7 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/Datatypes.dfy` | 29 verified, 15 errors | 29 verified, 15 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/DefaultParameters.dfy` | 72 verified, 74 errors | 72 verified, 74 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/Definedness.dfy` | 9 verified, 37 errors | 9 verified, 37 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/ForLoops.dfy` | 23 verified, 25 errors | 23 verified, 28 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny0/Fuel.dfy` | 31 verified, 39 errors | 31 verified, 23 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny0/GhostAutoInit.dfy` | 7 verified, 52 errors | 7 verified, 52 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/Includee.dfy` | 1 verified, 3 errors | 1 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/Inverses.dfy` | 31 verified, 3 errors | 31 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/Matrix-OOB.dfy` | 0 verified, 3 errors | 0 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/MultiSets.dfy` | 33 verified, 5 errors, 1 out of resource; 2 out of resource | 34 verified, 5 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny0/Predicates.dfy` | 16 verified, 3 errors | 16 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/PrefixTypeSubst.dfy` | 12 verified, 5 errors | 12 verified, 5 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/Refinement.dfy` | 28 verified, 13 errors | 28 verified, 13 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/ResultInTypeNewtype.dfy` | 3 verified, 87 errors | 3 verified, 92 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny0/ResultInTypeSubsetType.dfy` | 3 verified, 87 errors | 3 verified, 92 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny0/Skeletons.dfy` | 4 verified, 1 error | 4 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/SubsetTypes.dfy` | 13 verified, 91 errors | 13 verified, 91 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/Termination.dfy` | 106 verified, 27 errors | 112 verified, 27 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny0/Twostate-Verification.dfy` | 65 verified, 42 errors | 65 verified, 42 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny0/TypeAntecedents.dfy` | 5 verified, 3 errors | 5 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny1/MoreInduction.dfy` | 26 verified, 4 errors | 36 verified, 4 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny1/SchorrWaite.dfy` | 272 verified, 0 errors | 282 verified, 0 errors, 1 out of resource; 2 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny2/MinWindowMax.dfy` | 228 verified, 0 errors | 250 verified, 0 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny2/SmallestMissingNumber-functional.dfy` | 53 verified, 0 errors, 7 out of resource; 8 out of resource | 85 verified, 0 errors, 6 out of resource; 7 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny2/SnapshotableTrees.dfy` | 103 verified, 1 error, 4 out of resource; 5 out of resource | 126 verified, 1 error, 4 out of resource; 5 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny2/pq-intrinsic-extrinsic.dfy` | 31 verified, 0 errors, 1 out of resource; 2 out of resource | 32 verified, 0 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny3/Inc.dfy` | 8 verified, 12 errors | 8 verified, 12 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny3/WideTrees.dfy` | 5 verified, 1 error | 5 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny3/Zip.dfy` | 3 verified, 5 errors | 3 verified, 5 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny4/ACL2-extractor.dfy` | 32 verified, 0 errors | 40 verified, 0 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny4/Bug88.dfy` | 0 verified, 2 errors | 0 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny4/Circ.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny4/FlyingRobots.dfy` | 31 verified, 0 errors | 30 verified, 0 errors, 1 out of resource; 2 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny4/GHC-MergeSort.dfy` | 55 verified, 0 errors | 54 verified, 1 error | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny4/Primes.dfy` | 15 verified, 0 errors | 14 verified, 0 errors, 1 out of resource; 2 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny4/Regression16.dfy` | 5 verified, 0 errors | 3 verified, 0 errors, 2 out of resource; 3 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny4/SoftwareFoundations-Basics.dfy` | 53 verified, 1 error | 54 verified, 1 error | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny4/gcd.dfy` | 95 verified, 0 errors, 2 out of resource; 3 out of resource | 105 verified, 0 errors, 2 out of resource; 3 out of resource | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `dafny4/git-issue147.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `dafny4/regression-calc.dfy` | 0 verified, 2 errors | 0 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-1248.dfy` | 0 verified, 2 errors | 0 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-132-newtype-partial-total-universal-proof-nonvacuity.dfy` | 1 verified, 3 errors | 1 verified, 3 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-143-codatatype-abstemious-constructor-helper-positive.dfy` | 1 verified, 2 errors | 1 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-143-codatatype-destructive-guarded-observation-positive.dfy` | 1 verified, 2 errors | 1 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-145-nonvacuity.dfy` | 2 verified, 2 errors | 2 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-1989.dfy` | 17 verified, 7 errors | 17 verified, 7 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-19b.dfy` | 19 verified, 9 errors | 19 verified, 9 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-2211.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-2211a.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-2703.dfy` | 0 verified, 4 errors | 0 verified, 4 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-356-errors2.dfy` | 2 verified, 7 errors | 2 verified, 9 errors | Checked-obligation count or verdict changed; declaration outcomes audited above |
| on | `git-issues/git-issue-370.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-4035.dfy` | 2 verified, 4 errors | 2 verified, 4 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-4224.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-4787.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-600.dfy` | 2 verified, 1 error | 2 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-6366.dfy` | 4 verified, 2 errors | 4 verified, 2 errors | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-6531.dfy` | 4 verified, 1 error | 4 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-6533.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |
| on | `git-issues/git-issue-6535.dfy` | 1 verified, 1 error | 1 verified, 1 error | Diagnostic locations only; exit and verification summary unchanged |

The ordinary runner reports batches as verified/error counts. Added local checks can increase those counts; moving a diagnostic to the implicit check can change its reported line. Counts alone do not identify accepted source obligations. Existing negative declarations remain classified Invalid in these comparisons. Standard-library acceptance, ordinary required CI and independent human soundness review are still pending.
