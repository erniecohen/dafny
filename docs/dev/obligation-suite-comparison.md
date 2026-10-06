# Full-suite and standard-library obligation comparison

Status: historical complete native comparisons for the preceding exit-publication correction with Z3 5.1.0; acceptance failed. The option remains experimental and default off.

Product and build identities are recorded in [obligation-validation.md](obligation-validation.md). The complete existing runners compare baseline, candidate with the option off, and candidate with it on, under both additional-axiom settings. Proof inputs, source options, warning policy, resource ceilings and expected-verdict tables are unchanged. These results describe the preceding product, not the later reveal-scope correction. The current correction preserves legacy reveal scopes and repairs the three definition-visibility examples in focused native validation. Fresh complete comparisons are in progress; see [current validation](obligation-validation.md).

## Default-off compatibility

Every one of the 1,161 suite programs matches the baseline and committed expected verdicts in both settings. Complete named declaration/batch outcomes and resource entries match. Expected parser, resolver and command-line negatives remain in the program denominator.

The standard-library comparisons cover the main Std project and all six target-specific parts. Every one of the 2,190 declaration verdicts and seven run rows matches the baseline and committed expectations. All emitted operational Boogie matches after excluding complete comment lines. Library resource entries vary under the existing declaration-order behavior documented in `std-verdicts.py`; exact controlled library cost compatibility is not accepted. A post-verification capture check incorrectly assumed one Boogie file per part. The already completed native evidence was audited with module coverage instead; the original capture failure is preserved separately and no proofs were rerun for that correction.

## Enabled suite declaration changes

All newly failing suite declarations exhaust their unchanged resource ceilings. AllLiteralsAxiom and GHC-MergeSort now pass in both complete enabled comparisons. No baseline Errors declaration becomes Correct. This bounded result does not establish soundness or uniform superiority.

| Additional axioms | Source | Declaration | Baseline | Enabled |
| --- | --- | --- | --- | --- |
| off | `dafny0/NoTypeArgs.dfy` | `Lemma (correctness)` | Correct | OutOfResource |
| off | `dafny1/ExtensibleArray.dfy` | `ExtensibleArray.Append (correctness)` | Correct | OutOfResource |
| off | `dafny1/ExtensibleArrayAuto.dfy` | `ExtensibleArray.Set (correctness)` | Correct | OutOfResource |
| off | `dafny1/SchorrWaite-stages.dfy` | `M2.SchorrWaite (correctness)` | Correct | OutOfResource |
| off | `dafny2/MinWindowMax.dfy` | `MinimumWindowMax (correctness)` | Correct | OutOfResource |
| off | `dafny2/pq-intrinsic-extrinsic.dfy` | `PriorityQueue_extrinsic.AboutInsert (correctness)` | OutOfResource | Correct |
| off | `dafny4/FlyingRobots.dfy` | `FormArmy (correctness)` | Correct | OutOfResource |
| off | `dafny4/NumberRepresentations.dfy` | `dec (correctness)` | Correct | OutOfResource |
| off | `dafny4/Primes.dfy` | `Composite (correctness)` | Correct | OutOfResource |
| on | `dafny0/NoTypeArgs.dfy` | `Lemma (correctness)` | Correct | OutOfResource |
| on | `dafny1/ExtensibleArray.dfy` | `ExtensibleArray.Append (correctness)` | Correct | OutOfResource |
| on | `dafny1/ExtensibleArrayAuto.dfy` | `ExtensibleArray.Set (correctness)` | Correct | OutOfResource |
| on | `dafny1/SchorrWaite-stages.dfy` | `M2.SchorrWaite (correctness)` | Correct | OutOfResource |
| on | `dafny2/pq-intrinsic-extrinsic.dfy` | `PriorityQueue_extrinsic.AboutInsert (correctness)` | OutOfResource | Correct |
| on | `dafny4/FlyingRobots.dfy` | `FormArmy (correctness)` | Correct | OutOfResource |
| on | `dafny4/NumberRepresentations.dfy` | `dec (correctness)` | Correct | OutOfResource |
| on | `dafny4/Primes.dfy` | `Composite (correctness)` | Correct | OutOfResource |

## Enabled library declaration changes

The four formerly Correct declarations that now report Errors are listed below. Native diagnostics identify a failed postcondition, calculation steps and a function precondition. The [documented failure examples](obligation-failure-examples.md) include controlled native diagnosis: three hide/reveal cases lose definition visibility after added scope pops, and retaining the relevant outer reveals restores success. The power failure is separately isolated to its added second-clause exit package, with its exact solver mechanism still unresolved. No baseline Errors declaration becomes Correct. Some formerly resource-limited declarations succeed, while another remains unproved with an Errors outcome.

