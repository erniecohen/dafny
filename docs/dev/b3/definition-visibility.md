# Scoped definition contexts (issue 125)

This is the English implementation and review plan for the bounded first G3
phase. Source checkpoints are unexecuted until the focused compiler, worker and
actual Dafny controls have completed. This phase adds source-owned formulas to
the opt-in B3 backend; it does not introduce new source axioms or claim complete
prelude, fuel, heap or general quantified-definition support.

## Pinned availability rules

The reference is Boogie `73a0e214a87df85fc058270268c1d0706fd05bc9`, shipped as
`Boogie.ExecutionEngine 3.5.5-review.37e4435d`. `RevealedAnalysis.cs`,
`DataflowAnalysis.cs`, `Pruner.cs` and `FunctionVisitor.cs` under
`Source/VCGeneration/Prune` determine which source definitions reach the prover.
The analysis starts with all functions revealed. Named hide/reveal updates an
offset set; wildcard commands replace the mode and clear that set. Push copies
the top state; pop restores its predecessor. AlwaysRevealed functions remain
revealed. Ordinary assumptions and learned assertions have separate lifetimes.

The pinned merge has a particular mixed-mode rule: it returns the Reveal-mode
operand unchanged. Its fixed-point equality compares only the top frame. The
owned metadata pass preserves those rules, including their limitations. It
requires equal full outer stacks at joins and rejects changing outer frames
behind a native-equal top frame. It rejects visibility commands on CFG cycles.
Loops whose visibility remains unchanged retain the existing induction lowering.
Changed backedges need separate initiation/preservation origins before support.

`Pruner.GetRevealedState` merges assertion states across a native split. The
exact owned port remains separate from a conservative sufficient guard. A raw
join can be in Reveal mode even though an incoming path hides the owner. Native
`Splits/Split.cs` can remove successors (`CloneBlock`) and split paths (`SplitAt`);
therefore a raw per-assert mask alone cannot establish availability in every
native split. This phase makes no exact split-parity claim.

For at most 64 catalogue owners, a second owned analysis intersects revealed
owners at every join. Each bit means the owner is revealed on every original
path reaching that lexical frame. Push/pop preserves full matching stacks;
wildcards replace the state and AlwaysRevealed is honored. The analysis also
records whether all paths have Reveal mode and whether any path has Reveal mode.
Changing cycles remain unsupported. Any path subset uses only original paths,
so a retained owner bit remains sufficient on that subset.

A selected source formula requires both the exact native mask and this must-owner
bit. It additionally requires either all target paths to have Reveal mode, or
every potential native assertion operand with a possible Reveal mode to retain
the owner bit. The operand inventory includes every original raw and nested
AssertCmd plus generated call, return and invariant roles, even checks with no
function demand. This guards the pinned mixed-mode aggregate: a target that can
become Hide mode after path splitting cannot rely on a different Reveal operand
that hides its owner. Missing source origins or unequal full outer stacks reject
normalization. Adding extra operands can only omit definitions.

Source functions remain declared even when a definition edge is hidden. Only
Function-to-CanHide-Axiom edges are cut by availability. A direct edge from a
demanded function to its active owned definition is sufficient for this phase;
general trigger-dependent reachability remains outside its scope.

## Owned CFG and source origins

The typed implementation already has raw Blocks produced by
`BigBlocksResolutionContext`; ordinary commands and invariant assertions retain
their source object identities. B3 builds new metadata nodes and adjacency lists
around those objects. It never clears source Block.Predecessors, rewrites goto
lists, marks expression polarity, desugars calls, passifies, prunes the original
program, constructs Boogie verification tasks, or calls a Boogie prover.

Resolve and Typecheck inspect Blocks rather than StructuredStmts
(`Implementation.cs:586-589,640-643`). The structured constructor builds Blocks
once (`Implementation.cs:400-403`). Typed input alone therefore does not establish
that those two mutable representations still correspond.

The bounded validator uses the exact producer at Boogie
`73a0e214a87df85fc058270268c1d0706fd05bc9`,
`Source/Core/AST/StructuredBoogie/BigBlocksResolutionContext.cs`:

- `CreateBlocks:351-375` preserves the complete source command sequence by object
  identity, including PrefixCommands applied once, and reuses explicit transfers.
- `AssignSuccessors:316-341` sets lexical continuation/backedge successors.
  `CreateBlocks:382-403,594-605` derives runoff and break transfers from them.
  `CheckLegalLabels:228-245` selects the nearest loop or named lexical enclosure.
