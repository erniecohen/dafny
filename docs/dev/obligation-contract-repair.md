# Contract checking consolidation experiment

This candidate retains legacy return-position reveal visibility and every original
postcondition formula, inherited guard, trigger and fuel term. It checks the
original implementation postconditions locally at every explicit return and
fallthrough, together with the assertion-style pieces, before publishing each
clause summary. Only the implementation procedure's corresponding user-defined
ensures become free: the callable procedure's contracts and frame/heap boilerplate
are unchanged. Missing saved original checks fail closed. Filtered translation
keeps the previous path.

The local argument is a proof cut: all exits check the exact saved original
formulas, and all added pieces, with ordinary publication, before any summary of
that clause. A summary cannot justify its own original contract. Earlier checked
clauses may assist later clauses in their established order. No hidden definition,
permission, fuel or source contract is removed. Local contract checks force
checking after retaining the inherited-condition guard, so an inherited origin
or an assume-mode source block cannot turn a relocated original check into an
unchecked premise. Every original check is emitted, including formulas identical
to a canonical piece; formula equality alone does not establish preservation of
the solver's search behavior.
Local call-site checks use ordinary
publication, preserving the method-requires policy; explicit split source assertions
keep their original check-and-forget behavior. Every new and original call
precondition still has to be checked before the actual call.

This is an unaccepted product-repair experiment. Structural and native evidence,
including unchanged complete files and matched seeds, must establish its actual
behavior before any completeness or performance claim. AI assisted the work.

The expression copier rebinds every copied bound variable in quantifier and
lambda bodies, attributes and triggers, and in let bodies. Let right-hand sides
retain their outer scope. Known procedure formals are rebound to implementation
formals; unresolved named identifiers remain unresolved until normal resolution.
This retains lexical binding as well as the emitted formula text.

The candidate additionally checks the exact publication formula at method exits
and call sites, using ordinary assertion publication. This retains the previous
summary's proposition, guards, trigger structure and fuel in the continuation.
It adds a proof obligation rather than an unchecked assumption, retaining every
original and canonical check. Native comparisons must determine its solver
behavior; no performance or acceptance claim follows from this local argument.

The next candidate makes the contract proof cut explicit in ordinary Boogie
control flow. A verification arm checks every canonical, original and exact
summary goal with the same ordered preparation, then ends with `assume false`.
The continuation arm receives those exact checked formulas and the same
declared-contract permissions. The proof arm's terminal assumption occurs after
all checks; it cannot discharge an earlier failed assertion. Verification
requires those checks independently of the continuation arm's assumptions. This
is the usual demonic-choice proof cut: the VC requires both the checked facts and the
continuation under those facts. No source evaluation, heap update or visibility
command is duplicated or moved, and no batching option or focus marker is added.

Every old and strengthened goal, including the exact summary checks, remains.
All previously published facts are retained, including inherited guards, trigger
patterns and fuel terms. Negative controls and matched native source/seed gates
are required to validate this cut and determine its actual solver behavior.

An empty user-contract package emits no proof cut or additional control flow.


The next experiment checks finite ground instances of the existing layer-synonym
axiom before the contract goals. For a known fuel-aware function and unchanged
arguments, the trusted axiom states `f(Succ(layer), args) == f(layer, args)`.
Each generated instance preserves the exact function, type arguments, receiver,
old/current heap, reveal flag and value arguments. Its only replacement is the
fuel argument by the already present successor's child. The generated function
signature supplies the fuel position; an unknown function or shape is skipped.
No new background axiom, can-call permission, hidden body or higher fuel is added.

The instances themselves are checked and published as a conjunction before
being used. All canonical, original and exact summary checks remain, as do their
facts, trigger structure, fuel terms and visibility effects. The collector does
not extract applications from quantified binders, lets or old-expression scopes;
it introduces no escaping bound variable. Native negative controls and matched
source/seed comparisons must validate this experiment before acceptance.


The next experiment checks a guarded folding equality for a non-fuel-aware
predicate whose existing assertion-style lowering already includes a free
`canCall && predicate && body` piece. The equality retains that exact predicate
application and body expression. It keeps the can-call premise, the actual
reveal flag when present, and good-heap guards for the actual heap arguments.
These guards are not granted unconditionally. The equality follows from the
existing function definition and is itself checked before publication or use.
The new equality omits other consequences of that axiom; every existing
permission, goal and published fact remains unchanged.

No new body expansion is performed: existing splitting already enforces body
availability, SCC height, `no_inline`, safe substitution and visibility. Blind
contexts, fuel-aware root functions and unsupported free-piece shapes retain
all existing checking paths. Both the original and strengthened checks remain,
as do the layer-equality checks and exact summary checks. This is an unaccepted
native experiment; isolated solver-input diagnostics are not product acceptance.


A separate collector now checks scoped instances of the same unconditional
layer-synonym law inside ordinary universal and existential contract binders.
Let substitution is lexical: right-hand sides retain the outer scope, and only
the copied support expression substitutes the bound declaration. The original
contract, source quantifiers, trigger patterns and every existing check remain
unchanged. Each support equality is universally closed over exactly its
referenced source dummies, with fresh binding identities and names. An
existential source occurrence never grants or exports a witness. The source
function application supplies a trigger covering those dummies. Generic
function signatures retain their actual fuel position and every other argument,
including current and old heaps and reveal flags.

Universal closure is valid because the layer-synonym equality holds for every
argument and layer, independently of can-call permissions or body visibility.
The new conjunction is checked before publication or use. No source expression
is evaluated, no body definition is unfolded and no higher fuel is introduced.
The ground-support checks, guarded body equalities, canonical pieces, original
checks and exact summary checks are all retained. Polymorphic Boogie binders,
dependent or attributed dummies, lambdas and extraction through old-expression
scopes retain their existing paths without a new scoped instance. This remains
an unaccepted native experiment pending complete structural and proof gates.
