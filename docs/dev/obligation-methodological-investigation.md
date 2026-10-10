# Assertion invariance: methodological benefit and regression diagnosis

**Historical status: original localization complete; four later source proof repairs are documented separately. No general translator performance repair validated. Keep PR 168 draft under the original criteria.** This supplements the [consolidated acceptance report](obligation-followup-acceptance.md) and [governing follow-up review](obligation-followup-review.md). The verifier implementation used in this investigation is `1e6fa5126d8076983f0841629194dc64094f0b2f`, compiled-source pin `aa25d753ab1e3aebe4cc08d14313e705d741446b`, against baseline `ab210b78b50adb5a192542897f0a00cdb40f3c35` and Z3 5.1.0. No translation, source test, expected verdict, proof ceiling or seed policy changed in this investigation. PR 169 remains unported.

## Purpose and requested investigation

The owner's follow-up emphasizes the methodological goal: users should not have to try copying a postcondition into the body as an assertion to obtain its assertion context. The requested evidence is (1) existing copies that could be eliminated, (2) what fails in each of the eleven regressions and what a repair would require, and (3) any missing Boogie/Z3 determinization configuration. Original assertion strength, triggering expressions, fuel policy and outer reveal effects must remain intact. The follow-up review prohibits open-ended `RemoveFactor` tuning and prefers a frozen translation unless a concrete generalizable defect is demonstrated.

This work uses bounded affected-declaration experiments and selected full-file controls. It does not repeat the entire suite or replace any of the six completed broad comparisons. A completed diagnostic gate includes expected resource-exhausted results; it is not a green verification claim. Isolation changes the batch structure and distributes the unchanged per-batch resource ceiling across more batches. Its passing results do not establish acceptable aggregate cost or validate a product repair.

## Existing assertion copies

The scan conservatively matched single-line postconditions against identical source assertions. It misses multiline or equivalent expressions and also finds expected failures and proof steps; the sample is not an exhaustive catalog. Selected original/removal variants were compared across baseline, feature OFF and feature ON, both resolvers and both additional-axiom settings.

Four pre-existing suite cases verify with their copies removed in the sampled diagnostic configurations. Their complete files also verify, including every supporting definition and reported WF check, in baseline and feature ON at refreshed resolver/additional axioms OFF. Both the filtered sample and these full-file confirmations use `--relax-definite-assignment`; these results retain that qualification rather than replacing the files' original RUN commands:

| Existing file and declaration | Removed copy; proof retained |
|---|---|
| `dafny4/GHC-MergeSort.dfy`, `sorted_reverse` | Quantified ordering postcondition; other reverse/order reasoning retained |
| `git-issues/git-issue-1250.dfy`, `FibSumManual` | Three copies of the sum/Fibonacci postcondition; calculation retained |
| `dafny4/git-issue74.dfy`, `L` | Quantified postcondition assertion; forall/reveal proof retained |
| `git-issues/git-issue-697h.dfy`, `test` | Quantified substring-bound assertion; loop/calculation retained |

These copies are already unnecessary on the pinned baseline. They are cleanup candidates, **not new feature-dependent successes**. No assertions were deleted from the PR's suite. `Primes.Composite`'s intermediate conjunction was also sampled, but both original and removal versions exhaust resources in the diagnostic configuration. Removal is therefore inconclusive; it is not a terminal-only copy. The sample uses a definite-assignment relaxation and does not replace that file's original RUN command. The existing focused issue5148 explicit-assertion fixture also passes after removal on baseline; it is kept separate from independently pre-existing suite cases.

The earlier complete, well-typed issue-100 identity controls remain positive evidence for the methodological goal: enabled direct postconditions verify where baseline fails even with an immediate body assertion. Supporting WF checks and genuine Invalid false controls remain part of that evidence. The unchanged refreshed-resolver example's inferred witness-type failure is retained separately. The sampler found no additional case whose assertion becomes unnecessary specifically because of this feature.

The owner subsequently requested proofs for the four persistent cases. The [four-remainder proof record](obligation-remainder-proofs.md) and portable patches preserve the resulting checked source repairs, their support closure and limits. The historical localization table below describes the original unchanged-source failures; the later proof artifacts do not replace the frozen-candidate broad comparison.

