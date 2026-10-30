# 007: An existential under a universal in a subset-type invariant is a silent matching loop

- **Status:** open
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

## Reproducer

`repro.dfy`:

```dafny
datatype Cut = Cut(below: iset<int>) {
  ghost predicate valid() {
    (exists q :: q in below) && forall q | q in below :: exists p :: p in below && q < p
  }
}
type C = c: Cut | c.valid() witness *

lemma Larger(x: C, q: int) returns (p: int)
  requires q in x.below
  ensures p in x.below && q < p
{ p :| p in x.below && q < p; }

lemma Unrelated(x: C, y: C) ensures x.below == y.below ==> x == y {}
```

## Expected

Dafny warns that the invariant's quantifier forms a matching loop, as it does
for ordinary loops.

## Observed

- No warning. In this minimal form everything verifies cheaply (`Larger`
  40,461 RU, `Unrelated` 10,196 RU).
- In a development where the invariant hangs off every value of the type and
  many membership terms are present, 36 of 48 declarations ran out of
  resource, including trivial ones such as commutativity of addition.
- Each membership term `q in below` produces a Skolem witness p, whose
  membership `p in below` fires the quantifier again.
- A false goal next to the invariant in this small file (for example
  `requires q in x.below ensures q < 0`) still fails quickly, so the blow-up
  needs the larger context. What the check below tracks is the missing
  warning.

## Workaround

Make the open-set part an opaque predicate, plus one lemma that reveals it and
returns the witness (`workaround.dfy`). Witness functions instead of the
existential (`bigger(q)`, with the trigger on `bigger(q)`) also work, but need
opaque bodies and cost more (in the development, the maximum RU rose from 0.99M
to 2.1M).

## Suggested fix

Extend matching-loop detection to existentials under universals: the Skolem
term of the inner existential re-matches the outer trigger. Warn, as for
ordinary loops.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

```check
exit 0
absent "loop"
verified Larger
verified Unrelated
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: REPRODUCES, with the figures above. The workaround verifies.
