# 006: Generic lemmas over function values: an unreachable conjunct, and the cost of Apply

- **Status:** open
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

## Reproducer

`repro.dfy` states one box lemma three ways. Over a function value
`f: (real,real) -> real`, the hypothesis is that the minimum `lo` and maximum
`hi` of f over a box's corners bound f on the box:

```dafny
ghost predicate cornered(f: (real,real) -> real) {
  forall a1, b1, a2, b2, t1, t2 {:trigger lo(f,a1,b1,a2,b2), f(t1,t2)}
    | a1 <= t1 <= b1 && a2 <= t2 <= b2 :: lo(f,a1,b1,a2,b2) <= f(t1,t2) <= hi(f,a1,b1,a2,b2)
}
```

`Common` uses it at two boxes through a common point. The modules are:

1. `OneTrigger`: the predicate above, plus `Sum` and `Product`, which prove
   `cornered` for `(a,b) => a+b` and `(a,b) => a*b`.
2. `TwoTriggers`: one quantifier per bound, each with its own trigger.
3. `Box`: first order, an abstract module with `function f(a: real, b: real): real`
   and an instantiation lemma.

## Expected

- `OneTrigger.Common` verifies, or Dafny warns that the trigger cannot reach
  the `hi` conjunct.
- The function-value forms cost about as much as the first-order form.

## Observed

| Formulation | `Common` |
|---|---|
| `OneTrigger` (one quantifier, trigger on `lo`) | fails, 44,850 RU |
| `TwoTriggers` (one quantifier per bound) | verifies, 60,577 RU |
| `Box` (first order) | verifies, 9,472 RU |

- In `OneTrigger.Common` the `hi` half is never instantiated, because nothing
  that mentions `lo(f,a1,...)` exists for the second box. Dafny accepts the
  explicit trigger without a warning that one conjunct of the body can never
  be reached through it.
- `OneTrigger.Sum` verifies (47,943 RU). `OneTrigger.Product` fails
  (708,859 RU); the product case needs a hand-written segment argument.
- In a larger development, the function-value formulation did not scale:
  - generic boundedness of a generated object ran out of resource;
  - with the hypothesis opaque and an explicit instantiation lemma it passed
    at 2.16M RU;
  - the lifting function's own well-formedness still ran out of resource;
  - the first-order formulation of the same development peaks at 3.0M RU.

## Workaround

The first-order form (module `Box` in `repro.dfy`): an abstract module with a
first-order function and an instantiation lemma, refined per operation. Where
function values cannot be avoided, one quantifier per conjunct, each with its
own trigger.

## Suggested fix

- Warn when an explicit trigger cannot match some function application that is
  needed to use one conjunct of the body.
- Optionally, split conjunctive quantifier bodies automatically, each part with
  its own trigger.
- Look at the Apply/Box encoding cost for two-argument function values: the
  same lemma costs over six times its first-order form.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

```check
exit 4
failed OneTrigger.Common
verified TwoTriggers.Common
verified Box.Common
ratio TwoTriggers.Common Box.Common >= 3
verified OneTrigger.Sum
failed OneTrigger.Product
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: REPRODUCES, with the figures above.
