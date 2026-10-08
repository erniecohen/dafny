# Declared-contract preparation argument

This is the implemented candidate correction following `58d8ac112`'s complete library
rejection. Its own structural and native evidence is required before acceptance. No gate of the preceding revision validates this candidate.
The actual implicit proposition remains mandatory and is checked once.

## Existing well-formedness proof

The method specification-WF procedure has no user `requires` assumptions on its
interface. It checks each requirement's WF before assuming that requirement,
in source order. It then checks frames, havocs permitted state and outputs,
and checks each postcondition's WF before assuming that postcondition.
Thus domain/null/bounds/type/termination facts for a clause are certified under
its typed inputs, preceding clauses and its own short-circuit guards; the
clause's truth is not supplied to its own preceding WF proof. Postcondition WF
uses the specification policy for reads, rather than the body's reads bound.
Delayed requirement reads checks may use all established requirements; those
reads checks therefore cannot be assumed before checking a caller requirement.

At a method exit, input/entry conditions and the permitted heap/output typing
hold, and earlier postconditions have already been checked in order. At a
method call, actuals have been frozen and typed, ordinary frame/termination
checks precede the contract checks, and earlier requirements have been checked.
Iterator yield contracts use their existing specification WF, exact yield old heap and inherited guard. The existing modular specification-WF proof licenses the clause's local domain
support at these points. Inherited preparation keeps its existing guard.
A filtered body-only result cannot substitute for specification-WF acceptance.

## Replay, without proving WF a second time

Use the existing source-local statement-WF traversal and fresh translator,
with its existing assertion fuel, binder materialization and can-call setup.
For declared contracts only, use the existing `AssertMode.Assume` context for
already certified non-read WF facts. Do not call `CheckWellformedAndAssume` on
the proposition: that would assume the proposition itself. After preparation,
restore ordinary checking and emit the sole actual clause check. No interface
contract proof or post-check publication policy changes.

Body reads-frame assertions are different: they are not licensed by
postcondition specification-WF, and delayed precondition reads checks can
depend on requirements not yet proved at a call. They must be neither checked
again nor assumed by this replay. Still translate those reads expressions, so
traversal/fuel state and legitimate metadata can-call support match the
existing assertion traversal. Discard only the body reads assertion at its dedicated
WF sink. A nested lambda establishes its own declared frame and keeps its
existing independent WF policy, rather than inheriting the surrounding method
reads bound. Preserve all other guards and source-local setup; inspect no surrounding
body expressions and introduce no background axiom.

## Required controls

Retain the specification's actual domain assertions while replacing their
local duplicated proofs by certified support. Keep the actual exit/call clause
assertion mandatory, including a false first postcondition whose later WF
would otherwise be vacuous. A conditional function reads set supplies an
important negative caller: its reads set is empty only when its requirement
is true. The caller with a false requirement must still fail; assuming that
reads-frame predicate would incorrectly make the caller's path infeasible.

Compare recursive nested-existential checked formulas with immediate assertions
under both resolvers, including a function reads expression containing another
existential. Reads-sink suppression must preserve translation/fuel state rather
than skipping the traversal. Keep explicit assertions unchanged. Run all paired
negatives and original/subset controls, then fresh complete suite/library gates.
The existing gate inputs, source hints, ceilings and verdict tables stay fixed.

## Guarded private argument bindings

A subsequent candidate normalizes only the freshly generated certified-WF
fragment. Pure deterministic branches can become guarded assumptions. The only
assignments moved before those assumptions bind freshly allocated function-WF
argument temporaries: they are private to this traversal, assigned exactly once,
and never change source variables or heaps. Each supporting fact retains the
conjunction of its original branch guards, expression, attributes and fuel.

The function-WF routine identifies these argument variables as it creates them;
eligibility does not infer privacy from variable names. Repeated assignments,
guards mentioning assigned temporaries, calls, real assertions, conditional
havoc, labels, scope commands and other statement forms retain the original
translation. Empty branches become the tautology `G ==> G`, using the original
guard expression on both sides and retaining any comments. This preserves the
guard terms without an empty control-flow split. Preparation still runs the
original traversal, and the actual proposition lowering is unchanged.

The justification projects away the private temporaries. On an active branch,
each unique temporary has the same value as before; on an inactive branch,
its newly assigned value is unobservable outside preparation and every fact
about it is guarded off. All non-temporary variables retain their state.
Consequently the same certified support holds at the same check point.
Moving a repeated assignment or a variable that a guard reads would invalidate
that argument, so those fragments are excluded.

The generated-Boogie Power experiment with private bindings moved and original
checks unchanged succeeds at three seeds in both axiom settings; false checks
remain invalid. Moving only assumptions while preserving conditional bindings
does not repair it. These are diagnostics, not native acceptance of the new
compiler candidate. Fresh compilation and complete native gates are required.


A focused replay of the current native Power body reproduces its failure at all
three seeds under both axiom settings. Replacing its one empty certified branch
by `G ==> G` verifies every selected seed, with both actual quantified exit checks
byte unchanged. False exit controls still fail. The tautology is true in every
Boogie state because expressions are total and the antecedent and consequent
are the identical expression; it adds no assumed domain fact or proposition.
This identifies the empty control-flow split in the replay, but fresh native
compiler evidence is still required. No whole-suite run is an iteration step
while the known regressions remain unresolved.
