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
receipt. Translation requires a successful current-program receipt and no parser,
resolver, or other admission errors before emitting declarations. Later
translator, verifier, or compiler diagnostics do not revoke the receipt;
a new resolution error still blocks translation.

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

## Exported standard-library cardinality contracts

The standard library satisfies the same representation, retention, and cycle
obligations as client declarations. Its migration adds 82 permissive `!` markers
across 55 declaration headers: 53 in library source and two in set-reader
examples. These are exported cardinality-contract changes. Affected parameter
positions no longer advertise cardinality preservation. A client that stores
one of these families under its own strict formal can therefore be rejected;
it must adjust its representation or expose the required permissive contract.
That contract can also propagate to the client's parents and stored families.

The changes preserve ordinary invariant variance, equality support `(==)`, and
non-heap characteristics `(!new)`. Specifications other than the exported type
cardinality contracts, proof bodies, and executable bodies are unchanged.
The less-obvious annotations follow these dependencies:

| Affected contracts | Dependency requiring the permissive mode |
| --- | --- |
| `FunctionAction<!I, !O>` and `TotalFunctionActionProof<!I, !O>` | Stored callback inputs require `!I`. The `!O` contracts follow their `Action` and `TotalActionProof` parents and the stored `FunctionAction` family; callback results themselves preserve cardinality. |
| `FoldingConsumer<!T, !R>` and `FunctionalIProducer<!S, !T>` | Stored callbacks have inputs `(R, T)` and `S`, respectively. `FunctionalIProducer` needs `!T` because its `IProducer<T>` and `TotalActionProof<(), T>` parents expose permissive slots. |
| `GenericAction`, `Action`, `TotalActionProof`, and the consumer/producer interfaces | The permissive contracts required by implementations propagate to their exposed parent slots. In particular, `FilteredProducer` requires permissive `Producer.T`, which passes through `Option<T>` to the output slots of `Action` and `TotalActionProof`. `Action` passes both modes to `GenericAction`. |
| State datatypes, composed/mapped/flattened producers, and total-proof helpers | Stored families carry their advertised modes. `ConsumerState` stores `Consumer<T>`, `ProducerState` stores `Producer<T>`, `ComposedAction` stores both `Action<I, M>` and `Action<M, O>`, and `FlattenedProducer` stores `Option<Producer<T>>`. Ghost storage has the same obligation. |
| Consumer/producer subclasses and batch adapters | Parent applications propagate permissive modes even when a subclass's own fields preserve cardinality. A permissive outer slot in `Producer<Batched<T, E>>`, `IConsumer<Batched<T, E>>`, or `TotalActionProof<Batched<T, E>, ...>` reaches both `T` and `E`; `Batched` itself stays strict. |
| `AtomicBox<!T>` and `MutableMap<!K(==), !V(==)>` | Stored ghost invariant callbacks take `T` or `(K, V)` as inputs. The replaceable interface, target implementations, and JavaScript helper classes advertise matching contracts. |
| `SetIReader<!T(==)>`, `SetReader<!T(==)>`, and `ProducerOfSetProof<!T>` | Producer parents require permissive `T`. `SetReader` also directly retains `T` in its non-reference `ProducerOfSetProof<T>` parent, so that parent's mode must be compatible. The stored sets preserve cardinality. |

Thus `EmptyProducer`, `RepeatProducer`, `SeqReader`, `ArrayWriter`, and
`SeqWriter` carry permissive contracts through their parents even without a
stored callback of their own. `Option`, `Result`, `Batched`, and `BatchedByte`
remain strict, as do the method-only `ActionCompositionProof` and
`ProducesSetProof` helpers. A permissive annotation satisfies a parameter
contract; it does not make an expansive nominal cycle admissible.

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

Validation distinguishes source admission, proof verification, archive builds,
and packaged-library loading. Resolution of migrated source establishes only
admission. Before shipping, all seven embedded libraries must be freshly built
with verification using the branch's `DefaultZ3Version`, normal library settings,
and existing budgets, then included in the executable tested with
`--standard-libraries` and target-specific loading. Results using Z3 5.1 are a
separate comparison; failures in that configuration alone do not establish that
proof changes are needed under the branch's default solver. Any reported Boogie
output or resource-count equivalence applies only to the controls actually
compared, not to every accepted program or the whole library.
