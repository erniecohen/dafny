# 004: Function bodies cannot bind the results of a lemma

- **Status:** open
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

## Reproducer

`repro.dfy`:

```dafny
lemma Find(n: nat) returns (m: nat) ensures m > n { m := n+1; }
ghost function Above(n: nat): nat { var m := Find(n); m }
```

## Expected

A ghost function can name a witness that a lemma computes.

## Observed

Resolution error: "expression is not allowed to invoke a lemma (Find)". A
lemma *call* is allowed in an expression (`L(n); e`); only binding its
out-parameters is not.

## Workaround

A second lemma that states existence, and a `:|` in the function
(`workaround.dfy`):

```dafny
ghost predicate Bigger(n: nat, m: nat) { m > n }
lemma FindExists(n: nat) ensures exists m: nat :: Bigger(n,m) {
  var m := Find(n); assert Bigger(n,m);
}
ghost function Above(n: nat): nat { FindExists(n); var m: nat :| Bigger(n,m); m }
```

The named predicate matters: written as `exists m: nat :: m > n`, the
quantifier has no trigger and `FindExists` itself fails. This was needed to
build bracket sequences with `seq(n, i => ...)`, whose elements come from an
approximation lemma.

## Suggested fix

In ghost expressions, desugar `var m := L(args); e` to the lemma call followed
by `var m :| <postcondition of L>; e`. This is sound because the postcondition
proves existence.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

```check
exit 2
message "expression is not allowed to invoke a lemma (Find)"
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: REPRODUCES, with the figures above. The workaround verifies.
