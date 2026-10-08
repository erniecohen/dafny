# Current affected-declaration seed classification

This diagnostic uses the current compiled product `072175269`, baseline
`ab210b78b` and Z3 5.1.0. It preserves source hints and resource ceilings.
Suite commands retain the exact original full-run options and one core per
process; the library retains its complete original project and two-core setting.
Every matrix covers baseline/default-off/enabled, both axiom settings and seeds
0, 1, 7. `V` means Correct, `R` means OutOfResource, and `E` means Errors.
A vector is in seed order. These are filtered diagnostics; recorded seed-zero
full-control movements must not be hidden or treated as whole-gate acceptance.

All four matrices have durable completed evidence: suite failures 108,
suite cost 198, library cost 144, and library failures 198, for 648 observations.
The first three have zero seed-zero outcome movements relative to their
completed full-gate controls. The recovered library failure matrix has four
recorded movements and a platform boundary, detailed below. An earlier attempt
without a durable artifact receipt is excluded. Power has its separate completed
native/causal matrix. The [complete gates](obligation-current-complete-gates.md)
remain rejected.

S13's both-partial variance rule requires **both** baseline and enabled vectors
to contain successful and unsuccessful seeds. A partly successful baseline with
an all-resource-exhausted enabled vector does not meet that rule. A successful
baseline with an enabled-only seed failure is recorded separately. This
classification diagnoses fixed-run search behavior; it does not weaken a check,
change a limit or add a proof hint.

## Suite failures

| Declaration | Additional axioms | Baseline | Default off | Enabled | Classification |
| --- | --- | --- | --- | --- | --- |
| `ExtensibleArray.Append (correctness)` | Off | VVR | VVR | RVV | S13 both-partial solver/resource variance |
| `ExtensibleArray.Append (correctness)` | On | VVR | VVR | RVV | S13 both-partial solver/resource variance |
| `SnapTree.Iterator.MoveNext (correctness)` | Off | VRR | VRR | RVV | S13 both-partial solver/resource variance |
| `SnapTree.Iterator.MoveNext (correctness)` | On | VRR | VRR | RVV | S13 both-partial solver/resource variance |
| `FormArmy (correctness)` | Off | VRR | VRR | RRR | Enabled exhausts resources at every selected seed; baseline is partly successful; remains an open resource movement |
| `FormArmy (correctness)` | On | VRR | VRR | RRR | Enabled exhausts resources at every selected seed; baseline is partly successful; remains an open resource movement |
| `AltPrimeDefinition (correctness)` | Off | VVR | VVR | RRR | Enabled exhausts resources at every selected seed; baseline is partly successful; remains an open resource movement |
| `AltPrimeDefinition (correctness)` | On | VVR | VVR | RRR | Enabled exhausts resources at every selected seed; baseline is partly successful; remains an open resource movement |
| `Composite (correctness)` | Off | VVV | VVV | RVV | Enabled has a seed-sensitive resource failure; baseline verifies every selected seed |
| `Composite (correctness)` | On | VVV | VVV | RVV | Enabled has a seed-sensitive resource failure; baseline verifies every selected seed |
| `RemoveFactor (correctness)` | Off | VRR | VRR | RRR | Enabled exhausts resources at every selected seed; baseline is partly successful; remains an open resource movement |
| `RemoveFactor (correctness)` | On | VRR | VRR | RRR | Enabled exhausts resources at every selected seed; baseline is partly successful; remains an open resource movement |
## Suite cost-review union