## All eleven regressions

Each new resource failure was reproduced at an affected configuration and matched to its actual failed checks. None of these new failures reports a genuine Invalid counterexample. Seven pass when checks are isolated at their original per-batch resource ceilings; four persist or exhibit baseline failures under isolation. This is localization, not a delivered fix.

Here `a0`/`a1` mean additional axioms OFF/ON. Library cases use the committed refreshed resolver and default declaration order unless stated otherwise. “Joint batch” means multiple source obligations are verified together; one resource-exhausted batch cannot identify which obligation alone would fail.

| Declaration; affected configuration | What fails / isolation result | What a repair would require |
|---|---|---|
| `PriorityQueue.SiftDown`; legacy/a0 | Joint loop/exit batch exhausts resources; isolation passes baseline and ON. Map/heap instantiation pressure grows in ON. | General context or batching control that retains loop facts and ordinary exit assertion strength. |
| `M2.SchorrWaite`; legacy/a0 | Joint batch fails; isolation still exhausts resources at the old-heap allocated-path/ReachableVia invariant, `SchorrWaite-stages.dfy:217`. Baseline also fails that isolated invariant and the one at line 219. | Diagnose invariant/certificate context under splitting; isolation alone is insufficient. Preserve the test's intentional assumptions and independent checks. |
| `RemoveFactor`; legacy/a0 | Joint batch fails; one bounded isolation experiment passes baseline and ON. | A demonstrated general batching/context remedy, rather than benchmark-specific lowering. Prior baseline `VRRRRRRR` versus ON `RRRRRRRR` remains unresolved; no expanded seed/cap/hint campaign. |
| `M3.UnionFind.JoinMaintainsReaches1`; legacy/a0 | Joint reachability/forall proof fails; isolation passes both. | Preserve quantified reachability and checked helper requirements while avoiding unrelated batch interference. |
| `ExtensibleArray.Append`; refresh/a0 | Original full-file failure reproduced; full-file isolation passes every declaration in both modes. | Context/batching control with explicit accounting for higher aggregate work after splitting. |
| `SnapTree.Iterator.MoveNext`; refresh/a0 | Already isolated: one recursive `R` precondition at the `Push` call, `SnapshotableTrees.dfy:637`. ON shows deeper sequence append/index and boxed-map instantiation. | A justified way to control the generated instances or improve the structural proof while preserving standard assertion fuel and all required clauses. More splitting cannot localize an already single-check failure. |
| `DivMod.LemmaDivByMultipleIsStronglyOrdered`; a0 | Joint division/modulus induction and postcondition batch fails; isolation passes both. Disabling declaration-order normalization instead makes baseline fail. | Lawful local proof context; changing order is not a general repair. |
| `Power.LemmaPowStrictlyIncreases`; a0 | Joint higher-order multiplication/power proof fails; isolation passes both but increases aggregate work. | Preserve induction/forall support and establish acceptable cost, rather than treating each split ceiling as a free total-budget increase. |
| `Base64.DecodeValidEncode1Padding`; a0 | Joint slice/encode/decode calculation fails; isolation passes both. Order0 restores ON but breaks baseline. | Localize preparation and function-domain work without dropping checked bounds or relying on an order that regresses baseline. |
| `DivMod.LemmaModNegNeg`; a1 | Already isolated: existing explicit assertion at `DivMod.dfy:1324`. ON shows deeper multiplication-wrapper and commutativity instantiation. Order0 restores both modes in this case. | Control arithmetic/quantifier interaction lawfully; preserve required havoc/binder preparation and visible effects. Neither general splitting nor a universal order switch is established as a remedy. |
| `Base64.EncodeBVIsBase64`; a1 | Isolation still fails ON at postcondition line 907 and helper/function precondition line 922. Baseline also fails isolated helper/postcondition checks at lines 922, 908 and 917. | Diagnose the bitvector/padding/string branch bridge and incoming facts; isolation alone is insufficient. |

`Append` needs a configuration qualification. Its earlier legacy/a1 reproduction passed on the alternate platform in both filtered and full-file checks, so filtering was not established as the cause of the original platform-dependent failure. The refreshed/a0 full-file check reproduces the original affected result, and the same-configuration isolation passes. Neither result erases the original legacy/a1 failure.

