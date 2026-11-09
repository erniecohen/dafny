# `--statement-expression-conclusions`

Issue: [#96](https://github.com/erniecohen/dafny/issues/96) (upstream dafny-lang/dafny#1371).
Pull request: [#226](https://github.com/erniecohen/dafny/pull/226).

## What 4.11.0 does

A statement expression `S; E` translates as `E`: `ExpressionTranslator.TrExpr` drops `S`.
Its well-formedness check runs `S`, so `S` is asserted (or, for `assume`, assumed) at the point
where `S; E` is evaluated. The conclusion of `S` is what `StmtExpr.GetStatementConclusion`
returns:

- the condition of an `assert`, `assume` or `expect`;
- the result of a `calc`;
- the conclusion of `assert ... by`.

That conclusion reaches the formula in only one place: `TrSplitExpr`'s `StmtExpr` case, which
turns `S; E` into `C ==> E` when `S; E` is at the top of a split. Under a quantifier, the split
goes through `TrSplitUniversalCheck`, which calls `TrExpr`, and `C` is lost. For

```dafny
assert (forall n: nat :: assume false; false);
```

`--bprint` shows

```boogie
havoc n#0;
if (LitInt(0) <= n#0)
{
    assume true;
    assume {:id "id0"} Lit(false);
}
...
assert {:id "id1"} (forall n#1: int :: LitInt(0) <= n#1 ==> Lit(false));
```

The assumption holds only inside the well-formedness block, and the assertion fails.

## What the option does

The option `--statement-expression-conclusions` is off by default. When it is on,
`TrAssertCondition` rewrites the condition of an `assert` statement before splitting it, with
`StatementConclusionSubstituter`, but only when all of these hold:

- the statement is translated with a translator whose heap is not an old heap
  (`!etran.UsesOldHeap`);
- the statement is not itself inside the statement of a statement expression
  (`statementExpressionDepth == 0`);
- the condition contains a Boolean `S; E`.

Each such `S; E` becomes:

- `C ==> E` where it occurs positively, or where its polarity is unknown;
- `C && E` where it occurs negatively, that is, under an odd number of negations,
  implication antecedents and ranges of a `forall`.

The rewriter does not enter the following expressions, which keep today's translation:

- `old`, `old@L`, `unchanged` and `fresh`;
- lambdas;
- set and map comprehensions;
- let-such-that expressions (`var x :| P; E`);
- binding-guard if-then-else expressions (`if x :| P then A else B`).

With the option off, `TrAssertCondition` takes the same path as before. The issue's assertion
becomes `(forall n#1: int :: LitInt(0) <= n#1 ==> (Lit(false) ==> Lit(false)))`.

Postconditions, invariants, preconditions at calls, calc steps and function bodies inlined in a
split are not rewritten.

## Why it is sound

The rewrite adds no axiom and no assumption. It replaces the checked condition by one with the
same value in every state where the check is made. The free assumption that follows an `assert`
(`TrAssertStmt` assumes `stmt.Expr`), and every later use of the same expression, keep the
original translation. Value equality is what makes it sound to mix the two. If the rewritten
condition were weaker anywhere, the check could pass while the assumption of the original adds
a false fact. That is how the counterexample in the review of #226 proved `ensures false`
(below).

The argument, for an assert statement `assert P` in the current heap:

1. **Same state.** `TrAssertStmt` calls `TrStmt_CheckWellformed(stmt.Expr, b, locals, etran)`,
   then `TrAssertCondition(stmt, etran, proofBuilder)`. `proofBuilder` is `b`, or, for a labeled
   assertion, a path-aside block that starts after those commands. So the check of the rewritten
   condition is made in the state right after the well-formedness check of `P`, with the same
   translator.
2. **The check establishes C where S; E is evaluated.** The well-formedness check of `P` runs
   `S` at each evaluation point of `S; E`, via `CheckWellformedStmtExpr` and `TrStmt`. For
   `assert`, `calc` and `assert ... by`, that asserts `C`. For `assume` it assumes `C`, which is
   the assumption the program states.
3. **The guards agree.** The rewriter descends only through constructs whose translation reaches
   the subexpression under the same guards as the well-formedness check:
   - `!`, `&&`, `||`, `==>` and `<==`: the check evaluates the right operand under the left (or
     its negation), and the translation is the same Boolean connective;
   - if-then-else: the check evaluates each branch under the test, as the translation does;
   - exact let: the check binds the variable to the right-hand side, and the translation
     substitutes or binds the same value;
   - match: the check splits on the constructor, and the translation's if-then-else on the
     constructor tests has the same cases;
   - `forall` and `exists` in the current heap. The check havocs the bound variables, assumes
     their types and `$IsAlloc` in the translator's heap (`SetupBoundVarsAsLocals` with
     `ISALLOC`), and evaluates the body under the range. The translation (`TrBoundVariables`)
     has the same type and range antecedents, and the same `$IsAlloc` antecedent unless the
     range makes it unnecessary (`freeOfAlloc`: `HasBounds(..., IndependentOfAlloc_or_ExplicitAlloc)`).
     The range then implies allocation in the heap where the bound collection was allocated. In
     the current heap, every value in scope is allocated: parameters, locals, bound variables of
     enclosing current-heap quantifiers, function results, and the values of `old` expressions
     (allocation is monotone). So the range implies `$IsAlloc` in the current heap, and the
     antecedents agree.
4. **Hence equal values.** Wherever the value of `S; E` counts, `C` holds, so `E`, `C ==> E`
   and `C && E` are equal there. Where `S; E` is not evaluated, because a guard is false, the
   enclosing operator does not depend on its value.

The choice between `C ==> E` and `C && E` affects only what can be proved, never soundness,
since both equal `E` where `C` holds.

## What is excluded, and why

**`old`, `old@L`, `unchanged`, `fresh`** (the counterexample in the review of #226). Inside
`old(...)`, a quantifier's well-formedness check assumes its bound variables allocated in the
old heap. Its translation leaves the antecedent out when the range is `o in s`. That is
justified for the current heap only: `s` can hold objects allocated since. The rewritten
condition was therefore weaker than the original:

```dafny
class C { }
method Bad() ensures false {
  var c := new C;
  var s: set<object> := {c};
  assert old(forall o | o in s :: (assert allocated(o); allocated(o)));
}
```

The rewritten check `forall o :: o in s ==> (old-allocated(o) ==> old-allocated(o))` passed.
The assumption of the original, `forall o :: o in s ==> old-allocated(o)`, is false for `c`,
and `ensures false` verified. Now the rewriter does not enter these expressions. The top-level
check also requires a translator that is not on an old heap, which excludes an assert statement
whose translator is an old one.

**Assert statements inside a statement expression.** `CheckWellformedStmtExpr` translates the
statement of a statement expression under `old` with the old translator. Statements that work
on the global `$Heap` see it set to the old heap (`BuildWithHeapAs`), so the heap that a
translator names is not a reliable test. Such assert statements are not rewritten, whatever
the heap.

**Lambdas.** A lambda's `Apply` is defined beyond its precondition, where `C` need not hold.

**Set and map comprehensions.** Their values are collections compared extensionally with other
collections, and their translation (`BuildSetComprehensionMap` and the map witnesses) has its
own antecedents.

**Let-such-that and binding-guard if-then-else.** The check and the translation choose the
witness differently. The argument above does not cover them, so they are left out.

**Postconditions, invariants, preconditions, calc steps, inlined function bodies.** Their
well-formedness check runs in another procedure or another state:
- the postcondition's in the well-formedness procedure, on an arbitrary post-state;
- the invariant's at the loop head, on an arbitrary iteration;
- the callee's precondition in the callee;
- an inlined function body in the function's well-formedness procedure, under its precondition
  and reads frame.

Covering them needs an argument per context, and the first version showed how easy one is to
get wrong. Only assert statements are rewritten.

## Controls

`git-issues/github-issue-96.dfy` runs `Inputs/github-issue-96-positive.dfy` and
`Inputs/github-issue-96-negative.dfy`, with the option off and on, and once through a project
file. The CI job `Statement-expression conclusions (opt-in, #96)`
(`.github/review/statement-conclusions.sh`) runs the same commands on the built binary with
Z3 5.1.0 and compares the transcript with `github-issue-96.dfy.expect`.

- **Positive** (all fail with the option off and verify with it on):
  - the issue's program;
  - an `assert ... by` under a quantifier;
  - the same under a guard;
  - a negative-polarity case.
- **Negative:** with the option on, every verdict is the one it has with the option off, except
  the first assertion of `VacuityPost`. That assertion holds once its conclusion is usable;
  the lemma's `ensures false` still fails. The negative file contains:
  - an inner `assert` that fails;
  - a body that is false besides the conclusion;
  - `ensures false` after quantified conclusions;
  - `assert false` after a conclusion under a binder without an allocation antecedent, in the
    current heap;
  - a guarded conclusion used outside its guard;
  - a lambda body;
  - a postcondition, which is not rewritten;
  - the review's counterexample and its variants, each with `ensures false` and `assert false`:
    `old`, `old@L`, `old` of `s0 + {c}`, a two-state lemma, a two-state predicate with a `new`
    parameter, `fresh`, `unchanged`, and an assert statement inside a statement expression
    under `old`.