| Additional axioms | Declaration | Baseline | Enabled |
| --- | --- | --- | --- |
| off | `Std Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered (correctness)` | Correct | OutOfResource |
| off | `Std Std.Arithmetic.DivMod.LemmaModEquivalenceAuto (correctness)` | Correct | OutOfResource |
| off | `Std Std.Arithmetic.DivMod.LemmaModNegNeg (correctness)` | OutOfResource | Correct |
| off | `Std Std.Arithmetic.Power.LemmaPowSubtractsAuto (correctness)` | Correct | Errors |
| off | `Std Std.Base64.AboutDecodeValid (correctness)` | OutOfResource | Correct |
| off | `Std Std.Base64.DecodeEncodeRecursively (correctness)` | OutOfResource | Errors |
| off | `Std Std.Base64.EncodeDecodeRecursively (correctness)` | Correct | Errors |
| off | `Std Std.BulkActions.BatchReader.Read (correctness)` | OutOfResource | Correct |
| off | `Std Std.Collections.Seq.LemmaFilterDistributesOverConcat (correctness)` | Correct | Errors |
| off | `Std Std.Collections.Seq.SortedUnique (correctness)` | OutOfResource | Correct |
| off | `Std Std.Collections.Seq.WillSplitOnDelim (correctness)` | Correct | Errors |
| off | `Std Std.Producers.MappedProducerOfNewProducers.Invoke (correctness)` | Correct | OutOfResource |
| on | `Std Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered (correctness)` | Correct | OutOfResource |
| on | `Std Std.Arithmetic.DivMod.LemmaModEquivalenceAuto (correctness)` | Correct | OutOfResource |
| on | `Std Std.Arithmetic.Power.LemmaPowIncreases (correctness)` | Correct | OutOfResource |
| on | `Std Std.Arithmetic.Power.LemmaPowMultiplies (correctness)` | Correct | OutOfResource |
| on | `Std Std.Arithmetic.Power.LemmaPowSubtractsAuto (correctness)` | Correct | Errors |
| on | `Std Std.Base64.AboutDecodeValid (correctness)` | OutOfResource | Correct |
| on | `Std Std.Base64.DecodeEncodeRecursively (correctness)` | OutOfResource | Errors |
| on | `Std Std.Base64.DecodeValidEncode1Padding (correctness)` | OutOfResource | Correct |
| on | `Std Std.Base64.DecodeValidEncode2Padding (correctness)` | OutOfResource | Correct |
| on | `Std Std.Base64.EncodeBVIsBase64 (correctness)` | Correct | OutOfResource |
| on | `Std Std.Base64.EncodeDecodeRecursively (correctness)` | Correct | Errors |
| on | `Std Std.BulkActions.BatchReader.Read (correctness)` | OutOfResource | Correct |
| on | `Std Std.Collections.Seq.LemmaFilterDistributesOverConcat (correctness)` | Correct | Errors |
| on | `Std Std.Collections.Seq.SortedUnique (correctness)` | OutOfResource | Correct |
| on | `Std Std.Collections.Seq.WillSplitOnDelim (correctness)` | Correct | Errors |
| on | `Std Std.JSON.ZeroCopy.Deserializer.Objects.BracketedToObject (correctness)` | OutOfResource | Correct |
| on | `Std Std.Producers.MappedProducerOfNewProducers.Invoke (correctness)` | Correct | OutOfResource |

Native per-check declaration capture confirms that the defining Filter axiom is present at both original calculation checks in the baseline, absent at both failed enabled checks, and present again in successful outer-reveal diagnostic witnesses. The enabled method-body ReturnPosition override introduces the scope pops responsible for that visibility change. Preserving checked formulas and fuel indices had not preserved the available definition axioms. No product correction or expected-verdict change is included in this diagnosis; the power interaction and resource regressions remain open.

## Successful cases requiring cost review

The defined review threshold is more than twice the baseline resource use with at least 100,000 additional resource units. These still-successful cases trigger review; no larger resource ceiling was substituted. Complete performance acceptance remains pending.

