# 009: Product terms anywhere in a large context make linear goals expensive

- **Status:** open
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

## Reproducer

`repro.dfy`: interval multiplication contains every product, from a segment
lemma alone; once with raw `*` (`Segment`, `Inclusion`) and once with `*`
behind an opaque `times` (`SegmentT`, `InclusionT`). This is the isolated
form: here both are cheap, and the friction needs a large context (see
Observed). The check records the isolated behaviour, so `friction/run` reports
this case as CONTEXT-ONLY.

## Expected

A lemma that is cheap in isolation stays cheap when the surrounding module
mentions products elsewhere.

## Observed

- In isolation, `Inclusion` with raw `*` verifies at 5,227 RU (`Segment`
  7,848 RU); the opaque form costs about the same.
- Inside a larger module of terms, the identical lemma ran out of resource. So
  did bound, isotonicity and Lipschitz lemmas there.
- Putting `*` behind an opaque `times(a,b)` with a handful of law lemmas
  brought each of them to 0.05M-1.2M RU.
- Lemmas whose context held product terms from imported contracts were
  consistently the expensive ones. A sign identity on rational products cost
  3.70M RU until the rationals' value definitions were hidden; it then cost
  55K.
- Unrelated product hypotheses added to `Inclusion` in this small file
  (`u*v*w == x*u + y*v` and the like) did not raise its cost (5,625 RU), so
  one product fact is not enough; the size of the context matters.
- The module attribute `{:disableNonlinearArithmetic}` exists, but mixing
  modules with different nonlinear settings in one verifier process has given
  unreliable results before, so it was not used.

## Workaround

Opaque `times` and `recip` functions with explicit order and ring laws,
revealed only in tiny lemmas (`SegmentT`/`InclusionT` in `repro.dfy` show the
shape).

## Suggested fix

A per-declaration attribute that turns off nonlinear arithmetic for that
declaration's verification conditions (Z3 accepts `smt.arith.nl` per check).
Alternatively, encode `*` as an uninterpreted function plus ring axioms in
declarations that opt out.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

```check
context-only
exit 0
verified Inclusion
ru Inclusion <= 50000
verified InclusionT
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: CONTEXT-ONLY, with the figures above: the isolated reproducer
behaves as recorded.
