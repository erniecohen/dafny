# Integer arithmetic correspondence

This is the source argument for the opt-in B3 integer division/modulo slice in
issue #124. It is conditional on the separately reviewed state/control
normalization and the existing external solver boundary. It is not a verified
soundness theorem for the complete B3 pipeline.

## Operators and direct bodies

`B3Normalizer.Binary` maps typed integer `Div` and `Mod` to the existing normalized
operators. `RawAstBuilder` maps those to B3 `Div` and `Mod`;
`RSolver.RExpr.OperatorToString` emits SMT `div` and `mod`. The vendored typing
predicate and executable checker already require integer operands for both.

`TryNativeIntegerBody` additionally accepts a monomorphic function with exactly
two integer parameters and integer result whose actual typed `Function.Body` is
exactly `formal0 div formal1` or `formal0 mod formal1`. Formal declaration object
identities, operand order, arity and types are checked. Recognition does not use
the function's name, an inline attribute, or detached definition metadata.

The pinned native translator's `TranslateFunctionCall` calls `ApplyExpansion`,
which expands every nonnull typed `Function.Body`, then substitutes its actual
arguments for the formals. The normalized operation is precisely that
substitution for these two recognized shapes. See
[pinned Boogie2VCExpr](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCExpr/Boogie2VCExpr.cs#L1518).

The ordinary prelude's source definition syntax does not by itself establish a
nonnull `Function.Body`: the pinned parser constructs `DefinitionAxiom` for a
function without `:inline`. Therefore this initial direct-body route alone does
not establish support for Dafny's default arithmetic wrappers. An active axiom
route requires a separate reviewed eligibility check.

## Model direction and zero

For every integer `a` and nonzero `b`, Euclidean division is the unique pair
`(q,r)` with `a = b*q + r` and `0 <= r < abs(b)`. The quotient rounds down for a
positive divisor and up for a negative divisor. In particular, a negative
dividend does not justify C# integer truncation. Both source and target operators
use the same SMT integer theory; the normalizer does no constant evaluation.
See [SMT integer theory](https://smt-lib.org/theories-Ints.shtml).

At a zero divisor, SMT arithmetic is total but underspecified. Fix the source
model's interpretations of division and modulo at zero and use those same
interpretations in the target model. No value is chosen by the normalizer, and
the Euclidean identity is not imposed at zero. Repeated occurrences retain the
same interpretation. No new arithmetic axiom, assertion, or nonzero assumption
is emitted. Existing source assertions, including Dafny's `DivisorNonZero`
checks, retain their original positions, learning modes and source identities.

Take any model and execution witnessing a failing source obligation. Rename its
variables and demanded declarations as in the existing normalization. Keep the
integer domain and all arithmetic interpretations, including their zero cases.
Direct operators have the same denotation, and recognized direct bodies have
the same denotation by formal substitution. Interpret any retained opaque
functions as their source interpretations. Omitted source assumptions impose no
additional target restriction. The same failing obligation therefore has a
normalized countermodel, subject to the separate state/control correspondence.
This is the direction required for normalized validity to imply source validity.
It does not assert that every target countermodel extends to the full source
prelude, or that proof costs match the native backend.

## Evidence boundary

The new structural controls inspect typed source, operand order, body eligibility,
source nonmutation, the absence of synthesized zero guards and the survival of
existing Dafny divisor checks. These controls are source-only and unexecuted at
introduction. They do not establish solver verdicts.

`RawAst/Values.dfy` still has placeholder value/type definitions, and the
vendored `Semantics.dfy` is commented out. Existing library contracts establish
structural/type and solver-state properties; this prose does not convert them
into a literal/source-model or complete verifier soundness theorem. Real sorts,
rational literals, arithmetic coercions and power remain outside this patch.
