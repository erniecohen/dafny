# Development Standard Library verdict registration

The first full feature review [run](https://github.com/erniecohen/dafny/actions/runs/37243414478) failed the original Standard Library expectations. Register only its five changed declaration rows with Z3 5.1.0 and six with Z3 4.16.0, plus each main Std summary row. Every other row, source contract, project/declaration budget and canonical command remains unchanged.

The subsequent [matched three-compiler comparison](https://github.com/erniecohen/dafny/actions/runs/37248167583) uses the original development compiler, a composition of independently reviewed existing-language repairs, and the extended-newtype compiler with the feature omitted. All 2,116 main Std declaration outcomes and all 85 raw translated programs are identical between repaired and feature compilers, separately for each solver. All historical verdict movements occur on the repaired baseline alone. The six TargetSpecific parts also preserve every verdict. This is evidence of no additional feature verdict movement on this cohort, not evidence that every Std obligation verifies.

| Solver | Declaration | Original | Repaired and feature |
|---|---|---|---|
| 5.1.0 | `Std Std.Arithmetic.DivMod.LemmaMultiplyDivideLe (correctness)` | Errors | Correct |
| 5.1.0 | `Std Std.Arithmetic.DivMod.LemmaMultiplyDivideLt (correctness)` | Errors | Correct |
| 5.1.0 | `Std Std.Base64.DecodeValidEncode2Padding (correctness)` | Correct | OutOfResource |
| 5.1.0 | `Std Std.Base64.EncodeBVLengthCongruentToZeroMod4 (correctness)` | OutOfResource | Correct |
| 5.1.0 | `Std Std.Producers.LimitedProducer.Invoke (correctness)` | OutOfResource | Correct |
| 4.16.0 | `Std Std.Arithmetic.DivMod.LemmaModNegNeg (correctness)` | Correct | OutOfResource |
| 4.16.0 | `Std Std.Arithmetic.DivMod.LemmaMultiplyDivideLt (correctness)` | Correct | Errors |
| 4.16.0 | `Std Std.Arithmetic.DivMod.LemmaRoundDown (correctness)` | OutOfResource | Correct |
| 4.16.0 | `Std Std.Base64.DecodeValidEncode2Padding (correctness)` | Correct | OutOfResource |
| 4.16.0 | `Std Std.Base64.EncodeBVIsBase64 (correctness)` | Correct | OutOfResource |
| 4.16.0 | `Std Std.Producers.ConcatenatedProducer.Invoke (correctness)` | Correct | OutOfResource |

The gains and losses are automatic proof changes under the repaired translation context. The Dafny source bodies of the moved declarations remain unchanged. Some translated arithmetic implementations gain the independently reviewed #132 lambda-allocation introduction; other changed declarations have identical translated bodies but a changed surrounding allocation context. Repaired-to-feature translated bodies and complete translated programs are byte-identical. The independent #132 repair removes the unsound reads-to-allocation converse and adds specifically guarded lambda/handle allocation introductions; these change the background formulas even for ordinary callbacks. The other independent repairs retain their separate issue and regression evidence. Individual repair attribution for each cost movement is not isolated, so this registration does not claim that a particular removal alone caused each changed outcome. In particular, an OutOfResource result is preserved as a loss; the failed arithmetic assertion is a loss of automatic proof, not a mathematical refutation or permission to weaken its contract.

For Z3 5.1.0 the Std summary is 7,617 verified, one error and five out-of-resource results. For Z3 4.16.0 it is 7,617 verified, one error and five out-of-resource results. The prior original matrices were already non-green proof developments. The suite acceptance gate compares expected outcomes; it does not erase those failures.

Strict resource equality is a separate unresolved obligation. The main matched comparison has 154/2,116 changed declaration resource counts with Z3 5.1.0 and 54/2,116 with Z3 4.16.0 despite identical raw translated programs. The [targeted solver-session diagnostic](https://github.com/erniecohen/dafny/actions/runs/37250405693) retains actual queries and session histories; no resource tolerance or oracle cost adjustment is introduced by this verdict registration. Full old-input suite/Std compatibility and the final enabled gate remain required.
