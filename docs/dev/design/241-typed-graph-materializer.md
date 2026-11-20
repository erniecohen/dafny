# Typed module graph materializer: executable prototype

This is the first literal-body materialization milestone for
[issue 241](https://github.com/erniecohen/dafny/issues/241). It is deliberately
confined to `DafnyCore.Test`; selecting `TypedGraphMaterializerTests` opts into
it. There is no product option or cache hit path. The compiler, library,
verification translation and ordinary CLI remain unchanged.

## What remains on the review line

The merged work establishes module-local resolution outputs and keys, worker
resolution, profiling and translation reuse. It does not provide detached
literal-body reuse. [PR 282](https://github.com/erniecohen/dafny/pull/282) proposes
a persistent envelope; draft [PR 519](https://github.com/erniecohen/dafny/pull/519)
implements a restricted alias profile and records the literal-body blocker.
This prototype builds directly on the review line and depends on neither draft.

`ExistingResolvedClonerDoesNotDetachTheWitness` reproduces that blocker on the
current base. A cloned call still points to the producer's function, expression
type, pretype and type-argument list. The module clone drops visibility, call
graphs and successful-resolution state, and the recursive function loses its
recursion flag. Re-resolving the clone would not demonstrate materialization.

## The boundary and representation

An observing resolver captures each non-default literal module at
`ResolveModuleOutput`, after local resolution and before global admission. Its
roots include the declaration, signatures, class-member tables and translation
inputs. Capture freezes a graph table of runtime type metadata, field names,
immutable scalar values, object indices and external selectors. No producer
AST, scope, collection, callback or inference object is retained in that table.

Materialization allocates fresh identities before restoring edges, including
cycles and readonly fields. Boxed value types are initialized before being
copied into their owners; hash containers are populated after key fields.
Supported collection comparers are explicit. Visibility scopes receive fresh
logical tokens while preserving token incidence and shared mutable scopes.
Scope debug names, locks and memoized visibility queries are rebuilt rather
than copied. Equal debug names never identify tokens.

External references bind to the independently resolved compilation's options,
parent, prerequisite outputs and canonical builtin catalogue. The full catalogue
matters: resolved pretypes can reach intrinsic declarations absent from a
worker's pruned translation view. External objects must retain exact current
reference identity; a same-named reconstructed builtin is not interchangeable.
Selectors are deterministic rooted field/index paths for this ABI, not the
persistent selector contract proposed in PR 282.

## Independent oracle and controls

A second, normal resolution supplies the expected graph. The equality checker
walks the two live graphs without serializing either one, checking runtime types,
scalars, fields, collections and comparers. A two-way identity map detects split
and merged identities as well as cycles. External edges require reference
equality. Scope comparison checks the multiset of logical token incidences over
all paired scopes, including external token bindings.

The only field exclusions are `ModuleDecl.CloneId` (parse-cache lineage) and
`TypeProxy.id` (the diagnostic-only counter used by `TypeProxy.TypeName`). Proxy
identity, targets and constraints remain checked. Scope runtime bookkeeping is
handled by the logical-scope comparison above. These are structural comparisons
at the pre-global boundary: later whole-program termination metadata is outside
the captured result.

Positive checks directly inspect local call targets, canonical array builtins,
visibility, call graphs, recursion flags, successful resolution, disjointness from
producer-owned objects, and scope sharing. Negative controls alter call targets,
types, builtin identities, visibility, accessible members, call graphs and
recursion flags; split and merged identities must also fail. Malformed entry
kinds, field schemas, references, scalar values, scope tokens, collection
comparers and dictionary arity are rejected. No trusted assumptions or axioms
are introduced.

The fixtures cover exports/imports, abstract imports, nesting, constants,
generics, datatype updates, tuple updates, arrays and mutual recursion, plus
selected existing lit programs. This is a bounded executable oracle, not a proof
that every possible resolved Dafny graph is supported. Unknown owned runtime
types and comparers fail closed in this test harness.

## Remaining milestones

1. Specify and implement a closed, versioned persistent schema with stable
   declaration/signature/builtin selectors, source rebinding, bounded validation
   and explicit supported-node admission. Reflection and runtime type metadata
   here are unsuitable as an untrusted disk format.
2. Materialize literal bodies into the real resolver path, with mixed hit/miss
   prerequisites, cancellation and current whole-program admission. Demonstrate
   equivalence through lowering and verification, including negative controls
   and plugin/option/source invalidation.
3. Establish suite and standard-library coverage and unchanged option-off output,
   verification outcomes and resource counts. Only then measure whether reuse
   saves work. This milestone makes no speedup claim.
