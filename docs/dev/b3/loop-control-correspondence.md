# P6: initial inductive control lowering

This argument concerns the checked pre-VC normalizer and the selected B3 source. It is a source-backed correspondence argument, not a machine-checked refinement proof or a claim of complete Boogie support. Normalization controls inspect actual Dafny translation and owned IR; worker verdict controls are a separate integration requirement.

## Reference schedule

The pinned Boogie conversion maps `while` to an invariant header with body and exit successors. A body guard is also inserted into the body's prefix when possible. `BreakCmd.BreakEnclosure` is the resolved enclosing `BigBlock`, including labeled breaks out of conditionals. [Structured conversion](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/AST/StructuredBoogie/BigBlocksResolutionContext.cs#L405), [break resolution](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/AST/StructuredBoogie/BigBlocksResolutionContext.cs#L220).

Standard loop removal copies checked invariants to incoming edges as initialization or preservation assertions, preserving order and attributes. The header then assumes every invariant. A free invariant is omitted on the checking edges unless `AlwaysAssumeFreeLoopInvariants` is enabled, in which case its assumption appears in its original position on both edges. Cutting a backedge adds `assume false`. Header havoc covers variables assigned in natural loops, collected before that havoc is inserted. [Standard loop removal](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCGeneration/Transformations/RemoveBackEdges.cs#L91), [natural-loop assigned variables](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCGeneration/Transformations/RemoveBackEdges.cs#L449).

The selected B3 loop never normally exits. It havocs `AssignmentTargets.Compute(body)`, then processes its body; lexical labels supply explicit exit continuations. The protocol supplies no native invariant specification, so native initialization, specification vetting and maintenance contribute no additional constraints or obligations. All normalized variables are ordinary outer-scope declarations with no native automatic invariant. [B3 loop and label processing](https://github.com/dafny-lang/b3/blob/ea6e8a18dfe9e317d313de769291f989957dc5f2/src/Verifier/Verifier.dfy#L311), [loop processing](https://github.com/dafny-lang/b3/blob/ea6e8a18dfe9e317d313de769291f989957dc5f2/src/Verifier/Verifier.dfy#L483), [assignment-target analysis](https://github.com/dafny-lang/b3/blob/ea6e8a18dfe9e317d313de769291f989957dc5f2/src/Verifier/AssignmentTargets.dfy).

## Lowering and induction

For each loop, the normalizer emits these statements:

1. Check the ordinary invariant predicates in order, with their assertion learning policy. Assume a free predicate here only under `AlwaysAssumeFreeLoopInvariants`.
2. Enter a lexical exit label surrounding a native `Loop` with an empty invariant list. Native processing jointly havocs its assignment targets.
3. Assume the where clauses of exactly the pinned Boogie natural-loop modified variables, then assume all invariant predicates in order.
4. Choose the guard-false exit or the guard-true body. A wildcard guard permits both branches without a guard assumption. The exit resumes the statements after the loop.
5. After body fallthrough, check ordinary invariant preservation in order, inserting free assumptions only under the same option. Then assume false to cut the backedge.

Initialization establishes the checked inductive premise before any header assumptions. The arbitrary header state represents every source header state permitted by the loop abstraction. Body fallthrough supplies the induction step; early exit and return bypass that step and retain their ordinary continuation or postcondition obligations. On guard-false exit the continuation has the invariant and negated guard. This is induction over arbitrary states and backedges; no iteration count or bounded unrolling is used. Initialization and preservation obligations have distinct stable roles in their identities and source descriptions.

For the projection direction used by the axiom-free model, choose all additional native havoc values equal to the corresponding original values. That includes normalization temporaries and targets on break-only paths. A successful universal check in this larger context suffices for the corresponding reference check; extra abstraction may cause false rejection.

## Header where clauses and target coverage

B3's syntactic assignment analysis may include a variable assigned only on a break path. Boogie's natural-loop set excludes such paths when they cannot return to the header. Re-assuming that variable's where clause after a different variable changes would strengthen the context incorrectly. For example, with `x where x == g`, a loop that changes `g` on its backedge and assigns `x` only before breaking must not assume `x == g` at its header.

The normalizer therefore recomputes the natural-loop set from already resolved CFG edges using owned predecessor, reachability and dominator sets. It identifies the generated header from the enclosing structured block, checks the header command/edge shape against the pinned conversion, and gathers assignment targets through pure command fields. It excludes `StateCmd` locals exactly as the source does. It never computes desugarings, invokes a VC transform, mutates predecessor fields or converts the graph to a reducible form. CFG size is limited to 256 blocks; external entries into a discovered natural-loop region fail closed.

A separate owned target analysis mirrors the selected B3 algorithm's normal/abrupt continuation calculation. Its outer `Compute` rule returns no targets when the whole body has no syntactic normal continuation. The normalizer requires native targets to cover every reference havoc target. Only the reference target set contributes where assumptions. Native extra havoc remains a weakening. The explicit cut-edge `assume false` still has a syntactic normal continuation for target analysis, while ordinary breaks and returns do not.

## Lexical exits, forward jumps and visibility

A resolved structured break becomes an exit from its active enclosing loop or conditional label. A single-target forward goto may exit a prefix of the current or enclosing statement list and resume at that list's later target block. The normalizer wraps only referenced prefixes in lexical labels; nested future targets create nested wrappers. Backward, self, multiple-target and cross-region jumps fail closed. This supports the initial Dafny break/continue translation, which uses forward `after_` and `continue_` labels; a continue resumes at the body's continuation point before preservation checks.

All routine variables, including scoped normalization temporaries, are declared outside these labels. B3's domain restriction on exit therefore preserves their current values. Early returns execute explicit postconditions before the routine exit. Unreachable checks remain in the static obligation manifest, while lexical control prevents their execution.

A bounded whole-unit inspection rejects every hide/reveal command. Under that condition, visibility scope push/pop commands only copy and restore the unchanged revealed state and can be omitted. Identity rewrites independently require `AlwaysRevealed`; other function definitions remain uninterpreted. [Pinned visibility analysis](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCGeneration/Prune/RevealedAnalysis.cs).

## Boundaries and controls

Yield invariants, k-induction, loop unrolling and concurrent Houdini remain unsupported. The normalizer also rejects graph, structured, lambda-capture and final owned-IR traversals exceeding their explicit bounds instead of returning a partial program. [Controls](../../../Source/DafnyB3Normalizer.Test/NormalizerTests.cs) include actual Dafny good, bad-initialization and bad-preservation loops; nested labeled breaks, continues and returns; free-invariant schedules under both option values; false invariants; break-only where clauses; modified-variable where order; forward exits and rejected backward/multiple jumps; source immutability; and identity visibility. These checks do not invoke Boogie VC generation or a prover.
