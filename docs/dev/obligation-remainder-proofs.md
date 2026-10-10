# Four persistent regressions: checked source proof repairs

**Historical focused source proofs; verifier implementation frozen for these experiments.** The owner asked to investigate the four cases that did not yield to diagnostic assertion isolation and drive them to proofs. The [portable patches](obligation-remainder-proofs/) below reconstruct those proofs without modifying the candidate's translation. They are review artifacts, **not applied changes to the suite or shipped standard library**. PR 168 remains draft; PR 169 is unchanged.

The exact compiler boundary for these experiments is implementation `1e6fa5126d8076983f0841629194dc64094f0b2f`, compiled-source pin `aa25d753ab1e3aebe4cc08d14313e705d741446b`, baseline `ab210b78b50adb5a192542897f0a00cdb40f3c35`, pinned Boogie and Z3 5.1.0. Original project/suite settings and per-batch resource ceilings were retained. No seed search, added axiom, unchecked premise, fuel override, contract weakening or translator-specific benchmark exception was introduced.

The later [final review](obligation-final-review.md) permits an experimental default-off merge after correctness and soundness closure; it retains these diagnostic patches and the original benchmark inputs.

## Success, failure and proof truth

The earlier successes are valid proofs of true claims. They are sensitive to context: changing batching, declaration order or the resolver can make an original proof exceed its resource ceiling. An earlier success is not thereby invalidated, and an exhausted query supplies no counterexample. Fixed-input repeats reproduced the original outcomes rather than alternating between success and failure. The evidence therefore supports **brittle successful proof searches and repeatable bounded failures**, not a blanket explanation of bad luck.

The repairs make the mathematical steps explicit and separate unrelated search. They have bounded repeat/configuration evidence at the same ceilings; this is not a guarantee under arbitrary future solver perturbations. Additional assertion-style support can activate more axioms and increase search even while preserving every legitimate premise. None of these repairs restores weaker checks or reduces the implicit check's assertion fuel.

## Four mathematical proofs

| Declaration and portable patch | Why the statement holds; what the source proof changes |
|---|---|
| `M2.SchorrWaite` — [patch](obligation-remainder-proofs/schorr-waite.patch) | The algorithm reverses graph pointers, but reachability refers to the initial graph. `ExtendInitialPath` uses the initial parent-to-child edge and initial prefix path to construct `Path.Extend(prefix,parent)` as a concrete initial-heap witness. It independently proves allocation, `ReachableVia`, and existential `Reachable`, then is called immediately before pointer reversal. Existing algorithm, invariants, specifications and reveals are retained. |
| `Std.Base64.EncodeBVIsBase64` — [patch](obligation-remainder-proofs/base64.patch) | Split bytes by length modulo three. The divisible prefix encodes without padding; the final one or two bytes give a four-character padded block. Two generic string lemmas prove the unpadded and concatenated cases using slice equalities. Existing byte-encoding equality/classification lemmas connect the result to `EncodeBV`. Local visibility separates string composition from bitvector implementation. All predicates and conclusions are unchanged. |
| `Std.Arithmetic.DivMod.LemmaModNegNeg` — [patch](obligation-remainder-proofs/modular-remainder.patch) | `x*d` has remainder zero, subtracting it preserves the remainder, and distributivity gives `x*(1-d) = x-x*d`. A calculation invokes four existing concrete arithmetic lemmas instead of discovering this through higher-order induction and broad multiplication automation. The theorem and its existing assertion-isolation attribute are unchanged. |
| `SnapTree.Iterator.MoveNext` — [patch](obligation-remainder-proofs/iterator.patch) | `R` describes the current root and rest stack. Compute the right-subtree `Push` before writing the iterator's `stack` and `N`, then assign the calculated next state. `Push` is read-only over existing nodes and does not modify iterator fields, so this reordering preserves the executable transition while keeping the structural proof in its current heap. The increment, preconditions, postconditions, original assertions and outer reveals are retained. |

The iterator's original failed check was the final recursive `R` precondition of `Push`, already in a single-check batch. Moving read-only work before iterator writes addresses that heap transition; more assertion isolation could not localize it further. Two preliminary helper-based repairs were rejected: one failed enabled legacy-resolver checks, and another regressed refreshed-resolver proofs. They are not the retained patch.

The Schorr-Waite repair also required proof closure: an intermediate empty-body helper passed enabled checking but failed baseline. Supplying the explicit initial path witness makes the retained helper independently prove in both. A parse-invalid intermediate experiment and an empty-selection wrapper were preserved as rejected diagnostics, not counted as proofs.

## Bounded resource costs of four source repairs

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

The table matches each original affected configuration to the same configuration
with all four portable patches applied together. Original sources are at the
compiled source pin above; the exact repaired files are reconstructed by the
linked patches. Every source and native component hash was checked in the retained
inputs. The repaired-file SHA-256 values are:

| Repaired file / patch | SHA-256 of complete repaired source |
|---|---|
| `SchorrWaite-stages.dfy` / [schorr-waite.patch](obligation-remainder-proofs/schorr-waite.patch) | `0be60363a55743acc2dfa43a5384ac38b61c67b787f9a9c168cabf4a7319e863` |
| `SnapshotableTrees.dfy` / [iterator.patch](obligation-remainder-proofs/iterator.patch) | `fe94ca756b8a49641bec3f87a2f6fb8c2fe4bbcc838b72cee153d24eb64d51f9` |
| `DivMod.dfy` / [modular-remainder.patch](obligation-remainder-proofs/modular-remainder.patch) | `339a0879436b7a8ee8a97e7d560ce4fac053f7296cfbeb02ba586ff3e0b1a6f2` |
| `Base64.dfy` / [base64.patch](obligation-remainder-proofs/base64.patch) | `fc7b033a01ca0cb90603b05a3fd835c9906bf42e81834df5130ba214ea79a6a4` |

All modes retain `--cores 1`, `--verification-time-limit 0`,
`DOTNET_PROCESSOR_COUNT=1`, the default zero seed policy and normalized names.
Suite targets use the original `--resource-limit 16000000`,
`--allow-warnings`, `/normalizeDeclarationOrder:0`, refreshed/legacy
`--type-system-refresh` and `--general-newtypes` settings. Schorr-Waite retains
`--allow-deprecation`; Iterator retains
`--solver-option=O:smt.qi.eager_threshold=80`. Library targets run through the
complete `src/Std/dfyconfig.toml` project, retaining its options and the existing
source attributes. No new isolation option or budget increase is applied.

| Target shorthand (exact declaration) | Matched affected configuration | Per-batch RU cap |
|---|---|---:|
| Schorr (`M2.SchorrWaite`) | legacy / a0 / declaration order 0 | 16,000,000 |
| Iterator (`SnapTree.Iterator.MoveNext`) | refresh / a0 / declaration order 0 | 16,000,000 |
| Mod (`Std.Arithmetic.DivMod.LemmaModNegNeg`) | refresh / a1 / default declaration order 1; existing assertion isolation | 1,000,000 |
| Encode (`Std.Base64.EncodeBVIsBase64`) | refresh / a1 / default declaration order 1 | 1,000,000 |

| Target | Mode | Original: verdict / batches / total / max RU | Patched: verdict / batches / total / max RU | Patched / original, same mode |
|---|---|---|---|---:|
| Schorr | Baseline | C / 1 / 6,952,077 / 6,952,077 | C / 1 / 2,083,397 / 2,083,397 | 0.300× |
| Schorr | OFF | C / 1 / 6,952,077 / 6,952,077 | C / 1 / 2,083,397 / 2,083,397 | 0.300× |
| Schorr | ON | OOR / 1 / 16,037,000 / 16,037,000 | C / 1 / 3,823,038 / 3,823,038 | Censored |
| Iterator | Baseline | C / 29 / 5,920,826 / 1,622,118 | C / 28 / 3,275,804 / 521,995 | 0.553× |
| Iterator | OFF | C / 29 / 5,920,826 / 1,622,118 | C / 28 / 3,275,804 / 521,995 | 0.553× |
| Iterator | ON | OOR / 29 / 21,520,666 / 16,059,620 | C / 28 / 3,347,345 / 615,328 | Censored |
| Mod | Baseline | C / 13 / 972,336 / 386,236 | C / 11 / 46,715 / 5,805 | 0.048× |
| Mod | OFF | C / 13 / 972,336 / 386,236 | C / 11 / 46,715 / 5,805 | 0.048× |
| Mod | ON | OOR / 13 / 1,660,560 / 1,023,167 | C / 11 / 46,740 / 5,805 | Censored |
| Encode | Baseline | C / 1 / 433,583 / 433,583 | C / 1 / 45,108 / 45,108 | 0.104× |
| Encode | OFF | C / 1 / 433,583 / 433,583 | C / 1 / 45,108 / 45,108 | 0.104× |
| Encode | ON | OOR / 1 / 1,061,524 / 1,061,524 | C / 1 / 46,895 / 46,895 | Censored |

The original ON results are censored, so none has a measured successful-proof
speedup ratio. Completed-proof comparisons are available against the original
baseline, and between patched ON and patched baseline:

| Target | Patched ON / original baseline total RU | Patched ON / patched baseline total RU |
|---|---:|---:|
| Schorr | 0.550× | 1.835× |
| Iterator | 0.565× | 1.022× |
| Mod | 0.048× | 1.001× |
| Encode | 0.108× | 1.040× |

The target table excludes independently checked added helpers. In the combined
run, `M2.ExtendInitialPath` used 29,368 / 29,368 / 28,913 RU in baseline/OFF/ON
(refresh/a0/order0); `Std.Base64.UnpaddedPrefixWithPaddingIsBase64` used
40,840 / 40,840 / 52,944 RU and `Std.Base64.UnpaddedStringIsBase64` used
13,709 / 13,709 / 13,762 RU (refresh/a1/default order). Each has one target
batch, so its maximum equals its aggregate. These helper figures are separate
from target cost and are not a complete surrounding-file cost comparison.

