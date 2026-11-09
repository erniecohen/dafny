# 003: A recursive member predicate on a constructed term fails in a precondition

- **Status:** filed ([#198](https://github.com/erniecohen/dafny/issues/198))
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

## Reproducer

`repro.dfy`:

```dafny
datatype Op = P | M
datatype T = Var(i: nat) | Bin(op: Op, a: T, b: T) {
  predicate scoped(n: nat) {
    match this
    case Var(i) => i < n
    case Bin(_,a,b) => a.scoped(n) && b.scoped(n)
  }
}

function size(t: T, s: seq<int>): int requires t.scoped(|s|) { |s| }

function Literal(x: int, y: int): int { size(Bin(P,Var(0),Var(1)),[x,y]) }

function Variable(op: Op, x: int, y: int): int { size(Bin(op,Var(0),Var(1)),[x,y]) }
```

## Expected

Both functions are well formed: the term has depth two and its leaves are
`Var(0)` and `Var(1)`, both below `|[x,y]| == 2`.

## Observed

- Exactly one of the two fails, with "function precondition could not be
  proved", and which one moves with the surroundings:
  - On the current recheck, `Variable` fails (18,979 RU) and `Literal`
    verifies (20,059 RU).
  - When first written up (same release, slightly different option set), `Literal`
    failed (20,608 RU) and `Variable` verified (18,775 RU).
  - In the larger development, the variable form failed.
- The check below therefore only records that one of them fails.

## Workaround

`assert Var(0).scoped(2) && Var(1).scoped(2);` before the call
(`workaround.dfy`).

## Suggested fix

Same family as case 002. Unfolding of member predicates on literal receivers
interacts badly with a non-literal argument (`|[x,y]|`); unfolding guided by
the literal receiver alone would make both forms go through.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

```check
exit 4
message "function precondition could not be proved"
failed-any Literal Variable
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: REPRODUCES, with the figures above. The workaround verifies.
