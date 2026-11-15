# Translation-cache compatibility evidence

This file retains historical observations made before the owner decision at
https://github.com/erniecohen/dafny/pull/262#issuecomment-6826006467.
They do not qualify the current translation table. The default prerequisite,
https://github.com/erniecohen/dafny/pull/285, owns translator changes and their
moved-proof ledger. After that prerequisite, option-off, enabled-cold and
enabled-warm must match in Boogie, raw SMT, verdicts, RU and reports; the former
permission for additional off/on movement no longer applies.

## Historical difference classification

The complete historical audit used frozen combined producer
`53e12caf7369d642534f68679f1b191702452402`: 2,342 option-profile cases,
including 1,164 suite programs and seven Std parts in each additional-axiom
profile. Its 194 cases with changed off/on task outcome or RU contain 5,575
changed task observations and seven task outcome changes across five declarations.

For each of those 194 cases, a solver-free reconstruction used that same
producer's assemblies and source revision, preserving the original verification
arguments. It captured each module immediately after translation, before Boogie
preparation, in default, naming-enabled and translation-cache-cold modes. It
found changed module text for every affected task, and module-qualified generated
identifiers in the changed text of every differing module. All 744 differing
module observations have this classification; zero tasks lack changed-module
text and zero modules remain unclassified. The per-module byte digests and
changed identifier-line counts are in
[241-historical-translation-inputs.tsv](241-historical-translation-inputs.tsv).
Those digests describe this reconstruction's common source bindings; they are
not portable path-normalized digests or digests captured during the original
proof run.

The cause category below is the change to history-free translation with
module-qualified generated names. It identifies a changed translator path and
actual emitted identifiers. It does not claim that renaming alone, isolated from
declaration layout and graph materialization, accounts quantitatively for every
RU movement. The naming-enabled intermediate and cache-cold modes emit equal
module bytes in this reconstruction. Their historical 46 cost-difference cases
therefore require investigation at Boogie's prepared solver-input boundary,
rather than attribution to a printed-Boogie difference between those two modes.
The updated compatibility audit records complete solver-interaction hashes for
that distinction. Any changed outcome or RU with identical relevant input is
blocking; a coarse module digest is not used to waive it.

