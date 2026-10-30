# 008: Two individually cheap set inclusions are expensive together

- **Status:** open
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

## Reproducer

`repro.dfy` proves `x + 0 == x` for cuts given as downward-closed sets of
reals with no largest element, where addition is the image set

```dafny
ghost function add(x: iset<real>, y: iset<real>): iset<real> {
  iset a, b | a in x && b in y :: a + b
}
```

once through two inclusion lemmas (`Sub`, `Sup`, combined by `AddZeroSplit`)
and once in a single lemma `AddZero` with the same hint for the one inclusion
that needs it. The witness of openness is a function `up(s,q)` with a trigger on
it, so the hypotheses cannot loop (compare case 007).

## Expected

The single lemma costs about what the two inclusions and their combination
cost.

## Observed

- In this small file: `Sub` 15,814 RU, `Sup` 24,091 RU, `AddZeroSplit`
  19,500 RU (59,405 RU together); `AddZero` alone 162,911 RU, more than eight
  times `AddZeroSplit`.
- In the development the gap was decisive:
  - the inclusion `add(x,0) ⊆ x` alone costs 0.30M RU;
  - the inclusion `x ⊆ add(x,0)` alone costs 1.78M RU;
  - combining the two through separate lemmas costs 64K RU;
  - in one lemma, even with each inclusion scoped in `assert ... by {...}`,
    the lemma runs out of resource (more than 5M);
  - the pattern repeated for every operation's associativity and identity.

## Workaround

Make the image set opaque and give it introduction and elimination lemmas for
membership (each about 0.1M RU in the development), so that law proofs never
reveal the comprehension (`workaround.dfy`). Proving each inclusion in its own
lemma also works.

## Suggested fix

When the goal is an equality of sets or isets, emit the two inclusions as
separate assertion batches. Also consider encoding image-set comprehension
membership with a dedicated witness function, rather than a bare existential
whose instantiation Z3 must search for.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

```check
exit 0
verified AddZero
verified AddZeroSplit
ratio AddZero AddZeroSplit >= 4
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: REPRODUCES, with the figures above. The workaround verifies.
