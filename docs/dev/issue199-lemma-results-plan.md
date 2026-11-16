# Binding a lemma's results in an expression: a semantic plan

Issue: [erniecohen/dafny#199](https://github.com/erniecohen/dafny/issues/199), friction case
[`friction/cases/004-lemma-in-function`](https://github.com/erniecohen/dafny/tree/review/4.11.0/friction/cases/004-lemma-in-function).
Status: plan only, for the owner's ruling.  Nothing here is implemented.

Every claim about the current code cites `review/4.11.0` at `1920c844b`; source
paths are relative to `Source/DafnyCore` unless they start with `Source/`.  The
experiments in section 9 ran with the fork's releases
`v4.11.0+fcb2042d.review.bcc0c421` and, for `--modular-termination`,
`v4.11.0+fcb2042d.review.4aa006a8`, with Z3 5.1.0.  They record verdicts only.

## 1. The decision in brief

> **`var y := L(a); e`, in an expression, binds `y` to a value chosen from the
> solutions of `L`'s postcondition at `a`.  The choice is a fixed function of
> the lemma and its arguments.  It is not the value that `L`'s body computes.**

| Question | Decision |
|---|---|
| Syntax | `var y1, ..., yk := L(a); e` and `ghost var ...`, where the right-hand side is one call of a lemma with `k` out-parameters.  No parser change. |
| Ghostness | The bound variables are ghost, by the existing inference for lets. |
| What `e` may assume | `Post_L(a, y)` and the declared types of the out-parameters. |
| What callers learn | The same, about the chosen value, wherever the function's definition is visible. |
| Determinism | One choice function per lemma (not per occurrence), applied to the type arguments, receiver and in-arguments, and to the heap only where the postcondition reads it. |
| Which lemmas | Every single-state lemma whose instantiated out-parameter types cannot contain references.  Soundness does not need the postcondition to determine the outputs uniquely (section 4.4). |
| Well-formedness | The binding is checked as the call statement `L(a)` is: precondition, termination, `--modular-termination` classification.  Stage 2 adds a frame check of the postcondition. |
| What is assumed | `L#bind$canCall(args)`, only where the call was checked.  Never a global axiom guarded by `L`'s precondition (section 4.3 shows why that is unsound). |
| Option | `--lemma-bindings`, default off.  Off: today's error, byte-identical output.  On with no binding in the program: byte-identical Boogie. |
| Stages | 1: postconditions that read no heap.  2: postconditions that read the heap, with a frame check.  Two-state lemmas and reference-typed results are not planned (section 8). |

## 2. The problem, and what Dafny does today

The reproducer:

```dafny
lemma Find(n: nat) returns (m: nat) ensures m > n { m := n+1; }
ghost function Above(n: nat): nat { var m := Find(n); m }
```

is rejected: "expression is not allowed to invoke a lemma (Find)".

**Facts about the current code.**

1. **Where the error comes from.**  The right-hand side of a `var x := E; B` let is parsed
   with `allowLemma` false (`Dafny.atg:4185`), but a lemma call
   `Find(n)` still parses as an ordinary application.  The resolver then rejects it
   because method calls are not allowed in that context:
   `Resolver/PreType/PreTypeResolve.Expressions.cs:2037-2041` (refreshed resolver) and
   `Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs:5870-5874`
   (legacy resolver).  So the feature needs no grammar change.
2. **The only lemma call an expression can hold** is the `L(a); E` form.  It is parsed
   at `Dafny.atg:3380-3388` into a `StmtExpr` around an `AssignStatement` with no
   left-hand side.  A lemma with out-parameters cannot be called that way: "wrong number
   of method result arguments (got 0, expected 1)",
   `Resolver/PreType/PreTypeResolve.Statements.cs:804` (experiment E4 below).  So today a
   lemma with results cannot be used in an expression at all.
3. **A lemma call in an expression contributes nothing to the function's definition.**
   `TrExpr` of a `StmtExpr` is the translation of its expression alone
   (`Verifier/BoogieGenerator.ExpressionTranslator.cs:402-403`), and so is its CanCall
   assumption (`ExpressionTranslator.cs:2576-2578`).  The statement is translated only in
   the well-formedness check (`Verifier/BoogieGenerator.ExpressionWellformed.cs:1692-1726`),
   where it is a call: precondition asserted, termination checked
   (`Verifier/Statements/BoogieGenerator.TrCall.cs:321-337`), postcondition assumed.  The
   only side effect a `StmtExpr` adds to a function's axiom is a `reveal`
   (`Verifier/BoogieGenerator.Functions.cs:576-594`).
   **The feature changes this: a function's definition will depend on a lemma's
   postcondition.**  That is where its soundness argument has work to do.
4. **The call graph.**  A call statement adds an edge from the enclosing callable to the
   lemma (`Resolver/CallGraphBuilder.cs:238-239`), also inside a `StmtExpr` in a function.
   A lemma's `ensures` adds edges from the lemma to the functions it calls: in experiment
   E2, `ghost function F(n: nat): nat { L(n); 0 }` with
   `lemma L(n: nat) ensures F(n) == 0` is rejected with "cannot prove termination" at the
   call `L(n)` in `F`.
5. **Let-such-that is the existing choice mechanism.**  `var x :| P(x, g); B` is
   translated with one Skolem function per bound variable and per occurrence: its name
   carries a fresh `LetId` (`Verifier/BoogieGenerator.LetExpr.cs:180`, `367`), its
   arguments are the free variables of `P`, plus the heap only if `P` reads it
   (`LetExpr.cs:171-181`).  Its one axiom is
   `$let#k$canCall(g) ==> typeOf(x(g)) && CanCall(P) && P(x(g), g)`, triggered on the
   Skolem terms (`LetExpr.cs:238-290`, the implication at `287`).  The well-formedness
   check proves existence by guessing witnesses (`ExpressionWellformed.cs:1852`), then
   assumes `$let#k$canCall(g)` (`ExpressionWellformed.cs:1879`).  The function's result
   is checked with the arbitrary witness, not the Skolem term
   (`ExpressionWellformed.cs:1881`).
6. **Ghost inference.**  `function F(n: nat): nat { var g := G(n); n }` with a ghost `G`
   is accepted: `g` is inferred ghost.  Using `g` in a compiled position is an error
   (experiment G1).
7. **Extreme lemmas have no out-parameters**
   (`AST/Members/MethodOrConstructor.cs:321-322`).

## 3. Semantics

### 3.1 Form and placement

```
var y1, ..., yk := L(a1, ..., an); e          // also: ghost var ...
```

- The right-hand side is exactly one call of a lemma (or of an instance lemma
  through a receiver, `r.L(a)`), and the left-hand side has one variable for
  each out-parameter, as in a call statement.  `_` is allowed for an output that
  is not used.
- Patterns on the left, several right-hand sides, `:-`, and a lemma call nested
  in a larger right-hand side are rejected.  Method (non-lemma) calls stay
  rejected.
- The binding is allowed wherever a lemma call `L(a); e` is allowed today, and
  only there.  In particular it is not allowed where `allowLemma` is false:
  `reads`, `decreases`, and (under the member-invariants plan) the invariant
  itself.
- The lemma is single-state.  A two-state lemma is rejected, as is any lemma
  whose instantiated out-parameter types may contain references (section 3.4).
- In a statement context, `var y := L(a);` is a call statement, as today.  The
  feature changes nothing there.

### 3.2 Ghostness

The bound variables are ghost.  The existing inference for lets already marks
them so (fact 6).  The binding is erased by the compilers, as the statement of a
`StmtExpr` is.  In a compiled function, `e` may use the variables only in ghost
positions.

### 3.3 Meaning

Let `L` have type parameters `X`, an optional receiver, in-parameters `x`,
out-parameters `y : Y`, precondition `Pre_L(x)` and postcondition
`Post_L(x, y)` (the conjunction of its `ensures` clauses, as a caller sees it).
Define the solution set

> `Sol_L(g) = { y | y ∈ Y  and  Post_L(g, y) }`

where `g` is the type arguments, the receiver and the in-arguments, and the heap
if `Post_L` reads it.  `y ∈ Y` is type membership without allocation.

Fix a choice function `ε` on non-empty sets.  Then

> `var y := L(a); e`  means  `e[y := ε(Sol_L(g_a))]`,

and it is well-formed if the call statement `L(a)` is well-formed at that point.

Equivalently, the feature behaves as if each lemma `L` had a hidden ghost
function

```dafny
ghost function L#Result(x): Y requires Pre_L(x) { var y: Y :| Post_L(x, y); y }
```

and `var y := L(a); e` were `var y := L#Result(a); e`, with the existence of `y`
justified by the call `L(a)` instead of by a proof of `exists`.  Experiment R1
runs this reference form, written by hand with an existence lemma.

### 3.4 What is assumed

- **In `e`:** `Post_L(a, y)` and `y ∈ Y` for the chosen `y`.  Nothing else.  In
  particular not what `L`'s body computes: for `Find`, `Above(n) > n` is
  provable and `Above(n) == n + 1` is not (experiment R1, `NotBody`).
- **Outside, where the function's definition is visible:** the same.  The fact
  travels with the function's CanCall facts, as a let-such-that's does
  (experiment E5, `Uses`).  If the function is opaque, the fact is hidden with
  its definition and returns with `reveal`.
- **Repeated use:** two bindings of the same lemma at equal arguments denote the
  same value, in one function or in two.  `(var m := L(a); m) == (var m := L(a); m)`
  holds, and so does `A(n) == B(n)` for two functions that bind `L(n)`.
  Today's let-such-that does not have this property across occurrences
  (experiment E5, `Across`, fails), which is why this plan keys the choice by the
  lemma rather than by the occurrence (section 5.2).
- **Tuples:** the outputs are chosen together, so a postcondition that relates
  them (`a + b == 10`) holds of the chosen pair.

**Why references are excluded.**  If `Y` may contain references, `y ∈ Y` at a
call includes allocation in the current heap.  A choice that depends on the
allocation state makes the function depend on state that no `reads` clause can
name, and a choice that ignores it may return an unallocated object, against
the allocation axiom of function results.  Today's let-such-that over a class
type cannot even prove existence (experiment E6).  This needs its own argument,
and is out of scope (section 8).  The check is on the *instantiated* types: a
generic lemma may be bound only at type arguments that cannot contain
references (the `!new` characteristic, checked at the binding).

## 4. Soundness

### 4.1 What is emitted

For each lemma `L` that the program binds somewhere (and only for those), once:

```
function L#bind$canCall(G): bool;
function L#bind#y_i(G): Y_i;                       // one per out-parameter
axiom (forall G :: {L#bind#y_1(G)} ... {L#bind#y_k(G)}
         L#bind$canCall(G) ==>
             typeOf(L#bind#y(G)) && CanCall(Post_L(G, L#bind#y(G))) && Post_L(G, L#bind#y(G)));
```

`G` is the type arguments, the receiver if `L` is an instance lemma, the
in-parameters, and `$Heap` only if `Post_L` reads it (stage 2).  This is the
shape of today's let-such-that axiom (fact 5), with the lemma's identity in
place of the occurrence's.

At a binding `var y := L(a); e` in a function `F`:

- **Translation:** `TrExpr` is `e[y := L#bind#y(G_a)]`.
- **CanCall:** `CanCall(a) && L#bind$canCall(G_a) && CanCall(e[...])`.
- **Well-formedness check of `F`:** check `a`; translate the call `L(a)` exactly
  as the call statement (precondition assert, termination check,
  `--modular-termination` classification, postcondition assume into fresh
  locals); then `assume L#bind$canCall(G_a)`; then check `e[y := L#bind#y(G_a)]`.
  Stage 2 adds the frame check of section 4.5 before the assumption.
- **No existence assertion.**  The call is the witness.  The trigger problem
  that the issue reports for the workaround (an `exists` with no trigger) does
  not arise.

### 4.2 Why it is consistent

Extend any model of the program without bindings as follows:

- `L#bind$canCall(G) := Sol_L(G) ≠ ∅`;
- `L#bind#y(G) := ε(Sol_L(G))` when `Sol_L(G) ≠ ∅`, and any value of `Y` otherwise.

The axiom of 4.1 then holds by the definition of `ε`.  It remains to show that
every place that assumes `L#bind$canCall(G_a)` is one where `Sol_L(G_a)` is not
empty:

- In `F`'s well-formedness check, the assumption follows a checked call of `L`
  at `a`.  The call proved `Pre_L(a)`.  A verified lemma is totally correct: from
  `Pre_L(a)` it terminates in a state satisfying `Post_L(a, y)` for some `y ∈ Y`.
  So `Sol_L(G_a)` is not empty.
- In the CanCall facts of `F`'s definition, `L#bind$canCall(G_a)` is implied by
  `F#canCall` under the path condition of the binding.  The well-formedness check
  of `F` proved `Pre_L(a)` under `F`'s precondition and the same path condition,
  so the same argument applies wherever `F#canCall` holds in the model, which is
  where `F`'s precondition holds.

**Circularity.**  `L`'s correctness may itself be proved with `F`'s definition,
which now mentions `L#bind#y`.  This is a recursive definition, and it is
well-founded for the reason ordinary recursion is: if `L` can reach `F` (through
its body or its specification), the two are in one strongly connected component
of the call graph (fact 4, experiment E2), and the call `L(a)` in `F` must
decrease.  So the model is built by well-founded induction on the decreases
measure of the component: `F` at a measure `d` depends on `Sol_L` at a smaller
measure, whose non-emptiness is `L`'s correctness at that smaller measure,
which uses `F` only at still smaller measures.  Experiment E7 checks the liar
`ghost predicate P(n, m) { m == F(n) + 1 }`, `F(n) { Ex(n); var m :| P(n, m); m }`:
today it is rejected by termination, and the binding form must be too.

### 4.3 Why the assumption is CanCall, not the precondition

A simpler encoding would state the axiom once, for all arguments:
`forall G :: Pre_L(G) ==> Post_L(G, L#bind#y(G))`.  That is unsound.  The axiom
is in the background of `L`'s own verification, so a lemma could prove its
postcondition at `n` from the axiom at `n`:

```dafny
lemma Bad(n: nat) returns (m: nat) ensures Q(n) && m == 0 { m := 0; }
```

With the global axiom, `Q(n)` follows from `Post_Bad(n, Bad#bind#m(n))` as
soon as anyone binds `Bad`, and the body has nothing to prove.  The canCall
form is sound because `L#bind$canCall(G)` is assumed only after a checked call,
and a call of `L` inside `L`'s own proof at the same measure is rejected by
termination.  The test plan has this control (V3 in section 7).

### 4.4 Determinism, and the class of lemmas

`F`'s value at its inputs is `e[y := ε(Sol_L(G_a))]`.  `G_a` is a function of
`F`'s inputs (and the heap, stage 2), and `Sol_L` depends only on `L`'s
specification.  So `F` is a deterministic function of its inputs.  Its value
does not depend on `L`'s body, which may be nondeterministic (it may use `:|`
statements, or call other lemmas whose results are not unique).

**No uniqueness is needed.**  Binding is sound for every lemma admitted by 3.1,
whether or not its postcondition determines its outputs.  Uniqueness changes
only what can be proved, never what is assumed.  Define the class precisely:

> `L` has **functional results** iff
> `forall G, y1, y2 :: Pre_L(G) && y1 ∈ Y && y2 ∈ Y && Post_L(G, y1) && Post_L(G, y2) ==> y1 == y2`.

For a lemma with functional results, the bound value equals the out-value of
every call statement `L(a)` at the same arguments.  The solver can derive this
from the postcondition alone; the feature adds no axiom for it.  For other
lemmas the bound value is some solution, and nothing relates it to a call
statement's result.

The feature must **not** assume, at a call statement, that the out-values equal
`L#bind#y(G)`.  The call statement's meaning is a relation, and a lemma body
returns whichever solution it computes.  Equating them would make the
statement-level meaning of every bound lemma deterministic, which no lemma's
verification justifies.

The class is not checked and not needed.  It is defined here because it is the
answer to "when does the bound value equal what the lemma returns?": exactly
when the lemma has functional results.

**Existential witnesses inside the postcondition.**  If `Post_L` contains
`exists k :: Q(y, k)`, the choice is over `y` only.  `k` stays existential in the
axiom, in a positive position, where Z3 skolemizes it.  To name `k`, the lemma
must return it.

### 4.5 Heap-reading postconditions (stage 2)

A lemma's postcondition is not framed: it may read any location.  Without a
check, binding such a lemma breaks `F`'s frame axiom.  The liar:

```dafny
class C { var f: int }
lemma Get(c: C) returns (x: int) ensures x == c.f { x := c.f; }
ghost function F(c: C): int { var x := Get(c); x }      // no reads clause
method M(c: C) modifies c { c.f := 0; var a := F(c); c.f := 1; assert a == F(c); assert false; }
```

`F`'s frame axiom says `F` does not depend on the heap, while `F(c) == c.f` in
every heap.  So the rule for stage 2:

- **Frame check.**  In `F`'s well-formedness check, every heap location that the
  instantiated `Post_L(a, y)` reads, under the path condition and `Pre_L(a)`,
  must be in `F`'s `reads` frame.  These are the same `reads` checks that
  checking `Post_L(a, y)` as an expression in `F`'s body would make.  Other
  well-formedness conditions of `Post_L` were checked in `L` and are not
  repeated.  Today's let-such-that already makes this check (experiment E4: "insufficient
  reads clause to read field").
- **Why that suffices, with one choice function per lemma shared by several
  functions.**  Choose `L#bind#y(h, G) := ε(Sol_L(h, G))`, a function of the
  *set* of solutions.  If two heaps `h1` and `h2` agree on `F`'s frame and `F`'s
  precondition holds, the frame check gives `Sol_L(h1, G) = Sol_L(h2, G)`, so the
  choice is the same and `F`'s frame axiom holds.  Each function's frame axiom
  constrains only heaps that agree on its own frame, so functions with
  different frames do not interfere.  Stage 1 needs none of this: its choice
  functions take no heap.

## 5. Alternatives considered

### 5.1 Expose the value the lemma's body computes

Rejected.  A lemma's body is not a function: it may be nondeterministic, and its
callers depend only on its specification.  Exposing it would make every
binding depend on proof details and break the abstraction that lemmas provide.

### 5.2 The issue's suggestion: desugar to `L(a); var y :| Post_L(a, y); e`

This is close to the plan, and it is sound for the reasons in section 4.  The
plan departs from it in three ways:

1. **One choice per lemma, not per occurrence.**  The desugaring gives each
   occurrence its own Skolem function (fact 5), so two functions that bind
   `L(n)` cannot be proved equal (experiment E5, `Across`), and a fact about a
   binding inside a lambda cannot be recovered by the caller.  The issue's own
   use is `seq(n, i => ...)`: with one choice per lemma, the caller can write the
   same binding and the terms match (experiment R1, `Seq`, with the named
   function playing the per-lemma choice).  Experiment B3 shows that an inline
   `:|` in the lambda body is not recoverable: `S1(n)[0] > 0` fails.
2. **No existence proof.**  The desugaring asks the solver to prove
   `exists y :: Post_L(a, y)` by guessing, which is where the issue's trigger
   failure came from.  The call already supplies the witness.
3. **No copy of the postcondition into the caller.**  The postcondition's
   well-formedness was checked in `L`.  Re-checking it in every caller costs
   resource units and adds failure points.  Only the frame check (stage 2) is
   repeated.

### 5.3 Restrict to lemmas with functional results

Rejected as unnecessary (section 4.4).  It would also need a uniqueness proof
at each lemma, which is the obligation that compiled let-such-that has today
and which ghost code does not need.

## 6. Interactions

### 6.1 Termination, and `--modular-termination` (issues 245 and 266)

- **Termination is the call's.**  The binding is checked as the call statement:
  the edge `F → L` (fact 4), the decreases check when `F` and `L` share a
  component (`TrCall.cs:321-337`), and a lemma's `ensures` adds edges to the
  functions it mentions (experiment E2).  No new edge is needed for the new
  definitional dependency of `F` on `Post_L`, because `F → L → (callees of
  Post_L)` is already in the graph.
- **`--modular-termination`.**  The analysis walks a `StmtExpr`'s statement and classifies the `CallStmt` in it
  (`Resolver/ModularTermination.cs:1103-1118`, and the call case at `693`).  A
  lemma call in a function body is classified today: experiment M1 reports, for
  `ghost function F(n: nat): nat { A.L(n); n }` in a client module, "a call of
  'A.L' ... runs under the caller's bound (same-bound rule)".  The binding must
  present the same `CallStmt` to that walk, so that it gets the same
  classification and the same message.  Test: the `--show-hints` output of
  `var m := A.L(n); m` names the call as `A.L(n); n` does.
- **The M4b modularity property** (a client depends only on a library's
  specifications) holds: a binding depends on `Post_L`, which is `L`'s published
  contract, and never on its body.  One consequence to document: a binding makes
  a library lemma's `ensures` part of the *meaning* of a client function.  If a
  library update changes `Post_L`, the chosen value may change.  Client proofs can
  only have used `Post_L`, so proofs that still verify stay valid.  Ghost values
  have no run-time behaviour to change.
- **Issue 245, user orders** ([issue 245](https://github.com/erniecohen/dafny/issues/245)).
  `decreases e by O` needs `O` closed and ghost.  `O` is a `decreases` component,
  where lemma calls are not allowed, so it cannot contain a binding.  Nothing else
  changes: bindings use the decreases clauses and orders of the callables
  involved, as calls do.
- **Issue 266, bounded callables** ([issue 266](https://github.com/erniecohen/dafny/issues/266)).
  Lemmas are not values, so no lemma becomes a bounded callable.  A binding inside
  a lambda body is a call inside that body, and callback formation already checks
  the calls in a lambda body.  The test: a lambda with a binding, passed to a
  `{:callback}` parameter, gets the same verdict as with `L(i); e`.

### 6.2 Opaque functions and `reveal`

- **The value does not depend on what is revealed.**  `Post_L` is translated to
  the same Boogie term whether or not the functions in it are revealed: `reveal`
  adds knowledge, not meaning.  The choice is over that term.
- **A `reveal` before a binding** affects only the well-formedness proof.  Its
  side effect on the axiom (`Functions.cs:576-594`) is unchanged, and a binding
  adds none.
- **An opaque function that binds a lemma** hides `Post_L(a, L#bind#y(G_a))` with
  its definition, because the fact reaches callers only through the definition's
  CanCall facts.  `reveal F()` brings it back.  Test: before `reveal`, `F(n) > n`
  fails; after, it holds.
- **`hide`/`reveal` and `--default-function-opacity`** behave the same, for the
  same reason.  A test runs the reproducer under `--default-function-opacity opaque`.

### 6.3 Member invariants ([draft PR 384](https://github.com/erniecohen/dafny/pull/384))

- **Out-parameter types with an invariant.**  `y ∈ Y` includes `Y`'s invariant,
  so the chosen value satisfies it.  Existence holds because `L`'s verification
  checked the conversion to `Y`.  A `raw T` out-parameter carries no invariant
  and nothing changes.
- **The invariant itself cannot bind**: PR 384 parses it with `allowLemma: false`.
- **The cone.**  PR 384's extension condition (its section 3.3) ranges over the
  *cone* of `T`'s invariant: invariants and the bodies and specifications of the
  functions they reach.  A lemma *call* in a function of the cone does not
  matter today (fact 3).  A *binding* does: it makes `Post_L` part of the
  function's value.  So with both features on:
  1. a binding's out-parameters are bound variables, and clause 1 applies to
     them: an out-parameter of a `T`-dependent type in the cone is rejected,
     like `:|` over `T` without a collection bound;
  2. the cone includes the `ensures` of every lemma bound in it;
  3. **recommended, conservative:** the cone also includes the body of every
     lemma bound in it.  The existence of a solution, which the model of 4.2
     needs, comes from the body's proof, and that proof may use `T`'s typing
     facts, which are defined by the cone.  An argument that the `ensures` alone
     suffice may exist; it is open question 3.
- **Reads.**  PR 384 requires an empty frame in the cone.  A stage-2 binding of
  a heap-reading postcondition fails the frame check there, as it should.

## 7. Implementation stages and tests

### Option

`--lemma-bindings`, default false, settable in `dfyconfig.toml`.  Requires
`--type-system-refresh`; the legacy resolver keeps today's error.  With the option
off, the error text and all output are unchanged.  With it on and no binding in
the program, no declaration is emitted, so the Boogie is byte-identical; the
pull request shows this on the suite and the standard library (verdicts and
resource counts).  The error without the option gains a hint: "use
--lemma-bindings to bind a lemma's results".

### Stage 1: heap-free postconditions

- **Resolver** (`PreTypeResolve.Expressions.cs`, the let case): when the option is
  on and a let's single right-hand side resolves to a lemma call, build a
  `LemmaBindingExpr` (a resolved form of `LetExpr`, keeping the `CallStmt` for
  the analyses).  Checks: count of left-hand sides; single-state, non-extreme
  lemma; instantiated out types `!new`; `Post_L` reads no heap (computed with
  `FreeVariablesUtil`, as fact 5's heap argument is).  Ghost inference for the
  bound variables.  Call-graph edge, and the `--modular-termination` walk, through
  the kept `CallStmt`.
- **Translator:** the declarations and axiom of 4.1, created on first use and
  shared; `TrExpr`, `CanCallAssumption` and well-formedness as in 4.1.
  Substitution and cloning must keep the lemma, the receiver and the arguments.
- **Compilers:** erase the binding.
- **Docs:** Reference Manual section on expressions; `--help` text; news fragment
  `docs/dev/news/199.feat`.

### Stage 2: heap-reading postconditions

The frame check of 4.5, and `$Heap` in `G` for lemmas whose postcondition reads
the heap.

### Tests

Regression `git-issues/github-issue-199.dfy`, run with the option and, for the
off case, without it.  Each "must fail" test has an `.expect` line.

**Accepted (positive):**

- P1: the reproducer; `Above(n) > n` proved by a caller.
- P2: two outputs with a relating postcondition (`a + b == 10`); the relation
  holds of the bound pair.
- P3: repeated use: `A(n) == B(n)` for two functions that bind `L(n)`; the same
  binding twice in one expression.
- P4: the issue's use: `seq(n, i requires 0 <= i => var m := L(i); m)`, and a
  caller that proves `S(n)[0] > 0` by writing the same binding.
- P5: bindings under `forall`, in a lambda, nested bindings, a binding in a
  function's own `ensures`.
- P6: generic lemma at a value type; instance lemma with a receiver; trait lemma
  through dispatch; a lemma with a precondition.
- P7: a binding in a compiled function, used only in ghost positions; the
  program compiles and runs.
- P8: a lemma whose results are not functional (`ensures m > n`) and one whose
  results are (`ensures m == n + 1`): only the second proves `F(n) == n + 1`.
- P9: opaque: `F(n) > n` fails before `reveal F()` and holds after; the same
  under `--default-function-opacity opaque`.
- P10 (stage 2): `ghost function F(c: C): int reads c { var x := Get(c); x }`
  accepted, and `F(c) == c.f` proved.

**Rejected (negative), and vacuity controls:**

- V1: in every positive file, a function with a binding followed by
  `assert false` in a caller must fail.
- V2: `NotBody`: `Above(n) == n + 1` must fail for `ensures m > n`.
- V3: the precondition-axiom liar of 4.3: a lemma that would prove its own
  postcondition from its binding is rejected by termination; and `Q(n)` is not
  provable at an argument where nothing called the lemma.
- V4: the circular definition of 4.2 (E7 in binding form) is rejected by
  termination.
- V5: precondition violated at the binding.
- V6: stage 1 rejects a heap-reading postcondition with a message naming stage 2's
  condition; stage 2 rejects the frame liar of 4.5 ("insufficient reads clause"),
  and its `assert false` must fail when the reads clause is added.
- V7: rejected forms: a method call, a two-state lemma, a reference out type, a
  generic lemma instantiated with a class type, a pattern on the left, two
  right-hand sides, `:-`, a binding in `decreases` or `reads`.
- V8: `--modular-termination`: a binding of an imported lemma gets the same
  classification and hint as the call form; a `{:termination_closed}` function
  that binds a lemma reachable to open dispatch is rejected as the call form is.
- V9: option off: the reproducer gives today's error, byte for byte.
- With PR 384 (when both exist): an out-parameter of the invariant's own type in
  the cone is rejected (clause 1).

## 8. Out of scope

- **Two-state lemmas**, bound in two-state functions.  The choice would take both
  heaps, and the frame argument of 4.5 would need the two-state frame.  Not hard,
  but not needed by the issue.
- **Reference-typed results** (section 3.4).  This needs an argument about
  allocation, which functions cannot read.
- **Statement-level determinism** (4.4): never.

## 9. Experiments

Each program is a few lines; the tests of section 7 restate them.  Verdicts:

| | Program | Verdict |
|---|---|---|
| E1 | the reproducer | resolution error "expression is not allowed to invoke a lemma (Find)" |
| E2 | `F(n) { L(n); 0 }`, `lemma L(n) ensures F(n) == 0` | termination errors at both edges: the lemma's `ensures` is a call-graph edge |
| E3 | `F(n) { L(n); 0 }`, `lemma L(n) { var x := F(n); }` | termination errors |
| E4 | existence lemma + `var x :| x == c.f` in a function without `reads` | "insufficient reads clause to read field" |
| E4' | `ghost function F(c: C): int { Get(c); ... }` with `Get` returning a value | "wrong number of method result arguments (got 0, expected 1)" |
| E5 | two functions with the same existence lemma and `:|` | `A(n) == A(n)` and `A(n) > n` verify; `A(n) == B(n)` fails |
| E6 | `ghost function G(): C { var o: C :| true; o }` | existence cannot be proved |
| E7 | `P(n, m) == (m == F(n) + 1)`, `F(n) { Ex(n); var m :| P(n, m); m }` | termination errors |
| E9 | `least lemma` with an out-parameter | "least lemmas are not allowed to have out-parameters" |
| G1 | ghost let in a compiled function | accepted unless the ghost variable is used in a compiled position |
| B3 | `seq` with an inline `:|` in the lambda vs a named choice function | inline: `S1(n)[0] > 0` fails; named: holds with the hint `S2(n)[0] == Pick(0)` |
| R1 | the reference desugaring of 3.3, by hand | `A(n) == B(n)`, `A(n) > n` and the `seq` property verify; `A(n) == n + 1` and `false` fail |
| M1 | `--modular-termination --show-hints`, lemma call in a client function | classified "same-bound rule", as any call |

## 10. Open questions for the owner

1. **One choice per lemma (recommended) or per occurrence?**  Per lemma gives
   repeated-use equality and recoverable facts under lambdas (5.2).  Per
   occurrence is what `:|` does today.  Both are sound.
2. **The option's name, and requiring `--type-system-refresh`.**  The plan says
   `--lemma-bindings`, and refreshed resolver only.
3. **With PR 384: must the bodies of bound lemmas be in the invariant's cone**
   (recommended, conservative), or only their `ensures`?
