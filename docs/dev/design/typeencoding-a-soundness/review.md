# Review of issue 284

## Findings first

- **Blocking — raw `char` makes the erased theory inconsistent:** it bounds `U` to 65,536 or 1,112,064 values, while other axioms inject all integers into `U`. [Character axioms](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L123-L143).
- **Blocking — Boolean maps independently make the erased theory inconsistent:** raw read-over-write stores arbitrary `U` values, but ISet extensionality compares decoded booleans; together they make `U -> bool` injective. [Map rows](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L298-L463), [extensionality](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L888-L892).
- **A char repair needs two translator changes:** char-based subset/newtype definitions and checked introductions must retain parent-character membership. [Type definitions](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L1239-L1253), [obligation checks](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.TypeObligations.cs#L26-L34).
- **Finite datatypes are not an ambient-domain collapse:** their constructor exhaustiveness is guarded by Dafny membership. [Datatype axioms](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L28-L48).
- **The paper does not supply the requested unrestricted theorem:** its arguments encoding relies on typed e-matching, a procedural premise absent from a general SMT model argument. [Leino–Rümmer §3.1](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/12/krml199.pdf).

The Boolean-map contradiction is small. If two raw payloads decode to the same Boolean, storing either at the same key produces maps with identical decoded reads. Extensionality equates those maps, and raw read-over-write equates their payloads. Therefore the decoder is injective, impossible with three distinct `U` values. This argument does not use characters or the removed reverse casts.

Four SMT implication obligations checking that derivation returned `unsat` under Z3 4.12.1. The complete quantified kernel returned `unknown` at 15 seconds under both Z3 4.12.1 and 5.1.0. **No compact Dafny program was observed falsely verifying under the pinned default pipeline.** Semantic inconsistency and observed default false acceptance are different claims.

The ZIP contains one patch adding `docs/dev/design/typeencoding-a-soundness.md`, a standalone copy of the report, exact source witnesses and local repair schemas, Dafny positive/negative controls, and the SMT obligations. The report covers all 291 explicit prelude axioms in 46 rows and 25 translator/VC families.

I recommend the guarded character repair and canonical-payload weakening of native-result map rows as the smallest first repairs. The report provides exact Boogie text and identifies the necessary translator edits. Those local repairs remove the demonstrated obstructions; **the entire repaired translation has not been proved sound**.

The conservative checker specification compares the complete target counterexample formula against an explicit representation-guard translation using checked syntactic weakening/normalization certificates. It includes the negated VC and generated map/cast axioms. It rejects unsupported formulas as uncertified; it does not declare every structurally plausible family safe. No implementation or empirical rejection-rate benchmark is claimed.

## Required closing assessment

1. **What is settled:** the complete pinned `:a` theory is semantically inconsistent for two independent reasons. The stated representation-guard comparison condition suffices for encoding soundness; current output does not satisfy it. A complete proof for the locally repaired output remains open.
2. **What is expected to change:** character axioms, two char-refinement optimizations, and native-result map support axioms or the alternative map representation. Source meanings should be preserved. Exact solver verdict/resource preservation still requires the repository's measured gates.
3. **The next question:** does the jointly repaired collection/boxing/handle/comprehension theory preserve every admitted countermodel? The report answers why local models are insufficient and specifies the remaining closure obligations.
4. **Feedback on the request:** the pins and demand for a checkable condition were useful. Include negated VCs and all encoder-generated axioms explicitly; distinguish a semantic theorem from a prover-discipline argument and finite-corpus verdict preservation from preservation of logical consequences.
5. **Why stopped:** the review, audits, counterexamples, local repairs and checker specification are delivered; the positive blanket theorem is refuted. The first unresolved substantive item is item 1's whole-theory countermodel proof after repairs. A pinned-default Dafny false-acceptance reproducer and measured verdict-preservation campaign are also unfinished.
