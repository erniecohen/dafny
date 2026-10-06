# Full-suite and standard-library obligation comparison

Status: complete library comparisons and one completed suite setting; the other suite setting is still running for the legacy reveal-scope correction with Z3 5.1.0; enabled acceptance failed. The option remains experimental and default off.

Product and build identities are recorded in [obligation-validation.md](obligation-validation.md). The complete existing runners compare baseline, candidate with the option off, and candidate with it on, under both additional-axiom settings. Proof inputs, source options, warning policy, resource ceilings and expected-verdict tables are unchanged. These results supersede the preceding exit-publication comparison.

## Default-off compatibility

Every one of the 1,161 suite programs matches the baseline and committed expected verdicts in the completed additional-axioms-on setting. Complete named declaration/batch outcomes and resource entries match. Expected parser, resolver and command-line negatives remain in the denominator.

The standard-library comparisons cover the main Std project and all six target-specific parts, with 2,190 declarations and seven run rows per mode. With additional axioms off, every default-off verdict matches the fresh baseline and committed expectations. With additional axioms on, the fresh baseline proves `Std.JSON.ZeroCopy.Deserializer.Objects.BracketedToObject`, while the default-off candidate exhausts its resource ceiling and matches the committed expectation. The corresponding summary row also differs. This movement is recorded rather than accepted as default-off compatibility. All emitted operational Boogie matches after excluding complete comment lines. Library resource entries also vary under the existing declaration-order behavior documented in `std-verdicts.py`; exact controlled library cost compatibility is not accepted. The capture gate counts all emitted modules and completes normally.

The required unchanged complete library repeat also finishes normally. In that repeat, baseline and default-off match every committed verdict, including resource exhaustion in `Objects.BracketedToObject`. Complete operational Boogie is identical across the first and repeated baseline/off runs. This confirms variation in the baseline result without a source or binary change; it does not establish controlled library cost compatibility. The initial enabled comparison has seven fresh-baseline failure movements, including that varying baseline declaration; the repeat has six.

## Enabled declaration changes

The three definition-visibility failures are repaired in the unchanged library proofs: `EncodeDecodeRecursively`, `LemmaFilterDistributesOverConcat` and `WillSplitOnDelim` verify in both additional-axiom settings. Method and forall proof bodies retain the legacy reveal scope. The unchanged issue 100 reproducer and the two previously repaired suite examples also pass.

The remaining changes are listed below. No resource ceiling was increased, no expected verdict was replaced, and no baseline Errors declaration becomes Correct. The registered negative controls are checked separately; comparison of batch positions is bounded evidence rather than a soundness proof.

