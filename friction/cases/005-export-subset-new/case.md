# 005: Exporting a subset type loses the known `!new` characteristic

- **Status:** filed ([#200](https://github.com/erniecohen/dafny/issues/200))
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** `--type-system-refresh --general-traits=full --general-newtypes`,
  resource limit 5,000,000 per declaration (`dfyconfig.toml`)

## Reproducer

`repro.dfy`:

```dafny
module M {
  export provides T, P, Q
  datatype D = D(s: iset<int>)
  type T = d: D | d.s != iset{} witness *
  ghost predicate Q(F: iset<T>, b: T) { forall x | x in F :: x.s <= b.s }
  ghost predicate P(F: iset<T>) requires exists b :: Q(F,b) { true }
}
```

## Expected

`T` contains no references, so the export set is consistent.

## Observed

Raised while checking the export set (without the export it resolves):

- "a exists expression involved in a predicate definition is not allowed to
  depend on the set of allocated references, but values of 'b' (of type 'T')
  may contain references (perhaps declare its type as 'T(!new)')"
- "This export set is not consistent: M"

## Workaround

Declare `type T(!new) = ...` (`workaround.dfy`).

A second, smaller rough edge shows up once that is done: with
`--type-system-refresh`, the bound variable of `exists b :: Q(F,b)` is
inferred as `D`, not `T`, and the call `Q(F,b)` then fails verification with
"value does not satisfy the subset constraints of 'T'". The workaround
therefore also writes `exists b: T :: Q(F,b)`.

## Suggested fix

When a type is provided opaquely, export its inferred characteristics (`!new`,
and equality support where known). Alternatively, check the export against
the characteristics the definition actually has.

## Source

A downstream development: Dedekind cuts with every arithmetic operation obtained
by one generic lifting of rational functions.

## Check

```check
exit 2
message "may contain references (perhaps declare its type as 'T(!new)')"
message "This export set is not consistent: M"
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: REPRODUCES, with the figures above. The workaround verifies.
