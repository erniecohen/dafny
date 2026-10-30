# 001: A universally quantified bound through a user function fails on sequence elements

- **Status:** filed ([#196](https://github.com/erniecohen/dafny/issues/196))
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

## Reproducer

`repro.dfy`:

```dafny
function abs(x: real): real { if x < 0.0 then -x else x }

lemma Scalar(x: real, d: real) requires d >= 0.0 ensures abs(x-x) <= d {}

lemma Indexed(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: abs(l[i]-l[i]) <= d
{}

lemma TwoSided(l: seq<real>, d: real)
  requires d >= 0.0
  ensures forall i | 0 <= i < |l| :: -d <= l[i]-l[i] <= d
{}
```

## Expected

All three lemmas verify.

## Observed

- `Scalar` verifies (2,981 RU) and `TwoSided` verifies (13,298 RU).
- `Indexed` fails: "a postcondition could not be proved on this return path"
  (14,666 RU).
- The same failure appeared for a predicate `near(s,s',d)` defined with `abs`,
  and also when a `forall` statement first proved exactly the needed body.

## Workaround

State such bounds two-sided, without `abs`, inside quantifiers
(`workaround.dfy`).

## Suggested fix

The definition axiom of `abs` does not appear to fire on the argument
`l[i]-l[i]`, which is built from unboxed sequence elements under a quantifier.
Check the trigger and the literal or fuel wrapping that the translator gives
function axioms, so that they match `abs($Unbox(Seq#Index(l,i)) - ...)`.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

Read by `friction/run`; the syntax is in `friction/README.md`.

```check
exit 4
message "a postcondition could not be proved on this return path"
verified Scalar
failed Indexed
verified TwoSided
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: REPRODUCES, with the figures above. The workaround verifies.