| Scope | Additional axioms | Declaration | Baseline | Enabled |
| --- | --- | --- | --- | --- |
| suite | on | dafny0/NoTypeArgs.dfy / Lemma (correctness) | Correct | OutOfResource |
| suite | on | dafny1/ExtensibleArrayAuto.dfy / ExtensibleArray.Set (correctness) | Correct | OutOfResource |
| suite | on | dafny1/SchorrWaite-stages.dfy / M2.SchorrWaite (correctness) | Correct | OutOfResource |
| suite | on | dafny2/pq-intrinsic-extrinsic.dfy / PriorityQueue_extrinsic.AboutInsert (correctness) | OutOfResource | Correct |
| suite | on | dafny4/FlyingRobots.dfy / FormArmy (correctness) | Correct | OutOfResource |
| suite | on | dafny4/Primes.dfy / Composite (correctness) | Correct | OutOfResource |
| library | off | Std Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered (correctness) | Correct | OutOfResource |
| library | off | Std Std.Arithmetic.DivMod.LemmaModEquivalenceAuto (correctness) | Correct | OutOfResource |
| library | off | Std Std.Arithmetic.DivMod.LemmaModNegNeg (correctness) | OutOfResource | Correct |
| library | off | Std Std.Arithmetic.Power.LemmaPowSubtractsAuto (correctness) | Correct | Errors |
| library | off | Std Std.Base64.AboutDecodeValid (correctness) | OutOfResource | Correct |
| library | off | Std Std.Base64.DecodeEncodeRecursively (correctness) | OutOfResource | Correct |
| library | off | Std Std.Base64.EncodeDecodeValid (correctness) | OutOfResource | Correct |
| library | off | Std Std.BulkActions.BatchReader.Read (correctness) | OutOfResource | Correct |
| library | off | Std Std.Collections.Seq.SortedUnique (correctness) | OutOfResource | Correct |
| library | off | Std Std.JSON.ZeroCopy.Deserializer.Arrays.BracketedToArray (correctness) | Correct | OutOfResource |
| library | off | Std Std.JSON.ZeroCopy.Deserializer.Objects.BracketedToObject (correctness) | OutOfResource | Correct |
| library | off | Std Std.Producers.MappedProducerOfNewProducers.Invoke (correctness) | Correct | OutOfResource |
| library | on | Std Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered (correctness) | Correct | OutOfResource |
| library | on | Std Std.Arithmetic.DivMod.LemmaModEquivalenceAuto (correctness) | Correct | OutOfResource |
| library | on | Std Std.Arithmetic.Power.LemmaPowIncreases (correctness) | Correct | OutOfResource |
| library | on | Std Std.Arithmetic.Power.LemmaPowMultiplies (correctness) | Correct | OutOfResource |
| library | on | Std Std.Arithmetic.Power.LemmaPowSubtractsAuto (correctness) | Correct | Errors |
| library | on | Std Std.Base64.AboutDecodeValid (correctness) | OutOfResource | Correct |
| library | on | Std Std.Base64.DecodeEncodeRecursively (correctness) | OutOfResource | Correct |
| library | on | Std Std.Base64.DecodeValidEncode1Padding (correctness) | OutOfResource | Correct |
| library | on | Std Std.Base64.DecodeValidEncode2Padding (correctness) | OutOfResource | Correct |
| library | on | Std Std.Base64.EncodeDecodeValid (correctness) | OutOfResource | Correct |
| library | on | Std Std.BulkActions.BatchReader.Read (correctness) | OutOfResource | Correct |
| library | on | Std Std.Collections.Seq.SortedUnique (correctness) | OutOfResource | Correct |
| library | on | Std Std.JSON.ZeroCopy.Deserializer.Objects.BracketedToObject (correctness) (baseline varies; repeat baseline/off/on all OutOfResource) | Correct | OutOfResource |
| library | on | Std Std.Producers.MappedProducerOfNewProducers.Invoke (correctness) | Correct | OutOfResource |

## Complete changed-row review

### suite-on

| Program or declaration | Review |
| --- | --- |
| dafny0/AutoContracts.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/BindingGuards.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/CanCall.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/ChainingDisjointTests.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/CoPrefix.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/ComputationsNeg.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/CustomErrorMesage.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/DTypes.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/Datatypes.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/DefaultParameters.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/Definedness.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/ForLoops.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny0/GhostAutoInit.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/Includee.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/Inverses.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/Matrix-OOB.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/MultiSets.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/NoTypeArgs.dfy | new failure |
| dafny0/Predicates.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/PrefixTypeSubst.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/Refinement.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/ResultInTypeSubsetType.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny0/Skeletons.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/SubsetTypes.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/Termination.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny0/Twostate-Verification.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny0/TypeAntecedents.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny1/ExtensibleArrayAuto.dfy | new failure |
| dafny1/MoreInduction.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny1/SchorrWaite-stages.dfy | new failure |
| dafny1/SchorrWaite.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny2/MinWindowMax.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny2/SmallestMissingNumber-functional.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny2/SnapshotableTrees.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny2/pq-intrinsic-extrinsic.dfy | proved former resource exhaustion |
| dafny3/Inc.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny3/WideTrees.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny3/Zip.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny4/ACL2-extractor.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny4/Bug88.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny4/Circ.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny4/FlyingRobots.dfy | new failure |
| dafny4/Primes.dfy | new failure |
| dafny4/SoftwareFoundations-Basics.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny4/gcd.dfy | proof/error counts changed; declaration outcomes unchanged |
| dafny4/git-issue147.dfy | diagnostic locations moved; declaration outcomes unchanged |
| dafny4/regression-calc.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-1248.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-132-newtype-partial-total-universal-proof-nonvacuity.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-143-codatatype-abstemious-constructor-helper-positive.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-143-codatatype-destructive-guarded-observation-positive.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-145-nonvacuity.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-1989.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-19b.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-2211.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-2211a.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-2703.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-370.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-4035.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-4224.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-4787.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-600.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-6366.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-6531.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-6533.dfy | diagnostic locations moved; declaration outcomes unchanged |
| git-issues/git-issue-6535.dfy | diagnostic locations moved; declaration outcomes unchanged |

