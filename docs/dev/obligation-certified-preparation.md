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
