# Contract checking consolidation experiment

This candidate retains legacy return-position reveal visibility and every original
postcondition formula, inherited guard, trigger and fuel term. It checks the
original implementation postconditions locally at every explicit return and
fallthrough, together with the assertion-style pieces, before publishing each
clause summary. The implementation procedure retains the exact original checked user-defined
ensures alongside the existing free counterparts. Callable contracts and
frame/heap boilerplate are unchanged. Missing saved original checks fail closed. Filtered translation
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

The scoped support copier also handles identifiers awaiting Boogie resolution.
Its optional name map binds only declarations in the current quantifier or let
scope, masking and restoring shadowed names on entry and exit. Other named
identifiers remain unresolved. Existing copier calls omit this map and retain
their previous behavior. Let right-hand sides use the outer name map before
installing the let declarations. Closure construction then uses declaration
identities, so renaming support dummies cannot leave a source binder reference
unbound or capture one under a nested binder.


Context support additionally reads the already translated statement prefix,
including loop invariants, conditional guards and predicate commands. It never
collects or modifies the original builder. It checks the same unconditional fuel
law for those existing terms, closing source binders universally and substituting
lets only in the support copy. This collects terms without importing an earlier
predicate as a premise: the law holds for any current values of implementation
locals and heaps. Every new equality is checked before use and publication.

An expression containing a reveal-parameter function application, including a
nested argument, is conservatively excluded from context collection. Lambdas
and unsupported expression-local constructs are excluded. The collector does
not extract a function application from inside an old-expression scope.
The existing reveal, hide, push and pop commands retain their original order and
outer-scope effects. The existing contract-only ground and scoped collectors,
body equalities and every original, canonical and summary check remain.


The collector distinguishes generated contract proof cuts by their statement
identity. It does not revisit those cuts when collecting source-context terms
for a later call or exit. Every earlier cut, check and published fact remains in
the original command stream; only the additional term search avoids feeding its
own support expressions back into itself. Successive identical source contexts
therefore produce the same bounded number of checked support instances.

Context eligibility now permits existing old heap or value arguments. The
collector itself still does not descend into an `OldExpr` to extract a function
application. When an eligible application already takes `old(heap)` or another
old-state argument, that exact argument is retained on both sides of its fuel
equality. The existing unconditional law holds for every heap and every value;
instantiating it with an old-state value is therefore valid. No old/current
state conversion, source evaluation, reveal permission or body expansion occurs.
Every earlier support instance, original check and published fact remains.


Context collection also covers resolved non-opaque fuel-aware function signatures
whose Boogie declarations have not yet been emitted. Refinement implementations
can precede inherited function declarations, so the declaration cache alone is
not a complete signature registry. The additional registry reads resolved source
metadata and mirrors the existing function-signature layout: type arguments,
fuel, prior heap when applicable, current heap when read, receiver and source
arguments. It emits no declaration, assigns no parameter name, and changes no
existing cache entry or source visibility. Conflicting duplicate signatures fail
closed. Opaque functions are excluded from this additional registry.

Every earlier cache-based instance is still collected by its unchanged pass. A
second pass adds only signatures absent from that cache, using the same existing
terms, old-state arguments, lexical closure and checked publication. Each new
instance is again a specialization of the existing unconditional successor law
for that exact resolved function. Original contract-only ground and scoped
collectors remain unchanged.


Newly materialized definition support validates its additional trigger hints
after substituting actual arguments. An argument can contain equality, a
comparison or a quantifier that the original definition's formal argument did
not contain; the resulting copied pattern can be illegal in Boogie. Only an
ill-formed additional pattern is omitted, leaving its complete guarded
proposition, every support check and every published consequence intact. Legal
patterns remain, as do all original source patterns and definition axioms. The
solver may infer patterns when no legal additional hint remains. This does not
change fuel, definition permissions, hide/reveal scope or the default-off path.
Native verification and full compatibility remain required before acceptance.


The next experiment gives definition-agreement checks explicit local instances
of the complete native definition axiom. Universal elimination of the existing
`forall a :: B(a)` supplies `B(actuals)`. If actuals contain source-bound
variables, universal generalization supplies the corresponding closed instance.
Every original implication premise, conjunction, heap and fuel argument remains
in that body. A binder audit rejects ambiguous identities, escaping dummies,
incomplete or ill-typed substitutions and unsupported scopes. Let right-hand
sides retain their outer scope. Illegal new trigger hints are omitted without
omitting their logical bodies; original axiom and source patterns stay intact.

These assumptions appear only in the existing verification arm. They are
instances of already trusted axioms, never assumptions of a user clause, its
summary or the extracted equality alone. No instance grants a can-call guard
unconditionally, adds body visibility, or changes outer reveal behavior.
Every narrow definition-agreement check, original and strengthened contract
check, checked summary and continuation fact remains. Unsupported additional
instantiations keep every existing check. Triggered false controls, native
negative examples and complete matched gates are still required for acceptance.