- `CreateBlocks:413-450` creates only exact Guard and Not(Guard) assumptions with
  one empty partition attribute, including the pinned `Expr.Not` simplifications
  (`Expression/AbsyExpr.cs:275-333`) and the typed Bool rewrites
  (`Expression/AbsyExpr.cs:2483-2502`), and reuses every yield/invariant object at the
  loop header. `453-477` supplies the loop body, backedge and exit topology.
- `CreateBlocks:494-525,527-584` supplies branch guards and exact successor order;
  conditional jump attributes are the original IfCmd attributes. Else-if guards
  remain attached to the preceding condition, not the next one.
- `StmtList.PrefixFirstBlock:100-129` puts a nonempty guard prefix inside an
  anonymous first block, otherwise creates a separate guard block; an empty
  prefix needs no extra block. The validator checks this placement.

The validator reconstructs only owned expected descriptions in deterministic
producer block order. It binds each generated label to the corresponding actual block, without
constructing native nodes, guessing label spellings or trusting mutable successor
metadata. Require exact command object sequences, exact generated guard shapes,
explicit transfer identities, matching goto names and resolved target objects,
all lexical loop/break/return edges, and every actual block consumed exactly once.
Bound source depth, command/edge totals and block count before expansion. Unknown
shapes, reordered/replaced/inserted commands, redirected or missing edges, and
ambiguous source objects fail `b3_cfg_correspondence`. Source statements shared
with their raw commands remain observed exactly once; mutating a shared command
changes both interpretations and does not bypass these checks.

For typed Boolean comparisons, producer negation occurs before Typecheck. The
shared Eq guard becomes Iff(a,b); its separately created Neq complement becomes
Iff(a,Not(b)). A source Neq guard becomes Iff(a,Not(b)) while its Eq complement
becomes Iff(a,b). The matcher accepts only those two complementary Iff forms
with the same first operand object and one direct Not wrapping the exact other
second operand object. Boolean semantics makes them complements under every
source interpretation. It adds no premise and performs no expression rewriting.
Changed captures, same polarity, swapped operands and other unmatched forms reject.

This establishes the representation premise for the separately reviewed lowering:
the raw CFG has precisely the source ordinary commands, generated guards and
control graph of the structured artifact being normalized. Every raw assertion,
including StateCmd descendants, must additionally have an original normalized
assert or invariant anchor; unmatched goals cannot be discarded or treated only
as availability data. The resulting visibility origin mappings refer to that
same checked graph. This is a source correspondence argument and implementation
plan, not executable or formal acceptance evidence.

Each ordinary assertion maps to its exact command. A checked call requirement
uses the original CallCmd entry state, since its reviewed desugaring introduces
no visibility changes. Explicit returns and checked postconditions use the exact return object.
Runoff postconditions aggregate only the generated CFG return sites that are
not source explicit returns; this is the native merge at those fallthroughs.
When no runoff return exists, the extra static appended exit is unreachable and
receives no definition premise. Ambiguous origins are unsupported. Invariant initiation
and preservation share a visibility mask only when the source cycle contains no
visibility changes. StateCmd assertions can inherit the enclosing command mask
only when the state introduces no hide/reveal or scope commands. Missing,
shared or unbalanced reachable visibility-sensitive origins fail closed. Known
entry-unreachable origins retain their normalized checks and receive no definition
premises; they do not seed an independent visibility path.

The graph is bounded before analysis. The worklist is bounded and its native
fixed-point behavior is retained. No source computed property that performs a
VC transformation is invoked.

## First eligible source theory

The useful first target is a pure, nonrecursive, heap-free, nongeneric literal
Bool/Int function with no Dafny value formals. An opaque declaration can add a
Bool reveal argument to its Boogie signature. Eligibility is checked against
the emitted typed source formula and its exact Function object, not a function
name, claimed builtin attribute, or a Dafny Body comparison alone.

Dafny `BoogieGenerator.Functions.GetFunctionAxiom` creates a CanHide definition
whose full formula is a canCall guard implying body canCall assumptions, side
effects and the defining equality. For a closed literal body, the formula can
be ground. The opaque defining application uses the literal reveal argument
`true`; ordinary calls keep their emitted reveal expression. The full source
guard and equality must remain intact. The bridge never replaces this with an
unconditional function-equals-literal equation and never assumes reveal or
canCall guards merely because a definition is loaded.

For a Bool-valued literal owner, Typecheck rewrites the defining equality into
Iff. This is admitted only when both equality operands have normalized Bool type;
the same complete source function application, active ownership, literal value
and full outer formula/guards are still required. Typed Bool equality is logical
equivalence, so recognizing that leaf does not introduce a different equation or
remove a guard. Int equalities continue to use Eq. Arbitrary Iff formulas cannot
qualify without the exact owned defining-call and literal recognition.