### library-off

| Program or declaration | Review |
| --- | --- |
| Std Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered (correctness) | new resource exhaustion |
| Std Std.Arithmetic.DivMod.LemmaModEquivalenceAuto (correctness) | new resource exhaustion |
| Std Std.Arithmetic.DivMod.LemmaModNegNeg (correctness) | proved former resource exhaustion |
| Std Std.Arithmetic.Power.LemmaPowSubtractsAuto (correctness) | new proof error |
| Std Std.Base64.AboutDecodeValid (correctness) | proved former resource exhaustion |
| Std Std.Base64.DecodeEncodeRecursively (correctness) | proved former resource exhaustion |
| Std Std.Base64.EncodeDecodeValid (correctness) | proved former resource exhaustion |
| Std Std.BulkActions.BatchReader.Read (correctness) | proved former resource exhaustion |
| Std Std.Collections.Seq.SortedUnique (correctness) | proved former resource exhaustion |
| Std Std.JSON.ZeroCopy.Deserializer.Arrays.BracketedToArray (correctness) | new resource exhaustion |
| Std Std.JSON.ZeroCopy.Deserializer.Objects.BracketedToObject (correctness) | proved former resource exhaustion |
| Std Std.Producers.MappedProducerOfNewProducers.Invoke (correctness) | new resource exhaustion |
| run Std | summary reflects declaration changes |

### library-on

| Program or declaration | Review |
| --- | --- |
| Std Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered (correctness) | new resource exhaustion |
| Std Std.Arithmetic.DivMod.LemmaModEquivalenceAuto (correctness) | new resource exhaustion |
| Std Std.Arithmetic.Power.LemmaPowIncreases (correctness) | new resource exhaustion |
| Std Std.Arithmetic.Power.LemmaPowMultiplies (correctness) | new resource exhaustion |
| Std Std.Arithmetic.Power.LemmaPowSubtractsAuto (correctness) | new proof error |
| Std Std.Base64.AboutDecodeValid (correctness) | proved former resource exhaustion |
| Std Std.Base64.DecodeEncodeRecursively (correctness) | proved former resource exhaustion |
| Std Std.Base64.DecodeValidEncode1Padding (correctness) | proved former resource exhaustion |
| Std Std.Base64.DecodeValidEncode2Padding (correctness) | proved former resource exhaustion |
| Std Std.Base64.EncodeDecodeValid (correctness) | proved former resource exhaustion |
| Std Std.BulkActions.BatchReader.Read (correctness) | proved former resource exhaustion |
| Std Std.Collections.Seq.SortedUnique (correctness) | proved former resource exhaustion |
| Std Std.JSON.ZeroCopy.Deserializer.Objects.BracketedToObject (correctness) | new resource exhaustion |
| Std Std.Producers.MappedProducerOfNewProducers.Invoke (correctness) | new resource exhaustion |
| run Std | summary reflects declaration changes |

Enabled completeness/performance, controlled default-off library cost, end-to-end invariance and independent soundness review remain unaccepted or pending. These results do not establish uniform superiority. The precise power exit-package interaction and remaining resource regressions require further work.
