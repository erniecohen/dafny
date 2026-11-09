# `--statement-expression-conclusions`

Issue: [#96](https://github.com/erniecohen/dafny/issues/96) (upstream dafny-lang/dafny#1371).

## What 4.11.0 does

A statement expression `S; E` translates as `E`: `ExpressionTranslator.TrExpr` drops `S`.
Its well-formedness check runs `S`, so that `S` is asserted, or for `assume`, assumed, at the
point where `S; E` is evaluated. But the conclusion of `S` (`StmtExpr.GetStatementConclusion`:
the condition of an `assert`, `assume` or `expect`, the result of a `calc`, the conclusion of
`assert ... by`) reaches the formula only in one place. That is `TrSplitExpr`'s `StmtExpr`
case, which turns `S; E` into `C ==> E` when `S; E` is at the top of a split. Under a
quantifier, the split goes through `TrSplitUniversalCheck`, which calls `TrExpr`, and `C` is
lost. For

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

With `--statement-expression-conclusions` (default off), `TrSplitExpr` rewrites any expression
that contains a boolean `S; E` below its top before splitting it
(`StatementConclusionSubstituter`). The rewrite does not enter lambdas or set and map
comprehensions. Each such `S; E` becomes:

- `C ==> E` where it occurs positively, or where its polarity is unknown;
- `C && E` where it occurs negatively, that is, under an odd number of negations,
  implication antecedents and ranges of a `forall`.

The rewritten expression is then split as usual, and the split is always reported, so that a
caller does not fall back to `TrExpr` of the original. With the option off, `TrSplitExpr` takes
the same path as before, and the Boogie program is unchanged.

The issue's assertion then becomes
`(forall n#1: int :: LitInt(0) <= n#1 ==> (Lit(false) ==> Lit(false)))`.

## Why it is sound

The rewrite adds no axiom and no assumption. It replaces a subterm by one with the same value
wherever that value matters:

1. Every expression that `TrSplitExpr` translates (an assertion, a postcondition, an invariant,
   a precondition at a call, a calc step, a forall statement's postcondition, a subset-type
   witness, or a function body inlined at a call whose precondition was checked) is first
   checked for well-formedness in the same context. That check executes `S` at each
   evaluation point of `S; E`. It is the same fact that the existing top-level case relies on.
2. The check reaches that point under the same guards as the translation does: the bound
   variables' type and allocation antecedents and the range of each enclosing quantifier
   (`TrBoundVariables` with the same heap), the left operand of `&&`, `||` and `==>`, the test
   of an if-then-else, and the bindings of `let` and `match`. Where the translation leaves out an
   allocation antecedent because the bound variable's range implies allocation
   (`freeOfAlloc`), the check assumes allocation, which the range implies in that context.
3. So at every point where the value of `S; E` counts, `C` holds, and `E`, `C ==> E` and
   `C && E` are equal there. Where `S; E` is not evaluated (a guard is false), the enclosing
   operator does not depend on its value. The rewritten formula therefore has the same value
   as the original in every state that the well-formedness check covers. A checked split is
   neither weaker nor stronger than before, and a free split assumes the same fact.
4. A lambda's body is excluded because the encoding defines a lambda's application beyond its
   precondition, where `C` need not hold. Set and map comprehensions are excluded because their
   values are compared extensionally with other collections. Neither exclusion affects
   soundness: those expressions are translated as before.

The choice between `C ==> E` and `C && E` affects only what can be proved, never soundness,
since both equal `E` where `C` holds.

## Controls

`git-issues/github-issue-96.dfy` runs `Inputs/github-issue-96-positive.dfy` and
`Inputs/github-issue-96-negative.dfy` with the option off and on, and once through a project
file.

- Positive: the issue's program, an `assert ... by` under a quantifier, a guarded
  postcondition, and a negative-polarity case. All fail with the option off and verify with it
  on, under the harness Z3 and Z3 5.1.0.
- Negative, which must keep failing with the option on:
  - an inner `assert` that does not hold is still reported;
  - a body that is false besides the conclusion still fails;
  - `ensures false` after quantified conclusions;
  - `assert false` after a conclusion under a binder whose range implies allocation;
  - a guarded conclusion used outside its guard;
  - a lambda body.