The exact Axiom object must occur in Program.TopLevelDeclarations and in that
Function's definition ownership collection. The owner must also be an active
source declaration. The defining call must refer to that same object with its
complete closed signature. The initial formula is ground. A universal formula
with only a small number of Bool binders may supply the finite false/true
instances of its entire guarded body. Those instances are consequences of the
actual source universal formula; no Int, fuel, heap or polymorphic instantiation
is guessed. Guards, captures and nonliteral bodies outside this recognition are
omitted or rejected at visibility-sensitive demand sites, never relabeled as
supported definitions.

The catalogue records normalized typed formula-instance hashes, source axiom
ordinals and source token line/column positions without source paths. Adding
a formula for a check additionally requires a revealed owner and a direct
source demand root at that check. Source-owned associations plus that root
justify the native Function-to-Axiom path. Other declarations and dependencies
remain conservative uninterpreted symbols.

## Stable facts and separate solver contexts

Normalize ordinary expressions and state once. Every function has the same
uninterpreted symbol in every context, including when hidden. Reveal does not
expand calls and hide does not rename them. Thus a fact learned about F() while
its definition was visible remains the same fact after hide F, with the same
state incarnation and function symbol.

Partition the complete original static check manifest by the immutable set of
selected source formula instances. A context has the same functions, variables,
assignments, control, assumptions and check conditions. It loads only its
selected source formulas as B3 axioms with empty Explains lists. B3's lazy
Explains-driven loading is not used to simulate Boogie's visibility.

A check selected in that context retains its original Learn flag. An unselected
check with Learn=true becomes Assume of the exact original condition; an
unselected nonlearning check contributes no assumption. The original schedule
is unchanged. Each original check belongs to exactly one context. The overall
unit succeeds only when every context completes successfully, so assumptions
from an unselected checked fact cannot turn a failing original obligation into
success. Reachable false controls remain required; unreachable checks may
legitimately have zero attempts and must not be fabricated into coverage.

Each context uses a fresh worker and solver session. No solver declaration cache,
axiomMap, RContext memo or learned fact is shared between contexts. The existing
schema 2 ProgramHash binds the complete formula/check bytes and the ordinary
worker completion identity checks remain mandatory. The backend validates the
exact original partition before dispatch, aggregates one original unit result,
and reports failure or incompleteness from any context. Resource totals remain
unavailable. One deadline covers the original unit, startup, IO and all contexts;
it is not reset for each mask. Mask count, total variant nodes and bytes are
bounded before materialization.

## Model argument and acceptance boundary

Fix a source interpretation. Existing normalized uninterpreted symbols keep
their source interpretation at each demanded closed type. Each selected formula
is the full active source formula, or a finite instance of its universal Bool
formula, made available through a reviewed source demand and revealed owner.
Its addition therefore preserves every corresponding source countermodel.
Omitting other source axioms enlarges the normalized model class. Ordinary facts
and state denote the same expressions across all contexts. Proving every
original check in its weaker context is consequently sufficient for the
corresponding source checks, within the separately reviewed command-lowering
boundary. This is an English correspondence argument, not a formal theorem for
the whole backend.

Acceptance requires actual Dafny literal definitions, canCall and opaque reveal
guards; visible, hidden and revealed cases; named and wildcard operations;
nested scopes and branch joins; unchanged-visibility loops; explicit unsupported
changed backedges and scope tails; precise call and return provenance; and exact
static partition receipts. A strong control must reveal, prove a learned fact,
hide, then reuse the same F expression. Its reachable-false counterpart and a
nonlearning counterpart must fail. A satisfiable assert-false case with a source
definition actually loaded must fail. Interleaved and concurrent contexts must
show independent permissive/restrictive solver sessions. Unknown is inconclusive
and cannot satisfy a negative control. Prior source hashes or bootstrap receipts
do not establish acceptance for an edited tree.

The remaining opaque function and constant routes additionally require exact
active top-level declaration membership. Native/identity/Body substitutions keep
the pinned object semantics. This protects the public typed-IR API from detached
same-name objects aliasing active opaque symbols; producer name uniqueness alone
is not used as an API correctness premise.

## Executable acceptance controls

`Source/DafnyB3Normalizer.Test/B3DefinitionContextTests.cs` checks active source
ownership, intact guards, finite Bool instances, declaration object identity,
original learning flags, mixed-mode availability and actual Dafny translation.
`B3CfgCorrespondenceTests.cs` adds 28 producer-shape/mutation controls for ordinary
commands, generated guards, branch/loop/break/return topology and complete block
consumption. Raw-only false-assertion controls also cover nested StateCmd goals.
The large-loop control now rejects at the earlier correspondence bound.
`B3VisibilityTests.cs` checks source immutability, unchanged-visibility loops and
explicit rejection of changing cycles or unequal outer scope stacks.
`B3ContextCoordinatorTests.cs` checks exact aggregate outcomes, one shared
original-unit deadline, cancellation cleanup and disjoint identities.