| Historical case | Changed task observations | Outcome changes | Differing modules | Classification |
|---|---:|---:|---:|---|
| `std-0/Std` | 2286 | 4 | 86 | History-free translation; qualified identifiers observed |
| `std-1/Std` | 2286 | 3 | 86 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/Array.dfy` | 1 | 0 | 2 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/ArrayElementInit.dfy` | 8 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/ArrayElementInitCompile.dfy` | 3 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/ArrayElementInitERR.dfy` | 8 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/AutoContracts.dfy` | 2 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/BoundedPolymorphismVerification.dfy` | 1 | 0 | 9 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/CanCall.dfy` | 2 | 0 | 7 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/Comprehensions.dfy` | 16 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/ComprehensionsNewSyntax.dfy` | 16 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/ControlStructures.dfy` | 13 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/DTypes.dfy` | 1 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/DatatypeUpdate.dfy` | 1 | 0 | 2 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/DefiniteAssignment.dfy` | 1 | 0 | 7 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/DiscoverBounds.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/ForLoops.dfy` | 3 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/GeneralNewtypeCollections.dfy` | 2 | 0 | 10 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/GeneralNewtypeCollectionsGeneric.dfy` | 2 | 0 | 10 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/GeneralNewtypeMemberVerify.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/GeneralNewtypeVerify.dfy` | 2 | 0 | 17 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/GhostGuards.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/Inverses.dfy` | 6 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/Iterators.dfy` | 1 | 0 | 9 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/LoopModifies.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/Maps.dfy` | 20 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/MiscTypeInferenceTests.dfy` | 1 | 0 | 10 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/MoForallCompilation.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/Newtypes.dfy` | 1 | 0 | 11 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/NoMoreAssume2Less2.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/SeqSlice.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/SharedDestructorsCompile.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/SubsetTypes.dfy` | 5 | 0 | 20 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/Twostate-Verification.dfy` | 7 | 0 | 2 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/TypeAdjustments.dfy` | 5 | 0 | 12 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/TypeConversions.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/TypeConversionsCompile.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/TypeInferenceRefresh.dfy` | 2 | 0 | 26 | History-free translation; qualified identifiers observed |
| `suite-0/dafny0/TypeMembers.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny1/BDD.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny1/ExtensibleArray.dfy` | 9 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny1/ExtensibleArrayAuto.dfy` | 4 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny1/KatzManna.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny1/MatrixFun.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny1/PriorityQueue.dfy` | 51 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny2/COST-verif-comp-2011-2-MaxTree-class.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny2/COST-verif-comp-2011-4-FloydCycleDetect.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny2/Classics.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny2/MinWindowMax.dfy` | 144 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny2/SegmentSum.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny2/SmallestMissingNumber-functional.dfy` | 6 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny2/SmallestMissingNumber-imperative.dfy` | 4 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny2/StoreAndRetrieve.dfy` | 3 | 0 | 6 | History-free translation; qualified identifiers observed |
| `suite-0/dafny2/TreeBarrier.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny3/Abstemious.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny3/Filter.dfy` | 10 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny3/GenericSort.dfy` | 2 | 0 | 7 | History-free translation; qualified identifiers observed |
| `suite-0/dafny3/Iter.dfy` | 4 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/Bug145.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/Bug54.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/Bug71.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/FlyingRobots.dfy` | 16 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/KozenSilva.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/Lucas-up.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/McCarthy91.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/Regression12.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/UnionFind.dfy` | 19 | 0 | 5 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/git-issue104.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/git-issue195.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/git-issue196.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/git-issue26.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/dafny4/git-issue39.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-1130.dfy` | 1 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-1163.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-1212.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-1250.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-1256.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-1676.dfy` | 1 | 0 | 6 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-2597-verification.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-2640.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-276a.dfy` | 2 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-276v.dfy` | 18 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-314.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-321a.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-3873.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-3883.dfy` | 1 | 0 | 2 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-4684.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-604.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-6090.dfy` | 1 | 0 | 5 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-701.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-731.dfy` | 3 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-731b.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-851.dfy` | 1 | 0 | 8 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-897.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-897a.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/git-issue-953.dfy` | 1 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-0/git-issues/github-issue-2174.dfy` | 8 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/Array.dfy` | 1 | 0 | 2 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/ArrayElementInit.dfy` | 8 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/ArrayElementInitCompile.dfy` | 3 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/ArrayElementInitERR.dfy` | 8 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/AutoContracts.dfy` | 2 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/BoundedPolymorphismVerification.dfy` | 1 | 0 | 9 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/CanCall.dfy` | 2 | 0 | 7 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/Comprehensions.dfy` | 14 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/ComprehensionsNewSyntax.dfy` | 14 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/ControlStructures.dfy` | 13 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/DTypes.dfy` | 1 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/DatatypeUpdate.dfy` | 1 | 0 | 2 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/DefiniteAssignment.dfy` | 1 | 0 | 7 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/DiscoverBounds.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/ForLoops.dfy` | 3 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/GeneralNewtypeCollections.dfy` | 2 | 0 | 10 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/GeneralNewtypeCollectionsGeneric.dfy` | 2 | 0 | 10 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/GeneralNewtypeMemberVerify.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/GeneralNewtypeVerify.dfy` | 2 | 0 | 17 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/GhostGuards.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/Inverses.dfy` | 7 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/Iterators.dfy` | 1 | 0 | 9 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/LoopModifies.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/Maps.dfy` | 20 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/MiscTypeInferenceTests.dfy` | 1 | 0 | 10 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/MoForallCompilation.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/Newtypes.dfy` | 1 | 0 | 11 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/NoMoreAssume2Less2.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/SeqSlice.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/SharedDestructorsCompile.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/SubsetTypes.dfy` | 5 | 0 | 20 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/Twostate-Functions.dfy` | 1 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/Twostate-Verification.dfy` | 7 | 0 | 2 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/TypeAdjustments.dfy` | 6 | 0 | 12 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/TypeConversions.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/TypeConversionsCompile.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/TypeInferenceRefresh.dfy` | 3 | 0 | 26 | History-free translation; qualified identifiers observed |
| `suite-1/dafny0/TypeMembers.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny1/BDD.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny1/ExtensibleArray.dfy` | 9 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny1/ExtensibleArrayAuto.dfy` | 4 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny1/KatzManna.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny1/MatrixFun.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny1/PriorityQueue.dfy` | 51 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/COST-verif-comp-2011-2-MaxTree-class.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/COST-verif-comp-2011-4-FloydCycleDetect.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/Classics.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/MinWindowMax.dfy` | 143 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/SegmentSum.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/SmallestMissingNumber-functional.dfy` | 6 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/SmallestMissingNumber-imperative.dfy` | 4 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/StoreAndRetrieve.dfy` | 3 | 0 | 6 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/TreeBarrier.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny2/pq-intrinsic-extrinsic.dfy` | 5 | 0 | 6 | History-free translation; qualified identifiers observed |
| `suite-1/dafny3/Abstemious.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny3/Filter.dfy` | 10 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny3/GenericSort.dfy` | 2 | 0 | 7 | History-free translation; qualified identifiers observed |
| `suite-1/dafny3/Iter.dfy` | 4 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/Bug145.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/Bug54.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/Bug71.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/FlyingRobots.dfy` | 16 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/KozenSilva.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/Lucas-up.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/McCarthy91.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/Regression12.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/UnionFind.dfy` | 18 | 0 | 5 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/git-issue104.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/git-issue195.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/git-issue196.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/git-issue26.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/dafny4/git-issue39.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-1130.dfy` | 1 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-1163.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-1212.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-1250.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-1256.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-1676.dfy` | 1 | 0 | 6 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-2597-verification.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-2640.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-276a.dfy` | 2 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-276v.dfy` | 18 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-314.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-321a.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-3873.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-3883.dfy` | 1 | 0 | 2 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-4684.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-604.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-6090.dfy` | 1 | 0 | 5 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-701.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-731.dfy` | 3 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-731b.dfy` | 2 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-851.dfy` | 1 | 0 | 8 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-897.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-897a.dfy` | 1 | 0 | 1 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/git-issue-953.dfy` | 1 | 0 | 4 | History-free translation; qualified identifiers observed |
| `suite-1/git-issues/github-issue-2174.dfy` | 8 | 0 | 1 | History-free translation; qualified identifiers observed |

## Historical outcome changes

The historical off/on audit has seven proof-task outcome changes (five declarations), all in Std. Each task is listed even when its enclosing declaration still has another failing split. These are frozen-producer observations, not updated shipped expectations.

| Additional axioms | Declaration | VC | Default outcome / RU | Enabled-cold outcome / RU |
|---|---|---:|---:|---:|
| off | `Std.BulkActions.BatchReader.Read (correctness)` | 41 | Valid / 817,799 | OutOfResource / 1,118,886 |
| off | `Std.Producers.ConcatenatedProducer.Invoke (correctness)` | 283 | OutOfResource / 100,186,708 | Valid / 9,402,527 |
| off | `Std.JSON.ZeroCopy.Deserializer.Core.TryStructural (well-formedness)` | 1 | Valid / 62,855,306 | OutOfResource / 100,105,061 |
| off | `Std.Base64.DecodeValidPartialsFrom2PaddedSeq (well-formedness)` | 1 | OutOfResource / 1,058,478 | Valid / 814,545 |
| on | `Std.BulkActions.BatchReader.Read (correctness)` | 41 | Valid / 837,048 | OutOfResource / 1,119,336 |
| on | `Std.JSON.ZeroCopy.Deserializer.Sequences.Elements (well-formedness)` | 151 | OutOfResource / 1,250,541 | Valid / 1,172,918 |
| on | `Std.JSON.ZeroCopy.Deserializer.Core.TryStructural (well-formedness)` | 1 | Valid / 62,855,306 | OutOfResource / 100,105,061 |

These observations retain the original profile and producer. They do not update
shipped default expectations, and are not substituted for the current shipped-off
and enabled cold/warm audit. Std aggregate outcomes can remain unchanged when
another split in the same declaration still exhausts resources.

The naming-only intermediate has one additional outcome movement relative to cache-cold materialization: in `suite-0/dafny2/MinWindowMax.dfy`, `MinimumWindowMax (correctness)`, VC 128, changes from OutOfResource / 100,019,953 RU to Valid / 73,919,501 RU. Default and cache-cold both succeed for this task. This is separate from the seven default-to-enabled changes above.
