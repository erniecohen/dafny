# Cardinality admission for general traits

The resolver checks the compilation unit and its loaded dependencies before
verification or code generation. General traits continue to admit value
implementations. Accepted definitions keep the existing boxing, subtype,
constructor, and runtime representations; the validator emits no new logical
assumptions or axioms.

The diagonal example in [issue 6547](https://github.com/dafny-lang/dafny/issues/6547)
stores `V -> bool` in a datatype that implements `V`. The implementation gives
an inclusion into `V`, and the recoverable function payload makes that inclusion
potentially expansive. Ordinary datatype recursion checking had no containment
edge from a trait to its value implementations. The related reference-field
contract pattern is described in
[issue 3152](https://github.com/dafny-lang/dafny/issues/3152).

## The three admission obligations

The analysis follows Dafny's strict/permissive parameter discipline described in
[KRML 280](https://leino.science/papers/krml280.html). `Preserving` and `Expanding`
are structural path flags, not inequalities calculated from actual finite
cardinalities. Joining or composing a path with expansion always leaves it
expansive. Two arrow inputs do not cancel. Empty subsets, singleton results,
and phantom parameters receive no semantic exemption.

1. A strict formal must not occur in an expansive representation position.
   Value representations include all constructor formals, even ghost storage,
   and the bases of user synonyms, subsets, and newtypes. Reference classes,
   reference traits, and iterators use their instance-field profiles only for
   formal-contract checks, including inherited fields after substitution.
   Receiver references do not acquire containment edges from their fields.
2. In each inclusion into a non-reference trait, every child formal must be
   retained as a direct parent argument after expanding identity synonyms only.
   At least one retaining slot must permit the child's advertised mode. This
   obligation applies to erasing intermediate traits and unused or phantom
   formals. A reference-only parent requires its exposed contracts to be
   respected, without requiring retention of absent child formals.
3. A separate nominal graph must have no strongly connected component containing
   an expansive internal edge. Value representation supplies containing-type to
   represented-type edges. A general-trait inclusion supplies parent-trait to
   implementing-declaration edges. Preserving-only cycles and expansive edges
   between different components are admissible to this check.

Finite collections, tuples, arrays, and arrow results preserve a path flag.
Arrow inputs, infinite-set elements, infinite-map domains, and permissive nominal
parameters introduce expansion. Nominal uses keep their declaration head and
profile their actual arguments using the advertised formal modes; callers do
not recursively unfold the representation. Only system declaration identities
receive built-in profiles.

## Identity, resolution, and entry paths

Atoms use declaration identity and positional formals. Pure visibility views
carry `CardinalityViewOf` links to their semantic definitions, preserving formal
positions and cardinality modes. Internal visibility synonyms and nullable or
non-null reference views preserve their actual argument substitutions. Distinct
refinement specializations retain separate identities even if names or source
positions coincide.

Within a selected replaceable-module environment, exact refinement-base links
connect an original interface carrier to its final selected declaration. This
selection map is local to the program and distinct from pure visibility views.
The selected representation contributes its dependencies to clients that name
the original interface. Its advertised parameter modes retain both interface
and selected contracts, with expansion taking precedence; strengthening a
selected body's contract does not silently strengthen the imported interface.
The selected body is still checked against its own declared strictness. Missing
replacement correspondence or incompatible arity fails closed. Selecting a
concrete implementation for an abstract import follows the recorded module
correspondence and selects that import's raw facade declarations before their
visibility links. Separate imports can therefore select different refinements
of the same abstract template without selecting the global template itself.

Kind-changing refinements retain inherited parent obligations separately where
ordinary inheritance storage cannot preserve them. Those obligations are
resolved in the new declaration's formal scope and are analysis metadata. They
are not logical AST children or new runtime subtype edges. Refinements cannot
relax an inherited strict parameter contract.

`ProgramResolver.Resolve` clears any old admission receipt, completes module
resolution, replacement selection, duplicate checks, and post-resolution
rewriters, then validates the resulting program. The validator runs outside the
per-module cache-miss path. Its local descriptors and graph are rebuilt for each
program; cached modules do not store a global admission decision. Cancellation,
ordinary resolution errors, or failed validation cannot leave a successful
receipt. Translation requires a successful current-program receipt and no
errors before emitting declarations.

Native `.doo` libraries preserve program text and resolve it again on loading.
Their definitions participate in admission even when library bodies are not
selected for verification. A library can be admissible until a later client
adds a closing implementation. Library emission and ordinary builds also run
resolution admission when `--no-verify` is selected. No trusted serialized
cardinality success bit or new library version is required.

## Graph and failure behavior

The graph is independent of function call, datatype grounding, and codatatype
guardedness graphs. It does not change termination inference or call recursion.
Duplicate edges keep the strongest weight and a deterministic source witness.
An iterative SCC computation identifies bad components; a stable breadth-first
return path supplies one cycle diagnostic per bad component. The primary
location prefers an inclusion clause, with related locations for expansive
representation occurrences and intervening edges.

Traversal uses explicit worklists and checks cancellation. Unknown resolved
types and malformed descriptors fail closed. The analysis does not enumerate
ground generic instances, impose a silent depth limit, mutate type inference,
or silently relax a formal's strict mode.

## Relative soundness argument and precision boundary

For a fixed parent instance, a direct retaining slot determines each child
formal. Each admitted implementation declaration therefore contributes at most
one child type instance, rather than a union over arbitrary type arguments.
The retaining slot's mode preserves the parent's interface contract.

The type profiles propagate each expansive representation occurrence to a
nominal edge or formal-contract obligation. A ground expansive carrier cycle
maps to a closed walk in the finite nominal graph, hence to an expansive edge
inside one SCC, which is rejected. Reference-field contract checks prevent a
helper class from disguising an expansive formal use as preserving.

This is a conservative extension of the existing admissibility discipline,
not a consistency proof of all Dafny or Boogie. Nominal heads can collapse
`V<int>` and `V<bool>` into an apparent cycle. A nested index such as `seq<T>`
may determine `T` mathematically but is not direct retention here. Correcting a
stored generic reference contract can reject clients that depended on its old
strict annotation. Existing variance, equality, initialization, grounding,
heap, allocation, termination, and compilation restrictions remain applicable.

The standard library applies the same field contracts to its stored callbacks.
`FunctionAction` and `TotalFunctionActionProof` use `!I` for callback inputs;
`FoldingConsumer` uses `!T` and `!R`, and `FunctionalIProducer` uses `!S`.
Stored action/producer families and their reference parents carry the required
permissive contracts, including `ProducerState` and the target-specific
`AtomicBox` and `MutableMap` implementations. Ordinary variance and existing
`(!new)` and `(==)` characteristics are preserved. A client storing one of these
families under its own strict formal may need the corresponding `!` annotation.

## Regression boundaries

Core xUnit tests distinguish new diagnostic IDs from pre-existing variance
failures. They cover built-in positions, advertised modes, identity and alias
substitution, malformed types, direct retention, duplicate slots, reference
contracts, preserving SCCs, expansive cycles, witness ordering, deep inputs, and
cancellation. An independent reachability oracle covers all directed three-node
graph topologies, while bounded seeded source transforms pair hidden forbidden
cycles with preserving analogues.

Integration tests keep resolution failures separate from positive verification,
legal non-vacuity sentinels, and backend runtime results. The exact reported
program uses the refreshed resolver because the legacy resolver rejects its cast
for an unrelated reason. Declaration-only cases exercise both inference paths
where supported. Existing recursive-cardinality rejection and unsupported
newtype bases are recorded as existing-rule regressions and do not count as
coverage of the new validator.

Language-server tests retain one session through safe, unsafe, and repaired
states. They edit downstream implementations with imported modules cached,
change an imported helper, and change provided/revealed views. Lifecycle tests
also require cancellation and invalid translation input to leave no successful
admission. Expected CLI output is recorded from executed test runs rather than
inferred from this design note.