The strict packaged-worker gate is `Source/DafnyB3Visibility.TestRunner`. It
contains 27 typed Boogie/actual Dafny cases plus one five-launch session-isolation
control. The typed Boogie fixtures explicitly attach their active source axioms
to the exact function ownership metadata used by native pruning. Actual Dafny
fixtures use only the producer's own metadata. Cases that require a definition
also require a nonzero loaded source-origin manifest. The opaque Bool-return
reveal/learn/hide pair includes a reachable self-inequality failure while its
Bool definition is loaded. Hidden and mixed-mode
cases require zero loaded definitions. Linear typed Boogie cases require exact
attempt outcomes and coverage. Four branch/path-subset cases require strict
outcomes, the expected definition count and coverage of their reachable checks
without assuming one attempt per check. Four additional typed Boolean equality/
inequality branch controls require Verified positives, Failed reachable-false
negatives and fixture-specific reachable coverage; actual Dafny cases allow legitimately unreachable
generated checks while requiring completed traversal, strict outcomes and a
failed attempt for each expected failure. Unknown cannot pass.

After the verified worker package exists, invoke:

```sh
dotnet run --project Source/DafnyB3Visibility.TestRunner -c Release -- \
  --worker build/b3-host-tests/package/DafnyB3Host.dll \
  --solver "$Z3_PATH" --solver-sha256 "$Z3_SHA256"
```

The solver digest is computed from the exact supplied file. The host independently
checks that file's digest and Z3 5.1.0 version. The runner records observed compiler,
normalizer assembly and worker identities; `--compiler-version` can require an
exact expected informational version. The complete `make test-b3` target includes
this gate after host/package preparation. The isolated and concurrent launches
use new WorkerProcessClient instances and new processes. No solver flags are
changed. The configured resource limit is forwarded to each fresh context;
aggregate proof-resource totals remain unavailable. Replay work increases the
number of solver sessions but cannot extend the original unit's wall deadline.

These controls and this implementation are source checkpoints until executed on
that exact edited source and pinned package. Prior Real, map and bootstrap gates
do not establish G3 acceptance.


## Typed producer correspondence repairs

