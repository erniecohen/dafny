# Legacy conversions to datatype subsets

`--datatype-subset-conversions` is disabled by default. With the legacy resolver,
it admits an explicit `as` conversion only when the destination is a visible
subset type whose underlying type is an inductive datatype. The source must
satisfy the same type compatibility relation used by assignment. This includes
instantiated type arguments; it does not introduce implicit container coercions.
The refreshed resolver already accepts these conversions and does not consult
this option.

## Verification argument

The change adds a guarded resolver branch. It changes no verifier encoding and
adds no axioms or unchecked assumptions. Type normalization continues to respect
export visibility. An opaque destination does not acquire a visible datatype
representation through this option.

Resolving an expression to its destination type is not a proof that its value
belongs there. The existing conversion well-formedness path first checks target
datatype membership through `CheckSubrange`, before assuming a target-typed
result. `CheckResultToBeInType` also checks the destination's redirecting-type
constraints, recursively instantiating the subset bases and their type
arguments. Thus an accepted cast must establish both the underlying generic
membership and every destination subset predicate. The cast preserves the
runtime representation already used for assignments to these subsets.

## Regression boundary

`git-issues/git-issue-961.dfy` runs the exact original and independent fixtures
with the option off and on under each resolver. The valid cases exercise direct,
nested and aliased constraints, generic bases, a generic subset declaration, and
reordered generic arguments. Invalid predicates and a negative integer hidden
inside `Box<nat>` must fail at the cast. A valid inhabited cast followed by
`assert false` must fail at that assertion.

Resolution controls retain rejection of incompatible datatypes, opaque legacy
destinations, plain datatype destinations and codatatype subsets. Compiled C#
checks preserve the values of direct, nested and generic casts. The project-file
fixture checks the option's ordinary project setting and command-line override.

The default-off acceptance requirement is paired suite and complete source-library
verdict and resource equality. Diagnostic job success alone does not establish
that requirement or the supported-line integration gate.
