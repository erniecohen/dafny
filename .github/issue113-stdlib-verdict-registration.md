# Shipped Standard Library verdict registration

The [first complete shipped review](https://github.com/erniecohen/dafny/actions/runs/37252682492) recorded four declaration changes with Z3 5.1.0 and additional axioms off, seven with additional axioms on, and one with Z3 4.12.1 and additional axioms off. Register only those rows and their main Std summary/location rows. The Z3 4.12.1 additional-axiom table is unchanged. All other rows, source contracts, literal commands, project budgets and declaration budgets retain their original bytes.

The [matched default-off compiler comparison](https://github.com/erniecohen/dafny/actions/runs/37252680850) uses frozen old inputs with a baseline composing the separately scoped existing-language repairs and with the extended-newtype compiler. In all four shipped solver/axiom cohorts, every declaration outcome and every comparable raw translated program agrees between those two compilers. Each historical declaration movement below appears on both arms and agrees with the first full review. These registrations preserve the inherited composite-repair verdicts; they do not attribute each movement to one component or establish that every Std obligation verifies.

| Solver | Additional axioms | Declaration | Original | Repaired and feature |
|---|---|---|---|---|
| 5.1.0 | off | `Arithmetic.DivMod.LemmaMultiplyDivideLe (correctness)` | Correct | OutOfResource |
| 5.1.0 | off | `Arithmetic.DivMod.LemmaRoundDown (correctness)` | OutOfResource | Correct |
| 5.1.0 | off | `Base64.DecodeValidPartialsFrom2PaddedSeq (well-formedness)` | Correct | OutOfResource |
| 5.1.0 | off | `Base64.DecodeValidUnpaddedPartialFrom1PaddedSeq (well-formedness)` | OutOfResource | Correct |
| 5.1.0 | on | `Arithmetic.DivMod.LemmaModNegNeg (correctness)` | Correct | OutOfResource |
| 5.1.0 | on | `Arithmetic.DivMod.LemmaMultiplyDivideLe (correctness)` | Correct | OutOfResource |
| 5.1.0 | on | `Arithmetic.DivMod.LemmaRoundDown (correctness)` | OutOfResource | Correct |
| 5.1.0 | on | `Base64.DecodeValidEncode1Padding (correctness)` | Correct | OutOfResource |
| 5.1.0 | on | `Base64.DecodeValidPartialsFrom2PaddedSeq (well-formedness)` | Correct | OutOfResource |
| 5.1.0 | on | `Base64.DecodeValidUnpaddedPartialFrom1PaddedSeq (well-formedness)` | OutOfResource | Correct |
| 5.1.0 | on | `Base64.EncodeBVIsBase64 (correctness)` | Correct | OutOfResource |
| 4.12.1 | off | `Arithmetic.DivMod.LemmaMultiplyDivideLt (correctness)` | Errors | Correct |

For Z3 5.1.0, the off cohort retains 7,588 verified, two errors and 22 out-of-resource results; changed diagnostic locations are part of the registered run row. The on cohort changes from 7,590 verified, two errors and 20 out-of-resource results to 7,587 verified, two errors and 23 out-of-resource results. For Z3 4.12.1 off, the previous 7,611 verified and one error becomes 7,612 verified and zero errors. Its on cohort retains its original table. OutOfResource results remain unproved obligations. An expected-verdict gate can accept a recorded negative or resource failure without proving that obligation.

The original Dafny bodies are unchanged. The independently scoped allocation repair removes an unsound reads-to-allocation converse and supplies guarded allocation introductions; other dependencies repair existing cycles, productivity consumers and backend behavior. Their composition changes the original translation context before the enhancement is added. The matched repaired-to-feature raw programs are byte-identical. Individual causal attribution of each historical proof or cost movement has not been isolated, and this registration makes no such claim.

Exact resource compatibility remains unresolved. In the main Std part, 47 of 2,108 declaration resource maps differ with Z3 5.1.0 off, 77 on, 211 with Z3 4.12.1 off and 18 on, despite the matched outcomes and raw programs. The [solver-session diagnostic and source review](../../docs/dev/extended-newtype-bases-query-session-cost.md) explains the observed command-order/session boundary without introducing a tolerance or changing costs in the oracle. This verdict-only commit neither waives that requirement nor makes the diagnostic workflow's success final feature acceptance. The strict final integration gate and enabled semantic/lifecycle/backend checks remain required.