| Declaration | Additional axioms | Baseline | Default off | Enabled | Classification |
| --- | --- | --- | --- | --- | --- |
| `Fill_All (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Fill_All (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Fill_True (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Fill_True (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `TheoremSeq (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `TheoremSeq (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Search (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; cost direction depends on seed |
| `Search (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; cost direction depends on seed |
| `FindWinner (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; cost direction depends on seed |
| `FindWinner (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; cost direction depends on seed |
| `PriorityQueue_direct.AboutMin (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `PriorityQueue_direct.AboutMin (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `NthAppendB (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `NthAppendB (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `FlyRobotArmy (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `FlyRobotArmy (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `sorted_ascending (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `sorted_ascending (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `INDUCTION_EVEN_ODD (correctness)` | Off | VRR | VRR | VRV | S13 both-partial solver/resource variance |
| `INDUCTION_EVEN_ODD (correctness)` | On | VRR | VRR | VRV | S13 both-partial solver/resource variance |
| `FibSumManual (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `FibSumManual (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
## Library cost-review union

| Declaration | Additional axioms | Baseline | Default off | Enabled | Classification |
| --- | --- | --- | --- | --- | --- |
| `Std.Arithmetic.DivMod.LemmaDivDecreases (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.DivMod.LemmaDivDecreases (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.DivMod.LemmaDivIsDivRecursive (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.DivMod.LemmaDivIsDivRecursive (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.DivMod.LemmaDivIsStrictlyOrderedByDenominator (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.DivMod.LemmaDivIsStrictlyOrderedByDenominator (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.DivMod.LemmaMulModNoopRight (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.DivMod.LemmaMulModNoopRight (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.Mul.LemmaMulEqualityConverse (correctness)` | Off | VVV | VVV | VRV | Enabled has a seed-sensitive resource failure; baseline verifies every selected seed |
| `Std.Arithmetic.Mul.LemmaMulEqualityConverse (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.Mul.LemmaMulLeftInequality (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Arithmetic.Mul.LemmaMulLeftInequality (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.JSON.ZeroCopy.Serializer.MembersSpec (well-formedness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.JSON.ZeroCopy.Serializer.MembersSpec (well-formedness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed; sampled default-off resource difference is retained |
| `Std.Unicode.Utf8EncodingScheme.LemmaDeserializeSerialize (correctness)` | Off | VVV | VVV | VVV | Every selected seed verifies; enabled uses more resources at every selected seed |
| `Std.Unicode.Utf8EncodingScheme.LemmaDeserializeSerialize (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies; enabled uses no more resources at these selected seeds |

## Library failures

This completed matrix has 198 observations on macOS arm64. The complete library
controls used Linux x86-64; both use the exact accepted platform-specific native
compiler and Z3 5.1.0. Four enabled seed-zero outcomes move from OutOfResource
in the full Linux controls to Correct here: `LemmaModMultiplesVanish` with
additional axioms on, `LemmaMulModNoopLeft` in both settings, and `LemmaRoundDown`
with additional axioms on. These are recorded platform/scope boundaries, not
an exact replay of the Linux full gate. The earlier attempt without a durable
artifact receipt remains excluded. Power subtraction is covered by its separate
complete native and causal matrices.

| Declaration | Additional axioms | Baseline | Default off | Enabled | Classification |
| --- | --- | --- | --- | --- | --- |
| `Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered (correctness)` | Off | VVR | VVR | RRR | Enabled exhausts resources at every selected seed; baseline is partly successful; remains an open resource movement |
| `Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered (correctness)` | On | VVR | VVR | RRR | Enabled exhausts resources at every selected seed; baseline is partly successful; remains an open resource movement |
| `Std.Arithmetic.DivMod.LemmaModMultiplesVanish (correctness)` | Off | VVV | VVV | RVR | Enabled has a seed-sensitive resource failure; baseline verifies every selected seed |
| `Std.Arithmetic.DivMod.LemmaModMultiplesVanish (correctness)` | On | VVV | VVV | VRR | Enabled has a seed-sensitive resource failure; baseline verifies every selected seed |
| `Std.Arithmetic.DivMod.LemmaMulModNoopLeft (correctness)` | Off | VVV | VVV | VRV | Enabled has a seed-sensitive resource failure; baseline verifies every selected seed |
| `Std.Arithmetic.DivMod.LemmaMulModNoopLeft (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies |
| `Std.Arithmetic.DivMod.LemmaRemainder (correctness)` | Off | VVV | VVV | RRR | Enabled exhausts resources at every selected seed; baseline verifies every selected seed |
| `Std.Arithmetic.DivMod.LemmaRemainder (correctness)` | On | VVV | VVV | RRR | Enabled exhausts resources at every selected seed; baseline verifies every selected seed |
| `Std.Arithmetic.DivMod.LemmaRoundDown (correctness)` | Off | VVV | VVV | RRR | Enabled exhausts resources at every selected seed; baseline verifies every selected seed |
| `Std.Arithmetic.DivMod.LemmaRoundDown (correctness)` | On | VVV | VVV | VVR | Enabled has a seed-sensitive resource failure; baseline verifies every selected seed |
| `Std.Arithmetic.Power.LemmaPowStrictlyIncreases (correctness)` | Off | VVV | VVV | RRR | Enabled exhausts resources at every selected seed; baseline verifies every selected seed |
| `Std.Arithmetic.Power.LemmaPowStrictlyIncreases (correctness)` | On | VVV | VVV | VVV | Every selected seed verifies |
| `Std.Base64.DecodeValidEncode1Padding (correctness)` | Off | VVR | VVR | RVV | S13 both-partial solver/resource variance |
| `Std.Base64.DecodeValidEncode1Padding (correctness)` | On | RRV | RRV | VVV | Enabled verifies every selected seed; baseline is partly successful |
| `Std.Base64.EncodeBVIsBase64 (correctness)` | Off | RRR | RRR | RRR | Baseline and enabled exhaust resources at every selected seed |
| `Std.Base64.EncodeBVIsBase64 (correctness)` | On | VRR | VRR | RRR | Enabled exhausts resources at every selected seed; baseline is partly successful; remains an open resource movement |
| `Std.Collections.Seq.LemmaMaxOfConcat (correctness)` | Off | VVV | VVV | RRR | Enabled exhausts resources at every selected seed; baseline verifies every selected seed |
| `Std.Collections.Seq.LemmaMaxOfConcat (correctness)` | On | VVV | VVV | RRR | Enabled exhausts resources at every selected seed; baseline verifies every selected seed |
| `Std.JSON.ZeroCopy.Deserializer.Sequences.Elements (well-formedness)` | Off | VRV | VRV | RRV | S13 both-partial solver/resource variance; sampled default-off resource difference is retained |
| `Std.JSON.ZeroCopy.Deserializer.Sequences.Elements (well-formedness)` | On | VRV | VRV | RRV | S13 both-partial solver/resource variance; sampled default-off resource difference is retained |
| `Std.Termination.TerminationMetric.MultisetOrdinalDecreasesToSubMultiset (correctness)` | Off | VVV | VVV | RRR | Enabled exhausts resources at every selected seed; baseline verifies every selected seed |
| `Std.Termination.TerminationMetric.MultisetOrdinalDecreasesToSubMultiset (correctness)` | On | VVV | VVV | RRR | Enabled exhausts resources at every selected seed; baseline verifies every selected seed |

`LemmaRemainder`, `LemmaMaxOfConcat` and
`MultisetOrdinalDecreasesToSubMultiset` exhaust resources at all selected enabled
seeds while baseline verifies all three, under both axiom settings.
`LemmaRoundDown` and `LemmaPowStrictlyIncreases` have the same pattern with
additional axioms off. These are stable sampled resource regressions, not S13
both-partial variance. The JSON `Elements` well-formedness target and the
one-padding Base64 decoding target (axioms off) are both-partial.

All baseline/default-off outcome vectors match in this matrix. The JSON
`Elements` target retains a seed-zero default-off resource difference in both
axiom settings. No resource policy or complete-gate rejection is waived.

## Interpretation and remaining limits

ExtensibleArray and the snapshot-tree iterator meet S13's both-partial rule.
FlyingRobots and two Primes targets do not: enabled mode exhausts resources at
every selected seed. Composite has a candidate-only seed failure. Several cost
cases verify at every seed while using more resources; that is a measured cost
regression, not a proof failure. `INDUCTION_EVEN_ODD` is both-partial.
`MulEqualityConverse` acquires a resource failure at one selected seed with
additional axioms off, despite the full seed-zero cost-review case verifying.

Default-off suite vectors match exactly in these matrices. The library cost
matrix retains a MembersSpec resource movement with additional axioms on.
The [actual query comparison](obligation-default-off-query-diagnosis.md)
shows identical logical content after bijective generated-name renaming and
changed static ordering. It cannot waive the full library's exact cost rule.
Overall acceptance and the independent soundness review remain open; no uniform
solver-improvement claim is made. No implementation is ported to #169.
