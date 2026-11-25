# Binding a lemma's results in an expression: a semantic plan

Issue: [erniecohen/dafny#199](https://github.com/erniecohen/dafny/issues/199), friction case
[`friction/cases/004-lemma-in-function`](https://github.com/erniecohen/dafny/tree/review/4.11.0/friction/cases/004-lemma-in-function).
Status: implemented, both stages, behind `--lemma-bindings`.  Section 12 says where
the implementation differs from the plan and what it settled.  The plan itself is
the second revision: an adversarial pass tried to break every soundness claim of
the first with a program, and section 11 lists what it changed.

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
| Determinism | One choice function per lemma declaration (not per occurrence), applied to the type arguments, the receiver and all in-arguments, and to the heap only where the postcondition uses it. |
| Which lemmas | A single-state lemma, at a binding where (a) each instantiated out-parameter type satisfies `(!new)`, and (b) the postcondition is an expression that a function could contain: it does not depend on the set of allocated objects and uses no two-state construct (section 3.1).  Soundness does not need the postcondition to determine the outputs uniquely (section 4.4). |
| Well-formedness | The binding is checked as the call statement `L(a)` is: precondition, termination, `--modular-termination` classification.  The body is then checked with the chosen value.  Stage 2 adds the reads checks of the postcondition. |
| What is assumed | `L#bind$canCall(args)`, only where the call was checked.  Never a global axiom guarded by `L`'s precondition (section 4.3 shows why that is unsound). |
| Trust | A binding assumes what the call statement at the same place assumes, and one thing more: that equal arguments give equal results. |
| Option | `--lemma-bindings`, default off.  Off: today's error, byte-identical output.  On with no binding in the program: byte-identical Boogie. |
| Stages | 1: postconditions that use no heap.  2: postconditions that use the heap, handled as a let-such-that's constraint is today.  Two-state lemmas and results that may show references are not planned (section 8). |

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
   `Resolver/PreType/PreTypeResolve.Statements.cs:804` (experiment E4').  So today a
   lemma with results cannot be used in an expression at all.  Only lemmas may be
   called there: "in a statement expression, calls are allowed only to lemmas"
   (`Resolver/GhostInterestVisitor.cs:230`).
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
   A lemma's specification adds edges from the lemma to the functions it mentions: in
   experiment E2, `ghost function F(n: nat): nat { L(n); 0 }` with
   `lemma L(n: nat) ensures F(n) == 0` is rejected with "cannot prove termination" at the
   call `L(n)` in `F`.  Experiment A5 repeats this in every expression context.
5. **Let-such-that is the existing choice mechanism.**  `var x :| P(x, g); B` is
   translated with one Skolem function per bound variable and per occurrence: its name
   carries a fresh `LetId` (`Verifier/BoogieGenerator.LetExpr.cs:180`, `367`), its
   arguments are the free variables of `P`, plus the heap only if `P` uses it
   (`LetExpr.cs:171-181`).  Its one axiom is
   `$let#k$canCall(g) ==> typeOf(x(g)) && CanCall(P) && P(x(g), g)`, triggered on the
   Skolem terms (`LetExpr.cs:238-290`, the implication at `287`).  The well-formedness
   check proves existence by guessing witnesses (`ExpressionWellformed.cs:1852`), then
   assumes `$let#k$canCall(g)` (`ExpressionWellformed.cs:1879`).  The body, and the
   function's result, are checked with an arbitrary witness, not the Skolem term
   (`ExpressionWellformed.cs:1857-1881`).
6. **Ghost inference.**  `function F(n: nat): nat { var g := G(n); n }` with a ghost `G`
   is accepted: `g` is inferred ghost.  Using `g` in a compiled position is an error
   (experiment G1).
7. **Extreme lemmas have no out-parameters**
   (`AST/Members/MethodOrConstructor.cs:321-322`).  A lemma cannot be declared
   `decreases *`, and cannot use `new` (`Resolver/GhostInterestVisitor.cs:564`).
8. **How a let-such-that's constraint is framed.**  In a function's well-formedness
   check, a reads check is recorded in a boolean when the walk reaches the read, and
   asserted at the end (`ExpressionWellformed.cs:104-112`).  For a let-such-that the
   walk of the constraint comes first, with an arbitrary value, and the constraint is
   assumed before the assertion.  So the check is in effect made **on solutions**: in a
   function without `reads`, `var x :| (x == 1 ==> c.f == 7) && x == 0; x` is accepted,
   and `var x :| x == 1 ==> c.f == 7; x` is rejected (experiment A9).  A function without
   a `reads` clause whose body mentions the heap is axiomatized in one fixed heap,
   `$OneHeap` (`Functions.cs:298-317`), and so is the Skolem term in it.
9. **A lemma's specification may depend on the allocation state; a function may not.**
   A quantifier over a reference type in a lemma ranges over the allocated objects.
   In a function (or constant, or type constraint) the resolver rejects it unless a
   bound makes it independent of allocation
   (`Resolver/BoundsDiscovery/BoundsDiscovery.cs:20-21`, `198-206`), and rejects
   `allocated` (`PreTypeResolve.Expressions.cs:477-486`), `old`, `fresh` and `unchanged`.
   A lemma's postcondition may use all of these (experiment A6).
   The computation of "uses the heap" behind fact 5 (`AST/FreeVariablesUtil.cs:48-115`)
   reports `allocated`, a mutable field, an array element, a heap-reading function
   and every application of a function value.  It does not report a quantifier over
   the allocated objects, which need not read any field.  Today's let-such-that
   shows the gap outside functions (experiment A16).  A quantifier with an
   allocation-independent bound is translated with no allocation guard, in a lemma
   as in a function.
10. **A lemma call under `old(...)` is checked in the old state**: the statement
    translation switches `$Heap` to the translator's heap for the statement
    (`ExpressionWellformed.cs:1702-1716`).  The precondition is checked there and the
    postcondition is assumed there (experiment A11).
11. **`(!new)` is decided by `MayShowReferences`** (`AST/Types/Types.cs:990-1000`,
    `AST/Types/UserDefinedType.cs:456-462`): a general arrow `~>` always may show a
    reference; a partial or total arrow does only through its argument and result types
    (experiment A8).
12. **A function's reference result is taken to be allocated** in every heap in which
    its arguments are (`Functions.cs:217`): for a bodyless `R(k: int): C`,
    `allocated(R(k))` verifies, and so does `old(allocated(R(k)))` (experiment A7).

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
- In a statement context, `var y := L(a);` is a call statement, as today.  The
  feature changes nothing there.

**Which lemmas can be bound.**  Three conditions, each checked at the binding
and reported there, naming the construct that fails:

1. **The lemma is single-state.**  A `twostate lemma` is rejected.
2. **Each instantiated out-parameter type satisfies `(!new)`** (fact 11): it
   cannot show a reference.  A generic lemma may be bound at type arguments that
   satisfy it; a function's own type parameter must then be declared `(!new)`.
   Total and partial arrow types over such types are allowed; `~>` is not.
3. **The postcondition is an expression that a function could contain.**  It
   passes the resolver's checks for a function body: no quantifier or
   comprehension that depends on the set of allocated objects, no `allocated`,
   `old`, `fresh` or `unchanged`, no call of a two-state function (fact 9).
   Stage 1 requires in addition that it uses no heap, as fact 9's computation
   reports it.  That computation counts every application of a function value, so
   a postcondition that applies one waits for stage 2.

Condition 3 is new in this revision, and it is needed: the first revision asked
only that the postcondition read no heap, and that check does not see
allocation (fact 9).  Section 4.6 has the counterexample.

The *precondition* of the lemma is not restricted.  It is checked at the
binding, in the binder's own state, as the call statement checks it.

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
if `Post_L` uses it.  `y ∈ Y` is type membership; by condition 2 it involves no
allocation.  By condition 3, `Sol_L(g)` depends on nothing but `g`.

Fix a choice function `ε` on non-empty sets.  Then

> `var y := L(a); e`  means  `e[y := ε(Sol_L(g_a))]`,

and it is well-formed if the call statement `L(a)` is well-formed at that point.

Equivalently, the feature behaves as if each lemma `L` had a hidden ghost
function

```dafny
ghost function L#Result(x): Y requires Pre_L(x) { var y: Y :| Post_L(x, y); y }
```

and `var y := L(a); e` were `var y := L#Result(a); e`, with the existence of `y`
justified by the call `L(a)` instead of by a proof of `exists`.  Experiments R1
and A1 run this reference form, written by hand with an existence lemma.

**The choice is keyed by the lemma declaration that the call resolves to.**  A
trait's lemma and a class's override of it are two declarations with two
postconditions, and two choices: nothing relates `(t as T).L()` bound through the
trait to `c.L()` bound through the class.  A refining module's copy of a lemma is
its own declaration.  The guarantee is exactly: the same declaration, at equal
type arguments, receiver and arguments (and, in stage 2, in the same heap), gives
the same value.

### 3.4 What is assumed

- **In `e`:** `Post_L(a, y)` and `y ∈ Y` for the chosen `y`.  Nothing else.  In
  particular not what `L`'s body computes: for `Find`, `Above(n) > n` is
  provable and `Above(n) == n + 1` is not (experiments R1 and A1, `NotBody`).
- **Outside, where the function's definition is visible:** the same.  The fact
  travels with the function's CanCall facts, as a let-such-that's does
  (experiment E5, `Uses`).  If the function is opaque, the fact is hidden with
  its definition and returns with `reveal`.
- **The whole postcondition, not only the part about the results.**  A caller
  that sees the chosen value learns every `ensures` clause of the lemma at those
  arguments, including one that does not mention the results
  (`ensures Fact(n) ensures m > n` gives the caller `Fact(n)`, experiment A14).
  This is sound: the checked call proved it.  If the body does not use the
  results (`var _ := L(a); e`), no choice term appears and the caller learns
  nothing, as with `L(a); e` today.
- **Repeated use:** two bindings of the same lemma at equal arguments denote the
  same value, in one function or in two.  `(var m := L(a); m) == (var m := L(a); m)`
  holds, and so does `A(n) == B(n)` for two functions that bind `L(n)`
  (experiment A1).  Today's let-such-that does not have this property across
  occurrences (experiments E5 and A2), which is why this plan keys the choice by
  the lemma rather than by the occurrence (section 5.2).
- **Tuples:** the outputs are chosen together, so a postcondition that relates
  them (`a + b == 10`) holds of the chosen pair.
- **Trust.**  A binding of a bodyless or `{:axiom}` lemma trusts that lemma as a
  call statement does, and no more.  A bodyless lemma whose result type is empty
  already proves `false` at a call statement today (experiment A13); a binding
  of it does the same.

**Why results that may show references are excluded.**  The chosen value would
be a function of the lemma's arguments alone, and Dafny takes a function's
reference result to be allocated in every heap in which its arguments are, old
heaps included (fact 12).  A lemma can pick out an object by a property that
needs no heap read, such as a constant field.  A heap-free choice with that
property, written as an axiom, proves `false` from "no such object has been
allocated yet" (experiment A7).  A choice that depended on the allocation state
instead would make the function depend on state that no `reads` clause can name.
Today's let-such-that does allow a reference to be chosen in a function
(`var c: C :| c in s; c`), because there the existence proof needs a witness
that the function can already name.  A lemma's existence proof has no such
limit.  So this plan is stricter than `:|` here, and relaxing it needs its own
argument (section 8).

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

`G` is the type arguments, the receiver if `L` is an instance lemma, all the
in-parameters, and `$Heap` only if `Post_L` uses it (stage 2).  This is the
shape of today's let-such-that axiom (fact 5), with the lemma's identity in
place of the occurrence's.

At a binding `var y := L(a); e` in a function `F`:

- **Translation:** `TrExpr` is `e[y := L#bind#y(G_a)]`.
- **CanCall:** `CanCall(a) && L#bind$canCall(G_a) && CanCall(e[...])`.
- **Well-formedness check of `F`:** check `a`; translate the call `L(a)` exactly
  as the call statement (precondition assert, termination check,
  `--modular-termination` classification, postcondition assume into fresh
  locals); then `assume L#bind$canCall(G_a)`; then check `e[y := L#bind#y(G_a)]`.
  Stage 2 makes the reads checks of section 4.5 on the fresh locals, before the
  assumption.
- **The body is checked with the chosen value, not with the call's fresh
  locals.**  This differs from let-such-that, which checks the body with an
  arbitrary witness (fact 5).  It has to: with the choice keyed by the lemma,
  `var a := L(n); var b := L(n); 10 / (1 + a - b)` is well-formed because `a`
  and `b` are the same term.  Checked with two arbitrary witnesses it would be
  rejected, and its definition would still say they are equal.  The reference
  form accepts it; the per-occurrence form reports a possible division by zero
  (experiments A1 and A2).
- **No existence assertion.**  The call is the witness.  The trigger problem
  that the issue reports for the workaround (an `exists` with no trigger) does
  not arise.

### 4.2 Why it is consistent

Take stage 1 first: `Post_L` uses no heap and does not depend on allocation, so
`Sol_L(G)` is a set determined by `G`.  Extend the intended model of the program
without bindings as follows:

- `L#bind$canCall(G)` holds iff `G` is well-typed and `Pre_L` holds of `G` in
  some heap in which the references in `G` are allocated;
- `L#bind#y(G) := ε(Sol_L(G))` when `Sol_L(G) ≠ ∅`, and any value otherwise.

**The axiom of 4.1 is then true at every `G`.**  Suppose `L#bind$canCall(G)`,
with `Pre_L` holding in heap `h`.  `L` is verified.  Its well-formedness check
showed that `Post_L(G, y)` is well-formed for every `y ∈ Y` under `Pre_L`, which
gives the `CanCall` conjunct.  Its body is totally correct: from `Pre_L` in `h`
it terminates with some `y ∈ Y` satisfying `Post_L(G, y)`.  So `Sol_L(G)` is not
empty, `ε` picks a member, and the other two conjuncts hold.  The first
revision defined `L#bind$canCall(G)` as `Sol_L(G) ≠ ∅` alone; that does not
justify the `CanCall` conjunct, whose well-formedness is known only under
`Pre_L`.

**Every assumption of `L#bind$canCall(G_a)` is then true, not merely
consistent:**

- In a well-formedness check, the assumption follows a checked call of `L` at
  `a`, which proved `Pre_L(a)` in the current heap.
- Everywhere else, `L#bind$canCall(G_a)` enters as part of `CanCall(E)` for an
  expression `E` that contains the binding.  The design rests on an invariant
  that Dafny already depends on for every function's `canCall` and every
  `$let$canCall`: **`CanCall(E)` is assumed only where `E` has passed its
  well-formedness check under the hypotheses then in force** (a function's
  definition under `F#canCall`, its consequence axiom, a method's specification,
  a loop invariant, a quantifier's or a lambda's body under its range).  Under
  those hypotheses the check proved `Pre_L(a)` on the path to the binding.
  Stage 1 gives a second line of defence: an instance of the axiom at `G_a` is
  true whenever `Pre_L` holds of `G_a` in any such heap at all.

An implementation item follows from this (section 7): read every place that
emits a `CanCall` assumption, and confirm the invariant for an expression that
contains a binding.

**Circularity.**  `L`'s correctness may itself be proved with `F`'s definition,
which now mentions `L#bind#y`.  This is a recursive definition, and it is
well-founded for the reason ordinary recursion is: if `L` can reach `F` (through
its body or its specification), the two are in one strongly connected component
of the call graph (fact 4), and the call `L(a)` in `F` must decrease.  So the
model is built by well-founded induction on the decreases measure of the
component: `F` at a measure `d` depends on `Sol_L` at a smaller measure, whose
non-emptiness is `L`'s correctness at that smaller measure, which uses `F` only
at still smaller measures.

Today a lemma call gives a function no value, so a cycle that Dafny failed to
reject would have been harmless.  With bindings it would not be.  Experiment A5
therefore tests the cycle in every context where `L(a); e` can stand: a function
body, the lemma's `requires`, the lemma's `ensures` (with and without a body), a
lemma's own `ensures`, the function's own `ensures`, a lambda in the function
body, a default parameter value, a third lemma's specification, a constant's
initializer, a constant of function type, and a subset type's constraint.  All
are rejected today: the first eight by "cannot prove termination", the constants
by "const definition contains a cycle", the constraint by "recursive constraint
dependency".  A cycle in which every call decreases is accepted, as it should
be.  The binding form must give the same verdicts (test V4).  Experiment E7
checks the liar `ghost predicate P(n, m) { m == F(n) + 1 }`,
`F(n) { Ex(n); var m :| P(n, m); m }`: rejected by termination.

### 4.3 Why the assumption is CanCall, not the precondition

A simpler encoding would state the axiom once, for all arguments:
`forall G :: Pre_L(G) ==> Post_L(G, L#bind#y(G))`.  That is unsound.  The axiom
would be a premise of `L`'s own verification, and its truth is `L`'s
correctness.  Experiment A3 writes the axiom for

```dafny
lemma Bad(n: nat) returns (m: nat) ensures Q(n) && m == 0 { m := 0; var _ := Sk(n); }
```

as the postcondition of a bodyless function `Sk`.  `Bad` then verifies, though
its body proves nothing about `Q`, and `false` follows from `Bad(20)` with
`Q(n) == (n < 10)`.  All the body needs is one mention of the choice term, which
the first revision's wording ("the body has nothing to prove") left out.  A
trigger keeps Z3 from using the axiom where the term does not occur, but a
soundness argument must not rest on triggers.

With the axiom guarded by `Bad#bind$canCall(n)`, the same `Bad` fails to verify:
the term is there, the guard is not.  The guard is assumed only after a checked
call, and a lemma that tries to get its own guard by calling a lemma that binds
it is rejected by termination (A3, third program).  In the model of 4.2 the
guarded axiom is simply true, so it can be a premise of any verification.

### 4.4 Determinism, and the class of lemmas

`F`'s value at its inputs is `e[y := ε(Sol_L(G_a))]`.  `G_a` is a function of
`F`'s inputs (and the heap, stage 2), and `Sol_L` depends only on `L`'s
specification and `G_a` (condition 3 of section 3.1).  So `F` is a deterministic
function of its inputs.  Its value does not depend on `L`'s body, which may be
nondeterministic (it may use `:|` statements, read the heap, or call other
lemmas whose results are not unique).

**No uniqueness is needed.**  Binding is sound for every lemma admitted by 3.1,
whether or not its postcondition determines its outputs.  Uniqueness changes
only what can be proved, never what is assumed.  Define the class precisely:

> `L` has **functional results** iff
> `forall G, y1, y2 :: Pre_L(G) && y1 ∈ Y && y2 ∈ Y && Post_L(G, y1) && Post_L(G, y2) ==> y1 == y2`.

For a lemma with functional results, the bound value equals the out-value of
every call statement `L(a)` at the same arguments.  The solver derives this from
the postcondition alone; the feature adds no axiom for it (experiment A1:
proved for `ensures m == n + 1`, not provable for `ensures m > n`).  For other
lemmas the bound value is some solution, and nothing relates it to a call
statement's result.

The feature must **not** assume, at a call statement, that the out-values equal
`L#bind#y(G)`.  A lemma body returns whichever solution it computes, and that
may depend on state that is not in `G`:

```dafny
lemma Two(c: C) returns (m: int) ensures m == 0 || m == 1 { m := if c.f == 0 then 0 else 1; }
method M(c: C) modifies c { var a := Two(c); c.f := c.f + 1; var b := Two(c); assert a == b; }
```

`Two` can be bound in stage 1: its postcondition uses no heap.  Its body reads
one.  The two calls in `M` return different values, and the assertion fails
today (experiment A4), as it must.  Equating a call statement's results with
the choice would prove it.

The class is not checked and not needed.  It is defined here because it is the
answer to "when does the bound value equal what the lemma returns?": exactly
when the lemma has functional results.

**Existential witnesses inside the postcondition.**  If `Post_L` contains
`exists k :: Q(y, k)`, the choice is over `y` only.  `k` stays existential in the
axiom, in a positive position, where Z3 skolemizes it.  To name `k`, the lemma
must return it.

### 4.5 Postconditions that use the heap (stage 2)

A lemma's postcondition is not framed: it may read any location.  Without a
check, binding such a lemma breaks `F`'s frame axiom.  The liar:

```dafny
class C { var f: int }
lemma Get(c: C) returns (x: int) ensures x == c.f { x := c.f; }
ghost function F(c: C): int { var x := Get(c); x }      // no reads clause
method M(c: C) modifies c { c.f := 0; var a := F(c); c.f := 1; assert a == F(c); assert false; }
```

`F` takes no heap, while `F(c) == c.f` in every heap (experiment A10 proves
`false` from the axiom form).

Stage 2 handles a postcondition that uses the heap exactly as a let-such-that's
constraint is handled today, and with the same argument:

- **The heap is an argument of the choice**, as it is of a let-such-that's
  Skolem function when its constraint uses the heap (fact 5).
- **Reads rule.**  For reads purposes, a binding reads what its arguments read,
  what its body reads, and what the instantiated postcondition reads *on the
  values the call returns*.  In `F`'s well-formedness check, the reads checks of
  `Post_L(a, y)` are made on the call's fresh out-values, after the
  postcondition is assumed.  This is the check a let-such-that gets (fact 8,
  experiment A9).  Other well-formedness conditions of `Post_L` were checked in
  `L` and are not repeated.  A binding nested in `Post_L` is covered by the same
  rule, applied to it.
- **Why a check on solutions suffices.**  Let `h1` and `h2` agree on `F`'s
  frame, with `F`'s precondition true (it is framed, so in both).  Take
  `y ∈ Sol_L(h1, G_a)`.  The evaluation of `Post_L(h1, a, y)` yields true and,
  by the check, reads only locations in the frame.  `Post_L` does not depend on
  allocation (condition 3).  So the same evaluation in `h2` reads the same
  values and yields true: `y ∈ Sol_L(h2, G_a)`.  The argument is symmetric, so
  the two solution sets are equal, `ε` picks the same value, and `F`'s frame
  axiom holds.  A value that is not a solution may read anything; it is in
  neither set.  This is the argument that justifies the frame axiom of any
  function whose body holds a let-such-that today.
- **A function without a `reads` clause** is axiomatized in the one fixed heap
  (fact 8).  The check above, with an empty frame, shows that the solution set
  is the same in every heap, so the non-emptiness proved in the checked heap
  holds in the fixed one.  `AST/FreeVariablesUtil.cs` must report a binding's
  use of the heap for this to happen.
- **Under `old(...)`** the call is checked, and `L#bind$canCall` assumed, for
  the heap the translation names (fact 10).  The binding must go through the
  same statement path.
- **One choice function shared by functions with different frames.**  Each
  function's frame axiom constrains only heaps that agree on its own frame, and
  for those the solution sets are equal.  So the functions do not interfere.

The model of 4.2 changes in one place: `L#bind$canCall(h, G)` holds where
`Sol_L(h, G)` is not empty and `Post_L` is well-formed there, which a checked
call in `h` establishes and the reads rule carries to the heaps that agree with
`h` on the frame.  Stage 1 needs none of this section: its choice functions
take no heap.

### 4.6 Why the postcondition may not depend on allocation

This is the hole the adversarial pass found in the first revision, whose stage 1
admitted any postcondition that reads no heap.

```dafny
class C { const id: int  constructor (k: int) ensures id == k { id := k; } }
lemma NoneYet(k: int) returns (b: bool) ensures b <==> forall o: C :: o.id != k
{ b := forall o: C :: o.id != k; }
```

`NoneYet` verifies.  Its postcondition reads no field: `id` is a constant.  But
the quantifier ranges over the allocated objects, so two calls, before and after
`new C(k)`, return `true` and `false`, and Dafny proves that (experiment A6).  A
heap-free choice `B(k)` with this postcondition says both `B(k)` and `!B(k)`:
the axiom form proves `false`.

Reading the postcondition in the binder as a function would read it (all
objects, allocated or not) is no repair: the lemma proved the allocated reading,
and the two differ.  Taking the heap as an argument (stage 2) is no repair
either: a function's frame axiom compares heaps that differ in allocation, and
no `reads` clause can name the allocation state.

The mechanism this plan borrows has the gap today.  A let-such-that in a method,
with a constraint that quantifies over the allocated objects and reads no field,
gets a choice function without a heap argument: experiment A16 reports an
internal error on one release of the fork and a proof of `false` on the next
(https://github.com/erniecohen/dafny/issues/431).  The fix proposed in
https://github.com/erniecohen/dafny/pull/448 makes the computation report such
a quantifier, a comprehension of the same kind, and a sequence constructor whose
initializer may read the heap.  Until that is merged, the binding's "uses the
heap" must not be that computation alone, in either stage; after it, condition 3
is still needed, for the reason above: a function may not depend on the
allocation state, with or without a heap argument.

Hence condition 3 of section 3.1: the postcondition must already be an expression
that a function may contain.  The same quantifier in a function is a resolution
error today, and a quantifier with a bound that is independent of allocation
(`forall o: C :: o in s ==> ...`) is accepted in both places (experiment A6).
The rule is checked on the lemma's declared postcondition, where a quantifier
over a type parameter needs that parameter to be `(!new)`, as in a function.

## 5. Alternatives considered

### 5.1 Expose the value the lemma's body computes

Rejected.  A lemma's body is not a function: it may be nondeterministic
(section 4.4), and its callers depend only on its specification.  Exposing it
would make every binding depend on proof details and break the abstraction that
lemmas provide.

### 5.2 The issue's suggestion: desugar to `L(a); var y :| Post_L(a, y); e`

This is close to the plan, and it is sound for the reasons in section 4.  The
plan departs from it in three ways:

1. **One choice per lemma, not per occurrence.**  The desugaring gives each
   occurrence its own Skolem function (fact 5), so two functions that bind
   `L(n)` cannot be proved equal, nor can two bindings in one function
   (experiments E5 and A2), and a fact about a binding inside a lambda cannot be
   recovered by the caller.  The issue's own use is `seq(n, i => ...)`: with one
   choice per lemma, the caller can write the same binding and the terms match
   (experiment R1, `Seq`, with the named function playing the per-lemma choice).
   Experiment B3 shows that an inline `:|` in the lambda body is not
   recoverable: `S1(n)[0] > 0` fails.
2. **No existence proof.**  The desugaring asks the solver to prove
   `exists y :: Post_L(a, y)` by guessing, which is where the issue's trigger
   failure came from.  The call already supplies the witness.
3. **No copy of the postcondition into the caller.**  The postcondition's
   well-formedness was checked in `L`.  Re-checking it in every caller costs
   resource units and adds failure points.  Only the reads checks (stage 2) are
   repeated.

The desugaring would also inherit condition 3 for free, because the copied
postcondition would be resolved as part of a function.  The plan has to state
it.

### 5.3 Restrict to lemmas with functional results

Rejected as unnecessary (section 4.4).  It would also need a uniqueness proof
at each lemma, which is the obligation that compiled let-such-that has today
and which ghost code does not need.

### 5.4 Key the choice by fewer arguments

A let-such-that's Skolem function takes only the free variables of its
constraint.  The same could be done here: only the in-parameters that `Post_L`
mentions.  Two calls that differ in a parameter used only by the precondition or
the proof would then bind the same value.  It is sound (the solution set does
not depend on the dropped arguments).  The plan keeps all in-parameters: "the
same call gives the same result" is the rule a user can predict, and moving
later from all arguments to fewer only adds equalities, so it cannot break a
proof, while the opposite move could.  Open question 4.

## 6. Interactions

### 6.1 Termination, and `--modular-termination` (issues 245 and 266)

- **Termination is the call's.**  The binding is checked as the call statement:
  the edge `F → L` (fact 4), the decreases check when `F` and `L` share a
  component (`TrCall.cs:321-337`), and a lemma's specification adds edges to the
  functions it mentions (experiments E2 and A5).  No new edge is needed for the
  new definitional dependency of `F` on `Post_L`, because `F → L → (callees of
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

- **The value does not depend on what is revealed.**  The bound value is the
  term `L#bind#y(G_a)`, which does not contain `Post_L`.  What is revealed
  decides only what can be proved about that term.  In the model, an opaque
  function has its real meaning whether or not it is revealed: every application
  passes the same global reveal constant
  (`ExpressionTranslator.cs:1133`), the definition axiom is guarded by it
  (`Functions.cs:340-341`), and `reveal` assumes it (`Methods.cs:1990`).  So
  `Post_L` in the axiom of 4.1 is one formula with one meaning, and `Sol_L` is
  one set.
- **A postcondition that mentions an opaque function** reaches the binder and
  its callers as stated.  With `opaque predicate Q` and `ensures Q(n, m)`:
  `Q(n, F(n))` is provable without a `reveal`, `F(n) > n` only after
  `reveal Q()`, and a function that makes the binding under `reveal Q();`
  has the same value as one that does not (experiment A12).
- **A `reveal` before a binding** affects only the well-formedness proof.  Its
  side effect on the axiom (`Functions.cs:576-594`) is unchanged, and a binding
  adds none.
- **An opaque function that binds a lemma** hides `Post_L(a, L#bind#y(G_a))` with
  its definition, because the fact reaches callers only through the definition's
  CanCall facts.  `reveal F()` brings it back.  Two opaque functions that bind
  the same lemma are equal only once both are revealed (experiment A12).
- **`hide *`** hides the binder's definition as any function's.  The choice's
  own axiom is emitted as a let-such-that's is, which `hide *` does not hide:
  in the reference form, revealing the functions involved is enough to recover
  the fact (experiment A12).
- **`--default-function-opacity opaque`** behaves the same (experiment A12).

### 6.3 Member invariants ([draft PR 384](https://github.com/erniecohen/dafny/pull/384))

- **Out-parameter types with an invariant.**  `y ∈ Y` includes `Y`'s invariant,
  so the chosen value satisfies it.  Existence holds because `L`'s verification
  checked the conversion to `Y`.  A `raw T` out-parameter carries no invariant
  and nothing changes, except in the cone of `T`'s invariant, where an
  out-parameter of any `T`-dependent type, `raw T` included, is rejected
  (item 1 below).
- **The invariant itself cannot bind**: PR 384 parses it with `allowLemma: false`.
- **The cone.**  PR 384's extension condition (its section 3.3) ranges over the
  *cone* of `T`'s invariant: invariants and the bodies and specifications of the
  functions they reach.  A lemma *call* in a function of the cone does not
  change the function's value (fact 3), but it is a proof, which is in the cone
  since issue 549 (item 3).  A *binding* also makes `Post_L` part of the
  function's value.  So with both features on:
  1. a binding's out-parameters are bound variables, and clause 1 applies to
     them: an out-parameter of a `T`-dependent type in the cone is rejected,
     like `:|` over `T` without a collection bound;
  2. the cone includes the `ensures` of every lemma bound in it;
  3. **settled: the proofs are in the cone too.**  A binding's axiom is
     justified by two proofs: the body of the lemma, which shows that results
     exist, and the well-formedness proof of the function that makes the
     binding, which shows the precondition.  A proof that learns that some
     value is in `T` learns `T`'s invariant, and with it the axioms of the
     bindings in its cone, so either proof could assume what it is meant to
     justify.  The termination check does not see this, because no call goes
     back to the lemma.  The `ensures` alone do not suffice: with `T`-free
     specifications, each of these proves `false` once item 2 is in place
     (issue 522, [PR 527](https://github.com/erniecohen/dafny/pull/527)):
     the bound lemma's body tests a raw value against `T`, or calls a lemma
     that does; a proof hint in a function of the cone establishes the
     binding's precondition with a lemma that does; the body reads a field or
     an array element of type `T` from the heap; the binding's receiver is a
     raw value of `T` and the lemma is an instance lemma of `T`; or the body
     chooses a value of `T` with `:|`, where `T` has a `ghost witness`.  So,
     when the cone contains a binding, it also contains the bodies of the
     bound lemmas, the statements inside the expressions of the cone, and the
     specifications and bodies of the lemmas these call.  In them the clauses
     of the extension condition apply, and also: a local variable of a
     `T`-dependent type has an initial value; a `:|` or `forall` statement
     over such a type has a collection bound; no lemma has an out-parameter
     of such a type; and no location of such a type is read from the heap.
     Of these, the uninitialized local and the `forall` statement have no
     known proof of `false`; they are rejected so that a proof in the cone
     follows the rules of a function there.  The tests are
     `git-issues/git-issue-199-invariant-proofs.dfy` and its controls.
     Issue 549 found that a binding is not needed: a proof hint in a function
     of the cone justifies that function's postcondition, termination and
     callee preconditions in the same circular way.  So the proofs above are
     in the cone whether or not it has a binding, and a dynamically bound
     lemma may not be called from them (as clause 4 rejects a dynamically
     bound function).  See section 10 of
     docs/dev/issue105-member-invariants-plan.md and
     `git-issues/git-issue-549-invariant-hints.dfy`.
- **Reads.**  PR 384 requires an empty frame in the cone.  A stage-2 binding of
  a postcondition that reads a location fails the reads check there, as it should.

## 7. Implementation stages and tests

### Option

`--lemma-bindings`, default false, settable in `dfyconfig.toml`.  Requires
`--type-system-refresh`; the legacy resolver keeps today's error.  With the option
off, the error text and all output are unchanged.  With it on and no binding in
the program, no declaration is emitted, so the Boogie is byte-identical; the
pull request shows this on the suite and the standard library (verdicts and
resource counts).  The error without the option gains a hint: "use
--lemma-bindings to bind a lemma's results".

The option gates the resolver only.  A program built without it may use a
library built with it, whose functions contain bindings, so the translator
handles a binding whatever the option says.

### Stage 1: heap-free postconditions

- **Resolver** (`PreTypeResolve.Expressions.cs`, the let case): when the option is
  on and a let's single right-hand side resolves to a lemma call, build a
  `LemmaBindingExpr` (a resolved form of `LetExpr`, keeping the `CallStmt` for
  the analyses).  Checks: count of left-hand sides; the three conditions of
  section 3.1.  Condition 3 reuses the function-context checks of fact 9 on the
  lemma's `ensures` clauses, with the lemma's out-parameters in scope; it is not
  the "uses the heap" computation of `FreeVariablesUtil`, which stage 1 applies
  in addition.  The translator asserts condition 3 when it builds the choice
  axiom, so that a binding that reaches it by another route (a library, a
  rewriter) fails closed.  Ghost inference for the bound variables.  Call-graph edge, and
  the `--modular-termination` walk, through the kept `CallStmt`.
- **Translator:** the declarations and axiom of 4.1, created on first use and
  shared; `TrExpr`, `CanCallAssumption` and well-formedness as in 4.1.
  Substitution and cloning must keep the lemma, the receiver and the arguments.
  The export-consistency checks must visit the binding.
- **The CanCall audit** of section 4.2.
- **Triggers.**  The trigger collector must treat a binding as the function
  application it stands for, `L#bind#y(G_a)`.  Otherwise a quantifier whose only
  candidate term is a binding (`forall n :: (var m := L(n); m) > n`) gets no
  trigger, where the reference form `forall n :: L#Result(n) > n` gets one
  (experiment A15).
- **Compilers:** erase the binding.
- **Docs:** Reference Manual section on expressions; `--help` text; news fragment
  `docs/dev/news/199.feat`.

### Stage 2: postconditions that use the heap

The heap in `G`; the reads checks of 4.5 on the call's fresh out-values;
`FreeVariablesUtil` reporting the heap use of a binding; the statement path
under `old(...)`.

### Tests

Regression `git-issues/github-issue-199.dfy`, run with the option and, for the
off case, without it.  Each "must fail" test has an `.expect` line.

**Accepted (positive):**

- P1: the reproducer; `Above(n) > n` proved by a caller.
- P2: two outputs with a relating postcondition (`a + b == 10`); the relation
  holds of the bound pair.
- P3: repeated use: `A(n) == B(n)` for two functions that bind `L(n)`; the same
  binding twice in one function, at `n` and at `n + 0`, with a body whose
  well-formedness needs the two to be equal (`10 / (1 + a - b)`).
- P4: the issue's use: `seq(n, i requires 0 <= i => var m := L(i); m)`, and a
  caller that proves `S(n)[0] > 0` by writing the same binding.
- P5: bindings under `forall` (including one that is the quantifier's only
  trigger candidate), in a lambda, nested bindings, a binding in a function's
  own `ensures`, a binding in a lemma's postcondition that is itself bound.
- P6: generic lemma at a value type and at a `(!new)` type parameter; a result
  of type `nat -> nat`; instance lemma with a receiver; trait lemma through
  dispatch; a lemma with a precondition; a bodyless `{:axiom}` lemma.
- P7: a binding in a compiled function, used only in ghost positions; the
  program compiles and runs.
- P8: a lemma whose results are not functional (`ensures m > n`) and one whose
  results are (`ensures m == n + 1`): only the second proves `F(n) == n + 1`,
  and only for the second does a call statement's result equal the bound value.
- P9: opaque: `F(n) > n` fails before `reveal F()` and holds after; a
  postcondition with an opaque predicate, as in 6.2; `hide *`; the same under
  `--default-function-opacity opaque`.
- P10 (stage 2): `ghost function F(c: C): int reads c { var x := Get(c); x }`
  accepted, and `F(c) == c.f` proved; a postcondition that reads a location only
  for values that are not solutions, bound in a function without `reads`; a
  binding under `old(...)` in a method.
- P11: mutual recursion in which every call decreases (A5, last row, in binding
  form).

**Rejected (negative), and vacuity controls:**

- V1: in every positive file, a function with a binding followed by
  `assert false` in a caller must fail.
- V2: `NotBody`: `Above(n) == n + 1` must fail for `ensures m > n`.
- V3: the lemma `Bad` of 4.3, whose body mentions a binding of `Bad` itself at
  the same argument, is rejected by termination; a lemma whose body mentions the
  choice only through a second function that binds it is rejected the same way.
- V4: the cycle, in binding form, in each of the contexts of experiment A5; each
  must give the verdict the call form gives today.
- V5: precondition violated at the binding.
- V6: stage 1 rejects a postcondition that uses the heap, with a message naming
  stage 2's condition; stage 2 rejects the liar of 4.5 ("insufficient reads
  clause"), and its `assert false` must fail when the reads clause is added.
- V7: rejected forms: a method call, a two-state lemma, a reference out type, a
  `~>` out type, a generic lemma instantiated with a class type, a pattern on
  the left, two right-hand sides, `:-`, a binding in `decreases` or `reads`.
- V8: `--modular-termination`: a binding of an imported lemma gets the same
  classification and hint as the call form; a `{:termination_closed}` function
  that binds a lemma reachable to open dispatch is rejected as the call form is.
- V9: option off: the reproducer gives today's error, byte for byte.
- V10: condition 3: `NoneYet` of 4.6 cannot be bound; nor can a lemma whose
  postcondition uses `allocated`, or a quantifier over an unconstrained type
  parameter; a lemma whose quantifier is bounded by a set can.
- V11: statement-level determinism: with the option on and `Two` of 4.4 bound
  somewhere, `assert a == b` after two call statements still fails.
- V12: `Q(n)` is not provable at an argument where no binding was checked, even
  with the choice term written out through a function that binds the lemma
  under a false path condition.
- With PR 384 (when both exist): an out-parameter of the invariant's own type in
  the cone is rejected (clause 1).

## 8. Out of scope

- **Two-state lemmas**, bound in two-state functions.  The choice would take both
  heaps, and the frame argument of 4.5 would need the two-state frame.  Not hard,
  but not needed by the issue.
- **Results that may show references** (section 3.4).  This needs an argument
  about allocation, which functions cannot read.
- **Postconditions that depend on allocation** (section 4.6): never.
- **Statement-level determinism** (4.4): never.

## 9. Experiments

Each program is a few lines; the tests of section 7 restate them.  "Reference
form" is the hidden function of section 3.3 written by hand.  "Axiom form" is a
bodyless function or predicate standing for the choice, with a bodyless lemma
standing for its axiom.  Verdicts:

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
| R1 | the reference form | `A(n) == B(n)`, `A(n) > n` and the `seq` property verify; `A(n) == n + 1` and `false` fail |
| M1 | `--modular-termination --show-hints`, lemma call in a client function | classified "same-bound rule", as any call |
| A1 | reference form, a lemma with `ensures P(n, m)` (not unique): bound twice in one function at `n` and `n + 0`, and in two functions | `Twice(n) == 0`, the well-formedness of `10 / (1 + a - b)`, and `A(n) == B(n)` verify.  Statement result against statement result, statement result against bound value, `A(n) == n + 1`, and `false` fail.  For `ensures m == n + 1` the statement result equals the bound value |
| A2 | the per-occurrence form: the same `:|` twice in one function, and in two functions | `Twice(n) == 0` and `A(n) == B(n)` fail; `10 / (1 + a - b)`: "possible division by zero" |
| A3 | axiom form of the precondition-guarded axiom, for `Bad` of 4.3 | `Bad` verifies and `false` follows.  With the axiom guarded by a token predicate, `Bad` fails.  A lemma that calls the token's site lemma on itself: termination errors |
| A4 | `Two` of 4.4, called twice around `c.f := c.f + 1`; a lemma with body `m :| m > n`, called twice | both lemmas verify; `assert a == b` fails in both |
| A5 | the cycle between a function and a lemma it calls, in each context | "cannot prove termination": lemma's `requires`; lemma's `ensures`, with and without a body; a lemma's own `ensures`; the function's own `ensures`; a lambda in the function body; a default parameter value; a third lemma's `requires`.  "const definition contains a cycle": a constant's initializer, with and without a lemma body; a constant of function type.  "recursive constraint dependency": a subset type's constraint.  With every call decreasing: verifies |
| A6 | allocation: `NoneYet` of 4.6 | the lemma verifies, and a method proves `b0` before `new C(k)` and `!b1` after.  The same quantifier in a function: resolution error.  The axiom form of a heap-free choice proves `false`.  `allocated(c)` in a lemma's postcondition and `old(c.f)` in a two-state lemma's: accepted; each, and `fresh` and `unchanged`, rejected in a function.  A quantifier bounded by `o in s`: accepted in both |
| A7 | reference results | for a bodyless `R(k: int): C`, `allocated(R(k))` and `old(allocated(R(k)))` verify; with `ensures R(k).id == k`, a method with `requires forall o: C :: o.id != k` proves `false`.  `var c: C :| c in s; c` in a function: accepted.  The reference form of a generic lemma, instantiated at a class type: accepted today |
| A8 | a `T(!new)` type parameter | accepted: `() -> int`, `int --> int`, a `() -> int` that captures an object.  Rejected: `() ~> int`, `C`, a datatype with a `C` field, `set<C>`, `C -> int` |
| A9 | `:|` constraints that read `c.f`, in a function without `reads` | rejected: `x == 0 \|\| (x == 1 && c.f == 7)`, `x == 1 ==> c.f == 7`, `(x == 1 && c.f == 7) \|\| x == 0`.  Accepted: `(x == 1 ==> c.f == 7) && x == 0`, `(x != 0 ==> c.f == 7) && x == 0`, `(x == 0 \|\| c.f == 7) && x == 0` |
| A10 | axiom form of the liar of 4.5 | `false` verifies |
| A11 | `old((Pos(c); ...))` with `lemma Pos(c) requires c.f > 0` | accepted when `c.f > 0` held in the old state only; "a precondition for this call could not be proved" when it holds in the new state only.  A postcondition `Q(c.f)` is obtained for the old value and not for the new |
| A12 | opaque and `reveal`, reference form | as stated in 6.2: with an opaque `Q` in the postcondition, `Q(n, F(n))` verifies, `F(n) > n` fails before `reveal Q()` and verifies after, `F(n) == G(n)` verifies for a binder under `reveal`, `false` fails.  Opaque binders: `F(n) > n` and `F(n) == G(n)` fail before `reveal` and verify after.  `hide *`: fails; with the three functions revealed: verifies.  `--default-function-opacity opaque`: fails without reveals, verifies with them |
| A13 | trust and lemma kinds | a bodyless lemma returning a value of an empty type: a method that calls it proves `false`; with a body: definite-assignment error.  A class that does not implement a trait's bodyless lemma: resolution error.  `decreases *` on a lemma: parse error.  `new` in a lemma: resolution error.  A ghost method in a statement expression: "calls are allowed only to lemmas" |
| A14 | `ensures Fact(n) ensures P(n, m)`, reference form | a caller of the binder proves `Fact(n)` when the binder's body uses the result, and not when it is `var _ := ...; 0` |
| A15 | `forall n: nat :: L#Result(n) > n`, reference form | verifies: the application is a trigger |
| A16 | today's let-such-that in a method, with a constraint that depends on allocation and reads no field: `var x: int :| x == 0 && forall o: C :: o.id != k; x`, then `new C(k)`, then the same with `exists o: C :: o.id == k` | on `v4.11.0+fcb2042d.review.bcc0c421`: internal error, "undeclared identifier: $heap".  On `v4.11.0+fcb2042d.review.4aa006a8`: `assert false` verifies, and the three controls (either let alone, neither) fail.  The choice functions take no heap.  With `allocated(c)` in the constraint, or a quantifier bounded by `o in s`: verifies, no error |

## 10. Open questions for the owner

1. **One choice per lemma or per occurrence?**  *Recommended: per lemma.*  It
   gives repeated-use equality, within a function and across functions, and
   facts under lambdas that a caller can recover by writing the same binding
   (5.2; experiments A1, A2, B3).  Per occurrence is what `:|` does today.  Both
   are sound.  Per lemma is what makes the 4.1 rule "check the body with the
   chosen value" necessary.
2. **The option's name, and requiring `--type-system-refresh`.**  *Recommended:
   `--lemma-bindings`, refreshed resolver only.*  The name says what the user
   writes.  The legacy resolver would need a second copy of the three conditions
   of 3.1, one of them a soundness condition, in code that is on its way out.
3. **With PR 384: must the bodies of bound lemmas be in the invariant's cone, or
   only their `ensures`?**  *Recommended: the bodies too, until someone proves
   the `ensures` suffice.*  The choice exists because the body's proof succeeded,
   and that proof may use the typing facts the cone defines.  Over-inclusion can
   only reject programs.  *Settled: the bodies, and every other proof that
   justifies a binding, must be in the cone; the `ensures` alone admit proofs
   of `false` (6.3, item 3).*
4. **Which arguments key the choice: all in-parameters, or only those the
   postcondition mentions?** (5.4)  *Recommended: all.*  Both are sound.  "The
   same call gives the same result" is predictable, and narrowing later only
   adds equalities.

## 11. What the adversarial pass changed

Each soundness claim of the first revision was attacked with a program, run with
the releases named at the top.  The outcome:

- **One real hole, now closed** (3.1 condition 3, 4.6, V10).  Stage 1 admitted
  any postcondition that reads no heap.  A postcondition can depend on the set
  of allocated objects without reading a field, and binding such a lemma proves
  `false` (A6).  Today's let-such-that has the same gap outside functions, where
  on the latest release it is a proof of `false` with no trusted construct
  (A16).  That is a defect of the fork as it stands, separate from this plan:
  https://github.com/erniecohen/dafny/issues/431, with a fix proposed in
  https://github.com/erniecohen/dafny/pull/448.
- **Result types** are judged by `(!new)`, that is by what a value may *show*,
  not by what it may involve: `~>` is out, `->` and `-->` over admissible types
  are in (fact 11, A8).  The reason for excluding references is restated with
  the program that shows it (3.4, A7); the old text's claim that `:|` cannot
  choose a reference at all was too strong.
- **The model of 4.2** was wrong in one definition (`canCall` as non-emptiness
  alone) and is now stated so that the axiom is true outright.  The invariant
  about `CanCall` that the design relies on is stated, with an audit as an
  implementation item.
- **4.3** claimed a lemma "has nothing to prove" under the precondition-guarded
  axiom; in fact its body must mention the choice term once.  The program is now
  in the plan (A3), and test V3 is corrected to match.
- **Body check** (4.1): the body is checked with the chosen value, unlike `:|`.
  The first revision did not say that this differs, or why it must (A1, A2).
- **Cycles** (4.2): tested in every expression context, not only function
  bodies (A5).  All are rejected today.
- **Statement-level determinism** (4.4) now has the program that shows why it
  would be wrong (A4).
- **Stage 2** (4.5) is restated as "what `:|` does today", with the argument
  made explicit: reads checked on solutions, the one fixed heap for functions
  without `reads`, and the old-state path (facts 8 and 10; A9, A10, A11).
- **Opaque and `reveal`** (6.2): the claim held under every attack (A12).  The
  argument is now the direct one: the bound term does not contain the
  postcondition.
- **Smaller additions:** callers learn the whole postcondition (3.4, A14); the
  choice is keyed by the resolved declaration (3.3); trust equals the call
  statement's (3.4, A13); the trigger collector must see a binding as an
  application (7, A15); the option gates the resolver only (7); a fourth owner
  question (5.4).

## 12. The implementation

Both stages are implemented, with the four recommendations of section 10.  The
tests are `git-issues/github-issue-199*.dfy`.  What differs from the text above,
or was left open by it:

- **The resolved form.**  There is no `LemmaBindingExpr`.  Resolution rewrites the
  let in place into

      var t1, ..., tn := receiver-and-arguments;
      var y1, ..., yk := (var z1, ..., zk := L(t1, ..., tn); R1), R2, ..., Rk;
      e

  where the parenthesized expression is a statement expression around the call
  statement, and each `Ri` is a `LemmaResultExpr` for one out-parameter at the
  `ti`.  So the call is checked by the code that checks every call statement, and
  everything that is assumed about the results comes from that statement
  (`LemmaBindingCanCall`), never from a result expression.  The let keeps the
  call that was written, for printing and for cloning before resolution.  A
  substitution changes the receiver and arguments of the rewritten form only, so
  the let it leaves is printed from the rewritten form, as the call at the new
  arguments (`Find(x + 1)` for a precondition `(var m := Find(n); m) > 5` at a
  call with `x + 1`).
- **A library needs the option in its client.**  Section 7 said that a program
  built without the option may use a library built with it.  It may not: the
  client resolves the library's source again, with its own value of the option,
  and without it the library's bindings are the errors they are in any program.
  The option is not recorded in a library, so a library built with the option off
  is the file it was before.  A library built without the option can be used by a
  client with it.
- **Placement** (3.1, V7).  A binding is allowed wherever a parenthesized lemma
  call already is, which includes `decreases`, `reads` and `requires` clauses.
  Test V7's "a binding in `decreases` or `reads`" is therefore an accepted case.
- **Condition 3 has a fourth clause.**  A postcondition that depends on the heap
  may not bind results of its own lemma, directly or through the postconditions of
  other lemmas.  The reads check of 4.5 re-checks the postcondition at the
  binding, and for such a postcondition that check would contain itself.  Whether a
  postcondition depends on the heap is the least fixpoint over the lemmas whose
  results it binds, so a recursive postcondition that reads nothing stays
  admissible.
- **Extreme and prefix lemmas** are excluded with two-state lemmas (condition 1).
  A least or greatest lemma cannot have out-parameters in any case.
- **The reads check of stage 2** is the full well-formedness check of the
  postcondition at the values the call returned, in the enclosing frame.  It
  repeats the other well-formedness conditions of the postcondition, which hold
  there, because the reads checks are not separable from them.
- **Triggers and printing.**  A result expression is a trigger candidate.  It is
  printed, in triggers, with `--rprint` and in hints, as an expression that binds
  the results and returns that one, `var y := L(args); y`, with `_` for the other
  results.  A user can write the same thing as a trigger, and printed programs
  resolve again.  A binding has a statement in it, the call.  So, as for any
  statement expression, a term that contains a binding, such as
  `P(var y := L(x); y)`, is not selected as a trigger; the result in it is.
- **`:-`** is not a binding.  `var y :- L(args); e` gets the errors it got before.
- **The hint** for the option-off error is "a lemma's results can be bound in an
  expression under --lemma-bindings", shown with `--show-hints`.  The error itself
  is unchanged.
- **The translator fails closed.**  It asks conditions 2 and 3 again.  For a lemma
  that does not meet them it declares the functions, emits no axiom and never
  assumes `L#bind$canCall`.  The resolver rejects such a binding first, so this is
  not reachable in a test.

Left for later: the out-of-scope items of section 8.  The cone clause of 6.3
is done: [PR 527](https://github.com/erniecohen/dafny/pull/527) follows the
specifications of bound lemmas (item 2), and the follow-up to it the
out-parameters and the proofs (items 1 and 3), the proofs whether or not the
cone has a binding (issue 549).