| Scope | Additional axioms | Source or declaration |
| --- | --- | --- |
| Suite | off | `dafny1/PriorityQueue.dfy / PriorityQueue_Alternative.SiftDown (correctness)` |
| Suite | off | `dafny3/GenericSort.dfy / Sort.InsertionSort (correctness)` |
| Suite | off | `dafny2/COST-verif-comp-2011-4-FloydCycleDetect.dfy / Node.AnalyzeList (correctness)` |
| Suite | off | `dafny1/ExtensibleArray.dfy / ExtensibleArray.Set (correctness)` |
| Suite | off | `dafny4/Primes.dfy / AltPrimeDefinition (correctness)` |
| Suite | off | `dafny0/Termination.dfy / ExtEvensSumToEven (correctness)` |
| Suite | off | `dafny0/Array.dfy / Fill_All (correctness)` |
| Suite | off | `dafny0/Array.dfy / Fill_True (correctness)` |
| Suite | off | `dafny3/SimpleInduction.dfy / FibLemma_Alternative (correctness)` |
| Library | off | `Std / Std.Base64.DecodeValidEncode1Padding (correctness)` |
| Library | off | `Std / Std.JSON.Spec.EscapeUnicode (well-formedness)` |
| Library | off | `Std / Std.Arithmetic.Mul.LemmaMulProperties (correctness)` |
| Suite | on | `dafny1/PriorityQueue.dfy / PriorityQueue_Alternative.SiftDown (correctness)` |
| Suite | on | `dafny2/COST-verif-comp-2011-4-FloydCycleDetect.dfy / Node.AnalyzeList (correctness)` |
| Suite | on | `dafny1/ExtensibleArray.dfy / ExtensibleArray.Set (correctness)` |
| Suite | on | `dafny4/Primes.dfy / AltPrimeDefinition (correctness)` |
| Suite | on | `dafny0/Termination.dfy / ExtEvensSumToEven (correctness)` |
| Suite | on | `dafny0/Array.dfy / Fill_All (correctness)` |
| Suite | on | `dafny0/Array.dfy / Fill_True (correctness)` |
| Suite | on | `dafny3/SimpleInduction.dfy / FibLemma_Alternative (correctness)` |
| Suite | on | `dafny4/Lucas-up.dfy / Lucas_Theorem (correctness)` |
| Suite | on | `dafny0/InductivePredicates.dfy / Alt.MyLemma_Nicer# (correctness)` |
| Suite | on | `dafny0/InductivePredicates.dfy / Alt.MyLemma_NotSoNice# (correctness)` |
| Suite | on | `dafny2/MajorityVote.dfy / FindWinner (correctness)` |
| Library | on | `Std / Std.Arithmetic.Mul.LemmaMulProperties (correctness)` |

## Complete changed-program review

Every changed suite program row was reviewed against its native declaration outcomes. Diagnostic changes retain declaration verdicts; additional checked obligations can change verified/error counts without accepting a formerly erroneous declaration. Rows with outcome changes are covered above.

| Source | Additional axioms off | Additional axioms on |
| --- | --- | --- |
| `dafny0/AutoContracts.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/BindingGuards.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/CanCall.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/ChainingDisjointTests.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/CoPrefix.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/ComputationsNeg.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/CustomErrorMesage.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/DTypes.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/Datatypes.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/DefaultParameters.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/Definedness.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/ForLoops.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny0/GhostAutoInit.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/Includee.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/Inverses.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/Matrix-OOB.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/MultiSets.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/NoTypeArgs.dfy` | resource regression | resource regression |
| `dafny0/Predicates.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/PrefixTypeSubst.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/Refinement.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/ResultInTypeSubsetType.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny0/Skeletons.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/SubsetTypes.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/Termination.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny0/Twostate-Verification.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny0/TypeAntecedents.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny1/ExtensibleArray.dfy` | resource regression | resource regression |
| `dafny1/ExtensibleArrayAuto.dfy` | resource regression | resource regression |
| `dafny1/MoreInduction.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny1/SchorrWaite-stages.dfy` | resource regression | resource regression |
| `dafny1/SchorrWaite.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny2/MinWindowMax.dfy` | resource regression | proof/error counts changed; declaration outcomes unchanged |
| `dafny2/SmallestMissingNumber-functional.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny2/SnapshotableTrees.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny2/pq-intrinsic-extrinsic.dfy` | proved former resource exhaustion | proved former resource exhaustion |
| `dafny3/Inc.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny3/WideTrees.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny3/Zip.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny4/ACL2-extractor.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny4/Bug88.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny4/Circ.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny4/FlyingRobots.dfy` | resource regression | resource regression |
| `dafny4/NumberRepresentations.dfy` | resource regression | resource regression |
| `dafny4/Primes.dfy` | resource regression | resource regression |
| `dafny4/SoftwareFoundations-Basics.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny4/gcd.dfy` | proof/error counts changed; declaration outcomes unchanged | proof/error counts changed; declaration outcomes unchanged |
| `dafny4/git-issue147.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `dafny4/regression-calc.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-1248.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-132-newtype-partial-total-universal-proof-nonvacuity.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-143-codatatype-abstemious-constructor-helper-positive.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-143-codatatype-destructive-guarded-observation-positive.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-145-nonvacuity.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-1989.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-19b.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-2211.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-2211a.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-2703.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-370.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-4035.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-4224.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-4787.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-600.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-6366.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-6531.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-6533.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |
| `git-issues/git-issue-6535.dfy` | diagnostic locations moved; declaration outcomes unchanged | diagnostic locations moved; declaration outcomes unchanged |

Expected verdicts have not been changed. Ordinary required CI, complete regression integration, development porting, end-to-end invariance and independent human soundness review remain pending. This report records complete native comparisons with regressions, not merge or release acceptance. AI assisted the implementation and validation.