## Bounded resource costs of seven isolation cases

These are historical bounded measurements from the frozen compiled source
`aa25d753ab1e3aebe4cc08d14313e705d741446b` (implementation
`1e6fa5126d8076983f0841629194dc64094f0b2f`), native Dafny
`4.11.0+fcb2042d.review.5841c0e6`, compared with baseline source
`ab210b78b50adb5a192542897f0a00cdb40f3c35`, native Dafny
`4.11.0+fcb2042d.review.898e7500`. Both used pinned Z3 **5.1.0** and the same
Boogie dependency `3.5.5-review.37e4435d`. They do not measure the later soundness correction
`1c30dd9904e6087931b8efa7d35cb33cc0fb9558` or establish its final correctness.
That correction's final validation was pending when these tables were recorded;
its resource costs are unmeasured here. No new cost sweep was run for this record.

Baseline means the baseline binary; OFF and ON mean the candidate with
`--consistent-obligation-checks false` and `true`. Each cost cell is
**verdict / target batch count / aggregate RU / maximum batch RU**. `C` means
reported Correct; every target batch was Valid. Aggregate RU is the sum of the
recorded target VC resource counts, not whole-file or helper cost. `OOR` is a
resource-exhausted, censored observation: its recorded work is shown, but the cost
of a successful proof is unknown. Resource counts can slightly exceed the stated
ceiling. Ratios use aggregate RU and are shown only when both operands are
Correct; no ratio is inferred from an OOR result. Counts are exact; ratios are
rounded to three decimals. No elapsed-time or executor measurement is used.

The unmodified inputs are the suite and standard-library sources at the compiled
source pin above. Isolation adds only `--isolate-assertions`; original source,
imports, project settings, inherited attributes and per-batch caps are retained.
All invocations use `--cores 1`, `--verification-time-limit 0`, the default zero
seed policy and normalized names. Suite cases retain `--allow-warnings`,
`--resource-limit 16000000`, `/normalizeDeclarationOrder:0`, and matched
`--type-system-refresh`/`--general-newtypes` values (false for legacy, true for
refresh). Library cases verify through `src/Std/dfyconfig.toml`, retaining its
refreshed resolver, general-newtype and other project options and
`/normalizeDeclarationOrder:1`. Arithmetic modules retain
`@DisableNonlinearArithmetic`.

| Target shorthand (exact declaration) | Original source | Resolver / axioms / order | Per-batch RU cap |
|---|---|---|---:|
| SiftDown (`PriorityQueue.SiftDown`) | `LitTest/dafny1/PriorityQueue.dfy` | legacy / a0 / 0 | 16,000,000 |
| RemoveFactor (`RemoveFactor`) | `LitTest/dafny4/Primes.dfy` | legacy / a0 / 0 | 16,000,000 |
| Join (`M3.UnionFind.JoinMaintainsReaches1`) | `LitTest/dafny4/UnionFind.dfy` | legacy / a0 / 0 | 16,000,000 |
| Append (`ExtensibleArray.Append`) | `LitTest/dafny1/ExtensibleArray.dfy` | refresh / a0 / 0 | 16,000,000 |
| DivStrong (`Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered`) | `Std/Arithmetic/DivMod.dfy` | refresh / a0 / 1 | 1,000,000 |
| Power (`Std.Arithmetic.Power.LemmaPowStrictlyIncreases`) | `Std/Arithmetic/Power.dfy` | refresh / a0 / 1 | 1,000,000 |
| DecodePadding (`Std.Base64.DecodeValidEncode1Padding`) | `Std/Base64.dfy` | refresh / a0 / 1 | 12,000,000 (existing `@ResourceLimit("12e6")`) |

`LitTest` is `Source/IntegrationTests/TestFiles/LitTests/LitTest`; `Std` is
`Source/DafnyStandardLibraries/src/Std`. Join retains
`--relax-definite-assignment`. Append uses complete-file invocations with no
symbol filter on both original and isolated inputs, with
`DOTNET_PROCESSOR_COUNT=2`; the remaining pairs use the declaration filter and
`DOTNET_PROCESSOR_COUNT=1`. These Append numbers are the matched refreshed/a0
reproduction, not the distinct legacy/a1 passing configuration. Library targets
without a source override retain the one-million-unit project cap.