The Bool literal producer calls the prelude's generic `Lit<T>` at the resolved
Bool instance, whereas Int literals use monomorphic `LitInt`. The prelude's
universally quantified active identity definition and `AlwaysRevealed` status
justify the existing identity projection for every instance. The bounded ground
formula recognizer may therefore pass through that same audited projection when
the actual call and projected argument both have the same resolved Bool or Int
type. It validates the projected argument as ground and retains the entire owned
source formula, including its canCall/reveal guards. It accepts no claimed
identity attribute or function-name convention, detached defining axiom, generic
nonidentity function, or additional source equation. The Bool literal predicate
uses the existing projection independently. This expands producer recognition;
it asserts only the same complete active owned definition instances already
covered by the source-model argument above. [Prelude identities](../../../Source/DafnyCore/DafnyPrelude.bpl),
[prelude availability](../../../Source/DafnyCore/Verifier/BoogieGenerator.cs#L106),
[literal translation](../../../Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L862).

The visibility graph still owns and validates every original CFG block, command,
transfer, successor and normalized check anchor. Every bounded normalized unit
constructs this owned metadata graph, including units without hide/reveal or scope
commands. Metadata execution starts only
at the first raw block, traversing all structural successors without a
literal-false or guard-feasibility shortcut. Thus its paths are a conservative
superset of feasible execution paths. A disconnected trailing block cannot seed
an independent path with a fresh root scope. Unknown origins remain unsupported;
known disconnected origins have no state and no eligible definition premises.
All normalized checks, including those in disconnected source continuations,
remain in their original static partition. Runoff masks aggregate only
entry-reachable implicit returns. Reachable pops, complete outer-frame joins,
changing visibility cycles and ambiguous nested origins retain their strict
checks.

This entry boundary matches an unconditional native preparation step, rather
than relying on `RemoveEmptyBlocks`: pinned
`VerificationConditionGenerator.PrepareImplementation` calls
`RemoveBackEdges.ConvertCfg2Dag` before passification, and that method first calls
`Implementation.PruneUnreachableBlocks`. The latter starts at `Blocks[0]`.
Its optional literal-false edge pruning can remove still more paths; retaining
those paths in B3 metadata only weakens the must-availability sufficient guard.
Dafny emits unwinding pops before return/break transfers and also emits lexical
trailing pops after translating a statement list. The structured producer can
therefore retain disconnected trailing-pop blocks before native preparation.
These blocks require exact representation correspondence and static coverage,
not an invented initial visibility scope. [Native preparation](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCGeneration/VerificationConditionGenerator.cs#L427),
[unconditional native reachability](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCGeneration/Transformations/RemoveBackEdges.cs#L19),
[entry-rooted traversal](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/AST/Implementation.cs#L1020),
[Dafny scope schedule](../../../Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs#L860).

Two controls also require precise source-premise classification. Boogie's
Boolean inline attribute is `{:inline}` or `{:inline true}`; `{:inline 1}` does
not populate `Function.Body`. The detached-body control explicitly asserts that
the native body exists before cloning its function object. Mutable globals are
forbidden in the stateless resolution context of an axiom, so a source axiom
`F() == g` with mutable global `g` is a resolution-rejection control, not evidence
about normalization of a typed axiom. A valid immutable constant capture remains
a separate nonliteral-definition rejection. [Attribute rule](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/AST/QKeyValueExtensions.cs#L8),
[body construction](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/BoogiePL.atg#L529),
[stateless global rejection](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/AST/Expression/AbsyExpr.cs#L1313).

These are source correspondence arguments. Edited structural and worker controls
require execution on the exact compiler/library/solver pins before acceptance;
a static review is not a runtime result.


The next focused source fixtures exercise a module-imported opaque definition
through actual Dafny translation. Both reveal the imported function, check its
literal result, hide it, and use the already checked result under the original
learning rule. One then checks `false`, which must still fail with the imported
definition present. They use the ordinary default export and import declarations
and add no synthetic background equation. This complements the existing local
opaque controls and the five fresh-worker isolation launches, including two
concurrent launches with permissive and restrictive masks. The imported controls
remain pending until their exact worker receipts are inspected.


## Schema-3 native word child traversal

Both definition-demand discovery and context node accounting traverse every
`Ir.BitvectorOperation.Arguments` child. A normalized native `IntToBitvector`
whose integer argument calls an eligible guarded literal owner now contributes
that same owner demand to `SelectDefinitions`. This does not recognize a new
definition shape: the exact active source-owned formula, whole guards, pinned
visibility analysis and must-availability sufficient guard remain required.
Every loaded formula was already an eligible source premise for that original
check. In a fixed source model, its truth therefore justifies selecting it even
when the demanded call sits under a native word operation. Stable UF symbols,
ordinary context, source check identities and learning order remain unchanged.
This strengthens the previously incomplete target context only by those already
reviewed complete guarded source formulas; it introduces no wrapper equation or
new semantic recognition rule.

Context traversal also counts nested word expressions in the original body and
selected formulas. The existing per-program node/depth bound and aggregate
replay estimate therefore cover those actual expression children. This tightens
admission; it changes no retained expression or source premise. The 16-context,
200,000 aggregate-node, 64 MiB aggregate-byte and original-unit deadline limits
remain unchanged. `Replay` and `ValidatePartition` retain their exact contracts.
Unsigned F/native N/standalone A wrapper recognition is outside this repair.

`B3WordContextTraversalTests` adds two typed-Boogie structural controls and three
aggregate-bound cases (five focused cases). The guarded demand under native
int-to-word conversion loads its complete source implication only in the visible
context. Its hidden replay retains the prior learned word equality, stable owner
application and the original false goal. A missing-guard control checks that
selection does not manufacture that guard or erase the false goal. These are
structural source and replay checks, not worker-verdict evidence.

The boundary inputs use balanced bv1 expression trees and eight distinct masks.
A body contains one Block, one Assume of a word comparison and eight Check(true)
goals. If the tree has n nodes, the body has n+20 nodes, the eight original
identities add eight, and three one-node true formulas add six including their
axiom nodes. Thus the conservative admission estimate is `8 * (n + 34)`.
The ordinary premise says the word is at most 1 and is satisfiable for every bv1
value. Tree sizes 24,965, 24,966 and 24,967 give estimates 199,992, 200,000 and
200,008. The first two must be admitted, the last rejected specifically by the
aggregate-node check. Each input separately satisfies protocol node/depth
validation, and its message bytes and aggregate byte estimate have more than
half their limits free. This distinguishes node accounting from a byte-limit
rejection; the admission estimate is not a measured runtime node or resource
count. These edited controls are source-only until executed on their exact
compiler/library/solver pins.
