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

## Leading support order

The [native support-order diagnosis](obligation-support-order-diagnosis.md)
records a general local correction under validation. Create the existing support
expression at its original point; move its command after normalized certified
preparation only when it is independent of every private assignment and havoc
target. The same expression/command and every preparation fact remain before the
mandatory P check. Unsupported fragments retain the original order. This is
state-preserving commutation of an assume with independent pure setup, not a
new fact, proof, fuel policy or body-term collection.


## Caller proof scopes and original call publication

The current candidate retains each ordinary method call's complete preparation,
sole checked pieces and ordinary split summaries in one existing `PathAsideBlock`.
The branch ends after all required checks. Earlier requirements remain available
while proving later requirements; every check keeps its own complete preparation,
original fuel traversal, current substitutions, pre-call heap and visibility.
This changes the continuation boundary, not the strength of a check.

The ordinary call targets a separately named, nonchecking Call/CoCall interface
whose user requirements have the existing `always_assume` attribute. Conditions
are generated by the original method-spec splitter and translators, and Boogie
performs its original formal/actual and heap substitutions. This publishes the
original kind of call facts, rather than copying internal assertion pieces that
may differ under assertion induction. No second precondition proof is generated.
Frames, termination, old allocation and output handling retain their previous
locations and representations.

The original nonchecking interface remains the target of inherited/filtered
free calls. Those calls skip the local proof and do not acquire the ordinary
user requirement's new publication attribute. This preserves the original
Boogie behavior on these paths; indiscriminately marking every interface would
not. Default-off generation creates no additional interface or proof scope.

Only certified, normalized preparation can use the local proof scope. Its
commands may be comments, assumptions or bindings of recorded fresh private
argument variables. Havocs, source writes, calls and visibility commands reject
scoping. A statement expression in the checked proposition also rejects scoping,
so its previously outer reveals and other effects remain in the continuation.
For rejected preparation, append the original commands in their original order.
Only the proposition being checked and its freshly generated preparation are
inspected; no surrounding body or context terms are consulted.

The semantic argument separates two paths of the nondeterministic branch. One
path proves every mandatory precondition with all its original support and then
terminates; the other reaches the original call. Complete verification of the
branch checks establishes the preconditions before their normal call publication.
No later contract assumption licenses an earlier branch assertion. The diagnostic
with a direct `requires false` source call remains Invalid. Private preparation
bindings affect no source state, and rejected effects stay in their old outer
scope. This argument still requires the candidate's own structural and native
validation; preceding scratch results are not product acceptance.


The current direct native Boogie gate completes all eleven registered contract
fixtures against the candidate-matched pinned Boogie packages and Z3 5.1.0.
All expected summaries pass at the unchanged limits: normal publication is
available, a false local proof branch remains rejected even when the following
call publishes a false contract, the original inherited/free interface does not
publish ordinary requirements, and a modifying call invalidates the relevant
pre-call fact. The three new negative controls identify their failing assertions.
This establishes the required Boogie mechanism; native Dafny regression acceptance
is separate and remains pending.