| Target | Mode | Original: verdict / batches / total / max RU | Isolated: verdict / batches / total / max RU | Isolated / original, same mode |
|---|---|---|---|---:|
| SiftDown | Baseline | C / 1 / 10,315,363 / 10,315,363 | C / 74 / 3,724,318 / 708,974 | 0.361× |
| SiftDown | OFF | C / 1 / 10,315,363 / 10,315,363 | Unmeasured | Unknown |
| SiftDown | ON | OOR / 1 / 16,024,316 / 16,024,316 | C / 74 / 3,640,768 / 708,974 | Censored |
| RemoveFactor | Baseline | C / 1 / 15,158,230 / 15,158,230 | C / 13 / 172,272 / 39,290 | 0.011× |
| RemoveFactor | OFF | C / 1 / 15,158,230 / 15,158,230 | Unmeasured | Unknown |
| RemoveFactor | ON | OOR / 1 / 16,008,204 / 16,008,204 | C / 13 / 172,721 / 39,290 | Censored |
| Join | Baseline | C / 1 / 8,011,980 / 8,011,980 | C / 69 / 6,949,760 / 4,878,660 | 0.867× |
| Join | OFF | C / 1 / 8,011,980 / 8,011,980 | Unmeasured | Unknown |
| Join | ON | OOR / 1 / 16,027,333 / 16,027,333 | C / 69 / 8,466,671 / 6,378,062 | Censored |
| Append | Baseline | C / 1 / 8,651,815 / 8,651,815 | C / 63 / 26,960,785 / 3,360,158 | 3.116× |
| Append | OFF | C / 1 / 8,651,815 / 8,651,815 | Unmeasured | Unknown |
| Append | ON | OOR / 1 / 16,048,131 / 16,048,131 | C / 63 / 26,664,079 / 3,274,700 | Censored |
| DivStrong | Baseline | C / 1 / 663,295 / 663,295 | C / 9 / 453,230 / 276,137 | 0.683× |
| DivStrong | OFF | C / 1 / 663,295 / 663,295 | Unmeasured | Unknown |
| DivStrong | ON | OOR / 1 / 1,032,323 / 1,032,323 | C / 9 / 636,187 / 403,619 | Censored |
| Power | Baseline | C / 1 / 143,449 / 143,449 | C / 31 / 1,025,858 / 71,579 | 7.151× |
| Power | OFF | C / 1 / 143,449 / 143,449 | Unmeasured | Unknown |
| Power | ON | OOR / 1 / 1,024,729 / 1,024,729 | C / 31 / 1,129,769 / 71,821 | Censored |
| DecodePadding | Baseline | C / 1 / 2,644,805 / 2,644,805 | C / 93 / 9,522,460 / 2,969,048 | 3.600× |
| DecodePadding | OFF | C / 1 / 2,644,805 / 2,644,805 | Unmeasured | Unknown |
| DecodePadding | ON | OOR / 1 / 12,084,088 / 12,084,088 | C / 93 / 9,522,043 / 2,969,048 | Censored |

Isolated OFF was not measured for any of these seven pairs. Its verdict, batch
counts and costs remain unknown; original baseline/OFF equality does not supply
those missing measurements. All seven isolated baseline/ON target checks close
within their unchanged per-batch caps, but more batches can consume more total
work. The two useful completed-proof comparisons make that distinction explicit:

| Target | Isolated ON / original baseline total RU | Isolated ON / isolated baseline total RU |
|---|---:|---:|
| SiftDown | 0.353× | 0.978× |
| RemoveFactor | 0.011× | 1.003× |
| Join | 1.057× | 1.218× |
| Append | 3.082× | 0.989× |
| DivStrong | 0.959× | 1.404× |
| Power | 7.876× | 1.101× |
| DecodePadding | 3.600× | 1.000× |

These comparisons change proof batching; they are diagnostic cost observations,
not a uniform translator improvement or a whole-suite performance claim. In
particular, Append, Power and DecodePadding consume more aggregate resource units
than their original completed baseline proofs. Original benchmark inputs remain
unchanged.

## Triggering and fuel diagnosis

