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

## Active universal definitions

`TryNativeIntegerAxiom` supports the ordinary default-prelude route separately.
The pinned parser turns a source definition without `:inline` into a
`DefinitionAxiom`; Dafny marks prelude functions `AlwaysRevealed`. See
[parser construction](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/BoogiePL.atg#L528),
[prelude declaration setup](../../../Source/DafnyCore/Verifier/BoogieGenerator.cs),
and [actual integer wrappers](../../../Source/DafnyCore/DafnyPrelude.bpl).

Eligibility requires the exact `DefinitionAxiom` object in the input program's
top-level declarations and an `AlwaysRevealed`, monomorphic, two-integer-input,
integer-result function without a direct body. The axiom must be an unconditional
universal quantifier with zero type parameters and exactly two distinct integer
binders without where clauses. Its body must be an equality whose left side is a
call to that same function object, with no type instantiation and exactly the
ordered binders as arguments. Its right side must be native `Div` or `Mod` over
those same two argument declaration objects in the same order. At most one typed
identity `TypeCoercion` around the defining call is removed, as emitted by
`CreateDefinitionAxiom`; arbitrary coercions and reversed equalities are not
recognized.

These checks deliberately exclude detached metadata, equivalent but differently
identified axiom objects, guarded or existential definitions, extra/repeated or
reordered binders, captured constants, reordered operands, other functions and
closed instances of generic definitions. An unrecognized definition receives the
existing conservative treatment or unsupported diagnostic; its name never
determines its arithmetic meaning.

In every source model satisfying this eligible active axiom, instantiate its
universal binders with the values of the normalized actual arguments. The exact
ordered bijection makes this instance `F(a,b) = a div b` or `F(a,b) = a mod b` for
every pair of integers. Replacing the source call therefore preserves denotation
in every such model. This includes `b = 0`: the equation uses the source model's
own total, underspecified arithmetic interpretation. No nonzero antecedent is
invented. The original universal axiom is used as a source premise for the
substitution and is omitted from the target; no new global axiom is asserted.

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
Direct operators have the same denotation, recognized direct bodies have the
same denotation by formal substitution, and eligible active universal definitions
justify the same substitution in every source model. Interpret any retained opaque
functions as their source interpretations. Omitted source assumptions impose no
additional target restriction. The same failing obligation therefore has a
normalized countermodel, subject to the separate state/control correspondence.
This is the direction required for normalized validity to imply source validity.
It does not assert that every target countermodel extends to the full source
prelude, or that proof costs match the native backend.

## Evidence boundary

The new structural controls inspect typed source, operand order, body eligibility,
source nonmutation, the absence of synthesized zero guards and the survival of
existing Dafny divisor checks. The real-input corpus additionally specifies
positive and negative divisor cases, negative floor/remainder controls,
source-level zero-divisor failures, and an `assert false` control after both
operators. The corpus uses the normal CLI defaults without arithmetic overrides;
its successful cases must traverse complete worker units and its negative cases
must report actual B3 Failed outcomes rather than unsupported or tool-error
results. Structural controls and corpus verdict expectations are unexecuted at
introduction. They do not establish solver verdicts.

`RawAst/Values.dfy` still has placeholder value/type definitions, and the
vendored `Semantics.dfy` is commented out. Existing library contracts establish
structural/type and solver-state properties; this prose does not convert them
into a literal/source-model or complete verifier soundness theorem. Real sorts,
rational literals, arithmetic coercions and power remain outside this patch.