The proof restructuring changes target batch counts in Iterator and Mod while
retaining their original source isolation policy. The measurements support these
specific source repairs at the original caps; they do not establish a whole-suite
performance remedy, a new translator result, or costs for the later corrected
compiler. The four patches remain review artifacts, and the original suite and
standard-library inputs remain unchanged.

## Closure, controls and limits

All four patches applied together pass all 51 scheduled target/helper proof invocations: suite targets cover baseline/OFF/ON, both resolvers and both axiom settings; library targets cover baseline/OFF/ON, both axiom settings and the sampled declaration orders. This is a focused proof boundary, not an entire-project gate.

The repaired targets and every added helper have been checked independently. Original/repaired surrounding-file comparisons retain all declarations and WF checks rather than accepting a target whose helper or contract failed. Repeats and the combined-patch check use the same pinned compiler and unchanged ceilings.

- Schorr-Waite passes both resolvers and both additional-axiom settings. M2 and its M3 refinement close with the new lemma. The intentionally incomplete M0 stage still exhausts resources in matching original/repaired file runs; original M0/M1 trust findings remain visible.
- Base64 passes both declaration-order settings and both axiom settings. All selected encoder/classification support declarations close. Six unrelated original file failures persist, including `DecodeValidEncode1Padding`; this four-case task does not repair that separately isolated regression.
- Modular remainder passes both declaration-order settings and both axiom settings. Every direct arithmetic helper closes independently. Matched complete DivMod-file results introduce no failure and remove the requested enabled failure; other inherited arithmetic failures remain.
- Iterator passes baseline, feature OFF and feature ON with both resolvers and both axiom settings. Original/repaired complete-file checks cover the same declarations: every neighboring verdict and resource count matches in each mode. The existing concurrent-modification negative test and four unrelated insertion resource failures persist. Fixed-source repeats have identical solver streams. Both original/repaired audits report no findings.

Meaningful false controls reject a missing initial graph edge, a false `2-d` modular coefficient, malformed Base64 `AA=A`, and an incorrect iterator offset. Concrete Base64 strings and a two-node iterator witness, constructed with independently checked `Node.Init`, `Node.Build` and `Push`, check satisfiable intended cases. The wrong-increment iterator implementation has genuine Invalid checks in every mode; its enabled run also retains one resource-exhausted check. The separate false concrete Nil-offset control fails genuinely without exhaustion in either sampled mode. Audit/static source comparisons retain the existing trust boundary; no new assumption or unchecked declaration is used.

All six original/repaired source audits completed with byte-identical finding reports in each pair. The staged Schorr-Waite assumptions/nontermination declarations and unrelated library findings are unchanged; Iterator has no findings. A diagnostic wrapper completed the proofs before rejecting an unsupported audit option. Only the corrected audits were rerun; the failed wrapper is not relabeled as a successful complete execution. Source restructuring changes the generated batch shapes for Mod and Iterator, while retaining their existing isolation attributes and per-batch limits.

These are checked source proof repairs. They do not establish that the frozen verifier is uniformly better on unchanged programs, that the whole suite/library is green, or that a general translator performance remedy has been found. The [six original broad comparisons](obligation-followup-acceptance.md) remain their original evidence. The other seven localized regressions, significant cost changes and unresolved strict library default-OFF resource differences remain acceptance concerns. No whole-suite rerun or expected-table edit was made for this investigation.

## Applying the review artifacts

From this checkout, `git apply docs/dev/obligation-remainder-proofs/*.patch` applies all four independent source repairs. The patch paths name the original suite and library sources, and their original RUN commands/project options remain the verification boundary. Review the target, new helpers and their source dependencies independently; a filtered target alone does not certify a helper or contract. Complete-file checks intentionally retain the existing failures described above. No expected verdict table or compiler version marker is changed by these documentation artifacts.

## Additional methodological example

The Schorr-Waite investigation yields a concrete example of the intended sequential-postcondition benefit. An intermediate `ExtendInitialPath` helper has an empty body and three ordered postconditions: initial allocation of the concrete extended path; initial `ReachableVia` through that path; and initial existential `Reachable`. Enabled checking independently proves the helper: the checked concrete path fact supplies the witness for the later existential clause. Baseline fails that existential clause without an explicit witness proof. Repeated checks reproduce the distinction; a control without the needed edge fails genuinely.

This is a derived example from the investigated proof, not a claim that a pre-existing redundant assertion was removed from the suite. The portable repair deliberately retains the explicit witness so its helper also closes on baseline. It supplements the existing assertion-invariance controls and the earlier scan, which found only already-redundant assertion copies.

AI assisted the source proofs and investigation. No merge approval is requested.