Making more instances available can increase search work: additional ground terms can activate sequence, map and arithmetic axioms. That does not mean any required premise is false or that the checker received less assertion fuel. No removal of standard assertion triggering subterms or reduction of standard assertion fuel was identified here.

The iterator comparison is concrete. The former call-interface obligation checks `R` at its ordinary interface layer; the new local caller obligation checks `R` at the stronger layer used by an immediate explicit assertion. Ordinary caller publication remains at its interface layer. `Push` requires node membership and validity, interval/content bounds, a suffix relation, then the recursive `R` relation. The failure is at that final precondition. Reverting it to lower fuel would defeat the assertion-equivalence goal. The solver trace instead shows increased sequence append/index and boxed-map matching in this context.

`LemmaModNegNeg` fails an unchanged source `assert ... by`, retaining the ordinary assertion-by subsumption policy. The newly checked induction-call requirements introduce preparation with havoced binders, which cannot be discarded as pure temporary preparation. Multiplication-wrapper equality and commutativity are prominent in the failing trace. Under the alternate declaration order both modes verify, supporting an order-sensitive arithmetic/quantifier interaction; this does not license removing required preparation or outer effects.

Reporting-only profiles were checked against previously captured parsed SMT streams, outcomes and exact resource counts; all matched. Thus the observations concern the original failing queries rather than a changed solver experiment. Periodic per-quantifier counters are not exact total or per-VC measures, and deep instantiation does not by itself prove an infinite matching loop. The traces identify likely pressure, not an independently validated minimal cause or a safe general translation transformation.

## Determinization configuration

The pinned Dafny already sets [`NormalizeNames = true`](https://github.com/erniecohen/dafny/blob/aa25d753ab1e3aebe4cc08d14313e705d741446b/Source/DafnyCore/DafnyOptions.cs#L344). Pinned Boogie [`GetNamer`](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Provers/SMTLib/SMTLibProcessTheoremProver.cs#L75) selects normalized naming at seed zero; its [`GetReorderedDeclarations`](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCGeneration/Checker.cs#L198) separately controls content-hash declaration ordering. Naming normalization and declaration-order normalization are different settings; adding an explicit naming option would repeat an already enabled policy.

The audit found no missing explicit zero Z3 seed setting: the pinned solver reports zero defaults for SMT and SAT random seeds, and Boogie supplies explicit seeds when a nonzero seed is requested. `/deterministicLiteralHashes:1` is unrecognized by both pinned native runtimes; it is not an available project setting for this candidate. No substitute solver or Boogie build was used.

The suite comparisons already use `/normalizeDeclarationOrder:0` and no wall-clock proof limit. The standard-library comparison intentionally retains the library gate's default declaration order. The [committed runner](../../.github/review/std-verdicts.py) documents baseline variability and earlier order0 difficulty; target-specific project options must also match its compiled library binary. A global library configuration change is a separate comparison, not an acceptance waiver.

For `Core.TryStructural` WF and `Numbers.Exp` WF, baseline/OFF/repeat have identical operational Boogie but different parsed SMT streams under default order. With order0 all three have identical streams and resources. This establishes a concrete emission/order source of variation. It does **not** explain every earlier larger OFF outlier: those larger costs were not reproduced, and each original outlier has not been matched to its exact alternate SMT stream. Strict library OFF equality remains rejected.

Order0 restores enabled `LemmaModNegNeg` and `DecodeValidEncode1Padding` locally but introduces baseline failures in `LemmaDivByMultipleIsStronglyOrdered`, `DecodeValidEncode1Padding` and `EncodeBVIsBase64`; other enabled failures remain. Better repeatability is not uniform proof improvement.

## Review disposition

The requested methodological scan, all-eleven localization, solver-input/profile diagnosis and configuration audit are recorded. No safe general performance repair is established. The broad enabled regressions, substantial cost increases and unresolved strict library OFF differences remain merge blockers under the original criteria. Preserve the accepted assertion-equivalence architecture and all original checking/visibility obligations; investigate any future translation change against a concrete generalizable diagnosis. This report neither changes expected verdicts nor waives acceptance. PR 168 remains draft, and PR 169 requires separate owner authorization. AI assisted this investigation.
