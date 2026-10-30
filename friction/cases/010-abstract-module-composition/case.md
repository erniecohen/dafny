# 010: Abstract modules work for generic lemmas, but cost a module per composite shape

- **Status:** tip
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

Not a bug: the workaround is accepted and no fix is wanted. The reproducer is
the working pattern; the check confirms it still verifies cheaply.

## Reproducer

`repro.dfy`: a generic theorem stated once over an abstract operation,
instantiated twice, and a composition that needs two operations at once.

```dafny
abstract module Op {
  function f(a: real, b: real): real
  lemma monotone(a: real, b: real, c: real, d: real)
    requires a <= c && b <= d
    ensures f(a,b) <= f(c,d)
  lemma Generic(a: real, b: real) requires a <= b ensures f(a,a) <= f(b,b) { monotone(a,a,b,b); }
}
module Sum refines Op { ... }
module Max refines Op { ... }
abstract module Compose {
  import F : Op
  import G : Op
  lemma Composed(a: real, b: real) requires a <= b ensures F.f(G.f(a,a),a) <= F.f(G.f(b,b),b) { ... }
}
module SumOfMax refines Compose { import F = Sum  import G = Max }
```

## Expected / observed

- This verifies cheaply (2,639-4,006 RU per lemma), and inherited lemmas are
  not re-verified in refining modules.
- However, a field law relates up to five operation instances (for example
  x·(y+z) = x·y + x·z). Each composite shape needs its own parameterized
  module and its own refining instance, and the "same polynomial, same value"
  step compares functions from different module instances, which no single
  lemma can state.

## Workaround (accepted)

- For a few fixed operations: abstract modules as above. Generic lemmas over a
  first-order abstract function are much cheaper than over function values
  (case 006).
- For laws that relate many operation instances: a deep embedding (a `Term`
  datatype with an evaluator) expresses the shapes as data.

## Suggested fix

None wanted: this is a tip. For reference, first-class module functors
applied in expressions, or generic lemmas over first-order function
parameters (`lemma L<f: (real,real)->real>` with f treated as an
uninterpreted symbol rather than a function value), would make the shallow
form usable.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

```check
exit 0
verified Op.Generic
verified Compose.Composed
ru Compose.Composed <= 50000
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: TIP-HOLDS, with the figures above.
