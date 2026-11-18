# Issue 284 research package

This package contains one documentation patch, the resulting document, a short review, exact Boogie/Dafny source witnesses and repair schemas, and two SMT files. It contains no repository snapshot or toolchain.

Apply `typeencoding-a-soundness.patch` at the root of the Dafny repository. It adds only `docs/dev/design/typeencoding-a-soundness.md`. The standalone copy at that path is provided for reading without applying the patch. Source/product changes discussed in the report are proposals, not applied code changes.

Pins:

- Dafny: `25203d9115f8f104b325c8a058bf41051bdbbe69`
- Boogie: `45a8869bb43456f126577f5ba6fd24725ed6e4d9`

## Evidence and expectations

| File | Meaning and expected result |
|---|---|
| `examples/char-source.bpl` | Satisfiable many-sorted theory, false assertion. Pinned erasure is logically inconsistent; no default verifier outcome is claimed. |
| `examples/iset-source.bpl` | Satisfiable many-sorted Boolean-map theory, false assertion. Pinned erasure is logically inconsistent; no default verifier outcome is claimed. |
| `examples/axioms-alone-counterexample.bpl` | Source assertion has a singleton-sort countermodel; erased assertion is valid although the erased axioms are consistent. |
| `examples/char-repair.bpl` | Standalone local guarded-character schema; negative assertion is semantically false. Does not include other prelude families. |
| `examples/map-repair.bpl` | Standalone target-schema repair with canonical Boolean store payloads; has an infinite-carrier model. Negative assertion must not be proved by a sound complete reasoning chain; a solver may return unknown. |
| `examples/character-controls.dfy` | Four true character-range lemmas and one intentional false assertion. Proposed repair must retain the positive results and reject the final assertion. Not run here. |
| `examples/iset-controls.dfy` | True iset equality followed by an intentional false assertion. Not an observed old-version exploit. Not run here. |
| `examples/certificate-accepted.bpl` | Illustrates an accepted fragment of the proposed syntactic comparison: Read is correct, Negative is incorrect. Checker not implemented. |
| `proofs/iset-proof-obligations.smt2` | Four checked implication obligations. Z3 4.12.1 returned four `unsat` results. Later stages explicitly use previously proved consequences. |
| `proofs/iset-erased-core.smt2` | Unassisted quantified kernel. Logical result is unsatisfiable; observed result was `unknown` at 15 seconds under Z3 4.12.1 and 5.1.0. |

Run the SMT obligations with `z3 proofs/iset-proof-obligations.smt2`. A toolchain worker can run the Boogie source witnesses with `/typeEncoding:a /prune:0` to preserve the intended axiom context and compare against monomorphic/guarded encoding. Do not infer an operational exploit from the semantic expectations in this table, and do not treat timeout/unknown as either a model or a proof.

No pinned end-to-end build, checker implementation, default Dafny false-acceptance reproduction, or repaired regression-verdict campaign is claimed. The report identifies those limits and the remaining whole-theory proof obligation.
