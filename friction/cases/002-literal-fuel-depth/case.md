# 002: Structural recursion on a literal term stops after two levels

- **Status:** filed ([#197](https://github.com/erniecohen/dafny/issues/197))
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

## Reproducer

`repro.dfy`:

```dafny
datatype T = V | C(c: real) | Add(a: T, b: T)

function ev(t: T, x: real): real {
  match t
  case V => x
  case C(c) => c
  case Add(a,b) => ev(a,x)+ev(b,x)
}

function one(): real { 1.0 }

lemma LiteralDepth3(x: real) ensures ev(Add(V,Add(C(1.0),V)),x) == 2.0*x+1.0 {}

lemma CallDepth3(x: real) ensures ev(Add(V,Add(C(one()),V)),x) == 2.0*x+1.0 {}

lemma CallDepth3Hinted(x: real) ensures ev(Add(V,Add(C(one()),V)),x) == 2.0*x+1.0 {
  assert ev(Add(C(one()),V),x) == 1.0+x;
}
```

## Expected

All three verify. The term argument is a constructor literal, and the
recursion is structural on it.

## Observed

- `LiteralDepth3` fails (9,160 RU) and `CallDepth3` fails (9,973 RU), both with
  "a postcondition could not be proved on this return path".
- `CallDepth3Hinted` verifies (15,776 RU).
- Because `x` is not a literal, the literal fast path does not apply, and
  ordinary fuel (2) runs out at depth 3.

## Workaround

Assert the value of each subterm deeper than two levels (`workaround.dfy`).
Every polynomial identity whose terms nest three deep needed such hints.

## Suggested fix

When the argument a function matches on (or its `decreases` argument) is a
constructor literal, unfold that function regardless of the other arguments:
literal-guided unfolding per argument, rather than only when all arguments are
literals.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

```check
exit 4
failed LiteralDepth3
failed CallDepth3
verified CallDepth3Hinted
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: REPRODUCES, with the figures above. The workaround verifies.
