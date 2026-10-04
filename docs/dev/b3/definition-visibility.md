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

`Pruner.GetRevealedState` merges assertion states across a native split. A B3
context may use a stricter per-check state and a subset of eligible definitions;
this is conservative incompleteness, not an exact split-parity claim. Source
functions remain declared even when a definition edge is hidden. Only
Function-to-CanHide-Axiom edges are cut by that availability decision. A direct
edge from a demanded function to its owned active definition is sufficient for
this first phase. General trigger-dependent reachability is outside the phase.

## Owned CFG and source origins

The typed implementation already has raw Blocks produced by
`BigBlocksResolutionContext`; ordinary commands and invariant assertions retain
their source object identities. B3 builds new metadata nodes and adjacency lists
around those objects. It never clears source Block.Predecessors, rewrites goto
lists, marks expression polarity, desugars calls, passifies, prunes the original
program, constructs Boogie verification tasks, or calls a Boogie prover.

Each ordinary assertion maps to its exact command. A checked call requirement
uses the original CallCmd entry state, since its reviewed desugaring introduces
no visibility changes. Generated returns and checked postconditions require an
exact return-site origin; ambiguous origins are unsupported. Invariant initiation
and preservation share a visibility mask only when the source cycle contains no
visibility changes. StateCmd assertions can inherit the enclosing command mask
only when the state introduces no hide/reveal or scope commands. Missing,
shared, unreachable or unbalanced visibility-sensitive origins fail closed.

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

The catalogue records exact typed formula hashes and source identities. Adding
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
